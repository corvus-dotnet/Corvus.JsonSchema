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

(** Lies under the choices of a memo point. When it is reached, every way from the point has failed, and the bit pos of
    the memory is set (see [memo] of the state). *)
let e_mark = 5

(** Restores the entry position of group pc to pos. *)
let e_tmp = 6

(** Restores the start and end of group pc to pos and a. *)
let e_cap = 7

(** Restores the count and the iteration start of loop pc to pos and a. *)
let e_counter = 8

exception Match_time
(** A match was abandoned because it ran for longer than its limit. It is not an answer. *)

exception Match_memory
(** A match was abandoned because its backtracking stack reached [max_stack] entries. It is not an answer. *)

(** The number of steps the matcher takes between two looks at the clock and at the depth of its stack. A step is one
    instruction, and eight characters of a single character loop or of a backreference. A step takes a few nanoseconds,
    so the clock is read every few tens of microseconds, a match that ends before its first check never reads it, and a
    match goes past its limit by that much at most. *)
let check_every = 1 lsl 12

(** The number of entries the backtracking stack of a match with a limit may hold (four words each, which is 32 MB). A
    match that needs more is abandoned. A pattern that keeps a choice for every character of its text needs one entry or
    a few for each, so this is room for a text of some hundred thousand characters, and a pattern that pushes without
    consuming, such as [(?:a?){2147483647}], reaches it in a few milliseconds. It is looked at with the clock, so the
    stack can go past it by [check_every] entries. *)
let max_stack = 1 lsl 20

(** The largest memory of failed places, in words of 64 bits: 8 MB. A match that would need more goes without it. *)
let max_memo_words = 1 lsl 20

(** The number of steps for each position of the text that a match makes before it takes the memory into use. *)
let memo_steps_per_position = 64

(** The largest memory kept in the state between matches, in words of 64 bits. *)
let max_retained_memo = 1 lsl 16

(** The depth of the backtracking stack, in entries, beyond which a memo point is not marked. A mark is an entry of the
    stack, and a pattern that keeps no choice for a character still marks a place for each, so on a long text the marks
    alone would reach [max_stack]. A place that is not marked is not remembered, which loses time and nothing else, and
    the places below this depth are remembered when the stack unwinds, so a later start has the room to mark the next
    ones. *)
let max_mark_depth = max_stack / 2

(** Makes every match take its memory of failed places before its first step. The tests set it, to run every pattern
    they have with the memory in use, and so does the harness that runs the pattern datasets, to compare the answers
    with those of a match that takes the memory when it would by itself. Nothing else writes to it. *)
let memo_from_the_start = ref false

(* Why a match was abandoned. *)
let stop_none = 0
let stop_time = 1
let stop_memory = 2

