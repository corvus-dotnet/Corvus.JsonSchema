# Patterns on which the translation once differed from V8, by class, with V8's answers. Each row is a pattern and
# V8's answer on every text of V8_TEXTS, as hexadecimal digits of four answers each, first text highest. The answers
# were written by Node 24.11.0 (V8 13.6), with the u flag when the pattern is valid with it and with no flag
# otherwise. V8_TEXTS is every text of up to three characters over "a", "x" and "-", and eight longer ones.

const V8_TEXTS = let texts = [""]
    for n in 1:3, t in Iterators.product(ntuple(_ -> ("a", "x", "-"), n)...)
        push!(texts, join(reverse(t)))
    end
    append!(texts, ["aaaa", "xxxx", "xa-x", "a-a-", "axax", "-xa-x", "aaxaa", "x-x-x-"])
end

# The texts on which the pattern does not answer as the row says.
function v8differences(pattern::String, hex::String)
    compiled = E.compile(pattern)
    return String[text for (k, text) in enumerate(V8_TEXTS) if E.ismatch(compiled, text) != bit(hex, k - 1)]
end

function checkv8table(rows)
    for (pattern, hex) in rows
        differences = try
            v8differences(pattern, hex)
        catch e
            e isa Union{E.PatternError,E.MatchError} || rethrow()
            [sprint(showerror, e)]
        end
        isempty(differences) || println("[v8tests] ", repr(pattern), " differs from V8 on ", differences)
        @test isempty(differences)
    end
end

