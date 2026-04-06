// <copyright file="JsonataTestSuiteRunner.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Runs every test case from the official JSONata test suite
/// (<c>Jsonata-Test-Suite/test/test-suite/</c>) against <see cref="JsonataEvaluator"/>.
/// </summary>
/// <remarks>
/// <para>
/// Test cases come in two file formats: individual JSON objects (<c>case###.json</c>)
/// and batch JSON arrays containing multiple sub-cases. Both formats are enumerated
/// by <see cref="GetTestCases"/>.
/// </para>
/// <para>
/// Error expectations appear in two forms: a top-level <c>"code"</c> string, or a
/// nested <c>"error": { "code": "..." }</c> object.
/// </para>
/// </remarks>
public class JsonataTestSuiteRunner
{
    /// <summary>
    /// Per-test timeout. Prevents runaway recursive expressions from
    /// consuming unbounded time. The evaluation runs on a thread-pool
    /// thread so a <see cref="StackOverflowException"/> on that thread
    /// will still terminate the process; the timeout handles infinite
    /// loops that do not overflow the stack.
    /// </summary>
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private static readonly string TestSuiteRoot = FindTestSuiteRoot();
    private static readonly JsonataEvaluator Evaluator = new();
    private readonly ITestOutputHelper output;

    public JsonataTestSuiteRunner(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Theory]
    [Trait("Category", "testsuite")]
    [MemberData(nameof(GetTestCases))]
    public void RunTestCase(string group, string caseName, string caseFilePath)
    {
        string caseJson = File.ReadAllText(caseFilePath);
        using var caseDoc = JsonDocument.Parse(caseJson);
        JsonElement root = caseDoc.RootElement;

        // Batch files are JSON arrays; extract the sub-case by index.
        if (root.ValueKind == JsonValueKind.Array)
        {
            // caseName is "filename[index]" — extract the index.
            int bracketPos = caseName.IndexOf('[');
            int closeBracket = caseName.IndexOf(']');
            int index = int.Parse(caseName.Substring(bracketPos + 1, closeBracket - bracketPos - 1));
            root = root[index];
        }

        string expr;
        if (root.TryGetProperty("expr", out var exprEl))
        {
            expr = exprEl.GetString()!;
        }
        else if (root.TryGetProperty("expr-file", out var exprFileEl))
        {
            string exprFileName = exprFileEl.GetString()!;
            string groupDir = Path.Combine(TestSuiteRoot, "groups", group);
            expr = File.ReadAllText(Path.Combine(groupDir, exprFileName));
        }
        else
        {
            Assert.Fail("Test case has neither 'expr' nor 'expr-file'");
            return;
        }

        this.output.WriteLine($"Group:      {group}");
        this.output.WriteLine($"Case:       {caseName}");
        this.output.WriteLine($"Expression: {expr}");

        // Determine depth limit (from test case or default)
        int maxDepth = 500;
        if (root.TryGetProperty("depth", out var depthEl))
        {
            maxDepth = depthEl.GetInt32();
        }

        // Load input data
        JsonElement inputData = default;
        JsonDocument? dataDoc = null;
        bool hasData = false;

        if (root.TryGetProperty("data", out var dataElement))
        {
            string dataJson = dataElement.GetRawText();
            dataDoc = JsonDocument.Parse(dataJson);
            inputData = dataDoc.RootElement;
            hasData = true;
        }
        else if (root.TryGetProperty("dataset", out var datasetElement))
        {
            if (datasetElement.ValueKind != JsonValueKind.Null)
            {
                string datasetName = datasetElement.GetString()!;
                string datasetPath = Path.Combine(TestSuiteRoot, "datasets", datasetName + ".json");
                string datasetJson = File.ReadAllText(datasetPath);
                dataDoc = JsonDocument.Parse(datasetJson);
                inputData = dataDoc.RootElement;
                hasData = true;
            }
        }

        // Parse optional bindings
        Dictionary<string, JsonElement>? bindings = null;
        if (root.TryGetProperty("bindings", out var bindingsElement) && bindingsElement.ValueKind == JsonValueKind.Object)
        {
            bindings = new Dictionary<string, JsonElement>();
            foreach (var prop in bindingsElement.EnumerateObject())
            {
                bindings[prop.Name] = prop.Value.Clone();
            }
        }

        try
        {
            // Determine expected outcome — error cases use either top-level
            // "code" or nested "error.code".
            string? expectedErrorCode = null;
            if (root.TryGetProperty("code", out var codeElement))
            {
                expectedErrorCode = codeElement.GetString()!;
            }
            else if (root.TryGetProperty("error", out var errorElement) &&
                     errorElement.ValueKind == JsonValueKind.Object &&
                     errorElement.TryGetProperty("code", out var nestedCode))
            {
                expectedErrorCode = nestedCode.GetString()!;
            }

            if (expectedErrorCode is not null)
            {
                RunErrorCase(expectedErrorCode, expr, inputData, hasData, bindings, maxDepth);
                this.output.WriteLine($"Got expected error: {expectedErrorCode}");
            }
            else if (root.TryGetProperty("undefinedResult", out _))
            {
                RunUndefinedCase(expr, inputData, hasData, bindings, maxDepth);
                this.output.WriteLine("Got expected undefined result");
            }
            else if (root.TryGetProperty("result", out var expectedResult))
            {
                RunResultCase(expectedResult, expr, inputData, hasData, bindings, maxDepth);
            }
            else
            {
                Assert.Fail("Test case has no expected outcome (result, undefinedResult, code, or error)");
            }
        }
        finally
        {
            dataDoc?.Dispose();
        }
    }