type state = {
  text : Utf8.text;
  mutable stack : int array;
      (** The entries, four elements each: the kind, pc, pos and a. The first [sp] elements are in use. *)
  mutable sp : int;
  mutable caps : int array;  (** The start and end of each tracked group, or -1. *)
  mutable tmp : int array;  (** The position at which each tracked group was entered. *)
  mutable counts : int array;
      (** For each loop, the number of iterations started and the position at which the last one started. *)
  mutable units : bool;
      (** Says the program of the match in hand matches by UTF-16 code unit (see utf8.ml). It is set from the program
          when a match starts. *)
  clock : float array;
      (** Three times in seconds, in an array of floats so that none is a block of its own and reading the clock
          allocates nothing. The first is the limit of a match, or zero for no limit: it belongs to the state and every
          match with the state has it. The second is the deadline of the match in hand, set at its first check, and the
          third the time of its last check. *)
  mutable timed : bool;  (** Says the deadline of the match in hand is set. *)
  mutable own_clock : (unit -> float) option;
      (** The clock the limit is counted on, in seconds, when the caller has given one. Without one it is the processor
          time of the process. *)
  mutable steps : int;  (** The number of steps left before the next check. *)
  mutable stop : int;  (** Why the match in hand was abandoned, or [stop_none]. *)
  mutable memo : Bytes.t;
      (** The memory of the places where a way has failed: one bit for each memo point of the program, each position of
          the text and each state of the loops around the point (see [m] of an instruction). A bit is set when every way
          from the place has failed, and a way that comes to a place whose bit is set fails at once. So no place is gone
          through twice, and a pattern of repetitions inside repetitions takes time proportional to its size times the
          text and not time that doubles with each character.

          The memory costs its size to clear, and a mark on the stack for each place gone through, so a match starts
          without it. It takes it at the check by which it has made as many steps as the memory has words and
          [memo_steps_per_position] more for each position of the text, which a match that goes through its text once or
          a few times never makes. A place whose way failed before then is not remembered, which loses time and nothing
          else. *)
  mutable memo_on : bool;  (** Says the memory is in use. *)
  mutable memo_off : bool;
      (** Says the memory never will be in use: the program has no memo points, or the memory would be larger than
          [max_memo_words]. *)
  mutable lo : int;  (** The start of the text of the match in hand. *)
  mutable width : int;  (** The number of positions of the text. *)
  mutable checks : int;  (** The checks the match in hand has made. *)
  mutable points : int;  (** The memo points of the program of the match in hand. *)
}
(** The working memory of one match. A match allocates only while the stack is still growing to the depth the pattern
    and the text need. *)

(** The largest stack kept between matches, in entries. *)
let max_retained_stack = 1 lsl 16

let new_state () =
  {
    text = Utf8.new_text ();
    stack = [||];
    sp = 0;
    caps = [||];
    tmp = [||];
    counts = [||];
    units = false;
    clock = [| 0.; 0.; 0. |];
    timed = false;
    own_clock = None;
    steps = 0;
    stop = stop_none;
    memo = Bytes.empty;
    memo_on = false;
    memo_off = true;
    lo = 0;
    width = 0;
    checks = 0;
    points = 0;
  }

(* The number of words of 64 bits of the memory of a match. *)
let memo_words st = ((st.points * (st.width lsl Compile.max_memo_loops)) + 63) lsr 6

(* Takes the memory of failed places into use when the match has made enough steps to pay for it. *)
let remember st =
  let words = memo_words st in
  if words > max_memo_words then st.memo_off <- true
  else if st.checks * check_every >= words + (memo_steps_per_position * st.width) || !memo_from_the_start then begin
    if Bytes.length st.memo < 8 * words then st.memo <- Bytes.make (8 * words) '\000'
    else Bytes.fill st.memo 0 (8 * words) '\000';
    st.memo_on <- true
  end

(* Returns the bit of the memory for the memo point of an instruction at a position, with the state of the loops around
   the point: for each, whether it would refuse an iteration that ended here as empty. *)
let memo_index st (i : inst) pos =
  let idx = (((i.m - 1) * st.width) + (pos - st.lo)) lsl Compile.max_memo_loops in
  let counts = st.counts in
  let idx = if i.ml0 >= 0 && counts.(2 * i.ml0) > i.mm0 && counts.((2 * i.ml0) + 1) = pos then idx lor 1 else idx in
  if i.ml1 >= 0 && counts.(2 * i.ml1) > i.mm1 && counts.((2 * i.ml1) + 1) = pos then idx lor 2 else idx

let failed st idx = (Char.code (Bytes.get st.memo (idx lsr 3)) lsr (idx land 7)) land 1 <> 0

let set_failed st idx =
  Bytes.set st.memo (idx lsr 3) (Char.chr (Char.code (Bytes.get st.memo (idx lsr 3)) lor (1 lsl (idx land 7))))

(** [set_limit st seconds] sets the time every match with the state may take from now on. Zero or less, or a value that
    is not a number, is no limit. *)
let set_limit st seconds = st.clock.(0) <- (if seconds > 0. then seconds else 0.)

