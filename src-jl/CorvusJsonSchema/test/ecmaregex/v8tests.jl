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

# The texts of the tables of what each grammar takes.
const V8_NAME_TEXTS = ["", "a", "k", "K", "x", "p", "\\", "<", "ak", "ka", "kk", "xk", "aa", "xa", "k<a>", "a<a>",
    "\u212a", "\u30ab", "\u3042", "\u30fc", "\u30ab\u3042", "z", "-", "pp"]

# Checks rows of a pattern, whether V8 takes it with the u flag, whether it takes it without, and its answers on
# V8_NAME_TEXTS, which are those with the u flag when it takes the pattern with it.
function checkgrammars(rows)
    for (pattern, unicode, legacy, hex) in rows
        @test E.parsesunicode(pattern) == unicode
        @test E.isvalidpattern(pattern) == unicode
        @test isnotapattern(pattern) == !(unicode || legacy)
        (unicode || legacy) && checkv8table([(pattern, hex)], V8_NAME_TEXTS)
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
        @test E.translate("^(?:a|\\b)*\$").main == "\\A(?:a*)\\z"
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

    # Two classes that another port answered wrongly. A lookahead may set a group on only some of its ways to match,
    # and the backreference then reads nothing. And a group of a lookaround is put back when the match goes back to
    # before the lookaround, so a backreference in a later iteration does not read what an abandoned way left.
    # PCRE2 does both as ECMA-262 does, and the translation was right. The tables keep it so.
    @testset "groups of a lookahead" begin
        rows = [
            # A group that a lookahead sets on only some of its ways to match.
            (raw"(?=(a)?x)\1", "25d2ebfeba6f"),
            (raw"(?=(a)?x)\1x", "25d2ebfeba6f"),
            (raw"(?=(?:(a)|x)*-)\1x", "0040201e0801"),
            (raw"(?=(a|(x))-?)\1\2", "4fa7fff9f4fe"),
            (raw"(?:(?=(a)?x).\1)+$", "24924924926c"),
            (raw"(?!(a)?x)\1-", "12793c9fff35"),
            # A group of a lookaround and a backreference to it in one repetition, with a way to go back between them.
            (raw"^(?:(?=([ax]))[ax](?:-|-a)\1)*$", "800010040000"),
            (raw"^(?:(?=(a|x))[ax](?:-|-a)\1)*$", "800010040000"),
            (raw"^(?:(?=(.))(?:a|ax)\1)+$", "080080000080"),
            (raw"^(?:(?=(a)?)(?:x|xa)\1)*-$", "104000900000"),
        ]
        checkv8table(rows)
        for (pattern, _) in rows
            @test E.unsupportedreason(pattern) === nothing
        end
        @test matches("(?=(a)?b)\\1", "b")
        @test matches("(?=(a)?b)\\1", "ab")
        @test !matches("(?=(a)?b)\\1", "a")
        @test !matches("^(?:(?=([ab]))[ab](?:c|ca)\\1)*\$", "acab")
        @test matches("^(?:(?=([ab]))[ab](?:c|ca)\\1)*\$", "aca")
        @test matches("^(?:(?=([ab]))[ab](?:c|ca)\\1)*\$", "acaa")
        @test matches("^(?:(?=([ab]))[ab](?:c|ca)\\1)*\$", "acabcb")
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
            # A lazy repetition with nothing to its left but parts that match the empty string anywhere runs its
            # minimum too.
            (raw"(?<=a?(x*?))-\1", "12793c9fff35"),
            (raw"(?<=(a)*x+?)-\1", "0040001e0801"),
            (raw"(?<=(a)*x+?|-+?)\1", "33f9ffffff7f"),
            (raw"(?<=a*(x){1,2}?)-\1", "000000040001"),
            (raw"(?<=(?:a|)(x)+?)-\1", "000000040001"),
            (raw"(?<=a*(x|-)??)\1", "ffffffffffff"),
        ]
        checkv8table(rows)
        for (pattern, _) in rows
            @test E.unsupportedreason(pattern) === nothing
        end
        # A group in an end of a fixed length: that end, and then a lookbehind for what stands before it.
        @test E.translate("(?<=a*(x))-\\1").main == "(?<=(?<g1>x)(?<=(?C2)(?s:.){1}))\\x{2d}\\k<g1>"
        @test E.translate("(?<=a*(x))-\\1").lookbehinds == ["(?C1)(?>(?:(?:(?C254)(?s:.))*(?C254)[^\\x{61}])?)" *
            "(?:(?C254)[\\x{61}])*(?:(?:(?C254)a)*)(?C0)"]
        # A repetition first: a search for where it starts, with the rest of the pattern inside the search. The
        # search stops where the lookbehind first matches, as the condition of a group does not take another way.
        @test E.translate("(?<=(a)+)x\\1").main == "(?=(?<r1>(?s:.*)))(?=(?&w2))(?(DEFINE)(?<w2>(?(?=(?<m3>" *
            "(?<!(?:a))(?<g1>a)(?:a)*(?=\\k<r1>\\z)))\\k<m3>x\\k<g1>|(?<=(?=(?&w2))(?:a)))))"
        @test isempty(E.translate("(?<=(a)+)x\\1").lookbehinds)
        # A group that can never take part is not written, and nor is a part that can never match.
        @test E.translate("(?<=(|a)x)-\\1").main == "(?<=(?:)x)\\x{2d}"
        @test E.translate("(?<=(?<!|\\1)(a)|x)-\\1").main == "(?<=x)\\x{2d}"
        # A pattern with no backreference is written as it was.
        @test E.translate("(?<=(|a)x)-").lookbehinds == ["(?C1)(?>(?:(?:(?C254)(?s:.))*(?C254)[^\\x{61}\\x{78}])?)" *
            "(?:(?C254)[\\x{61}\\x{78}])*(?:(?:|(?:(?C254)a))(?:(?C254)x))(?C0)"]
        @test E.translate("a(?!)b|c").main == "a(?!)b|c"
        # The match does not come back into the lookbehind: with the group set to "a", this cannot match "ax-".
        @test !matches("(?<=(?:(a)|.)+)x\\1", "ax-")
        @test matches("(?<=(?:(a)|.)+)x\\1", "axa")
        @test matches("(?<=([abc])+)x\\1", "cb" * "a"^300 * "xc")
        @test !matches("(?<=([abc])+)x\\1", "-b" * "a"^300 * "xc")
        # What is still refused: the lookbehind is not a term of the pattern itself, or what its groups hold
        # depends on the order in which ECMA-262 tries the ways through it from its end.
        for pattern in ["(?:(?<=(a)+)x\\1)+", "(?<=(a*)(x+))-\\1", "(?<=(\\ba*))x\\1", "(?<=(a+|x)-)\\1",
            "(?<=a(x){1,2}?)-\\1"]
            @test E.unsupportedreason(pattern) == E.REFUSED_GROUP_IN_LOOKBEHIND
            @test E.parsesunicode(pattern)
        end
    end

    # The classes below were found by the six later families of the ECMA-262 regular expression test data.

    # A pattern that is valid only without the u flag matches UTF-16 code units, as ECMA-262 says. A character beyond
    # the Basic Multilingual Plane is two of them, to the dot, a class, a quantifier, an assertion and a
    # backreference, in the text and in the pattern. The texts are every text of up to three of "a", U+10400, U+10428
    # and U+1F600, and seven more.
    @testset "patterns valid only without the u flag match code units" begin
        texts = [""]
        for n in 1:3, t in Iterators.product(ntuple(_ -> ("a", "\U10400", "\U10428", "\U1F600"), n)...)
            push!(texts, join(reverse(t)))
        end
        append!(texts, ["x", "uu", "-", "\U10400x", "aa\U10400\U10400", "\U10400"^4, "\u00e9"])
        @test length(texts) == 92
        @test texts[1:7] == ["", "a", "\U10400", "\U10428", "\U1F600", "aa", "a\U10400"]
        rows = [
            # The dot, a class and a negated class take one code unit.
            (raw"^\-?.$", "40000000000000000000051"),
            (raw"^\-?..$", "3c000000000000000000020"),
            (raw"\-{0}^.{3}$", "03c44400000000000000008"),
            (raw"^\-?[^a]$", "00000000000000000000051"),
            (raw"^\-?[^a]{2}$", "38000000000000000000020"),
            (raw"\-{0}^\W\W$", "38000000000000000000000"),
            (raw"\-{0}\S{3}$", "03fffffffffffffffffff8e"),
            (raw"\-{0}^[\s\S]{4}$", "003bbbc4440004000400000"),
            # A character beyond the Basic Multilingual Plane in the pattern is two code units, and a quantifier repeats the second.
            (raw"^\-?𐐀*$", "20000000000000000000000"),
            (raw"^\-?𐐀?x$", "00000000000000000000008"),
            (raw"\-{0}^𐐀*?a", "00400000078000000000000"),
            (raw"\-{0}^𐐀{2}$", "00000000000000000000000"),
            (raw"\-{0}^x𐐀?$", "00000000000000000000000"),
            (raw"\-{0}^[a𐐀]+$", "66600660066000000000006"),
            (raw"\-{0}^[a-𐐀]+$", "6660066006600000000006f"),
            (raw"\-{0}^[^𐐀]$", "40000000000000000000051"),
            (raw"\-{0}𐐀|😀", "2afafafafffffafafffff8e"),
            # The escapes of a surrogate pair are two code units too, and \u{...} is the letter u counted.
            (raw"\-{0}^\ud801\udc00*$", "20000000000000000000000"),
            (raw"\-{0}^\ud801\udc00{0}$", "00000000000000000000000"),
            (raw"\-{0}^\u{2}$", "00000000000000000000020"),
            (raw"\-{0}^[\u{10400}]+$", "00000000000000000000020"),
            (raw"^\-?[\ud800-\udbff][\udc00-\udfff]$", "38000000000000000000000"),
            (raw"^\-?[^\ud800-\udfff]$", "40000000000000000000051"),
            (raw"^\-?[\ud801-\udc28]+$", "30330000003300330000002"),
            (raw"\-{0}\ud801$", "00000000000000000000000"),
            (raw"\-{0}^\udc00", "00000000000000000000000"),
            # Assertions stand between code units.
            (raw"\-{0}(?<=\ud801)\udc00", "227a227a27fffa7a227a20e"),
            (raw"\-{0}(?<!\ud801)\udc00", "00000000000000000000000"),
            (raw"\-{0}\ud801(?!\udc00)", "11179117911797fff917900"),
            (raw"\-{0}\ud83d\B", "0888f888f888f888fffff80"),
            (raw"\-{0}^.\b", "4380007ff80000000000040"),
            (raw"\-{0}(?<=^..)a", "00444400078007800780000"),
            (raw"\-{0}(?<=a.*)\udc28$", "01000111110001000100000"),
            # A backreference compares code units.
            (raw"\-{0}^(.)\1$", "04000000000000000000020"),
            (raw"\-{0}^(..)\1$", "00210800000000000000000"),
            (raw"\-{0}^(.).\1", "00330c00007f807f8000782"),
            (raw"\-{0}^(?:(.)\1)+$", "04000000000000000000020"),
            # A group name is read by character.
            (raw"^\-?(?<𐐀>.)\k<𐐀>$", "04000000000000000000020"),
            (raw"^\-?(?<\ud801\udc00>.)\k<𐐀>$", "04000000000000000000020"),
            (raw"^\-?(?<\u{10400}>..)\k<\ud801\udc00>$", "00210800000000000000000"),
            # A case-insensitive group. A surrogate has no other case.
            (raw"\-{0}(?i:^𐐀$)", "20000000000000000000000"),
            (raw"\-{0}(?i:^[𐐨a]$)", "40000000000000000000000"),
            (raw"\-{0}(?i:^.A$)", "04000000000000000000000"),
        ]
        checkv8table(rows, texts)
        for (pattern, _) in rows
            @test !E.parsesunicode(pattern)
        end
        # A class range from one such character to another runs from a low surrogate down to a high one, which is
        # an error in both grammars. So is a group name that is half of a character.
        @test isnotapattern(raw"\-{0}^[𐐀-𐐨]$")
        @test isnotapattern(raw"^\-?[😀-𐐀]$")
        @test isnotapattern(raw"^\-?(?<\ud801>.)$")
        @test !isnotapattern(raw"^[𐐀-𐐨]$")
        # A surrogate is written as a character of its own from U+F0000, and the text is written the same way.
        @test E.translate(raw"\-{0}^𐐀*$").main == raw"\A\x{f0001}\x{f0400}*\z"
        @test E.translate(raw"^\-?[\ud800-\udbff]$").main == raw"\A\x{2d}?[\x{f0000}-\x{f03ff}]\z"
        @test String(E.codeunits!(UInt8[], codeunits("a\U10400\u00e9\U1F600"))) ==
              "a\U000f0001\U000f0400\u00e9\U000f003d\U000f0600"
        # A pattern that names no surrogate is written as it was.
        @test E.translate(raw"^\-?.$").main == raw"\A\x{2d}?[^\x{a}\x{d}\x{2028}-\x{2029}]\z"
        # Bytes, a string and a view are all matched as code units.
        dot = E.compile(raw"^\-?.$")
        @test !E.ismatch(dot, "\U10400")
        @test !E.ismatch(dot, codeunits("\U10400"))
        @test !E.ismatch(dot, SubString("x\U10400", 2))
        @test !E.ismatch(dot, view(Vector{UInt8}("x\U10400"), 2:5))
        @test E.ismatch(dot, "\u00e9")
        # A pattern that is valid with the u flag matches characters.
        @test E.ismatch(E.compile("^.\$"), "\U10400")
    end

    # A lookbehind of no fixed length is a search for where its body starts. The body is held to the place the
    # lookbehind stands at, and the starts are tried from that place backwards. Before, the body ran on past the
    # place and the starts were tried from the start of the text, and each of these patterns reached the limit on
    # the steps of a search on some of the long texts, where V8 answers at once.
    @testset "the search for a lookbehind" begin
        long = ["aaaaaaaaaaaaa", "aaaaaaaaaaaaaa-", "aaaaaaaaaaax", "aaaaaaaaaaxa", "aaaaaaaaaxaaaaaaaaaa",
            "axaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxax-", "xaaaaaaaaaaaaaa", "-axaxaxaxaxaxaxaxaxaxaxaxaxaxaxax",
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-a"]
        matched = [
            (raw"(?<=((?:[ax]+){2,}))$", ["aaaaaaaaaaaaa", "aaaaaaaaaaax", "aaaaaaaaaaxa", "aaaaaaaaaxaaaaaaaaaa", "xaaaaaaaaaaaaaa", "-axaxaxaxaxaxaxaxaxaxaxaxaxaxaxax"]),
            (raw"(?<=((?:(?:\b)*[ax]+){2,}))$", ["aaaaaaaaaaaaa", "aaaaaaaaaaax", "aaaaaaaaaaxa", "aaaaaaaaaxaaaaaaaaaa", "xaaaaaaaaaaaaaa", "-axaxaxaxaxaxaxaxaxaxaxaxaxaxaxax"]),
            (raw"(?<=((?:(?:[ax]+|\2){1,2}|(?:(a){0})?|(?:[ax]+x{0}){2}?){2}))$", ["aaaaaaaaaaaaa", "aaaaaaaaaaaaaa-", "aaaaaaaaaaax", "aaaaaaaaaaxa", "aaaaaaaaaxaaaaaaaaaa", "axaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxax-", "xaaaaaaaaaaaaaa", "-axaxaxaxaxaxaxaxaxaxaxaxaxaxaxax", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-a"]),
            (raw"(?<=((?:()|(?:[ax]+|(?<!))+(?:\b|(?!)){2}?){1,3}))$", ["aaaaaaaaaaaaa", "aaaaaaaaaaaaaa-", "aaaaaaaaaaax", "aaaaaaaaaaxa", "aaaaaaaaaxaaaaaaaaaa", "axaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxax-", "xaaaaaaaaaaaaaa", "-axaxaxaxaxaxaxaxaxaxaxaxaxaxaxax", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-a"]),
            (raw"(?<=((?:(?:(?:x|)+|a){3}|(?<=)){3}))$", ["aaaaaaaaaaaaa", "aaaaaaaaaaaaaa-", "aaaaaaaaaaax", "aaaaaaaaaaxa", "aaaaaaaaaxaaaaaaaaaa", "axaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxax-", "xaaaaaaaaaaaaaa", "-axaxaxaxaxaxaxaxaxaxaxaxaxaxaxax", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-a"]),
            (raw"(?<=(?:a|aa)+)x", ["aaaaaaaaaaax", "aaaaaaaaaaxa", "aaaaaaaaaxaaaaaaaaaa", "axaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxax-", "-axaxaxaxaxaxaxaxaxaxaxaxaxaxaxax"]),
            (raw"(?<!(?:a+|x)+-)a$", ["aaaaaaaaaaaaa", "aaaaaaaaaaxa", "aaaaaaaaaxaaaaaaaaaa", "xaaaaaaaaaaaaaa"]),
            (raw"(?<=(?=a)(?:[ax]+)+)-", ["aaaaaaaaaaaaaa-", "axaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxax-", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-a"]),
            (raw"(?<=^(?:a|x)+)-", ["aaaaaaaaaaaaaa-", "axaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxaxax-", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-a"]),
            (raw"(?<=(?:a+)+x)a$", ["aaaaaaaaaaxa"]),
            (raw"(?<=-(?:a|x)+(?!a))x", ["-axaxaxaxaxaxaxaxaxaxaxaxaxaxaxax"]),
        ]
        for (pattern, texts) in matched
            compiled = E.compile(pattern)
            for text in long
                got = try
                    E.ismatch(compiled, text)
                catch e
                    e isa E.MatchError || rethrow()
                    sprint(showerror, e)
                end
                got == (text in texts) || println("[v8tests] ", repr(pattern), " on ", repr(text), ": ", got)
                @test got == (text in texts)
            end
        end
        checkv8table([
            (raw"(?<=((?:[ax]+){2,}))$", "0d86c361b0ca"),
            (raw"(?<=((?:(?:\b)*[ax]+){2,}))$", "0d86c361b0ca"),
            (raw"(?<=((?:(?:[ax]+|\2){1,2}|(?:(a){0})?|(?:[ax]+x{0}){2}?){2}))$", "ffffffffffff"),
            (raw"(?<=((?:()|(?:[ax]+|(?<!))+(?:\b|(?!)){2}?){1,3}))$", "ffffffffffff"),
            (raw"(?<=((?:(?:(?:x|)+|a){3}|(?<=)){3}))$", "ffffffffffff"),
            (raw"(?<=(?:a|aa)+)x", "0402e100800a"),
            (raw"(?<!(?:a+|x)+-)a$", "492482412482"),
            (raw"(?<=(?=a)(?:[ax]+)+)-", "02013c804034"),
            (raw"(?<=^(?:a|x)+)-", "02413c9e0031"),
            (raw"(?<=(?:a+)+x)a$", "000080000000"),
            (raw"(?<=-(?:a|x)+(?!a))x", "000000009000"),
        ])
        # How the body is written: the callout 254 before each character, and before the body what takes the
        # text up to the start that is tried. A body that begins with ^ has one start.
        @test E.translate("(?<=(?:a|aa)+)x").lookbehinds == [raw"(?C1)(?>(?:(?:(?C254)(?s:.))*(?C254)[^\x{61}])?)" *
            raw"(?:(?C254)[\x{61}])*(?:(?:(?:(?C254)a)|(?:(?C254)a)(?:(?C254)a))+)(?C0)"]
        @test E.translate("(?<=^a+)b").lookbehinds == [raw"(?C1)(?:\A(?:(?C254)a)+)(?C0)"]
        # Under a large count the characters are written without the callout, as PCRE2 keeps the count of a
        # character and writes a group out once for each iteration.
        @test E.translate("(?<=a{70}b*)x").lookbehinds == [raw"(?C1)(?>(?:(?:(?C254)(?s:.))*(?C254)[^\x{61}-\x{62}])?)" *
            raw"(?:(?C254)[\x{61}-\x{62}])*(?:a{70}(?:(?C254)b)*)(?C0)"]
        # The body is searched for only where the character before the lookbehind is one it can end with.
        @test E.translate(raw"(?<=(?:a+)+x)a$").main == raw"(?<=[\x{78}])(?C2)a\z"
        @test E.translate(raw"(?<!(?:a+|x)+-)a$").main == raw"(?!(?<=[\x{2d}])(?C2))a\z"
        @test E.translate("(?<=a?)x").main == "(?C2)x"
        # A lookahead inside the body sees the text after the lookbehind.
        @test matches("(?<=a+(?=.*x))-", "aa-x")
        @test !matches("(?<=a+(?=.*x))-", "aa-a")
        @test matches("(?<=a+(?=-x))-", "aa-x")
    end

    # PCRE2 keeps the count of a repeated character or class as a number and writes a repeated group out once for
    # each iteration, and it has a limit on what it writes. A group that the translation leaves with one character,
    # or with alternatives of one character each, is written as that character or class.
    @testset "a counted group of one character" begin
        rows = [
            (raw"^(?!-)(?:(?:(?:a|){1,300}x){1,300}-)+$", "004020100001"),
            (raw"^(?:(?:(a|)|x){1,70}-){1,70}$", "124924924911"),
            (raw"^(?:(?:[ax]|){2,90}-){0,90}$", "924924924911"),
        ]
        checkv8table(rows)
        @test E.translate(raw"^(?!-)(?:(?:(?:a|){1,300}x){1,300}-)+$").main ==
              raw"\A(?!\x{2d})(?:(?:(?:(?:a|)a{0,299})x){1,300}\x{2d})+\z"
        @test E.translate(raw"^(?:a|\b|x)*$").main == raw"\A(?:[\x{61}\x{78}]*)\z"
    end

    # ECMA-262 compares a case-insensitive backreference one character at a time, by Canonicalize, which is simple
    # case folding with the u flag. V8 13.6 compares the whole text by full case folding, under which U+00DF is
    # "ss" and U+FB00 is "ff", and so says that each of these matches. The answers here are those of ECMA-262.
    @testset "a case-insensitive backreference is compared character by character" begin
        @test !matches(raw"(?i:^(...)\1$)", "\u1e9eS\u00df\u1e9e\u00df\u017f")
        @test !matches(raw"(?i:^(..)\1$)", "\u00dfSs\u1e9e")
        @test !matches(raw"(?i:^(..)\1$)", "\ufb00ff\ufb00")
        @test !matches(raw"(?i:^(?<n0>\1|\u00df)*?(..\W){1,2}\2{2}$)", "\u00df\u1e9e\u017f\u00df\u1e9eS\u00df\u1e9e\u00df\u017f")
        # Character by character these are the same text.
        @test matches(raw"(?i:^(...)\1$)", "\u1e9eS\u00df\u00dfs\u1e9e")
        @test matches(raw"(?i:^(..)\1$)", "\u00dfS\u1e9e\u017f")
        @test matches(raw"(?i:^(.)(.)(.)\1\2\3$)", "\u1e9eS\u00df\u00dfs\u1e9e")
        @test !matches(raw"(?i:^(.)(.)(.)\1\2\3$)", "\u1e9eS\u00df\u1e9e\u00df\u017f")
    end

    # \k in a class. With the u flag it is not an escape there. Without it, it is the letter k, but in a pattern
    # with a named group, where \k is a reference to a group and nothing else, in a class as outside one. Such a
    # pattern was taken, with the class read as the letter k.
    @testset "\\k in a class" begin
        @test length(V8_NAME_TEXTS) == 24
        checkgrammars([
            (raw"[\k]", false, true, "20f200"),
            (raw"^[\k]$", false, true, "200000"),
            (raw"^[\k-z]$", false, true, "2c0004"),
            (raw"^[\kA]+$", false, true, "202000"),
            (raw"(a)[\k]", false, true, "008000"),
            (raw"(?:a)[\k]", false, true, "008000"),
            (raw"(?i:[\k])", false, true, "30f200"),
            (raw"[\k<a>]", false, true, "61ff00"),
            (raw"(a)[\k<a>]", false, true, "008b00"),
            (raw"(?<n>a)[\k]", false, false, ""),
            (raw"[\k](?<n>a)", false, false, ""),
            (raw"(?<a>x)[\k]", false, false, ""),
            (raw"(?<n>a)[\k<a>]", false, false, ""),
            (raw"[\k<a>](?<n>a)", false, false, ""),
            (raw"(?<a>x)[\k<a>]", false, false, ""),
            (raw"(?<n>a)^[\k<a>]$", false, false, ""),
            (raw"^[\k<a>]$(?<n>a)", false, false, ""),
            (raw"(?<n>a)[^\k]", false, false, ""),
            (raw"(?<n>a)[a-\k]", false, false, ""),
            # Outside a class it was already so.
            (raw"\k", false, true, "20f200"),
            (raw"(a)\k", false, true, "008000"),
            (raw"(?<n>a)\k", false, false, ""),
            (raw"(?<n>a)\k<n>", true, true, "000800"),
            (raw"\k<n>(?<n>a)", true, true, "40cf00"),
        ])
    end

    # A count whose first number is the greater is an error. The numbers are compared as they are written, and not
    # as they are kept, which is up to 2^31 - 2.
    @testset "the numbers of a count out of order" begin
        checkgrammars([
            (raw"a{2,1}", false, false, ""),
            (raw"a{2,01}", false, false, ""),
            (raw"a{02,1}", false, false, ""),
            (raw"a{002,2}", true, true, "000800"),
            (raw"a{10,9}", false, false, ""),
            (raw"a{9,10}x", true, true, "000000"),
            (raw"a{2147483647,2147483646}", false, false, ""),
            (raw"^a{1,2147483648}$", true, true, "400800"),
            (raw"^a{0,99999999999999999999}$", true, true, "c00800"),
        ])
        # V8 13.6 keeps a number up to 2^31 - 1 and compares what it kept, so it takes these three. ECMA-262 says
        # each is an error, and so does the package.
        for pattern in [raw"a{2147483648,2147483647}", raw"a{4294967296,4294967295}",
            raw"(?:a){99999999999999999999,99999999999999999998}"]
            @test isnotapattern(pattern)
            @test !E.isvalidpattern(pattern)
        end
        @test !isnotapattern(raw"a{2147483647,2147483648}")
        @test E.isvalidpattern(raw"a{99999999999999999998,99999999999999999999}")
    end

    # The Script value Katakana_Or_Hiragana, or Hrkt, is in PropertyValueAliases.txt, so ECMA-262 has it. No
    # character has it. V8 13.6 does not know the name. The answers here are V8's for the same pattern with
    # \P{Any} where it has \p{sc=Hrkt}, and \p{Any} where it has \P{sc=Hrkt}.
    @testset "the script that no character has" begin
        rows = [
            (raw"\p{sc=Hrkt}", "000000"),
            (raw"\P{sc=Hrkt}", "7fffff"),
            (raw"^\p{Script=Katakana_Or_Hiragana}$", "000000"),
            (raw"^\P{Script=Katakana_Or_Hiragana}$", "7f00f6"),
            (raw"^[\p{scx=Hrkt}a]$", "400000"),
            (raw"^[^\p{Script_Extensions=Katakana_Or_Hiragana}a]$", "3f00f6"),
            (raw"^[\P{scx=Hrkt}]+$", "7fffff"),
            (raw"^[^\P{sc=Hrkt}]$", "000000"),
            (raw"^[\P{sc=Hrkt}\p{sc=Hrkt}]$", "7f00f6"),
            (raw"^(?:\p{sc=Hrkt}|k)+$", "202000"),
            (raw"(?i:\P{scx=Katakana_Or_Hiragana}k)", "00b000"),
        ]
        checkv8table(rows, V8_NAME_TEXTS)
        for (pattern, _) in rows
            @test E.parsesunicode(pattern)
            @test E.isvalidpattern(pattern)
        end
        @test isempty(E.property("sc=Hrkt"))
        @test isempty(E.property("Script_Extensions=Katakana_Or_Hiragana"))
        @test E.property("scx=Hira") != E.property("sc=Hira")
        # The name is a value of Script and of Script_Extensions only, and it is matched exactly.
        for expression in ["Hrkt", "Katakana_Or_Hiragana", "sc=hrkt", "gc=Hrkt", "sc=Katakana_or_Hiragana", "sc=Hrkt "]
            @test !E.isproperty(expression)
            @test !E.parsesunicode("\\p{" * expression * "}")
        end
    end
end
