(** The regular expression engine behind every pattern that has no faster matcher. This module is the only one that
    names it. A port of [pattern_engine.go] of the Go module. *)

type t = Corvus_json_schema_ecmaregex.t
(** A compiled pattern of the engine, which matches a pattern anywhere in UTF-8 text. It is immutable and safe to share
    between threads and domains. *)

type scratch = Corvus_json_schema_ecmaregex.scratch
(** The working memory of a match. One match at a time may use a scratch, so each thread or domain, or each evaluation,
    has its own. *)

val create_scratch : unit -> scratch
(** [create_scratch ()] makes working memory for {!is_match}. It can serve any number of compiled patterns, one match at
    a time. *)

(** Why a pattern was not compiled. *)
type error =
  | Invalid  (** The pattern is not a valid ECMA-262 regular expression. *)
  | Too_large of string * int * string
      (** The pattern is over a limit of size: the name of the limit, its value, and what the engine says of it. *)

val compile : string -> (t, error) result
(** [compile source] compiles an ECMA-262 pattern (with the u flag, or failing that without it, as many schemas need).
    (compileEngine of the Go source.) *)

val check : string -> (bool, error) result
(** [check source] reads a pattern without compiling it. It is [Ok true] for a pattern valid with the u flag, [Ok false]
    for one valid only without it, and the error {!compile} would return otherwise. (checkRegex of the Go source.) *)

val valid : string -> bool
(** [valid source] reports whether a string is a valid ECMA-262 regular expression with the u flag (the regex format,
    and the patterns the engine reads without falling back). (validRegex of the Go source.) *)

val is_match : scratch -> t -> Bytes.t -> int -> int -> bool
(** [is_match scratch re text off len] reports whether the pattern matches somewhere in the [len] bytes of [text] from
    [off]. It allocates nothing once the scratch has grown to what the match needs.

    @raise Invalid_argument if [off] and [len] do not name a range of [text].
    @raise Match_time when the scratch has a limit and the match ran for longer.
    @raise Match_memory when the scratch has a limit and the match needed more backtracking memory than a match may. *)

exception Match_time
(** A match was abandoned because it ran for longer than the limit of its scratch. It is not an answer. *)

exception Match_memory
(** A match was abandoned because its backtracking stack reached the cap a match with a limit has. It is not an answer.
*)

val set_limit : scratch -> float -> (unit -> float) option -> unit
(** [set_limit scratch seconds clock] gives every match with the scratch from now on a limit of that many seconds,
    counted on [clock], a function that returns the time in seconds, or on the processor time of the process when it is
    [None]. Zero or less is no limit. *)

val create_limited_scratch : float -> (unit -> float) option -> scratch
(** [create_limited_scratch seconds clock] is {!create_scratch} followed by {!set_limit}. *)
