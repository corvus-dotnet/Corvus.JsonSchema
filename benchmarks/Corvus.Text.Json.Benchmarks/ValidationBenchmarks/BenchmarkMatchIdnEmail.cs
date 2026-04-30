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
public class BenchmarkMatchIdnEmail
{
    private System.Text.Json.JsonDocument? _cjsEmail;
    private JsonIdnEmail _cjsEmailElement;
    private ParsedJsonDocument<JsonElement>? _ctjEmail;

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _cjsEmail?.Dispose();
        _ctjEmail?.Dispose();
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _cjsEmail = System.Text.Json.JsonDocument.Parse("\"Dörte@Sörensen.example.com\"");
        _ctjEmail = ParsedJsonDocument<JsonElement>.Parse("\"Dörte@Sörensen.example.com\"");
        _cjsEmailElement = JsonIdnEmail.FromJson(_cjsEmail.RootElement);
    }

    [Benchmark(Baseline = true)]
    public bool ValidateCorvusJsonSchema()
    {
        ValidationContext result = ValidateWithoutCoreType.TypeIdnEmail(_cjsEmailElement, ValidationContext.ValidContext, ValidationLevel.Flag);
        return result.IsValid;
    }

    [Benchmark]
    public bool ValidateCorvusTextJson()
    {
        // This is normally all wrapped up in codegen; you don't have to do this yourself.
        var context = JsonSchemaContext.BeginContext(_ctjEmail!, 0, false, false);

        try
        {
            return JsonSchemaEvaluation.MatchIdnEmail("Dörte@Sörensen.example.com"u8, "dummy"u8, ref context);
        }
        finally
        {
            context.Dispose();
        }
    }
}