(** [set_clock st clock] sets the clock the limit of every match with the state is counted on from now on: a function
    that returns the time in seconds, or [None] for the processor time of the process. *)
let set_clock st clock = st.own_clock <- clock

(** [limit st] is the limit of the state, or zero for none. *)
let limit st = st.clock.(0)

(* Without a clock of the caller's, the clock is the processor time of the process, from Sys.time. The standard library
   has no other clock, and this library uses nothing else. Sys.time returns an unboxed float and allocates nothing. It
   does not move while the process waits, so a match is abandoned for the time the process computed and not for the
   time that passed. It is the time of the whole process, so in a program that computes on several domains at once it
   runs that many times faster, and a match there is abandoned sooner than its limit. A caller for whom that matters
   gives a clock of its own, such as Unix.gettimeofday or a monotonic one. Such a clock is a closure that returns a
   float, which is a block of its own for each read.

   A clock of the caller's can be set. Set forward, a match is abandoned early, which is an error and not a wrong
   answer. Set back, the deadline is counted again from the new time, so a match never gets more than its limit after
   a change.

   check is called every check_every steps. It reports whether the match may go on. The clock starts at the first
   check. *)
let check st =
  st.steps <- check_every;
  if (not st.memo_on) && not st.memo_off then begin
    st.checks <- st.checks + 1;
    remember st
  end;
  let limit = st.clock.(0) in
  if limit <= 0. then true
  else if st.sp > 4 * max_stack then begin
    st.stop <- stop_memory;
    false
  end
  else
    let now = match st.own_clock with None -> Sys.time () | Some clock -> clock () in
    if (not st.timed) || now < st.clock.(2) then begin
      st.timed <- true;
      st.clock.(1) <- now +. limit;
      st.clock.(2) <- now;
      true
    end
    else if now > st.clock.(1) then begin
      st.stop <- stop_time;
      false
    end
    else begin
      st.clock.(2) <- now;
      true
    end

let byte (t : Utf8.text) i = if t.is_string then Char.code (String.get t.s i) else Char.code (Bytes.get t.b i)

(* The stack grows to twice its size. A match with a limit is abandoned at the first check after its stack has passed
   max_stack entries, so its stack is not grown far beyond what it can use before then: the entries of max_stack, of
   the steps between two checks, and of the groups one instruction can clear. *)
let grow st =
  let length = Array.length st.stack in
  let doubled = (2 * length) + 256 in
  let enough = 4 * (max_stack + check_every + (1 lsl 16)) in
  let size = if st.clock.(0) > 0. && length < enough && doubled > enough then enough else doubled in
  let grown = Array.make size 0 in
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

(* Reads the character at the position at, which is not ASCII: a code unit when the match is by code unit, and a code
   point otherwise. decode_before reads the character that ends at the position. *)
let decode_at st t lo at hi = if st.units then Utf8.unit_at t lo at hi else Utf8.decode t at hi
let decode_before st t lo at hi = if st.units then Utf8.unit_before t lo at hi else Utf8.decode_last t lo at

(* Returns the width of the character at the position at when the instruction's character or class matches it, and 0
   otherwise. The character is a code unit when the match is by code unit. *)
let one st i t lo at hi =
  if at >= hi then 0
  else
    let c = byte t at in
    let packed = if c < Utf8.rune_self then (c lsl 3) lor 1 else decode_at st t lo at hi in
    let r = Utf8.rune packed in
    if i.has_set then if Charset.contains i.set r then Utf8.width packed else 0
    else if r = i.r then Utf8.width packed
    else 0

(* Reports whether a way with the quick q can go on from the position. *)
let allows (q : quick) t pos hi =
  if pos >= hi then q.at_end
  else if q.mid then true
  else
    let c = byte t pos in
    if c < Utf8.rune_self then (Char.code (Bytes.get q.ascii (c lsr 3)) lsr (c land 7)) land 1 <> 0 else q.other

