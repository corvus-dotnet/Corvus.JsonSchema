# The regular expression engine behind every pattern that has no faster matcher, and the Unicode tables that come
# with it. This file is the only one that names the engine.
#
# TEMPORARY: until the EcmaRegex submodule (src/ecmaregex) loads and passes its tests, patterns run on Base.Regex
# (PCRE), which does not have ECMA-262's semantics exactly, and the Unicode tables are read from the submodule's data
# files alone.

module EngineUnicode
include("ecmaregex/unicode_data.jl")
include("ecmaregex/unicode.jl")
end

# The ranges of a property expression of \p{...}, such as "Lu" or "Script=Greek".
unicode_property(expression::String) = copy(EngineUnicode.property(expression)::Vector{Int32})

# A compiled pattern of the engine.
const EnginePattern = Regex

const SHIM_COMPILE_OPTIONS = Base.PCRE.UTF | Base.PCRE.MATCH_INVALID_UTF | Base.PCRE.DOLLAR_ENDONLY
const SHIM_UNICODE_OPTIONS = SHIM_COMPILE_OPTIONS | Base.PCRE.UCP

# Compiles an ECMA-262 pattern (with the u flag, or failing that without it, as many schemas need). Nothing when the
# pattern is invalid.
function compile_engine(source::String)
    try
        options = occursin("\\p", source) || occursin("\\P", source) ? SHIM_UNICODE_OPTIONS : SHIM_COMPILE_OPTIONS
        translated = replace(source, "\\u{" => "\\x{", r"\\u([0-9a-fA-F]{4})" => s"\\x{\1}", "\\cX" => "\\x18")
        re = Regex(translated, options, Base.DEFAULT_MATCH_OPTS)
        Base.compile(re)
        return re
    catch
        return nothing
    end
end

# Reports whether the pattern matches somewhere in UTF-8 text.
engine_match(p::EnginePattern, s::Bytes) = occursin(p, String(s))

# Reports whether a string is a valid ECMA-262 regular expression with the u flag (the regex format, and the
# patterns the engine reads without falling back).
valid_regex(source::String) = compile_engine(source) !== nothing
valid_regex(source::Bytes) = valid_regex(String(source))
