// <copyright file="CustomFunctionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Xunit;

namespace Corvus.Text.Json.JsonPath.Tests;

/// <summary>
/// Tests for the custom function extension mechanism in <see cref="JsonPathEvaluator"/>.
/// </summary>
public class CustomFunctionTests
{
    // ── ValueType → ValueType: ceil(value) ──────────────────────────

    [Fact]
    public void CeilFunction_ReturnsRoundedUp()
    {
        JsonElement data = JsonElement.ParseValue("""[{"price": 9.3}, {"price": 10.0}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["ceil"] = new CeilFunction(),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?ceil(@.price) == 10]", data);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void CeilFunction_NoMatch()
    {
        JsonElement data = JsonElement.ParseValue("""[{"price": 10.5}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["ceil"] = new CeilFunction(),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?ceil(@.price) == 10]", data);

        // ceil(10.5) == 11, not 10 → no match
        Assert.Equal(0, result.Count);
    }

    // ── LogicalType return: is_even(value) → logical ────────────────

    [Fact]
    public void IsEvenFunction_FiltersByPredicate()
    {
        JsonElement data = JsonElement.ParseValue("[1, 2, 3, 4, 5, 6]"u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["is_even"] = new IsEvenFunction(),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?is_even(@)]", data);

        Assert.Equal(3, result.Count);
        Assert.Equal(2, result[0].GetInt32());
        Assert.Equal(4, result[1].GetInt32());
        Assert.Equal(6, result[2].GetInt32());
    }

    // ── Multi-arg function: add(value, value) → value ───────────────

    [Fact]
    public void AddFunction_SumsTwoValues()
    {
        JsonElement data = JsonElement.ParseValue("""[{"a": 1, "b": 2}, {"a": 3, "b": 4}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["add"] = new AddFunction(),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?add(@.a, @.b) == 3]", data);

        Assert.Equal(1, result.Count);
        Assert.Equal(1, result[0].GetProperty("a").GetInt32());
    }

    // ── NodesType arg: node_count(nodes) → value ────────────────────

    [Fact]
    public void NodeCountFunction_CountsNodes()
    {
        JsonElement data = JsonElement.ParseValue(
            """[{"items": [1, 2, 3]}, {"items": [1]}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["node_count"] = new NodeCountFunction(),
        });

        // node_count(@.items[*]) should return 3 for first element, 1 for second
        using JsonPathResult result = evaluator.QueryNodes("$[?node_count(@.items[*]) == 3]", data);

        Assert.Equal(1, result.Count);
    }

    [Fact]
    public void SumFunction_SumsNodeValues()
    {
        JsonElement data = JsonElement.ParseValue(
            """[{"prices": [10, 20, 30]}, {"prices": [1, 2]}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["sum"] = new SumFunction(),
        });

        // sum(@.prices[*]) == 60 matches the first element
        using JsonPathResult result = evaluator.QueryNodes("$[?sum(@.prices[*]) == 60]", data);

        Assert.Equal(1, result.Count);
    }

    // ── Reserved name rejection ─────────────────────────────────────

    [Theory]
    [InlineData("length")]
    [InlineData("count")]
    [InlineData("value")]
    [InlineData("match")]
    [InlineData("search")]
    public void CannotOverrideBuiltInFunction(string name)
    {
        Assert.Throws<ArgumentException>(() =>
            new JsonPathEvaluator(new Dictionary<string, IJsonPathFunction>
            {
                [name] = new CeilFunction(),
            }));
    }

    // ── Unknown function with no registry throws ────────────────────

    [Fact]
    public void UnknownFunctionWithNoRegistry_Throws()
    {
        JsonElement data = JsonElement.ParseValue("[]"u8);

        Assert.Throws<JsonPathException>(() =>
            JsonPathEvaluator.Default.QueryNodes("$[?ceil(@) == 1]", data));
    }

    // ── Caching works with custom evaluator ─────────────────────────

    [Fact]
    public void CachingWorksWithCustomFunctions()
    {
        JsonElement data1 = JsonElement.ParseValue("[1, 2, 3]"u8);
        JsonElement data2 = JsonElement.ParseValue("[4, 5, 6]"u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["is_even"] = new IsEvenFunction(),
        });

        using JsonPathResult r1 = evaluator.QueryNodes("$[?is_even(@)]", data1);
        using JsonPathResult r2 = evaluator.QueryNodes("$[?is_even(@)]", data2);

        Assert.Equal(1, r1.Count); // only 2
        Assert.Equal(2, r2.Count); // 4 and 6
    }

    // ── Wrong arity throws at parse time ────────────────────────────

    [Fact]
    public void WrongArity_ThrowsAtParseTime()
    {
        JsonElement data = JsonElement.ParseValue("[]"u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["ceil"] = new CeilFunction(),
        });

        Assert.Throws<JsonPathException>(() =>
            evaluator.QueryNodes("$[?ceil(@, @) == 1]", data));
    }

    // ── Wrong arg type throws at parse time ─────────────────────────

    [Fact]
    public void WrongArgType_ThrowsAtParseTime()
    {
        JsonElement data = JsonElement.ParseValue("[]"u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["is_even"] = new IsEvenFunction(),
        });

        // is_even expects ValueType, but @.* is NodesType (non-singular)
        Assert.Throws<JsonPathException>(() =>
            evaluator.QueryNodes("$[?is_even(@.*)]", data));
    }

    // ── Test helpers ─────────────────────────────────────────────────

    private sealed class CeilFunction : IJsonPathFunction
    {
        private static readonly JsonPathFunctionType[] ParamTypes = [JsonPathFunctionType.ValueType];

        public JsonPathFunctionType ReturnType => JsonPathFunctionType.ValueType;

        public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => ParamTypes;

        public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> arguments, JsonWorkspace workspace)
        {
            JsonElement val = arguments[0].Value;
            if (val.ValueKind != JsonValueKind.Number)
            {
                return JsonPathFunctionResult.Nothing;
            }

            double d = val.GetDouble();
            int ceil = (int)Math.Ceiling(d);
            return JsonPathFunctionResult.FromValue(
                JsonPathCodeGenHelpers.IntToElement(ceil, workspace));
        }
    }

    private sealed class IsEvenFunction : IJsonPathFunction
    {
        private static readonly JsonPathFunctionType[] ParamTypes = [JsonPathFunctionType.ValueType];

        public JsonPathFunctionType ReturnType => JsonPathFunctionType.LogicalType;

        public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => ParamTypes;

        public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> arguments, JsonWorkspace workspace)
        {
            JsonElement val = arguments[0].Value;
            if (val.ValueKind != JsonValueKind.Number || !val.TryGetInt32(out int n))
            {
                return JsonPathFunctionResult.FromLogical(false);
            }

            return JsonPathFunctionResult.FromLogical(n % 2 == 0);
        }
    }

    private sealed class AddFunction : IJsonPathFunction
    {
        private static readonly JsonPathFunctionType[] ParamTypes =
            [JsonPathFunctionType.ValueType, JsonPathFunctionType.ValueType];

        public JsonPathFunctionType ReturnType => JsonPathFunctionType.ValueType;

        public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => ParamTypes;

        public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> arguments, JsonWorkspace workspace)
        {
            JsonElement a = arguments[0].Value;
            JsonElement b = arguments[1].Value;
            if (a.ValueKind != JsonValueKind.Number || b.ValueKind != JsonValueKind.Number)
            {
                return JsonPathFunctionResult.Nothing;
            }

            double sum = a.GetDouble() + b.GetDouble();
            return JsonPathFunctionResult.FromValue(
                JsonPathCodeGenHelpers.DoubleToElement(sum, workspace));
        }
    }

    private sealed class NodeCountFunction : IJsonPathFunction
    {
        private static readonly JsonPathFunctionType[] ParamTypes = [JsonPathFunctionType.NodesType];

        public JsonPathFunctionType ReturnType => JsonPathFunctionType.ValueType;

        public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => ParamTypes;

        public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> arguments, JsonWorkspace workspace)
        {
            int count = arguments[0].NodeCount;
            return JsonPathFunctionResult.FromValue(
                JsonPathCodeGenHelpers.IntToElement(count, workspace));
        }
    }

    private sealed class SumFunction : IJsonPathFunction
    {
        private static readonly JsonPathFunctionType[] ParamTypes = [JsonPathFunctionType.NodesType];

        public JsonPathFunctionType ReturnType => JsonPathFunctionType.ValueType;

        public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => ParamTypes;

        public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> arguments, JsonWorkspace workspace)
        {
            ReadOnlySpan<JsonElement> nodes = arguments[0].Nodes;
            double total = 0;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].ValueKind == JsonValueKind.Number)
                {
                    total += nodes[i].GetDouble();
                }
            }

            return JsonPathFunctionResult.FromValue(
                JsonPathCodeGenHelpers.DoubleToElement(total, workspace));
        }
    }
}