(* The greedy single character loop of Op_rep. It takes characters from at while the instruction matches and the max
   allows, and then pushes the choice of giving some back. n counts the characters taken and floor is the position
   once the minimum was met. It returns the position reached, or -1 when the minimum was not met. *)
let rec rep st i t lo hi pc at n floor =
  if n <> i.max && at < hi then
    let c = byte t at in
    let w =
      if c < Utf8.rune_self then if if i.has_set then Charset.ascii_has i.set.ascii c else c = i.r then 1 else 0
      else
        let packed = decode_at st t lo at hi in
        let r = Utf8.rune packed in
        if if i.has_set then Charset.contains i.set r else r = i.r then Utf8.width packed else 0
    in
    if w = 0 then rep_done st i pc at n floor
    else
      let at = at + w and n = n + 1 in
      rep st i t lo hi pc at n (if n = i.min then at else floor)
  else rep_done st i pc at n floor

and rep_done st i pc at n floor =
  st.steps <- st.steps - (n lsr 3);
  if n >= i.min then
    if st.memo_on && i.m <> 0 then begin
      let idx = memo_index st i at in
      if failed st idx then begin
        (* What follows the loop has failed from here before. The loop gives a character back. *)
        if at > floor then push st e_rep (pc + 1) at floor;
        -1
      end
      else begin
        if at > floor then push st e_rep (pc + 1) at floor
        else if st.sp < 4 * max_mark_depth then push st e_mark 0 idx 0;
        at
      end
    end
    else begin
      if at > floor then push st e_rep (pc + 1) at floor;
      at
    end
  else -1

(* Compares the group from..until with the text at the position at, going forward, a character at a time: to fold
   case (with fold), or because a group matched by code unit can begin or end between the halves of a character,
   where its bytes do not say what it holds. It returns the position after the text that matched, or -1. (The second
   half of a character lies beyond the end of a group that ends between the halves, so a code unit of the group is
   read from the whole text.) *)
let rec backref_chars st t lo hi fold flag from until at =
  if from >= until then at
  else if at >= hi then -1
  else
    let want = if st.units then Utf8.unit_at t lo from hi else Utf8.decode t from until in
    let got = decode_at st t lo at hi in
    let w = Utf8.rune want and g = Utf8.rune got in
    if g = w || (fold && Charset.canonicalize g flag = Charset.canonicalize w flag) then
      backref_chars st t lo hi fold flag (from + Utf8.width want) until (at + Utf8.width got)
    else -1

(* The same going backward: the group is read from its end and the text before the position at. *)
let rec backref_chars_back st t lo hi fold flag from until at =
  if from >= until then at
  else if at <= lo then -1
  else
    let want = if st.units then Utf8.unit_before t lo until hi else Utf8.decode_last t from until in
    let got = decode_before st t lo at hi in
    let w = Utf8.rune want and g = Utf8.rune got in
    if g = w || (fold && Charset.canonicalize g flag = Charset.canonicalize w flag) then
      backref_chars_back st t lo hi fold flag from (until - Utf8.width want) (at - Utf8.width got)
    else -1

(* Reports whether a code point before or at a position is a word character of a case-insensitive word boundary. *)
let fold_word_before t lo pos = pos > lo && Charset.is_fold_word_rune (Utf8.rune (Utf8.decode_last t lo pos))
let fold_word_at t pos hi = pos < hi && Charset.is_fold_word_rune (Utf8.rune (Utf8.decode t pos hi))

(* Runs the instruction at pc with the text at pos. It reports whether the program matches. It keeps its choices on
   an explicit stack, so the depth of the search is bounded by memory and not by the stack of the thread. When it
   fails, every group and counter is back to the value it had on entry. *)
let rec step insts st t lo hi pc pos =
  let left = st.steps - 1 in
  st.steps <- left;
  if left < 0 && not (check st) then false else exec insts st t lo hi pc pos

