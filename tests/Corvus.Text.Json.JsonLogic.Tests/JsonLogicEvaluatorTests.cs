// <copyright file="JsonLogicEvaluatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using Corvus.Text.Json.JsonLogic;
using Xunit;

namespace Corvus.Text.Json.JsonLogic.Tests;

/// <summary>
/// Tests for <see cref="JsonLogicEvaluator"/> using the official tests.json suite.
/// </summary>
public class JsonLogicEvaluatorTests
{
    /// <summary>
    /// Runs a single test vector from the official JsonLogic test suite.
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

        // TODO: Phase 2 — implement evaluation and assert results
        // For now, just verify we can parse the test vectors
        // and that the test infrastructure is working.
        Assert.NotNull(rule);
        Assert.NotNull(data);
        Assert.NotNull(expected);
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
}
