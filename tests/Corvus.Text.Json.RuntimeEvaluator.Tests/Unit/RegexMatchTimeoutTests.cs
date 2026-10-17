// <copyright file="RegexMatchTimeoutTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;
using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// A regular expression match has a timeout. Some patterns take .NET's engines longer than anyone will wait, and an
/// evaluation of one of them throws <see cref="RegexMatchTimeoutException"/> to its caller where it used not to
/// return.
/// </summary>
[TestClass]
public class RegexMatchTimeoutTests
{
    // On SlowText this pattern backtracks through every way to divide the run of "a" into "a" and "aa", some five
    // thousand million of them, in a few megabytes, in both engines of .NET. (A group is translated without capture,
    // and .NET then runs ^(a+)+$ as ^a+$, which does not backtrack.)
    private const string BacktracksWithoutEnd = "^(a|aa)+$";

    private static readonly string SlowText = "\"" + new string('a', 48) + "#\"";

    // A run of "a" on which the interpreted engine takes several seconds to try every division (some sixty million
    // of them) and then answers. Each "a" more multiplies the time by 1.6.
    private const int LongerThanTheDefaultRun = 38;

    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(250);

    // The wait for an evaluation whose timeout is the short one. A match with no timeout is a failed assertion at the
    // end of this wait, and not a test run that never ends.
    private static readonly TimeSpan ShortWait = TimeSpan.FromSeconds(5);

    // The wait for an evaluation whose timeout is the default.
    private static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(30);

    /// <summary>How the evaluator under test is made and called.</summary>
    public enum Route
    {
        /// <summary>The schema is compiled from its text and interpreted, with no results collected.</summary>
        Interpreter,

        /// <summary>The schema is compiled from its text and interpreted, with results collected.</summary>
        InterpreterWithResults,

        /// <summary>The evaluator is loaded from a program image.</summary>
        ProgramImage,

        /// <summary>The schema is compiled to IL before its first evaluation.</summary>
        GeneratedCode,

        /// <summary>The evaluator for an entry point of a compiled schema, which shares that schema's options.</summary>
        EntryPoint,
    }

    [TestMethod]
    public void TheDefaultIsOneSecond()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(1), JsonSchemaEvaluatorOptions.DefaultRegexMatchTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(1), new JsonSchemaEvaluatorOptions().RegexMatchTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(1), JsonSchemaEvaluatorOptions.Default.RegexMatchTimeout);
    }

