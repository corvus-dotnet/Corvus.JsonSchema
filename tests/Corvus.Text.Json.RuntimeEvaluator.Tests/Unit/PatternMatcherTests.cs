// <copyright file="PatternMatcherTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.RegularExpressions;
using Corvus.Text.Json.RuntimeEvaluator;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// The regex-free matchers (prefix, range, literal alternation, class sequence) agree with the regular expression
/// they replace on every input, including the edge cases that distinguish them: empty strings, non-ASCII text,
/// escaped punctuation and quantifier bounds.
/// </summary>
[TestClass]
public class PatternMatcherTests
{
    private static readonly string[] Patterns =
    [
        "^[a-zA-Z0-9_\\.\\-\\|@#]*$",
        "^[A-Z0-9_\\-\\/]+$",
        "^[a-z][a-z0-9_]+$",
        "^[a-z][a-z0-9]{0,29}$",
        "^[\\w\\*]{0,6}$",
        "^[A-F0-9]{1,32}$",
        "^\\d{4}-\\d{2}-\\d{2}$",
        "^[a-z]{1,2}$",
        "^[a-zA-Z0-9_.:-]+$",
        "^x-",
        "^(schemas|responses|parameters|examples)$",
        "^(?:on|off)$",
        "^[1-5](?:[0-9]{2}|XX)$",
        "^\\d+[:-]\\d+$",
        "^[a-z_]+\\.[a-z_]+$",
        "^.{2,4}$",
        "^[^\\W\\.\\-][\\w\\.\\-]*$",
        "^[Ee][Ss]2022(\\.([Aa][Rr][Rr][Aa][Yy]|[Ee][Rr][Rr][Oo][Rr]|[Ss][Yy][Mm][Bb][Oo][Ll].[Ww][Ee][Ll][Ll]))?$",
        "^[Ww][Ee][Bb](\\.[Ii][Mm][Pp])?$",
        "^es(5|6|next)$",
        "^[a-z]+(-[a-z]+)?$",
        "^v(\\d+)(\\.\\d+)?$",
        "^a.c$",
        "^(x|y)(1|2)$",
        "^(a|ab)c$",
        "^[@$_#]",
        "^x-[0-9]",
        "^[a-z]+_",
        "^\\d{2}.*",
        "^(?=[^!*,;{}[\\]~\\n]+$)(?=(.*\\w)).+$",
        "^(?=!+[^!*,;{}[\\]~\\n]+$)(?=(.*\\w)).+$",
        "^ES5|ES6|ES7$",
        "^ES5|ES6",
        "ES6|ES7$",
        "es5|es6",
        "^[^\\W\\.\\-][\\w\\.\\-]*$",
        "^\\{\\{[^\\W\\.\\-][\\w\\.\\-]*\\}\\}$",
        "^\\{\\{[\\w][\\w\\.\\-]*\\}\\}$",
        "^[^:]+:[^:]+$",
        "^[^- @#$%^&()!]+$",
        "^[a-z_]+\\.[a-z_]+$",
        "^\\d+[:-]\\d+$",
        "^\\/[^\\*\\?\\&\\%]*(\\/\\*)?$",
        "^[0-9]+(ns|ms|us|µs|s|m|h)$",
        "default|^[0-9]+$",
        "(base64key|awskms|azurekeyvault|gcpkms|hashivault)://(.*)",
        "^.*\\.(?:txt|trie)(?:\\.gz)?$",
        "^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}(?:\\.\\d+)?Z$",
        "^([t|T][o|O][p|P])|([c|C][e|E][n|N][t|T][e|E][r|R])|([b|B][o|O][t|T][t|T][o|O][m|M])$",
        "^([a-zA-Z][a-zA-Z0-9_]{0,39})(\\.[a-zA-Z][a-zA-Z0-9_]{0,39})*$",
        "^([a-zA-Z_$][a-zA-Z0-9_$]{0,39}\\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,39})$",
        "^([a-zA-Z0-9]{2,3})(-[a-zA-Z0-9]{1,6})*$",
        "^([-\\w_\\s]+)(,[-\\w_\\s]+)*$",
        "^(![-\\w_\\s]+)(,![-\\w_\\s]+)*$",
        "^(!?[-\\w_\\s]+)|(\\*)$",
        "^[a-zA-Z0-9_\\.\\-]+[\\|]?[a-zA-Z0-9_\\.\\-]+$",
        "^3\\.1\\.\\d+(-.+)?$",
        "\\{.*\\}",
        "(^([0-9]+)\\.([0-9]+)$)|(^\\{[A-F0-9]{8}(-[A-F0-9]{4}){3}-[A-F0-9]{12}\\}$)",
        "^(User[.]env|UserDefault(.extended)?)[.][^.]+$",
        "^[0-9]{1,}.[0-9]{1,}.[0-9]{1,}$",
        "^[A-Za-z]{2,}.[A-Za-z]{2,}",
        "^(\\\\[0-9a-zA-Z_]+\\\\)?[a-zA-Z][a-zA-Z0-9_]*$",
        "^((\\.(?!\\.)\\/)?\\w+\\/?)+$",
        "[a-z]+",
        "^$",
        "^",
        "a\\sb",
        "^\\S+$",
        "^[\\s\\S]*$",
        "^[^\\s]+$",
        "^\\D*$",
        ".*",
        "(.*)",
        "^.*$",
        "^(.*)$",
        ".+",
        "(.+)",
        ".",
        "^.+$",
        "^(.+)$",
        "[\\s\\S]*",
    ];