and exec insts st t lo hi pc pos =
  let i = insts.(pc) in
  match i.op with
  | Op_char ->
      if pos < hi then
        let c = byte t pos in
        if c < Utf8.rune_self then
          if c = i.r then step insts st t lo hi (pc + 1) (pos + 1) else backtrack insts st t lo hi
        else
          let packed = decode_at st t lo pos hi in
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
          let packed = decode_at st t lo pos hi in
          if Charset.contains i.set (Utf8.rune packed) then step insts st t lo hi (pc + 1) (pos + Utf8.width packed)
          else backtrack insts st t lo hi
      else backtrack insts st t lo hi
  | Op_lit ->
      let n = String.length i.lit in
      st.steps <- st.steps - (n lsr 3);
      if hi - pos >= n && equal_lit t pos i.lit 0 then step insts st t lo hi (pc + 1) (pos + n)
      else backtrack insts st t lo hi
  | Op_char_back ->
      if pos > lo then
        let c = byte t (pos - 1) in
        let packed = if c < Utf8.rune_self then (c lsl 3) lor 1 else decode_before st t lo pos hi in
        if Utf8.rune packed = i.r then step insts st t lo hi (pc + 1) (pos - Utf8.width packed)
        else backtrack insts st t lo hi
      else backtrack insts st t lo hi
  | Op_set_back ->
      if pos > lo then
        let c = byte t (pos - 1) in
        let packed = if c < Utf8.rune_self then (c lsl 3) lor 1 else decode_before st t lo pos hi in
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
      (* A place from which every way has failed before fails at once. The mark is under whatever comes of this place,
         a choice or not, so that the place is remembered when it has all failed. *)
      if st.memo_on && i.m <> 0 && failed st (memo_index st i pos) then backtrack insts st t lo hi
      else begin
        if st.memo_on && i.m <> 0 && st.sp < 4 * max_mark_depth then push st e_mark 0 (memo_index st i pos) 0;
        (* A way the next character rules out is not taken, and no choice is kept for it. *)
        if not (allows insts.(i.y).q t pos hi) then
          if not (allows insts.(i.x).q t pos hi) then backtrack insts st t lo hi else step insts st t lo hi i.x pos
        else if not (allows insts.(i.x).q t pos hi) then step insts st t lo hi i.y pos
        else begin
          push st e_choice i.y pos 0;
          step insts st t lo hi i.x pos
        end
      end
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
      st.steps <- st.steps - (i.b - i.a);
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
        st.steps <- st.steps - (n lsr 3);
        if hi - pos >= n && equal_spans t pos from n then step insts st t lo hi (pc + 1) (pos + n)
        else backtrack insts st t lo hi
  | Op_backref_back ->
      let from = st.caps.(2 * i.a) and until = st.caps.((2 * i.a) + 1) in
      if from < 0 then step insts st t lo hi (pc + 1) pos
      else
        let n = until - from in
        st.steps <- st.steps - (n lsr 3);
        if pos - lo >= n && equal_spans t (pos - n) from n then step insts st t lo hi (pc + 1) (pos - n)
        else backtrack insts st t lo hi
  | Op_backref_fold | Op_backref_units ->
      let from = st.caps.(2 * i.a) and until = st.caps.((2 * i.a) + 1) in
      if from < 0 then step insts st t lo hi (pc + 1) pos
      else
        let () = st.steps <- st.steps - ((until - from) lsr 3) in
        let at = backref_chars st t lo hi (i.op = Op_backref_fold) i.flag from until pos in
        if at >= 0 then step insts st t lo hi (pc + 1) at else backtrack insts st t lo hi
  | Op_backref_fold_back | Op_backref_units_back ->
      let from = st.caps.(2 * i.a) and until = st.caps.((2 * i.a) + 1) in
      if from < 0 then step insts st t lo hi (pc + 1) pos
      else
        let () = st.steps <- st.steps - ((until - from) lsr 3) in
        let at = backref_chars_back st t lo hi (i.op = Op_backref_fold_back) i.flag from until pos in
        if at >= 0 then step insts st t lo hi (pc + 1) at else backtrack insts st t lo hi
  | Op_look ->
      (* What follows a lookaround starts where the lookaround does, and so does the body of a lookahead. *)
      if not (allows i.q t pos hi) then backtrack insts st t lo hi
      else begin
        push st (if i.flag then e_look_neg else e_look) i.x pos 0;
        step insts st t lo hi (pc + 1) pos
      end
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
      let at = rep st i t lo hi pc pos 0 pos in
      if at >= 0 then step insts st t lo hi (pc + 1) at else backtrack insts st t lo hi
  | Op_rep_lazy ->
      let at = ref pos and n = ref 0 and stuck = ref false in
      while !n < i.min && not !stuck do
        let w = one st i t lo !at hi in
        if w = 0 then stuck := true
        else begin
          at := !at + w;
          incr n
        end
      done;
      st.steps <- st.steps - (!n lsr 3);
      if !n = i.min then
        if st.memo_on && i.m <> 0 then begin
          let idx = memo_index st i !at in
          if failed st idx then begin
            (* What follows the loop has failed from here before. The loop takes one more character. *)
            if !n <> i.max then push st e_rep_lazy pc !at !n;
            backtrack insts st t lo hi
          end
          else begin
            if !n <> i.max then push st e_rep_lazy pc !at !n
            else if st.sp < 4 * max_mark_depth then push st e_mark 0 idx 0;
            step insts st t lo hi (pc + 1) !at
          end
        end
        else begin
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
         a body that can be empty. The loop also gives up when fewer bytes are left than any way from here to a match
         consumes: what follows the loop, and the iterations it has still to make. A count is below 2^31 and a need at
         most 2^30, so their product fits an int. *)
      let left = hi - pos in
      if
        left < i.need
        || (n < i.min && i.iter <> 0 && left - i.need < (i.min - n) * i.iter)
        || (n > i.min && pos = st.counts.((2 * i.a) + 1))
      then backtrack insts st t lo hi
      else if n < i.min then step insts st t lo hi (pc + 1) pos
      else if i.max >= 0 && n >= i.max then step insts st t lo hi i.x pos
      else if st.memo_on && i.m <> 0 && failed st (memo_index st i pos) then
        (* The loop may be left or entered from here, and both have failed from here before. *)
        backtrack insts st t lo hi
      else begin
        if st.memo_on && i.m <> 0 && st.sp < 4 * max_mark_depth then push st e_mark 0 (memo_index st i pos) 0;
        if not (allows insts.(i.x).q t pos hi) then
          (* The next character rules out leaving the loop, so there is no choice. *)
          step insts st t lo hi (pc + 1) pos
        else if not (allows insts.(pc + 1).q t pos hi) then step insts st t lo hi i.x pos
        else if i.flag then begin
          push st e_choice (pc + 1) pos 0;
          step insts st t lo hi i.x pos
        end
        else begin
          push st e_choice i.x pos 0;
          step insts st t lo hi (pc + 1) pos
        end
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
      let pc = stack.(e + 1) and pos = stack.(e + 2) and floor = stack.(e + 3) in
      (* What followed the loop from the position it stood at has failed. *)
      let rep = insts.(pc - 1) in
      let marked = st.memo_on && rep.m <> 0 in
      if marked then set_failed st (memo_index st rep pos);
      (* Give back the last character taken. The characters between a and pos were decoded going forward, and
         decoding the last of them going backward finds the same boundary. *)
      let w =
        if byte t (pos - 1) < Utf8.rune_self then 1
        else if st.units then Utf8.width (Utf8.unit_before t lo pos hi)
        else Utf8.width (Utf8.decode_last t floor pos)
      in
      let pos = pos - w in
      stack.(e + 2) <- pos;
      let last = pos <= floor in
      (* A position from which what follows the loop cannot go on is passed over, and so is one from which it has
         failed before. *)
      if not (allows insts.(pc).q t pos hi) then begin
        if last then st.sp <- e;
        st.steps <- st.steps - 1;
        backtrack insts st t lo hi
      end
      else if marked && failed st (memo_index st rep pos) then begin
        if last then st.sp <- e;
        st.steps <- st.steps - 1;
        backtrack insts st t lo hi
      end
      else begin
        if last then
          if marked then begin
            (* The loop has nothing more to give back. The entry stays to note this last way. *)
            stack.(e) <- e_mark;
            stack.(e + 2) <- memo_index st rep pos
          end
          else st.sp <- e;
        step insts st t lo hi pc pos
      end
    end
    else if kind = e_rep_lazy then begin
      let pc = stack.(e + 1) in
      let rep = insts.(pc) in
      (* What followed the loop from the position it stood at has failed. *)
      let marked = st.memo_on && rep.m <> 0 in
      if marked then set_failed st (memo_index st rep stack.(e + 2));
      let w = one st rep t lo stack.(e + 2) hi in
      if w = 0 then begin
        st.sp <- e;
        backtrack insts st t lo hi
      end
      else begin
        let pos = stack.(e + 2) + w and taken = stack.(e + 3) + 1 in
        stack.(e + 2) <- pos;
        stack.(e + 3) <- taken;
        let last = taken = rep.max in
        if marked && failed st (memo_index st rep pos) then begin
          if last then st.sp <- e;
          st.steps <- st.steps - 1;
          backtrack insts st t lo hi
        end
        else begin
          if last then
            if marked then begin
              stack.(e) <- e_mark;
              stack.(e + 2) <- memo_index st rep pos
            end
            else st.sp <- e;
          step insts st t lo hi (pc + 1) pos
        end
      end
    end
    else if kind = e_mark then begin
      (* Every way from a memo point has failed. *)
      set_failed st stack.(e + 2);
      st.sp <- e;
      backtrack insts st t lo hi
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
  if at < 0 || hi - at < p.need then false
  else if run p st t lo hi at then true
  else if st.stop <> stop_none then false
  else search_prefix p st t lo hi (at + 1)

