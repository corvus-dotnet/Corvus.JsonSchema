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
    ];

    private static readonly string[] Inputs =
    [
        string.Empty, "a", "ab", "abc", "abcd", "abcde", "A", "Z9", "a1_b", "1abc", "_abc", "a.b", "a-b", "a|b", "a@b", "a#b",
        "hello world", "x-vendor", "x", "-x", "schemas", "responses", "Schemas", "schema", "on", "off", "onoff",
        "200", "2XX", "6XX", "20", "2024-01-31", "2024-1-31", "12:34", "12-34", "1234", "ab.cd", "ab.", ".cd",
        "ABCDEF0123", "abcdef", "G", "café", "naïve.x", "日本語", "aé", "é", "\t", " ", "a b",
        "abcdefghijklmnopqrstuvwxyz0123", "abcdefghijklmnopqrstuvwxyz01234", "F1", "ff", "~", "a~b",
        "ES2022", "es2022", "ES2022.Array", "es2022.array", "ES2022.error", "ES2022.Symbol.Well", "ES2022.SymbolXWell", "ES2022.", "ES2022.arr", "ES2023", "ES2022.arrayx",
        "WEB", "web.imp", "web.", "webimp", "es5", "es6", "esnext", "es7", "es", "abc-def", "abc-", "-def", "abc-def-ghi", "v1", "v1.2", "v1.", "v1.2.3", "v", "1.2",
        "abc", "a.c", "a\nc", "a\rc", "a\u2028c", "aéc", "ac", "x1", "y2", "xy1", "x12", "z1",
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
                Assert.AreEqual(expected, actual, $"pattern {pattern} input \"{input}\"");
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
        Assert.IsTrue(PatternMatcher.Create("^[a-z_]+\\.[a-z_]+$", options).UsesRegex, "A variable atom before another needs backtracking.");
        Assert.IsTrue(PatternMatcher.Create("^[^\\W\\.\\-][\\w\\.\\-]*$", options).UsesRegex, "Negated classes cover non-ASCII characters.");
        Assert.IsTrue(PatternMatcher.Create("[a-z]+", options).UsesRegex, "Unanchored patterns search.");
        Assert.IsFalse(PatternMatcher.Create("^[Ee][Ss]2022(\\.([Aa][Rr][Rr][Aa][Yy]|[Ee][Rr][Rr][Oo][Rr]))?$", options).UsesRegex, "A trailing optional group of alternatives is matched against the remainder.");
        Assert.IsFalse(PatternMatcher.Create("^es(5|6|next)$", options).UsesRegex);
        Assert.IsFalse(PatternMatcher.Create("^a.c$", options).UsesRegex, "A dot is a one-rune class.");
        Assert.IsFalse(PatternMatcher.Create("^(x|y)(1|2)$", options).UsesRegex, "Fixed-width groups before the last need no backtracking.");
        Assert.IsTrue(PatternMatcher.Create("^[Ee][Ss]5|[Ee][Ss]6|[Ee][Ss]7$", options).UsesRegex, "A top-level alternation anchors each alternative differently.");
        Assert.IsFalse(PatternMatcher.Create("^(a|ab)c$", options).UsesRegex, "Groups are flattened into whole alternatives, so no backtracking is needed.");
        Assert.IsTrue(PatternMatcher.Create("^(a|ab)+c$", options).UsesRegex, "A quantified group needs backtracking.");
        Assert.IsTrue(PatternMatcher.Create("^[a-z]+(-[a-z]+)?$", options).UsesRegex, "A variable atom before another needs backtracking.");
    }
}