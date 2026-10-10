(* The backtracking matcher. This is the port of vm.go of the ecmaregex package of the Go module.

   What differs from the Go source:

   The text is a range of a [Bytes.t] or of a [string], from [lo] up to [hi], where Go has a slice. The start of the
   text is [lo] and its end is [hi], and nothing outside them is read.

   Go keeps the working memory of a match in a pool of the program. Here it is a {!state} the caller gives, so that a
   program is a value nothing writes to. The interface of the library says who owns the state of each entry point.

   The loop of the Go source is two functions here that call each other in tail position, [step] and [backtrack].
   Every value they pass is an [int] or a pointer that already exists, so a match allocates only when the stack
   grows. A decoded character is one [int] (see utf8.ml), and an entry of the stack is four elements of an [int
   array], where Go has a slice of structures. *)

open Compile

(* The kinds of entry on the backtracking stack. A choice entry says where to resume. An undo entry restores a value
   that was overwritten after the entries below it were pushed. *)

(** Resumes at pc with the position pos. *)
let e_choice = 0

(** Belongs to a greedy single character loop that stopped at pos. Resuming gives back one character, down to the
    position a. *)
let e_rep = 1

(** Belongs to a lazy single character loop that has taken a characters and stands at pos. Resuming takes one more. *)
let e_rep_lazy = 2

(** [e_look] marks the start of a positive lookaround and [e_look_neg] of a negative one. Both hold the position to
    restore, and [e_look_neg] the instruction to continue at when its body has failed. *)
let e_look = 3

let e_look_neg = 4

(** Restores the entry position of group pc to pos. *)
let e_tmp = 5

(** Restores the start and end of group pc to pos and a. *)
let e_cap = 6

(** Restores the count and the iteration start of loop pc to pos and a. *)
let e_counter = 7

type state = {
  text : Utf8.text;
  mutable stack : int array;
      (** The entries, four elements each: the kind, pc, pos and a. The first [sp] elements are in use. *)
  mutable sp : int;
  mutable caps : int array;  (** The start and end of each tracked group, or -1. *)
  mutable tmp : int array;  (** The position at which each tracked group was entered. *)
  mutable counts : int array;
      (** For each loop, the number of iterations started and the position at which the last one started. *)
}
(** The working memory of one match. A match allocates only while the stack is still growing to the depth the pattern
    and the text need. *)

(** The largest stack kept between matches, in entries. *)
let max_retained_stack = 1 lsl 16

let new_state () = { text = Utf8.new_text (); stack = [||]; sp = 0; caps = [||]; tmp = [||]; counts = [||] }
let byte (t : Utf8.text) i = if t.is_string then Char.code (String.get t.s i) else Char.code (Bytes.get t.b i)

let grow st =
  let grown = Array.make ((2 * Array.length st.stack) + 256) 0 in
  Array.blit st.stack 0 grown 0 st.sp;
  st.stack <- grown

let push st kind pc pos a =
  if st.sp + 4 > Array.length st.stack then grow st;
  let stack = st.stack and sp = st.sp in
  stack.(sp) <- kind;
  stack.(sp + 1) <- pc;
  stack.(sp + 2) <- pos;
  stack.(sp + 3) <- a;
  st.sp <- sp + 4

(* Applies the undo entry at the element [e] of the stack. *)
let undo st e =
  let stack = st.stack in
  let kind = stack.(e) and pc = stack.(e + 1) and pos = stack.(e + 2) and a = stack.(e + 3) in
  if kind = e_tmp then st.tmp.(pc) <- pos
  else if kind = e_cap then begin
    st.caps.(2 * pc) <- pos;
    st.caps.((2 * pc) + 1) <- a
  end
  else if kind = e_counter then begin
    st.counts.(2 * pc) <- pos;
    st.counts.((2 * pc) + 1) <- a
  end

(* Reports whether the character before the position is an ECMA-262 line terminator, and line_terminator_at whether
   the character at the position is one. U+2028 and U+2029 are E2 80 A8 and E2 80 A9. *)
let line_terminator_before t lo pos =
  let c = byte t (pos - 1) in
  if c = 0x0A || c = 0x0D then true
  else if c = 0xA8 || c = 0xA9 then pos - lo >= 3 && byte t (pos - 3) = 0xE2 && byte t (pos - 2) = 0x80
  else false

let line_terminator_at t pos hi =
  let c = byte t pos in
  if c = 0x0A || c = 0x0D then true
  else if c = 0xE2 then pos + 2 < hi && byte t (pos + 1) = 0x80 && (byte t (pos + 2) = 0xA8 || byte t (pos + 2) = 0xA9)
  else false

