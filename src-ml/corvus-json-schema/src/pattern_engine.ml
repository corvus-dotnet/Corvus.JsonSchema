(* The regular expression engine behind every pattern that has no faster matcher. This file is the only one that names
   it. *)

module Engine = Corvus_json_schema_ecmaregex

(* A compiled pattern of the engine matches a pattern anywhere in UTF-8 text. *)
type t = Engine.t
type scratch = Engine.scratch

let create_scratch = Engine.create_scratch

(* Why a pattern was not compiled: it is not valid, or it is over a limit of size, with the name of the limit, its
   value and what the engine says of it. *)
type error = Invalid | Too_large of string * int * string

let error_of (e : Engine.error) =
  match e with Invalid _ -> Invalid | Too_large (limit, value) -> Too_large (limit, value, Engine.error_message e)

(* compile compiles an ECMA-262 pattern (with the u flag, or failing that without it, as many schemas need). *)
let compile source = match Engine.compile source with Ok re -> Ok re | Error e -> Error (error_of e)

(* check reads a pattern without compiling it, and reports whether it is valid with the u flag. *)
let check source = match Engine.check source with Ok unicode -> Ok unicode | Error e -> Error (error_of e)

(* valid reports whether a string is a valid ECMA-262 regular expression with the u flag (the regex format, and the
   patterns the engine reads without falling back). *)
let valid source = Engine.valid source
let is_match scratch re text off len = Engine.is_match_with scratch re text off len

exception Match_time = Engine.Match_time
exception Match_memory = Engine.Match_memory

(* set_limit gives the matches with a scratch a limit of that many seconds, or none for zero, counted on the clock given
   or, without one, on the processor time of the process. *)
let set_limit scratch seconds clock =
  Engine.set_limit scratch seconds;
  Engine.set_clock scratch clock

(* create_limited_scratch makes working memory whose matches have that limit. *)
let create_limited_scratch seconds clock =
  let scratch = Engine.create_scratch () in
  set_limit scratch seconds clock;
  scratch
