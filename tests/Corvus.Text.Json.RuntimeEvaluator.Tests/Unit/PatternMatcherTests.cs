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
    ];

    private static readonly string[] Inputs =
    [
        string.Empty, "a", "ab", "abc", "abcd", "abcde", "A", "Z9", "a1_b", "1abc", "_abc", "a.b", "a-b", "a|b", "a@b", "a#b",
        "hello world", "x-vendor", "x", "-x", "schemas", "responses", "Schemas", "schema", "on", "off", "onoff",
        "200", "2XX", "6XX", "20", "2024-01-31", "2024-1-31", "12:34", "12-34", "1234", "ab.cd", "ab.", ".cd",
        "ABCDEF0123", "abcdef", "G", "café", "naïve.x", "日本語", "aé", "é", "\t", " ", "a b",
        "abcdefghijklmnopqrstuvwxyz0123", "abcdefghijklmnopqrstuvwxyz01234", "F1", "ff", "~", "a~b",
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
        Assert.IsTrue(PatternMatcher.Create("^[1-5](?:[0-9]{2}|XX)$", options).UsesRegex, "Alternation inside a sequence needs backtracking.");
        Assert.IsTrue(PatternMatcher.Create("^[a-z_]+\\.[a-z_]+$", options).UsesRegex, "A variable atom before another needs backtracking.");
        Assert.IsTrue(PatternMatcher.Create("^[^\\W\\.\\-][\\w\\.\\-]*$", options).UsesRegex, "Negated classes cover non-ASCII characters.");
        Assert.IsTrue(PatternMatcher.Create("[a-z]+", options).UsesRegex, "Unanchored patterns search.");
    }
}