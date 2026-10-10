(* A deterministic automaton for the patterns that need no backtracking, over ASCII text. This is the port of dfa.go
   of the ecmaregex package of the Go module.

   Matching a pattern by walking its instructions costs tens of nanoseconds per byte of text. The strings a schema
   tests are short and nearly always ASCII, and most patterns in schemas are small. For such a pattern every state
   set the text can lead to is worked out once, and a match is then one table read per byte.

   The automaton reads bytes below 0x80 only. A text with any other byte is handed to the backtracking matcher from
   the start, so nothing here needs to know UTF-8 or the Unicode classes: a class is its ASCII members. A pattern
   with a word boundary, or whose automaton has more states than a byte can number, has no automaton and keeps to
   the backtracking matcher.

   What differs from the Go source, as in the Object Pascal port: Go hands the texts the automaton does not decide to
   the regexp package of its standard library, and builds the automaton on the first match, behind a sync.Once. Here
   those texts go to the backtracking matcher, and the automaton is built when the pattern is compiled, because a
   compiled pattern is a value that threads and domains share without a lock. *)

open Parse

(* The states every automaton has. dead: no match can follow. accept: a match was found. bail: the text is not
   ASCII. *)
let dfa_dead = 0
let dfa_accept = 1
let dfa_bail = 2
let dfa_first = 3

(* An automaton has at most this many states (they are numbered in a byte), built from at most this many states of
   the pattern, in at most this many steps (a step is one state of the pattern looked at while a set is made). The
   steps bound what a pattern too large for an automaton costs before that is known, to about a millisecond. *)
let max_dfa_states = 256
let max_nfa_states = 512
let max_dfa_steps = 400000

type nfa_kind =
  | Nfa_set  (** Reads one byte of its set and goes to [out]. *)
  | Nfa_split  (** Goes to [out] and to [alt], reading nothing. *)
  | Nfa_bol  (** [Nfa_bol] and [Nfa_eol] go to [out] at the start and at the end of the text. *)
  | Nfa_eol
  | Nfa_match

type nfa_state = {
  nkind : nfa_kind;
  bits : string;  (** The 128 bits of the set, as in {!Charset.t}. *)
  mutable out : int;
  alt : int;
}

type nfa_builder = {
  mutable states : nfa_state array;  (** The states, of which the first [count] are in use. *)
  mutable count : int;
  mutable failed : bool;  (** The pattern has something an automaton over bytes cannot decide, or is too large. *)
}

let no_bits = String.make 16 '\000'
let no_state = { nkind = Nfa_match; bits = no_bits; out = 0; alt = 0 }

let add b nkind bits out alt =
  if b.count >= max_nfa_states then begin
    b.failed <- true;
    0
  end
  else begin
    if b.count = Array.length b.states then begin
      let grown = Array.make ((2 * b.count) + 16) no_state in
      Array.blit b.states 0 grown 0 b.count;
      b.states <- grown
    end;
    b.states.(b.count) <- { nkind; bits; out; alt };
    b.count <- b.count + 1;
    b.count - 1
  end

