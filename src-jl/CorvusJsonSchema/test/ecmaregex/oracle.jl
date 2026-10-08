# Checks the translator of ECMA-262 patterns against what V8 answers. This is the Java port's EcmaRegexOracleTest.
#
# v8_oracle.json holds patterns with V8's verdict on each (valid with the u flag, valid with no flag) and its answer
# on a set of texts. It is a copy of the file the Java and Go ports are tested with, written by the Go port's
# testdata/gen_oracle.js under Node 24. For every pattern the validity must agree, and for a valid pattern the answer
# on every text must agree. The exceptions are counted by reason and the counts are asserted, so a pattern cannot
# move into an exception unnoticed:
#
# - a pattern that is valid ECMA-262 and that PCRE2 cannot run with the same meaning. The translator refuses these
#   and says why;
# - six patterns on which V8 13.6 itself answers a case-insensitive modifier group differently from the same pattern
#   with the i flag, which ECMA-262 does not allow. Only their validity is taken from the file. The same behaviour is
#   checked through the patterns that V8 matched with the flag set.
#
# V8 matches a pattern that is valid only with no flag by UTF-16 code unit, and this library matches every pattern by
# code point, so such a pattern is not asked about a text with a character beyond the Basic Multilingual Plane.

# The patterns of the file that V8 answers inconsistently.
const V8_MODIFIER_BUGS = Set(["^(?i:\\u212a)\$", "^(?i:\\u017f)\$", "^(?i:\\u1e9e)\$", "^(?i:\\u03bc)\$",
    "^(?i:\\u03bc)\\&?\$", "(?i:\\Bs)"])

const ORACLE = readjson(joinpath(@__DIR__, "v8_oracle.json"))
const ORACLE_INPUTS = String[s for s in ORACLE["inputs"]]
const ORACLE_SHORT_INPUTS = String[s for s in ORACLE["shortInputs"]]

# What one run over a part of the file found.
mutable struct Tally
    patterns::Int
    answers::Int
    matched::Int
    inconsistent::Int
    # The patterns whose match on some text was given up at a limit.
    limited::Vector{String}
    unsupported::Dict{String,Int}
    refused::Vector{String}
    failures::Vector{String}
end

Tally() = Tally(0, 0, 0, 0, String[], Dict{String,Int}(), String[], String[])

fail!(tally::Tally, message::String) = length(tally.failures) < 60 && push!(tally.failures, message)

function report(tally::Tally, what::String)
    println("[oracle] ", what, ": ", tally.patterns, " patterns, ", tally.answers, " answers (", tally.matched,
        " of them a match), ", tally.inconsistent, " answered inconsistently by V8, ", length(tally.limited),
        " given up at a limit, unsupported ", sort!(collect(tally.unsupported)))
    isempty(tally.failures) || println(what, ":\n", join(tally.failures, "\n"))
    @test isempty(tally.failures)
end

# Bit k of a string of hexadecimal digits, four bits to a digit, first bit highest.
bit(hex::String, k::Int) = (parse(Int, hex[k ÷ 4 + 1]; base=16) >> (3 - k % 4)) & 1 == 1

beyondbmp(s::String) = any(c -> c > '\uffff', s)

# The reason of a refusal, without what PCRE2 said when it is the one that refused.
reasonof(e) = startswith(e.reason, EcmaRegex.REFUSED_BY_ENGINE) ? EcmaRegex.REFUSED_BY_ENGINE : e.reason

# Checks one pattern: its validity under each grammar, and, when it can be run and `bits` is not nothing, its answer
# on every text. The answers of a pattern in `inconsistent` are not compared.
function check!(tally::Tally, p::String, validunicode::Bool, validlegacy::Bool, texts::Vector{String}, bits,
        inconsistent)
    tally.patterns += 1
    if EcmaRegex.isvalidpattern(p) != validunicode
        fail!(tally, "isvalidpattern($(repr(p))) is $(!validunicode), V8 says $validunicode")
    end
    compiled = nothing
    reason = nothing
    invalid = nothing
    try
        compiled = EcmaRegex.compile(p)
    catch e
        e isa EcmaRegex.PatternError || rethrow()
        if e.unsupported
            reason = reasonof(e)
        else
            invalid = e.reason
        end
    end
    if !validunicode && !validlegacy
        if invalid === nothing
            fail!(tally, "$(repr(p)) is not valid in V8. Got $(compiled !== nothing ? compiled.translation : reason)")
        end
        return
    end
    if reason !== nothing
        tally.unsupported[reason] = get(tally.unsupported, reason, 0) + 1
        push!(tally.refused, p)
        return
    end
    if compiled === nothing
        fail!(tally, "$(repr(p)) is valid in V8 (u $validunicode, no flag $validlegacy) and was refused: $invalid")
        return
    end
    if compiled.unicode != validunicode
        fail!(tally, "$(repr(p)) was read with the wrong grammar")
    end
    bits === nothing && return
    if p in inconsistent
        tally.inconsistent += 1
        return
    end
    for (i, text) in enumerate(texts)
        !validunicode && beyondbmp(text) && continue
        want = bit(bits, i - 1)
        got = try
            EcmaRegex.ismatch(compiled, codeunits(text))
        catch e
            e isa EcmaRegex.MatchError || rethrow()
            push!(tally.limited, p)
            return
        end
        tally.answers += 1
        tally.matched += want
        if got != want
            fail!(tally, "$(repr(p)) on $(repr(text)): got $(!want), V8 says $want  [$(compiled.translation)]")
        end
    end
    return
