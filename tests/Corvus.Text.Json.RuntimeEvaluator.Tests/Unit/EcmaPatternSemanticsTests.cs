// <copyright file="EcmaPatternSemanticsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.RuntimeEvaluator;
using Corvus.Text.Json.Tests.EcmaPatterns;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// The <c>pattern</c> keyword answers as V8 does for the patterns of <see cref="EcmaPatternCases"/>, with compiled
/// regular expressions, with interpreted ones, and in generated code.
/// </summary>
[TestClass]
public class EcmaPatternSemanticsTests
{
    /// <summary>How the evaluator under test matches a pattern.</summary>
    public enum Engine
    {
        /// <summary>The default options: the regular expression is compiled.</summary>
        CompiledRegex,

        /// <summary><see cref="JsonSchemaEvaluatorOptions.CompileRegularExpressions"/> is off.</summary>
        InterpretedRegex,

        /// <summary>The schema is compiled to IL before its first evaluation.</summary>
        GeneratedCode,
    }

    [TestMethod]
    [DataRow(Engine.CompiledRegex)]
    [DataRow(Engine.InterpretedRegex)]
    [DataRow(Engine.GeneratedCode)]
    public void PatternsWrittenFromTheTreeAnswerAsV8(Engine engine)
    {
        AssertCases(EcmaPatternCases.WrittenFromTheTree, engine);
    }

    [TestMethod]
    [DataRow(Engine.CompiledRegex)]
    [DataRow(Engine.InterpretedRegex)]
    [DataRow(Engine.GeneratedCode)]
    public void PatternsWithBackreferencesAnswerAsV8(Engine engine)
    {
        AssertCases(EcmaPatternCases.Backreferences, engine);
    }

    [TestMethod]
    [DataRow(Engine.CompiledRegex)]
    [DataRow(Engine.InterpretedRegex)]
    [DataRow(Engine.GeneratedCode)]
    public void PatternsWithEmptyIterationsAnswerAsV8(Engine engine)
    {
        AssertCases(EcmaPatternCases.EmptyIterations, engine);
    }

    [TestMethod]
    [DataRow(Engine.CompiledRegex)]
    [DataRow(Engine.InterpretedRegex)]
    [DataRow(Engine.GeneratedCode)]
    public void PatternsThatFaultedTheEngineAnswerAsV8(Engine engine)
    {
        AssertCases(EcmaPatternCases.EngineFaults, engine);
    }

    [TestMethod]
    [DataRow(Engine.CompiledRegex)]
    [DataRow(Engine.InterpretedRegex)]
    [DataRow(Engine.GeneratedCode)]
    public void PatternsWithACaseInsensitiveBackreferenceAnswerAsV8(Engine engine)
    {
        AssertCases(EcmaPatternCases.CaseInsensitiveBackreferences, engine);
    }

