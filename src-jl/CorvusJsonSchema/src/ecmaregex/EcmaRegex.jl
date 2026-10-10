"""
    EcmaRegex

ECMA-262 regular expressions, as JSON Schema's `pattern`, `patternProperties` and `format: regex` require them, run
by the PCRE2 that Julia bundles after an exact translation. A pattern that cannot be translated exactly is refused
with an error that names the construct. It is never run with another meaning.

The module started as a port of `EcmaRegex.java` of the Java library, which does the same for `java.util.regex`. The
parser, the validator and the Unicode data are the same. What is written for the engine is PCRE2's own, and
`emitter.jl` says how.

# Backreferences and repetitions

A backreference means what it does in ECMA-262: the text its group captured when the group last took part, and the
empty string when it has not, where the groups of a repeated body stop having taken part when an iteration starts.
An iteration of a repetition that matches the empty string is refused once the minimum count is met, as ECMA-262
refuses it. PCRE2 does neither, so the translation writes both out (see `emitter.jl`). It is refused in four cases:

* a backreference inside a lookbehind to a group of the same lookbehind that is matched before it (one to its
  right), where that group is not in an end of the lookbehind of a fixed length, such as `(?<=\\1(a+))`;
* a backreference to a group inside a lookbehind of no fixed length, where what the group holds can only be told
  by matching the lookbehind backwards, as ECMA-262 does, such as `(?<=(a*)(x+))-\\1` (see Lookbehinds below);
* a case-insensitive backreference in a pattern that is only valid with no flag, where the group can hold a
  character that ECMA-262 compares differently there than PCRE2 does (see Unicode below);
* repetitions that have to be written twice, inside one another, past 65,536 characters of translation.

# Lookbehinds

ECMA-262 matches a lookbehind backwards, from its end to its start, and PCRE2 matches one forwards from a place it
steps back to. Whether the lookbehind holds is the same either way. What its groups capture is not, so a lookbehind
with a group that a backreference after it reads is written in a form that captures what ECMA-262 captures:

* a lookbehind of a fixed length is written as it is, with the groups of a repetition taken from its leftmost
  iteration;
* a group in an end of the lookbehind that has a fixed length is at a place the length fixes, whatever stands
  before it, as in `(?<=a*(x))-\\1`;
* a lookbehind that is a term of the pattern itself, and not inside a group, a repetition or an alternative, may
  start with a greedy repetition of a body of a fixed length, as in `(?<=([abc])+)x\\1`, or with alternatives of
  fixed lengths, as in `(?<=(ab|a)x)-\\1`, and may be alternatives of such forms. It is written as a search for
  where it starts, with the rest of the pattern inside the search. The search takes time that grows with the
  length of the text before the lookbehind;
* a part that can never match, and an alternative that is never taken, set no group.

Any other lookbehind whose groups are read after it is refused. That is one that is not a term of the pattern
itself and has no fixed length, as in `(?:(?<=(a)+)x\\1)+`, and one whose groups hold what depends on the order
in which ECMA-262 tries the ways through it from its end, as in `(?<=(a*)(x+))-\\1`. A lookbehind none of whose
groups is read after it is never refused for this.

# Grammars

A pattern is read first with the grammar of the `u` flag, as JSON Schema's `pattern` specifies. A pattern that is
not valid with it is read with the Annex B grammar of a pattern with no flag, as many schemas hold such patterns
(identity escapes like `\\&`, lone braces). [`isvalidpattern`](@ref), which backs the `regex` format, accepts only
the `u` flag grammar.

# Unicode

* Matching is by Unicode code point over UTF-8 text, whichever grammar accepted the pattern, as in the Java and Go
  ports. PCRE2 is given the `PCRE2_UTF` option for that. (V8 matches a pattern with no flag by UTF-16 code unit. No
  text of JSON is a sequence of code units, so there is nothing to be faithful to.)
* PCRE2 is not given `PCRE2_UCP`, and nothing that is written asks it what a character is. Every class and property
  is written as the ranges of its code points from Unicode data that the package carries (`UNICODE_VERSION`), and
  case-insensitive matching is written as the set of the characters ECMA-262 holds equivalent, from the same data.
  So `\\p{...}`, `\\w`, `\\d`, `\\s`, `\\b`, the dot and `(?i:...)` mean the same on every Julia, whatever Unicode
  version its PCRE2 has.
* The one thing left to PCRE2 is the comparison of a case-insensitive backreference, such as `(.)(?i:\\1)`. PCRE2
  compares by Unicode simple case folding, which is what ECMA-262 does with the `u` flag, in the Unicode version
  PCRE2 was built with. So a pattern that is valid with the `u` flag is always run, and its backreference holds two
  characters equivalent when that Unicode version does. The PCRE2 10.46 of Julia 1.13 has Unicode 16, as V8 13.6
  has, and the two agree on every pair of characters. The PCRE2 10.42 of Julia 1.10 has Unicode 14, and does not
  hold equivalent the thirty pairs Unicode 15.1 and 16 added, which the README lists. Nothing is written to make up
  for that: the fix is Julia 1.13. (The data of the package is Unicode 17, which added 28 pairs more. A literal or
  a class holds them equivalent on every Julia, and a backreference on neither of the two.)
* A pattern that is only valid with no flag compares by the uppercase mapping of one UTF-16 code unit, which is not
  what PCRE2 does for 78 characters of the Basic Multilingual Plane (PCRE2 holds U+212A equal to k and U+017F to
  s, and ECMA-262 does not there) and for the characters beyond it. Such a pattern is refused when a group that a
  case-insensitive backreference reads can hold one of those characters and a character with a case variant. The
  error says so. No pattern that is valid with the `u` flag is refused for this.
* The text should be well-formed UTF-8. It is not checked, so a match costs nothing for the check. PCRE2 is given
  `PCRE2_MATCH_INVALID_UTF`, with which a byte sequence that is not UTF-8 is not an error and not undefined: it
  matches no character, not even a negated class or the dot.
* A surrogate (U+D800 to U+DFFF) is not a character of UTF-8 text. A pattern may name one, as `\\uD800` or within
  `\\p{Cs}` or a negated class, and it matches nothing. A pair of surrogate escapes is one character, as in
  ECMA-262.

# Matching

[`ismatch`](@ref) allocates nothing and may be called from several tasks at once. What a match writes to belongs to
the thread it runs on. PCRE2's JIT compiler is used where the PCRE2 of the Julia in use has one, and the interpreter
runs a pattern the JIT compiler does not take and a match too deep for the stack of JIT code.

A match that reaches a limit throws a [`MatchError`](@ref). It is never reported as no match. The limits are those
of PCRE2 on the steps of a match (ten million) and on the memory of the interpreter (64 MiB here), and one of this
module on the steps of the searches for lookbehinds.

# PCRE2 versions

The module was written against PCRE2 10.42 (Julia 1.10) and 10.46 (Julia 1.13), and what it writes is the same on
both but for the form of a class of many ranges, which is a matter of speed. Four things are done because one of
them answers wrongly otherwise, and `emitter.jl` and `pcre.jl` say where: PCRE2 is told not to work out where a
match can start, a lookbehind is given to PCRE2 only when its length is fixed, a class is written so that no
range of it crosses U+00FF, and a repetition that never runs is not written.
"""
module EcmaRegex

