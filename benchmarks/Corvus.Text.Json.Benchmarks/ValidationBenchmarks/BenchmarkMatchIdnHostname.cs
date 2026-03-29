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
public class BenchmarkMatchIdnHostname
{
    private System.Text.Json.JsonDocument? _cjsIdnHostname;
    private JsonIdnHostname _cjsIdnHostnameElement;
    private ParsedJsonDocument<JsonElement>? _ctjIdnHostname;

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _cjsIdnHostname?.Dispose();
        _ctjIdnHostname?.Dispose();
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _cjsIdnHostname = System.Text.Json.JsonDocument.Parse("\"ƒøø.ßår\"");
        _ctjIdnHostname = ParsedJsonDocument<JsonElement>.Parse("\"ƒøø.ßår\"");
        _cjsIdnHostnameElement = JsonIdnHostname.FromJson(_cjsIdnHostname.RootElement);
    }

    [Benchmark(Baseline = true)]
    public bool ValidateCorvusJsonSchema()
    {
        ValidationContext result = ValidateWithoutCoreType.TypeIdnHostName(_cjsIdnHostnameElement, ValidationContext.ValidContext, ValidationLevel.Flag);
        return result.IsValid;
    }

    [Benchmark]
    public bool ValidateCorvusTextJson()
    {
        // This is normally all wrapped up in codegen; you don't have to do this yourself.
        var context = JsonSchemaContext.BeginContext(_ctjIdnHostname!, 0, false, false);

        try
        {
            return JsonSchemaEvaluation.MatchIdnHostname("ƒøø.ßår"u8, "dummy"u8, ref context);
        }
        finally
        {
            context.Dispose();
        }
    }
}