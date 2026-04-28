// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.JsonPath;
using System.Buffers;
using JsonPath.Expressions;

// Load the bookstore document
using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
    """
    {
      "store": {
        "book": [
          {"category": "reference", "author": "Sandi Toksvig", "title": "Between the Stops", "price": 8.95},
          {"category": "fiction", "author": "Evelyn Waugh", "title": "Sword of Honour", "price": 12.99},
          {"category": "fiction", "author": "Jane Austen", "title": "Pride and Prejudice", "price": 8.99},
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
JsonElement[] buf = ArrayPool<JsonElement>.Shared.Rent(16);
try
{
    using (JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.store.book[*].title", data, buf.AsSpan()))
    {
        Console.WriteLine($"    Found {result.Count} titles:");
        foreach (JsonElement node in result.Nodes)
        {
            Console.WriteLine($"    - {node}");
        }
    }
}
finally
{
    ArrayPool<JsonElement>.Shared.Return(buf);
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

Console.WriteLine();

// ── 12. Custom function — ceil (ValueType → ValueType) ─────────────────────
Console.WriteLine("12. Custom function — ceil()");

var ceilFunc = new CeilFunction();
var evaluator = new JsonPathEvaluator(
    new Dictionary<string, IJsonPathFunction> { ["ceil"] = ceilFunc });

Console.WriteLine($"    $.store.book[?ceil(@.price)==9].title = {evaluator.Query("$.store.book[?ceil(@.price)==9].title", data)}");
Console.WriteLine();

// ── 13. Custom function — is_fiction (ValueType → LogicalType) ──────────────
Console.WriteLine("13. Custom function — is_fiction()");

var isFictionFunc = new IsFictionFunction();
var evaluator2 = new JsonPathEvaluator(
    new Dictionary<string, IJsonPathFunction>
    {
        ["is_fiction"] = isFictionFunc,
    });

Console.WriteLine($"    $.store.book[?is_fiction(@.category)].title = {evaluator2.Query("$.store.book[?is_fiction(@.category)].title", data)}");
Console.WriteLine();

// ── 14. Custom function — cheapest (NodesType → ValueType) ──────────────────
Console.WriteLine("14. Custom function — cheapest()");

var cheapestFunc = new CheapestFunction();
var evaluator3 = new JsonPathEvaluator(
    new Dictionary<string, IJsonPathFunction> { ["cheapest"] = cheapestFunc });

Console.WriteLine($"    $.store.book[?@.price==cheapest($.store.book[*].price)].title = {evaluator3.Query("$.store.book[?@.price==cheapest($.store.book[*].price)].title", data)}");

// ────────────────────────────────────────────────────────────────────────────
// Custom function implementations
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A custom JSONPath function that returns the ceiling of a numeric value.
/// Signature: ceil(ValueType) → ValueType.
/// </summary>
sealed class CeilFunction : IJsonPathFunction
{
    private static readonly JsonPathFunctionType[] ParamTypes = [JsonPathFunctionType.ValueType];

    public JsonPathFunctionType ReturnType => JsonPathFunctionType.ValueType;

    public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => ParamTypes;

    public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> arguments)
    {
        JsonElement value = arguments[0].Value;
        if (value.ValueKind != JsonValueKind.Number)
        {
            return JsonPathFunctionResult.Nothing;
        }

        int ceiled = (int)Math.Ceiling(value.GetDouble());
        return JsonPathFunctionResult.FromValue(
            JsonPathCodeGenHelpers.IntToElement(ceiled));
    }
}

/// <summary>
/// A custom JSONPath function that tests whether a string equals "fiction".
/// Signature: is_fiction(ValueType) → LogicalType.
/// </summary>
sealed class IsFictionFunction : IJsonPathFunction
{
    private static readonly JsonPathFunctionType[] ParamTypes = [JsonPathFunctionType.ValueType];

    public JsonPathFunctionType ReturnType => JsonPathFunctionType.LogicalType;

    public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => ParamTypes;

    public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> arguments)
    {
        JsonElement value = arguments[0].Value;
        if (value.ValueKind != JsonValueKind.String)
        {
            return JsonPathFunctionResult.FromLogical(false);
        }

        return JsonPathFunctionResult.FromLogical(
            value.ValueEquals("fiction"u8));
    }
}

/// <summary>
/// A custom JSONPath function that returns the minimum numeric value from a node list.
/// Signature: cheapest(NodesType) → ValueType.
/// </summary>
sealed class CheapestFunction : IJsonPathFunction
{
    private static readonly JsonPathFunctionType[] ParamTypes = [JsonPathFunctionType.NodesType];

    public JsonPathFunctionType ReturnType => JsonPathFunctionType.ValueType;

    public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => ParamTypes;

    public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> arguments)
    {
        ReadOnlySpan<JsonElement> nodes = arguments[0].Nodes;
        if (nodes.Length == 0)
        {
            return JsonPathFunctionResult.Nothing;
        }

        double min = double.MaxValue;
        JsonElement minElement = default;
        foreach (JsonElement node in nodes)
        {
            if (node.ValueKind == JsonValueKind.Number)
            {
                double v = node.GetDouble();
                if (v < min)
                {
                    min = v;
                    minElement = node;
                }
            }
        }

        return min < double.MaxValue
            ? JsonPathFunctionResult.FromValue(minElement)
            : JsonPathFunctionResult.Nothing;
    }
}
