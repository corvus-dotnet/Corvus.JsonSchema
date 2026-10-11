(* The compiler of the backtracking matcher. This is the port of compile.go of the ecmaregex package of the Go
   module. *)

open Parse

type opcode =
  | Op_char
      (** [Op_char] and [Op_set] consume one code point going forward. The back forms consume the one before the
          position, which is how the body of a lookbehind runs. *)
  | Op_char_back
  | Op_set
  | Op_set_back
  | Op_lit  (** Consumes a run of literal bytes going forward. *)
  | Op_fail
  | Op_bol
  | Op_eol
  | Op_word_boundary
  | Op_not_word_boundary
  | Op_line_start
      (** [Op_line_start] and [Op_line_end] are [^] and [$] with the m modifier. The fold forms of the word boundaries
          and of the backreferences belong to a case-insensitive group. *)
  | Op_line_end
  | Op_word_boundary_fold
  | Op_not_word_boundary_fold
  | Op_jmp
  | Op_split  (** Continues at [x] and leaves [y] as the alternative. *)
  | Op_save_tmp  (** Notes where a group was entered. [Op_capture] records the group once it has closed. *)
  | Op_capture
  | Op_clear  (** Resets the groups [a] to [b], as ECMA-262 does at the start of each iteration of a quantified body. *)
  | Op_backref
  | Op_backref_back
  | Op_backref_fold
  | Op_backref_fold_back
  | Op_backref_units
      (** [Op_backref_units] and [Op_backref_units_back] compare a group a code unit at a time, for a pattern matched by
          code unit. *)
  | Op_backref_units_back
  | Op_look
      (** Opens a lookaround. [Op_look_ok] ends the body of a positive one and [Op_look_fail] the body of a negative
          one. *)
  | Op_look_ok
  | Op_look_fail
  | Op_rep  (** [Op_rep] and [Op_rep_lazy] repeat one character or class without entering the general loop. *)
  | Op_rep_lazy
  | Op_loop_init  (** [Op_loop_init], [Op_loop] and [Op_loop_iter] run a counted or possibly empty quantified body. *)
  | Op_loop
  | Op_loop_iter
  | Op_match

type inst = {
  op : opcode;
  flag : bool;
      (** Marks a lazy [Op_loop], a backward [Op_capture], a negative [Op_look], and an [Op_backref_fold] that folds
          case as the u flag grammar does. *)
  r : int;
  set : Charset.t;
  has_set : bool;  (** Says [set] is in use, where the Go source tests the set for nil. *)
  lit : string;
  mutable x : int;  (** [x] and [y] are jump targets. *)
  mutable y : int;
  a : int;  (** [a] and [b] are a group, a loop or a range of groups, depending on the instruction. *)
  b : int;
  min : int;  (** [min] and [max] bound a repetition. A max of -1 is no bound. *)
  max : int;
}
(** One instruction of the backtracking matcher. The Go source holds the jump targets, the groups and the bounds in 32
    bits. Here each is an [int]. *)

let no_inst =
  { op = Op_fail; flag = false; r = 0; set = Charset.none; has_set = false; lit = ""; x = 0; y = 0; a = 0; b = 0; min = 0; max = 0 }
[@@ocamlformat "disable"]

type program = {
  insts : inst array;
  groups : int;  (** The number of capturing groups some backreference names. *)
  loops : int;  (** The number of counted loops. *)
  units : bool;
      (** Says the pattern was read without the u flag, so the text is matched by UTF-16 code unit (see utf8.ml). *)
  anchored : bool;  (** Says every match starts at the start of the text. *)
  prefix : string;  (** Literal text every match starts with. *)
  first : Charset.t;
      (** Every code point a match can start with. It is in use when [has_first] is set, which it is not when a match
          can be empty or when the set is not known. *)
  has_first : bool;
}
(** A pattern compiled for the backtracking matcher. Nothing writes to it once it is made. The Go source keeps a pool of
    working memory in the program. Here the working memory belongs to the caller of the match (see vm.ml). *)

