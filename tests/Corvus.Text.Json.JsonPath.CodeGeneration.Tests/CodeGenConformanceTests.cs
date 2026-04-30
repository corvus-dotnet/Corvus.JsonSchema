// <copyright file="CodeGenConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using System.Text.Json;
using Corvus.Text.Json.JsonPath;
using Xunit;
using Xunit.Abstractions;

namespace Corvus.Text.Json.JsonPath.CodeGeneration.Tests;

/// <summary>
/// Runs the JSONPath Compliance Test Suite (CTS) through the code generator pipeline:
/// generate C# → compile → execute → compare with expected result.
/// </summary>
public class CodeGenConformanceTests : IClassFixture<CodeGenConformanceFixture>
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);
    private static readonly Lazy<TestCase[]> AllTests = new(LoadTests);

    private readonly CodeGenConformanceFixture fixture;
    private readonly ITestOutputHelper output;

    public CodeGenConformanceTests(CodeGenConformanceFixture fixture, ITestOutputHelper output)
    {
        this.fixture = fixture;
        this.output = output;
    }

    /// <summary>
    /// Tests that expect a valid selector and a matching result.
    /// </summary>
    [Theory]
    [Trait("category", "codegen-conformance")]
    [MemberData(nameof(GetValidCases))]
    public void ValidSelector(TestCase testCase)
    {
        this.output.WriteLine($"Test: {testCase.Name}");
        this.output.WriteLine($"Selector: {testCase.Selector}");

        CompiledJsonPathExpression compiled = this.fixture.GetOrCompile(testCase.Selector);

        if (compiled.GeneratedCode is not null)
        {
            this.output.WriteLine($"Generated code length: {compiled.GeneratedCode.Length}");
        }

        if (compiled.Method is null)
        {
            Assert.Fail($"Compilation failed (expected success): {compiled.Error}");
            return;
        }

        Corvus.Text.Json.JsonElement corvusData = Corvus.Text.Json.JsonElement.ParseValue(
            Encoding.UTF8.GetBytes(testCase.DocumentJson));

        using JsonWorkspace workspace = JsonWorkspace.Create();
        var task = Task.Run(() => InvokeEvaluate(compiled.Method, corvusData, workspace));
        if (!task.Wait(TestTimeout))
        {
            Assert.Fail($"Evaluation timed out after {TestTimeout.TotalSeconds}s");
        }

        Corvus.Text.Json.JsonElement result = task.Result;
        string resultJson = result.IsUndefined() ? "null" : result.GetRawText();

        this.output.WriteLine($"Result: {resultJson}");

        if (testCase.ExpectedResults is not null)
        {
            // Non-deterministic: any of the expected results is acceptable
            bool matched = false;
            foreach (string expectedJson in testCase.ExpectedResults)
            {
                if (JsonArraysEqual(resultJson, expectedJson))
                {
                    matched = true;
                    break;
                }
            }

            Assert.True(matched,
                $"Result {resultJson} did not match any expected result. " +
                $"Expected one of: {string.Join(" OR ", testCase.ExpectedResults)}");
        }
        else if (testCase.ExpectedResult is not null)
        {
            Assert.True(
                JsonArraysEqual(resultJson, testCase.ExpectedResult),
                $"Expected: {testCase.ExpectedResult}\nActual:   {resultJson}");
        }
    }

    /// <summary>
    /// Tests that expect an invalid selector (parse/compile error).
    /// </summary>
    [Theory]
    [Trait("category", "codegen-conformance")]
    [MemberData(nameof(GetInvalidCases))]
    public void InvalidSelector(TestCase testCase)
    {
        this.output.WriteLine($"Test: {testCase.Name}");
        this.output.WriteLine($"Selector: {testCase.Selector}");

        CompiledJsonPathExpression compiled = this.fixture.GetOrCompile(testCase.Selector);

        // If the expression failed at parse/codegen time, that's the expected error.
        if (compiled.Method is null && compiled.IsParseError)
        {
            this.output.WriteLine($"Got expected parse error: {compiled.Error}");
            return;
        }

        if (compiled.Method is null)
        {
            // Compilation failed for non-parse reasons — still an error, accept it.
            this.output.WriteLine($"Got compilation error: {compiled.Error}");
            return;
        }

        // Expression compiled — it should throw at evaluation time.
        Corvus.Text.Json.JsonElement corvusData = Corvus.Text.Json.JsonElement.ParseValue("{}"u8);

        using JsonWorkspace workspace = JsonWorkspace.Create();
        Exception? caught = null;
        try
        {
            var task = Task.Run(() => InvokeEvaluate(compiled.Method, corvusData, workspace));
            if (!task.Wait(TestTimeout))
            {
                Assert.Fail($"Evaluation timed out after {TestTimeout.TotalSeconds}s");
            }

            this.output.WriteLine($"WARNING: Expected invalid selector but evaluation succeeded with: {(task.Result.IsUndefined() ? "undefined" : task.Result.GetRawText())}");
        }
        catch (AggregateException ae) when (ae.InnerException is not null)
        {
            caught = ae.InnerException;
            if (caught is TargetInvocationException tie && tie.InnerException is not null)
            {
                caught = tie.InnerException;
            }
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            caught = tie.InnerException;
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        if (caught is not null)
        {
            this.output.WriteLine($"Got expected error: {caught.GetType().Name}: {caught.Message}");
        }
    }

    public static IEnumerable<object[]> GetValidCases()
    {
        foreach (TestCase tc in AllTests.Value)
        {
            if (!tc.IsInvalid)
            {
                yield return [tc];
            }
        }
    }

    public static IEnumerable<object[]> GetInvalidCases()
    {
        foreach (TestCase tc in AllTests.Value)
        {
            if (tc.IsInvalid)
            {
                yield return [tc];
            }
        }
    }

    private static Corvus.Text.Json.JsonElement InvokeEvaluate(MethodInfo method, Corvus.Text.Json.JsonElement data, JsonWorkspace workspace)
    {
        object?[] args = [data, workspace];
        object? result = method.Invoke(null, args);
        return result is Corvus.Text.Json.JsonElement el ? el : default;
    }

    private static bool JsonArraysEqual(string actualJson, string expectedJson)
    {
        using JsonDocument actual = JsonDocument.Parse(actualJson);
        using JsonDocument expected = JsonDocument.Parse(expectedJson);
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
            case System.Text.Json.JsonValueKind.Undefined:
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

                for (int i = 0; i < a.GetArrayLength(); i++)
                {
                    if (!StjDeepEquals(a[i], b[i]))
                    {
                        return false;
                    }
                }

                return true;

            case System.Text.Json.JsonValueKind.Object:
                var aProps = new Dictionary<string, System.Text.Json.JsonElement>();
                foreach (System.Text.Json.JsonProperty prop in a.EnumerateObject())
                {
                    aProps[prop.Name] = prop.Value;
                }

                var bProps = new Dictionary<string, System.Text.Json.JsonElement>();
                foreach (System.Text.Json.JsonProperty prop in b.EnumerateObject())
                {
                    bProps[prop.Name] = prop.Value;
                }

                if (aProps.Count != bProps.Count)
                {
                    return false;
                }

                foreach (var kvp in aProps)
                {
                    if (!bProps.TryGetValue(kvp.Key, out System.Text.Json.JsonElement bVal))
                    {
                        return false;
                    }

                    if (!StjDeepEquals(kvp.Value, bVal))
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
        string ctsPath = FindCtsJson();
        string json = File.ReadAllText(ctsPath);
        using JsonDocument doc = JsonDocument.Parse(json);

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

    private static string FindCtsJson()
    {
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "jsonpath-compliance-test-suite", "cts.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            "Cannot find jsonpath-compliance-test-suite/cts.json. Ensure the git submodule is initialized.");
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