(* Adds the states of a node that go on to next, and returns the state they start at. *)
let rec compile b n next =
  if b.failed then 0
  else
    match n.kind with
    | Empty -> next
    | Char ->
        let bits = Bytes.make 16 '\000' in
        if n.r < 128 then Bytes.set bits (n.r lsr 3) (Char.chr (1 lsl (n.r land 7)));
        add b Nfa_set (Bytes.to_string bits) next 0
    | Set -> add b Nfa_set n.set.ascii next 0
    | Cat ->
        let next = ref next in
        for i = Array.length n.subs - 1 downto 0 do
          next := compile b n.subs.(i) !next
        done;
        !next
    | Alt ->
        let start = ref (compile b n.subs.(Array.length n.subs - 1) next) in
        for i = Array.length n.subs - 2 downto 0 do
          let out = compile b n.subs.(i) next in
          start := add b Nfa_split no_bits out !start
        done;
        !start
    | Group -> compile b n.subs.(0) next
    | Repeat ->
        let sub = n.subs.(0) in
        let next = ref next in
        if n.max = unbounded then begin
          (* The loop, then the copies that must match before it. *)
          let loop = add b Nfa_split no_bits 0 !next in
          if not b.failed then begin
            b.states.(loop).out <- compile b sub loop;
            next := loop
          end
        end
        else begin
          (* The copies that may match, innermost last. *)
          let i = ref n.min in
          while !i < n.max && not b.failed do
            let out = compile b sub !next in
            next := add b Nfa_split no_bits out !next;
            incr i
          done
        end;
        if b.failed then 0
        else begin
          let i = ref 0 in
          while !i < n.min && not b.failed do
            next := compile b sub !next;
            incr i
          done;
          !next
        end
    | Bol -> add b Nfa_bol no_bits next 0
    | Eol -> add b Nfa_eol no_bits next 0
    | Line_start | Line_end | Word_boundary | Not_word_boundary | Look | Backref ->
        (* A word boundary, or anything only the backtracking matcher can run. *)
        b.failed <- true;
        0

type t = {
  byte_class : string;  (** The class of each of the 256 bytes. *)
  trans : string;  (** [trans.[(state lsl shift) lor class]] is the state after a byte of that class. *)
  shift : int;
  start : int;
  at_end : string;  (** For each state: a byte that is not zero when the pattern matches if the text ends there. *)
  empty : bool;  (** The pattern matches the empty text. *)
}
(** The automaton. A state is a byte, a byte of the text is one of a few classes (bytes the pattern does not tell
    apart), and a transition is one read of [trans]. Every part is a string, so nothing can write to it. *)

(* The answers of a match. *)

(** The pattern does not match the text. *)
let no_match = 0

(** The pattern matches the text. *)
let matched = 1

(** The text is not ASCII, which this automaton does not decide. *)
let undecided = 2

let finish s = if s = dfa_accept then matched else if s = dfa_bail then undecided else no_match

let rec run_string d text i hi s =
  if i = hi then if String.get d.at_end s <> '\000' then matched else no_match
  else
    let c = Char.code (String.get text i) in
    let s = Char.code (String.get d.trans ((s lsl d.shift) lor Char.code (String.get d.byte_class c))) in
    if s < dfa_first then finish s else run_string d text (i + 1) hi s

let rec run_bytes d text i hi s =
  if i = hi then if String.get d.at_end s <> '\000' then matched else no_match
  else
    let c = Char.code (Bytes.get text i) in
    let s = Char.code (String.get d.trans ((s lsl d.shift) lor Char.code (String.get d.byte_class c))) in
    if s < dfa_first then finish s else run_bytes d text (i + 1) hi s

(** [match_string d text lo hi] says whether the pattern matches the bytes [lo] up to [hi] of the text: {!matched},
    {!no_match}, or {!undecided} when the text is not ASCII. It does not allocate. *)
let match_string d text lo hi =
  if lo = hi then if d.empty then matched else no_match
  else if d.start = dfa_accept then matched
  else run_string d text lo hi d.start

(** [match_bytes d text lo hi] is {!match_string} for bytes. The loop is written once for each kind of text so that each
    is one table read per byte with no test of the kind. *)
let match_bytes d text lo hi =
  if lo = hi then if d.empty then matched else no_match
  else if d.start = dfa_accept then matched
  else run_bytes d text lo hi d.start

type list_ = { mutable items : int array; mutable n : int }
(** A list of states of the pattern, of which the first [n] elements of [items] are in use. *)

let new_list () = { items = Array.make 16 0; n = 0 }

let append list value =
  if list.n = Array.length list.items then begin
    let grown = Array.make ((2 * list.n) + 16) 0 in
    Array.blit list.items 0 grown 0 list.n;
    list.items <- grown
  end;
  list.items.(list.n) <- value;
  list.n <- list.n + 1