type compiler = {
  mutable code : inst array;  (** The instructions, of which the first [count] are in use. *)
  mutable count : int;
  tracked : int array;
      (** The slot of each capturing group by its number, for the groups some backreference names, and -1 for the
          others, which are not recorded at all. *)
  mutable loop_count : int;
  unicode_mode : bool;
      (** Says the pattern was read with the u flag grammar, which decides how a backreference folds case. *)
}

let rec collect_backrefs n seen =
  if n.kind = Backref then Array.iter (fun g -> seen.(g) <- true) n.groups;
  Array.iter (fun sub -> collect_backrefs sub seen) n.subs

let add c inst =
  if c.count = Array.length c.code then begin
    let grown = Array.make ((2 * c.count) + 16) no_inst in
    Array.blit c.code 0 grown 0 c.count;
    c.code <- grown
  end;
  c.code.(c.count) <- inst;
  c.count <- c.count + 1;
  c.count - 1

let pc c = c.count
let slot c group = if group > 0 && group < Array.length c.tracked then c.tracked.(group) else -1

(* Returns the first and last slot of the tracked groups inside n, or (-1, -1) when it has none. *)
let group_range c n =
  let lo = ref (-1) and hi = ref (-1) in
  let rec walk n =
    if n.kind = Group && n.group > 0 then begin
      let t = slot c n.group in
      if t >= 0 then begin
        if !lo < 0 || t < !lo then lo := t;
        if t > !hi then hi := t
      end
    end;
    Array.iter walk n.subs
  in
  walk n;
  (!lo, !hi)

(** Reports whether a code point can be matched by comparing its UTF-8 bytes. A surrogate has no encoding, and U+FFFD is
    also what a malformed byte of the text decodes to. *)
let literal_rune r = Utf8.valid_rune r && r <> Utf8.rune_error

(** Reports whether n can match without consuming anything. It errs towards true. *)
let rec nullable n =
  match n.kind with
  | Char | Set -> false
  | Cat -> Array.for_all nullable n.subs
  | Alt -> Array.exists nullable n.subs
  | Group -> nullable n.subs.(0)
  | Repeat -> n.min = 0 || nullable n.subs.(0)
  | _ -> true

(* Writes the instructions for n. With back set the instructions match right to left, as the body of a lookbehind
   must. *)
