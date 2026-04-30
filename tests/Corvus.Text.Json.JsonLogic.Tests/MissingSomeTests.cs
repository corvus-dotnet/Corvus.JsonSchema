// <copyright file="MissingSomeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.JsonLogic;
using Xunit;

namespace Corvus.Text.Json.JsonLogic.Tests;

/// <summary>
/// Tests for the <c>missing_some</c> JsonLogic operator, which checks that at least N
/// of the specified data paths are present and returns the missing ones if not.
/// </summary>
public class MissingSomeTests
{
    // ----- Basic cases -----

    [Fact]
    public void AllPresent_ReturnsEmptyArray()
    {
        string result = Evaluate(
            """{"missing_some":[1, ["a","b","c"]]}""",
            """{"a":1, "b":2, "c":3}""");
        Assert.Equal("[]", result);
    }

    [Fact]
    public void NonePresent_ReturnsMissingPaths()
    {
        string result = Evaluate(
            """{"missing_some":[1, ["a","b","c"]]}""",
            """{}""");
        Assert.Equal("""["a","b","c"]""", result);
    }

    [Fact]
    public void SomePresent_EnoughToMeetThreshold_ReturnsEmpty()
    {
        string result = Evaluate(
            """{"missing_some":[2, ["a","b","c"]]}""",
            """{"a":1, "b":2}""");
        Assert.Equal("[]", result);
    }

    [Fact]
    public void SomePresent_NotEnough_ReturnsMissing()
    {
        string result = Evaluate(
            """{"missing_some":[2, ["a","b","c"]]}""",
            """{"a":1}""");
        Assert.Equal("""["b","c"]""", result);
    }

    // ----- Edge cases -----

    [Fact]
    public void ThresholdZero_AlwaysReturnsEmpty()
    {
        string result = Evaluate(
            """{"missing_some":[0, ["a","b","c"]]}""",
            """{}""");
        Assert.Equal("[]", result);
    }

    [Fact]
    public void ThresholdGreaterThanTotal_AllPresent_ReturnsEmpty()
    {
        string result = Evaluate(
            """{"missing_some":[10, ["a","b"]]}""",
            """{"a":1, "b":2}""");
        Assert.Equal("[]", result);
    }

    [Fact]
    public void ThresholdGreaterThanTotal_SomeMissing_ReturnsMissing()
    {
        string result = Evaluate(
            """{"missing_some":[10, ["a","b"]]}""",
            """{"a":1}""");
        Assert.Equal("""["b"]""", result);
    }

    [Fact]
    public void EmptyPathsArray_ReturnsEmpty()
    {
        string result = Evaluate(
            """{"missing_some":[1, []]}""",
            """{"a":1}""");
        Assert.Equal("[]", result);
    }

    [Fact]
    public void SinglePath_Present_ReturnsEmpty()
    {
        string result = Evaluate(
            """{"missing_some":[1, ["a"]]}""",
            """{"a":1}""");
        Assert.Equal("[]", result);
    }

    [Fact]
    public void SinglePath_Missing_ReturnsThatPath()
    {
        string result = Evaluate(
            """{"missing_some":[1, ["a"]]}""",
            """{}""");
        Assert.Equal("""["a"]""", result);
    }

    [Fact]
    public void NullData_TreatsAllAsMissing()
    {
        string result = Evaluate(
            """{"missing_some":[1, ["a","b"]]}""",
            "null");
        Assert.Equal("""["a","b"]""", result);
    }

    // ----- Helpers -----

    private static string Evaluate(string rule, string data)
    {
        byte[] ruleUtf8 = Encoding.UTF8.GetBytes(rule);
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(data);

        Corvus.Text.Json.JsonElement ruleElement = Corvus.Text.Json.JsonElement.ParseValue(ruleUtf8);
        Corvus.Text.Json.JsonElement dataElement = Corvus.Text.Json.JsonElement.ParseValue(dataUtf8);

        JsonLogicRule logicRule = new(ruleElement);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Corvus.Text.Json.JsonElement result = JsonLogicEvaluator.Default.Evaluate(logicRule, dataElement, workspace);

        return NormalizeJson(GetRawText(result));
    }

    private static string GetRawText(Corvus.Text.Json.JsonElement element)
    {
        if (element.IsNullOrUndefined())
        {
            return "null";
        }

        return element.GetRawText();
    }

    private static string NormalizeJson(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        using var ms = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(ms, new System.Text.Json.JsonWriterOptions { Indented = false }))
        {
            doc.RootElement.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
