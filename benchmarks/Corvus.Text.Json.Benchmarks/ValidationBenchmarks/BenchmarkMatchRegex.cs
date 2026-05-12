// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using BenchmarkDotNet.Attributes;
using Corvus.Json;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace ValidationBenchmarks;

/// <summary>
/// Construct elements from a JSON element.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkMatchRegex
{
    private System.Text.Json.JsonDocument? _cjsRegex;
    private JsonRegex _cjsRegexElement;
    private ParsedJsonDocument<JsonElement>? _ctjRegex;

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _cjsRegex?.Dispose();
        _ctjRegex?.Dispose();
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _cjsRegex = System.Text.Json.JsonDocument.Parse("\"\\\\D+(?<digit>\\\\d+)\\\\D+(?<digit>\\\\d+)?\"");
        _ctjRegex = ParsedJsonDocument<JsonElement>.Parse("\"\\\\D+(?<digit>\\\\d+)\\\\D+(?<digit>\\\\d+)?\"");
        _cjsRegexElement = JsonRegex.FromJson(_cjsRegex.RootElement);
    }

    [Benchmark(Baseline = true)]
    public bool ValidateCorvusJsonSchema()
    {
        ValidationContext result = ValidateWithoutCoreType.TypeRegex(_cjsRegexElement, ValidationContext.ValidContext, ValidationLevel.Flag);
        return result.IsValid;
    }

    [Benchmark]
    public bool ValidateCorvusTextJson()
    {
        // This is normally all wrapped up in codegen; you don't have to do this yourself.
        var context = JsonSchemaContext.BeginContext(_ctjRegex!, 0, false, false);

        try
        {
            return JsonSchemaEvaluation.MatchRegex("\\D+(?<digit>\\d+)\\D+(?<digit>\\d+)?"u8, "dummy"u8, ref context);
        }
        finally
        {
            context.Dispose();
        }
    }
}