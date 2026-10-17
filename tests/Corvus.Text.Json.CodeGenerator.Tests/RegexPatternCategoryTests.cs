// <copyright file="RegexPatternCategoryTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.RegularExpressions;
using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// The inline tests the emitter writes for <c>patternProperties</c> patterns (always, non-empty, prefix and length
/// range) agree with the regular expression they replace, including on line terminators, which ECMA-262's <c>.</c>
/// does not match.
/// </summary>
[TestClass]
public class RegexPatternCategoryTests
{
    private static readonly string[] Patterns =
    [
        ".*", "(.*)", "^.*$", "^(.*)$", "[\\s\\S]*", "^[\\s\\S]*$", ".+", "(.+)", ".", "^.+$", "^(.+)$",
        "^.{1,3}$", "^x-", "^x-.*", "^/api", "^a.b", "^v1.0", "^abc\n", "^abc.*\n", "^.{1,3}$\n", "^x-\n",
    ];

    private static readonly string[] Inputs =
    [
        string.Empty, "a", "ab", "abcd", "\n", "\r", "\r\n", "a\nb", "\u2028", "a\u2029", "\u2027", "\u00e9", "x-", "x-y", "x-\n",
        "/api", "/api/v1", "a.b", "axb", "a.bc", "v1.0", "v1x0", "abc", "abcd", "abc\n", "abcd\n", "abc\nd", "ab\n",
    ];

    [TestMethod]
    public void InlineMatchesAgreeWithTheRegularExpression()
    {
        foreach (string pattern in Patterns)
        {
            RegexPatternCategory category = CodeGenerationExtensions.ClassifyRegexPattern(pattern);
            var regex = new Regex(EcmaRegexTranslator.TranslateOrFallback(pattern), RegexOptions.CultureInvariant);
            foreach (string input in Inputs)
            {
                byte[] utf8 = Encoding.UTF8.GetBytes(input);
                bool actual = category switch
                {
                    RegexPatternCategory.Noop => true,
                    RegexPatternCategory.NonEmpty => JsonSchemaEvaluation.MatchNonEmptyRegularExpression(utf8),
                    RegexPatternCategory.Prefix => utf8.AsSpan().StartsWith(Encoding.UTF8.GetBytes(CodeGenerationExtensions.ExtractRegexPrefix(pattern))),
                    RegexPatternCategory.Range => JsonSchemaEvaluation.MatchRangeRegularExpression(utf8, CodeGenerationExtensions.ExtractRegexRange(pattern).Min, CodeGenerationExtensions.ExtractRegexRange(pattern).Max),
                    _ => regex.IsMatch(input),
                };

                Assert.AreEqual(regex.IsMatch(input), actual, $"pattern {pattern} ({category}) on \"{Regex.Escape(input)}\"");
            }
        }
    }

    [TestMethod]
    [DataRow(".*", "Noop")]
    [DataRow("^[\\s\\S]*$", "Noop")]
    [DataRow(".+", "NonEmpty")]
    [DataRow(".", "NonEmpty")]
    [DataRow("^.*$", "Range")]
    [DataRow("^(.+)$", "Range")]
    [DataRow("^x-", "Prefix")]
    [DataRow("^a.b", "FullRegex")]
    [DataRow("^abc\n", "FullRegex")]
    [DataRow("^abc.*\n", "FullRegex")]
    [DataRow("^.{1,3}$\n", "FullRegex")]
    public void PatternsAreClassifiedInline(string pattern, string expected)
    {
        Assert.AreEqual(expected, CodeGenerationExtensions.ClassifyRegexPattern(pattern).ToString());
    }

    /// <summary>
    /// A pattern that ends in a line feed is not the prefix or the length range it would be without it. The line feed
    /// is a character the property name must hold. The answers are V8's:
    /// <c>new RegExp(pattern, "u").test(name)</c>.
    /// </summary>
    [TestMethod]
    [DataRow("^abc\n", "abc", false)]
    [DataRow("^abc\n", "abcd", false)]
    [DataRow("^abc\n", "abcd\n", false)]
    [DataRow("^abc\n", "abc\n", true)]
    [DataRow("^abc\n", "abc\nd", true)]
    [DataRow("^abc.*\n", "abc", false)]
    [DataRow("^abc.*\n", "abcd", false)]
    [DataRow("^abc.*\n", "abcd\n", true)]
    [DataRow("^abc.*\n", "abc\nd", true)]
    [DataRow("^.{1,3}$\n", "ab", false)]
    [DataRow("^.{1,3}$\n", "abc", false)]
    [DataRow("^.{1,3}$\n", "ab\n", false)]
    [DataRow("^.{1,3}$\n", "\n", false)]
    [DataRow("^abc", "abcd", true)]
    [DataRow("^abc", "abc\n", true)]
    [DataRow("^abc.*", "abc\nd", true)]
    [DataRow("^.{1,3}$", "ab", true)]
    [DataRow("^.{1,3}$", "ab\n", false)]
    [DataRow("^.+$", "ab\n", false)]
    [DataRow("^.*$", "\n", false)]
    public void APatternThatEndsInALineFeedIsNotAPrefixOrARange(string pattern, string input, bool expected)
    {
        Assert.AreEqual(expected, InlineMatch(pattern, input), $"pattern {Regex.Escape(pattern)} ({CodeGenerationExtensions.ClassifyRegexPattern(pattern)}) on \"{Regex.Escape(input)}\"");
    }

    /// <summary>What the test the emitter writes for a pattern answers for a property name.</summary>
    private static bool InlineMatch(string pattern, string input)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(input);
        return CodeGenerationExtensions.ClassifyRegexPattern(pattern) switch
        {
            RegexPatternCategory.Noop => true,
            RegexPatternCategory.NonEmpty => JsonSchemaEvaluation.MatchNonEmptyRegularExpression(utf8),
            RegexPatternCategory.Prefix => utf8.AsSpan().StartsWith(Encoding.UTF8.GetBytes(CodeGenerationExtensions.ExtractRegexPrefix(pattern))),
            RegexPatternCategory.Range => JsonSchemaEvaluation.MatchRangeRegularExpression(utf8, CodeGenerationExtensions.ExtractRegexRange(pattern).Min, CodeGenerationExtensions.ExtractRegexRange(pattern).Max),
            _ => new Regex(EcmaRegexTranslator.TranslateOrFallback(pattern), RegexOptions.CultureInvariant).IsMatch(input),
        };
    }
}
