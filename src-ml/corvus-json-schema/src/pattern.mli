(** [pattern] and [patternProperties] matching with ECMA-262 semantics (the u flag), as JSON Schema specifies. A port of
    [pattern.go] of the Go module.

    A pattern gets the cheapest matcher that decides it exactly:

    - patterns every string matches ([""], [".*"], ["[\s\S]*"] and the like), [".+"], and line lengths (["^.{1,256}$"]);
    - anchored sequences of quantified ASCII character classes and literals (["^[a-z][a-z0-9_]{0,29}$"], ["^x-"],
      ["^[@$_#]"]), matched in one pass over the string;
    - alternatives of such sequences once groups are multiplied out (["^([a|A]uto)|([n|N]one)$"]), sets of literals, and
      separated lists (["^([a-z]+)(\.[a-z]+)*$"]);
    - anything else by the regular expression engine (see {!Pattern_engine}).

    Matching allocates nothing on the OCaml heap, whichever matcher runs. *)

type t
(** A compiled pattern. It is immutable, and safe to share between threads and domains. *)

val compile : string -> t option
(** [compile source] compiles a pattern. [None] when it is not a valid ECMA-262 regular expression. (compilePattern of
    the Go source.)

    There is no process-wide cache. The Go source keeps every compiled pattern in one, and this library keeps no
    process-wide table of patterns (its only process-wide state is the regular expression engine's case-folding tables,
    built once on first use), so each call compiles the pattern it is given. A caller that meets the same pattern many
    times (the schema compiler) shares the result itself. *)

val source : t -> string
(** [source p] is the text the pattern was compiled from. *)

val matches : t -> Pattern_engine.scratch -> Bytes.t -> int -> int -> bool -> bool
(** [matches p scratch text off len ascii] reports whether the pattern matches somewhere in the [len] bytes of [text]
    from [off], which are valid UTF-8. [ascii] says whether that range is known to be ASCII (a document knows that of
    its strings). It must be [false] when that is not known, and may be [false] for ASCII text. (match of the Go
    source.)

    It allocates nothing. Only a pattern matched by the engine uses [scratch], which one match at a time may hold, and
    such a match has the limit of the scratch (see {!Pattern_engine.set_limit}). Nothing is written to [text], and no
    byte outside the range is read.

    @raise Invalid_argument if [off] and [len] do not name a range of [text] and the matcher reads outside it.
    @raise Match_limit when the match reached the limit of the scratch. *)

exception Match_limit of string * bool
(** A match on the engine reached the limit of its scratch and was abandoned: the source of the pattern, and whether the
    memory of the match was the limit and not its time. It is not an answer. *)

val match_string : t -> Pattern_engine.scratch -> string -> bool
(** [match_string p scratch s] is {!matches} for a string that is not known to be ASCII, for the schema compiler and the
    collecting evaluator. It copies the string, so it allocates. (matchString of the Go source.) *)

val is_ascii : Bytes.t -> int -> int -> bool
(** [is_ascii text off len] reports whether the [len] bytes of [text] from [off] are all ASCII. It allocates nothing. *)

val kind_name : t -> string
(** [kind_name p] names the matcher the pattern takes, for the tests that check which one a pattern gets:
    ["everything"], ["literal"], ["sequence"], ["separated list"], ["has content"], ["line"], ["literals"],
    ["alternatives"], ["excluded class with word"] or ["engine"]. *)

(** {1 Internal, for the tests} *)

val shape : string -> t option
(** [shape source] is the faster matcher of a pattern when it has one (choosePattern of the Go source), with no test of
    the pattern's validity and without the engine. {!compile} is the entry point for everything but the tests, which use
    this to exercise the faster matchers by themselves. *)