    private static readonly string[] Inputs =
    [
        string.Empty, "ES5", "ES6", "ES7", "ES5x", "xES5", "xES6x", "xES7", "ES7x", "es5", "ES6|ES7", "a", "ab", "abc", "abcd", "abcde", "A", "Z9", "a1_b", "1abc", "_abc", "a.b", "a-b", "a|b", "a@b", "a#b",
        "hello world", "x-vendor", "x", "-x", "schemas", "responses", "Schemas", "schema", "on", "off", "onoff",
        "200", "2XX", "6XX", "20", "2024-01-31", "2024-1-31", "12:34", "12-34", "1234", "ab.cd", "ab.", ".cd",
        "ABCDEF0123", "abcdef", "G", "café", "naïve.x", "日本語", "aé", "é", "\t", " ", "a b",
        "abcdefghijklmnopqrstuvwxyz0123", "abcdefghijklmnopqrstuvwxyz01234", "F1", "ff", "~", "a~b",
        "ES2022", "es2022", "ES2022.Array", "es2022.array", "ES2022.error", "ES2022.Symbol.Well", "ES2022.SymbolXWell", "ES2022.", "ES2022.arr", "ES2023", "ES2022.arrayx",
        "WEB", "web.imp", "web.", "webimp", "es5", "es6", "esnext", "es7", "es", "abc-def", "abc-", "-def", "abc-def-ghi", "v1", "v1.2", "v1.", "v1.2.3", "v", "1.2",
        "abc", "a.c", "a\nc", "a\rc", "a\u2028c", "aéc", "ac", "x1", "y2", "xy1", "x12", "z1",
        "@x", "$", "#tag", "_", "x@", "x-1", "x-12", "x-", "x-a", "ab_", "ab_c", "_a", "a_b_", "12", "123x", "1x",
        "word", "!word", "!!word", "!", "!!", "wo,rd", "!wo*rd", "---", "!---", "a b", "!a b", "wörd", "ö", "!ö", "a\nb", "!a\nb", "{}", "a]b",
        "top", "TOP", "tOp", "xtopx", "center", "bottom", "topcenter", "t|p", "a.b.c", "a.b.", ".a", "a1.b2_c", "a..b", "$a.b$", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "{{name}}", "{{na-me}}", "{{}}", "{{-x}}", "{{ a }}", "{{a}}}", "{{é}}", "1:2", "a:b", "a:b:c", ":", "a:", ":b", "é:é", "10ns", "10", "ns", "10µs", "10 s", "10s ",
        "/api/*", "/api", "/", "/a*b", "/a?b", "/a/*/", "/*", "default", "xdefaultx", "123", "12a", "awskms://key", "xawskms://", "base64key:/", "awskms:/\n/",
        "a,b", "a,b,c", ",a", "a,", "!a,!b", "!a,b", "a b,c d", "a*", "*", "!*", "a,*", "file.txt", "file.trie.gz", "file.txt.gz", "file.gz", "txt", ".txt", "a\n.txt", "a\u2028.txt",
        "2024-01-31T12:34:56Z", "2024-01-31T12:34:56.789Z", "2024-01-31T12:34:56.Z", "2024-01-31T12:34:56", "ab-cd-ef", "abc-defghi", "abcd-e", "a-", "abc-defghij",
        "3.1.0", "3.1.0-beta", "3.1", "3.2.0", "3.1.0-", "{a}", "{", "}", "a{b}c", "{\n}", "{a\nb}", "1.2", "{ABCDEF01-1234-ABCD-EF01-123456789ABC}", "{abcdef01-1234-abcd-ef01-123456789abc}", "1.2.3",
        "a\u00a0b", "a\u3000b", "\u00a0", "\ufeff", "\u2028", "a\u1680b", "User.env.x", "UserDefault.x", "UserDefault.extended.x", "User.env", "UserXenv.x", "\\abc\\d", "\\a\\", "$a", "a$", "abc.de", "ab.c.d",
        "a/b", "./a/b", "a//b", "a/", "../a", "a|b|c", "a|", "|a", "ab|cd", "ab||cd", "1234567", "12", "a\tb", "a\vb", "\v", "\f", "x\u0085y", "é.é", "日本.語",
        "\n", "\r", "\r\n", "\n\n", "\u2029", "\u2028\u2029", "\na", "a\r",
        "ab-c-d", "ab-cdefghi", "a-b", "abc-", "aB", "a.bC", "a.b.C", "A.b", "_$.a$", "a.B.c.D", "12µs", "µs", "12ms", "12 µs", "!", "!!a", "a!b", "?", "!?",
    ];

