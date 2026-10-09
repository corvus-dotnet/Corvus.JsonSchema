# Checks the translator against V8 on patterns built from a grammar. v8_fuzz.json is written by gen_fuzz_oracle.js,
# which says what the patterns are. Every pattern is valid with the u flag. Each must be valid here, and each that is
# not refused must answer as V8 does on every text. The refused ones are counted by reason, as in oracle.jl.
#
# One pattern is given up on. It holds lookbehinds of no fixed length inside lazy repetitions whose bodies can match
# nothing, and the searches for the lookbehinds run past the limit on their steps (a MatchError) on texts of a few
# characters. V8 answers it at once, as ECMA-262 refuses an iteration that matches nothing where PCRE2 tries it.

const EXPECTED_UNSUPPORTED_FUZZ = Dict(
    EcmaRegex.REFUSED_BACKREFERENCE_IN_LOOKBEHIND => 67,
    EcmaRegex.REFUSED_GROUP_IN_LOOKBEHIND => 45,
    EcmaRegex.REFUSED_GROUP_IN_LOOKAROUND => 17,
    EcmaRegex.REFUSED_GROUP_IN_EMPTY_REPETITION => 37,
    EcmaRegex.REFUSED_CASE_INSENSITIVE_BACKREFERENCE => 78)
const EXPECTED_LIMITED_FUZZ = 1

@testset "V8 on patterns built from a grammar" begin
    fuzz = readjson(joinpath(@__DIR__, "v8_fuzz.json"))
    texts = String[s for s in fuzz["texts"]]
    tally = Tally()
    for entry in fuzz["patterns"]
        flags = entry["f"]::String
        p = isempty(flags) ? entry["p"]::String : string("(?", flags, ":", entry["p"], ")")
        check!(tally, p, true, false, texts, entry["m"], ())
    end
    report(tally, "built from a grammar")
    @test tally.patterns == 6000
    @test tally.unsupported == EXPECTED_UNSUPPORTED_FUZZ
    @test length(tally.limited) == EXPECTED_LIMITED_FUZZ
    @test all(p -> occursin("(?<", p), tally.limited)
    haskey(ENV, "ECMAREGEX_SHOW_REFUSED") && foreach(p -> println("  refused ", repr(p)), tally.refused)

    # A set of many ranges is written in one of two forms, by the PCRE2 in use. Some of the patterns are run again
    # with the other form.
    other = Tally()
    try
        EcmaRegex.SEARCH_TREES[] = !EcmaRegex.usesearchtrees()
        for entry in fuzz["patterns"][1:1500]
            occursin("\\p", entry["p"]) || occursin("\\P", entry["p"]) || continue
            flags = entry["f"]::String
            p = isempty(flags) ? entry["p"]::String : string("(?", flags, ":", entry["p"], ")")
            check!(other, p, true, false, texts, entry["m"], ())
        end
    finally
        EcmaRegex.SEARCH_TREES[] = nothing
    end
    report(other, "built from a grammar, with the other form of a large class")
    @test other.patterns > 400
    @test length(other.limited) <= EXPECTED_LIMITED_FUZZ
end
