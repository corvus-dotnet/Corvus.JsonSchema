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

type quick = {
  ascii : Bytes.t;
      (** The characters below 128 a way can consume next, a bit for each in 16 bytes. Nothing writes to it once
          {!set_quick} has returned. *)
  other : bool;  (** Says a way can consume a character that is not ASCII. *)
  mid : bool;  (** Says a way reaches the end of its match consuming nothing more while text is left. *)
  at_end : bool;  (** Says a way does at the end of the text. *)
}
(** What can come next on a way from an instruction to the end of its match, which is the end of the pattern or of the
    lookaround the instruction is in. The matcher looks at it before it takes a way: when the next character of the text
    is none that the way can start with, and the way cannot end without one, the way fails, so it is not taken, and
    where it was one of two no choice is kept for it. V8 makes the same check ("quick check") at its choices. It is what
    keeps a choice between ways that need different characters from being a choice. *)

let quick_none = { ascii = Bytes.make 16 '\000'; other = false; mid = false; at_end = false }
let quick_all = { ascii = Bytes.make 16 '\255'; other = true; mid = true; at_end = true }

(** The number of loops whose state a memo point can tell apart. *)
let max_memo_loops = 2

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
  mutable need : int;
      (** The least number of bytes of text that every way from this instruction to a match consumes going forward (see
          {!set_needs}). It is set for an [Op_loop], and counts none of the iterations the loop has still to make. *)
  iter : int;  (** The least number of bytes one iteration of the body of an [Op_loop] consumes going forward. *)
  mutable q : quick;  (** What can come next on a way from the instruction (see {!set_quick}). *)
  m : int;
      (** The number of the instruction's memo point, counted from 1, or 0 when it has none. It is set for an
          [Op_split], an [Op_loop], an [Op_rep] and an [Op_rep_lazy].

          A memo point is a place where the matcher makes a choice and can remember that the choice has failed: a split,
          a loop that may be entered or left, and what follows a single character loop that gives characters back or
          takes more. Whether a way from such a place leads to a match depends on the place and the position alone when
          nothing else the matcher keeps can change what follows. So a pattern with a backreference has no memo points
          (what a group holds changes what follows), and neither has a place inside a loop that counts its iterations up
          to a bound. Inside a loop with no upper bound and a least count of zero or one, one thing more decides what
          follows: whether the loop would refuse the iteration as empty if it ended here. That is one bit for each such
          loop the place is in, and a place in more than [max_memo_loops] of them has no memo point. *)
  ml0 : int;
      (** [ml0] and [ml1] are the loops around the memo point, outermost first, or -1, and [mm0] and [mm1] the least
          count of each. *)
  ml1 : int;
  mm0 : int;
  mm1 : int;
}
(** One instruction of the backtracking matcher. The Go source holds the jump targets, the groups and the bounds in 32
    bits. Here each is an [int]. *)

let no_inst =
  { op = Op_fail; flag = false; r = 0; set = Charset.none; has_set = false; lit = ""; x = 0; y = 0; a = 0; b = 0; min = 0; max = 0; need = 0; iter = 0; q = quick_none; m = 0; ml0 = -1; ml1 = -1; mm0 = 0; mm1 = 0 }
[@@ocamlformat "disable"]