(* Reports whether the bytes of lit are in the text at the position at. The caller has checked that they fit. *)
let rec equal_lit t at lit k =
  k = String.length lit || (byte t (at + k) = Char.code (String.get lit k) && equal_lit t at lit (k + 1))

(* Reports whether the n bytes at a and the n bytes at b are the same. *)
let rec equal_spans t a b n = n = 0 || (byte t a = byte t b && equal_spans t (a + 1) (b + 1) (n - 1))

(* Returns the first position at or after at where the bytes of lit are in the text, or -1. *)
let rec index_of t at hi lit =
  if at > hi - String.length lit then -1
  else if byte t at = Char.code (String.get lit 0) && equal_lit t at lit 1 then at
  else index_of t (at + 1) hi lit

(* Returns the width of the character at the position at when the instruction's character or class matches it, and 0
   otherwise. *)
let one i t at hi =
  if at >= hi then 0
  else
    let c = byte t at in
    let packed = if c < Utf8.rune_self then (c lsl 3) lor 1 else Utf8.decode t at hi in
    let r = Utf8.rune packed in
    if i.has_set then if Charset.contains i.set r then Utf8.width packed else 0
    else if r = i.r then Utf8.width packed
    else 0

(* The greedy single character loop of Op_rep. It takes characters from at while the instruction matches and the max
   allows, and then pushes the choice of giving some back. n counts the characters taken and floor is the position
   once the minimum was met. It returns the position reached, or -1 when the minimum was not met. *)
let rec rep st i t hi pc at n floor =
  if n <> i.max && at < hi then
    let c = byte t at in
    let w =
      if c < Utf8.rune_self then if if i.has_set then Charset.ascii_has i.set.ascii c else c = i.r then 1 else 0
      else
        let packed = Utf8.decode t at hi in
        let r = Utf8.rune packed in
        if if i.has_set then Charset.contains i.set r else r = i.r then Utf8.width packed else 0
    in
    if w = 0 then rep_done st i pc at n floor
    else
      let at = at + w and n = n + 1 in
      rep st i t hi pc at n (if n = i.min then at else floor)
  else rep_done st i pc at n floor

and rep_done st i pc at n floor =
  if n >= i.min then begin
    if at > floor then push st e_rep (pc + 1) at floor;
    at
  end
  else -1

(* Compares the group from..to with the text at the position at, going forward and ignoring case. It returns the
   position after the text that matched, or -1. *)
let rec backref_fold t hi flag from until at =
  if from >= until then at
  else if at >= hi then -1
  else
    let want = Utf8.decode t from until in
    let got = Utf8.decode t at hi in
    let w = Utf8.rune want and g = Utf8.rune got in
    if g = w || Charset.canonicalize g flag = Charset.canonicalize w flag then
      backref_fold t hi flag (from + Utf8.width want) until (at + Utf8.width got)
    else -1

(* The same going backward: the group is read from its end and the text before the position at. *)
let rec backref_fold_back t lo flag from until at =
  if from >= until then at
  else if at <= lo then -1
  else
    let want = Utf8.decode_last t from until in
    let got = Utf8.decode_last t lo at in
    let w = Utf8.rune want and g = Utf8.rune got in
    if g = w || Charset.canonicalize g flag = Charset.canonicalize w flag then
      backref_fold_back t lo flag from (until - Utf8.width want) (at - Utf8.width got)
    else -1

(* Reports whether a code point before or at a position is a word character of a case-insensitive word boundary. *)
let fold_word_before t lo pos = pos > lo && Charset.is_fold_word_rune (Utf8.rune (Utf8.decode_last t lo pos))
let fold_word_at t pos hi = pos < hi && Charset.is_fold_word_rune (Utf8.rune (Utf8.decode t pos hi))

(* Runs the instruction at pc with the text at pos. It reports whether the program matches. It keeps its choices on
   an explicit stack, so the depth of the search is bounded by memory and not by the stack of the thread. When it
   fails, every group and counter is back to the value it had on entry. *)