    /// <summary>
    /// Patterns with a backreference that ignores case inside the group it reads. Compiling a schema with one of
    /// them did not return when the group refers to itself twice, so the schemas are compiled on another thread
    /// here and one that does not compile in time is a failure of the test.
    /// </summary>
    [TestMethod]
    [DataRow(Engine.CompiledRegex)]
    [DataRow(Engine.InterpretedRegex)]
    [DataRow(Engine.GeneratedCode)]
    public void PatternsWithACaseInsensitiveBackreferenceToItsOwnGroupAnswerAsV8(Engine engine)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                AssertCases(EcmaPatternCases.CaseInsensitiveBackreferencesToTheirOwnGroup, engine);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        })
        {
            IsBackground = true,
        };
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(120)), "The schemas were not compiled and evaluated in 120 seconds.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    [TestMethod]
    [DataRow(Engine.CompiledRegex)]
    [DataRow(Engine.InterpretedRegex)]
    [DataRow(Engine.GeneratedCode)]
    public void PatternsWithABackreferenceToAGroupThatIsNotSetAnswerAsV8(Engine engine)
    {
        AssertCases(EcmaPatternCases.BackreferencesToGroupsThatAreNotSet, engine);
    }

    [TestMethod]
    [DataRow(Engine.CompiledRegex)]
    [DataRow(Engine.InterpretedRegex)]
    [DataRow(Engine.GeneratedCode)]
    public void PatternsWithARepeatedBackreferenceAnswerAsV8(Engine engine)
    {
        AssertCases(EcmaPatternCases.RepeatedBackreferences, engine);
    }

    [TestMethod]
    [DataRow(Engine.CompiledRegex)]
    [DataRow(Engine.InterpretedRegex)]
    [DataRow(Engine.GeneratedCode)]
    public void PatternsWithAGroupThatCapturesTheEmptyStringAnswerAsV8(Engine engine)
    {
        AssertCases(EcmaPatternCases.GroupsThatCaptureTheEmptyString, engine);
    }

    /// <summary>
    /// Plain patterns of that kind on a text of 100,000 characters, with the default match limit of 1 second. Each
    /// is answered, in every engine. Before the groups were left unset where they capture nothing, an iteration of
    /// each was made to fail when it matched nothing by a check that reads the rest of the input, which .NET's
    /// interpreted engine does one character at a time: the first pattern took it 24 seconds on this text, and under
    /// the limit that is an exception and no answer. V8 answers each of these in time in proportion to the text.
    /// </summary>
    [TestMethod]
    [DataRow(Engine.CompiledRegex)]
    [DataRow(Engine.InterpretedRegex)]
    [DataRow(Engine.GeneratedCode)]
    public void ALongTextIsAnsweredWithinTheDefaultLimitWhereAGroupCapturesTheEmptyString(Engine engine)
    {
        const int Half = 50_000;
        string ab = string.Concat(Enumerable.Repeat("ab", Half));
        (string Pattern, string Matches, string Fails)[] cases =
        [
            (@"^(a|)(?:\1|b)*$", "a" + new string('b', 2 * Half), "a" + new string('b', 2 * Half) + "!"),
            (@"^(a|)(?:\1|b)*(c|)(?:\2|d)*$", "a" + new string('b', Half) + "c" + new string('d', Half), "a" + new string('b', Half) + "c" + new string('d', Half) + "!"),
            (@"^(?:(a)|b|)*\1$", ab + "aa", ab + "a!"),
            (@"^((a|)\2)*$", new string('a', 2 * Half), new string('a', 2 * Half) + "!"),
            (@"^(?:(|a)\1)+$", new string('a', 2 * Half), new string('a', 2 * Half) + "!"),
            (@"^(?:(a*)\1b|c)*$", string.Concat(Enumerable.Repeat("aab", Half / 2)), string.Concat(Enumerable.Repeat("aab", Half / 2)) + "!"),
        ];

        if (engine == Engine.GeneratedCode && !PatternValidityTests.GeneratedCodeIsSupported)
        {
            Assert.Inconclusive("Dynamic code is not supported here.");
        }

        // The options have the default limit: none is set.
        var options = new JsonSchemaEvaluatorOptions
        {
            CompileRegularExpressions = engine != Engine.InterpretedRegex,
            CodeGeneration = engine == Engine.GeneratedCode ? JsonSchemaCodeGeneration.Eager : JsonSchemaCodeGeneration.Disabled,
        };

        foreach ((string pattern, string matches, string fails) in cases)
        {
            using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile("{\"pattern\": " + PatternValidityTests.Json(pattern) + "}", options);
            Assert.IsTrue(evaluator.Evaluate("\"" + matches + "\""), pattern);
            Assert.IsFalse(evaluator.Evaluate("\"" + fails + "\""), pattern);
        }
    }

    [TestMethod]
    [DataRow(Engine.CompiledRegex)]
    [DataRow(Engine.InterpretedRegex)]
    [DataRow(Engine.GeneratedCode)]
    public void PatternsWithTheScriptOfNoCharacterAnswerAsEcma262Says(Engine engine)
    {
        AssertCases(EcmaPatternCases.ScriptOfNoCharacter, engine);
    }

    [TestMethod]
    [DataRow(Engine.CompiledRegex)]
    [DataRow(Engine.InterpretedRegex)]
    [DataRow(Engine.GeneratedCode)]
    public void PatternsWithAPartThatNeverMatchesAnswerAsV8(Engine engine)
    {
        AssertCases(EcmaPatternCases.PartsThatNeverMatch, engine);
    }

    /// <summary>
    /// Patterns whose translation keeps a lazy repetition of a group in a lookbehind, after a repetition whose
    /// iterations hold a lookbehind and can match nothing. .NET's compiled engine does not return from a match of
    /// these, and its interpreted engine answers. The translation is marked for the interpreted engine, and the
    /// evaluator honours the mark whatever its options say. The second pattern is from the ECMA-262 regular
    /// expression test data, and the answers are V8's.
    /// </summary>
    [TestMethod]
    [DataRow(Engine.CompiledRegex)]
    [DataRow(Engine.InterpretedRegex)]
    [DataRow(Engine.GeneratedCode)]
    public void PatternsTheCompiledEngineDoesNotReturnFromAnswerAsV8(Engine engine)
    {
        (string Pattern, string[] Matches, string[] Fails)[] cases =
        [
        ("(\\B|[]){4,}(?:(?<=(.??)\\2))$", ["", "-", "-x-"], ["-a", "--a", "a", "ab", "-ab", "abab", "xx-a", "x", "ax", "-x", "--x", "a-x", "aabab"]),
        ("(?:([^a]*?|x)+\\B|[^a]){4,}(?:[ax](?<=(.??){1}\\2|(?!ax))|\\b|)+x$", ["ax", "-x", "--x"], ["", "-", "-a", "--a", "-x-", "a", "ab", "-ab", "abab", "xx-a", "x", "a-x", "aabab"]),
        ("(?:x*\\B){2}(?<=((?:ab)*?)\\1)$", ["", "-", "-x-"], ["-a", "--a", "a", "ab", "-ab", "abab", "xx-a", "x", "ax", "-x", "--x", "a-x", "aabab"]),
        ];

        AssertCases(cases, engine);
    }

    internal static void AssertCases((string Pattern, string[] Matches, string[] Fails)[] cases, Engine engine)
    {
        if (engine == Engine.GeneratedCode && !PatternValidityTests.GeneratedCodeIsSupported)
        {
            Assert.Inconclusive("Dynamic code is not supported here.");
        }

        // A match that does not return is a failure here and not a test run that never ends.
        var options = new JsonSchemaEvaluatorOptions
        {
            CompileRegularExpressions = engine != Engine.InterpretedRegex,
            CodeGeneration = engine == Engine.GeneratedCode ? JsonSchemaCodeGeneration.Eager : JsonSchemaCodeGeneration.Disabled,
            RegexMatchTimeout = TimeSpan.FromSeconds(5),
        };

        var failures = new List<string>();
        int wrongPatterns = 0;
        int counted = 0;
        foreach ((string pattern, string[] matches, string[] fails) in cases)
        {
            wrongPatterns += failures.Count > counted ? 1 : 0;
            counted = failures.Count;
            JsonSchemaEvaluator evaluator;
            try
            {
                evaluator = JsonSchemaEvaluator.Compile("{\"pattern\": " + PatternValidityTests.Json(pattern) + "}", options);
            }
            catch (Exception ex)
            {
                failures.Add($"{pattern} was refused: {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            using (evaluator)
            {
                foreach ((string[] texts, bool expected) in new[] { (matches, true), (fails, false) })
                {
                    foreach (string text in texts)
                    {
                        try
                        {
                            if (evaluator.Evaluate(PatternValidityTests.Json(text)) != expected)
                            {
                                failures.Add($"{pattern} on {PatternValidityTests.Json(text)}: expected {expected}");
                            }
                        }
                        catch (Exception ex)
                        {
                            failures.Add($"{pattern} on {PatternValidityTests.Json(text)}: {ex.GetType().Name}");
                        }
                    }
                }
            }
        }

        wrongPatterns += failures.Count > counted ? 1 : 0;
        if (failures.Count > 0)
        {
            Assert.Fail($"{failures.Count} wrong answers in {wrongPatterns} of {cases.Length} patterns. The first of them:\n{string.Join("\n", failures.Take(25))}");
        }
    }
}