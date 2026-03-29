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
public class BenchmarkMatchUri
{
    private System.Text.Json.JsonDocument? _cjsUri;
    private JsonUri _cjsUriElement;
    private ParsedJsonDocument<JsonElement>? _ctjUri;

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _cjsUri?.Dispose();
        _ctjUri?.Dispose();
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _cjsUri = System.Text.Json.JsonDocument.Parse("\"http://foo.bar/?q=Test%20URL-encoded%20stuff\"");
        _ctjUri = ParsedJsonDocument<JsonElement>.Parse("\"http://foo.bar/?q=Test%20URL-encoded%20stuff\"");
        _cjsUriElement = JsonUri.FromJson(_cjsUri.RootElement);
    }

    [Benchmark(Baseline = true)]
    public bool ValidateCorvusJsonSchema()
    {
        ValidationContext result = ValidateWithoutCoreType.TypeUri(_cjsUriElement, ValidationContext.ValidContext, ValidationLevel.Flag);
        return result.IsValid;
    }

    [Benchmark]
    public bool ValidateCorvusTextJson()
    {
        // This is normally all wrapped up in codegen; you don't have to do this yourself.
        var context = JsonSchemaContext.BeginContext(_ctjUri!, 0, false, false);

        try
        {
            return JsonSchemaEvaluation.MatchUri("http://foo.bar/?q=Test%20URL-encoded%20stuff"u8, "dummy"u8, ref context);
        }
        finally
        {
            context.Dispose();
        }
    }
}