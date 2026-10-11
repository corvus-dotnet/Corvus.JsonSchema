(* The regular expression engine behind every pattern that has no faster matcher. This file is the only one that names
   it. *)

module Engine = Corvus_json_schema_ecmaregex

(* A compiled pattern of the engine matches a pattern anywhere in UTF-8 text. *)
type t = Engine.t
type scratch = Engine.scratch

let create_scratch = Engine.create_scratch

(* compile compiles an ECMA-262 pattern (with the u flag, or failing that without it, as many schemas need). None when
   the pattern is invalid. *)
let compile source = match Engine.compile source with Ok re -> Some re | Error _ -> None

(* valid reports whether a string is a valid ECMA-262 regular expression with the u flag (the regex format, and the
   patterns the engine reads without falling back). *)
let valid source = Engine.valid source
let is_match scratch re text off len = Engine.is_match_with scratch re text off len

exception Match_time = Engine.Match_time
exception Match_memory = Engine.Match_memory

let set_limit = Engine.set_limit

(* create_limited_scratch makes working memory whose matches have a limit of that many seconds, or none for zero. *)
let create_limited_scratch seconds =
  let scratch = Engine.create_scratch () in
  Engine.set_limit scratch seconds;
  scratch
