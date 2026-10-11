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

    A pattern the u flag grammar accepts is matched by Unicode code point over the UTF-8 text. The dot, a class and a
    quantifier each see a character beyond the Basic Multilingual Plane as one character, and a surrogate pair written
    as two [\u] escapes names one code point.

    A pattern that only the Annex B grammar accepts is matched by UTF-16 code unit, as ECMA-262 matches a pattern with
    no u flag, and as the Go package and the Object Pascal port do. A character beyond the Basic Multilingual Plane is
    then two characters, a high surrogate and a low one, to the dot, a class, a quantifier, a group and a backreference.
    A match can start or end between the two. Such a character in the pattern is two atoms, and a class range between
    two of them is an error. Case is folded by the Canonicalize of a pattern with no u flag. The text stays UTF-8 and no
    UTF-16 copy of it is made (see utf8.ml). Only a text with a character beyond the Basic Multilingual Plane is read
    differently by the two.

    Malformed UTF-8 in the text is read one byte at a time as U+FFFD. The text is UTF-8, so it cannot hold half of a
    surrogate pair, and a pattern that names one, such as [\ud800], matches it only as half of a character and only when
    the pattern has no u flag.

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
    allocate once the stack has grown. A pattern with nested quantifiers can take exponential time on a text it does not
    match, as it can in any backtracking engine, so a match can be given a limit (see the limits below). A repetition
    whose body cannot consume a character is not such a pattern. It is run once, or not at all (see simplify of
    compile.ml), as V8 runs it. And a counted loop gives up at a position from which the rest of the pattern needs more
    text than is left (see set_needs of compile.ml), as V8 does, so a loop whose iterations can be empty does not try
    every combination of them there.

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

    - A pattern has three limits of size: {!max_pattern_length} bytes, groups {!max_nesting} deep, and a size of
      {!max_size} as the parser counts it. A pattern over one is refused with {!Too_large}, which is not the error of a
      pattern that is not valid.
    - The bounds of a counted quantifier saturate at 2,147,483,647.
    - A pattern is tried for an automaton when it is {!regular}. It gets one when the automaton has at most 256 states
      built from at most 512 states of the pattern in at most 400,000 steps. Finding out that a pattern is too large
      costs about a millisecond when it is compiled.
    - A match with a {!scratch} that has a limit ({!set_limit}) is abandoned when it has run for longer than the limit
      ({!Match_time}), or when its backtracking stack has reached {!max_stack} entries ({!Match_memory}). The matcher
      counts its steps and reads the clock every {!check_every} of them. A match with no limit has neither bound.
    - The working memory of the matcher keeps what the largest match needed, up to 65,536 entries of four words. A match
      that needed more gives the memory back when it ends.

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

(** Why a pattern was not compiled. *)
type error =
  | Invalid of string  (** The pattern is not valid under either grammar, with the message of the u flag grammar. *)
  | Too_large of string * int
      (** The pattern is over a limit of size: the name of the limit (["length"], ["nesting"] or ["size"]) and its
          value. The pattern was not read to its end, so this says nothing of whether it is valid. *)

val error_message : error -> string
(** [error_message e] is what the engine says of the error. For a pattern that is too large it is
    ["the pattern is longer than 1048576 bytes"], ["the groups of the pattern nest more than 1000 deep"] or
    ["the pattern has a size of more than 1048576 bytes when it is read"]. *)

val max_pattern_length : int
(** The longest pattern that is read, in bytes of UTF-8: 1,048,576. *)

val max_nesting : int
(** The deepest groups may nest: 1,000. It bounds the recursion of the parser and of the compilers. *)

val max_size : int
(** The largest size a pattern may have, 1,048,576, in the bytes the parser counts for it as it reads: 64 for each term
    (an atom and the repetition or group around it) and 32 for each further alternative, 8 for each range of a class as
    its members are read, for each range that case folding or a negated property builds, and for each enclosing
    alternative and each group of the same name that a named group is compared with, and 1 for each character with a
    case variant in a class that is folded. The syntax tree, the program and the time to make them are proportional to
    this figure. The Go package and the Object Pascal port count the same figure for every pattern. *)

val compile : string -> (t, error) result
(** [compile pattern] compiles an ECMA-262 pattern (UTF-8) with the semantics JSON Schema requires. The u flag grammar
    is tried first, then Annex B. It returns [Error (Invalid message)] for a pattern that neither accepts, and
    [Error (Too_large _)] for a pattern over a limit of size, whether or not it is valid. *)

val valid : string -> bool
(** [valid pattern] reports whether the pattern is a valid ECMA-262 pattern (for [format: regex]). It applies the
    grammar of the u flag alone. A pattern over a limit of size is not read to its end and is reported as not valid. *)

