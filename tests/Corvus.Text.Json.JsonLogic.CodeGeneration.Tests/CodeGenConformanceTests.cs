// <copyright file="CodeGenConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Corvus.Text.Json.JsonLogic.CodeGeneration.Tests;

/// <summary>
/// Runs every JsonLogic conformance test case through the code generator pipeline:
/// generate C# → dynamically compile → invoke → compare result against expected.
/// </summary>
public class CodeGenConformanceTests : IClassFixture<CodeGenConformanceFixture>
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private readonly CodeGenConformanceFixture fixture;
    private readonly ITestOutputHelper output;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeGenConformanceTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared compilation fixture.</param>
    /// <param name="output">The xUnit test output helper.</param>
    public CodeGenConformanceTests(CodeGenConformanceFixture fixture, ITestOutputHelper output)
    {
        this.fixture = fixture;
        this.output = output;
    }

    /// <summary>
    /// Runs a single conformance test from the official JsonLogic test suite through code generation.
    /// </summary>
    /// <param name="index">The test index.</param>
    /// <param name="rule">The JsonLogic rule as a JSON string.</param>
    /// <param name="data">The data as a JSON string.</param>
    /// <param name="expected">The expected result as a JSON string.</param>
    [Theory]
    [Trait("category", "codegen-conformance")]
    [MemberData(nameof(GetTestCases))]
    public void RunTestCase(int index, string rule, string data, string expected)
    {
        this.output.WriteLine($"Index:    {index}");
        this.output.WriteLine($"Rule:     {rule}");
        this.output.WriteLine($"Data:     {data}");
        this.output.WriteLine($"Expected: {expected}");

        // Dynamically compile the rule (cached per unique rule string).
        CompiledRule compiled = this.fixture.GetOrCompile(rule);

        if (compiled.GeneratedCode is not null)
        {
            this.output.WriteLine($"Generated code:\n{compiled.GeneratedCode}");
        }

        if (compiled.Method is null)
        {
            Assert.Fail($"Compilation failed: {compiled.Error}");
            return;
        }

        // Parse input data.
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(data);
        using ParsedJsonDocument<JsonElement>? dataDoc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8);
        JsonElement dataElement = dataDoc.RootElement;

        // Execute with timeout.
        JsonElement result;
        using JsonWorkspace workspace = JsonWorkspace.Create();
        try
        {
            var task = Task.Run(() =>
            {
                object?[] args = [dataElement, workspace];
                object? ret = compiled.Method.Invoke(null, args);
                return ret is JsonElement el ? el : default;
            });

            if (!task.Wait(TestTimeout))
            {
                Assert.Fail($"Evaluation timed out after {TestTimeout.TotalSeconds}s");
                return;
            }

            result = task.Result;
        }
        catch (AggregateException ae) when (ae.InnerException is TargetInvocationException tie)
        {
            // The generated code threw during evaluation — this is a test failure.
            Assert.Fail($"Evaluation threw {tie.InnerException?.GetType().Name}: {tie.InnerException?.Message}");
            return;
        }
        catch (AggregateException ae) when (ae.InnerException is not null)
        {
            Assert.Fail($"Evaluation threw {ae.InnerException.GetType().Name}: {ae.InnerException.Message}");
            return;
        }

        // Compare result.
        string expectedText = NormalizeJson(expected);
        string actualText = NormalizeJson(GetRawText(result));

        this.output.WriteLine($"Actual:   {actualText}");

        Assert.Equal(expectedText, actualText);
    }

    /// <summary>
    /// Gets the test cases from the embedded tests.json resource.
    /// </summary>
    /// <returns>An enumerable of test case data: [index, rule, data, expected].</returns>
    public static IEnumerable<object[]> GetTestCases()
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Corvus.Text.Json.JsonLogic.CodeGeneration.Tests.tests.json")!;

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

    private static string GetRawText(JsonElement element)
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
            NormalizeElement(doc.RootElement, writer);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void NormalizeElement(System.Text.Json.JsonElement element, System.Text.Json.Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Number:
                // Write the number as a double to normalize scientific notation
                // (e.g. 5E-1 → 0.5, 1E1 → 10).
                double d = element.GetDouble();

                // If the value is an integer, write it without a decimal point.
                if (d == Math.Truncate(d) && !double.IsInfinity(d) && Math.Abs(d) < 1e15)
                {
                    writer.WriteNumberValue((long)d);
                }
                else
                {
                    writer.WriteNumberValue(d);
                }

                break;

            case System.Text.Json.JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (System.Text.Json.JsonElement item in element.EnumerateArray())
                {
                    NormalizeElement(item, writer);
                }

                writer.WriteEndArray();
                break;

            case System.Text.Json.JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (System.Text.Json.JsonProperty prop in element.EnumerateObject())
                {
                    writer.WritePropertyName(prop.Name);
                    NormalizeElement(prop.Value, writer);
                }

                writer.WriteEndObject();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }
}