let rec emit c n back =
  match n.kind with
  | Empty -> ()
  | Char -> ignore (add c { no_inst with op = (if back then Op_char_back else Op_char); r = n.r })
  | Set ->
      if Charset.is_empty n.set then ignore (add c { no_inst with op = Op_fail })
      else ignore (add c { no_inst with op = (if back then Op_set_back else Op_set); set = n.set; has_set = true })
  | Cat ->
      if back then
        for i = Array.length n.subs - 1 downto 0 do
          emit c n.subs.(i) true
        done
      else begin
        let count = Array.length n.subs in
        let i = ref 0 in
        while !i < count do
          (* A run of literal characters becomes one comparison of bytes. *)
          let j = ref !i in
          let lit = Buffer.create 16 in
          while !j < count && n.subs.(!j).kind = Char && literal_rune n.subs.(!j).r do
            Utf8.add_rune lit n.subs.(!j).r;
            incr j
          done;
          if !j - !i >= 2 then begin
            ignore (add c { no_inst with op = Op_lit; lit = Buffer.contents lit });
            i := !j
          end
          else begin
            emit c n.subs.(!i) false;
            incr i
          end
        done
      end
  | Alt ->
      let jumps = ref [] in
      let last = Array.length n.subs - 1 in
      Array.iteri
        (fun i sub ->
          if i = last then emit c sub back
          else begin
            let split = add c { no_inst with op = Op_split } in
            c.code.(split).x <- pc c;
            emit c sub back;
            jumps := add c { no_inst with op = Op_jmp } :: !jumps;
            c.code.(split).y <- pc c
          end)
        n.subs;
      List.iter (fun j -> c.code.(j).x <- pc c) !jumps
  | Group ->
      let t = slot c n.group in
      if t < 0 then emit c n.subs.(0) back
      else begin
        ignore (add c { no_inst with op = Op_save_tmp; a = t });
        emit c n.subs.(0) back;
        ignore (add c { no_inst with op = Op_capture; a = t; flag = back })
      end
  | Bol -> ignore (add c { no_inst with op = Op_bol })
  | Eol -> ignore (add c { no_inst with op = Op_eol })
  | Line_start -> ignore (add c { no_inst with op = Op_line_start })
  | Line_end -> ignore (add c { no_inst with op = Op_line_end })
  | Word_boundary -> ignore (add c { no_inst with op = (if n.fold then Op_word_boundary_fold else Op_word_boundary) })
  | Not_word_boundary ->
      ignore (add c { no_inst with op = (if n.fold then Op_not_word_boundary_fold else Op_not_word_boundary) })
  | Backref ->
      let op =
        if n.fold && back then Op_backref_fold_back
        else if n.fold then Op_backref_fold
        else if back then if c.unicode_mode then Op_backref_back else Op_backref_units_back
        else if c.unicode_mode then Op_backref
        else Op_backref_units
      in
      (* When several groups share the name, at most one has taken part, and the others match the empty string. So
         matching each in turn matches the one that took part. *)
      Array.iter (fun g -> ignore (add c { no_inst with op; a = slot c g; flag = c.unicode_mode })) n.groups
  | Look ->
      let look = add c { no_inst with op = Op_look; flag = n.negative } in
      emit c n.subs.(0) n.behind;
      ignore (add c { no_inst with op = (if n.negative then Op_look_fail else Op_look_ok) });
      c.code.(look).x <- pc c
  | Repeat -> emit_repeat c n back

and emit_repeat c n back =
  let body = n.subs.(0) in
  if n.max <> 0 then
    if (not back) && (body.kind = Char || (body.kind = Set && not (Charset.is_empty body.set))) then
      ignore
        (add c
           {
             no_inst with
             op = (if n.lazy_ then Op_rep_lazy else Op_rep);
             r = body.r;
             set = body.set;
             has_set = body.kind = Set;
             min = n.min;
             max = n.max;
           })
    else begin
      let clear_lo, clear_hi = group_range c body in
      let emit_body () =
        if clear_lo >= 0 then ignore (add c { no_inst with op = Op_clear; a = clear_lo; b = clear_hi });
        emit c body back
      in
      (* split writes a choice between entering the body and leaving the loop, in the order the quantifier prefers. *)
      let split () = add c { no_inst with op = Op_split } in
      let set_split at enter leave =
        if n.lazy_ then begin
          c.code.(at).x <- leave;
          c.code.(at).y <- enter
        end
        else begin
          c.code.(at).x <- enter;
          c.code.(at).y <- leave
        end
      in
      (* A body that always consumes something needs no check for empty iterations, so the unbounded forms and the
         optional form are plain choices. *)
      let plain = not (nullable body) in
      if plain && n.min = 0 && n.max = unbounded then begin
        let at = split () in
        let enter = pc c in
        emit_body ();
        ignore (add c { no_inst with op = Op_jmp; x = at });
        set_split at enter (pc c)
      end
      else if plain && n.min = 1 && n.max = unbounded then begin
        let enter = pc c in
        emit_body ();
        let at = split () in
        set_split at enter (pc c)
      end
      else if plain && n.min = 0 && n.max = 1 then begin
        let at = split () in
        let enter = pc c in
        emit_body ();
        set_split at enter (pc c)
      end
      else begin
        let k = c.loop_count in
        c.loop_count <- c.loop_count + 1;
        ignore (add c { no_inst with op = Op_loop_init; a = k });
        let loop = add c { no_inst with op = Op_loop; a = k; min = n.min; max = n.max; flag = n.lazy_ } in
        ignore (add c { no_inst with op = Op_loop_iter; a = k });
        emit_body ();
        ignore (add c { no_inst with op = Op_jmp; x = loop });
        c.code.(loop).x <- pc c
      end
    end

