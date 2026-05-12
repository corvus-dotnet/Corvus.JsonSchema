// <copyright file="CustomFunctionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JsonPath.Tests;

/// <summary>
/// Tests for the custom function extension mechanism in <see cref="JsonPathEvaluator"/>.
/// </summary>
[TestClass]
public class CustomFunctionTests
{
    // ── ValueType → ValueType: ceil(value) ──────────────────────────

    [TestMethod]
    public void CeilFunction_ReturnsRoundedUp()
    {
        JsonElement data = JsonElement.ParseValue("""[{"price": 9.3}, {"price": 10.0}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["ceil"] = new CeilFunction(),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?ceil(@.price) == 10]", data);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void CeilFunction_NoMatch()
    {
        JsonElement data = JsonElement.ParseValue("""[{"price": 10.5}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["ceil"] = new CeilFunction(),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?ceil(@.price) == 10]", data);

        // ceil(10.5) == 11, not 10 → no match
        Assert.AreEqual(0, result.Count);
    }

    // ── LogicalType return: is_even(value) → logical ────────────────

    [TestMethod]
    public void IsEvenFunction_FiltersByPredicate()
    {
        JsonElement data = JsonElement.ParseValue("[1, 2, 3, 4, 5, 6]"u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["is_even"] = new IsEvenFunction(),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?is_even(@)]", data);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(2, result[0].GetInt32());
        Assert.AreEqual(4, result[1].GetInt32());
        Assert.AreEqual(6, result[2].GetInt32());
    }

    // ── Multi-arg function: add(value, value) → value ───────────────

    [TestMethod]
    public void AddFunction_SumsTwoValues()
    {
        JsonElement data = JsonElement.ParseValue("""[{"a": 1, "b": 2}, {"a": 3, "b": 4}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["add"] = new AddFunction(),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?add(@.a, @.b) == 3]", data);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(1, result[0].GetProperty("a").GetInt32());
    }

    // ── NodesType arg: node_count(nodes) → value ────────────────────

    [TestMethod]
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

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
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

        Assert.AreEqual(1, result.Count);
    }

    // ── Reserved name rejection ─────────────────────────────────────

    [TestMethod]
    [DataRow("length")]
    [DataRow("count")]
    [DataRow("value")]
    [DataRow("match")]
    [DataRow("search")]
    public void CannotOverrideBuiltInFunction(string name)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new JsonPathEvaluator(new Dictionary<string, IJsonPathFunction>
            {
                [name] = new CeilFunction(),
            }));
    }

    // ── Unknown function with no registry throws ────────────────────

    [TestMethod]
    public void UnknownFunctionWithNoRegistry_Throws()
    {
        JsonElement data = JsonElement.ParseValue("[]"u8);

        Assert.ThrowsExactly<JsonPathException>(() =>
            JsonPathEvaluator.Default.QueryNodes("$[?ceil(@) == 1]", data));
    }

    // ── Caching works with custom evaluator ─────────────────────────

    [TestMethod]
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

        Assert.AreEqual(1, r1.Count); // only 2
        Assert.AreEqual(2, r2.Count); // 4 and 6
    }

    // ── Wrong arity throws at parse time ────────────────────────────

    [TestMethod]
    public void WrongArity_ThrowsAtParseTime()
    {
        JsonElement data = JsonElement.ParseValue("[]"u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["ceil"] = new CeilFunction(),
        });

        Assert.ThrowsExactly<JsonPathException>(() =>
            evaluator.QueryNodes("$[?ceil(@, @) == 1]", data));
    }

    // ── Wrong arg type throws at parse time ─────────────────────────

    [TestMethod]
    public void WrongArgType_ThrowsAtParseTime()
    {
        JsonElement data = JsonElement.ParseValue("[]"u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["is_even"] = new IsEvenFunction(),
        });

        // is_even expects ValueType, but @.* is NodesType (non-singular)
        Assert.ThrowsExactly<JsonPathException>(() =>
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

    // ═══════════════════════════════════════════════════════════════════
    // Tests for JsonPathFunction factory methods (delegate-based API)
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void Factory_Value_SingleArg()
    {
        // Lines 44-48: JsonPathFunction.Value(Func<JsonElement, JsonWorkspace, JsonElement>)
        JsonElement data = JsonElement.ParseValue("""[{"x": 2.7}, {"x": 3.1}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["floor"] = JsonPathFunction.Value((v, ws) =>
                JsonPathCodeGenHelpers.IntToElement((int)Math.Floor(v.GetDouble()), ws)),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?floor(@.x) == 2]", data);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Factory_Value_TwoArgs()
    {
        // Lines 58-62: JsonPathFunction.Value(Func<JsonElement, JsonElement, JsonWorkspace, JsonElement>)
        JsonElement data = JsonElement.ParseValue("""[{"a": 10, "b": 3}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["modulo"] = JsonPathFunction.Value((a, b, ws) =>
                JsonPathCodeGenHelpers.IntToElement(a.GetInt32() % b.GetInt32(), ws)),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?modulo(@.a, @.b) == 1]", data);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Factory_Logical()
    {
        // Lines 72-76: JsonPathFunction.Logical(Func<JsonElement, bool>)
        JsonElement data = JsonElement.ParseValue("""["cat", "dog", "catfish"]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["starts_with_cat"] = JsonPathFunction.Logical(v =>
                v.ValueKind == JsonValueKind.String && v.GetString()!.StartsWith("cat")),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?starts_with_cat(@)]", data);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void Factory_NodesValue()
    {
        // Lines 91-95: JsonPathFunction.NodesValue(Func<JsonElement[], JsonWorkspace, JsonElement>)
        JsonElement data = JsonElement.ParseValue("""[{"items": [10, 20, 30]}, {"items": [1, 2]}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["sum"] = JsonPathFunction.NodesValue((nodes, ws) =>
            {
                double total = 0;
                foreach (JsonElement n in nodes)
                {
                    if (n.ValueKind == JsonValueKind.Number)
                    {
                        total += n.GetDouble();
                    }
                }

                return JsonPathCodeGenHelpers.DoubleToElement(total, ws);
            }),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?sum(@.items[*]) == 60]", data);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Factory_NodesLogical()
    {
        // Lines 110-114: JsonPathFunction.NodesLogical(Func<JsonElement[], bool>)
        JsonElement data = JsonElement.ParseValue("""[{"tags": ["a", "b"]}, {"tags": ["c"]}]"""u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["has_multiple"] = JsonPathFunction.NodesLogical(nodes => nodes.Length > 1),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?has_multiple(@.tags[*])]", data);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Factory_Create_CustomSignature()
    {
        // Lines 126-130: JsonPathFunction.Create(returnType, parameterTypes, evaluate)
        JsonElement data = JsonElement.ParseValue("[1, 2, 3, 4, 5]"u8);
        JsonPathEvaluator evaluator = new(new Dictionary<string, IJsonPathFunction>
        {
            ["gt3"] = JsonPathFunction.Create(
                JsonPathFunctionType.LogicalType,
                [JsonPathFunctionType.ValueType],
                (args, ws) => JsonPathFunctionResult.FromLogical(args[0].Value.GetInt32() > 3)),
        });

        using JsonPathResult result = evaluator.QueryNodes("$[?gt3(@)]", data);
        Assert.AreEqual(2, result.Count);
    }
}
