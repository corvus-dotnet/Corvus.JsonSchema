// <copyright file="PatternSizeLimitTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Corvus.Text.Json.RuntimeEvaluator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// A schema whose <c>pattern</c> or <c>patternProperties</c> pattern is over a limit of size is refused when it is
/// compiled, with <see cref="JsonSchemaCompilationException"/>. The message says that the pattern is too large and
/// names the limit. It does not say that the pattern is not valid. A schema may come from outside, and no pattern in
/// it is to make <see cref="JsonSchemaEvaluator.Compile(string, JsonSchemaEvaluatorOptions?)"/> run for long or hold
/// much memory.
/// </summary>
[TestClass]
public class PatternSizeLimitTests
{
    private const string NestedCounts = "(?:(?:(?:(?:(?:(?:(?:(?:(?:ab){4}){4}){4}){4}){4}){4}){4}){4}){4}";

    private const string NestedCountsMessage =
        "The pattern '" + NestedCounts + "' is a valid ECMA-262 regular expression, and is too large. Its repetitions with a least count of 2, 3 or 4, one inside another, come to more than 65,536 copies of what they repeat, beyond four times its size.";

    // No refusal here takes a hundredth of this on a machine with nothing else to do. The bound is for one that is busy.
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(20);

    /// <summary>How the evaluator is configured.</summary>
    public enum Configuration
    {
        /// <summary>The default options: regular expressions are compiled.</summary>
        Compiled,

        /// <summary>Regular expressions are interpreted.</summary>
        Interpreted,

        /// <summary>The schema is compiled to IL before its first evaluation.</summary>
        GeneratedCode,
    }

    [TestMethod]
    [DataRow(Configuration.Compiled, false)]
    [DataRow(Configuration.Interpreted, false)]
    [DataRow(Configuration.GeneratedCode, false)]
    [DataRow(Configuration.Compiled, true)]
    [DataRow(Configuration.Interpreted, true)]
    [DataRow(Configuration.GeneratedCode, true)]
    public void ASchemaWithAPatternOverALimitIsRefusedAsTooLarge(Configuration configuration, bool asPropertyName)
    {
        string schema = asPropertyName
            ? "{\"type\": \"object\", \"patternProperties\": {" + Json(NestedCounts) + ": {\"type\": \"integer\"}}}"
            : "{\"type\": \"string\", \"pattern\": " + Json(NestedCounts) + "}";

        // Twice, as the second time the pattern is one the process has seen.
        for (int time = 0; time < 2; time++)
        {
            JsonSchemaCompilationException thrown = Assert.ThrowsExactly<JsonSchemaCompilationException>(
                () => JsonSchemaEvaluator.Compile(schema, Options(configuration)));
            Assert.AreEqual(NestedCountsMessage, thrown.Message);
        }
    }

    /// <summary>One pattern for each limit, with what the message ends in.</summary>
    [TestMethod]
    [DataRow("length", "It is longer than 1,048,576 characters.")]
    [DataRow("nesting", "Its groups nest more than 200 deep.")]
    [DataRow("unclosed", "Its groups nest more than 200 deep.")]
    [DataRow("written", "What is written for .NET from it is longer than 1,048,576 characters.")]
    [DataRow("doubling", "What is written for .NET from it is longer than 1,048,576 characters.")]
    [DataRow("steps", "It takes more than 2,000,000 steps to translate.")]
    [DataRow("copies", "Its repetitions with a least count of 2, 3 or 4, one inside another, come to more than 65,536 copies of what they repeat, beyond four times its size.")]
    [DataRow("pieces", "Its sequences and alternations are longer than .NET constructs a regular expression from in bounded time: by the count of the translator, a cost of more than 100,000,000.")]
    [DataRow("alternatives", "Its sequences and alternations are longer than .NET constructs a regular expression from in bounded time: by the count of the translator, a cost of more than 100,000,000.")]
    public void EachLimitIsNamedInTheMessageAndMetInBoundedTime(string which, string limit)
    {
        string pattern = which switch
        {
            "length" => new string('a', 1_048_577),
            "nesting" => Nested("(", "a", ")", 10_000),
            "unclosed" => new string('(', 10_000),
            "written" => @"(?i:^(.)\1\1\1\1\1\1\1$)",
            "doubling" => "^" + Nested("(?:", "(a)", "|){1,2}", 70) + @"\1$",
            "steps" => string.Concat(Enumerable.Repeat("(?<n>a)|", 4000)) + "b",
            "copies" => Nested("(?:", "a", "){2}", 140),
            "pieces" => string.Concat(Enumerable.Repeat(@"\.", 12_000)),
            "alternatives" => string.Join("|", Enumerable.Range(0, 25_000).Select(k => "w" + k)),
            _ => throw new ArgumentException(which),
        };

        var elapsed = Stopwatch.StartNew();
        JsonSchemaCompilationException thrown = Assert.ThrowsExactly<JsonSchemaCompilationException>(
            () => JsonSchemaEvaluator.Compile("{\"pattern\": " + Json(pattern) + "}"));
        Assert.IsTrue(elapsed.Elapsed < Generous, $"{elapsed.Elapsed} to refuse the pattern.");

        StringAssert.EndsWith(thrown.Message, limit);
        StringAssert.Contains(thrown.Message, "too large");
        Assert.IsTrue(thrown.Message.IndexOf("not a valid", StringComparison.Ordinal) < 0, thrown.Message);

        // A pattern over a limit may be megabytes long. The message has its start.
        Assert.IsTrue(thrown.Message.Length < 600, thrown.Message.Length.ToString());
    }