export compile, ismatch, isvalidpattern

"""
    PatternError

Thrown by [`compile`](@ref) for a pattern that is not valid ECMA-262 (`unsupported` is false), or that is valid and
cannot be run with the same meaning (`unsupported` is true). `reason` says what is wrong, or which construct cannot
be run.
"""
struct PatternError <: Exception
    pattern::String
    reason::String
    unsupported::Bool
end

function Base.showerror(io::IO, e::PatternError)
    if e.unsupported
        print(io, "EcmaRegex.PatternError: the ECMA-262 pattern ", repr(e.pattern),
            " cannot be run with the same meaning by PCRE2: it holds ", e.reason)
    else
        print(io, "EcmaRegex.PatternError: ", repr(e.pattern), " is not an ECMA-262 pattern: ", e.reason)
    end
end

include("unicode_data.jl")
include("unicode.jl")
include("parser.jl")
include("emitter.jl")
include("validator.jl")
include("pcre.jl")

# Reads a pattern with the grammar of the u flag, and with the Annex B grammar when that one does not accept it.
function parsepattern(pattern::String)
    parser = Parser(pattern, true)
    try
        return parser, parse!(parser)
    catch e
        e isa PatternError || rethrow()
    end
    parser = Parser(pattern, false)
    return parser, parse!(parser)
end

function compileparsed(parser::Parser, root::Node)
    emitter = analyse!(Emitter(parser, root))
    compiled = pcrecompile(translation(emitter, false), parser.pattern, parser.unicode)
    if compiled isa String && emitter.haslookbehind
        # PCRE2 did not take a lookbehind as it is (one longer than it allows, for one). A callout has no limit.
        compiled = pcrecompile(translation(emitter, true), parser.pattern, parser.unicode)
    end
    if compiled isa String
        unsupported(emitter, string(REFUSED_BY_ENGINE, " (", compiled, ")"))
    end
    return compiled