type program = {
  insts : inst array;
  groups : int;  (** The number of capturing groups some backreference names. *)
  loops : int;  (** The number of counted loops. *)
  units : bool;
      (** Says the pattern was read without the u flag, so the text is matched by UTF-16 code unit (see utf8.ml). *)
  anchored : bool;  (** Says every match starts at the start of the text. *)
  need : int;  (** The least number of bytes a match consumes (see {!set_needs}). *)
  need_passes : int;  (** The passes over the instructions that {!set_needs} made, the last of which changed nothing. *)
  points : int;  (** The number of memo points of the program (see [m] of an instruction). *)
  quick_passes : int;
      (** The passes over the instructions that {!set_quick} made, the last of which changed nothing. *)
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
  memo : bool;  (** Says the pattern has no backreference, so its choices can have memo points. *)
  mutable open_loops : (int * int * bool) list;
      (** The counted loops around the instruction being written, innermost first, inside the innermost lookaround (what
          a lookaround's body does depends on nothing outside it): the number of each, its least count, and whether its
          state is one bit to a memo point (no upper bound, and a least count of zero or one). *)
  mutable point_count : int;
}

(* Returns an instruction with the number of a new memo point for a place here, or the instruction as it is when a
   choice here cannot be remembered. *)
let with_point c inst =
  let loops = List.rev c.open_loops in
  if (not c.memo) || List.length loops > max_memo_loops || List.exists (fun (_, _, simple) -> not simple) loops then
    inst
  else begin
    c.point_count <- c.point_count + 1;
    let loop k = match List.nth_opt loops k with Some (l, _, _) -> l | None -> -1 in
    let least k = match List.nth_opt loops k with Some (_, m, _) -> m | None -> 0 in
    { inst with m = c.point_count; ml0 = loop 0; ml1 = loop 1; mm0 = least 0; mm1 = least 1 }
  end

(** Reports whether n can match one character or more. It errs towards true. *)
let rec consumes n =
  match n.kind with
  | Char | Set | Backref -> true
  | Cat | Alt | Group -> Array.exists consumes n.subs
  | Repeat -> n.max <> 0 && consumes n.subs.(0)
  | _ -> false

(** [simplify n inside] returns the tree with two rewrites, both of which V8 makes. Neither changes what the pattern
    matches or what any group holds. Without them a pattern can take time exponential in its counts for no reason that
    ECMA-262 gives. [inside] is the capturing groups n is inside of. The tree is a value nothing writes to, so a node
    that changes is a new node.

    A backreference to a group it is inside of is the empty string. The group is not set until it closes, and the
    repetition around a group clears it at the start of each iteration, so inside the group it is never set.

    A repetition whose body cannot consume a character, such as [(?:\b|(?=a)|){4,5}], is the body once when its minimum
    is one or more, and nothing when its minimum is zero. Every iteration of such a body is empty. ECMA-262 refuses an
    empty iteration after the minimum, so the iterations are exactly the minimum. Each of them starts with the groups of
    the body cleared and at the same position, so what the last one can do does not depend on the ones before it, and
    the ways through the last one come in the same order whatever the others chose. Run as written, the matcher would
    try every combination of the ways through every iteration. *)
let rec simplify n inside =
  if n.kind = Backref then if Array.for_all (fun g -> List.mem g inside) n.groups then empty_node else n
  else if Array.length n.subs = 0 then n
  else
    let inside = if n.kind = Group && n.group > 0 then n.group :: inside else inside in
    let subs = Array.map (fun sub -> simplify sub inside) n.subs in
    let out = if Array.for_all2 ( == ) subs n.subs then n else { n with subs } in
    if out.kind = Repeat && out.max <> 0 && not (consumes out.subs.(0)) then
      if out.min = 0 then empty_node else out.subs.(0)
    else out

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

(** The need of an instruction from which no way leads to a match. Every need is capped at it. *)
let no_match = 1 lsl 30

(** Returns count times need, capped at [no_match]. *)
let times_need count need =
  if count = 0 || need = 0 then 0 else if need >= no_match || count >= no_match / need then no_match else count * need

(** Returns the least number of bytes a match of n consumes, capped at [no_match]. Like the needs, it counts a character
    as its UTF-8 bytes when the matcher compares bytes for it and as one byte otherwise, so it can only be too small. *)
let rec min_length n =
  match n.kind with
  | Char -> if literal_rune n.r then Utf8.rune_length n.r else 1
  | Set -> if Charset.is_empty n.set then no_match else 1
  | Cat ->
      let rec sum i total =
        if i = Array.length n.subs then total
        else
          let total = total + min_length n.subs.(i) in
          if total >= no_match then no_match else sum (i + 1) total
      in
      sum 0 0
  | Alt -> Array.fold_left (fun least sub -> min least (min_length sub)) no_match n.subs
  | Group -> min_length n.subs.(0)
  | Repeat -> times_need n.min (min_length n.subs.(0))
  | _ -> 0

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
            let split = add c (with_point c { no_inst with op = Op_split }) in
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
      (* Whether the body matches depends on nothing outside it. *)
      let outer = c.open_loops in
      c.open_loops <- [];
      emit c n.subs.(0) n.behind;
      c.open_loops <- outer;
      ignore (add c { no_inst with op = (if n.negative then Op_look_fail else Op_look_ok) });
      c.code.(look).x <- pc c
  | Repeat -> emit_repeat c n back

and emit_repeat c n back =
  let body = n.subs.(0) in
  if n.max <> 0 then
    if (not back) && (body.kind = Char || (body.kind = Set && not (Charset.is_empty body.set))) then
      ignore
        (add c
           (with_point c
              {
                no_inst with
                op = (if n.lazy_ then Op_rep_lazy else Op_rep);
                r = body.r;
                set = body.set;
                has_set = body.kind = Set;
                min = n.min;
                max = n.max;
              }))
    else begin
      let clear_lo, clear_hi = group_range c body in
      let emit_body () =
        if clear_lo >= 0 then ignore (add c { no_inst with op = Op_clear; a = clear_lo; b = clear_hi });
        emit c body back
      in
      (* split writes a choice between entering the body and leaving the loop, in the order the quantifier prefers. *)
      let split () = add c (with_point c { no_inst with op = Op_split }) in
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
        let iter = if back then 0 else min_length body in
        let simple = n.max = unbounded && n.min <= 1 in
        let loop_inst = { no_inst with op = Op_loop; a = k; min = n.min; max = n.max; flag = n.lazy_; iter } in
        (* The loop is left or entered from here, and what follows either depends on the loops around this one. *)
        let loop = add c (if simple then with_point c loop_inst else loop_inst) in
        ignore (add c { no_inst with op = Op_loop_iter; a = k });
        c.open_loops <- (k, n.min, simple) :: c.open_loops;
        emit_body ();
        c.open_loops <- List.tl c.open_loops;
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

(** [set_needs insts] works out, for every instruction, the least number of bytes of text that any way from it to a
    match consumes going forward, and writes it into the counted loops. It returns the need of the whole program, and
    the number of passes it made. The matcher gives up at a counted loop when fewer bytes than that are left, as V8 does
    at every choice. A loop whose iterations can be empty would otherwise try every combination of its ways at a
    position from which the rest of the pattern cannot match at all.

    A counted loop that is entered needs its least count of iterations, each of them the least its body consumes
    ([iter]), and then what follows it. The loop instruction itself is also reached after each iteration, so its own
    need counts only what follows, and the matcher adds the iterations it has still to make.

    A character is counted as one byte, a literal run as its bytes and a single character loop as its minimum, so the
    figure can only be too small. The body of a lookaround ends at its own last instruction and the instruction that
    opens one goes straight to what follows it, so a body is counted by itself. An instruction that matches right to
    left, which is the body of a lookbehind, counts nothing. *)
let set_needs insts =
  let count = Array.length insts in
  let need = Array.make count no_match in
  (* The needs only fall, and nearly every way goes forward, so going through the instructions from the last to the
     first settles them in one pass, and a second pass finds nothing to change. The one way that goes back is the jump
     at the end of the body of a counted loop, to the loop instruction. The need of that instruction is the need of
     what follows the loop, since the other way from it, through the body, comes back to it and cannot need less. So
     the jump takes the need of what follows the loop, which this pass has settled already. The jump at the end of the
     body of a plain loop goes back to a choice between the body and what follows, and takes the lesser of the two in
     the same way. Taking the need of the
     loop instruction, which this pass has not reached, would settle one loop of a nest for each pass: a thousand
     passes for loops a thousand deep. *)
  let next target weight = need.(target) + weight in
  let changed = ref true and passes = ref 0 in
  while !changed do
    changed := false;
    incr passes;
    for pc = count - 1 downto 0 do
      let i = insts.(pc) in
      let n =
        match i.op with
        | Op_match | Op_look_ok | Op_look_fail -> 0
        | Op_fail -> no_match
        | Op_char | Op_set -> next (pc + 1) 1
        | Op_lit -> next (pc + 1) (String.length i.lit)
        | Op_rep | Op_rep_lazy -> next (pc + 1) i.min
        | Op_jmp when i.x < pc && insts.(i.x).op = Op_loop -> next insts.(i.x).x 0
        | Op_jmp when i.x < pc && insts.(i.x).op = Op_split -> min (next insts.(i.x).x 0) (next insts.(i.x).y 0)
        | Op_jmp | Op_look -> next i.x 0
        | Op_split -> min (next i.x 0) (next i.y 0)
        | Op_loop -> min (next (pc + 1) 0) (next i.x 0)
        | Op_loop_init ->
            let loop = insts.(pc + 1) in
            next (pc + 1) (times_need loop.min loop.iter)
        | _ -> next (pc + 1) 0
      in
      let n = min n no_match in
      if n < need.(pc) then begin
        need.(pc) <- n;
        changed := true
      end
    done
  done;
  Array.iteri (fun pc i -> if i.op = Op_loop then i.need <- need.(pc)) insts;
  (need.(0), !passes)

let quick_union a b =
  {
    ascii = Bytes.init 16 (fun k -> Char.chr (Char.code (Bytes.get a.ascii k) lor Char.code (Bytes.get b.ascii k)));
    other = a.other || b.other;
    mid = a.mid || b.mid;
    at_end = a.at_end || b.at_end;
  }

let quick_inter a b =
  {
    ascii = Bytes.init 16 (fun k -> Char.chr (Char.code (Bytes.get a.ascii k) land Char.code (Bytes.get b.ascii k)));
    other = a.other && b.other;
    mid = a.mid && b.mid;
    at_end = a.at_end && b.at_end;
  }

let quick_rune r =
  if r < Utf8.rune_self then begin
    let ascii = Bytes.make 16 '\000' in
    Bytes.set ascii (r lsr 3) (Char.chr (1 lsl (r land 7)));
    { quick_none with ascii }
  end
  else { quick_none with other = true }

let quick_set (set : Charset.t) =
  let n = Array.length set.ranges in
  { quick_none with ascii = Bytes.of_string set.ascii; other = n > 0 && set.ranges.(n - 1) >= Utf8.rune_self }

let quick_equal a b = Bytes.equal a.ascii b.ascii && a.other = b.other && a.mid = b.mid && a.at_end = b.at_end

(** [set_quick insts] works out the quick of every instruction, and returns the number of passes it made. Every figure
    is a set that can only be too large: an assertion is taken to hold, a backreference and an instruction that matches
    right to left (the body of a lookbehind) to allow anything. A positive lookahead needs both its body and what
    follows it to go on from the same position, so it allows what both allow. *)
let set_quick insts =
  let count = Array.length insts in
  (* The sets only grow, and nearly every way goes forward, so the instructions are gone through from the last to the
     first until a pass changes nothing. The one way that goes back is the jump at the end of the body of a loop, to
     the instruction at its head, which the pass has not reached. Taking what that instruction had after the pass
     before would settle one loop of a nest for each pass: a thousand passes for loops a thousand deep. The head of a
     loop allows what follows the loop, which this pass has settled, and what its body starts with. So the jump takes
     what follows the loop too. Then the first pass settles the head of every loop, since all it still lacks at the
     jump is a part of itself, the second settles every instruction, and the third finds nothing to change, whatever
     the depth. *)
  let changed = ref true and passes = ref 0 in
  while !changed do
    changed := false;
    incr passes;
    for pc = count - 1 downto 0 do
      let i = insts.(pc) in
      let q k = insts.(k).q in
      let n =
        match i.op with
        | Op_match | Op_look_ok | Op_look_fail -> quick_all
        | Op_fail -> quick_none
        | Op_char -> quick_rune i.r
        | Op_set -> quick_set i.set
        | Op_lit -> quick_rune (Char.code (String.get i.lit 0))
        | Op_rep | Op_rep_lazy ->
            let own = if i.has_set then quick_set i.set else quick_rune i.r in
            if i.min = 0 then quick_union own (q (pc + 1)) else own
        | Op_jmp when i.x < pc && insts.(i.x).op = Op_loop -> quick_union (q i.x) (q insts.(i.x).x)
        | Op_jmp when i.x < pc && insts.(i.x).op = Op_split ->
            quick_union (q i.x) (quick_union (q insts.(i.x).x) (q insts.(i.x).y))
        | Op_jmp -> q i.x
        | Op_split -> quick_union (q i.x) (q i.y)
        | Op_loop -> quick_union (q (pc + 1)) (q i.x)
        | Op_eol -> { quick_none with at_end = (q (pc + 1)).at_end }
        | Op_look -> if i.flag then q i.x else quick_inter (q i.x) (q (pc + 1))
        | Op_char_back | Op_set_back | Op_backref | Op_backref_back | Op_backref_fold | Op_backref_fold_back
        | Op_backref_units | Op_backref_units_back ->
            quick_all
        | _ -> q (pc + 1)
      in
      if not (quick_equal n i.q) then begin
        i.q <- n;
        changed := true
      end
    done
  done;
  !passes

(** The most bytes of the literal text a match starts with that the search for a match looks for. *)
let max_prefix = 16

(** Compiles the tree the parser read for the backtracking matcher. [group_count] is the number of capturing groups of
    the pattern. *)
let compile_program root group_count unicode_mode =
  let root = simplify root [] in
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
  let c =
    {
      code = [||];
      count = 0;
      tracked;
      loop_count = 0;
      unicode_mode;
      memo = !slots = 0;
      open_loops = [];
      point_count = 0;
    }
  in
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
  let insts = Array.sub c.code 0 c.count in
  let need, need_passes = set_needs insts in
  let quick_passes = set_quick insts in
  {
    insts;
    groups = !slots;
    loops = c.loop_count;
    units = not unicode_mode;
    anchored;
    need;
    need_passes;
    points = c.point_count;
    quick_passes;
    (* The search looks for the prefix at every position of the text, a byte at a time, so only its first bytes are
       used: a long prefix that nearly matches everywhere would cost its length at each position, outside the steps
       the matcher counts. The matcher compares the rest when it runs. *)
    prefix = Buffer.sub prefix 0 (min (Buffer.length prefix) max_prefix);
    first = (match first with Some set -> set | None -> Charset.none);
    has_first = first <> None;
  }