let rec step insts st t lo hi pc pos =
  let i = insts.(pc) in
  match i.op with
  | Op_char ->
      if pos < hi then
        let c = byte t pos in
        if c < Utf8.rune_self then
          if c = i.r then step insts st t lo hi (pc + 1) (pos + 1) else backtrack insts st t lo hi
        else
          let packed = Utf8.decode t pos hi in
          if Utf8.rune packed = i.r then step insts st t lo hi (pc + 1) (pos + Utf8.width packed)
          else backtrack insts st t lo hi
      else backtrack insts st t lo hi
  | Op_set ->
      if pos < hi then
        let c = byte t pos in
        if c < Utf8.rune_self then
          if Charset.ascii_has i.set.ascii c then step insts st t lo hi (pc + 1) (pos + 1)
          else backtrack insts st t lo hi
        else
          let packed = Utf8.decode t pos hi in
          if Charset.contains i.set (Utf8.rune packed) then step insts st t lo hi (pc + 1) (pos + Utf8.width packed)
          else backtrack insts st t lo hi
      else backtrack insts st t lo hi
  | Op_lit ->
      let n = String.length i.lit in
      if hi - pos >= n && equal_lit t pos i.lit 0 then step insts st t lo hi (pc + 1) (pos + n)
      else backtrack insts st t lo hi
  | Op_char_back ->
      if pos > lo then
        let c = byte t (pos - 1) in
        let packed = if c < Utf8.rune_self then (c lsl 3) lor 1 else Utf8.decode_last t lo pos in
        if Utf8.rune packed = i.r then step insts st t lo hi (pc + 1) (pos - Utf8.width packed)
        else backtrack insts st t lo hi
      else backtrack insts st t lo hi
  | Op_set_back ->
      if pos > lo then
        let c = byte t (pos - 1) in
        let packed = if c < Utf8.rune_self then (c lsl 3) lor 1 else Utf8.decode_last t lo pos in
        if Charset.contains i.set (Utf8.rune packed) then step insts st t lo hi (pc + 1) (pos - Utf8.width packed)
        else backtrack insts st t lo hi
      else backtrack insts st t lo hi
  | Op_fail -> backtrack insts st t lo hi
  | Op_bol -> if pos = lo then step insts st t lo hi (pc + 1) pos else backtrack insts st t lo hi
  | Op_eol -> if pos = hi then step insts st t lo hi (pc + 1) pos else backtrack insts st t lo hi
  | Op_word_boundary ->
      let before = pos > lo && Charset.is_word_byte (byte t (pos - 1)) in
      let after = pos < hi && Charset.is_word_byte (byte t pos) in
      if before <> after then step insts st t lo hi (pc + 1) pos else backtrack insts st t lo hi
  | Op_not_word_boundary ->
      let before = pos > lo && Charset.is_word_byte (byte t (pos - 1)) in
      let after = pos < hi && Charset.is_word_byte (byte t pos) in
      if before = after then step insts st t lo hi (pc + 1) pos else backtrack insts st t lo hi
  | Op_line_start ->
      if pos = lo || line_terminator_before t lo pos then step insts st t lo hi (pc + 1) pos
      else backtrack insts st t lo hi
  | Op_line_end ->
      if pos = hi || line_terminator_at t pos hi then step insts st t lo hi (pc + 1) pos else backtrack insts st t lo hi
  | Op_word_boundary_fold ->
      if fold_word_before t lo pos <> fold_word_at t pos hi then step insts st t lo hi (pc + 1) pos
      else backtrack insts st t lo hi
  | Op_not_word_boundary_fold ->
      if fold_word_before t lo pos = fold_word_at t pos hi then step insts st t lo hi (pc + 1) pos
      else backtrack insts st t lo hi
  | Op_jmp -> step insts st t lo hi i.x pos
  | Op_split ->
      push st e_choice i.y pos 0;
      step insts st t lo hi i.x pos
  | Op_save_tmp ->
      push st e_tmp i.a st.tmp.(i.a) 0;
      st.tmp.(i.a) <- pos;
      step insts st t lo hi (pc + 1) pos
  | Op_capture ->
      let caps = st.caps in
      push st e_cap i.a caps.(2 * i.a) caps.((2 * i.a) + 1);
      if i.flag then begin
        caps.(2 * i.a) <- pos;
        caps.((2 * i.a) + 1) <- st.tmp.(i.a)
      end
      else begin
        caps.(2 * i.a) <- st.tmp.(i.a);
        caps.((2 * i.a) + 1) <- pos
      end;
      step insts st t lo hi (pc + 1) pos
  | Op_clear ->
      let caps = st.caps in
      for g = i.a to i.b do
        if caps.(2 * g) >= 0 then begin
          push st e_cap g caps.(2 * g) caps.((2 * g) + 1);
          caps.(2 * g) <- -1;
          caps.((2 * g) + 1) <- -1
        end
      done;
      step insts st t lo hi (pc + 1) pos
  | Op_backref ->
      (* A group that has not taken part matches the empty string. *)
      let from = st.caps.(2 * i.a) and until = st.caps.((2 * i.a) + 1) in
      if from < 0 then step insts st t lo hi (pc + 1) pos
      else
        let n = until - from in
        if hi - pos >= n && equal_spans t pos from n then step insts st t lo hi (pc + 1) (pos + n)
        else backtrack insts st t lo hi
  | Op_backref_back ->
      let from = st.caps.(2 * i.a) and until = st.caps.((2 * i.a) + 1) in
      if from < 0 then step insts st t lo hi (pc + 1) pos
      else
        let n = until - from in
        if pos - lo >= n && equal_spans t (pos - n) from n then step insts st t lo hi (pc + 1) (pos - n)
        else backtrack insts st t lo hi
  | Op_backref_fold ->
      let from = st.caps.(2 * i.a) and until = st.caps.((2 * i.a) + 1) in
      if from < 0 then step insts st t lo hi (pc + 1) pos
      else
        let at = backref_fold t hi i.flag from until pos in
        if at >= 0 then step insts st t lo hi (pc + 1) at else backtrack insts st t lo hi
  | Op_backref_fold_back ->
      let from = st.caps.(2 * i.a) and until = st.caps.((2 * i.a) + 1) in
      if from < 0 then step insts st t lo hi (pc + 1) pos
      else
        let at = backref_fold_back t lo i.flag from until pos in
        if at >= 0 then step insts st t lo hi (pc + 1) at else backtrack insts st t lo hi
  | Op_look ->
      push st (if i.flag then e_look_neg else e_look) i.x pos 0;
      step insts st t lo hi (pc + 1) pos
  | Op_look_ok ->
      (* The body matched. A lookaround is atomic, so the choices made inside it are dropped. The undo entries stay,
         because the groups it set remain set until something below this point is retried. *)
      let stack = st.stack in
      let f = ref (st.sp - 4) in
      while stack.(!f) <> e_look do
        f := !f - 4
      done;
      let pos = stack.(!f + 2) in
      let keep = ref !f in
      let k = ref (!f + 4) in
      while !k < st.sp do
        if stack.(!k) >= e_tmp then begin
          stack.(!keep) <- stack.(!k);
          stack.(!keep + 1) <- stack.(!k + 1);
          stack.(!keep + 2) <- stack.(!k + 2);
          stack.(!keep + 3) <- stack.(!k + 3);
          keep := !keep + 4
        end;
        k := !k + 4
      done;
      st.sp <- !keep;
      step insts st t lo hi (pc + 1) pos
  | Op_look_fail ->
      (* The body of a negative lookaround matched, so the lookaround fails and everything it did is undone. *)
      let stack = st.stack in
      let e = ref (st.sp - 4) in
      while stack.(!e) <> e_look_neg do
        undo st !e;
        e := !e - 4
      done;
      st.sp <- !e;
      backtrack insts st t lo hi
  | Op_rep ->
      let at = rep st i t hi pc pos 0 pos in
      if at >= 0 then step insts st t lo hi (pc + 1) at else backtrack insts st t lo hi
  | Op_rep_lazy ->
      let at = ref pos and n = ref 0 and stuck = ref false in
      while !n < i.min && not !stuck do
        let w = one i t !at hi in
        if w = 0 then stuck := true
        else begin
          at := !at + w;
          incr n
        end
      done;
      if !n = i.min then begin
        if !n <> i.max then push st e_rep_lazy pc !at !n;
        step insts st t lo hi (pc + 1) !at
      end
      else backtrack insts st t lo hi
  | Op_loop_init ->
      let counts = st.counts in
      push st e_counter i.a counts.(2 * i.a) counts.((2 * i.a) + 1);
      counts.(2 * i.a) <- 0;
      counts.((2 * i.a) + 1) <- -1;
      step insts st t lo hi (pc + 1) pos
  | Op_loop ->
      let n = st.counts.(2 * i.a) in
      (* Once the minimum is met, ECMA-262 rejects an iteration that consumed nothing. That is what stops a loop over
         a body that can be empty. *)
      if n > i.min && pos = st.counts.((2 * i.a) + 1) then backtrack insts st t lo hi
      else if n < i.min then step insts st t lo hi (pc + 1) pos
      else if i.max >= 0 && n >= i.max then step insts st t lo hi i.x pos
      else if i.flag then begin
        push st e_choice (pc + 1) pos 0;
        step insts st t lo hi i.x pos
      end
      else begin
        push st e_choice i.x pos 0;
        step insts st t lo hi (pc + 1) pos
      end
  | Op_loop_iter ->
      let counts = st.counts in
      push st e_counter i.a counts.(2 * i.a) counts.((2 * i.a) + 1);
      counts.(2 * i.a) <- counts.(2 * i.a) + 1;
      counts.((2 * i.a) + 1) <- pos;
      step insts st t lo hi (pc + 1) pos
  | Op_match -> true

