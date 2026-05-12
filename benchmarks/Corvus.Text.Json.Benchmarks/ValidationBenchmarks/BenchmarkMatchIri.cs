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
public class BenchmarkMatchIri
{
    private System.Text.Json.JsonDocument? _cjsIri;
    private JsonIri _cjsIriElement;
    private ParsedJsonDocument<JsonElement>? _ctjIri;

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _cjsIri?.Dispose();
        _ctjIri?.Dispose();
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _cjsIri = System.Text.Json.JsonDocument.Parse("\"http://ƒøø.ßår/?∂éœ=πîx#πîüx\"");
        _ctjIri = ParsedJsonDocument<JsonElement>.Parse("\"http://ƒøø.ßår/?∂éœ=πîx#πîüx\"");
        _cjsIriElement = JsonIri.FromJson(_cjsIri.RootElement);
    }

    [Benchmark(Baseline = true)]
    public bool ValidateCorvusJsonSchema()
    {
        ValidationContext result = ValidateWithoutCoreType.TypeIri(_cjsIriElement, ValidationContext.ValidContext, ValidationLevel.Flag);
        return result.IsValid;
    }

    [Benchmark]
    public bool ValidateCorvusTextJson()
    {
        // This is normally all wrapped up in codegen; you don't have to do this yourself.
        var context = JsonSchemaContext.BeginContext(_ctjIri!, 0, false, false);

        try
        {
            return JsonSchemaEvaluation.MatchIri("http://ƒøø.ßår/?∂éœ=πîx#πîüx"u8, "dummy"u8, ref context);
        }
        finally
        {
            context.Dispose();
        }
    }
}