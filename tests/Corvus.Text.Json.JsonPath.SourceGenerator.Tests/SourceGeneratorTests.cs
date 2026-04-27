// <copyright file="SourceGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Corvus.Text.Json.JsonPath.SourceGenerator.Tests;

/// <summary>
/// Integration tests for the JSONPath source generator.
/// </summary>
public class SourceGeneratorTests
{
    private static readonly string BookStoreJson = """
        {
            "store": {
                "book": [
                    { "category": "reference", "author": "Nigel Rees", "title": "Sayings of the Century", "price": 8.95 },
                    { "category": "fiction", "author": "Evelyn Waugh", "title": "Sword of Honour", "price": 12.99 },
                    { "category": "fiction", "author": "Herman Melville", "title": "Moby Dick", "isbn": "0-553-21311-3", "price": 8.99 },
                    { "category": "fiction", "author": "J. R. R. Tolkien", "title": "The Lord of the Rings", "isbn": "0-395-19395-8", "price": 22.99 }
                ],
                "bicycle": { "color": "red", "price": 19.95 }
            }
        }
        """;

    private readonly ITestOutputHelper output;

    public SourceGeneratorTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    [Trait("category", "sourcegen")]
    public void BookAuthors_ReturnsAllAuthors()
    {
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(BookStoreJson));
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonElement result = BookAuthors.Evaluate(data, workspace);

        string resultJson = result.GetRawText();
        this.output.WriteLine($"Result: {resultJson}");

        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(resultJson);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(4, doc.RootElement.GetArrayLength());
        Assert.Equal("Nigel Rees", doc.RootElement[0].GetString());
        Assert.Equal("Evelyn Waugh", doc.RootElement[1].GetString());
        Assert.Equal("Herman Melville", doc.RootElement[2].GetString());
        Assert.Equal("J. R. R. Tolkien", doc.RootElement[3].GetString());
    }

    [Fact]
    [Trait("category", "sourcegen")]
    public void CheapBooks_ReturnsBooksUnderTen()
    {
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(BookStoreJson));
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonElement result = CheapBooks.Evaluate(data, workspace);

        string resultJson = result.GetRawText();
        this.output.WriteLine($"Result: {resultJson}");

        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(resultJson);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal("Sayings of the Century", doc.RootElement[0].GetString());
        Assert.Equal("Moby Dick", doc.RootElement[1].GetString());
    }

    [Fact]
    [Trait("category", "sourcegen")]
    public void AllPrices_ReturnsAllPricesViaRecursiveDescent()
    {
        JsonElement data = JsonElement.ParseValue(Encoding.UTF8.GetBytes(BookStoreJson));
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonElement result = AllPrices.Evaluate(data, workspace);

        string resultJson = result.GetRawText();
        this.output.WriteLine($"Result: {resultJson}");

        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(resultJson);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, doc.RootElement.ValueKind);

        // Should find 5 prices: 4 books + 1 bicycle
        Assert.Equal(5, doc.RootElement.GetArrayLength());
    }
}