    /// <summary>
    /// Patterns with an anchor, a word boundary or a lookaround in a place where a matcher for a shape could lose it,
    /// and patterns with an escaped <c>$</c> or <c>^</c>, which is a literal character. Each has the texts it
    /// matches and the texts it does not. The answers are those of V8: <c>new RegExp(pattern, "u").test(text)</c>.
    /// </summary>
    private static readonly (string Pattern, string[] Matches, string[] Fails)[] AnchorCases =
    [
        ("^x(,a$)*$", ["x", "x,a"], ["x,a,a", "x,a,a,a", "", "x,", "x,b"]),
        ("^x(,a$)+$", ["x,a"], ["x", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
        ("^x(?:,a$)*$", ["x", "x,a"], ["x,a,a", "x,a,a,a", "", "x,", "x,b"]),
        ("^x$(,a)*$", ["x"], ["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
        ("^(x$)(,a)*$", ["x"], ["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
        ("^(?:x$)(,a)+$", [], ["x", "x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
        ("^$(,a)*$", [""], [",a", ",a,a", "a"]),
        ("^(a,$)*b$", ["b"], ["a,b", "a,a,b", "a,", "", "a,a"]),
        ("^(a,$)+b$", [], ["b", "a,b", "a,a,b", "a,", "", "a,a"]),
        ("^(?:a,$)*b$", ["b"], ["a,b", "a,a,b", "a,", "", "a,a"]),
        ("^([a-z]+)(\\.[a-z]+$)*$", ["a", "a.b"], ["a.b.c", "a.", ""]),
        ("^([a-z]+\\.$)*[a-z]+$", ["a"], ["a.b", "a.b.c", ".a", ""]),
        ("^x(,a$$)*$", ["x", "x,a"], ["x,a,a", "x,a,a,a", "", "x,", "x,b"]),
        ("^x(^,a)*$", ["x"], ["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
        ("^x(,^a)*$", ["x"], ["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
        ("^^x(,a)*$", ["x", "x,a", "x,a,a", "x,a,a,a"], ["", "x,", "x,b"]),
        ("^(^x)(,a)*$", ["x", "x,a", "x,a,a", "x,a,a,a"], ["", "x,", "x,b"]),
        ("^(^a,)*b$", ["b", "a,b"], ["a,a,b", "a,", "", "a,a"]),
        ("^(a,)*^b$", ["b"], ["a,b", "a,a,b", "a,", "", "a,a"]),
        ("^x(,(a$))*$", ["x", "x,a"], ["x,a,a", "x,a,a,a", "", "x,", "x,b"]),
        ("^x(,(?:a$))*$", ["x", "x,a"], ["x,a,a", "x,a,a,a", "", "x,", "x,b"]),
        ("^x(,a|$)*$", ["x", "x,a", "x,a,a", "x,a,a,a"], ["", "x,", "x,b"]),
        ("^x(,(a|$))*$", ["x", "x,a", "x,a,a", "x,a,a,a", "x,"], ["", "x,b"]),
        ("^x((^|,)a)*$", ["x", "x,a", "x,a,a", "x,a,a,a"], ["", "x,", "x,b"]),
        ("^(a,)*(b$)$", ["b", "a,b", "a,a,b"], ["a,", "", "a,a"]),
        ("^(a,)*b$$", ["b", "a,b", "a,a,b"], ["a,", "", "a,a"]),
        ("^x(,a\\b)*$", ["x", "x,a", "x,a,a", "x,a,a,a"], ["", "x,", "x,b", "x,ab"]),
        ("^x(\\b,a)*$", ["x", "x,a", "x,a,a", "x,a,a,a"], ["", "x,", "x,b"]),
        ("^x(,\\Ba)*$", ["x"], ["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
        ("^x(\\B,a)*$", ["x"], ["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
        ("^x\\b(,a)*$", ["x", "x,a", "x,a,a", "x,a,a,a"], ["", "x,", "x,b"]),
        ("^x\\B(,a)*$", [], ["x", "x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
        ("^(a\\b,)*b$", ["b", "a,b", "a,a,b"], ["a,", "", "a,a"]),
        ("^(a,\\B)*b$", ["b"], ["a,b", "a,a,b", "a,", "", "a,a"]),
        ("^x(,a(?=,|$))*$", ["x", "x,a", "x,a,a", "x,a,a,a"], ["", "x,", "x,b"]),
        ("^x(,(?!a)a)*$", ["x"], ["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
        ("^x(?=,)(,a)*$", ["x,a", "x,a,a", "x,a,a,a"], ["x", "", "x,", "x,b"]),
        ("^x(,a(?<=a))*$", ["x", "x,a", "x,a,a", "x,a,a,a"], ["", "x,", "x,b"]),
        ("^x(,(?<!,)a)*$", ["x"], ["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
        ("^x(?=,a$)(,a)*$", ["x,a"], ["x", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
        ("^(a,(?=b))*b$", ["b", "a,b"], ["a,a,b", "a,", "", "a,a"]),
        ("^(?!a,a)(a,)*b$", ["b", "a,b"], ["a,a,b", "a,", "", "a,a"]),
        ("^x(,a\\$)*$", ["x", "x,a$", "x,a$,a$"], ["x,a", "x,a,a", "x,a$,a"]),
        ("^x\\$(,a)*$", ["x$", "x$,a", "x$,a,a"], ["x", "x,a"]),
        ("^(a\\$,)*b$", ["b", "a$,b", "a$,a$,b"], ["a,b", "a$,a,b"]),
        ("^x(,\\^a)*$", ["x", "x,^a", "x,^a,^a"], ["x,a", "x,a,a"]),
        ("^(a,)*b\\$$", ["b$", "a,b$", "a,a,b$"], ["b", "a,b"]),
        ("^x(,[$^]a)*$", ["x", "x,$a", "x,^a,$a"], ["x,a", "x,a,a"]),
        ("^a$b", [], ["a", "ab", "a$b", ""]),
        ("^a^b", [], ["a", "ab", "b"]),
        ("^a$$", ["a"], ["ab", ""]),
        ("^^a", ["a", "ab"], ["ba"]),
        ("^a\\$b$", ["a$b"], ["ab", "a"]),
        ("^a\\b", ["a", "a b", "a,"], ["ab"]),
        ("^a\\Bb", ["ab"], ["a b", "a"]),
        ("^\\ba", ["a"], ["ba", ""]),
        ("^a(?=b)", ["ab"], ["a", "ac"]),
        ("^a(?!b)", ["a", "ac"], ["ab"]),
        ("^[a-c]+\\b$", ["a", "abc"], [""]),
        ("^[a-c]+$\\b", ["a", "abc"], [""]),
        ("^$", [""], ["a"]),
        ("^a$.*", ["a"], ["ab", ""]),
        ("^a\\b.*", ["a", "a b"], ["ab"]),
        ("^a^.*", [], ["a", "ab"]),
        ("^\\$.*", ["$", "$a"], ["a"]),
        ("^a$|b", ["a", "ab", "b", "cb"], ["c"]),
        ("a$|^b", ["a", "ba", "bc"], ["ab", "cb"]),
        ("^(a$|b)$", ["a", "b"], ["ab", ""]),
        ("^(a|^b)$", ["a", "b"], ["ab"]),
        ("^(a|b\\b)$", ["a", "b"], ["ab"]),
        ("^(?:a\\$|b)$", ["a$", "b"], ["a"]),
        ("^(a|$)$", ["a", ""], ["b"]),
        ("^(^|b)$", ["", "b"], ["a"]),
        ("^(a$|b)c", ["bc", "bcd"], ["a", "ac"]),
        ("(^a|b)c$", ["ac", "bc", "xbc"], ["xac", "acx"]),
        ("a(b|^c)", ["ab", "xab"], ["ac", "c"]),
        ("(a$){2}", [], ["a", "aa", ""]),
        ("(^a){2}", [], ["a", "aa"]),
        ("x(a$)?b", ["xb"], ["xab", "xa"]),
        ("^x(a$)?", ["x", "xa", "xab", "xb"], []),
        ("(a|b$)c", ["ac"], ["bc", "b"]),
        ("a|\\bb", ["a", "b", "c b"], ["cb"]),
        ("a\\b|c", ["a", "c", "b a"], ["ab"]),
        ("(?=a)a|b", ["a", "b"], ["c"]),
        ("(?<=a)b|c", ["ab", "c"], ["b"]),
        ("a^|b", ["b"], ["a", ""]),
        ("$a|b", ["b"], ["a", ""]),
        ("^|a", ["", "b"], []),
        ("a|$", ["", "b"], []),
        ("(a|b)$c", [], ["ac", "a", "bc"]),
        ("^(a|b)^c", [], ["ac", "c"]),
        ("[a-c]{2}$|^x", ["ab", "x", "yab"], ["abx", "yx"]),
        ("[a-c]\\b|x", ["a", "x", "d a"], ["ad"]),
        ("^[a-c]+$|^\\d$", ["abc", "1"], ["a1", ""]),
        ("^(a|b){2}$", ["ab"], ["a", "abc"]),
        ("^.{1,3}$$", ["a"], ["abcd", ""]),
        ("^.$^", [], ["a", ""]),
        ("^(.*)$", ["", "a"], []),
        ("^es$|ms", ["es", "xms"], ["esx", "x"]),
        ("^x-$", ["x-"], ["x-a"]),
        ("^x-\\b", ["x-a"], ["x-", "x--"]),
    ];

    /// <summary>
    /// Separated lists whose final item can hold the separator's first character, so that the text of the final
    /// item can also be read as an item and a separator. The answers are those of V8.
    /// </summary>
    private static readonly (string Pattern, string[] Matches, string[] Fails)[] AmbiguousFinalItemCases =
    [
        ("^(a*,)+,$", ["a,,", ",,", "a,a,,", "a,,,"], ["a,", ",", ""]),
        ("^(?:a?\\^)*.$", ["^", "a^^", "^^", "a", "a^b"], ["a^", "", "^a^"]),
        ("^(?:a.)+a{2}$", ["aaaaaa", "axaa", "aaaa", "axaxaa"], ["aaa", "aa", "axa"]),
        ("^([a-z]+\\.)*.$", ["a.b", "a..", "b", ".", "a.b.c"], ["a.", "a.bc"]),
        ("^(\\d+-)+-$", ["1--", "1-2--"], ["1-", "--", "12-3-"]),
    ];

    /// <summary>
    /// Alternatives inside a group that encloses a whole alternative, with an anchor of their own. The anchor binds
    /// to its alternative alone. The answers are those of V8.
    /// </summary>
    private static readonly (string Pattern, string[] Matches, string[] Fails)[] AnchoredAlternativeInAGroupCases =
    [
        ("(b|[a-c]{1,2}$)", ["b.", "b", "ab", "ab.", "xb."], ["."]),
        ("(^a|b)", ["a", "xb", "b"], ["xa", "c"]),
        ("(a|b$)", ["a", "ax", "b", "xb"], ["bx", "c"]),
        ("(?:^a|b)", ["ab", "xb"], ["xa", "c"]),
        ("((a|b$))", ["ax", "xb"], ["bx"]),
        ("(^a|b)|c$", ["a", "xb", "c", "xc"], ["xa", "cx"]),
        ("x|(a|b$)", ["x", "ax", "bx", "b"], ["c"]),
        ("(^[a-c]+|\\d{2}$)", ["abx", "x12"], ["xab", "12x", "1"]),
        ("^(a|b$)", ["a", "ax", "b"], ["bx", "xa"]),
        ("(^a|b)$", ["a", "b", "xb"], ["ax", "bx"]),
    ];

    /// <summary>
    /// Texts with a line terminator at an end, against patterns of every kind the evaluator matches by itself and
    /// some it leaves to the regular expression. In ECMA-262 without the multiline flag <c>$</c> holds only at the end
    /// of the text, where the <c>$</c> of a .NET regular expression also holds before a final line feed, and
    /// <c>.</c> matches none of the four line terminators. The answers are those of V8:
    /// <c>new RegExp(pattern, "u").test(text)</c>.
    /// </summary>
    private static readonly (string Pattern, string[] Matches, string[] Fails)[] LineTerminatorCases =
    [
        ("^abc$", ["abc"], ["abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a", "a\n", "de", "de\n", "a,a", "a,a\n", "12", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
        ("abc$", ["abc", "\nabc"], ["abc\n", "abc\r", "abc\u2028", "abc\u2029", "abc\n\n", "abc\r\n", "a", "a\n", "de", "de\n", "a,a", "a,a\n", "12", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
        ("^abc", ["abc", "abc\n", "abc\r", "abc\u2028", "abc\u2029", "abc\n\n", "abc\r\n"], ["\nabc", "a", "a\n", "de", "de\n", "a,a", "a,a\n", "12", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
        ("^[a-z]+$", ["abc", "a", "de"], ["abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a\n", "de\n", "a,a", "a,a\n", "12", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
        ("^.{1,3}$", ["abc", "a", "de", "a,a", "12", " "], ["abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a\n", "de\n", "a,a\n", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " \n"]),
        ("^.*$", ["abc", "a", "de", "a,a", "12", "", " "], ["abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a\n", "de\n", "a,a\n", "12\n", "x-\n", "\n", "c\n", "\nd", "a\nc", "ab\n", " \n"]),
        ("^.+$", ["abc", "a", "de", "a,a", "12", " "], ["abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a\n", "de\n", "a,a\n", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " \n"]),
        ("^(abc|de)$", ["abc", "de"], ["abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a", "a\n", "de\n", "a,a", "a,a\n", "12", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
        ("^a(,a)*$", ["a", "a,a"], ["abc", "abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a\n", "de", "de\n", "a,a\n", "12", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
        ("^(a,)*a$", ["a", "a,a"], ["abc", "abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a\n", "de", "de\n", "a,a\n", "12", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
        ("^\\w+$", ["abc", "a", "de", "12"], ["abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a\n", "de\n", "a,a", "a,a\n", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
        ("^[^x]+$", ["abc", "abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a", "a\n", "de", "de\n", "a,a", "a,a\n", "12", "12\n", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"], ["x-\n", ""]),
        ("^a.*$", ["abc", "a", "a,a"], ["abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a\n", "de", "de\n", "a,a\n", "12", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
        ("^\\d{2}$", ["12"], ["abc", "abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a", "a\n", "de", "de\n", "a,a", "a,a\n", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
        ("^x-", ["x-\n"], ["abc", "abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a", "a\n", "de", "de\n", "a,a", "a,a\n", "12", "12\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
        ("^abc\\n$", ["abc\n"], ["abc", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a", "a\n", "de", "de\n", "a,a", "a,a\n", "12", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
        ("^abc\n", ["abc\n", "abc\n\n"], ["abc", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\r\n", "a", "a\n", "de", "de\n", "a,a", "a,a\n", "12", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
        ("^abc\n$", ["abc\n"], ["abc", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a", "a\n", "de", "de\n", "a,a", "a,a\n", "12", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
        ("ab|c$|^d", ["abc", "abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "de", "de\n", "a\nc", "ab\n"], ["a", "a\n", "a,a", "a,a\n", "12", "12\n", "x-\n", "", "\n", "c\n", "\nd", " ", " \n"]),
        ("^a.c$", ["abc"], ["abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a", "a\n", "de", "de\n", "a,a", "a,a\n", "12", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
        ("$", ["abc", "abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a", "a\n", "de", "de\n", "a,a", "a,a\n", "12", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"], []),
        ("^$", [""], ["abc", "abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a", "a\n", "de", "de\n", "a,a", "a,a\n", "12", "12\n", "x-\n", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
        ("^\\s$", ["\n", " "], ["abc", "abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a", "a\n", "de", "de\n", "a,a", "a,a\n", "12", "12\n", "x-\n", "", "c\n", "\nd", "a\nc", "ab\n", " \n"]),
        ("^[a-c]{3}$", ["abc"], ["abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a", "a\n", "de", "de\n", "a,a", "a,a\n", "12", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
        ("^\\S+$", ["abc", "a", "de", "a,a", "12"], ["abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a\n", "de\n", "a,a\n", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
        ("^a.+$", ["abc", "a,a"], ["abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a", "a\n", "de", "de\n", "a,a\n", "12", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
        (".$", ["abc", "\nabc", "a", "de", "a,a", "12", "\nd", "a\nc", " "], ["abc\n", "abc\r", "abc\u2028", "abc\u2029", "abc\n\n", "abc\r\n", "a\n", "de\n", "a,a\n", "12\n", "x-\n", "", "\n", "c\n", "ab\n", " \n"]),
        ("^ab?c*$", ["abc", "a"], ["abc\n", "abc\r", "abc\u2028", "abc\u2029", "\nabc", "abc\n\n", "abc\r\n", "a\n", "de", "de\n", "a,a", "a,a\n", "12", "12\n", "x-\n", "", "\n", "c\n", "\nd", "a\nc", "ab\n", " ", " \n"]),
    ];

    [TestMethod]
    public void MatchersAgreeWithTheRegularExpression()
    {
        var options = new JsonSchemaEvaluatorOptions { CompileRegularExpressions = false };
        int checkedCount = 0;
        foreach (string pattern in Patterns)
        {
            PatternMatcher matcher = PatternMatcher.Create(pattern, options);
            // The reference is the .NET form the evaluator itself would construct: ECMA-262 classes are ASCII (\w is
            // [a-zA-Z0-9_]), which the translator makes explicit.
            var regex = new Regex(JsonSchemaEvaluator.ToDotNetPattern(pattern), RegexOptions.CultureInvariant);
            foreach (string input in Inputs)
            {
                bool expected = regex.IsMatch(input);
                bool actual = matcher.IsMatch(Encoding.UTF8.GetBytes(input));
                Assert.AreEqual(expected, actual, $"pattern {pattern} translated {regex} input \"{input}\" bytes {BitConverter.ToString(Encoding.UTF8.GetBytes(input))}");
                checkedCount++;
            }
        }

        Assert.IsTrue(checkedCount > 500);
    }

    [TestMethod]
    public void OnlyPatternsOutsideTheSubsetNeedARegularExpression()
    {
        var options = new JsonSchemaEvaluatorOptions { CompileRegularExpressions = false };
        Assert.IsFalse(PatternMatcher.Create("^[a-zA-Z0-9_\\.\\-\\|@#]*$", options).UsesRegex);
        Assert.IsFalse(PatternMatcher.Create("^\\d{4}-\\d{2}-\\d{2}$", options).UsesRegex);
        Assert.IsFalse(PatternMatcher.Create("^(schemas|responses)$", options).UsesRegex);
        Assert.IsFalse(PatternMatcher.Create("^x-", options).UsesRegex);
        Assert.IsFalse(PatternMatcher.Create("^[1-5](?:[0-9]{2}|XX)$", options).UsesRegex, "A group of alternatives is flattened into whole alternatives.");
        Assert.IsFalse(PatternMatcher.Create("^[a-z_]+\\.[a-z_]+$", options).UsesRegex, "A variable atom followed by a disjoint fixed one is consumed greedily.");
        Assert.IsTrue(PatternMatcher.Create("^[a-z]+[a-z0-9]+$", options).UsesRegex, "Two adjacent variable atoms need backtracking.");
        Assert.IsFalse(PatternMatcher.Create("^[^\\W\\.\\-][\\w\\.\\-]*$", options).UsesRegex, "Negated classes admit non-ASCII characters wholesale.");
        Assert.IsFalse(PatternMatcher.Create("[a-z]+", options).UsesRegex, "Unanchored patterns search from each character.");
        Assert.IsFalse(PatternMatcher.Create("^([a-zA-Z][a-zA-Z0-9_]{0,39})(\\.[a-zA-Z][a-zA-Z0-9_]{0,39})*$", options).UsesRegex, "A separated list.");
        Assert.IsFalse(PatternMatcher.Create("^(0|[1-9]\\d*)\\.(0|[1-9]\\d*)$", options).UsesRegex, "Groups of alternatives flatten, and each flattened sequence is deterministic.");
        Assert.IsFalse(PatternMatcher.Create("^[0-9]+(ns|ms|us|µs|s|m|h)$", options).UsesRegex, "A literal outside ASCII is one character.");
        Assert.IsFalse(PatternMatcher.Create("(^([0-9]+)\\.([0-9]+)$)|(^\\{[A-F0-9]{8}(-[A-F0-9]{4}){3}-[A-F0-9]{12}\\}$)", options).UsesRegex, "Grouped alternatives with their own anchors, and a small fixed group repeat.");
        Assert.IsFalse(PatternMatcher.Create("^([a-zA-Z0-9]{2,3})(-[a-zA-Z0-9]{1,6})*$", options).UsesRegex, "A separated list whose first item has different bounds.");
        Assert.IsFalse(PatternMatcher.Create("^(!?[-\\w_\\s]+)|(\\*)$", options).UsesRegex, "An optional atom before a disjoint class.");
        Assert.IsTrue(PatternMatcher.Create("^[a-zA-Z0-9_\\.\\-]+[\\|]?[a-zA-Z0-9_\\.\\-]+$", options).UsesRegex, "Two variable atoms of the same class need backtracking.");
        Assert.IsTrue(PatternMatcher.Create("^[0-9]{1,}.[0-9]{1,}.[0-9]{1,}$", options).UsesRegex, "A dot after a variable atom admits the atom's characters.");
        Assert.IsFalse(PatternMatcher.Create("^[Ee][Ss]2022(\\.([Aa][Rr][Rr][Aa][Yy]|[Ee][Rr][Rr][Oo][Rr]))?$", options).UsesRegex, "A trailing optional group of alternatives is matched against the remainder.");
        Assert.IsFalse(PatternMatcher.Create("^es(5|6|next)$", options).UsesRegex);
        Assert.IsFalse(PatternMatcher.Create("^a.c$", options).UsesRegex, "A dot is a one-rune class.");
        Assert.IsFalse(PatternMatcher.Create("^(x|y)(1|2)$", options).UsesRegex, "Fixed-width groups before the last need no backtracking.");
        Assert.IsFalse(PatternMatcher.Create("^[Ee][Ss]5|[Ee][Ss]6|[Ee][Ss]7$", options).UsesRegex, "A top-level alternation anchors each alternative where its own ^ and $ say.");
        Assert.IsFalse(PatternMatcher.Create("^(a|ab)c$", options).UsesRegex, "Groups are flattened into whole alternatives, so no backtracking is needed.");
        Assert.IsTrue(PatternMatcher.Create("^(a|ab)+c$", options).UsesRegex, "A quantified group needs backtracking.");
        Assert.IsFalse(PatternMatcher.Create("^[a-z]+(-[a-z]+)?$", options).UsesRegex, "A variable atom followed by a disjoint literal is consumed greedily.");
        Assert.IsFalse(PatternMatcher.Create("^[@$_#]", options).UsesRegex, "A start-anchored class is a prefix test.");
        Assert.IsFalse(PatternMatcher.Create("^x-[0-9]", options).UsesRegex);
        Assert.IsFalse(PatternMatcher.Create("^[a-z]+_", options).UsesRegex, "A prefix whose variable atom is followed by a disjoint literal.");
        Assert.IsFalse(PatternMatcher.Create("^\\d{2}.*", options).UsesRegex, "A trailing .* is redundant for a prefix.");
        Assert.IsFalse(PatternMatcher.Create("^(?=[^!*,;{}[\\]~\\n]+$)(?=(.*\\w)).+$", options).UsesRegex, "An excluded set plus a required word character is a scan.");
        Assert.IsFalse(PatternMatcher.Create("^(?=!+[^!*,;{}[\\]~\\n]+$)(?=(.*\\w)).+$", options).UsesRegex);
        Assert.IsTrue(PatternMatcher.Create("^(?=[^a]+$).+$", options).UsesRegex, "Only the exact two-lookahead form is recognised.");
    }

    /// <summary>
    /// A shape that cannot express an anchor or an assertion leaves the pattern to the regular expression. (The
    /// separated list of the Rust, Go, Java, Julia and Object Pascal ports dropped a <c>$</c> at the end of its
    /// repeated group and after its first item, so <c>^x(,a$)*$</c> took <c>x,a,a</c>. This evaluator's separated
    /// list refuses an item or a group with an anchor in it, and the table holds it to that.)
    /// </summary>
    [TestMethod]
    public void AnAnchorOrAnAssertionInsideAShapeIsNotDropped()
    {
        AssertCases(AnchorCases);
    }

    /// <summary>
    /// A final item that differs from the repeated one is matched over what is left once no separator follows an
    /// item. When the final item can hold the separator's first character, its own text can be read as an item and
    /// a separator: <c>^(a*,)+,$</c> matches <c>a,,</c>, and the list took both commas for separators. Such a
    /// pattern is not a separated list.
    /// </summary>
    [TestMethod]
    public void AFinalItemThatCanHoldTheSeparatorIsNotASeparatedList()
    {
        AssertCases(AmbiguousFinalItemCases);
        var options = new JsonSchemaEvaluatorOptions { CompileRegularExpressions = false };
        foreach ((string pattern, _, _) in AmbiguousFinalItemCases)
        {
            Assert.IsTrue(PatternMatcher.Create(pattern, options).UsesRegex, pattern);
        }

        Assert.IsFalse(PatternMatcher.Create("^([a-zA-Z_$][a-zA-Z0-9_$]{0,39}\\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,39})$", options).UsesRegex, "A final item without the separator's character is still a separated list.");
        Assert.IsFalse(PatternMatcher.Create("^([a-z]+,)*[a-z]{2}$", options).UsesRegex, "A final item with other bounds is still a separated list.");
    }

    /// <summary>
    /// A group that encloses a whole alternative is removed before the alternative's anchors are read. When the
    /// group holds alternatives, an anchor at either end of its text belongs to the first or the last of them, not
    /// to them all: <c>(a|b$)</c> is <c>a|b$</c>, and was matched as <c>(a|b)$</c>.
    /// </summary>
    [TestMethod]
    public void AnAnchorOfOneAlternativeInAGroupIsNotGivenToTheOthers()
    {
        AssertCases(AnchoredAlternativeInAGroupCases);
        var options = new JsonSchemaEvaluatorOptions { CompileRegularExpressions = false };
        Assert.IsFalse(PatternMatcher.Create("(a|b$)", options).UsesRegex, "The alternatives of the group are whole alternatives, each with its own anchors.");
        Assert.IsFalse(PatternMatcher.Create("(^[a-c]+|\\d{2}$)", options).UsesRegex);
    }

    [TestMethod]
    public void ASeparatedListWithAnEscapedAnchorIsStillMatchedWithoutARegularExpression()
    {
        var options = new JsonSchemaEvaluatorOptions { CompileRegularExpressions = false };
        Assert.IsFalse(PatternMatcher.Create("^[a-z]+(\\$[a-z]+)*$", options).UsesRegex, "An escaped anchor is a literal character, here the separator.");
        Assert.IsFalse(PatternMatcher.Create("^a\\$(,a\\$)*$", options).UsesRegex);
        Assert.IsFalse(PatternMatcher.Create("^(a\\$,)*a\\$$", options).UsesRegex);
        Assert.IsFalse(PatternMatcher.Create("^a(,[$^]a)*$", options).UsesRegex);
        Assert.IsTrue(PatternMatcher.Create("^a(,a$)*$", options).UsesRegex, "An anchor inside the group is not part of a list.");
        Assert.IsTrue(PatternMatcher.Create("^a$(,a)*$", options).UsesRegex, "An anchor after the first item is not part of a list.");
        Assert.IsTrue(PatternMatcher.Create("^(a,$)*a$", options).UsesRegex);
        Assert.IsTrue(PatternMatcher.Create("^a(,a\\b)*$", options).UsesRegex, "A word boundary is not part of a list.");
    }

    /// <summary>
    /// A <c>$</c> does not hold before a line feed that ends the text, as it does in a .NET regular expression:
    /// <c>^abc$</c> does not match "abc" followed by a line feed.
    /// </summary>
    [TestMethod]
    public void ADollarDoesNotHoldBeforeAFinalLineFeed()
    {
        AssertCases(LineTerminatorCases);
    }

    /// <summary>Each case through the matcher, and through an evaluator that interprets and one that generates code.</summary>
    /// <summary>
    /// Patterns a matcher for a shape read differently from ECMA-262, with V8's answers. In ECMA-262 a <c>]</c>
    /// closes a class wherever it stands, so <c>[]</c> holds nothing and <c>[^]</c> is every character, where the
    /// class sequence took a leading <c>]</c> for a member. An escaped digit in a class is not that digit:
    /// <c>[\0]</c> is U+0000 and <c>[\1]</c> is U+0001. And a pattern that is valid only without the <c>u</c> flag
    /// is matched by UTF-16 code unit, so a character beyond the Basic Multilingual Plane is two characters to its
    /// dot and to its negated class, where the matchers count code points.
    /// </summary>
    private static readonly (string Pattern, string[] Matches, string[] Fails)[] MisreadCases =
    [
        ("^[^][^a]", ["ab", "\u000Ab", "a\u000A", "\uD83D\uDE00b"], ["aa", "b", ""]),
        ("[]]", [], ["]", "a]", "[]]", ""]),
        ("^[^]]$", ["a]", "]]", "\u000A]"], ["a", "]", "", "\uD83D\uDE00]"]),
        ("^[\\0]$", ["\u0000"], ["0", ""]),
        ("^[\\1]$", ["\u0001"], ["1", ""]),
        ("^[^\\%]{1,3}$", ["abc", "\uD83D\uDE00", "\uD83D\uDE00a"], ["\uD83D\uDE00\uD83D\uDE00", "a%", "", "abcd"]),
        ("^.\\-$", ["a-"], ["\uD83D\uDE00-", "-", "a", "\u000A-"]),
    ];

    /// <summary>
    /// Patterns that are valid only without the <c>u</c> flag and name no character beyond the Basic Multilingual
    /// Plane, by a dot, a negated class or otherwise. Code units and code points are the same thing to them, and
    /// they keep their matchers.
    /// </summary>
    private static readonly (string Pattern, string[] Matches, string[] Fails)[] LegacyCasesThatKeepTheirMatcher =
    [
        ("^x\\-y$", ["x-y"], ["xy", "x-y "]),
        ("^[a-z\\-]+\\@$", ["a-b@", "a@"], ["@", "a-b"]),
    ];

    [TestMethod]
    public void APatternAMatcherWouldMisreadIsLeftToTheRegularExpression()
    {
        AssertCases(MisreadCases);
        var options = new JsonSchemaEvaluatorOptions { CompileRegularExpressions = false };
        foreach ((string pattern, _, _) in MisreadCases)
        {
            Assert.IsTrue(PatternMatcher.Create(pattern, options).UsesRegex, pattern);
        }
    }

    [TestMethod]
    public void APatternValidOnlyWithoutTheUFlagKeepsItsMatcherWhenCodeUnitsAndCodePointsAgree()
    {
        AssertCases(LegacyCasesThatKeepTheirMatcher);
        var options = new JsonSchemaEvaluatorOptions { CompileRegularExpressions = false };
        foreach ((string pattern, _, _) in LegacyCasesThatKeepTheirMatcher)
        {
            Assert.IsFalse(PatternMatcher.Create(pattern, options).UsesRegex, pattern);
        }
    }

    private static void AssertCases((string Pattern, string[] Matches, string[] Fails)[] cases)
    {
        var options = new JsonSchemaEvaluatorOptions { CompileRegularExpressions = false };
        var wrong = new List<string>();
        foreach ((string pattern, string[] matches, string[] fails) in cases)
        {
            PatternMatcher matcher = PatternMatcher.Create(pattern, options);
            string schema = "{\"pattern\": " + Quote(pattern) + "}";
            using JsonSchemaEvaluator interpreted = JsonSchemaEvaluator.Compile(schema, new JsonSchemaEvaluatorOptions { CodeGeneration = JsonSchemaCodeGeneration.Disabled });
            using JsonSchemaEvaluator generated = JsonSchemaEvaluator.Compile(schema, new JsonSchemaEvaluatorOptions { CodeGeneration = JsonSchemaCodeGeneration.Eager });
            foreach ((string[] texts, bool expected) in new[] { (matches, true), (fails, false) })
            {
                foreach (string text in texts)
                {
                    if (matcher.IsMatch(Encoding.UTF8.GetBytes(text)) != expected)
                    {
                        wrong.Add($"matcher: {pattern} on \"{text}\", want {expected}");
                    }

                    if (interpreted.Evaluate(Quote(text)) != expected)
                    {
                        wrong.Add($"interpreted: {pattern} on \"{text}\", want {expected}");
                    }

                    if (generated.Evaluate(Quote(text)) != expected)
                    {
                        wrong.Add($"generated: {pattern} on \"{text}\", want {expected}");
                    }
                }
            }
        }

        Assert.AreEqual(string.Empty, string.Join("\n", wrong));
    }

    /// <summary>The text as a JSON string.</summary>
    private static string Quote(string text)
    {
        return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\0", "\\u0000").Replace("\u0001", "\\u0001") + "\"";
    }
}