#if NET
    [TestMethod]
    [DataRow(false, Route.Interpreter)]
    [DataRow(false, Route.InterpreterWithResults)]
    [DataRow(false, Route.ProgramImage)]
    [DataRow(false, Route.GeneratedCode)]
    [DataRow(false, Route.EntryPoint)]
    [DataRow(true, Route.Interpreter)]
    [DataRow(true, Route.InterpreterWithResults)]
    [DataRow(true, Route.ProgramImage)]
    [DataRow(true, Route.GeneratedCode)]
    [DataRow(true, Route.EntryPoint)]
    public void APatternThatDoesNotFinishThrowsAtTheTimeout(bool compileRegularExpressions, Route route)
    {
        string schema = """{"$defs": {"entry": {"type": "string", "pattern": """ + Quote(BacktracksWithoutEnd) + """}}, "$ref": "#/$defs/entry"}""";
        AssertTimesOut(schema, SlowText, compileRegularExpressions, route);
    }

    [TestMethod]
    [DataRow(false, Route.Interpreter)]
    [DataRow(false, Route.InterpreterWithResults)]
    [DataRow(false, Route.ProgramImage)]
    [DataRow(false, Route.GeneratedCode)]
    [DataRow(true, Route.Interpreter)]
    [DataRow(true, Route.InterpreterWithResults)]
    [DataRow(true, Route.ProgramImage)]
    [DataRow(true, Route.GeneratedCode)]
    public void APatternPropertyThatDoesNotFinishThrowsAtTheTimeout(bool compileRegularExpressions, Route route)
    {
        string schema = """{"$defs": {"entry": {"type": "object", "patternProperties": {""" + Quote(BacktracksWithoutEnd) + """: {"type": "integer"}}}}, "$ref": "#/$defs/entry"}""";
        AssertTimesOut(schema, "{" + SlowText + ": 1}", compileRegularExpressions, route);
    }

    /// <summary>
    /// The options a caller does not set have the timeout. This is the one test here that waits for the default
    /// timeout itself.
    /// </summary>
    [TestMethod]
    public void WithDefaultOptionsAMatchThatDoesNotFinishThrowsAtTheDefaultTimeout()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile("""{"type": "string", "pattern": """ + Quote(BacktracksWithoutEnd) + "}");

        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        AssertThrowsTimeout(() => evaluator.Evaluate(SlowText), DefaultWait);
        Assert.IsTrue(elapsed.Elapsed >= TimeSpan.FromMilliseconds(900), $"The match stopped after {elapsed.Elapsed}, before the default timeout.");

        // The default is 1 second. The bound is several times that, for a machine that is busy.
        Assert.IsTrue(elapsed.Elapsed < TimeSpan.FromSeconds(8), $"The match stopped after {elapsed.Elapsed}, long after the default timeout.");
    }

    /// <summary>
    /// With <see cref="Regex.InfiniteMatchTimeout"/> a match runs for as long as it takes, as it did before there was
    /// a timeout. The text is sized so that the match takes several times the default timeout and then answers.
    /// </summary>
    [TestMethod]
    public void WithTheTimeoutTurnedOffAMatchLongerThanTheDefaultTimeoutGivesItsAnswer()
    {
        const string schema = """{"type": "string", "pattern": "^(a|aa)+$"}""";
        string text = "\"" + new string('a', LongerThanTheDefaultRun) + "#\"";

        using JsonSchemaEvaluator limited = JsonSchemaEvaluator.Compile(schema, new JsonSchemaEvaluatorOptions { CompileRegularExpressions = false });
        AssertThrowsTimeout(() => limited.Evaluate(text), DefaultWait);

        using JsonSchemaEvaluator unlimited = JsonSchemaEvaluator.Compile(
            schema,
            new JsonSchemaEvaluatorOptions { CompileRegularExpressions = false, RegexMatchTimeout = Regex.InfiniteMatchTimeout });
        Assert.IsFalse(unlimited.Evaluate(text));
    }

    /// <summary>
    /// A match that finishes is not affected: the same options give the pattern's answers.
    /// </summary>
    [TestMethod]
    public void APatternThatFinishesGivesItsAnswerUnderAShortTimeout()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(
            """{"type": "string", "pattern": "^(?:[a-z]+-)+[0-9]{2,}$"}""",
            new JsonSchemaEvaluatorOptions { RegexMatchTimeout = ShortTimeout });

        Assert.IsTrue(evaluator.Evaluate("\"ab-cd-12\""));
        Assert.IsFalse(evaluator.Evaluate("\"ab-cd-1\""));
    }

    /// <summary>
    /// A pattern proved to match in time proportional to the text (EcmaRegexTranslator.IsLinear) has no timeout: a
    /// match of a text of four million characters runs for longer than a timeout of a millisecond in the interpreted
    /// engine, and returns its answer. (The compiled engine can finish this one within the millisecond, so only the
    /// interpreted case would throw if the expression had the timeout.) The pattern is not one the evaluator matches
    /// without a regular expression.
    /// </summary>
    /// <param name="compileRegularExpressions">Whether the expression is compiled.</param>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ALinearPatternHasNoTimeout(bool compileRegularExpressions)
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(
            """{"type": "string", "pattern": "^x[0-9]+?y$"}""",
            new JsonSchemaEvaluatorOptions { CompileRegularExpressions = compileRegularExpressions, RegexMatchTimeout = TimeSpan.FromMilliseconds(1) });

        string digits = new('0', 4_000_000);
        Assert.IsTrue(evaluator.Evaluate("\"x" + digits + "y\""));
        Assert.IsFalse(evaluator.Evaluate("\"x" + digits + "z\""));
    }

    private static void AssertTimesOut(string schema, string instance, bool compileRegularExpressions, Route route)
    {
        var options = new JsonSchemaEvaluatorOptions
        {
            CompileRegularExpressions = compileRegularExpressions,
            RegexMatchTimeout = ShortTimeout,
            CodeGeneration = route == Route.GeneratedCode ? JsonSchemaCodeGeneration.Eager : JsonSchemaCodeGeneration.Disabled,
        };

        if (route == Route.GeneratedCode && !Corvus.Text.Json.RuntimeEvaluator.CodeGeneration.SchemaLowering.IsSupported)
        {
            Assert.Inconclusive("Dynamic code is not supported here.");
        }

        using JsonSchemaEvaluator compiled = JsonSchemaEvaluator.Compile(schema, options);
        JsonSchemaEvaluator evaluator = route switch
        {
            Route.ProgramImage => JsonSchemaEvaluator.FromProgramImage(compiled.ToProgramImage(), options),
            Route.EntryPoint => compiled.ForEntryPoint("#/$defs/entry"),
            _ => compiled,
        };

        try
        {
            if (route == Route.InterpreterWithResults)
            {
                AssertThrowsTimeout(
                    () =>
                    {
                        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
                        evaluator.Evaluate(instance, collector);
                    },
                    ShortWait);
            }
            else
            {
                AssertThrowsTimeout(() => evaluator.Evaluate(instance), ShortWait);
            }
        }
        finally
        {
            if (!ReferenceEquals(evaluator, compiled))
            {
                evaluator.Dispose();
            }
        }
    }

    /// <summary>
    /// Runs the evaluation on a worker and waits for it for a bounded time. A worker that has not finished by then is
    /// left behind as a background thread, which does not keep the test process alive.
    /// </summary>
    private static void AssertThrowsTimeout(Action evaluate, TimeSpan wait)
    {
        Exception? thrown = null;
        bool returned = false;
        var worker = new Thread(() =>
        {
            try
            {
                evaluate();
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

        worker.Start();
        if (!worker.Join(wait))
        {
            Assert.Fail($"The evaluation had not finished after {wait}. The match has no timeout.");
        }

        Assert.IsFalse(returned, "The evaluation returned an answer. The match was expected not to finish.");
        Assert.IsInstanceOfType<RegexMatchTimeoutException>(thrown);
    }

    private static string Quote(string pattern) => "\"" + pattern.Replace("\\", "\\\\") + "\"";
#endif
}