    public static IEnumerable<object[]> GetTestCases()
    {
        string groupsDir = Path.Combine(TestSuiteRoot, "groups");
        if (!Directory.Exists(groupsDir))
        {
            yield break;
        }

        foreach (string groupDir in Directory.GetDirectories(groupsDir).OrderBy(d => d))
        {
            string groupName = Path.GetFileName(groupDir);
            foreach (string caseFile in Directory.GetFiles(groupDir, "*.json").OrderBy(f => f))
            {
                string baseName = Path.GetFileNameWithoutExtension(caseFile);

                // Detect batch files (JSON arrays) vs individual cases.
                string raw = File.ReadAllText(caseFile);
                using var probe = JsonDocument.Parse(raw);
                if (probe.RootElement.ValueKind == JsonValueKind.Array)
                {
                    int count = probe.RootElement.GetArrayLength();
                    for (int i = 0; i < count; i++)
                    {
                        yield return [groupName, $"{baseName}[{i}]", caseFile];
                    }
                }
                else
                {
                    yield return [groupName, baseName, caseFile];
                }
            }
        }
    }

    private static void RunErrorCase(string expectedCode, string expr, JsonElement inputData, bool hasData, Dictionary<string, JsonElement>? bindings, int maxDepth)
    {
        Exception? caught = null;
        try
        {
            var task = Task.Run(() => Evaluator.Evaluate(expr, hasData ? inputData : default, bindings, maxDepth));
            if (!task.Wait(TestTimeout))
            {
                Assert.Fail($"Evaluation timed out after {TestTimeout.TotalSeconds}s");
            }
        }
        catch (AggregateException ae) when (ae.InnerException is not null)
        {
            caught = ae.InnerException;
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);

        if (caught is JsonataException jex)
        {
            Assert.Equal(expectedCode, jex.Code);
        }
    }

    private static void RunUndefinedCase(string expr, JsonElement inputData, bool hasData, Dictionary<string, JsonElement>? bindings, int maxDepth)
    {
        var task = Task.Run(() => Evaluator.Evaluate(expr, hasData ? inputData : default, bindings, maxDepth));
        if (!task.Wait(TestTimeout))
        {
            Assert.Fail($"Evaluation timed out after {TestTimeout.TotalSeconds}s");
        }

        Assert.Equal(JsonValueKind.Undefined, task.Result.ValueKind);
    }

    private void RunResultCase(JsonElement expectedResult, string expr, JsonElement inputData, bool hasData, Dictionary<string, JsonElement>? bindings, int maxDepth)
    {
        var task = Task.Run(() => Evaluator.Evaluate(expr, hasData ? inputData : default, bindings, maxDepth));
        if (!task.Wait(TestTimeout))
        {
            Assert.Fail($"Evaluation timed out after {TestTimeout.TotalSeconds}s");
        }

        JsonElement result = task.Result;

        string expectedJson = expectedResult.GetRawText();
        string actualJson = result.ValueKind == JsonValueKind.Undefined ? "undefined" : result.GetRawText();

        this.output.WriteLine($"Expected: {expectedJson}");
        this.output.WriteLine($"Actual:   {actualJson}");

        Assert.NotEqual(JsonValueKind.Undefined, result.ValueKind);
        AssertJsonEqual(expectedResult, result);
    }

    private static string FindTestSuiteRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "Jsonata-Test-Suite", "test", "test-suite");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Jsonata-Test-Suite", "test", "test-suite"));
    }

    private static void AssertJsonEqual(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind == JsonValueKind.Number && actual.ValueKind == JsonValueKind.Number)
        {
            double e = expected.GetDouble();
            double a = actual.GetDouble();
            Assert.Equal(e, a, 10);
            return;
        }

        if (expected.ValueKind != actual.ValueKind)
        {
            Assert.Fail($"Value kind mismatch: expected {expected.ValueKind}, actual {actual.ValueKind}");
        }

        if (expected.ValueKind == JsonValueKind.Array)
        {
            int expectedLen = expected.GetArrayLength();
            int actualLen = actual.GetArrayLength();
            Assert.Equal(expectedLen, actualLen);
            for (int i = 0; i < expectedLen; i++)
            {
                AssertJsonEqual(expected[i], actual[i]);
            }

            return;
        }

        if (expected.ValueKind == JsonValueKind.Object)
        {
            var expectedProps = new Dictionary<string, JsonElement>();
            foreach (var prop in expected.EnumerateObject())
            {
                expectedProps[prop.Name] = prop.Value;
            }

            var actualProps = new Dictionary<string, JsonElement>();
            foreach (var prop in actual.EnumerateObject())
            {
                actualProps[prop.Name] = prop.Value;
            }

            Assert.Equal(expectedProps.Count, actualProps.Count);
            foreach (var kvp in expectedProps)
            {
                Assert.True(actualProps.ContainsKey(kvp.Key), $"Missing property: {kvp.Key}");
                AssertJsonEqual(kvp.Value, actualProps[kvp.Key]);
            }

            return;
        }

        Assert.Equal(expected.GetRawText(), actual.GetRawText());
    }
}