    /// <summary>
    /// A pattern that is not valid is still refused as one, with what is wrong and where, and a pattern that is
    /// neither is compiled and matched.
    /// </summary>
    [TestMethod]
    [DataRow(Configuration.Compiled)]
    [DataRow(Configuration.Interpreted)]
    [DataRow(Configuration.GeneratedCode)]
    public void APatternUnderTheLimitsIsNotAffected(Configuration configuration)
    {
        JsonSchemaCompilationException invalid = Assert.ThrowsExactly<JsonSchemaCompilationException>(
            () => JsonSchemaEvaluator.Compile("""{"pattern": "^[a-z]{3,1}$"}""", Options(configuration)));
        Assert.AreEqual(
            "The pattern '^[a-z]{3,1}$' is not a valid ECMA-262 regular expression. Numbers out of order in {} quantifier at offset 11.",
            invalid.Message);

        using JsonSchemaEvaluator ordinary = JsonSchemaEvaluator.Compile("""{"pattern": "^(?:(?:ab){4}){4}-[0-9a-f]{8}$"}""", Options(configuration));
        Assert.IsTrue(ordinary.Evaluate("\"" + string.Concat(Enumerable.Repeat("ab", 16)) + "-0123abcd\""));
        Assert.IsFalse(ordinary.Evaluate("\"" + string.Concat(Enumerable.Repeat("ab", 15)) + "-0123abcd\""));

        // Six case-insensitive backreferences to a group that holds any character are just under the limit on what
        // is written (some 945,000 characters), and the schema compiles and answers.
        using JsonSchemaEvaluator large = JsonSchemaEvaluator.Compile("""{"pattern": "(?i:^(.)\\1\\1\\1\\1\\1\\1$)"}""", Options(configuration));
        Assert.IsTrue(large.Evaluate("\"kK\u212AkK\u212Ak\""));
        Assert.IsFalse(large.Evaluate("\"kK\u212AkK\u212Aj\""));
    }

    [TestMethod]
    public void ToDotNetPatternRefusesAPatternOverALimitAsTheCompilerDoes()
    {
        JsonSchemaCompilationException thrown = Assert.ThrowsExactly<JsonSchemaCompilationException>(
            () => JsonSchemaEvaluator.ToDotNetPattern(NestedCounts));
        Assert.AreEqual(NestedCountsMessage, thrown.Message);

        Assert.AreEqual(@"^(?:a|aa)+\z", JsonSchemaEvaluator.ToDotNetPattern("^(a|aa)+$"));
    }

    /// <summary>
    /// The evaluator keeps the translation and the regular expression of a pattern for the life of the process, so
    /// that a pattern many schemas share is read once. A process may compile schemas from outside for as long as it
    /// runs, so what is kept is bounded: past the bound it is let go, and a pattern met again is read again.
    /// </summary>
    [TestMethod]
    public void WhatIsKeptOfPatternsForTheLifeOfTheProcessIsBounded()
    {
        var options = new JsonSchemaEvaluatorOptions { CompileRegularExpressions = false };
        string unique = Guid.NewGuid().ToString("N");
        int most = 0;
        for (int k = 0; k < Corvus.Text.Json.RuntimeEvaluator.Compilation.PatternMatcher.MaxCachedPatterns + 600; k++)
        {
            using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile("{\"pattern\": \"^(?:a|b)+" + unique + k + "$\"}", options);
            most = Math.Max(most, Corvus.Text.Json.RuntimeEvaluator.Compilation.PatternMatcher.CachedPatternCount);
        }

        // Other tests compile patterns while this one runs, so the count may pass the bound by what they add
        // between one look at it and the next.
        Assert.IsTrue(most <= Corvus.Text.Json.RuntimeEvaluator.Compilation.PatternMatcher.MaxCachedPatterns + 64, most.ToString());

        // A pattern that was let go is read again, and answers as it did.
        using JsonSchemaEvaluator again = JsonSchemaEvaluator.Compile("{\"pattern\": \"^(?:a|b)+" + unique + "0$\"}", options);
        Assert.IsTrue(again.Evaluate("\"ab" + unique + "0\""));
        Assert.IsFalse(again.Evaluate("\"ab" + unique + "1\""));
    }

    private static JsonSchemaEvaluatorOptions Options(Configuration configuration)
    {
        // Where code cannot be generated the setting is accepted and the schema is interpreted.
        return new JsonSchemaEvaluatorOptions
        {
            CompileRegularExpressions = configuration != Configuration.Interpreted,
            CodeGeneration = configuration == Configuration.GeneratedCode ? JsonSchemaCodeGeneration.Eager : JsonSchemaCodeGeneration.Disabled,
        };
    }

    private static string Nested(string open, string inner, string close, int depth)
    {
        return string.Concat(Enumerable.Repeat(open, depth)) + inner + string.Concat(Enumerable.Repeat(close, depth));
    }

    private static string Json(string text) => "\"" + text.Replace("\\", "\\\\") + "\"";
}