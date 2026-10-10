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

    # ECMA-262 refuses an iteration past the minimum that matches the empty string. PCRE2 takes one, and tries every
    # way of putting such iterations among the others, so these patterns ended in its match limit (a MatchError)
    # on texts of a few characters, where V8 answers at once.
    @testset "iterations that match the empty string" begin
        checkv8table([
            # From the test data, where each ended in PCRE2's match limit.
            (raw"(?:((?=x)|()|){4,5}?){4}?$", "ffffffffffff"),
            (raw"^(|(|[^a](a|)){2,4}\B){4,6}(?:[^a]?|)+a", "4f27ffc9e4be"),
            (raw"^((?!$)(||\Ba?){1,3}|-x){4}?x$", "200000001000"),
            (raw"(?:$-|((-??|\b)*|){3,5}\b|a??$)+$", "ffffffffffff"),
            (raw"^(?:(((?:)){2,}(?:-|\2){3}|-|$){0,1}?(\B|-){3,5}?|a){3,4}a", "08270001c482"),
            (raw"^(?:.){2}?(?:(?:\B||\B){4}a*?|(?<=x|[^a]|\1)\1){4}x$", "000249249208"),
            (raw"^(a*((?<!^)|^|((?<=^)-){2,3}){2,3}|(?<=a)x|(|a)(|(?<=a)|){3,3})+x", "25c2e3fe006b"),
            (raw"(((?:|(?=x)){3,3}?(|a)|[^a]*?(|)){0,}){3,5}?(()){0}?x$", "24924924926c"),
            # A body that can only match the empty string.
            (raw"^(?:|\b){2,}x", "21c003fe0061"),
            (raw"^(?:(?=a)|\B)*a", "4e07fc00009a"),
            (raw"(?:(?=x)|^){3}?x$", "24924924926c"),
            (raw"^(?:(|\b)*-)+$", "100800000100"),
            # An iteration past the minimum takes a character.
            (raw"^(?:a*)*$", "c80400000080"),
            (raw"^(?:a?x?)*-$", "124120900000"),
            (raw"^(?:(?:a|){0,3}x)*-$", "104020100000"),
            (raw"^(?:(?:a*x?)+-)*$", "924924924911"),
            (raw"^(?:a|\b|x){2,}-$", "024120900000"),
            (raw"^(?:(?:a|(?=x))*x){2,3}$", "008041200008"),
            (raw"^(?:\1a|(x)|){3,}-$", "124120900000"),
            (raw"^(x|(a|)\2){2,}-", "1079001fff05"),
        ])
        # A repetition inside a repetition, on texts long enough for the ways to be too many to try.
        @test !matches("^(?:(?:a|)*x)*\$", "x"^40 * "-")
        @test matches("^(?:(?:a|)*x)*\$", "x"^40)
        @test !matches("^(?:a?){0,30}\$", "a"^15 * "-")
        @test matches("^(?:a?){0,30}\$", "a"^30)
        @test !matches("^(?:a?){0,30}\$", "a"^31)
        @test !matches("^(?:(?:a|\\b|(?=x))*x){2,}\$", "x"^40 * "-")
        # A body that is not plain to write without its empty matches (it holds a backreference).
        @test !matches("^(x)(?:(?:\\1|a|)*-)*\$", "x" * "-"^40 * "a")
        @test matches("^(x)(?:(?:\\1|a|)*-)*\$", "x" * "x-a-"^10)
        # How they are written.
        @test E.translate("^(?:a*)*\$").main == "\\A(?:(?:a+)*)\\z"
        @test E.translate("^(?:a?x?)*\$").main == "\\A(?:(?:ax?|x)*)\\z"
        @test E.translate("^(?:a*)+\$").main == "\\A(?:(?:a*)(?:a+)*)\\z"
        @test E.translate("^(?:a|\\b)*\$").main == "\\A(?:(?:a)*)\\z"
        @test E.translate("^(a)(?:\\1|x|)*\$").main ==
            "\\A(?<g1>a)(?:(?:(?=(?<e1>(?s:.*)))(?:\\k<g1>|x|)(?!\\k<e1>))*)\\z"
        # A body that can only match the empty string is written once, or not at all for a minimum of zero.
        @test E.translate("^(?:|\\b){2,}x").main == "\\A(?:|" * E.translate("\\b").main * ")x"
        @test E.translate("^(?:(?=a)|\\B)*a").main == "\\Aa"
        # A body that cannot match the empty string is written with its count, as it always was.
        @test E.translate("^(?:a+x?)*\$").main == "\\A(?:a+x?)*\\z"
        @test E.translate("^(?:a|x){2,5}\$").main == "\\A(?:a|x){2,5}\\z"
    end

    # ECMA-262 matches a lookbehind from its end to its start, so a backreference in one reads a group to its right.
    @testset "backreferences inside a lookbehind" begin
        rows = [
            # A backreference in a lookbehind to a group at its end, which ECMA-262 matches first.
            (raw"(?<=\1(.))x", "000200200242"),
            (raw"(?<=\1(a))x", "000200000002"),
            (raw"(?<=\1(a|x))-", "000100100000"),
            (raw"(?<=\1(?:(a)|.))x", "0492e974ba6f"),
            (raw"(?<!\1(a)|x)-", "12381c83ff34"),
            (raw"(?<=\1\1(a))x", "000000000000"),
            (raw"(?<=(?:\1-|x)(a)-)x", "000000000024"),
            (raw"(?<=\2(a)(x))-", "000000000000"),
            (raw"^(?:a|-|(?<=\1(x)))+$", "5a2d14014590"),
            # A lazy repetition that a lookbehind matches last runs its minimum.
            (raw"(?<=(a|x)+?)-\1", "000010040011"),
            (raw"(?<=(a|x){2,}?)-\1", "000000000024"),
            (raw"(?<=(a)*?x)-\1", "0040201e0801"),
            # The groups of a repetition in a lookbehind are those of its leftmost iteration.
            (raw"(?<=(a|x){2})-\1", "000000000024"),
            (raw"(?<=(?:(a)|x){3})-\1", "000000000000"),
            (raw"(?<=(?:(a|x)(-|x)){2})-\1\2", "000000000000"),
            (raw"(?<=((a|x){2}-){2})\2\1", "000000000000"),
            # Alternatives of different lengths in a lookbehind, one of which writes a group.
            (raw"(.)?(?<=a(?=\1*?)|(($)+?))x-$", "000020000000"),
            (raw"(?:(a)|x)*(?<=x|(?=x{3}\1*|x))x(?<=\1)\1", "25d2ebfeba6f"),
            (raw"(a|x)(?<=(?=(?!\1{2,})(?:\1*?a)*?)$|-)-", "000000000000"),
        ]
        checkv8table(rows)
        for (pattern, _) in rows
            @test E.unsupportedreason(pattern) === nothing
        end
        @test startswith(E.translate("(?<=\\1(a))x").main, "(?<=(?<g1>a)(?<=(?=(?<r1>(?s:.*)))(?=(?&w2))(?s:.){1}))x")
        @test E.translate("(?<=(a|x)+?)-\\1").main == "(?<=(?<g1>a|x))\\x{2d}\\k<g1>"
        # Where the end of the lookbehind has no fixed length, the pattern is still refused.
        @test E.unsupportedreason("(?<=\\1(a+))x") == E.REFUSED_BACKREFERENCE_IN_LOOKBEHIND
        @test E.unsupportedreason("(?<=(a)+)x\\1") == E.REFUSED_GROUP_IN_LOOKBEHIND
    end
end
