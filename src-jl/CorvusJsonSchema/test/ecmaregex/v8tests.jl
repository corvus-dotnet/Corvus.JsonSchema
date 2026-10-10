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

# The texts of the tables of case-insensitive patterns: the empty text, each of eighteen characters, every pair of
# them, every text of four of "a", "A", "k" and U+212A, one more, and every text of three of "a", "A", "k", "K",
# U+212A and "-".
const V8_CASE_TEXTS = let texts = [""]
    chars = ["a", "A", "k", "K", "\u212a", "s", "S", "\u017f", "\u00df", "\u1e9e", "\u03c3", "\u03c2", "\u03a3",
        "\u00e9", "\u00c9", "\U10400", "\U10428", "-"]
    append!(texts, chars)
    for c in chars, d in chars
        push!(texts, c * d)
    end
    four = ["a", "A", "k", "\u212a"]
    for a in four, b in four, c in four, d in four
        push!(texts, a * b * c * d)
    end
    push!(texts, "aA-Aa")
    three = ["a", "A", "k", "K", "\u212a", "-"]
    for a in three, b in three, c in three
        push!(texts, a * b * c)
    end
    texts
end

# The texts on which the pattern does not answer as the row says.
function v8differences(pattern::String, hex::String, texts::Vector{String}=V8_TEXTS)
    compiled = E.compile(pattern)
    return String[text for (k, text) in enumerate(texts) if E.ismatch(compiled, text) != bit(hex, k - 1)]
end

