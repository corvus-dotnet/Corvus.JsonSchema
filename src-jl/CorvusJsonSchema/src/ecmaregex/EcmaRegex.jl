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
* repetitions that have to be written twice, inside one another, past 65,536 characters of translation, and a
  pattern PCRE2 does not compile, which a count of several thousand of a group of several characters is (a count
  of one character or class never is, see `blocks` in `emitter.jl`).

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
in which ECMA-262 tries the ways through it from its end, as in `(?<=(a*)(x+))-\\1`. A
lookbehind none of whose groups is read after it is never refused for this.

# The lookbehind exception

A pattern that is valid with the `u` flag is run and answers as V8 does, with one named exception, which is the two
lookbehind cases of the list above. Both need the lookbehind to be matched backwards, which PCRE2 cannot do:

* a group captured inside a lookbehind of no fixed length and read after it, outside the forms above, such as
  `(?<=(a*)(x+))-\\1`. The error says "a backreference to a group inside a lookbehind of no fixed length, where the
  group holds what only matching the lookbehind backwards can tell";
* a backreference inside a lookbehind to a group of it that is not in an end of it of a fixed length, such as
  `(?<=\\1(a+))x`. The error says "a backreference inside a lookbehind to a group that the lookbehind matches
  before it".

These are refused when the schema is compiled, and are never run with another meaning. (The limits on size, which
are the other refusals of the list, are not about what a pattern means.)

# Grammars

A pattern is read first with the grammar of the `u` flag, as JSON Schema's `pattern` specifies. A pattern that is
not valid with it is read with the Annex B grammar of a pattern with no flag, as many schemas hold such patterns
(identity escapes like `\\&`, lone braces). [`isvalidpattern`](@ref), which backs the `regex` format, accepts only
the `u` flag grammar.

What is a pattern is what ECMA-262 says, and the answers were checked against V8 13.6, which departs from it in a
few places. V8 does not know the Script value `Katakana_Or_Hiragana` (`Hrkt`), which Unicode lists and no character
has, and it takes a count whose first number is the greater when both are past 2^31 - 1
(`a{2147483648,2147483647}`). Here the first is a name and the second an error. The data of the package is Unicode
17, so it has the four scripts that version added and V8 13.6 has not.

# Unicode

* A pattern that is valid with the `u` flag matches Unicode code points over UTF-8 text. PCRE2 is given the
  `PCRE2_UTF` option for that.
* A pattern that is only valid with no flag matches UTF-16 code units, as ECMA-262 says: a character beyond the
  Basic Multilingual Plane is two of them, to the dot, a class, a quantifier, an assertion and a backreference, so
  `^\\-?.\$` does not match U+10400. PCRE2 has no such matching over UTF-8, so the surrogate U+D800 + n is written
  as the character U+F0000 + n, in the pattern and in the text. The parser reads such a pattern as code units (a
  group name apart, which is read by character in both grammars), and [`ismatch`](@ref) writes a text that has a
  character of four bytes again with each half of its surrogate pair as one of those characters (see
  `codeunits!`). A text within the Basic Multilingual Plane is matched as it is.
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
* A surrogate (U+D800 to U+DFFF) is not a character of UTF-8 text. A pattern that is valid with the `u` flag may
  name one, as `\\uD800` or within `\\p{Cs}` or a negated class, and it matches nothing. A pair of surrogate escapes
  is one character there, as in ECMA-262. In a pattern that is only valid with no flag a surrogate is a code unit,
  half of a character beyond the Basic Multilingual Plane, and two escapes are two code units.

# Matching

[`ismatch`](@ref) may be called from several tasks at once. In the steady state it allocates nothing. A text with a
character beyond the Basic Multilingual Plane is written again as code units for a pattern that is only valid with
no flag, into a buffer that is kept (see `matchunits`). What a match writes to belongs to the thread it runs on.
PCRE2's JIT compiler is used where the PCRE2 of the Julia in use has one, and the interpreter runs a pattern the JIT
compiler does not take and a match too deep for the stack of JIT code.

A match that reaches a limit throws a [`MatchError`](@ref). It is never reported as no match. The limits are on the
steps of a match (`MATCH_LIMIT`, 500 million, where PCRE2 by itself has ten million), on the memory of the
interpreter (`HEAP_LIMIT_KIB`, 64 MiB), and one of this module on the steps of the searches for lookbehinds
(`STEP_LIMIT`). They are set in the match context a thread keeps, so a match sets nothing.

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

Whether the pattern matches anywhere in the UTF-8 text (an unanchored search). It is safe to call from several tasks
at once. Throws a [`MatchError`](@ref) when PCRE2 reaches one of its limits. In the steady state it allocates nothing.
A pattern that is only valid with no flag is matched by UTF-16 code unit, so a text with a character beyond the Basic
Multilingual Plane is written again for it, into a buffer that the module keeps and uses again (one that has grown
past 4 MiB is not kept, so such a match of a text of more than 2 MiB allocates).

