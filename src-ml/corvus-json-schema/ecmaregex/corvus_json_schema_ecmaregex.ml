(* A stub. The engine follows. *)

type t = { pattern : string }
type engine = Dfa | Backtrack

let engine_name = function Dfa -> "dfa" | Backtrack -> "backtrack"
let compile (_ : string) : (t, string) result = Error "ecmaregex: not implemented yet"
let valid (_ : string) = false
let check_range name length off len = if off < 0 || len < 0 || off > length - len then invalid_arg name

let is_match (_ : t) text off len =
  check_range "Corvus_json_schema_ecmaregex.is_match" (Bytes.length text) off len;
  false

let is_match_sub (_ : t) text off len =
  check_range "Corvus_json_schema_ecmaregex.is_match_sub" (String.length text) off len;
  false

let is_match_string re text = is_match_sub re text 0 (String.length text)

type scratch = unit

let create_scratch () = ()
let is_match_with () re text off len = is_match re text off len
let is_match_sub_with () re text off len = is_match_sub re text off len
let engine (_ : t) = Backtrack
let unicode (_ : t) = true
let pattern re = re.pattern
let regular (_ : t) = false
let compile_backtrack = compile
