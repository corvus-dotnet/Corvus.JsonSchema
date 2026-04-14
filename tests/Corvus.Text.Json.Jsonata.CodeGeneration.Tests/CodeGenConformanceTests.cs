// <copyright file="CodeGenConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Jsonata;
using Xunit;
using Xunit.Abstractions;

namespace Corvus.Text.Json.Jsonata.CodeGeneration.Tests;

/// <summary>
/// Runs every JSONata conformance test case through the code generator pipeline:
/// generate C# → compile → execute → compare with expected result.
/// </summary>
/// <remarks>
/// <para>
/// Each expression is dynamically compiled the first time it is encountered and
/// cached via <see cref="CodeGenConformanceFixture"/> for subsequent test cases
/// that share the same expression. This mirrors the per-test dynamic compilation
/// pattern used by the JSON Schema test suite.
/// </para>
/// </remarks>
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

    [Theory]
    [Trait("category", "codegen-conformance")]
    [MemberData(nameof(GetTestCases))]
    public void RunTestCase(string group, string caseName)
    {
        // Reconstruct file path: caseName is "filename" or "filename[index]".
        string baseName = caseName;
        int arrayIndex = -1;
        int bracketPos = caseName.IndexOf('[');
        if (bracketPos >= 0)
        {
            int closeBracket = caseName.IndexOf(']');
            arrayIndex = int.Parse(caseName.Substring(bracketPos + 1, closeBracket - bracketPos - 1));
            baseName = caseName[..bracketPos];
        }

        string caseFilePath = Path.Combine(TestSuiteRoot, "groups", group, baseName + ".json");
        string caseJson = File.ReadAllText(caseFilePath);
        using var caseDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(caseJson));
        JsonElement root = caseDoc.RootElement;

        if (arrayIndex >= 0)
        {
            root = root[arrayIndex];
        }

        // Extract the expression.
        string? expr;
        try
        {
            if (root.TryGetProperty("expr", out var exprEl))
            {
                expr = exprEl.GetString();
            }
            else if (root.TryGetProperty("expr-file", out var exprFileEl))
            {
                string exprFileName = exprFileEl.GetString()!;
                string groupDir = Path.Combine(TestSuiteRoot, "groups", group);
                expr = File.ReadAllText(Path.Combine(groupDir, exprFileName));
            }
            else
            {
                this.output.WriteLine("SKIP: Test case has neither 'expr' nor 'expr-file'");
                return;
            }
        }
        catch (InvalidOperationException)
        {
            // Malformed UTF-16 (e.g., lone surrogates in encodeUrl tests).
            this.output.WriteLine("SKIP: Expression contains malformed UTF-16");
            return;
        }

        if (expr is null)
        {
            this.output.WriteLine("SKIP: Expression is null");
            return;
        }

        this.output.WriteLine($"Group:      {group}");
        this.output.WriteLine($"Case:       {caseName}");
        this.output.WriteLine($"Expression: {expr}");

        // Extract bindings, depth, and timelimit if present.
        bool hasBindings = root.TryGetProperty("bindings", out var bindingsEl) &&
                           bindingsEl.ValueKind == JsonValueKind.Object &&
                           bindingsEl.EnumerateObject().Any();
        int maxDepth = 500;
        if (root.TryGetProperty("depth", out var depthEl))
        {
            maxDepth = depthEl.GetInt32();
        }

        int timeLimitMs = 0;
        if (root.TryGetProperty("timelimit", out var timelimitEl))
        {
            timeLimitMs = timelimitEl.GetInt32();
        }

        bool needsBindingsOverload = hasBindings || maxDepth != 500 || timeLimitMs != 0;

        // Dynamically compile the expression (cached per unique expression string).
        CompiledExpression compiled = this.fixture.GetOrCompile(expr);

        this.output.WriteLine($"Inlined:    {compiled.IsInlined}");

        // Determine expected outcome first — we need this to know how to handle
        // compilation failures.
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

        bool expectsUndefined = root.TryGetProperty("undefinedResult", out _);
        bool expectsResult = root.TryGetProperty("result", out var expectedResult);

        if (compiled.Method is null)
        {
            // Compilation failed — was this expected?
            if (compiled.ErrorCode is not null && expectedErrorCode is not null)
            {
                // Parse error with a matching expected error code — validate it.
                this.output.WriteLine($"Got parse error: {compiled.ErrorCode}");
                Assert.Equal(expectedErrorCode, compiled.ErrorCode);
                return;
            }

            if (compiled.ErrorCode is not null && expectedErrorCode is null)
            {
                // Parse error but we expected a result/undefined — this is a codegen bug.
                Assert.Fail($"Unexpected parse error (expected {(expectsUndefined ? "undefined" : "result")}): {compiled.Error}");
                return;
            }

            // Non-parse failure (timeout, Roslyn error, etc.) — fail with details.
            Assert.Fail($"Compilation failed: {compiled.Error}");
            return;
        }

        // Load input data.
        JsonElement inputData = default;
        ParsedJsonDocument<JsonElement>? dataDoc = null;
        bool hasData = false;

        if (root.TryGetProperty("data", out var dataElement))
        {
            string dataJson = dataElement.GetRawText();
            dataDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(dataJson));
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
                dataDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(datasetJson));
                inputData = dataDoc.RootElement;
                hasData = true;
            }
        }

        // Parse bindings if present.
        Dictionary<string, JsonElement>? bindings = null;
        ParsedJsonDocument<JsonElement>? bindingsDoc = null;
        if (hasBindings)
        {
            string bindingsJson = bindingsEl.GetRawText();
            bindingsDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(bindingsJson));
            bindings = new Dictionary<string, JsonElement>();
            foreach (var prop in bindingsDoc.RootElement.EnumerateObject())
            {
                bindings[prop.Name] = prop.Value;
            }
        }

        try
        {
            // Choose invocation method: 5-parameter overload when bindings/depth/timeout are needed.
            MethodInfo method = needsBindingsOverload ? (compiled.BindingsMethod ?? compiled.Method) : compiled.Method;

            if (expectedErrorCode is not null)
            {
                RunErrorCase(method, expectedErrorCode, inputData, hasData, needsBindingsOverload, bindings, maxDepth, timeLimitMs);
                this.output.WriteLine($"Got expected error: {expectedErrorCode}");
            }
            else if (expectsUndefined)
            {
                RunUndefinedCase(method, inputData, hasData, needsBindingsOverload, bindings, maxDepth, timeLimitMs);
                this.output.WriteLine("Got expected undefined result");
            }
            else if (expectsResult)
            {
                RunResultCase(method, expectedResult, inputData, hasData, needsBindingsOverload, bindings, maxDepth, timeLimitMs);
            }
            else
            {
                this.output.WriteLine("SKIP: No expected outcome (result, undefinedResult, code, or error)");
            }
        }
        finally
        {
            dataDoc?.Dispose();
            bindingsDoc?.Dispose();
        }
    }

    /// <summary>
    /// Discovers all test cases from the JSONata test suite.
    /// Returns [group, caseName] where caseName is "filename" or "filename[index]".
    /// </summary>
    public static List<object[]> GetTestCases()
    {
        string groupsDir = Path.Combine(TestSuiteRoot, "groups");
        if (!Directory.Exists(groupsDir))
        {
            return [];
        }

        var results = new List<object[]>();

        foreach (string groupDir in Directory.GetDirectories(groupsDir).OrderBy(d => d))
        {
            string groupName = Path.GetFileName(groupDir);
            foreach (string caseFile in Directory.GetFiles(groupDir, "*.json").OrderBy(f => f))
            {
                string baseName = Path.GetFileNameWithoutExtension(caseFile);

                byte[] raw;
                try
                {
                    raw = File.ReadAllBytes(caseFile);
                }
                catch
                {
                    continue;
                }

                // Quick check: does it start with '[' (JSON array)?
                int firstNonWhitespace = 0;
                while (firstNonWhitespace < raw.Length && (raw[firstNonWhitespace] == ' ' || raw[firstNonWhitespace] == '\t' || raw[firstNonWhitespace] == '\r' || raw[firstNonWhitespace] == '\n'))
                {
                    firstNonWhitespace++;
                }

                if (firstNonWhitespace < raw.Length && raw[firstNonWhitespace] == (byte)'[')
                {
                    // Count array elements by parsing with System.Text.Json.
                    using var stjDoc = System.Text.Json.JsonDocument.Parse(raw);
                    int count = stjDoc.RootElement.GetArrayLength();
                    for (int i = 0; i < count; i++)
                    {
                        results.Add([groupName, $"{baseName}[{i}]"]);
                    }
                }
                else
                {
                    results.Add([groupName, baseName]);
                }
            }
        }

        return results;
    }

    private static JsonElement InvokeEvaluate(MethodInfo method, JsonElement data, JsonWorkspace workspace)
    {
        object?[] args = [data, workspace];
        object? result = method.Invoke(null, args);
        return result is JsonElement el ? el : default;
    }

    private static JsonElement InvokeEvaluateWithBindings(
        MethodInfo method,
        JsonElement data,
        JsonWorkspace workspace,
        Dictionary<string, JsonElement>? bindings,
        int maxDepth,
        int timeLimitMs)
    {
        object?[] args = [data, workspace, (IReadOnlyDictionary<string, JsonElement>?)bindings, maxDepth, timeLimitMs];
        object? result = method.Invoke(null, args);
        return result is JsonElement el ? el : default;
    }

    private void RunErrorCase(
        MethodInfo method, string expectedCode, JsonElement inputData, bool hasData,
        bool useBindingsOverload, Dictionary<string, JsonElement>? bindings, int maxDepth, int timeLimitMs)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Exception? caught = null;
        try
        {
            var task = useBindingsOverload
                ? Task.Run(() => InvokeEvaluateWithBindings(method, hasData ? inputData : default, workspace, bindings, maxDepth, timeLimitMs))
                : Task.Run(() => InvokeEvaluate(method, hasData ? inputData : default, workspace));
            if (!task.Wait(TestTimeout))
            {
                Assert.Fail($"Evaluation timed out after {TestTimeout.TotalSeconds}s");
            }
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

        Assert.NotNull(caught);

        if (caught is JsonataException jex)
        {
            Assert.Equal(expectedCode, jex.Code);
        }
    }

    private void RunUndefinedCase(
        MethodInfo method, JsonElement inputData, bool hasData,
        bool useBindingsOverload, Dictionary<string, JsonElement>? bindings, int maxDepth, int timeLimitMs)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        var task = useBindingsOverload
            ? Task.Run(() => InvokeEvaluateWithBindings(method, hasData ? inputData : default, workspace, bindings, maxDepth, timeLimitMs))
            : Task.Run(() => InvokeEvaluate(method, hasData ? inputData : default, workspace));
        if (!task.Wait(TestTimeout))
        {
            Assert.Fail($"Evaluation timed out after {TestTimeout.TotalSeconds}s");
        }

        Assert.Equal(JsonValueKind.Undefined, task.Result.ValueKind);
    }

    private void RunResultCase(
        MethodInfo method, JsonElement expectedResult, JsonElement inputData, bool hasData,
        bool useBindingsOverload, Dictionary<string, JsonElement>? bindings, int maxDepth, int timeLimitMs)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        var task = useBindingsOverload
            ? Task.Run(() => InvokeEvaluateWithBindings(method, hasData ? inputData : default, workspace, bindings, maxDepth, timeLimitMs))
            : Task.Run(() => InvokeEvaluate(method, hasData ? inputData : default, workspace));
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

        if (expected.ValueKind == JsonValueKind.String && actual.ValueKind == JsonValueKind.String)
        {
            Assert.Equal(expected.GetString(), actual.GetString());
            return;
        }

        Assert.Equal(expected.GetRawText(), actual.GetRawText());
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
}