end

"""
    compile(pattern::AbstractString) -> Pattern

Compiles an ECMA-262 pattern with the semantics JSON Schema requires. Throws a [`PatternError`](@ref) for a pattern
that is not valid ECMA-262, or that cannot be run exactly (the message says which construct).
"""
function compile(pattern::AbstractString)::Pattern
    parser, root = parsepattern(String(pattern))
    return compileparsed(parser, root)
end

"""
    ismatch(p::Pattern, text::AbstractVector{UInt8}) -> Bool
    ismatch(p::Pattern, text::AbstractString) -> Bool

Whether the pattern matches anywhere in the UTF-8 text (an unanchored search). It allocates nothing and is safe to
call from several tasks at once. Throws a [`MatchError`](@ref) when PCRE2 reaches one of its limits.

The bytes of a `Vector{UInt8}`, of a contiguous view of one, of the code units of a `String` and of a `String` or
`SubString{String}` are matched where they are. Any other vector or string is copied first.
"""
function ismatch end

const ContiguousBytes = Union{DenseVector{UInt8},
    Base.FastContiguousSubArray{UInt8,1,<:DenseVector{UInt8}}}

function ismatch(p::Pattern, text::ContiguousBytes)::Bool
    GC.@preserve p text begin
        return unsafe_ismatch(p, pointer(text), length(text))
    end
end

function ismatch(p::Pattern, text::Union{String,SubString{String}})::Bool
    GC.@preserve p text begin
        return unsafe_ismatch(p, pointer(text), ncodeunits(text))
    end
end

ismatch(p::Pattern, text::AbstractVector{UInt8}) = ismatch(p, Vector{UInt8}(text))
ismatch(p::Pattern, text::AbstractString) = ismatch(p, String(text))

const VALIDATORS = Validator[]
const VALIDATOR_LOCK = Threads.SpinLock()

"""
    isvalidpattern(pattern::AbstractString) -> Bool

Whether the pattern is a valid ECMA-262 pattern under the `u` flag (for `format: regex`). It does not compile the
pattern, and for a `String` it allocates nothing once the validators it keeps have grown.
"""
function isvalidpattern(pattern::String)::Bool
    lock(VALIDATOR_LOCK)
    validator = isempty(VALIDATORS) ? nothing : pop!(VALIDATORS)
    unlock(VALIDATOR_LOCK)
    if validator === nothing
        validator = Validator()
    end
    valid = validate(validator, pattern)
    lock(VALIDATOR_LOCK)
    push!(VALIDATORS, validator)
    unlock(VALIDATOR_LOCK)
    return valid
end

isvalidpattern(pattern::AbstractString) = isvalidpattern(String(pattern))

# For tests.

"""
    translate(pattern) -> Translation

The PCRE2 patterns an ECMA-262 pattern is run as, before PCRE2 has seen them. Throws as [`compile`](@ref) does, but
for a pattern PCRE2 does not compile.
"""
function translate(pattern::AbstractString)
    parser, root = parsepattern(String(pattern))
    return translation(analyse!(Emitter(parser, root)), false)
end

"""
    unsupportedreason(pattern) -> Union{Nothing,String}

Why a valid ECMA-262 pattern cannot be run, or nothing when it can be or is not valid.
"""
function unsupportedreason(pattern::AbstractString)
    try
        compile(pattern)
        return nothing
    catch e
        e isa PatternError || rethrow()
        return e.unsupported ? e.reason : nothing
    end
end

"""
    parsesunicode(pattern) -> Bool

Whether the parser reads the pattern with the `u` flag grammar, which is what the validator must agree with.
"""
function parsesunicode(pattern::AbstractString)
    try
        parse!(Parser(String(pattern), true))
        return true
    catch e
        e isa PatternError || rethrow()
        return false
    end
end

"""
    verdict(pattern) -> Int

The verdict on a pattern read with the `u` flag grammar only: 1 valid, 0 not ECMA-262, 2 valid ECMA-262 that cannot
be run with the same meaning.
"""
function verdict(pattern::AbstractString)
    parser = Parser(String(pattern), true)
    root = try
        parse!(parser)
    catch e
        e isa PatternError || rethrow()
        return 0
    end
    try
        compileparsed(parser, root)
        return 1
    catch e
        e isa PatternError || rethrow()
        return 2
    end
end

end
