# The regular expression engine behind every pattern that has no faster matcher, and the Unicode tables that come
# with it: the EcmaRegex submodule (src/ecmaregex). This file is the only one that names the engine.

include("ecmaregex/EcmaRegex.jl")

# The ranges of a property expression of \p{...}, such as "Lu" or "Script=Greek", as inclusive pairs in ascending
# order.
unicode_property(expression::String) = copy(EcmaRegex.property(expression)::Vector{Int32})

# A compiled pattern of the engine.
const EnginePattern = EcmaRegex.Pattern

# Compiles an ECMA-262 pattern (with the u flag, or failing that without it, as many schemas need). Nothing when the
# pattern is invalid. A pattern that is valid and that the engine cannot run with the same meaning is a compilation
# error that says so.
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
@inline engine_match(p::EnginePattern, s::Bytes) = EcmaRegex.ismatch(p, view(s.b, s.off+1:s.off+s.len))

# Reports whether a string is a valid ECMA-262 regular expression with the u flag (the regex format, and the
# patterns the engine reads without falling back).
valid_regex(source::String) = EcmaRegex.isvalidpattern(source)
valid_regex(source::Bytes) = valid_regex(String(source))