The bytes of a `Vector{UInt8}`, of a contiguous view of one, of the code units of a `String` and of a `String` or
`SubString{String}` are matched where they are. Any other vector or string is copied first.
"""
function ismatch end

const ContiguousBytes = Union{DenseVector{UInt8},
    Base.FastContiguousSubArray{UInt8,1,<:DenseVector{UInt8}}}

function ismatch(p::Pattern, text::ContiguousBytes)::Bool
    if !p.unicode && beyondbmp(text)
        return matchunits(p, text)
    end
    GC.@preserve p text begin
        return unsafe_ismatch(p, pointer(text), length(text))
    end
end

function ismatch(p::Pattern, text::Union{String,SubString{String}})::Bool
    if !p.unicode && beyondbmp(codeunits(text))
        return matchunits(p, codeunits(text))
    end
    GC.@preserve p text begin
        return unsafe_ismatch(p, pointer(text), ncodeunits(text))
    end
end

# Whether UTF-8 text has a character beyond the Basic Multilingual Plane, which is one of four bytes.
function beyondbmp(text::AbstractVector{UInt8})
    for b in text
        b >= 0xF0 && return true
    end
    return false
end

"""
    codeunits!(out::Vector{UInt8}, text::AbstractVector{UInt8}) -> Vector{UInt8}

Writes UTF-8 text as the translation of a pattern without the `u` flag reads it, where each UTF-16 code unit is one
character. A character beyond the Basic Multilingual Plane is two code units, a surrogate pair, and the surrogate
U+D800 + n is written as the character U+F0000 + n, so that PCRE2, which matches by character, takes the two halves
one at a time. Every other character is written as it is. Bytes that are not UTF-8 are copied. `out` is given the
length of what is written, and it allocates only when it has to grow.
"""
function codeunits!(out::Vector{UInt8}, text::AbstractVector{UInt8})
    # A character of four bytes is written as two of four, and nothing else grows.
    resize!(out, 2 * length(text))
    n = 0
    i = firstindex(text)
    last = lastindex(text)
    while i <= last
        b = text[i]
        if b >= 0xF0 && b <= 0xF4 && i + 3 <= last && (text[i+1] & 0xC0) == 0x80 && (text[i+2] & 0xC0) == 0x80 &&
           (text[i+3] & 0xC0) == 0x80
            c = (UInt32(b & 0x07) << 18) | (UInt32(text[i+1] & 0x3F) << 12) | (UInt32(text[i+2] & 0x3F) << 6) |
                UInt32(text[i+3] & 0x3F)
            if c >= 0x10000 && c <= 0x10FFFF
                v = c - 0x10000
                for unit in (v >> 10, 0x400 + (v & 0x3FF))
                    # U+F0000 + unit, which is below U+F0800.
                    out[n+1] = 0xF3
                    out[n+2] = 0xB0
                    out[n+3] = 0x80 | ((unit >> 6) % UInt8)
                    out[n+4] = 0x80 | ((unit & 0x3F) % UInt8)
                    n += 4
                end
                i += 4
                continue
            end
        end
        n += 1
        out[n] = b
        i += 1
    end
    return resize!(out, n)
end

# The buffers that texts are written into as code units. A match takes one and puts it back, so that in the steady
# state it allocates nothing: there are as many as matches of this kind have run at once, and each has grown to the
# longest text it was given. One that has grown past MAX_KEPT_UNITS bytes is not kept, so that one long string does
# not hold its size in memory for the life of the process.
const UNIT_BUFFERS = Vector{UInt8}[]
const UNIT_BUFFER_LOCK = Threads.SpinLock()
const MAX_KEPT_UNITS = 4 * 1024 * 1024

# A match of a pattern without the u flag against text with a character beyond the Basic Multilingual Plane. The
# text is written again as code units, into a buffer that is kept.
@noinline function matchunits(p::Pattern, text::AbstractVector{UInt8})::Bool
    lock(UNIT_BUFFER_LOCK)
    units = isempty(UNIT_BUFFERS) ? nothing : pop!(UNIT_BUFFERS)
    unlock(UNIT_BUFFER_LOCK)
    if units === nothing
        units = UInt8[]
    end
    try
        codeunits!(units, text)
        GC.@preserve p units begin
            return unsafe_ismatch(p, pointer(units), length(units))
        end
    finally
        if length(units) <= MAX_KEPT_UNITS
            lock(UNIT_BUFFER_LOCK)
            push!(UNIT_BUFFERS, units)
            unlock(UNIT_BUFFER_LOCK)
        end
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
