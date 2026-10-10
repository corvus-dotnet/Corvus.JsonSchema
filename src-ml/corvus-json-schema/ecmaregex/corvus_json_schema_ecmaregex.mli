(** ECMA-262 regular expressions matched against UTF-8 text, with the semantics JSON Schema requires of the [pattern]
    and [patternProperties] keywords and of the [regex] format. It is a port of the ecmaregex package of the Go module
    ([src-go/corvus-json-schema/internal/ecmaregex]), with the choice of back end of the Object Pascal port.

    {b This library is internal to the corvus-json-schema package.} It is installed because the package is built from
    it, and it is not a supported interface: it can change in any release. Programs use {!Corvus_json_schema}.

    {1 Unicode mode}

    A pattern is read first with the grammar of the ECMA-262 u flag, which is what JSON Schema specifies. A pattern that
    is not valid under that grammar is then read with the Annex B grammar of a pattern with no flag, which accepts what
    many schemas in the wild contain (identity escapes such as [\&], a lone brace, legacy octal escapes). Only a pattern
    that neither grammar accepts is an error. This is the choice the Rust, Java, Go and Object Pascal ports make.
    {!valid}, which backs the regex format, accepts only the u flag grammar, as those ports do.

    Whichever grammar accepts the pattern, matching is by Unicode code point over the UTF-8 text. The dot, a class and a
    quantifier each see a character beyond the Basic Multilingual Plane as one character, and a surrogate pair written
    as two [\u] escapes names one code point. Malformed UTF-8 in the text is read one byte at a time as U+FFFD.

    JSON Schema gives a pattern no flags, so matching is case sensitive, [^] and [$] match only at the ends of the text,
    and the dot excludes the four line terminators. A pattern can change that for part of itself with a modifier group
    of ECMAScript 2025, such as [(?i:...)], [(?m:...)] or [(?s:...)].

    {1 Back ends, and the one difference from the Go package}

    The Go package hands a pattern with no lookaround, no backreference, no multiline anchor and no case-insensitive
    word boundary to the regexp package of the Go standard library. OCaml has no such library and this one uses none, so
    that translation is not ported. Every pattern runs on the two matchers of the library itself, as in the Object
    Pascal port.

    A pattern of that kind gets a deterministic automaton over ASCII text when it is small enough and has no word
    boundary, which decides an ASCII text with one table read per byte. Every other case runs on the backtracking
    matcher: a pattern with no automaton, and a text that is not ASCII for a pattern that has one. That matcher keeps
    its choices on an explicit stack, so a long text cannot overflow the stack of the thread and a match does not
    allocate once the stack has grown. It has no step budget, so a pattern with nested quantifiers can take exponential
    time on a text it does not match, as it can in any backtracking engine.

    The automaton is built when the pattern is compiled, where Go builds it on the first match.

    {1 Sharing between threads and domains}

    A compiled pattern may be used by any number of threads and domains at once. The automaton and the program of the
    backtracking matcher are never written to after {!compile} returns. The backtracking matcher needs working memory
    for a match (its stack and its captures), and there are two ways to give it some.

    - {!is_match}, {!is_match_sub} and {!is_match_string} use working memory kept in the compiled pattern, which one
      match at a time may hold. The holder is chosen with an [Atomic] compare and set, which is sound on OCaml 4.14 with
      system threads and on OCaml 5 with domains. A match that finds the memory held by another thread or domain makes
      its own for that one match, so it is still correct, but it allocates.
    - {!is_match_with} and {!is_match_sub_with} use a {!scratch} the caller owns. A caller that matches from several
      threads or domains gives each its own, and then no match allocates and none contends with another. This is the
      entry point for an evaluator, which owns per-evaluation state anyway.

    {1 The text}

    The text is a range of a [Bytes.t] or of a [string], given by an offset and a length. Neither is copied and the two
    cost the same. Nothing is written to the bytes. A caller must not change them from another thread while a match
    reads them.

    {1 Coverage and limits}

    The grammar is that of ECMAScript 2025. It includes lookbehind, named groups, group names shared between separate
    alternatives, modifier groups, and Unicode property escapes for every property, value and alias ECMA-262 lists, with
    Unicode 17 data from {!Corvus_json_schema_ucd}. The v flag (set notation and properties of strings) does not apply,
    because a JSON Schema pattern has no flags.

    The limits are those of the Go package and of the Object Pascal port.

    - Groups may nest 1000 deep. A pattern beyond that is not valid.
    - A pattern may be of any length. The bounds of a counted quantifier saturate at 2,147,483,647.
    - A pattern is tried for an automaton when it is {!regular}. It gets one when the automaton has at most 256 states
      built from at most 512 states of the pattern in at most 400,000 steps. Finding out that a pattern is too large
      costs about a millisecond when it is compiled.
    - The backtracking matcher has no step budget and no limit on its stack but memory. Its working memory keeps what
      the largest match needed, up to 65,536 entries of four words. A match that needed more gives the memory back when
      it ends.

    The library assumes the 63-bit [int] of a 64-bit OCaml. *)