type dfa_builder = {
  nfa : nfa_state array;
  mark : int array;  (** Scratch for closure: the states visited, by generation, and the stack. *)
  mutable gen : int;
  stack : list_;
  mutable steps : int;  (** The steps left (see [max_dfa_steps]). *)
}
(** Makes the automaton's states from sets of the pattern's states. *)

(* Adds to set every state that reading nothing reaches from the states in from, keeping the states that read a byte,
   the end anchors still to pass, and the match. A start anchor is passed only at_start, and an end anchor only
   at_end. *)
let closure b set from at_start at_end =
  b.gen <- b.gen + 1;
  b.stack.n <- 0;
  for k = 0 to from.n - 1 do
    append b.stack from.items.(k)
  done;
  while b.stack.n > 0 do
    b.stack.n <- b.stack.n - 1;
    let i = b.stack.items.(b.stack.n) in
    b.steps <- b.steps - 1;
    if b.mark.(i) <> b.gen then begin
      b.mark.(i) <- b.gen;
      let s = b.nfa.(i) in
      match s.nkind with
      | Nfa_split ->
          append b.stack s.out;
          append b.stack s.alt
      | Nfa_bol -> if at_start then append b.stack s.out
      | Nfa_eol -> if at_end then append b.stack s.out else append set i
      | Nfa_set | Nfa_match -> append set i
    end
  done

let has_match b set =
  let rec scan k = k < set.n && (b.nfa.(set.items.(k)).nkind = Nfa_match || scan (k + 1)) in
  scan 0

(* A set as the key of a table: its states in increasing order, two bytes each. It sorts the set. *)
let set_key set =
  (* Insertion sort: the sets are small. *)
  for i = 1 to set.n - 1 do
    let j = ref i in
    while !j > 0 && set.items.(!j) < set.items.(!j - 1) do
      let swap = set.items.(!j) in
      set.items.(!j) <- set.items.(!j - 1);
      set.items.(!j - 1) <- swap;
      decr j
    done
  done;
  let key = Bytes.create (2 * set.n) in
  for k = 0 to set.n - 1 do
    Bytes.set key (2 * k) (Char.chr (set.items.(k) land 0xFF));
    Bytes.set key ((2 * k) + 1) (Char.chr (set.items.(k) lsr 8))
  done;
  Bytes.to_string key

exception Too_large

(** [build root] builds the automaton of a pattern for an unanchored search, from the tree the parser read. It returns
    [None] when the pattern has none. The caller must not ask for the automaton of a pattern with a counted quantifier
    above 1000 or with such quantifiers nested to a product above 1000: the number of states bounds the work here only
    when each repetition adds a state. *)
