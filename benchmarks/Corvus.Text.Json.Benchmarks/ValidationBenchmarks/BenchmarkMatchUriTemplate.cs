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
public class BenchmarkMatchUriTemplate
{
    private System.Text.Json.JsonDocument? _cjsUriTemplate;
    private JsonUriTemplate _cjsUriTemplateElement;
    private ParsedJsonDocument<JsonElement>? _ctjUriTemplate;

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _cjsUriTemplate?.Dispose();
        _ctjUriTemplate?.Dispose();
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _cjsUriTemplate = System.Text.Json.JsonDocument.Parse("\"http://foo.bar/{?var}/?q=Test%20URL-encoded%20stuff\"");
        _ctjUriTemplate = ParsedJsonDocument<JsonElement>.Parse("\"http://foo.bar/{?var}/?q=Test%20URL-encoded%20stuff\"");
        _cjsUriTemplateElement = JsonUriTemplate.FromJson(_cjsUriTemplate.RootElement);
    }

    [Benchmark(Baseline = true)]
    public bool ValidateCorvusJsonSchema()
    {
        ValidationContext result = ValidateWithoutCoreType.TypeUriTemplate(_cjsUriTemplateElement, ValidationContext.ValidContext, ValidationLevel.Flag);
        return result.IsValid;
    }

    [Benchmark]
    public bool ValidateCorvusTextJson()
    {
        // This is normally all wrapped up in codegen; you don't have to do this yourself.
        var context = JsonSchemaContext.BeginContext(_ctjUriTemplate!, 0, false, false);

        try
        {
            return JsonSchemaEvaluation.MatchUriTemplate("http://foo.bar/{?var}/?q=Test%20URL-encoded%20stuff"u8, "dummy"u8, ref context);
        }
        finally
        {
            context.Dispose();
        }
    }
}