(* Reports whether every way through n begins with ^. *)
let rec starts_at_bol n =
  match n.kind with
  | Bol -> true
  | Cat -> starts_at_bol n.subs.(0)
  | Alt -> Array.for_all starts_at_bol n.subs
  | Group -> starts_at_bol n.subs.(0)
  | Repeat -> n.min >= 1 && starts_at_bol n.subs.(0)
  | _ -> false

(* Appends the literal text every match of n starts with. *)
let rec literal_prefix n prefix =
  match n.kind with
  | Char -> if literal_rune n.r then Utf8.add_rune prefix n.r
  | Cat ->
      let rec each i =
        if i < Array.length n.subs then
          let sub = n.subs.(i) in
          if sub.kind <> Char || not (literal_rune sub.r) then
            begin if Buffer.length prefix = 0 && sub.kind = Group then literal_prefix sub prefix
            end
          else begin
            Utf8.add_rune prefix sub.r;
            each (i + 1)
          end
      in
      each 0
  | Group -> literal_prefix n.subs.(0) prefix
  | _ -> ()

exception Unknown_first

(* Appends, as inclusive pairs, the code points a match of n can start with, and reports whether n can match the
   empty string. It raises Unknown_first when the set is not known. Assertions are treated as matching the empty
   string, which can only make the set larger than it need be. *)
let rec first_chars n pairs =
  match n.kind with
  | Char ->
      pairs := [| n.r; n.r |] :: !pairs;
      false
  | Set ->
      pairs := n.set.ranges :: !pairs;
      false
  | Cat ->
      let rec each i =
        if i = Array.length n.subs then true else if first_chars n.subs.(i) pairs then each (i + 1) else false
      in
      each 0
  | Alt -> Array.fold_left (fun empty sub -> first_chars sub pairs || empty) false n.subs
  | Group -> first_chars n.subs.(0) pairs
  | Repeat ->
      if n.max = 0 then true
      else
        let empty = first_chars n.subs.(0) pairs in
        empty || n.min = 0
  | Backref -> raise Unknown_first
  | _ -> true

(** Compiles the tree the parser read for the backtracking matcher. [group_count] is the number of capturing groups of
    the pattern. *)
let compile_program root group_count unicode_mode =
  let seen = Array.make (group_count + 1) false in
  collect_backrefs root seen;
  (* Slots follow group order, so the groups inside any one subtree take a contiguous range of slots. *)
  let tracked = Array.make (group_count + 1) (-1) in
  let slots = ref 0 in
  for g = 1 to group_count do
    if seen.(g) then begin
      tracked.(g) <- !slots;
      incr slots
    end
  done;
  let c = { code = [||]; count = 0; tracked; loop_count = 0; unicode_mode } in
  emit c root false;
  ignore (add c { no_inst with op = Op_match });
  let anchored = starts_at_bol root in
  let prefix = Buffer.create 16 in
  if not anchored then literal_prefix root prefix;
  let first =
    if anchored || Buffer.length prefix > 0 then None
    else
      let pairs = ref [] in
      match first_chars root pairs with
      | false -> Some (Charset.make (Array.concat !pairs) false)
      | true -> None
      | exception Unknown_first -> None
  in
  {
    insts = Array.sub c.code 0 c.count;
    groups = !slots;
    loops = c.loop_count;
    units = not unicode_mode;
    anchored;
    prefix = Buffer.contents prefix;
    first = (match first with Some set -> set | None -> Charset.none);
    has_first = first <> None;
  }
