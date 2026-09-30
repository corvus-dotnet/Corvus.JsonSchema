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
        "^.{1,3}$", "^x-", "^x-.*", "^/api", "^a.b", "^v1.0",
    ];

    private static readonly string[] Inputs =
    [
        string.Empty, "a", "ab", "abcd", "\n", "\r", "\r\n", "a\nb", "\u2028", "a\u2029", "\u2027", "\u00e9", "x-", "x-y", "x-\n",
        "/api", "/api/v1", "a.b", "axb", "a.bc", "v1.0", "v1x0",
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
    public void PatternsAreClassifiedInline(string pattern, string expected)
    {
        Assert.AreEqual(expected, CodeGenerationExtensions.ClassifyRegexPattern(pattern).ToString());
    }
}