end

# The patterns the translator refuses, by reason. emitter.jl says what each reason means.
const EXPECTED_UNSUPPORTED_HAND = Dict(
    EcmaRegex.REFUSED_BACKREFERENCE_IN_LOOKBEHIND => 6,
    EcmaRegex.REFUSED_GROUP_IN_LOOKBEHIND => 3,
    EcmaRegex.REFUSED_GROUP_IN_LOOKAROUND => 2,
    EcmaRegex.REFUSED_CASE_INSENSITIVE_BACKREFERENCE => 8,
    EcmaRegex.REFUSED_BY_ENGINE => 3)
const EXPECTED_UNSUPPORTED_FLAGGED = Dict(
    EcmaRegex.REFUSED_BACKREFERENCE_IN_LOOKBEHIND => 8,
    EcmaRegex.REFUSED_CASE_INSENSITIVE_BACKREFERENCE => 14)
const EXPECTED_UNSUPPORTED_GENERATED = Dict{String,Int}()

@testset "V8 oracle" begin
    # The hand-written patterns: validity, and the answer on every text.
    @testset "hand-written patterns" begin
        tally = Tally()
        for h in ORACLE["hand"]
            check!(tally, h["p"], h["u"], h["l"], ORACLE_INPUTS, get(h, "m", nothing), V8_MODIFIER_BUGS)
        end
        report(tally, "hand-written")
        @test tally.unsupported == EXPECTED_UNSUPPORTED_HAND
        @test tally.inconsistent == length(V8_MODIFIER_BUGS)
        @test isempty(tally.limited)
        haskey(ENV, "ECMAREGEX_SHOW_REFUSED") && foreach(p -> println("  refused ", repr(p)), tally.refused)
    end

    # The six patterns V8 answers inconsistently are answered here as V8 answers the same pattern with the i flag.
    @testset "the patterns V8 answers inconsistently" begin
        expected = [
            ["^(?i:\\u212a)\$", "k", "K", "\u212a"],
            ["^(?i:\\u017f)\$", "s", "S", "\u017f"],
            ["^(?i:\\u1e9e)\$", "\u00df", "\u1e9e"],
            ["^(?i:\\u03bc)\$", "\u00b5", "\u03bc", "\u039c"],
            ["^(?i:\\u03bc)\\&?\$", "\u00b5", "\u03bc", "\u039c"],
        ]
        for e in expected
            @test e[1] in V8_MODIFIER_BUGS
            pattern = EcmaRegex.compile(e[1])
            for text in ORACLE_INPUTS
                @test EcmaRegex.ismatch(pattern, text) == (text in e[2:end])
            end
        end
    end

    # Case-insensitive, multiline and dot-all matching. V8 matched each pattern with the flags set for the whole
    # pattern, and here the pattern is wrapped in the modifier group that means the same.
    @testset "patterns matched with flags" begin
        tally = Tally()
        for f in ORACLE["flagged"]
            u = f["u"]::Bool
            check!(tally, string("(?", f["f"], ":", f["p"], ")"), u, !u, ORACLE_INPUTS, get(f, "m", nothing), ())
        end
        report(tally, "matched with flags")
        @test tally.unsupported == EXPECTED_UNSUPPORTED_FLAGGED
        @test tally.inconsistent == 0
        @test isempty(tally.limited)
        haskey(ENV, "ECMAREGEX_SHOW_REFUSED") && foreach(p -> println("  refused ", repr(p)), tally.refused)
    end

    # Random patterns over an alphabet of syntax characters, built with the generator the file was written with.
    # Each one's validity is checked, and the first few thousand are also matched against short texts.
    @testset "generated patterns" begin
        tally = Tally()
        verdicts = ORACLE["verdicts"]::String
        matches = String[s for s in ORACLE["randomMatches"]]
        alphabet = collect("ab0\\\\^\$.*+?()[]{}|-,:=!<>dwsbpkuxc1L")
        seed = Ref(0x9e3779b9)
        # One step of xorshift32.
        function next()
            s = seed[]
            s ⊻= s << 13
            s ⊻= s >> 17
            s ⊻= s << 5
            seed[] = s
            return Int(s)
        end
        counts = [0, 0, 0]
        for i in 1:ncodeunits(verdicts)
            n = 1 + next() % 8
            p = String([alphabet[next() % length(alphabet) + 1] for _ in 1:n])
            verdict = verdicts[i]
            counts[verdict - '0' + 1] += 1
            bits = i <= length(matches) && verdict != '0' ? matches[i] : nothing
            check!(tally, p, verdict == '1', verdict == '2', ORACLE_SHORT_INPUTS, bits, ())
        end
        println("[oracle] generated: ", counts[1], " not valid, ", counts[2], " valid with u, ", counts[3],
            " valid only with no flag")
        report(tally, "generated")
        @test tally.unsupported == EXPECTED_UNSUPPORTED_GENERATED
        @test isempty(tally.limited)
    end
end
