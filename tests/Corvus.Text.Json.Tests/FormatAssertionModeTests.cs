// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.Validator;
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

        StringAssert.Contains(code, "MatchDateTime");
        Assert.IsFalse(code.Contains("WarnDateTime"), "Default mode must not emit the warning variant.");
    }

    [TestMethod]
    public async Task DisableOverride_EmitsIgnoredAnnotation()
    {
        string code = await GenerateAsync(
            DateTimeObject,
            validateFormat: true,
            overrides: new Dictionary<string, FormatAssertionMode> { ["date-time"] = FormatAssertionMode.Disable });

        StringAssert.Contains(code, "IgnoredFormatNotAsserted");
        Assert.IsFalse(code.Contains("MatchDateTime"), "Disabled format must not emit an assertion.");
        Assert.IsFalse(code.Contains("WarnDateTime"), "Disabled format must not emit the warning variant.");
    }

    [TestMethod]
    public async Task WarningOverride_EmitsWarningVariant()
    {
        string code = await GenerateAsync(
            DateTimeObject,
            validateFormat: true,
            overrides: new Dictionary<string, FormatAssertionMode> { ["date-time"] = FormatAssertionMode.Warning });

        StringAssert.Contains(code, "WarnDateTime");
        Assert.IsFalse(code.Contains("MatchDateTime"), "Warning mode must replace the assertion with the warning variant.");
    }

    [TestMethod]
    public async Task WarningOverride_IsPerFormat()
    {
        // Only date-time is downgraded to a warning; uuid still asserts.
        string code = await GenerateAsync(
            DateTimeAndUuidObject,
            validateFormat: true,
            overrides: new Dictionary<string, FormatAssertionMode> { ["date-time"] = FormatAssertionMode.Warning });

        StringAssert.Contains(code, "WarnDateTime");
        StringAssert.Contains(code, "MatchUuid");
        Assert.IsFalse(code.Contains("MatchDateTime"), "date-time should use the warning variant.");
        Assert.IsFalse(code.Contains("WarnUuid"), "uuid should still assert.");
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