let rec search_first p st t lo hi at =
  if hi - at < p.need then false
  else if at = hi then (not p.has_first) && run p st t lo hi at
  else
    let c = byte t at in
    let packed = if c < Utf8.rune_self then (c lsl 3) lor 1 else decode_at st t lo at hi in
    if ((not p.has_first) || Charset.contains p.first (Utf8.rune packed)) && run p st t lo hi at then true
    else if st.stop <> stop_none then false
    else search_first p st t lo hi (at + Utf8.width packed)

let search p st t lo hi =
  if hi - lo < p.need then false
  else if p.anchored then run p st t lo hi lo
  else if String.length p.prefix > 0 then search_prefix p st t lo hi lo
  else search_first p st t lo hi lo

(* Makes the state ready for a match of the program, searches, and lets go of the text. With a limit in the state the
   match is abandoned when it has run for longer than that, or when its stack has reached max_stack, and the exception
   says which. *)
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
  st.units <- p.units;
  st.timed <- false;
  st.steps <- check_every;
  st.stop <- stop_none;
  st.memo_on <- false;
  st.memo_off <- p.points = 0;
  st.checks <- 0;
  st.lo <- lo;
  st.width <- hi - lo + 1;
  st.points <- p.points;
  if !memo_from_the_start && not st.memo_off then remember st;
  let t = st.text in
  match search p st t lo hi with
  | matched ->
      if Array.length st.stack > 4 * max_retained_stack then st.stack <- [||];
      if Bytes.length st.memo > 8 * max_retained_memo then st.memo <- Bytes.empty;
      t.b <- Bytes.empty;
      t.s <- "";
      (* A match that was abandoned has no answer. The exceptions carry nothing, so raising one allocates nothing. *)
      if st.stop = stop_time then raise Match_time else if st.stop = stop_memory then raise Match_memory else matched
  | exception e ->
      st.stack <- [||];
      st.memo <- Bytes.empty;
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
