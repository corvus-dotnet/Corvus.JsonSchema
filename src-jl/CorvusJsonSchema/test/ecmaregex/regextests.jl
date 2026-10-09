# The translator of ECMA-262 patterns on the points where the Java port once differed from ECMA-262. This is the
# Java port's EcmaRegexTest. The expected answers are those of the Unicode Character Database and of V8. oracle.jl
# checks the translator against V8 in bulk.

const GRINNING_FACE = "\U1F600"

@testset "translator" begin
    # Every binary property of ECMA-262 (table 69) is the property, under its name and under its alias.
    @testset "binary properties are properties" begin
        names = ["ASCII", "ASCII_Hex_Digit", "AHex", "Alphabetic", "Alpha", "Any", "Assigned", "Bidi_Control",
            "Bidi_C", "Bidi_Mirrored", "Bidi_M", "Case_Ignorable", "CI", "Cased", "Changes_When_Casefolded", "CWCF",
            "Changes_When_Casemapped", "CWCM", "Changes_When_Lowercased", "CWL", "Changes_When_NFKC_Casefolded",
            "CWKCF", "Changes_When_Titlecased", "CWT", "Changes_When_Uppercased", "CWU", "Dash",
            "Default_Ignorable_Code_Point", "DI", "Deprecated", "Dep", "Diacritic", "Dia", "Emoji", "Emoji_Component",
            "EComp", "Emoji_Modifier", "EMod", "Emoji_Modifier_Base", "EBase", "Emoji_Presentation", "EPres",
            "Extended_Pictographic", "ExtPict", "Extender", "Ext", "Grapheme_Base", "Gr_Base", "Grapheme_Extend",
            "Gr_Ext", "Hex_Digit", "Hex", "IDS_Binary_Operator", "IDSB", "IDS_Trinary_Operator", "IDST",
            "ID_Continue", "IDC", "ID_Start", "IDS", "Ideographic", "Ideo", "Join_Control", "Join_C",
            "Logical_Order_Exception", "LOE", "Lowercase", "Lower", "Math", "Noncharacter_Code_Point", "NChar",
            "Pattern_Syntax", "Pat_Syn", "Pattern_White_Space", "Pat_WS", "Quotation_Mark", "QMark", "Radical",
            "Regional_Indicator", "RI", "Sentence_Terminal", "STerm", "Soft_Dotted", "SD", "Terminal_Punctuation",
            "Term", "Unified_Ideograph", "UIdeo", "Uppercase", "Upper", "Variation_Selector", "VS", "White_Space",
            "space", "XID_Continue", "XIDC", "XID_Start", "XIDS"]
        for name in names
            pattern = "^\\p{$name}\$"
            @test validandcompiles(pattern)
            # A name the u flag grammar does not know is read as the text "p{name}" by the grammar with no flag.
            @test !matches(pattern, "p{$name}")
            @test !matches("^(?:\\p{$name}|\\P{$name})\$", "p{$name}")
            @test matches("^(?:\\p{$name}|\\P{$name})\$", "a")
        end
        @test matches("^\\p{Dash}\$", "-")
        @test matches("^\\p{Dash}\$", "\u2014")
        @test !matches("^\\p{Dash}\$", "a")
        @test matches("^\\p{Diacritic}\$", "\u0301")
        @test matches("^\\p{Quotation_Mark}\$", "\u00ab")
        @test matches("^\\p{RI}\$", "\U1F1E6")
        @test matches("^\\p{Math}\$", "+")
        @test matches("^\\p{AHex}+\$", "09afAF")
        @test !matches("^\\p{AHex}+\$", "\uff10")
        @test matches("^\\p{Hex}+\$", "\uff10")
        # The names ECMA-262 does not list are not properties, whatever PCRE2 makes of them.
        for name in ["IsAlphabetic", "javaLowerCase", "Latin", "InGreek", "L&", "Letter&", "blank", "Titlecase",
            "Other_Alphabetic", "Hyphen", "Composition_Exclusion", "alpha", "ascii", "Space", "Xan", "Xwd", "Any=",
            "Greek", "L*", "^L", "Lu,Ll", "bidiclass=al"]
            @test !isvalid262("\\p{$name}")
        end
    end

    # Script_Extensions is its own property. U+0640 ARABIC TATWEEL has the script Common and nine extensions.
    @testset "Script_Extensions is not Script" begin
        tatweel = "\u0640"
        @test matches("^\\p{sc=Zyyy}\$", tatweel)
        @test matches("^\\p{Script=Common}\$", tatweel)
        @test !matches("^\\p{sc=Arab}\$", tatweel)
        @test !matches("^\\p{Script=Syriac}\$", tatweel)
        @test !matches("^\\p{scx=Zyyy}\$", tatweel)
        @test !matches("^\\p{Script_Extensions=Common}\$", tatweel)
        for script in ["Adlm", "Arab", "Mand", "Mani", "Ougr", "Phlp", "Rohg", "Sogd", "Syrc", "Arabic", "Syriac",
            "Adlam", "Old_Uyghur"]
            @test matches("^\\p{scx=$script}\$", tatweel)
            @test matches("^\\p{Script_Extensions=$script}\$", tatweel)
        end
        @test !matches("^\\p{scx=Latn}\$", tatweel)
        # U+3001 IDEOGRAPHIC COMMA is Common, and is used with Han, Hiragana and others.
        @test matches("^\\p{sc=Common}\$", "\u3001")
        @test matches("^\\p{scx=Hani}\$", "\u3001")
        @test matches("^\\p{scx=Hira}\$", "\u3001")
        @test !matches("^\\p{sc=Hani}\$", "\u3001")
        # A script with no extension of its own still has its members.
        @test matches("^\\p{scx=Grek}+\$", "\u03b1\u03b2")
        @test matches("^\\P{scx=Arab}\$", "a")
        @test !matches("^\\P{scx=Arab}\$", tatweel)
        # Scripts by every name ECMA-262 lists, whatever the Unicode version of the PCRE2 in use.
        for good in ["\\p{Script=Garay}", "\\p{sc=Gara}", "\\p{scx=Sidetic}", "\\p{sc=Unknown}", "\\p{sc=Zzzz}",
            "\\p{sc=Qaai}", "\\p{sc=Qaac}"]
            @test validandcompiles(good)
        end
        for bad in ["\\p{sc=Nope}", "\\p{sc=GREEK}", "\\p{sc=greek}", "\\p{scx=}", "\\p{Script}", "\\p{Greek}",
            "\\p{Block=Basic_Latin}"]
            @test !isvalid262(bad)
        end
    end

    # White_Space is the Unicode property, not the set of the \s escape.
    @testset "White_Space is the Unicode property" begin
        @test !matches("\\p{White_Space}", "\ufeff")
        @test matches("\\p{White_Space}", "\u0085")
        @test !matches("\\p{space}", "\ufeff")
        @test matches("\\p{space}", "\u0085")
        @test matches("^\\p{White_Space}+\$",
            "\t\n\u000b\f\r \u0085\u00a0\u1680\u2000\u200a\u2028\u2029\u202f\u205f\u3000")
        @test !matches("\\p{White_Space}", "\u200b")
        @test matches("\\s", "\ufeff")
        @test !matches("\\s", "\u0085")
        @test matches("\\P{White_Space}", "\ufeff")
        @test !matches("\\P{White_Space}", "\u0085")
    end

    # The emoji properties, which PCRE2 has only in its later versions.
    @testset "emoji properties on every Julia" begin
        @test matches("^\\p{Emoji}\$", GRINNING_FACE)
        @test !matches("^\\p{Emoji}\$", "a")
        @test matches("^\\p{Emoji}\$", "#")
        @test !matches("^\\p{Emoji}\$", "p{Emoji}")
        @test matches("^\\p{Emoji_Presentation}\$", GRINNING_FACE)
        @test !matches("^\\p{Emoji_Presentation}\$", "\u00a9")
        @test matches("^\\p{Emoji}\$", "\u00a9")
        @test matches("^\\p{Extended_Pictographic}\$", "\u00a9")
        @test matches("^\\p{Emoji_Modifier}\$", "\U1F3FB")
        @test matches("^\\p{Emoji_Modifier_Base}\$", "\U1F44D")
        @test matches("^\\p{Emoji_Component}\$", "\u200d")
        @test !matches("^\\P{Emoji}\$", GRINNING_FACE)
    end

    # The data is that of one Unicode version on every Julia. These code points were assigned after Unicode 13, and
    # the PCRE2 of Julia 1.10 has Unicode 14.
    @testset "properties do not depend on the Julia" begin
        # U+1F6DC WIRELESS (Unicode 15), U+1FAE9 FACE WITH BAGS UNDER EYES (Unicode 16).
        @test matches("^\\p{Emoji}\$", "\U1F6DC")
        @test matches("^\\p{So}\$", "\U1FAE9")
        # U+A7CB LATIN CAPITAL LETTER RAMS HORN (Unicode 16), U+10D50 GARAY CAPITAL LETTER A (Unicode 16).
        @test matches("^\\p{Lu}\$", "\ua7cb")
        @test matches("^\\p{Uppercase}\$", "\ua7cb")
        @test matches("^\\p{sc=Latin}\$", "\ua7cb")
        @test matches("^\\p{sc=Garay}\$", "\U10D50")
        @test matches("^\\p{L}\$", "\U10D50")
        # U+16D40 KIRAT RAI SIGN ANUSVARA and U+1CCF0 OUTLINED DIGIT ZERO (Unicode 16).
        @test matches("^\\p{Lm}\$", "\U16D40")
        @test matches("^\\p{Nd}\$", "\U1CCF0")
        # U+088F ARABIC LETTER NOON WITH RING ABOVE and U+A7CE LATIN CAPITAL LETTER PHARYNGEAL VOICED FRICATIVE
        # (Unicode 17, which also made U+0295 an Lo where it was an Ll).
        @test matches("^\\p{Lu}\$", "\ua7ce")
        @test matches("^\\p{Lo}\$", "\u0295")
        @test !matches("^\\p{Ll}\$", "\u0295")
        @test matches("^\\p{L}\$", "\u088f")
        @test matches("^\\p{Alphabetic}\$", "\u088f")
        @test matches("^\\p{Assigned}\$", "\u088f")
        @test !matches("^\\p{Cn}\$", "\u088f")
        @test matches("^\\p{Cn}\$", "\u0378")
        @test matches("^\\p{sc=Unknown}\$", "\u0378")
        # Case-insensitive matching is that of Unicode 17 too. U+A7CB folds to U+0264 since Unicode 16.
        @test matches("^(?i:\ua7cb)\$", "\u0264")
        @test matches("^(?i:\u0264)\$", "\ua7cb")
    end

    # General_Category values under each of their names.
    @testset "general categories" begin
        @test matches("^\\p{L}\\p{Letter}\\p{gc=L}\\p{General_Category=Letter}\$", "aaaa")
        @test matches("^\\p{LC}\\p{Cased_Letter}\$", "aA")
        @test !matches("^\\p{LC}\$", "\u00aa")
        @test matches("^\\p{Lo}\$", "\u00aa")
        @test matches("^\\p{M}\\p{Mark}\\p{Combining_Mark}\$", "\u0301\u0301\u0301")
        @test matches("^\\p{digit}\\p{Nd}\\p{Decimal_Number}\$", "123")
        @test matches("^\\p{punct}\\p{P}\\p{Punctuation}\$", "!!!")
        @test matches("^\\p{cntrl}\\p{Cc}\\p{Control}\$", "\u0001\u0001\u0001")
        @test matches("^\\p{Co}\\p{Private_Use}\$", "\ue000\ue000")
        @test matches("^\\p{C}\\p{Other}\$", "\u0378\u0001")
        @test matches("^\\p{Zs}\\p{Space_Separator}\\p{Z}\\p{Separator}\$", "  \u2028\u2029")
        for bad in ["\\p{gc=Dash}", "\\p{gc=ASCII}", "\\p{General_Category}", "\\p{L=}", "\\p{ L}", "\\p{l}",
            "\\p{Lu=Lu}", "\\p{sc=Greek=x}"]
            @test !isvalid262(bad)
        end
    end

    # A surrogate is no character of UTF-8 text. The Java port, whose text is UTF-16, matches \p{Cs} against a lone
    # surrogate. Here a pattern that names one is valid and matches nothing, however the bytes are written.
    @testset "surrogates match nothing" begin
        lone = String(UInt8[0xED, 0xA0, 0x80])
        for pattern in ["\\p{Cs}", "\\p{Surrogate}", "\\uD800", "[\\uD800-\\uDFFF]", "\\P{L}", "[^a]", ".", "(?s:.)",
            "\\p{Any}", "\\p{C}"]
            @test validandcompiles(pattern)
            @test !matches(pattern, lone)
        end
        @test matches("^\\P{Cs}\$", "a")
        @test matches("^[\\uD800-\\uE000]\$", "\ue000")
        @test matches("^[\\uD7FF-\\uDC00]\$", "\ud7ff")
        @test matches("^(?:\\uD800|a)\$", "a")
        @test matches("^\\uD800*a\$", "a")
        # A pair of surrogate escapes is one character.
        @test matches("^\\ud83d\\ude00\$", GRINNING_FACE)
        @test matches("^[\\ud83d\\ude00-\\ud83d\\ude4f]\$", "\U1F601")
        # Text that is not UTF-8 matches no character and is no error.
        for bytes in (UInt8[0xFF], UInt8[0x61, 0x80], UInt8[0xC3], UInt8[0xF0, 0x9F, 0x98])
            @test !E.ismatch(E.compile("^.+\$"), bytes)
            @test !E.ismatch(E.compile("^[^b]+\$"), bytes)
            @test E.ismatch(E.compile(""), bytes)
        end
        @test E.ismatch(E.compile("a"), UInt8[0x61, 0x80])
    end

    # The modifier groups of ECMAScript 2025.
    @testset "modifier groups" begin
        @test matches("^(?i:abc)\$", "AbC")
        @test !matches("^a(?i:b)c\$", "AbC")
        @test matches("^a(?i:b)c\$", "aBc")
        @test matches("^(?i:a(?-i:b)c)\$", "AbC")
        @test !matches("^(?i:a(?-i:b)c)\$", "ABC")
        @test matches("^(?i:[a-c]+)\$", "AbC")
        @test !matches("^(?i:[^a-c]+)\$", "A")
        # With the u flag grammar, case-insensitive matching is Unicode simple case folding.
        @test matches("^(?i:k)\$", "\u212a")
        @test matches("^(?i:\u00df)\$", "\u1e9e")
        @test !matches("^(?i:i)\$", "\u0131")
        @test !matches("^(?i:i)\$", "\u0130")
        @test matches("^(?i:\u03c3)\$", "\u03c2")
        @test matches("^(?i:\\w)\$", "\u017f")
        @test !matches("^\\w\$", "\u017f")
        @test matches("(?i:\\b)\u212a", "\u212a")
        # Dot-all and multiline.
        @test !matches("^.\$", "\n")
        @test matches("^(?s:.)\$", "\n")
        @test matches("^(?s:.)\$", "\u2028")
        @test !matches("^(?s:(?-s:.))\$", "\n")
        @test !matches("a\$", "a\nb")
        @test !matches("a\$", "a\n")
        @test matches("(?m:a\$)", "a\nb")
        @test matches("(?m:^b)", "a\nb")
        @test matches("(?m:^b)", "a\rb")
        @test matches("(?m:^b)", "a\u2028b")
        @test !matches("(?m:^b)", "a\u0085b")
        @test matches("\\r(?m:^)\\n", "\r\n")
        @test matches("\\n(?m:^\$)", "a\n")
        @test matches("^(?ims:A.b\$)", "a\nB\n")
        for bad in ["(?i)a", "(?-:a)", "(?ii:a)", "(?i-i:a)", "(?x:a)", "(?-i-s:a)", "(?i", "(?i:a", "(?u:a)",
            "(?g:a)"]
            @test !isvalid262(bad)
            @test isnotapattern(bad)
        end
        for good in ["(?i:a)", "(?-i:a)", "(?i-:a)", "(?ims:a)", "(?i-ms:a)", "(?s-i:a)", "(?m-is:a)"]
            @test validandcompiles(good)
        end
    end

    # A group name may be shared by groups in separate alternatives, and a backreference finds the one that matched.
    @testset "duplicate group names" begin
        @test matches("^(?:(?<a>x)|(?<a>y))\\k<a>\$", "xx")
        @test matches("^(?:(?<a>x)|(?<a>y))\\k<a>\$", "yy")
        @test !matches("^(?:(?<a>x)|(?<a>y))\\k<a>\$", "xy")
        @test !matches("^(?:(?<a>x)|(?<a>y))\\k<a>\$", "x")
        @test matches("^(?:(?<a>a)|(?<a>b))(?:(?<b>a)|(?<b>b))\\k<a>\\k<b>\$", "abab")
        @test !matches("^(?:(?<a>a)|(?<a>b))(?:(?<b>a)|(?<b>b))\\k<a>\\k<b>\$", "abba")
        @test matches("^(?<y>\\d{4})-\\d\\d|\\d\\d-(?<y>\\d{4})\$", "12-2026")
        @test validandcompiles("(?<a>x)|(?<a>y)|(?<a>z)")
        @test validandcompiles("(?<a>x)|(?:(?<a>y))")
        for bad in ["(?<a>x)(?<a>y)", "(?<a>(?<a>x))", "(?<a>x)|((?<a>y)(?<a>z))", "(?:(?<a>x)|(?<b>y))(?<a>z)",
            "(?:(?<a>x)|b)(?:(?<a>y)|c)"]
            @test !isvalid262(bad)
        end
        @test isnotapattern("(?<a>x)(?<a>y)")
        # A name is any ECMA-262 identifier, which PCRE2's own names are not.
        @test matches("^(?<\$_\u03c0\u0301\u200d9>a)\\k<\$_\u03c0\u0301\u200d9>\$", "aa")
        @test matches("^(?<" * "n"^100 * ">a)\\k<" * "n"^100 * ">\$", "aa")
    end

    # A character of a group name may be written as a \u escape.
    @testset "Unicode escapes in group names" begin
        @test matches("^(?<\\u0061>a)\\k<a>\$", "aa")
        @test matches("^(?<a>a)\\k<\\u0061>\$", "aa")
        @test matches("^(?<\\u{61}b>a)\\k<ab>\$", "aa")
        @test matches("^(?<\u03c0>a)\\k<\\u03c0>\$", "aa")
        @test validandcompiles("(?<\\u{1d4d1}>a)")
        @test validandcompiles("(?<\\ud835\\udcd1>a)")
        @test validandcompiles("(?<a\\u0030>a)\\k<a0>")
        for bad in ["(?<\\u0030>a)", "(?<a\\u0020>a)", "(?<\\x61>a)", "(?<\\u>a)", "(?<\\u{}>a)", "(?<a>a)\\k<b>"]
            @test !isvalid262(bad)
        end
    end

    # A lookbehind of any length, which PCRE2 runs itself only when the length is fixed.
    @testset "lookbehind of any length" begin
        @test matches("(?<=a*)b", "aab")
        @test matches("(?<=^a*)b", "aab")
        @test !matches("(?<=^a*)b", "cab")
        @test matches("(?<!^a*)b", "cab")
        @test !matches("(?<!^a*)b", "aab")
        @test matches("(?<=(\\d+)(\\d+))\$", "2015")
        @test !matches("(?<=(\\d+)(\\d+))\$", "x5")
        @test matches("(?<=(?:ab)+)x", "ababx")
        @test !matches("(?<=^(?:ab)+)x", "abax")
        @test matches("(?<=(?:a|ab){2})c", "aabc")
        @test matches("(?<=\\b\\w+)\\W", "ab!")
        @test matches("(?<=\u00e9.*)a", "\u00e9xxa")
        @test !matches("(?<=\u00e9.*)a", "\u00e9x\na")
        # A lookbehind counts characters, not bytes.
        @test matches("(?<=^.{2})a", GRINNING_FACE * "xa")
        @test !matches("(?<=^.{2})a", GRINNING_FACE * "a")
        @test matches("(?<=^.)a", GRINNING_FACE * "a")
        @test !matches("(?<=[^" * GRINNING_FACE * "]b)", GRINNING_FACE * "b")
        @test matches("(?<=" * GRINNING_FACE * ".?)a", GRINNING_FACE * "a")
        @test matches("(?<!" * GRINNING_FACE * ".*)a", "xa")
        @test !matches("(?<!" * GRINNING_FACE * ".*)a", GRINNING_FACE * "xa")
        @test matches("(?<=(?<!b)a)a", "aa")
        @test matches("(?<=^(?:a|bc)*)d", "abcad")
        # One lookbehind of no fixed length inside another, and one in a repetition.
        @test matches("(?<=(?<=a+)b+)c", "aabbc")
        @test !matches("(?<=(?<=a+)b+)c", "bbc")
        @test matches("^(?:a(?<=^a+))+\$", "aaaa")
        @test matches("(?<!(?<!x+)y+)z", "xyz")
        @test !matches("(?<!(?<!x+)y+)z", "yz")
        # Which form a lookbehind is written in.
        @test isempty(E.translate("(?<=a|bc)x").lookbehinds)
        @test isempty(E.translate("(?<=a{3}[bc])x").lookbehinds)
        @test length(E.translate("(?<=(?:a|bc))x").lookbehinds) == 1
        @test length(E.translate("(?<=a?)x").lookbehinds) == 1
        # A fixed length PCRE2 does not take is a callout too.
        @test matches("(?<=^a{70000})b", "a"^70000 * "b")
        @test !matches("(?<=^a{70000})b", "a"^69999 * "b")
    end

    # Backreferences mean what ECMA-262 says, or the pattern is refused.
    @testset "backreferences" begin
        @test matches("^(a)\\1\$", "aa")
        @test matches("^(a|b|c)+?\\1\$", "abb")
        @test !matches("^(a|b|c)+?\\1\$", "ab")
        @test matches("^(\u00e9|" * GRINNING_FACE * ")+\\1\$", "\u00e9" * GRINNING_FACE * GRINNING_FACE)
        # A group that did not take part matches the empty string.
        @test matches("^(a)?\\1b\$", "b")
        @test matches("^(?:(a)|b)\\1\$", "b")
        @test matches("^\\1(a)\$", "a")
        @test matches("^(a\\1)\$", "a")
        # The groups of a negative lookahead are never set outside it.
        @test matches("(?!(a))\\1b", "ab")
        # ECMA-262 unsets the groups of a repeated body when an iteration starts.
        @test matches("^(?:(a)|b)*\\1\$", "ab")
        @test !matches("^(?:(a)|b)*\\1\$", "ba")
        @test matches("^(?:(a)|b)*\\1\$", "baa")
        @test matches("^(?:(a)|(b))+\\1\\2\$", "abb")
        @test !matches("^(?:(a)|(b))+\\1\\2\$", "abab")
        @test matches("^(?:(?<a>x)|(?<a>y))+\\k<a>\$", "xyy")
        @test !matches("^(?:(?<a>x)|(?<a>y))+\\k<a>\$", "xyx")
        # A digit after a backreference is not part of its number.
        @test matches("^(a)\\1(?:0)\$", "aa0")
        @test matches("^(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)\\10\$", "abcdefghijj")
        # With one group, \10 is not a reference. The grammar with no flag reads it as the octal escape of U+0008.
        @test matches("^(a)\\10\$", "a\b")
    end

    # A pattern that is valid and whose backreference PCRE2 cannot give the meaning of is refused.
    @testset "refused patterns" begin
        refused = [
            "(?<=\\1(a))b" => E.REFUSED_BACKREFERENCE_IN_LOOKBEHIND,
            "(?<=(a|b)+?)c\\1" => E.REFUSED_GROUP_IN_LOOKBEHIND,
            "^(?:(?:(a)|b)\\1)+\$" => E.REFUSED_GROUP_IN_REPETITION,
            "^(?:(?=(a)))?\\1b" => E.REFUSED_GROUP_IN_LOOKAROUND,
            "^(a*)+\\1b" => E.REFUSED_GROUP_IN_EMPTY_REPETITION,
            "^(.)(?i:\\1)\$" => E.REFUSED_CASE_INSENSITIVE_BACKREFERENCE,
            "^(\u00df)(?i:\\1)\$" => E.REFUSED_CASE_INSENSITIVE_BACKREFERENCE,
            # With no flag ECMA-262 does not fold U+212A to k, and PCRE2 does.
            "^([a-z]+)-(?i:\\1)\\&?\$" => E.REFUSED_CASE_INSENSITIVE_BACKREFERENCE,
            "^(k)(?i:\\1)\\&?\$" => E.REFUSED_CASE_INSENSITIVE_BACKREFERENCE,
        ]
        for (pattern, reason) in refused
            @test E.unsupportedreason(pattern) == reason
            @test_throws E.PatternError E.compile(pattern)
            @test isvalid262(pattern) == !occursin("\\&", pattern)
        end
        message = try
            E.compile("^(.)(?i:\\1)\$")
            ""
        catch e
            sprint(showerror, e)
        end
        @test occursin(E.REFUSED_CASE_INSENSITIVE_BACKREFERENCE, message)
        @test occursin("^(.)(?i:", message)
        # A case-insensitive backreference is exact where the group can hold no character that folds differently.
        @test matches("^(?i:([0-9]+)-\\1)\$", "12-12")
        @test matches("^(?i:([a-j]+)-\\1)\$", "abc-ABC")
        @test !matches("^(?i:([a-j]+)-\\1)\$", "abc-ABD")
        @test matches("^([a-j]+)-(?i:\\1)\\&?\$", "abc-ABC")
        @test !matches("^([a-j]+)-(?i:\\1)\\&?\$", "abc-ABD")
        # With the u flag grammar every ASCII letter is exact, as ECMA-262 and PCRE2 both fold U+212A to k and
        # U+017F to s. (The Java port refuses these. The answers are V8's.)
        @test matches("^(?i:([a-z]+)-\\1)\$", "sky-SKY")
        @test matches("^(?i:([a-z]+)-\\1)\$", "sky-S\u212aY")
        @test matches("^(?i:([a-z]+)-\\1)\$", "s\u212ay-\u017fky")
        @test !matches("^(?i:([a-z]+)-\\1)\$", "sky-SKX")
        @test matches("^([a-z]+)-(?i:\\1)\$", "sky-S\u212aY")
        @test !matches("^([a-z]+)-\\1\$", "sky-S\u212aY")
        # A group that holds no character with a case variant is compared as it is.
        @test matches("^(\u00df)(?i:\\1)\\&?\$", "\u00df\u00df")
        @test !matches("^(\u00df)(?i:\\1)\\&?\$", "\u00df\u1e9e")
        # An invalid pattern is not a refused one.
        @test E.unsupportedreason("(a") === nothing
        @test E.verdict("(a") == 0
        @test E.verdict("(a)") == 1
        @test E.verdict("(?<=\\1(a))b") == 2
    end

    # Counts beyond what PCRE2 holds in one quantifier.
    @testset "large counts" begin
        @test matches("^a{65535}\$", "a"^65535)
        @test matches("^a{65536}\$", "a"^65536)
        @test !matches("^a{65536}\$", "a"^65535)
        @test !matches("^a{65536}\$", "a"^65537)
        @test matches("^a{2,70000}\$", "a"^70000)
        @test !matches("^a{2,70000}\$", "a"^70001)
        @test !matches("^a{2,70000}\$", "a")
        @test matches("^a{70000,}\$", "a"^70001)
        @test !matches("^a{70000,}\$", "a"^69999)
        @test matches("^.{0,200000}?b\$", "a"^150000 * "b")
        @test matches("^[ab]{131070}\$", "ab"^65535)
        @test !matches("^[ab]{131071}\$", "ab"^65535)
        # A count no text reaches is more than PCRE2 can write, and is refused.
        @test E.unsupportedreason("a{2147483648}") !== nothing
        @test startswith(E.unsupportedreason("a{2147483648}"), E.REFUSED_BY_ENGINE)
        @test isvalid262("a{2147483648}")
    end

    # Smaller points on which the translator differed from V8.
    @testset "other divergences" begin
        # Leading zeros in a \u{...} escape.
        @test matches("^\\u{0000000061}\$", "a")
        @test !isvalid262("\\u{110000}")
        @test !isvalid262("\\u{00000000110000}")
        # Without the u flag grammar \p is the letter p.
        @test matches("^\\p\\&\$", "p&")
        # What PCRE2 reads differently when it is given the pattern as it is.
        @test matches("^\\cJ\$", "\n")
        @test matches("^[\\b]\$", "\b")
        @test matches("^\\0\$", "\0")
        @test matches("^a\\Z\$", "aZ")
        @test matches("^\\Aa\\z\\G\\K\\R\\X\\h\\e\$", "AazGKRXhe")
        @test matches("^\\Q\\E\$", "QE")
        @test matches("^a{,2}\$", "a{,2}")
        @test matches("^a{1}{\$", "a{")
        @test !isvalid262("x*+")
        @test isnotapattern("x*+")
        @test isnotapattern("(?#comment)a")
        @test isnotapattern("(?>a)")
        @test isnotapattern("(?|a)")
        @test isnotapattern("(?P<n>a)")
        @test isnotapattern("(?'n'a)")
        @test isnotapattern("a(?R)")
        @test isnotapattern("(*FAIL)")
        @test matches("^[[:alpha:]]\$", "a]")
        @test !matches("^[[:alpha:]]\$", "a")
        @test matches("^[a&&b]\$", "&")
        @test matches("^[\\d-x]\$", "-")
        @test matches("^\\x{41}\$", "x"^41)
        @test matches("^\\N\$", "N")
        @test matches("^a\$", "a")
        @test !matches("^a\$", "a\n")
        @test !matches("^a\$", "\na")
        @test matches("^\\w\\W\$", "a\u00e9")
        @test !matches("^\\d\$", "\u0663")
        @test matches("^\\s\$", "\u2029")
        @test !matches("^\\b\u00e9", "\u00e9")
        @test matches("\\B\u00e9", "\u00e9")
    end

    # Where the PCRE2 of a Julia answers wrongly when it is given the pattern in the plain way, and what the module
    # writes instead.
    @testset "what PCRE2 gets wrong" begin
        # PCRE2 10.42 (Julia 1.10) works out wrongly where a pattern that starts with a lookahead can start, and
        # PCRE2 10.46 (Julia 1.13) where one that starts with a lazy optional item can. The module has PCRE2 work
        # nothing out.
        @test matches("b??a[ab]{0,3}?", "a")
        @test matches("b??(a[\\w-]{0,3}[ab]{0,3}?)", "a")
        @test matches("(?m:c??(?:b\\w{0,3}?){2}(?:a))", "bcba")
        @test !matches("b??a[ab]{0,3}?", "b")
        @test matches("(?=b)a?b", "b")
        @test matches("(?=b)(?:ab|b)", "bcac")
        @test matches("(?=bc)(?:ab|b)", "bc")
        @test !matches("(?=b)a?b", "a")
        @test matches("^(?=b)a?b", "b")
        @test matches("(?=.*b)a?b\$", "xb")
        # PCRE2 10.46 (Julia 1.13) loses part of a range that starts below U+0100 and ends above U+00FF, in a class
        # of six or more ranges. The module writes such a set as the negated class of what it lacks.
        ranges = "\\u0000-\\ua9ff\\uaa10-\\uaa13\\uaa20-\\uaa23\\uaa30-\\uaa33\\uaa40-\\uaa43\\uaa50-\\uaa53"
        for text in ["\u0100", "\u7fff", "\u8000", "a", "\u00ff", "\uaa11"]
            @test matches("^[$ranges]\$", text)
            @test !matches("^[^$ranges]\$", text)
        end
        @test !matches("^[$ranges]\$", "\uaa14")
        @test matches("^[^$ranges]\$", "\uaa14")
        @test startswith(E.translate("[$ranges]").main, "[^")
        @test matches("^\\P{sc=Cham}+\$", "a\u00ff\u0100\u7fff\u8000")
        @test matches("^[^\\d\\u0100\\u0200\\u0300\\u0400\\u0500]+\$", "a\u00ff\u0101\u7fff\u8000")
        # A pattern with many large classes fits in what PCRE2 holds, as each is written once.
        many = E.compile("^" * "\\p{L}\\P{L}"^40 * "\$")
        @test E.ismatch(many, "a1"^40)
        @test !E.ismatch(many, "a1"^39 * "aa")
        @test count("(?<s", many.translation.main) == 2
    end

    # A lookbehind against its definition: it holds at a position when its body matches some stretch of the text
    # that ends there. The bodies are of fixed, bounded and unbounded length, with and without alternatives, and the
    # texts hold a character beyond the Basic Multilingual Plane.
    @testset "lookbehind agrees with its definition" begin
        pieces = ["a", "b", ".", "[^a]", "a*", "b+", ".?", "(?:ab|a)", "(?:a|bc){2}", "(?:ab|b)+", "\\b", "^",
            "(?:^|b)", GRINNING_FACE, ".{1,2}", "[^" * GRINNING_FACE * "]", "(?=b)", "(?<!a)", "\\n", "(?:a|)",
            "(?<=b+)", "a|bb"]
        alphabet = ['a', 'b', '\n', '\U1F600', ' ']
        random = Xorshift(0x2545f491)
        failures = String[]
        callouts = 0
        direct = 0
        for round in 1:1500
            body = join(pieces[nextbelow!(random, length(pieces)) + 1] for _ in 1:(1 + nextbelow!(random, 3)))
            negative = nextbelow!(random, 2) == 1
            pattern = (negative ? "(?<!" : "(?<=") * body * ")c"
            translated = E.compile(pattern)
            isempty(translated.translation.lookbehinds) ? (direct += 1) : (callouts += 1)
            for t in 1:12
                text = Char[alphabet[nextbelow!(random, 5) + 1] for _ in 1:nextbelow!(random, 6)]
                push!(text, 'c')
                nextbelow!(random, 2) == 1 && append!(text, ['a', 'c'])
                s = String(text)
                n = length(text)
                expected = false
                for i in findall(==('c'), text)
                    # Whether the body matches some stretch of the text from j characters in to the i-th one.
                    behind = any(0:(i - 1)) do j
                        whole = "^[\\s\\S]{$j}(?:$body)(?=[\\s\\S]{$(n - i + 1)}\$)"
                        E.ismatch(E.compile(whole), s)
                    end
                    if behind != negative
                        expected = true
                        break
                    end
                end
                if E.ismatch(translated, s) != expected
                    push!(failures, "$pattern on $(repr(s)): expected $expected")
                end
            end
        end
        isempty(failures) || println(join(failures[1:min(end, 20)], "\n"))
        @test isempty(failures)
        @test callouts > 300
        @test direct > 300
    end

    # A pattern nested too deeply to read is refused and is not an error of the stack.
    @testset "deep nesting" begin
        deep = "("^100_000 * ")"^100_000
        @test !isvalid262(deep)
        @test isnotapattern(deep)
        shallow = "(?:"^150 * "a" * ")"^150
        @test validandcompiles(shallow)
        @test matches(shallow, "a")
    end
end
