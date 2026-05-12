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
public class BenchmarkMatchJsonPointer
{
    private System.Text.Json.JsonDocument? _cjsJsonPointer;
    private JsonPointer _cjsJsonPointerElement;
    private ParsedJsonDocument<JsonElement>? _ctjJsonPointer;

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _cjsJsonPointer?.Dispose();
        _ctjJsonPointer?.Dispose();
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _cjsJsonPointer = System.Text.Json.JsonDocument.Parse("\"/foo/-/bar/~1~0.1~0~1~1\"");
        _ctjJsonPointer = ParsedJsonDocument<JsonElement>.Parse("\"/foo/-/bar/~1~0.1~0~1~1\"");
        _cjsJsonPointerElement = JsonPointer.FromJson(_cjsJsonPointer.RootElement);
    }

    [Benchmark(Baseline = true)]
    public bool ValidateCorvusJsonSchema()
    {
        ValidationContext result = ValidateWithoutCoreType.TypePointer(_cjsJsonPointerElement, ValidationContext.ValidContext, ValidationLevel.Flag);
        return result.IsValid;
    }

    [Benchmark]
    public bool ValidateCorvusTextJson()
    {
        // This is normally all wrapped up in codegen; you don't have to do this yourself.
        // (This is also why we just the direct string representation of the pointer, rather than the public
        // get methods.
        var context = JsonSchemaContext.BeginContext(_ctjJsonPointer!, 0, false, false);

        try
        {
            return JsonSchemaEvaluation.MatchJsonPointer("/foo/-/bar/~1~0.1~0~1~1"u8, "dummy"u8, ref context);
        }
        finally
        {
            context.Dispose();
        }
    }

    private static bool DummyPathProvider(Span<byte> buffer, out int written)
    { written = 0; return true; }
}