type t
(** A compiled ECMA-262 pattern. It is safe to share between threads and domains. *)

(** The back end a pattern compiled to. *)
type engine =
  | Dfa
      (** The pattern has a deterministic automaton over ASCII text. A text that is not ASCII runs on the backtracking
          matcher. *)
  | Backtrack  (** Every text runs on the backtracking matcher. *)

val engine_name : engine -> string
(** The name of the engine, ["dfa"] or ["backtrack"]. *)

val compile : string -> (t, string) result
(** [compile pattern] compiles an ECMA-262 pattern (UTF-8) with the semantics JSON Schema requires. The u flag grammar
    is tried first, then Annex B. It returns [Error message] for a pattern that neither accepts, with the message of the
    u flag grammar. *)

val valid : string -> bool
(** [valid pattern] reports whether the pattern is a valid ECMA-262 pattern (for [format: regex]). It applies the
    grammar of the u flag alone. *)

val is_match : t -> Bytes.t -> int -> int -> bool
(** [is_match re text off len] reports whether the pattern matches anywhere in the [len] bytes of [text] from [off],
    which are UTF-8 (an unanchored search). It allocates nothing in the steady state and is safe for one compiled
    pattern used from several threads or domains (see the notes on sharing above).

    It raises [Invalid_argument] if [off] and [len] do not name a range of [text]. *)

val is_match_sub : t -> string -> int -> int -> bool
(** [is_match_sub re text off len] is {!is_match} for a range of a string. It costs the same.

    It raises [Invalid_argument] if [off] and [len] do not name a range of [text]. *)

val is_match_string : t -> string -> bool
(** [is_match_string re text] is {!is_match_sub} for a whole string. *)

type scratch
(** The working memory of the backtracking matcher: its stack and its captures. One match at a time may use a scratch.
    It grows to what the largest match so far needed and keeps that. *)

val create_scratch : unit -> scratch
(** [create_scratch ()] makes working memory for {!is_match_with} and {!is_match_sub_with}. It can serve any number of
    compiled patterns, one match at a time. *)

val is_match_with : scratch -> t -> Bytes.t -> int -> int -> bool
(** [is_match_with scratch re text off len] is {!is_match} with working memory the caller owns. It never contends with
    another thread or domain and allocates nothing once the scratch has grown.

    It raises [Invalid_argument] if [off] and [len] do not name a range of [text]. *)

val is_match_sub_with : scratch -> t -> string -> int -> int -> bool
(** [is_match_sub_with scratch re text off len] is {!is_match_with} for a range of a string.

    It raises [Invalid_argument] if [off] and [len] do not name a range of [text]. *)

val engine : t -> engine
(** [engine re] reports which back end the pattern compiled to. *)

val unicode : t -> bool
(** [unicode re] reports whether the pattern is valid with the ECMA-262 u flag. When it is not, the pattern was read
    with the Annex B grammar. *)

val pattern : t -> string
(** [pattern re] returns the source of the pattern. *)

val regular : t -> bool
(** [regular re] reports whether the pattern has no construct that only the backtracking matcher can run, and no counted
    quantifier beyond what the regexp package of Go accepts. These are the patterns the Go package runs on the regexp
    package, and the ones tried for an automaton here. *)

(** {1 For the tests} *)

val compile_backtrack : string -> (t, string) result
(** {!compile} without the automaton, so that every text runs on the backtracking matcher. The tests use it to compare
    the back ends. *)

(** What the tests read of the inside of the engine. It is not for any other use. *)
module Internal : sig
  val automaton : t -> string -> bool option
  (** [automaton re text] is the answer of the automaton of the pattern alone. It is [None] when the pattern has no
      automaton, and when the text is not ASCII, which the automaton does not decide. *)

  val canonicalize : int -> bool -> int
  (** [canonicalize r unicode_mode] is the Canonicalize function of ECMA-262 for case-insensitive matching, for the u
      flag grammar or for the Annex B grammar. *)

  val fold_classes : bool -> int array * int array array
  (** [fold_classes unicode_mode] is the table behind case-insensitive matching for the grammar: the characters that are
      equivalent to some other character, in ascending order, and for each the members of its equivalence class. *)

  val property_resolves : string -> bool
  (** [property_resolves expr] reports whether the expression between the braces of [\p{...}] names a property. *)

  val property_pairs : string -> int array option
  (** [property_pairs expr] returns the ranges of a property expression as inclusive pairs. *)

  val binary_names : string array
  (** Every name and alias of a binary property that a property expression can use. *)

  val category_names : string array
  (** Every name and alias of a General_Category value that a property expression can use. *)
end
