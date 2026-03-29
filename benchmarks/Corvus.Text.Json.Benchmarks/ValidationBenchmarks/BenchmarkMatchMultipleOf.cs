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
public class BenchmarkMatchMultipleOf
{
    private System.Text.Json.JsonDocument? _cjsMultipleOf;
    private JsonNumber _cjsMultipleOfElement;
    private ParsedJsonDocument<JsonElement>? _ctjMultipleOf;
    private IJsonElement? _ctjMultipleOfElement;
    private BinaryJsonNumber _bjn = new(32);
    private ValidationLevel level = ValidationLevel.Flag;

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _cjsMultipleOf?.Dispose();
        _ctjMultipleOf?.Dispose();
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _cjsMultipleOf = System.Text.Json.JsonDocument.Parse("128");
        _ctjMultipleOf = ParsedJsonDocument<JsonElement>.Parse("128");
        _ctjMultipleOfElement = _ctjMultipleOf.RootElement;
        _cjsMultipleOfElement = JsonNumber.FromJson(_cjsMultipleOf.RootElement);
    }

    [Benchmark(Baseline = true)]
    public bool ValidateCorvusJsonSchema()
    {
        ValidationContext result = ValidationContext.ValidContext;
        if (_cjsMultipleOfElement.HasJsonElementBacking ? BinaryJsonNumber.IsMultipleOf(_cjsMultipleOfElement.AsJsonElement, _bjn) : _cjsMultipleOfElement.AsBinaryJsonNumber.IsMultipleOf(_bjn))
        {
            if (level == ValidationLevel.Verbose)
            {
                result = result.WithResult(true, validationLocationReducedPathModifier: new Corvus.Json.JsonReference("multipleOf"), "Example message");
            }
            else
            {
                if (level >= ValidationLevel.Detailed)
                {
                    result = result.WithResult(true, validationLocationReducedPathModifier: new Corvus.Json.JsonReference("multipleOf"), "Example message");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    result = result.WithResult(true, validationLocationReducedPathModifier: new Corvus.Json.JsonReference("multipleOf"), "Example message");
                }
                else
                {
                    result = ValidationContext.InvalidContext;
                }
            }
        }
        return result.IsValid;
    }

    [Benchmark]
    public bool ValidateCorvusTextJson()
    {
        // This is normally all wrapped up in codegen; you don't have to do this yourself.
        var context = JsonSchemaContext.BeginContext(_ctjMultipleOf!, 0, false, false);

        try
        {
            ReadOnlyMemory<byte> raw = _ctjMultipleOfElement!.ParentDocument.GetRawSimpleValue(_ctjMultipleOfElement.ParentDocumentIndex);
            JsonElementHelpers.ParseNumber(raw.Span, out bool isNegative, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);
            return JsonSchemaEvaluation.MatchMultipleOf(integral, fractional, exponent, 32, 0, "32", "dummy"u8, ref context);
        }
        finally
        {
            context.Dispose();
        }
    }
}