@testset "V8 by class" begin
    @test length(V8_TEXTS) == 48
    @test V8_TEXTS[1:6] == ["", "a", "x", "-", "aa", "ax"]

    # PCRE2 10.42 and 10.46 take a group that is counted {0} and has an alternative that starts with \A for a
    # pattern that can only match at the start of the text: (?:b|\A){0}a does not match "ba". A repetition that
    # never runs is not written.
    @testset "a repetition that never runs" begin
        checkv8table([
            # The five of the test data.
            (raw"(?:(?=x)|^){0}$", "ffffffffffff"),
            (raw"(()|^(?=x)){0}x$", "24924924926c"),
            (raw"(?:((?=a)|^x|(?=|(|a).)){0}|^[^a]){0}?x", "25d2ebfeba6f"),
            (raw"((?:(x|x\2){0,}?|^){0,0}(?!$)){4}?x", "25d2ebfeba6f"),
            (raw"(?:a|(?<=\Ba|(?:|[^a]){4,}?)^|){0}?(){1,}?x$", "24924924926c"),
            # Reduced.
            (raw"(?:a|^){0}x", "25d2ebfeba6f"),
            (raw"(?:a|^){0}?x", "25d2ebfeba6f"),
            (raw"(?:a|^){0,0}x", "25d2ebfeba6f"),
            (raw"(a|^){0}x$", "24924924926c"),
            (raw"(?:(?:a|^){0}x){2}", "008040701040"),
            (raw"(?:a|(?:x|^)){0}-", "12793c9fff35"),
            (raw"(?=(?:a|^){0}x)x", "25d2ebfeba6f"),
        ])
        @test E.translate("a(?:b|^){0}c").main == "ac"
        @test E.translate("a(?:b|^){0,0}?c").main == "ac"
        # What PCRE2 itself answers, which is why.
        @test !E.ismatch(rawpattern("(?:b|\\A){0}a"), "ba")
        @test E.ismatch(rawpattern("(?:b|\\A){0}a"), "a")
    end

    # A backreference reads what its group captured in the iteration the backreference is in, and the empty string
    # when the group has not taken part since that iteration started. PCRE2 fails a backreference to a group that
    # is not set and keeps what a group captured in an earlier iteration, and the translator used to refuse the
    # patterns in which that shows.
    @testset "backreferences" begin
        rows = [
            # A group that has not taken part reads as the empty string.
            (raw"^(?:(a)|x)\1-$", "004100000000"),
            (raw"^(a)?\1x$", "200200000000"),
            (raw"^\1(a)x", "0400e0000008"),
            (raw"^(a\1)x", "0400e0000008"),
            (raw"(?:(a)|(x))+\1\2-", "000100100000"),
            (raw"^(?:(?<n>a)|(?<n>x))+\k<n>-$", "000100100000"),
            # The groups of a body are unset when an iteration starts.
            (raw"^(?:(a)|x)*\1-$", "104120100000"),
            (raw"^(?:(?:(a)|x)\1)+$", "2882022000c2"),
            (raw"^(?:(a)x?|-)+\1$", "1a0da4014192"),
            (raw"^(?:(a)|x){2}\1-", "000020100000"),
            (raw"^(?:(?:(a)|(x)|-)\1\2)*$", "9889001111c0"),
            (raw"^(?:(a)|(x\1))+\2-", "02011c900030"),
            # The groups of a lookaround.
            (raw"^(?=(a)|x)\1x", "25c0e3fe0069"),
            (raw"(?!(a))\1x", "25d2ebfeba6f"),
            (raw"^(?:(?=(a))a|x)+\1-", "0041201e0001"),
            (raw"^(?=(?:(a)|x)*)\1-", "12381c01ff14"),
            # A group of a repetition that can match the empty string, read after it.
            (raw"^(a*)+\1x", "21c203fe0063"),
            (raw"^(?:(a)|)+\1x", "21c203fe0063"),
            (raw"^(a|){2,3}\1x", "25c2e3fe006b"),
            (raw"^(?:(a)|(?=x))*\1x", "21c203fe0063"),
            (raw"^(?:(?=(a)))?\1a", "4e07fc00009a"),
            (raw"^(?:(a|)x?)*\1-$", "104120100000"),
            (raw"^(?:(a)|(x)|)+?\1\2-", "10390011ff04"),
            (raw"^(?=(|(?=-).){3,5}?[ax])\1a", "4e27fc01c09a"),
            (raw"^(?:(a)|-|){2,}\1x$", "241208000200"),
            # A lookbehind that reads a group outside it, and a group of a lookbehind of a fixed length.
            (raw"(a)(?<=\1\1)x", "000200000002"),
            (raw"(a|x)-(?<!\1-)", "000000000000"),
            (raw"^(?:(a)|x)(?<=\1)-", "02401c0e0011"),
            (raw"(?<=(a)|(x)-)\1\2", "080702050083"),
            (raw"(?<=(?<n>a)|(?<n>x-))\k<n>-", "000100000000"),
            # From the test data, where each was refused.
            (raw"^(\1){0,2}?(?:x?|\1|){2}x$", "208000200000"),
            (raw"^(|[^a]){3}?(?:\1(?:$|x){1}|){0,2}?a$", "412000482400"),
            (raw"^(a|(?<!^)|a)+(?:\1[^a]|[ax]){3,}a", "000000000002"),
            (raw"^(|$){3,4}?(?:(?:-x|(?!$)\1){0,1}?|(?:(?<!^)|()){4}){3}?a", "4e07fc00209e"),
            (raw"^(|)*((?<=a)|(?<=(?:\1a)+?)|aa){1}?a$", "000400000000"),
            (raw"(x.|x*){2,4}((?:)||(?:(?:|xa)+|\b(?<=\1)){3,}){1,3}", "ffffffffffff"),
            (raw"(){1,}?(?:(?<!^)$|(?<![^a]a|[^a]\1|\1)|(?<=a).){0,}a$", "492492492482"),
            (raw"^(?:|(a|)(?<=\1)|(?<=a)){0,0}?a$", "400000000000"),
            (raw"(((a|a\2){1,1}|x)*(?<=\1|\3)|){1,1}a$", "492492492482"),
        ]
        checkv8table(rows)
        for (pattern, _) in rows
            @test E.unsupportedreason(pattern) === nothing
        end
        # How a group that an alternative or a repetition can go round is written.
        @test E.translate("^(?:(a)|x)\\1\$").main == "\\A(?:(?|(?<g1>a)|(?<g1>)x))\\k<g1>\\z"
        @test E.translate("^(a)?\\1\$").main == "\\A(?|(?<g1>a)|(?<g1>))\\k<g1>\\z"
        @test E.translate("^(?:(a)|(x))+\\1\\2\$").main ==
            "\\A(?:(?|(?<g1>a)(?<g2>)|(?<g1>)(?<g2>x)))+\\k<g1>\\k<g2>\\z"
        # A backreference that can never read its group is not written.
        @test E.translate("^\\1(a)(a\\2)\$").main == "\\A(?:a)(?:a)\\z"
        @test E.translate("^(?:(a)|x\\1)\$").main == "\\A(?:(?:a)|x)\\z"
        # The groups of a pattern in which no backreference reads one are written as they were.
        @test E.translate("^(?:(a)|x)*(-)?\$").main == "\\A(?:(?:a)|x)*(?:\\x{2d})?\\z"
        # A lookbehind that reads a group outside it is a search PCRE2 runs, and it reads the group.
        @test occursin("(?(DEFINE)(?<w2>", E.translate("(a)(?<=\\1\\1)x").main)
        @test isempty(E.translate("(a)(?<=\\1\\1)x").lookbehinds)
        @test matches("(a+)-(?<=\\1\\1-)x", "aaaa-x")
        @test !matches("(a+)-(?<=x\\1\\1-)x", "aaaa-x")
        @test matches("(a+)-(?<=x\\1\\1-)x", "x" * "a"^300 * "-x")
        # A reference after a lookahead stands where the lookahead started, which is before the place its group
        # was set at.
        @test matches("^(?=(|(?=-).){3,5}?[ax])\\1a", "-a")
        # Repetitions written twice inside one another, past what is written out.
        nested = "(a|)+\\1"
        for _ in 1:12
            nested = "(?:" * nested * "|x)+\\1"
        end
        @test E.unsupportedreason("^" * nested * "\$") == E.REFUSED_TOO_LARGE
    end
end
