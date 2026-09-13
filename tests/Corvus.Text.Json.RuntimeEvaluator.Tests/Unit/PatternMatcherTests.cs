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
        "ab-c-d", "ab-cdefghi", "a-b", "abc-", "aB", "a.bC", "a.b.C", "A.b", "_$.a$", "a.B.c.D", "12µs", "µs", "12ms", "12 µs", "!", "!!a", "a!b", "?", "!?",
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
}