let build root =
  let nb = { states = [||]; count = 0; failed = false } in
  let match_state = add nb Nfa_match no_bits 0 0 in
  let start = compile nb root match_state in
  if nb.failed then None
  else begin
    let nfa = Array.sub nb.states 0 nb.count in
    let b = { nfa; mark = Array.make (Array.length nfa) 0; gen = 0; stack = new_list (); steps = max_dfa_steps } in
    (* The classes of bytes: two bytes are in one class when every set of the pattern has both or neither. Class 0 is
       the bytes that are not ASCII. *)
    let byte_class = Bytes.make 256 '\000' in
    let classes = ref 1 in
    let signatures = Hashtbl.create 64 in
    let signature = Buffer.create ((Array.length nfa / 8) + 1) in
    for c = 0 to 127 do
      Buffer.clear signature;
      let acc = ref 0 and n = ref 0 in
      Array.iter
        (fun s ->
          if s.nkind = Nfa_set then begin
            acc := (!acc lsl 1) lor (if Charset.ascii_has s.bits c then 1 else 0) land 0xFF;
            incr n;
            if !n = 8 then begin
              Buffer.add_char signature (Char.chr !acc);
              acc := 0;
              n := 0
            end
          end)
        nfa;
      Buffer.add_char signature (Char.chr !acc);
      let key = Buffer.contents signature in
      let class_ =
        match Hashtbl.find_opt signatures key with
        | Some class_ -> class_
        | None ->
            let class_ = !classes in
            incr classes;
            Hashtbl.replace signatures key class_;
            class_
      in
      Bytes.set byte_class c (Char.chr class_)
    done;
    let classes = !classes in
    let shift = ref 0 in
    while 1 lsl !shift < classes do
      incr shift
    done;
    let shift = !shift in
    (* One byte of each class, to move a set by. *)
    let sample = Array.make 129 0 in
    for c = 127 downto 0 do
      sample.(Char.code (Bytes.get byte_class c)) <- c
    done;
    let from_start = new_list () in
    append from_start start;
    (* The search is unanchored: a match may start at any position, so after every byte the states the pattern starts
       in (not at the start of the text) join the set. *)
    let restart = new_list () in
    closure b restart from_start false false;
    let at_both = new_list () in
    closure b at_both from_start true true;
    let empty = has_match b at_both in
    let ids = Hashtbl.create 64 in
    let sets = ref (Array.make 16 [||]) in
    let set_count = ref dfa_first in
    let stride = 1 lsl shift in
    (* The transitions, by state and class. The rows of the states an automaton may have are all there from the start,
       and the ones in use are cut out at the end. The row of the dead state stays zero. *)
    let table = Bytes.make (max_dfa_states * stride) '\000' in
    for class_ = 0 to stride - 1 do
      Bytes.set table ((dfa_accept * stride) + class_) (Char.chr dfa_accept);
      Bytes.set table ((dfa_bail * stride) + class_) (Char.chr dfa_bail)
    done;
    let at_end = Buffer.create 16 in
    Buffer.add_string at_end "\000\000\000";
    let ends = new_list () and ends_closed = new_list () in
    (* Gives a set its state. It raises Too_large when there are too many. *)
    let intern set =
      if has_match b set then dfa_accept
      else if set.n = 0 then dfa_dead
      else
        let key = set_key set in
        match Hashtbl.find_opt ids key with
        | Some id -> id
        | None ->
            if !set_count >= max_dfa_states then raise Too_large;
            let id = !set_count in
            Hashtbl.replace ids key id;
            if id = Array.length !sets then begin
              let grown = Array.make (2 * id) [||] in
              Array.blit !sets 0 grown 0 id;
              sets := grown
            end;
            !sets.(id) <- Array.sub set.items 0 set.n;
            incr set_count;
            (* At the end of the text the end anchors are passed. *)
            ends.n <- 0;
            for k = 0 to set.n - 1 do
              let s = nfa.(set.items.(k)) in
              if s.nkind = Nfa_eol then append ends s.out
            done;
            ends_closed.n <- 0;
            if ends.n <> 0 then closure b ends_closed ends false true;
            Buffer.add_char at_end (if ends.n <> 0 && has_match b ends_closed then '\001' else '\000');
            id
    in
    try
      let first = new_list () in
      closure b first from_start true false;
      let start = intern first in
      let moved = new_list () and next = new_list () in
      let id = ref dfa_first in
      while !id < !set_count do
        let set = !sets.(!id) in
        for class_ = 0 to stride - 1 do
          if class_ = 0 || class_ >= classes then
            (* Not ASCII (or no such class). *)
            Bytes.set table ((!id * stride) + class_) (Char.chr dfa_bail)
          else begin
            let c = sample.(class_) in
            moved.n <- 0;
            Array.iter
              (fun i ->
                let s = nfa.(i) in
                if s.nkind = Nfa_set && Charset.ascii_has s.bits c then append moved s.out)
              set;
            for k = 0 to restart.n - 1 do
              append moved restart.items.(k)
            done;
            next.n <- 0;
            closure b next moved false false;
            let target = intern next in
            if b.steps < 0 then raise Too_large;
            Bytes.set table ((!id * stride) + class_) (Char.chr target)
          end
        done;
        incr id
      done;
      Some
        {
          byte_class = Bytes.to_string byte_class;
          trans = Bytes.sub_string table 0 (!set_count * stride);
          shift;
          start;
          at_end = Buffer.contents at_end;
          empty;
        }
    with Too_large -> None
  end
