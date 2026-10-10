// <copyright file="RegexMatchTimeoutTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Validator.Tests;

/// <summary>
/// A validation whose regular expression match does not finish throws <see cref="RegexMatchTimeoutException"/> at
/// the evaluator's default timeout of 10 seconds. <see cref="JsonSchema.Options"/> has no setting for the timeout,
/// so the test waits for the default.
/// </summary>
[TestClass]
public class RegexMatchTimeoutTests
{
    [TestMethod]
    public void Validate_AMatchThatDoesNotFinish_ThrowsAtTheDefaultTimeout()
    {
        // On this text the pattern backtracks through 2^48 ways to divide the run of "a", in a few megabytes.
        JsonSchema schema = JsonSchema.FromText("""{"$id": "https://example.com/regex-match-timeout", "type": "string", "pattern": "^(a+)+$"}""");
        string slow = "\"" + new string('a', 48) + "#\"";

        Assert.IsTrue(schema.Validate("\"aaa\""));

        // The worker is a background thread. If the match has no timeout it is left behind, and the test fails at
        // the end of the wait instead of never ending.
        Exception thrown = null;
        bool returned = false;
        var worker = new Thread(() =>
        {
            try
            {
                schema.Validate(slow);
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
        if (!worker.Join(TimeSpan.FromSeconds(30)))
        {
            Assert.Fail("The validation had not finished after 30 seconds. The match has no timeout.");
        }

        Assert.IsFalse(returned, "The validation returned an answer. The match was expected not to finish.");
        Assert.IsInstanceOfType<RegexMatchTimeoutException>(thrown);
        Assert.IsTrue(elapsed.Elapsed >= TimeSpan.FromSeconds(9), $"The match stopped after {elapsed.Elapsed}, before the default timeout.");
    }
}
#endif