(* The instruction failed. Takes up the most recent choice, undoing what was recorded since. *)
and backtrack insts st t lo hi =
  if st.sp = 0 then false
  else
    let stack = st.stack in
    let e = st.sp - 4 in
    let kind = stack.(e) in
    if kind = e_choice then begin
      st.sp <- e;
      step insts st t lo hi stack.(e + 1) stack.(e + 2)
    end
    else if kind = e_rep then begin
      (* Give back the last character taken. The characters between a and pos were decoded going forward, and
         decoding the last of them going backward finds the same boundary. *)
      let pos = stack.(e + 2) and floor = stack.(e + 3) in
      let w = if byte t (pos - 1) >= Utf8.rune_self then Utf8.width (Utf8.decode_last t floor pos) else 1 in
      let pos = pos - w in
      stack.(e + 2) <- pos;
      if pos <= floor then st.sp <- e;
      step insts st t lo hi stack.(e + 1) pos
    end
    else if kind = e_rep_lazy then begin
      let pc = stack.(e + 1) in
      let rep = insts.(pc) in
      let w = one rep t stack.(e + 2) hi in
      if w = 0 then begin
        st.sp <- e;
        backtrack insts st t lo hi
      end
      else begin
        let pos = stack.(e + 2) + w and taken = stack.(e + 3) + 1 in
        stack.(e + 2) <- pos;
        stack.(e + 3) <- taken;
        if taken = rep.max then st.sp <- e;
        step insts st t lo hi (pc + 1) pos
      end
    end
    else if kind = e_look then begin
      (* The body of a positive lookaround has no way left to match. *)
      st.sp <- e;
      backtrack insts st t lo hi
    end
    else if kind = e_look_neg then begin
      (* The body of a negative lookaround has no way to match, so the lookaround holds. *)
      st.sp <- e;
      step insts st t lo hi stack.(e + 1) stack.(e + 2)
    end
    else begin
      undo st e;
      st.sp <- e;
      backtrack insts st t lo hi
    end

