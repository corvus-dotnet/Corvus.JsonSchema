// <copyright file="ComplianceTestSuiteTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonPath;
using Xunit;
using Xunit.Abstractions;

namespace Corvus.Text.Json.JsonPath.Tests;

/// <summary>
/// Runs the JSONPath Compliance Test Suite (CTS) from the
/// <c>jsonpath-compliance-test-suite</c> submodule against
/// <see cref="JsonPathEvaluator"/>.
/// </summary>
public class ComplianceTestSuiteTests
{
    private static readonly Lazy<TestCase[]> AllTests = new(LoadTests);
    private readonly ITestOutputHelper output;

    public ComplianceTestSuiteTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    public static IEnumerable<object[]> ValidTestCases()
    {
        foreach (TestCase tc in AllTests.Value)
        {
            if (!tc.IsInvalid)
            {
                yield return new object[] { tc };
            }
        }
    }

    public static IEnumerable<object[]> InvalidTestCases()
    {
        foreach (TestCase tc in AllTests.Value)
        {
            if (tc.IsInvalid)
            {
                yield return new object[] { tc };
            }
        }
    }

    [Theory]
    [MemberData(nameof(ValidTestCases))]
    public void ValidSelector(TestCase testCase)
    {
        this.output.WriteLine($"Test: {testCase.Name}");
        this.output.WriteLine($"Selector: {testCase.Selector}");

        JsonElement data = JsonElement.ParseValue(
            System.Text.Encoding.UTF8.GetBytes(testCase.DocumentJson));

        JsonElement result = JsonPathEvaluator.Default.Query(testCase.Selector, data);

        this.output.WriteLine($"Result: {result}");

        if (testCase.ExpectedResults != null)
        {
            // Non-deterministic: any of the expected results is acceptable
            bool matched = false;
            foreach (string expectedJson in testCase.ExpectedResults)
            {
                if (JsonArraysEqual(result.ToString()!, expectedJson))
                {
                    matched = true;
                    break;
                }
            }

            Assert.True(matched,
                $"Result {result} did not match any expected result. " +
                $"Expected one of: {string.Join(" OR ", testCase.ExpectedResults)}");
        }
        else if (testCase.ExpectedResult != null)
        {
            Assert.True(
                JsonArraysEqual(result.ToString()!, testCase.ExpectedResult),
                $"Expected: {testCase.ExpectedResult}\nActual:   {result}");
        }
    }

    [Theory]
    [MemberData(nameof(InvalidTestCases))]
    public void InvalidSelector(TestCase testCase)
    {
        this.output.WriteLine($"Test: {testCase.Name}");
        this.output.WriteLine($"Selector: {testCase.Selector}");

        Assert.ThrowsAny<JsonPathException>(() =>
        {
            JsonElement data = JsonElement.ParseValue("{}"u8);
            JsonPathEvaluator.Default.Query(testCase.Selector, data);
        });
    }

    private static bool JsonArraysEqual(string actualJson, string expectedJson)
    {
        using System.Text.Json.JsonDocument actual = System.Text.Json.JsonDocument.Parse(actualJson);
        using System.Text.Json.JsonDocument expected = System.Text.Json.JsonDocument.Parse(expectedJson);

        return StjDeepEquals(actual.RootElement, expected.RootElement);
    }

    private static bool StjDeepEquals(System.Text.Json.JsonElement a, System.Text.Json.JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            return false;
        }

        switch (a.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Null:
            case System.Text.Json.JsonValueKind.True:
            case System.Text.Json.JsonValueKind.False:
                return true;

            case System.Text.Json.JsonValueKind.Number:
                return a.GetDecimal() == b.GetDecimal();

            case System.Text.Json.JsonValueKind.String:
                return a.GetString() == b.GetString();

            case System.Text.Json.JsonValueKind.Array:
                if (a.GetArrayLength() != b.GetArrayLength())
                {
                    return false;
                }

                using (System.Text.Json.JsonElement.ArrayEnumerator ae = a.EnumerateArray())
                using (System.Text.Json.JsonElement.ArrayEnumerator be = b.EnumerateArray())
                {
                    while (ae.MoveNext() && be.MoveNext())
                    {
                        if (!StjDeepEquals(ae.Current, be.Current))
                        {
                            return false;
                        }
                    }
                }

                return true;

            case System.Text.Json.JsonValueKind.Object:
                int aCount = 0;
                int bCount = 0;
                foreach (System.Text.Json.JsonProperty unused1 in a.EnumerateObject())
                {
                    aCount++;
                }

                foreach (System.Text.Json.JsonProperty unused2 in b.EnumerateObject())
                {
                    bCount++;
                }

                if (aCount != bCount)
                {
                    return false;
                }

                foreach (System.Text.Json.JsonProperty prop in a.EnumerateObject())
                {
                    if (!b.TryGetProperty(prop.Name, out System.Text.Json.JsonElement bv) ||
                        !StjDeepEquals(prop.Value, bv))
                    {
                        return false;
                    }
                }

                return true;

            default:
                return false;
        }
    }

    private static TestCase[] LoadTests()
    {
        string ctsPath = Path.Combine(AppContext.BaseDirectory, "cts.json");
        string json = File.ReadAllText(ctsPath);
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);

        System.Text.Json.JsonElement tests = doc.RootElement.GetProperty("tests");
        List<TestCase> result = new();

        foreach (System.Text.Json.JsonElement test in tests.EnumerateArray())
        {
            string name = test.GetProperty("name").GetString()!;
            string selector = test.GetProperty("selector").GetString()!;

            if (test.TryGetProperty("invalid_selector", out System.Text.Json.JsonElement inv) && inv.GetBoolean())
            {
                result.Add(new TestCase(name, selector, isInvalid: true));
                continue;
            }

            string? documentJson = null;
            if (test.TryGetProperty("document", out System.Text.Json.JsonElement docElem))
            {
                documentJson = docElem.GetRawText();
            }

            string? expectedResult = null;
            if (test.TryGetProperty("result", out System.Text.Json.JsonElement resultElem))
            {
                expectedResult = resultElem.GetRawText();
            }

            string[]? expectedResults = null;
            if (test.TryGetProperty("results", out System.Text.Json.JsonElement resultsElem))
            {
                List<string> list = new();
                foreach (System.Text.Json.JsonElement r in resultsElem.EnumerateArray())
                {
                    list.Add(r.GetRawText());
                }

                expectedResults = list.ToArray();
            }

            result.Add(new TestCase(
                name,
                selector,
                isInvalid: false,
                documentJson: documentJson ?? "null",
                expectedResult: expectedResult,
                expectedResults: expectedResults));
        }

        return result.ToArray();
    }

    /// <summary>
    /// Represents a single test case from the CTS.
    /// </summary>
    public class TestCase
    {
        public TestCase(
            string name,
            string selector,
            bool isInvalid,
            string? documentJson = null,
            string? expectedResult = null,
            string[]? expectedResults = null)
        {
            this.Name = name;
            this.Selector = selector;
            this.IsInvalid = isInvalid;
            this.DocumentJson = documentJson ?? "null";
            this.ExpectedResult = expectedResult;
            this.ExpectedResults = expectedResults;
        }

        public string Name { get; }

        public string Selector { get; }

        public bool IsInvalid { get; }

        public string DocumentJson { get; }

        public string? ExpectedResult { get; }

        public string[]? ExpectedResults { get; }

        public override string ToString() => Name;
    }
}
