// <copyright file="RegexMatchTimeoutTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;
using Corvus.Text.Json.RuntimeEvaluator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Validator.Tests;

/// <summary>
/// A validation whose regular expression match does not finish throws <see cref="RegexMatchTimeoutException"/> at
/// the timeout of <see cref="JsonSchema.Options.RegexMatchTimeout"/>, which is the evaluator's 1 second unless the
/// options say otherwise.
/// </summary>
[TestClass]
public class RegexMatchTimeoutTests
{
    // On SlowText the pattern backtracks through every way to divide the run of "a" into "a" and "aa", some five
    // thousand million of them, and for each tries the group again. The backreference keeps it from the memo of the
    // places that failed, which makes a pattern without one linear in the text: ^(a|aa)+$ answers at once.
    private const string Schema = """{"type": "string", "pattern": "^(a|aa)+\\1$"}""";

    private static readonly string SlowText = "\"" + new string('a', 48) + "#\"";

    [TestMethod]
    public void TheDefaultIsOneSecond()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(1), JsonSchema.Options.Default.RegexMatchTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(1), new JsonSchema.Options().RegexMatchTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(1), new JsonSchema.Options(alwaysAssertFormat: false).RegexMatchTimeout);

        // The constructors that have no parameter for it.
        Assert.AreEqual(TimeSpan.FromSeconds(1), new JsonSchema.Options(null, true, JsonSchemaDialect.Draft202012, true, null).RegexMatchTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(1), new JsonSchema.Options(null, true, JsonSchemaDialect.Draft202012, true, null, JsonSchemaCodeGeneration.Disabled).RegexMatchTimeout);
    }

    [TestMethod]
    public void TheOptionsHoldTheTimeoutTheyWereGiven()
    {
        Assert.AreEqual(TimeSpan.FromMilliseconds(250), new JsonSchema.Options(regexMatchTimeout: TimeSpan.FromMilliseconds(250)).RegexMatchTimeout);
        Assert.AreEqual(Regex.InfiniteMatchTimeout, new JsonSchema.Options(regexMatchTimeout: Regex.InfiniteMatchTimeout).RegexMatchTimeout);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-2)]
    [DataRow(-1000)]
    [DataRow(int.MaxValue)]
    public void ATimeoutARegularExpressionDoesNotAcceptIsRefused(int milliseconds)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new JsonSchema.Options(regexMatchTimeout: TimeSpan.FromMilliseconds(milliseconds)));
    }

#if NET
    [TestMethod]
    public void Validate_AMatchThatDoesNotFinish_ThrowsAtTheDefaultTimeout()
    {
        JsonSchema schema = JsonSchema.FromText(Schema, "https://example.com/regex-match-timeout");

        Assert.IsTrue(schema.Validate("\"aaa\""));

        TimeSpan elapsed = AssertThrowsTimeout(schema, TimeSpan.FromSeconds(30));
        Assert.IsTrue(elapsed >= TimeSpan.FromMilliseconds(900), $"The match stopped after {elapsed}, before the default timeout.");

        // The default is 1 second. The bound is several times that, for a machine that is busy.
        Assert.IsTrue(elapsed < TimeSpan.FromSeconds(8), $"The match stopped after {elapsed}, long after the default timeout.");
    }

    [TestMethod]
    [DataRow(JsonSchemaCodeGeneration.Disabled)]
    [DataRow(JsonSchemaCodeGeneration.Eager)]
    public void Validate_WithAShortTimeout_ThrowsAtThatTimeout(JsonSchemaCodeGeneration codeGeneration)
    {
        var options = new JsonSchema.Options(codeGeneration: codeGeneration, regexMatchTimeout: TimeSpan.FromMilliseconds(250));
        JsonSchema schema = JsonSchema.FromText(Schema, "https://example.com/regex-match-timeout-short-" + codeGeneration, options);

        Assert.IsTrue(schema.Validate("\"aaa\""));
        Assert.IsFalse(schema.Validate("\"aab\""));

        TimeSpan elapsed = AssertThrowsTimeout(schema, TimeSpan.FromSeconds(8));
        Assert.IsTrue(elapsed < TimeSpan.FromSeconds(8), $"The match stopped after {elapsed}, which is not the short timeout.");
    }

    /// <summary>
    /// A compiled schema is cached with the timeout it was compiled with, so the same schema asked for with another
    /// timeout is not the cached one.
    /// </summary>
    [TestMethod]
    public void Validate_TheSameSchemaWithAnotherTimeout_HasItsOwnTimeout()
    {
        const string uri = "https://example.com/regex-match-timeout-cache";
        JsonSchema withDefault = JsonSchema.FromText(Schema, uri);
        JsonSchema withShort = JsonSchema.FromText(Schema, uri, new JsonSchema.Options(regexMatchTimeout: TimeSpan.FromMilliseconds(250)));

        Assert.IsTrue(withDefault.Validate("\"aaa\""));
        TimeSpan elapsed = AssertThrowsTimeout(withShort, TimeSpan.FromSeconds(8));
        Assert.IsTrue(elapsed < TimeSpan.FromSeconds(8), $"The match stopped after {elapsed}, which is not the short timeout.");
    }

    /// <summary>
    /// With no timeout a match that finishes gives its answer. (One that does not finish is not run here.)
    /// </summary>
    [TestMethod]
    public void Validate_WithNoTimeout_GivesTheAnswerOfAMatchThatFinishes()
    {
        var options = new JsonSchema.Options(regexMatchTimeout: Regex.InfiniteMatchTimeout);
        JsonSchema schema = JsonSchema.FromText(Schema, "https://example.com/regex-match-timeout-none", options);

        Assert.IsTrue(schema.Validate("\"aaa\""));
        Assert.IsFalse(schema.Validate("\"aab\""));
    }

    /// <summary>
    /// Validates SlowText on a worker and waits for it for a bounded time. The worker is a background thread. If
    /// the match has no timeout it is left behind, and the test fails at the end of the wait instead of never ending.
    /// </summary>
    private static TimeSpan AssertThrowsTimeout(JsonSchema schema, TimeSpan wait)
    {
        Exception thrown = null;
        bool returned = false;
        var worker = new Thread(() =>
        {
            try
            {
                schema.Validate(SlowText);
                returned = true;
            }
            catch (Exception ex)
            {
                thrown = ex;
            }
        })
        {
            IsBackground = true,
        };

        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        worker.Start();
        if (!worker.Join(wait))
        {
            Assert.Fail($"The validation had not finished after {wait}. The match has no timeout, or not the one asked for.");
        }

        Assert.IsFalse(returned, "The validation returned an answer. The match was expected not to finish.");
        Assert.IsInstanceOfType<RegexMatchTimeoutException>(thrown);
        return elapsed.Elapsed;
    }
#endif
}