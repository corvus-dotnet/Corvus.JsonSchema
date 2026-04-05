// <copyright file="JsonLogicEvaluatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json.JsonLogic;
using Xunit;

namespace Corvus.Text.Json.JsonLogic.Tests;

/// <summary>
/// Tests for <see cref="JsonLogicEvaluator"/> using the official tests.json suite.
/// </summary>
public class JsonLogicEvaluatorTests
{
    /// <summary>
    /// Runs a single test vector from the official JsonLogic test suite using the bytecode VM.
    /// </summary>
    /// <param name="index">The test index.</param>
    /// <param name="rule">The JsonLogic rule as a JSON string.</param>
    /// <param name="data">The data as a JSON string.</param>
    /// <param name="expected">The expected result as a JSON string.</param>
    [Theory]
    [MemberData(nameof(GetTestCases))]
    public void OfficialTestSuite(int index, string rule, string data, string expected)
    {
        _ = index;

        // Parse the rule and data as Corvus JsonElements
        byte[] ruleUtf8 = Encoding.UTF8.GetBytes(rule);
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(data);

        Corvus.Text.Json.JsonElement ruleElement = Corvus.Text.Json.JsonElement.ParseValue(ruleUtf8);
        Corvus.Text.Json.JsonElement dataElement = Corvus.Text.Json.JsonElement.ParseValue(dataUtf8);

        JsonLogicRule logicRule = new(ruleElement);
        Corvus.Text.Json.JsonElement result = JsonLogicEvaluator.Default.Evaluate(logicRule, dataElement);

        // Compare using normalized JSON text
        string expectedText = NormalizeJson(expected);
        string actualText = NormalizeJson(GetRawText(result));
        Assert.Equal(expectedText, actualText);
    }

    /// <summary>
    /// Runs a single test vector from the official JsonLogic test suite using the functional evaluator.
    /// </summary>
    /// <param name="index">The test index.</param>
    /// <param name="rule">The JsonLogic rule as a JSON string.</param>
    /// <param name="data">The data as a JSON string.</param>
    /// <param name="expected">The expected result as a JSON string.</param>
    [Theory]
    [MemberData(nameof(GetTestCases))]
    public void OfficialTestSuiteFunctional(int index, string rule, string data, string expected)
    {
        _ = index;

        byte[] ruleUtf8 = Encoding.UTF8.GetBytes(rule);
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(data);

        Corvus.Text.Json.JsonElement ruleElement = Corvus.Text.Json.JsonElement.ParseValue(ruleUtf8);
        Corvus.Text.Json.JsonElement dataElement = Corvus.Text.Json.JsonElement.ParseValue(dataUtf8);

        JsonLogicRule logicRule = new(ruleElement);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Corvus.Text.Json.JsonElement result = JsonLogicEvaluator.Default.EvaluateFunctional(logicRule, dataElement, workspace);

        string expectedText = NormalizeJson(expected);
        string actualText = NormalizeJson(GetRawText(result));
        Assert.Equal(expectedText, actualText);
    }

    /// <summary>
    /// Gets the test cases from the embedded tests.json resource.
    /// </summary>
    /// <returns>An enumerable of test case data.</returns>
    public static IEnumerable<object[]> GetTestCases()
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Corvus.Text.Json.JsonLogic.Tests.tests.json")!;

        using var doc = System.Text.Json.JsonDocument.Parse(stream);
        System.Text.Json.JsonElement root = doc.RootElement;

        int index = 0;
        foreach (System.Text.Json.JsonElement element in root.EnumerateArray())
        {
            // Skip comment strings (e.g., "# Non-rules get passed through")
            if (element.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                continue;
            }

            // Each test case is [rule, data, expected]
            if (element.ValueKind == System.Text.Json.JsonValueKind.Array && element.GetArrayLength() == 3)
            {
                string rule = element[0].GetRawText();
                string data = element[1].GetRawText();
                string expected = element[2].GetRawText();
                yield return [index, rule, data, expected];
                index++;
            }
        }
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
        // Normalize by re-serializing without indentation to get consistent compact formatting
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        using var ms = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(ms, new System.Text.Json.JsonWriterOptions { Indented = false }))
        {
            doc.RootElement.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