function checkv8table(rows, texts::Vector{String}=V8_TEXTS)
    for (pattern, hex) in rows
        differences = try
            v8differences(pattern, hex, texts)
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

    # A case-insensitive backreference compares as PCRE2 compares, which is by Unicode simple case folding, as
    # ECMA-262 does with the u flag. The translator used to refuse every such pattern whose group could hold a
    # character with a case variant other than an ASCII letter.
    @testset "case-insensitive backreferences" begin
        @test length(V8_CASE_TEXTS) == 816
        @test V8_CASE_TEXTS[2:6] == ["a", "A", "k", "K", "\u212a"]
        rows = [
            # The patterns of the test data, which are valid with the u flag.
            (raw"^(.)(?i:\1)$", "0000180006000070001c0007000038000e00038000180006000070001c0007000030000c0000c0003000020000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"),
            (raw"^(?i:(.)\1)$", "0000180006000070001c0007000038000e00038000180006000070001c0007000030000c0000c0003000020000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"),
            (raw"^(?i:(.+)\1)$", "0000180006000070001c0007000038000e00038000180006000070001c0007000030000c0000c0003000039801980066006601980198006600660001980198006600660198019800660066000000000000000000000000000000000000000000000000000000"),
            (raw"^(?<n>.)(?i:\k<n>)+$", "0000180006000070001c0007000038000e00038000180006000070001c0007000030000c0000c0003000039801980000000001980198000000000000000000006600660000000000660066c30000000c3000000000038e38000038e38000038e380000000001"),
            (raw"^(?i:(?:(.)\1)+)$", "0000180006000070001c0007000038000e00038000180006000070001c0007000030000c0000c0003000039867986600000001986798660000000000000001986798660000000198679866000000000000000000000000000000000000000000000000000000"),
            (raw"(?<=(?i:\1)(.))-", "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001041000000041000000000041040000041040000041040000000001"),
            (raw"^(\u00df)(?i:\1)$", "000000000000000000000000000000000000000000180000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"),
            (raw"^(\u03c2)(?i:\1)$", "000000000000000000000000000000000000000000000000000000001c00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"),
            (raw"^(\u{10400})(?i:\1)$", "0000000000000000000000000000000000000000000000000000000000000000000000000000c0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"),
            # Others.
            (raw"^(.+)(?i:\1)$", "0000180006000070001c0007000038000e00038000180006000070001c0007000030000c0000c0003000039801980066006601980198006600660001980198006600660198019800660066000000000000000000000000000000000000000000000000000000"),
            (raw"^(?i:(k)\1)$", "0000000000000070001c0007000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"),
            (raw"^(?i:(s+)\1)$", "000000000000000000000000000038000e00038000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"),
            (raw"^([a-z]+)(?i:\1)$", "000018000000007000000000000038000000000000000000000000000000000000000000000000000000019800000066000000000000000000000001980000006600000000000000000000000000000000000000000000000000000000000000000000000000"),
            (raw"^(?i:(.)(?-i:\1))$", "000010000200004000080001000020000400008000100002000040000800010000200004000080001000020000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"),
            (raw"^(.)(?i:\1)(?-i:\1)$", "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000820000000410000000000208200000104100000082080000000001"),
            (raw"^(?:(.)|-)(?i:\1)$", "0000380006000070001c0007000038000e00038000180006000070001c0007000030000c0000c0003000020000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"),
            (raw"^(?i:(.)\1\1)$", "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000c30000000c3000000000038e38000038e38000038e380000000001"),
            (raw"(?<=(.)(?i:\1))-", "00000000080002000080002000080002000080002000080002000080002000080002000080002000080002000000000000000000000000000000000000000000000000000000000000000104104107f04104107f04104107f04104107f04104107f04104107f"),
            # Valid only with no flag, with a group that holds no character the two grammars compare differently.
            (raw"^([a-j]+)(?i:\1)\&?$", "000018000000000000000000000000000000000000000000000000000000000000000000000000000000019800000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"),
            (raw"^(?i:([a-jA-J])\1)\&?-$", "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000041000000041000000000000000000000000000000000000000000"),
        ]
        checkv8table(rows, V8_CASE_TEXTS)
        for (pattern, _) in rows
            @test E.unsupportedreason(pattern) === nothing
        end
        @test E.translate("^(.)(?i:\\1)\$").main ==
            "\\A(?<g1>[^\\x{a}\\x{d}\\x{2028}-\\x{2029}])(?:(?i:\\k<g1>))\\z"
        # A group that holds no character with a case variant is compared as it is.
        @test E.translate("^([0-9]+)(?i:\\1)\$").main == "\\A(?<g1>[\\x{30}-\\x{39}]+)(?:\\k<g1>)\\z"
        # A pattern that is only valid with no flag compares by the uppercase mapping of a UTF-16 code unit, which
        # is not what PCRE2 does for k and U+212A, among others. It is refused when its group can hold a character
        # with a case variant and one of those.
        for pattern in ["^(.)(?i:\\1)\\&?\$", "^(?i:(.+)\\1)\\&?\$", "^([a-z]+)(?i:\\1)\\&?\$", "^(k)(?i:\\1)\\&?\$",
            "^(a|\\u212a)(?i:\\1)\\&?\$"]
            @test E.unsupportedreason(pattern) == E.REFUSED_CASE_INSENSITIVE_BACKREFERENCE
            @test !E.parsesunicode(pattern)
        end
        # The pairs of characters Unicode 15.1 and 16 made equivalent, each both ways round. V8 13.6 holds every
        # one of them equivalent. So does a PCRE2 with the data of Unicode 16 (10.46, in Julia 1.13). One with the
        # data of Unicode 14 (10.42, in Julia 1.10) holds none of them equivalent, and nothing is done about that:
        # the README lists the pairs and says that the fix is Julia 1.13.
        texts = String[]
        for (a, b) in PAIRS_SINCE_UNICODE_14
            push!(texts, string(Char(a), Char(b)), string(Char(b), Char(a)))
        end
        @test length(texts) == 60
        differences = v8differences(raw"^(.)(?i:\1)$", "fffffffffffffff", texts)
        engine = VersionNumber(E.pcreunicodeversion())
        if engine >= v"16"
            @test isempty(differences)
        elseif engine < v"15"
            @test differences == texts
        else
            @test issubset(differences, texts)
        end
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
    end

    # A backreference after a lookbehind of no fixed length reads a group of it. ECMA-262 matches the lookbehind
    # backwards, and the group holds what that captures. PCRE2 sets a group only where the match passes over its
    # text, so the lookbehind is written forwards in a form that captures the same, where there is one. The
    # translator used to refuse every such pattern.
    @testset "groups of a lookbehind of no fixed length" begin
        rows = [
            # The three of the test data.
            (raw"(?<=([abc])+)x\1", "00008000000a"),
            (raw"(?:(?:(?<=$|(|a)\b)(|-)?|.x){4,}?\1){2,}a", "4e27fc49c49a"),
            (raw"((?:(|a)\2|(?<!x)){0}?||(?<=(?<!|\1)([^a]|[^a][^a]){3}|a|a)\3){1,}(?:(|){1,2}(\B|(?=x)|){1,}){2}$", "ffffffffffff"),
            # A greedy repetition that a lookbehind matches last runs as often as it can, and its groups are those of the
            # leftmost iteration.
            (raw"(?<=(a|x)+)-\1", "000010040035"),
            (raw"(?<=(a|x)*)-\1", "10381407ff35"),
            (raw"(?<=(a|x){2,})-\1", "000000000024"),
            (raw"(?<=(a|x){1,2})-\1", "000010040035"),
            (raw"(?<=(a|x){0,2}-)\1", "10381407ff35"),
            (raw"(?<=(?:(a)|x)+)-\1", "0040109e0835"),
            (raw"(?<=((a)|x){2,3})-\2", "000000900024"),
            (raw"(?<=(a+))-\1", "000010000010"),
            (raw"(?<=(a*)x)-\1", "0040001e0801"),
            (raw"(?<=(x)*a)-\1", "02011c004034"),
            (raw"(?<=((?:a|x)-)+)\1", "000000000011"),
            (raw"(.)(?<=([ax])+-)\2\1", "000000000011"),
            (raw"(?<=([ax])+)-(?<=(x)*-)\1\2", "000010000034"),
            (raw"(?<=(a|x)+)-(?:\1|x)$", "000018040024"),
            (raw"^(?:a|x)*(?<=(a|x)+)-\1", "000010040031"),
            # A part before a group at a fixed distance from the end does not say what the group holds.
            (raw"(?<=a*(x))-\1", "000000040001"),
            (raw"(?<=a+(x|-))\1", "000044000000"),
            (raw"(?<=(?:a|xx)?(.)-)\1", "000010040111"),
            (raw"(.)(?<=\1*(x)-)\2", "000000040001"),
            (raw"(?:(?<=a*(x))-\1)+$", "000000040000"),
            # Alternatives after one that matches the empty string anywhere are never taken, and a part that cannot match
            # sets no group.
            (raw"(?<=(|a)x)-\1", "0040201e0801"),
            (raw"(?<=(a??)x)-\1", "0040201e0801"),
            (raw"(?<=(?<!|\1)(a)|x)-\1", "0040201e0801"),
            (raw"(?<=(?!)(a+)|x)-\1", "0040201e0801"),
            (raw"(?<=(?:(a+)(?!))?x)-\1", "0040201e0801"),
            # Alternatives: the first that matches is taken, and the match does not come back into the lookbehind.
            (raw"(?<=^|(a){2,}x)\1", "ffffffffffff"),
            (raw"(?<=(a|)x)\1", "21d0cbfe3a6f"),
            (raw"(?<=(ax|a)-)\1", "000010000010"),
            (raw"(?<=(a|ax)-)\1", "000010000010"),
            (raw"(?<=(?:(a)|(xa)|)-)\1\2", "1078341fff15"),
            (raw"(?<=a|(x)+)-\1", "02011c844035"),
            (raw"(?<=(x)+|(a)-)\1\2", "008050701050"),
            (raw"(.)(?<=\1+|(a)+)-\2", "02493c9e4f35"),
            (raw"(?<=(?:(a)|.)+)x\1", "00908174ba6f"),
            (raw"(?<=(?:(a)|[ax])+)-\1", "0040109e0835"),
            (raw"(?<=(?:(a)|.)*-)\1$", "104810924901"),
        ]
        checkv8table(rows)
        for (pattern, _) in rows
            @test E.unsupportedreason(pattern) === nothing
        end
        # A group in an end of a fixed length: that end, and then a lookbehind for what stands before it.
        @test E.translate("(?<=a*(x))-\\1").main == "(?<=(?<g1>x)(?<=(?C2)(?s:.){1}))\\x{2d}\\k<g1>"
        @test E.translate("(?<=a*(x))-\\1").lookbehinds == ["(?C1)(?:a*)(?C0)"]
        # A repetition first: a search for where it starts, with the rest of the pattern inside the search. The
        # search stops where the lookbehind first matches, as the condition of a group does not take another way.
        @test E.translate("(?<=(a)+)x\\1").main == "(?=(?<r1>(?s:.*)))(?=(?&w2))(?(DEFINE)(?<w2>(?(?=(?<m3>" *
            "(?<!(?:a))(?<g1>a)(?:a)*(?=\\k<r1>\\z)))\\k<m3>x\\k<g1>|(?<=(?=(?&w2))(?:a)))))"
        @test isempty(E.translate("(?<=(a)+)x\\1").lookbehinds)
        # A group that can never take part is not written, and nor is a part that can never match.
        @test E.translate("(?<=(|a)x)-\\1").main == "(?<=(?:)x)\\x{2d}"
        @test E.translate("(?<=(?<!|\\1)(a)|x)-\\1").main == "(?<=x)\\x{2d}"
        # A pattern with no backreference is written as it was.
        @test E.translate("(?<=(|a)x)-").lookbehinds == ["(?C1)(?:(?:|a)x)(?C0)"]
        @test E.translate("a(?!)b|c").main == "a(?!)b|c"
        # The match does not come back into the lookbehind: with the group set to "a", this cannot match "ax-".
        @test !matches("(?<=(?:(a)|.)+)x\\1", "ax-")
        @test matches("(?<=(?:(a)|.)+)x\\1", "axa")
        @test matches("(?<=([abc])+)x\\1", "cb" * "a"^300 * "xc")
        @test !matches("(?<=([abc])+)x\\1", "-b" * "a"^300 * "xc")
        # What is still refused: the lookbehind is not a term of the pattern itself, or what its groups hold
        # depends on the order in which ECMA-262 tries the ways through it from its end.
        for pattern in ["(?:(?<=(a)+)x\\1)+", "(?<=(a*)(x+))-\\1", "(?<=(\\ba*))x\\1", "(?<=(a+|x)-)\\1"]
            @test E.unsupportedreason(pattern) == E.REFUSED_GROUP_IN_LOOKBEHIND
            @test E.parsesunicode(pattern)
        end
    end
end
