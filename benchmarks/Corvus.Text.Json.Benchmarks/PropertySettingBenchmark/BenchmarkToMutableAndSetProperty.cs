// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;

namespace PropertySettingBenchmarks;

/// <summary>
/// Construct elements from a JSON element.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkToMutableAndSetProperty
{
    // Create a JSON document to work with.
    private const string Json = """
            {
                "age": 51,
                "name": {
                    "firstName": "Michael",
                    "lastName": "Adams",
                    "otherNames": ["Francis", "James"]
                },
                "competedInYears": [2012, 2016, 2024]
            }
            """;

    [Benchmark]
    public string SetPropertyCorvusJsonSchema()
    {
        using var corvusParsedValue = Corvus.Json.ParsedValue<Corvus.Json.JsonObject>.Parse(Json);
        if (!corvusParsedValue.Instance.TryGetProperty("name", out Corvus.Json.JsonAny nameValue))
        {
            throw new InvalidOperationException();
        }

        Corvus.Json.JsonObject result = nameValue.AsObject.SetProperty("firstName", (Corvus.Json.JsonString)"Matthew");
        return result.ToString();
    }

    [Benchmark]
    public string SetPropertyCorvusTextJson()
    {
        using var corvusDocument = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(Json);
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create(1);
        using Corvus.Text.Json.JsonDocumentBuilder<Corvus.Text.Json.JsonElement.Mutable> nameValueDoc = corvusDocument!.RootElement.GetProperty("name").CreateBuilder(workspace);
        nameValueDoc.RootElement.SetProperty("firstName"u8, "Matthew"u8);
        return nameValueDoc.RootElement.ToString();
    }

    [Benchmark(Baseline = true)]
    public string SetPropertyJsonObjectDirect()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(Json);
        JsonObject nameValue = node!["name"]?.AsObject() ?? throw new InvalidOperationException();
        nameValue["firstName"] = "Matthew";
        return nameValue.ToJsonString();
    }

    [Benchmark]
    public string SetPropertyJsonObjectFromJsonElement()
    {
        using var document = System.Text.Json.JsonDocument.Parse(Json);
        System.Text.Json.Nodes.JsonObject nameValue = System.Text.Json.JsonSerializer.SerializeToNode(document.RootElement.GetProperty("name"))?.AsObject() ?? throw new InvalidOperationException();
        nameValue["firstName"] = "Matthew";
        return nameValue.ToJsonString();
    }
}