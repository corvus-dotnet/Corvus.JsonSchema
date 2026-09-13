// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Json.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for per-format assertion modes (assert/disable/warning) in V5 code generation,
/// and for the shared <see cref="FormatAssertionModeParser"/>.
/// </summary>
[TestClass]
public class FormatAssertionModeTests
{
    private const string DateTimeObject =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "created": { "type": "string", "format": "date-time" }
            }
        }
        """;

    private const string DateTimeAndUuidObject =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "created": { "type": "string", "format": "date-time" },
                "id": { "type": "string", "format": "uuid" }
            }
        }
        """;

    [TestMethod]
    public async Task DefaultMode_EmitsFormatAssertion()
    {
        string code = await GenerateAsync(DateTimeObject, validateFormat: true, overrides: null);

        // Validation is performed by the schema evaluation program; the default mode asserts every format
        // and emits no per-format override.
        StringAssert.Contains(code, "AssertFormat = true");
        Assert.IsFalse(code.Contains("FormatModes"), "Default mode must not emit format overrides.");
    }

    [TestMethod]
    public async Task DisableOverride_EmitsIgnoredAnnotation()
    {
        string code = await GenerateAsync(
            DateTimeObject,
            validateFormat: true,
            overrides: new Dictionary<string, FormatAssertionMode> { ["date-time"] = FormatAssertionMode.Disable });

        StringAssert.Contains(code, "[\"date-time\"] = JsonSchemaFormatMode.Disable");
    }

    [TestMethod]
    public async Task WarningOverride_EmitsWarningVariant()
    {
        string code = await GenerateAsync(
            DateTimeObject,
            validateFormat: true,
            overrides: new Dictionary<string, FormatAssertionMode> { ["date-time"] = FormatAssertionMode.Warning });

        StringAssert.Contains(code, "[\"date-time\"] = JsonSchemaFormatMode.Warning");
    }

    [TestMethod]
    public async Task WarningOverride_IsPerFormat()
    {
        // Only date-time is downgraded to a warning; uuid still asserts.
        string code = await GenerateAsync(
            DateTimeAndUuidObject,
            validateFormat: true,
            overrides: new Dictionary<string, FormatAssertionMode> { ["date-time"] = FormatAssertionMode.Warning });

        StringAssert.Contains(code, "[\"date-time\"] = JsonSchemaFormatMode.Warning");
        Assert.IsFalse(code.Contains("[\"uuid\"]"), "uuid has no override and still asserts.");
    }

    [TestMethod]
    public async Task WildcardOverride_SetsModeForEveryFormat()
    {
        // The "*" wildcard sets the default mode for all formats — this is how a single switch
        // ("--formatMode disable") yields annotation-only output, even overriding a vocabulary that asserts.
        string code = await GenerateAsync(
            DateTimeAndUuidObject,
            validateFormat: true,
            overrides: new Dictionary<string, FormatAssertionMode> { ["*"] = FormatAssertionMode.Disable });

        StringAssert.Contains(code, "[\"*\"] = JsonSchemaFormatMode.Disable");
    }

    [TestMethod]
    public async Task WildcardOverride_IsOutrankedByPerFormatOverride()
    {
        // A specific per-format override takes precedence over the "*" wildcard default.
        string code = await GenerateAsync(
            DateTimeAndUuidObject,
            validateFormat: true,
            overrides: new Dictionary<string, FormatAssertionMode> { ["*"] = FormatAssertionMode.Disable, ["uuid"] = FormatAssertionMode.Assert });

        StringAssert.Contains(code, "[\"*\"] = JsonSchemaFormatMode.Disable");
        StringAssert.Contains(code, "[\"uuid\"] = JsonSchemaFormatMode.Assert");
    }

    [TestMethod]
    public async Task WarningOverride_GeneratedCodeCompiles()
    {
        DynamicJsonType type = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            "formatMode_warning_compiles.json",
            DateTimeObject,
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            formatModeOverrides: new Dictionary<string, FormatAssertionMode> { ["date-time"] = FormatAssertionMode.Warning },
            hostAssembly: Assembly.GetExecutingAssembly());

        Assert.IsNotNull(type);
    }

    [TestMethod]
    [DataRow("assert", FormatAssertionMode.Assert)]
    [DataRow("ASSERT", FormatAssertionMode.Assert)]
    [DataRow("disable", FormatAssertionMode.Disable)]
    [DataRow("Warning", FormatAssertionMode.Warning)]
    public void Parser_TryParseMode_RecognizesModes(string text, FormatAssertionMode expected)
    {
        Assert.IsTrue(FormatAssertionModeParser.TryParseMode(text, out FormatAssertionMode mode));
        Assert.AreEqual(expected, mode);
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("")]
    [DataRow(null)]
    public void Parser_TryParseMode_RejectsUnknown(string? text)
    {
        Assert.IsFalse(FormatAssertionModeParser.TryParseMode(text, out _));
    }

    [TestMethod]
    public void Parser_ParseSpecification_ParsesCommaAndSemicolonPairs()
    {
        IReadOnlyDictionary<string, FormatAssertionMode> result =
            FormatAssertionModeParser.ParseSpecification("date-time=disable,time=warning;uuid=assert", ';', ',');

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(FormatAssertionMode.Disable, result["date-time"]);
        Assert.AreEqual(FormatAssertionMode.Warning, result["time"]);
        Assert.AreEqual(FormatAssertionMode.Assert, result["uuid"]);
    }

    [TestMethod]
    [DataRow("disable", FormatAssertionMode.Disable)]
    [DataRow("assert", FormatAssertionMode.Assert)]
    [DataRow("warning", FormatAssertionMode.Warning)]
    public void Parser_ParseSpecification_BareModeSetsWildcard(string bareMode, FormatAssertionMode expected)
    {
        // A bare mode (no '<format>=') is the global default for every format, recorded under "*".
        IReadOnlyDictionary<string, FormatAssertionMode> result =
            FormatAssertionModeParser.ParseSpecification(bareMode, ',');

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(expected, result["*"]);
    }

    [TestMethod]
    public void Parser_ParseSpecification_BareModeCombinesWithPerFormatPairs()
    {
        // A bare global mode plus explicit per-format pairs: the bare mode lands under "*".
        IReadOnlyDictionary<string, FormatAssertionMode> result =
            FormatAssertionModeParser.ParseSpecification("disable,uuid=assert", ',');

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(FormatAssertionMode.Disable, result["*"]);
        Assert.AreEqual(FormatAssertionMode.Assert, result["uuid"]);
    }

    [TestMethod]
    public void Parser_ParseSpecification_EmptyIsEmpty()
    {
        Assert.AreEqual(0, FormatAssertionModeParser.ParseSpecification(null, ',').Count);
        Assert.AreEqual(0, FormatAssertionModeParser.ParseSpecification("  ", ',').Count);
    }

    [TestMethod]
    [DataRow("date-time")]
    [DataRow("date-time=bogus")]
    [DataRow("=disable")]
    public void Parser_ParseSpecification_ThrowsOnMalformed(string spec)
    {
        Assert.ThrowsExactly<System.FormatException>(() => FormatAssertionModeParser.ParseSpecification(spec, ','));
    }

    private static ValueTask<string> GenerateAsync(string schema, bool validateFormat, IReadOnlyDictionary<string, FormatAssertionMode>? overrides)
    {
        return TestJsonSchemaCodeGenerator.GenerateCodeTextForVirtualFile(
            "formatMode_test.json",
            schema,
            "Corvus.Text.Json.Tests.FormatModeGenerated",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat,
            overrides);
    }
}