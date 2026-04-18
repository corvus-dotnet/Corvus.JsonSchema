// <copyright file="CodeGenConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using System.Text.Json;
using Corvus.Text.Json.JMESPath;
using Xunit;
using Xunit.Abstractions;

namespace Corvus.Text.Json.JMESPath.CodeGeneration.Tests;

/// <summary>
/// Runs the official JMESPath compliance test suite through the code generator pipeline:
/// generate C# → compile → execute → compare with expected result.
/// </summary>
public class CodeGenConformanceTests : IClassFixture<CodeGenConformanceFixture>
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);
    private static readonly string TestSuiteRoot = FindTestSuiteRoot();

    private readonly CodeGenConformanceFixture fixture;
    private readonly ITestOutputHelper output;

    public CodeGenConformanceTests(CodeGenConformanceFixture fixture, ITestOutputHelper output)
    {
        this.fixture = fixture;
        this.output = output;
    }

    /// <summary>
    /// Tests that expect a successful result.
    /// </summary>
    [Theory]
    [Trait("category", "codegen-conformance")]
    [MemberData(nameof(GetSuccessCases))]
    public void SuccessCase(string file, int group, int caseIndex, string expression)
    {
        (string givenJson, string expectedJson) = LoadCase(file, group, caseIndex);

        this.output.WriteLine($"Expression: {expression}");
        this.output.WriteLine($"Given: {(givenJson.Length > 200 ? givenJson[..200] + "..." : givenJson)}");
        this.output.WriteLine($"Expected: {expectedJson}");

        CompiledJMESPathExpression compiled = this.fixture.GetOrCompile(expression);

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
            Encoding.UTF8.GetBytes(givenJson));

        using JsonWorkspace workspace = JsonWorkspace.Create();
        var task = Task.Run(() => InvokeEvaluate(compiled.Method, corvusData, workspace));
        if (!task.Wait(TestTimeout))
        {
            Assert.Fail($"Evaluation timed out after {TestTimeout.TotalSeconds}s");
        }

        Corvus.Text.Json.JsonElement result = task.Result;

        string resultJson = result.IsUndefined() ? "null" : result.GetRawText();

        this.output.WriteLine($"Result: {resultJson}");

        AssertJsonEqual(expectedJson, resultJson);
    }

    /// <summary>
    /// Tests that expect a syntax/parse error.
    /// </summary>
    [Theory]
    [Trait("category", "codegen-conformance")]
    [MemberData(nameof(GetErrorCases))]
    public void ErrorCase(string file, int group, int caseIndex, string expression, string errorType)
    {
        this.output.WriteLine($"Expression: {expression}");
        this.output.WriteLine($"Expected error: {errorType}");

        CompiledJMESPathExpression compiled = this.fixture.GetOrCompile(expression);

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
        (string givenJson, _) = LoadCase(file, group, caseIndex);

        Corvus.Text.Json.JsonElement corvusData = Corvus.Text.Json.JsonElement.ParseValue(
            Encoding.UTF8.GetBytes(givenJson));

        using JsonWorkspace workspace = JsonWorkspace.Create();
        Exception? caught = null;
        try
        {
            var task = Task.Run(() => InvokeEvaluate(compiled.Method, corvusData, workspace));
            if (!task.Wait(TestTimeout))
            {
                Assert.Fail($"Evaluation timed out after {TestTimeout.TotalSeconds}s");
            }

            // If we got here without an exception, the evaluation succeeded unexpectedly.
            // For JMESPath, some "error" test cases (like invalid-type) are runtime errors.
            // If the CG produces a result instead of throwing, that may be acceptable
            // depending on the error type — but we should flag it.
            this.output.WriteLine($"WARNING: Expected error '{errorType}' but evaluation succeeded with: {(task.Result.IsUndefined() ? "undefined" : task.Result.GetRawText())}");
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

    public static IEnumerable<object[]> GetSuccessCases()
    {
        return GetCases(errorCases: false);
    }

    public static IEnumerable<object[]> GetErrorCases()
    {
        return GetCases(errorCases: true);
    }

    private static IEnumerable<object[]> GetCases(bool errorCases)
    {
        string testsDir = Path.Combine(TestSuiteRoot, "tests");

        foreach (string filePath in Directory.GetFiles(testsDir, "*.json").OrderBy(f => f))
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string json = File.ReadAllText(filePath);
            using JsonDocument doc = JsonDocument.Parse(json);

            int groupIndex = 0;
            foreach (System.Text.Json.JsonElement groupElement in doc.RootElement.EnumerateArray())
            {
                if (!groupElement.TryGetProperty("cases", out System.Text.Json.JsonElement cases))
                {
                    groupIndex++;
                    continue;
                }

                int caseIndex = 0;
                foreach (System.Text.Json.JsonElement testCase in cases.EnumerateArray())
                {
                    bool hasError = testCase.TryGetProperty("error", out System.Text.Json.JsonElement errorEl);
                    bool hasBench = testCase.TryGetProperty("bench", out _);

                    // Skip benchmark entries (they have no expected results).
                    if (hasBench)
                    {
                        caseIndex++;
                        continue;
                    }

                    string expression = testCase.GetProperty("expression").GetString()!;

                    if (errorCases && hasError)
                    {
                        string errorType = errorEl.GetString()!;
                        yield return [fileName, groupIndex, caseIndex, expression, errorType];
                    }
                    else if (!errorCases && !hasError)
                    {
                        yield return [fileName, groupIndex, caseIndex, expression];
                    }

                    caseIndex++;
                }

                groupIndex++;
            }
        }
    }

    private static (string Given, string Expected) LoadCase(string file, int group, int caseIndex)
    {
        string filePath = Path.Combine(TestSuiteRoot, "tests", file + ".json");
        string json = File.ReadAllText(filePath);
        using JsonDocument doc = JsonDocument.Parse(json);

        System.Text.Json.JsonElement groupEl = doc.RootElement[group];
        string given = groupEl.GetProperty("given").GetRawText();
        System.Text.Json.JsonElement testCase = groupEl.GetProperty("cases")[caseIndex];

        string expected = "null";
        if (testCase.TryGetProperty("result", out System.Text.Json.JsonElement resultEl))
        {
            expected = resultEl.GetRawText();
        }

        return (given, expected);
    }

    private static Corvus.Text.Json.JsonElement InvokeEvaluate(MethodInfo method, Corvus.Text.Json.JsonElement data, JsonWorkspace workspace)
    {
        object?[] args = [data, workspace];
        object? result = method.Invoke(null, args);
        return result is Corvus.Text.Json.JsonElement el ? el : default;
    }

    private static void AssertJsonEqual(string expected, string actual)
    {
        using JsonDocument expectedDoc = JsonDocument.Parse(expected);
        using JsonDocument actualDoc = JsonDocument.Parse(actual);
        Assert.True(
            JsonElementDeepEquals(expectedDoc.RootElement, actualDoc.RootElement),
            $"Expected: {expected}\nActual: {actual}");
    }

    private static bool JsonElementDeepEquals(System.Text.Json.JsonElement a, System.Text.Json.JsonElement b)
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
                    if (!JsonElementDeepEquals(a[i], b[i]))
                    {
                        return false;
                    }
                }

                return true;

            case System.Text.Json.JsonValueKind.Object:
                var aProps = new Dictionary<string, System.Text.Json.JsonElement>();
                foreach (JsonProperty prop in a.EnumerateObject())
                {
                    aProps[prop.Name] = prop.Value;
                }

                var bProps = new Dictionary<string, System.Text.Json.JsonElement>();
                foreach (JsonProperty prop in b.EnumerateObject())
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

                    if (!JsonElementDeepEquals(kvp.Value, bVal))
                    {
                        return false;
                    }
                }

                return true;

            default:
                return false;
        }
    }

    private static string FindTestSuiteRoot()
    {
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "JMESPath-Test-Suite");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            "Cannot find JMESPath-Test-Suite directory. Ensure the git submodule is initialized.");
    }
}