(* Reports whether the program matches at the position start. *)
let run p st t lo hi start =
  st.sp <- 0;
  step p.insts st t lo hi 0 start

let rec search_prefix p st t lo hi at =
  let at = index_of t at hi p.prefix in
  if at < 0 then false else run p st t lo hi at || search_prefix p st t lo hi (at + 1)

let rec search_first p st t lo hi at =
  if at = hi then (not p.has_first) && run p st t lo hi at
  else
    let c = byte t at in
    let packed = if c < Utf8.rune_self then (c lsl 3) lor 1 else Utf8.decode t at hi in
    if ((not p.has_first) || Charset.contains p.first (Utf8.rune packed)) && run p st t lo hi at then true
    else search_first p st t lo hi (at + Utf8.width packed)

let search p st t lo hi =
  if p.anchored then run p st t lo hi lo
  else if String.length p.prefix > 0 then search_prefix p st t lo hi lo
  else search_first p st t lo hi lo

(* Makes the state ready for a match of the program, searches, and lets go of the text. *)
let match_text p st lo hi =
  (* The memory grows to what this program needs. The groups start out not having taken part. The entry positions and
     the counters need no start value: a match writes each before it uses it, and until then only saves and restores
     what it finds there. *)
  if Array.length st.caps < 2 * p.groups then begin
    st.caps <- Array.make (2 * p.groups) (-1);
    st.tmp <- Array.make p.groups 0
  end;
  if Array.length st.counts < 2 * p.loops then st.counts <- Array.make (2 * p.loops) 0;
  for k = 0 to (2 * p.groups) - 1 do
    st.caps.(k) <- -1
  done;
  let t = st.text in
  match search p st t lo hi with
  | matched ->
      if Array.length st.stack > 4 * max_retained_stack then st.stack <- [||];
      t.b <- Bytes.empty;
      t.s <- "";
      matched
  | exception e ->
      st.stack <- [||];
      t.b <- Bytes.empty;
      t.s <- "";
      raise e

(** [match_bytes p st text lo hi] reports whether the program matches anywhere in the bytes [lo] up to [hi] of the text,
    with [st] as its working memory. The caller has checked the range. *)
let match_bytes p st text lo hi =
  let t = st.text in
  t.b <- text;
  t.is_string <- false;
  match_text p st lo hi

(** [match_string p st text lo hi] is {!match_bytes} for a string. *)
let match_string p st text lo hi =
  let t = st.text in
  t.s <- text;
  t.is_string <- true;
  match_text p st lo hi
