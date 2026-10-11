(* The port of ecmaregex.go of the Go module, with the choice of back end of the Object Pascal port
   (Corvus.JsonSchema.EcmaRegex.pas). The interface describes the engine as a whole.

   charset.ml is charset.go and fold.go, parse.ml is parse.go, compile.ml is compile.go, vm.ml is vm.go and dfa.ml is
   dfa.go. utf8.ml holds what the Go source takes from the unicode/utf8 package. *)

type t = {
  pattern : string;
  unicode : bool;  (** Says the pattern was read with the u flag grammar. *)
  regular : bool;
  dfa : Dfa.t option;  (** The automaton over ASCII text, when the pattern has one. *)
  prog : Compile.program;  (** The pattern compiled for the backtracking matcher. *)
  slot : Vm.state;
      (** Working memory of the backtracking matcher for the entry points that are given none. Only the match that holds
          [busy] touches it. *)
  busy : bool Atomic.t;
}

type engine = Dfa | Backtrack

let engine_name = function Dfa -> "dfa" | Backtrack -> "backtrack"

(* The largest count the regexp package of Go accepts in a counted quantifier, and the largest product of the counts
   of nested ones. *)
let max_regexp_repeat = 1000

(* Reports whether a quantifier is one the Go package writes with braces for the regexp package: every one but *, +
   and ?. *)
let is_counted (n : Parse.node) =
  not ((n.min = 0 && n.max = Parse.unbounded) || (n.min = 1 && n.max = Parse.unbounded) || (n.min = 0 && n.max = 1))

(* Reports whether the counted quantifiers at and below n nest to a product of at most limit. It is the rule of the
   same name in the regexp/syntax package of Go. *)
let rec repeat_is_valid (n : Parse.node) limit =
  let counted = n.kind = Parse.Repeat && is_counted n in
  let m = if counted then if n.max < 0 then n.min else n.max else 1 in
  if counted && n.max = 0 then true
  else if counted && m > limit then false
  else
    let limit = if counted && m > 0 then limit / m else limit in
    Array.for_all (fun sub -> repeat_is_valid sub limit) n.subs

(* Reports whether every counted quantifier at and below n is one the regexp package of Go accepts. The Go package
   runs a pattern on the backtracking matcher alone when the regexp package refuses its translation, which it does
   for these quantifiers. The rule is kept here so that the same patterns are tried for an automaton, and because it
   bounds the work of building one (see Dfa.build). The other limits of the regexp package, on the size and the depth
   of a pattern, are not kept: a pattern beyond them is too large for an automaton in any case. *)
let rec within_regexp_limits (n : Parse.node) =
  Array.for_all within_regexp_limits n.subs
  && not
       (n.kind = Parse.Repeat && is_counted n
       && (n.min > max_regexp_repeat || n.max > max_regexp_repeat
          || ((n.min >= 2 || n.max >= 2) && not (repeat_is_valid n max_regexp_repeat))))

(* compile with a way to leave out the automaton, which the tests use to compare the back ends. *)
let compile_with pattern force_backtrack =
  let parsed =
    match Parse.parse pattern true with
    | Ok (root, ps) -> Ok (root, ps, true)
    | Error message -> (
        match Parse.parse pattern false with Ok (root, ps) -> Ok (root, ps, false) | Error _ -> Error message)
  in
  match parsed with
  | Error message -> Error message
  | Ok (root, ps, unicode) ->
      let regular = (not ps.needs_backtracking) && within_regexp_limits root in
      let dfa = if regular && not force_backtrack then Dfa.build root else None in
      let prog = Compile.compile_program root (max ps.groups ps.group_count) unicode in
      Ok { pattern; unicode; regular; dfa; prog; slot = Vm.new_state (); busy = Atomic.make false }

let compile pattern = compile_with pattern false
let compile_backtrack pattern = compile_with pattern true
let valid pattern = match Parse.parse pattern true with Ok _ -> true | Error _ -> false
let check_range name length off len = if off < 0 || len < 0 || off > length - len then invalid_arg name

type scratch = Vm.state

let create_scratch = Vm.new_state

let is_match_with scratch re text off len =
  check_range "Corvus_json_schema_ecmaregex.is_match_with" (Bytes.length text) off len;
  let answer = match re.dfa with Some d -> Dfa.match_bytes d text off (off + len) | None -> Dfa.undecided in
  if answer <> Dfa.undecided then answer = Dfa.matched else Vm.match_bytes re.prog scratch text off (off + len)

let is_match_sub_with scratch re text off len =
  check_range "Corvus_json_schema_ecmaregex.is_match_sub_with" (String.length text) off len;
  let answer = match re.dfa with Some d -> Dfa.match_string d text off (off + len) | None -> Dfa.undecided in
  if answer <> Dfa.undecided then answer = Dfa.matched else Vm.match_string re.prog scratch text off (off + len)

let is_match re text off len =
  check_range "Corvus_json_schema_ecmaregex.is_match" (Bytes.length text) off len;
  let answer = match re.dfa with Some d -> Dfa.match_bytes d text off (off + len) | None -> Dfa.undecided in
  if answer <> Dfa.undecided then answer = Dfa.matched
  else if Atomic.compare_and_set re.busy false true then (
    match Vm.match_bytes re.prog re.slot text off (off + len) with
    | matched ->
        Atomic.set re.busy false;
        matched
    | exception e ->
        Atomic.set re.busy false;
        raise e)
  else
    (* Another thread or domain is matching with the memory of the pattern. This match makes its own. *)
    Vm.match_bytes re.prog (Vm.new_state ()) text off (off + len)

let is_match_sub re text off len =
  check_range "Corvus_json_schema_ecmaregex.is_match_sub" (String.length text) off len;
  let answer = match re.dfa with Some d -> Dfa.match_string d text off (off + len) | None -> Dfa.undecided in
  if answer <> Dfa.undecided then answer = Dfa.matched
  else if Atomic.compare_and_set re.busy false true then (
    match Vm.match_string re.prog re.slot text off (off + len) with
    | matched ->
        Atomic.set re.busy false;
        matched
    | exception e ->
        Atomic.set re.busy false;
        raise e)
  else Vm.match_string re.prog (Vm.new_state ()) text off (off + len)

let is_match_string re text = is_match_sub re text 0 (String.length text)
let engine re = match re.dfa with Some _ -> Dfa | None -> Backtrack
let unicode re = re.unicode
let pattern re = re.pattern
let regular re = re.regular

module Internal = struct
  let automaton re text =
    match re.dfa with
    | None -> None
    | Some d ->
        let answer = Dfa.match_string d text 0 (String.length text) in
        if answer = Dfa.undecided then None else Some (answer = Dfa.matched)

  let canonicalize = Charset.canonicalize

  let fold_classes unicode_mode =
    let table = Charset.fold_classes unicode_mode in
    (table.points, table.classes)

  let property_resolves expr = Charset.property_set expr <> None
  let property_pairs = Charset.property_pairs
  let binary_names = Array.map fst Charset.binary_names
  let category_names = Array.map fst Charset.category_names

  let program_shape re =
    ( re.prog.loops,
      Array.fold_left (fun n (i : Compile.inst) -> if i.op = Compile.Op_backref then n + 1 else n) 0 re.prog.insts )

  let unpack packed = (Utf8.rune packed, Utf8.width packed)
  let unit_at text at = unpack (Utf8.unit_at { b = Bytes.empty; s = text; is_string = true } 0 at (String.length text))

  let unit_before text at =
    unpack (Utf8.unit_before { b = Bytes.empty; s = text; is_string = true } 0 at (String.length text))
end