val check : string -> (bool, error) result
(** [check pattern] reads the pattern without compiling it. It is [Ok true] for a pattern valid with the u flag grammar,
    [Ok false] for one valid only with the Annex B grammar, and the error {!compile} would return otherwise. *)

val is_match : t -> Bytes.t -> int -> int -> bool
(** [is_match re text off len] reports whether the pattern matches anywhere in the [len] bytes of [text] from [off],
    which are UTF-8 (an unanchored search). It allocates nothing in the steady state and is safe for one compiled
    pattern used from several threads or domains (see the notes on sharing above). It has no limit of time.

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
    compiled patterns, one match at a time. It has no limit until {!set_limit} gives it one. *)

(** {1 The limit of a match} *)

val set_limit : scratch -> float -> unit
(** [set_limit scratch seconds] gives every match with the scratch from now on a limit of that many seconds. Zero or
    less, or a value that is not a number, is no limit.

    The time is counted on the clock of the scratch (see {!set_clock}), which is the processor time of the process
    unless the caller gives another. The clock is read every {!check_every} steps of the backtracking matcher and starts
    at the first read, so a match that ends before then never reads it, and a match goes past its limit by the time
    those steps take. The automaton takes time proportional to the text and is not stopped. *)

val set_clock : scratch -> (unit -> float) option -> unit
(** [set_clock scratch clock] sets the clock the limit of every match with the scratch is counted on from now on: a
    function that returns the time in seconds, or [None] for the default.

    The default is [Sys.time], the processor time of the process, which is the only clock of the standard library. It
    allocates nothing. It does not move while the process waits, so a match is abandoned for the time the process
    computed and not for the time that passed. And it is the time of the whole process, so in a program that computes on
    several domains at once it runs that many times faster, and a match there is abandoned sooner than its limit.

    A clock of the caller's, such as [Unix.gettimeofday] or a monotonic clock, has neither property. Each read of it
    allocates the float it returns. When such a clock is set forward during a match the match is abandoned early, and
    when it is set back the limit is counted again from the new time. *)

val limit : scratch -> float
(** [limit scratch] is the limit of the scratch in seconds, or [0.] for none. *)

exception Match_time
(** A match was abandoned because it ran for longer than its limit. It is not an answer. *)

exception Match_memory
(** A match was abandoned because its backtracking stack reached {!max_stack} entries, which only a match with a limit
    is held to. It is not an answer. *)

val check_every : int
(** The number of steps the backtracking matcher takes between two looks at the clock and at the depth of its stack:
    4,096. A step is one instruction, and eight characters of a single character loop or of a backreference. *)

val max_stack : int
(** The number of entries the backtracking stack of a match with a limit may hold: 1,048,576, of four words each, which
    is 32 MB. *)

val is_match_with : scratch -> t -> Bytes.t -> int -> int -> bool
(** [is_match_with scratch re text off len] is {!is_match} with working memory the caller owns. It never contends with
    another thread or domain and allocates nothing once the scratch has grown.

    It raises [Invalid_argument] if [off] and [len] do not name a range of [text], and {!Match_time} or {!Match_memory}
    when the scratch has a limit and the match reached it. *)

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

val compile_backtrack : string -> (t, error) result
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

  val unit_at : string -> int -> int * int
  (** [unit_at text at] is the UTF-16 code unit at the index [at] of the text and the number of bytes it spans, as the
      matcher reads it for a pattern with no u flag. A half of a character of four bytes spans two. *)

  val program_shape : t -> int * int
  (** [program_shape re] is the number of counted loops of the program of the backtracking matcher, and the number of
      its instructions that compare a backreference by its bytes. *)

  val needs : t -> int * int array
  (** [needs re] is the least number of bytes a match of the program of the backtracking matcher consumes, and for each
      of its counted loops, in order, the least that what follows the loop consumes. *)

  val iterations : t -> int array
  (** [iterations re] is the least number of bytes one iteration of each counted loop of the program consumes, in order.
  *)

  val no_match : int
  (** The need of a program that no text matches. *)

  val prefix : t -> string
  (** [prefix re] is the literal text the search for a match looks for at each position of the text: the first bytes, 16
      at most, of the literal text every match starts with, or [""]. *)

  val size : string -> bool -> int
  (** [size pattern unicode] is the size the parser counts for the pattern (see {!max_size}) under the u flag grammar or
      under the Annex B grammar, or -1 when that grammar does not accept it or the pattern is over a limit. *)

  val unit_before : string -> int -> int * int
  (** [unit_before text at] is the code unit that ends at the index [at] of the text and the number of bytes it spans.
  *)
end
