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

val compile : string -> t option
(** [compile source] compiles an ECMA-262 pattern (with the u flag, or failing that without it, as many schemas need).
    [None] when the pattern is invalid. (compileEngine of the Go source.) *)

val valid : string -> bool
(** [valid source] reports whether a string is a valid ECMA-262 regular expression with the u flag (the regex format,
    and the patterns the engine reads without falling back). (validRegex of the Go source.) *)

val is_match : scratch -> t -> Bytes.t -> int -> int -> bool
(** [is_match scratch re text off len] reports whether the pattern matches somewhere in the [len] bytes of [text] from
    [off]. It allocates nothing once the scratch has grown to what the match needs.

    @raise Invalid_argument if [off] and [len] do not name a range of [text]. *)
