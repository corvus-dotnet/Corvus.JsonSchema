// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Corvus.Text.Json.EvaluatorTestSuite.Tests;

/// <summary>
/// Covers per-format assertion modes (assert/disable/warning) in the standalone schema
/// evaluator generation path (<see cref="Corvus.Text.Json.CodeGeneration.CodeGenerationMode.SchemaEvaluationOnly"/>).
/// </summary>
[TestClass]
public class FormatAssertionModeEvaluatorTests
{
    private const string DateTimeSchema =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "format": "date-time"
        }
        """;

    private const string Invalid = "\"not-a-date-time\"";
    private const string Valid = "\"2024-01-02T03:04:05Z\"";

    [TestMethod]
    public async Task Assert_InvalidValue_Fails()
    {
        CompiledEvaluator evaluator = await GenerateAsync(validateFormat: true, overrides: null);

        StringAssert.Contains(evaluator.GeneratedCode!, "MatchDateTime");

        using var invalid = ParsedJsonDocument<JsonElement>.Parse(Invalid);
        Assert.IsFalse(evaluator.Evaluate(invalid.RootElement), "Default assert mode must reject a non-conformant value.");

        using var valid = ParsedJsonDocument<JsonElement>.Parse(Valid);
        Assert.IsTrue(evaluator.Evaluate(valid.RootElement));
    }

    [TestMethod]
    public async Task Warning_InvalidValue_Passes()
    {
        CompiledEvaluator evaluator = await GenerateAsync(
            validateFormat: true,
            overrides: new Dictionary<string, FormatAssertionMode> { ["date-time"] = FormatAssertionMode.Warning });

        // The standalone evaluator emits the warning-mode sibling.
        StringAssert.Contains(evaluator.GeneratedCode!, "WarnDateTime");
        Assert.IsFalse(evaluator.GeneratedCode!.Contains("MatchDateTime"), "Warning mode must replace the assertion.");

        using var invalid = ParsedJsonDocument<JsonElement>.Parse(Invalid);
        Assert.IsTrue(evaluator.Evaluate(invalid.RootElement), "Warning mode must accept a non-conformant value.");

        using var valid = ParsedJsonDocument<JsonElement>.Parse(Valid);
        Assert.IsTrue(evaluator.Evaluate(valid.RootElement));
    }

    [TestMethod]
    public async Task Disable_InvalidValue_Passes()
    {
        CompiledEvaluator evaluator = await GenerateAsync(
            validateFormat: true,
            overrides: new Dictionary<string, FormatAssertionMode> { ["date-time"] = FormatAssertionMode.Disable });

        Assert.IsFalse(evaluator.GeneratedCode!.Contains("MatchDateTime"), "Disabled format must not assert.");
        Assert.IsFalse(evaluator.GeneratedCode!.Contains("WarnDateTime"), "Disabled format must not warn.");

        using var invalid = ParsedJsonDocument<JsonElement>.Parse(Invalid);
        Assert.IsTrue(evaluator.Evaluate(invalid.RootElement), "Disabled format must accept a non-conformant value.");
    }

    private static ValueTask<CompiledEvaluator> GenerateAsync(bool validateFormat, IReadOnlyDictionary<string, FormatAssertionMode>? overrides)
    {
        return TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
            "formatModeEvaluator.json",
            DateTimeSchema,
            "Corvus.Text.Json.EvaluatorTestSuite.Tests.FormatModeEvaluator",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat,
            overrides,
            Assembly.GetExecutingAssembly());
    }
}