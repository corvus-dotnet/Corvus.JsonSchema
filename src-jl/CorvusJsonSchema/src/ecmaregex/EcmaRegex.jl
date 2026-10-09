"""
    EcmaRegex

ECMA-262 regular expressions, as JSON Schema's `pattern`, `patternProperties` and `format: regex` require them, run
by the PCRE2 that Julia bundles after an exact translation. A pattern that cannot be translated exactly is refused
with an error that names the construct. It is never run with another meaning.

The module is a port of `EcmaRegex.java` of the Java library, which does the same for `java.util.regex`. The parser,
the validator, the Unicode data and the analysis of backreferences are the same. What is written for the engine
differs where PCRE2 differs from `java.util.regex`. `emitter.jl` says how.

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
  version its PCRE2 has. The one thing left to PCRE2 is the comparison of a case-insensitive backreference, and only
  for text in which the characters with a case variant are ASCII letters, which every PCRE2 compares the same way.
  Any other case-insensitive backreference is refused.
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
both but for the form of a class of many ranges, which is a matter of speed. Three things are done because one of
them answers wrongly otherwise, and `emitter.jl` and `pcre.jl` say where: PCRE2 is told not to work out where a
match can start, a lookbehind is given to PCRE2 only when its length is fixed, and a class is written so that no
range of it crosses U+00FF.
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
