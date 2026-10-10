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