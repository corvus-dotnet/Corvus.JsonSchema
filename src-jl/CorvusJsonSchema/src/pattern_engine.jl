# The regular expression engine behind every pattern that has no faster matcher, and the Unicode tables that come
# with it: the EcmaRegex submodule (src/ecmaregex). This file is the only one that names the engine.

include("ecmaregex/EcmaRegex.jl")

# The ranges of a property expression of \p{...}, such as "Lu" or "Script=Greek", as inclusive pairs in ascending
# order.
unicode_property(expression::String) = copy(EcmaRegex.property(expression)::Vector{Int32})

# A compiled pattern of the engine.
const EnginePattern = EcmaRegex.Pattern

"""
    PatternMatchError

Thrown by a validation when one match of a `pattern` or `patternProperties` regular expression of the schema was
given up because it reached a limit. Whether the string matches is then not known, so it is never reported as valid
or as invalid. The field `limit` says which limit:

- `:time`: the match ran for the limit on its time, which is the `pattern_match_limit` of
  [`compile_schema`](@ref), one second unless it is set;
- `:memory`: the match needed more than the 64 MiB the regular expression engine may take for one match. A pattern
  that repeats a group reaches that on a string of some hundreds of thousands of characters;
- `:steps`: the match used its steps and could not be timed. That is a match with the limit on time turned off
  that took the 4,294,967,295 steps that are the most PCRE2 can be told to allow, and a match of a pattern too
  large for PCRE2 to hold with what times a match (an alternation of 3,000 words is one) that used the steps it
  has before it is timed, which is sooner than the limit on its time.

`pattern` is the pattern and `message` names the limit and its value.
"""
const PatternMatchError = EcmaRegex.MatchError

"""
    PatternTooLargeError

Thrown by [`compile_schema`](@ref) for a schema one of whose `pattern` or `patternProperties` regular expressions
is over a limit of size. It is not the error of a pattern that is not valid, which is a [`CompileError`](@ref): the
field `valid` says whether the whole pattern was read before the limit was met, in which case it is a valid
ECMA-262 regular expression that is too large to run, and otherwise it is too large to read and nothing is known of
whether it is valid. `pattern` is the pattern and `limit` names the limit and its value.

The limits: a pattern of at most 1,048,576 bytes of UTF-8, groups nested at most 200 deep, and a translation for
the regular expression engine (PCRE2) of at most 1,048,576 bytes, written in at most 524,288 steps, that PCRE2
takes. PCRE2, as Julia builds it, holds a compiled pattern of at most 64 KiB, so that is what a pattern of a few
hundred characters with large counts of groups meets.
"""
const PatternTooLargeError = EcmaRegex.TooLargeError

# The limit on the time of one match by the engine.
const PatternMatchLimit = EcmaRegex.MatchLimit

# Compiles an ECMA-262 pattern (with the u flag, or failing that without it, as many schemas need). Nothing when the
# pattern is invalid. A pattern that is valid and that the engine cannot run with the same meaning is a compilation
# error that says so. A pattern over a limit of size is a PatternTooLargeError, which the engine throws.
function compile_engine(source::String)
    try
        return EcmaRegex.compile(source)
    catch err
        err isa EcmaRegex.PatternError || rethrow()
        err.unsupported && throw(CompileError(sprint(showerror, err)))
        return nothing
    end
end

# Reports whether the pattern matches somewhere in UTF-8 text.
@inline engine_match(p::EnginePattern, s::Bytes, limit::PatternMatchLimit) =
    EcmaRegex.ismatch(p, view(s.b, s.off+1:s.off+s.len), limit)

# The bytes the engine keeps for a compiled pattern.
engine_bytes(p::EnginePattern) = EcmaRegex.keptbytes(p)

# Reports whether a string is a valid ECMA-262 regular expression with the u flag (the regex format, and the
# patterns the engine reads without falling back).
valid_regex(source::String) = EcmaRegex.isvalidpattern(source)
valid_regex(source::Bytes) = valid_regex(String(source))
