# What the tests share.

const E = EcmaRegex

# Whether the pattern, which must be one the module runs, matches somewhere in the text.
function matches(pattern::String, text::String)
    reason = E.unsupportedreason(pattern)
    reason === nothing || error("$(repr(pattern)) is refused: $reason")
    return E.ismatch(E.compile(pattern), text)
end

isvalid262(pattern::String) = E.isvalidpattern(pattern)

# Whether the pattern is valid and compiles.
function validandcompiles(pattern::String)
    E.isvalidpattern(pattern) || return false
    E.compile(pattern)
    return true
end

# Whether compile refuses the pattern as not valid ECMA-262 under either grammar.
function isnotapattern(pattern::String)
    try
        E.compile(pattern)
        return false
    catch e
        e isa E.PatternError || rethrow()
        return !e.unsupported
    end
end

# The pairs of characters that Unicode 15.1 and 16 made equivalent under simple case folding, which a PCRE2 with
# the data of Unicode 14 (10.42, in Julia 1.10) does not hold equivalent, and those Unicode 17 added, which a PCRE2
# with the data of Unicode 16 (10.46, in Julia 1.13) does not either.
const PAIRS_SINCE_UNICODE_14 = vcat(
    [(0x19b, 0xa7dc), (0x264, 0xa7cb), (0x390, 0x1fd3), (0x3b0, 0x1fe3), (0x1c89, 0x1c8a), (0xa7cc, 0xa7cd),
        (0xa7da, 0xa7db), (0xfb05, 0xfb06)],
    [(0x10d50 + k, 0x10d70 + k) for k in 0:21])
const PAIRS_OF_UNICODE_17 = vcat(
    [(0xa7ce, 0xa7cf), (0xa7d2, 0xa7d3), (0xa7d4, 0xa7d5)],
    [(0x16ea0 + k, 0x16ebb + k) for k in 0:24])

# A PCRE2 pattern compiled as the module compiles its translations, for tests of what PCRE2 itself does.
function rawpattern(text::String)
    compiled = E.pcrecompile(E.Translation(text, String[]), text, true)
    compiled isa E.Refusal && error("PCRE2 does not compile $(repr(text)): $(compiled.message)")
    return compiled
end

# Every code point that is a character of UTF-8 text, in order, as text.
const EVERY_CODE_POINT = let io = IOBuffer()
    for c in 0:0x10FFFF
        (0xD800 <= c <= 0xDFFF) || print(io, Char(c))
    end
    take!(io)
end

# The set of the code points at which a PCRE2 pattern for one character matches, from one pass over a text of every
# code point (or over `text`). It reads the bounds of each match, which `ismatch` does not give, so it calls PCRE2
# itself with match data of its own.
function matchedset(pattern::E.Pattern, text::Vector{UInt8}=EVERY_CODE_POINT)
    # The repetition is possessive, so that a long run costs the matcher no memory.
    runs = rawpattern("(?:" * pattern.translation.main * ")++")
    matchdata = ccall((:pcre2_match_data_create_8, E.PCRE_LIB), Ptr{Cvoid}, (UInt32, Ptr{Cvoid}), 1, C_NULL)
    ovector = ccall((:pcre2_get_ovector_pointer_8, E.PCRE_LIB), Ptr{Csize_t}, (Ptr{Cvoid},), matchdata)
    pairs = Int32[]
    at = 0
    GC.@preserve runs text begin
        while at < length(text)
            rc = ccall((:pcre2_match_8, E.PCRE_LIB), Cint,
                (Ptr{Cvoid}, Ptr{UInt8}, Csize_t, Csize_t, UInt32, Ptr{Cvoid}, Ptr{Cvoid}),
                runs.code, text, length(text), at, 0, matchdata, C_NULL)
            rc == -1 && break
            rc >= 0 || error("PCRE2 error $rc")
            start = Int(unsafe_load(ovector, 1))
            stop = Int(unsafe_load(ovector, 2))
            stop > start || error("an empty match")
            run = String(text[(start + 1):stop])
            push!(pairs, Int32(UInt32(first(run))), Int32(UInt32(last(run))))
            # Within a run the code points of the text may skip some, where the text does.
            at = stop
        end
    end
    ccall((:pcre2_match_data_free_8, E.PCRE_LIB), Cvoid, (Ptr{Cvoid},), matchdata)
    return E.setof(pairs)
end

# The set as `matchedset` sees it in a text of every code point: without the surrogates, unless the characters on
# either side of them are both in the set, as a run of matches then goes straight from U+D7FF to U+E000.
function astext(set::E.CodeSet)
    out = E.intersection(set, E.NOT_SURROGATE_SET)
    if E.inset(out, 0xD7FF) && E.inset(out, 0xE000)
        return E.setunion(out, E.SURROGATE_SET)
    end
    return out
end

# The directory of the JSON Schema Test Suite's tests, or nothing. The suite is a submodule at the root of the
# repository, and CORVUS_JSON_SCHEMA_TEST_SUITE names another copy.
function suitetests()
    root = get(ENV, "CORVUS_JSON_SCHEMA_TEST_SUITE", "")
    if isempty(root)
        root = joinpath(@__DIR__, "..", "..", "..", "..", "JSON-Schema-Test-Suite")
    end
    tests = joinpath(root, "tests")
    return isdir(tests) ? tests : nothing
end

# A small generator of numbers that gives the same ones on every Julia (xorshift32).
mutable struct Xorshift
    state::UInt32
end

function nextbelow!(r::Xorshift, n::Integer)
    s = r.state
    s ⊻= s << 13
    s ⊻= s >> 17
    s ⊻= s << 5
    r.state = s
    return Int(s % UInt32(n))
end
