// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.JsonPath;
using JsonPath.Expressions;

// Load the bookstore document
using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
    """
    {
      "store": {
        "book": [
          {"category": "reference", "author": "Nigel Rees", "title": "Sayings of the Century", "price": 8.95},
          {"category": "fiction", "author": "Evelyn Waugh", "title": "Sword of Honour", "price": 12.99},
          {"category": "fiction", "author": "Herman Melville", "title": "Moby Dick", "price": 8.99},
          {"category": "fiction", "author": "J. R. R. Tolkien", "title": "The Lord of the Rings", "price": 22.99}
        ],
        "bicycle": {"color": "red", "price": 399.99}
      }
    }
    """);

JsonElement data = doc.RootElement;

// ── 1. Property access ──────────────────────────────────────────────────────
Console.WriteLine("1. Property access");
Console.WriteLine($"   $.store.bicycle.color = {JsonPathEvaluator.Default.Query("$.store.bicycle.color", data)}");
Console.WriteLine();

// ── 2. Wildcard ─────────────────────────────────────────────────────────────
Console.WriteLine("2. Wildcard — all book authors");
Console.WriteLine($"   $.store.book[*].author = {JsonPathEvaluator.Default.Query("$.store.book[*].author", data)}");
Console.WriteLine();

// ── 3. Recursive descent ────────────────────────────────────────────────────
Console.WriteLine("3. Recursive descent — all authors at any depth");
Console.WriteLine($"   $..author = {JsonPathEvaluator.Default.Query("$..author", data)}");
Console.WriteLine();

// ── 4. Index access ─────────────────────────────────────────────────────────
Console.WriteLine("4. Index access");
Console.WriteLine($"   $.store.book[0].title = {JsonPathEvaluator.Default.Query("$.store.book[0].title", data)}");
Console.WriteLine($"   $.store.book[-1].title = {JsonPathEvaluator.Default.Query("$.store.book[-1].title", data)}");
Console.WriteLine();

// ── 5. Array slicing ────────────────────────────────────────────────────────
Console.WriteLine("5. Array slicing — first two books");
Console.WriteLine($"   $.store.book[0:2].title = {JsonPathEvaluator.Default.Query("$.store.book[0:2].title", data)}");
Console.WriteLine();

// ── 6. Filter expressions ───────────────────────────────────────────────────
Console.WriteLine("6. Filter — books cheaper than 10");
Console.WriteLine($"   $.store.book[?@.price<10].title = {JsonPathEvaluator.Default.Query("$.store.book[?@.price<10].title", data)}");
Console.WriteLine();

// ── 7. Filter with logical operators ────────────────────────────────────────
Console.WriteLine("7. Filter with logical operators — fiction books under 10");
Console.WriteLine($"   $.store.book[?@.price<10 && @.category=='fiction'].title = {JsonPathEvaluator.Default.Query("$.store.book[?@.price<10 && @.category=='fiction'].title", data)}");
Console.WriteLine();

// ── 8. Filter with function extension ───────────────────────────────────────
Console.WriteLine("8. Filter function — books with long titles");
Console.WriteLine($"   $.store.book[?length(@.title)>15].title = {JsonPathEvaluator.Default.Query("$.store.book[?length(@.title)>15].title", data)}");
Console.WriteLine();

// ── 9. All prices (recursive descent) ───────────────────────────────────────
Console.WriteLine("9. Recursive descent — all prices");
Console.WriteLine($"   $..price = {JsonPathEvaluator.Default.Query("$..price", data)}");
Console.WriteLine();

// ── 10. Zero-allocation QueryNodes ──────────────────────────────────────────
Console.WriteLine("10. Zero-allocation QueryNodes");
JsonElement[] buf = new JsonElement[16];
using (JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.store.book[*].title", data, buf.AsSpan()))
{
    Console.WriteLine($"    Found {result.Count} titles:");
    foreach (JsonElement node in result.Nodes)
    {
        Console.WriteLine($"    - {node}");
    }
}

Console.WriteLine();

// ── 11. Source-generated expression ─────────────────────────────────────────
Console.WriteLine("11. Source-generated expression ($..author)");
using (JsonPathResult sgResult = AllAuthors.QueryNodes(data))
{
    Console.WriteLine($"    Found {sgResult.Count} authors:");
    foreach (JsonElement node in sgResult.Nodes)
    {
        Console.WriteLine($"    - {node}");
    }
}
