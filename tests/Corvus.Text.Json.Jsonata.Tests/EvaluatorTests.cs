// <copyright file="EvaluatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

public class EvaluatorTests
{
    private static readonly JsonataEvaluator Evaluator = JsonataEvaluator.Default;

    [Fact]
    public void SimpleFieldAccess()
    {
        var result = Evaluator.EvaluateToString("name", """{"name": "John"}""");
        Assert.Equal("\"John\"", result);
    }

    [Fact]
    public void DottedPath()
    {
        var result = Evaluator.EvaluateToString("Address.City", """{"Address": {"City": "London"}}""");
        Assert.Equal("\"London\"", result);
    }

    [Fact]
    public void NumericLiteral()
    {
        var result = Evaluator.EvaluateToString("42", "{}");
        Assert.Equal("42", result);
    }

    [Fact]
    public void StringLiteral()
    {
        var result = Evaluator.EvaluateToString("\"hello\"", "{}");
        Assert.Equal("\"hello\"", result);
    }

    [Fact]
    public void Addition()
    {
        var result = Evaluator.EvaluateToString("1 + 2", "{}");
        Assert.Equal("3", result);
    }

    [Fact]
    public void Subtraction()
    {
        var result = Evaluator.EvaluateToString("10 - 3", "{}");
        Assert.Equal("7", result);
    }

    [Fact]
    public void Multiplication()
    {
        var result = Evaluator.EvaluateToString("6 * 7", "{}");
        Assert.Equal("42", result);
    }

    [Fact]
    public void Division()
    {
        var result = Evaluator.EvaluateToString("10 / 4", "{}");
        Assert.Equal("2.5", result);
    }

    [Fact]
    public void Modulo()
    {
        var result = Evaluator.EvaluateToString("10 % 3", "{}");
        Assert.Equal("1", result);
    }

    [Fact]
    public void UnaryNegation()
    {
        var result = Evaluator.EvaluateToString("-5", "{}");
        Assert.Equal("-5", result);
    }

    [Fact]
    public void BooleanTrue()
    {
        var result = Evaluator.EvaluateToString("true", "{}");
        Assert.Equal("true", result);
    }

    [Fact]
    public void BooleanFalse()
    {
        var result = Evaluator.EvaluateToString("false", "{}");
        Assert.Equal("false", result);
    }

    [Fact]
    public void NullLiteral()
    {
        var result = Evaluator.EvaluateToString("null", "{}");
        Assert.Equal("null", result);
    }

    [Fact]
    public void Comparison()
    {
        var result = Evaluator.EvaluateToString("5 > 3", "{}");
        Assert.Equal("true", result);
    }

    [Fact]
    public void ComparisonLessThan()
    {
        var result = Evaluator.EvaluateToString("3 < 5", "{}");
        Assert.Equal("true", result);
    }

    [Fact]
    public void ComparisonGreaterThanOrEqual()
    {
        var result = Evaluator.EvaluateToString("5 >= 5", "{}");
        Assert.Equal("true", result);
    }

    [Fact]
    public void ComparisonLessThanOrEqual()
    {
        var result = Evaluator.EvaluateToString("3 <= 5", "{}");
        Assert.Equal("true", result);
    }

    [Fact]
    public void StringConcat()
    {
        var result = Evaluator.EvaluateToString("\"hello\" & \" \" & \"world\"", "{}");
        Assert.Equal("\"hello world\"", result);
    }

    [Fact]
    public void UndefinedField()
    {
        var result = Evaluator.EvaluateToString("missing", """{"name": "John"}""");
        Assert.Null(result);
    }

    [Fact]
    public void ContextVariable()
    {
        var result = Evaluator.EvaluateToString("$", """42""");
        Assert.Equal("42", result);
    }

    [Fact]
    public void RootVariable()
    {
        var result = Evaluator.EvaluateToString("$$", """{"x":1}""");
        Assert.Equal("{\"x\":1}", result);
    }

    [Fact]
    public void VariableBinding()
    {
        var result = Evaluator.EvaluateToString("($x := 5; $x + 3)", "{}");
        Assert.Equal("8", result);
    }

    [Fact]
    public void MultipleVariableBindings()
    {
        var result = Evaluator.EvaluateToString(
            """($tax := 0.08; $price := 100; $price * (1 + $tax))""",
            "{}");
        Assert.Equal("108", result);
    }

    [Fact]
    public void BlockExpression()
    {
        var result = Evaluator.EvaluateToString("($a := 1; $b := 2; $a + $b)", "{}");
        Assert.Equal("3", result);
    }

    [Fact]
    public void TernaryConditionTrue()
    {
        var result = Evaluator.EvaluateToString("5 > 3 ? \"yes\" : \"no\"", "{}");
        Assert.Equal("\"yes\"", result);
    }

    [Fact]
    public void TernaryConditionFalse()
    {
        var result = Evaluator.EvaluateToString("5 < 3 ? \"yes\" : \"no\"", "{}");
        Assert.Equal("\"no\"", result);
    }

    [Fact]
    public void Equality()
    {
        var result = Evaluator.EvaluateToString("1 = 1", "{}");
        Assert.Equal("true", result);
    }

    [Fact]
    public void Inequality()
    {
        var result = Evaluator.EvaluateToString("1 != 2", "{}");
        Assert.Equal("true", result);
    }

    [Fact]
    public void AndOperator()
    {
        var result = Evaluator.EvaluateToString("true and false", "{}");
        Assert.Equal("false", result);
    }

    [Fact]
    public void OrOperator()
    {
        var result = Evaluator.EvaluateToString("true or false", "{}");
        Assert.Equal("true", result);
    }

    [Fact]
    public void InOperator()
    {
        var result = Evaluator.EvaluateToString("2 in [1, 2, 3]", "{}");
        Assert.Equal("true", result);
    }

    [Fact]
    public void ArrayConstructor()
    {
        var result = Evaluator.EvaluateToString("[1, 2, 3]", "{}");
        Assert.Equal("[1,2,3]", result);
    }

    [Fact]
    public void ObjectConstructor()
    {
        var result = Evaluator.EvaluateToString("{\"name\": \"John\"}", "{}");
        Assert.Equal("{\"name\":\"John\"}", result);
    }

    [Fact]
    public void RangeOperator()
    {
        var result = Evaluator.EvaluateToString("[1..3]", "{}");
        Assert.Equal("[1,2,3]", result);
    }

    [Fact]
    public void WildcardAccess()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1, "b": 2, "c": 3}"""u8.ToArray());
        var result = Evaluator.Evaluate("*", doc.RootElement);
        Assert.NotEqual(JsonValueKind.Undefined, result.ValueKind);
    }

    [Fact]
    public void NestedPathWithArrayAutoFlatten()
    {
        var result = Evaluator.EvaluateToString(
            "Account.Order.Product.Price",
            """{"Account": {"Order": [{"Product": {"Price": 10}}, {"Product": {"Price": 20}}]}}""");
        Assert.NotNull(result);
    }

    [Fact]
    public void ArithmeticWithFieldValues()
    {
        var result = Evaluator.EvaluateToString("price * quantity", """{"price": 10, "quantity": 5}""");
        Assert.Equal("50", result);
    }

    [Fact]
    public void EvaluateReturnsUndefinedForMissingField()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"x": 1}"""u8.ToArray());
        var result = Evaluator.Evaluate("y", doc.RootElement);
        Assert.Equal(JsonValueKind.Undefined, result.ValueKind);
    }

    [Fact]
    public void EvaluateCaching()
    {
        var result1 = Evaluator.EvaluateToString("1 + 1", "{}");
        var result2 = Evaluator.EvaluateToString("1 + 1", "{}");
        Assert.Equal(result1, result2);
        Assert.Equal("2", result1);
    }

    [Fact]
    public void SumFunction()
    {
        var result = Evaluator.EvaluateToString("$sum([1, 2, 3])", "{}");
        Assert.Equal("6", result);
    }

    [Fact]
    public void CountFunction()
    {
        var result = Evaluator.EvaluateToString("$count([10, 20, 30])", "{}");
        Assert.Equal("3", result);
    }

    [Fact]
    public void MaxFunction()
    {
        var result = Evaluator.EvaluateToString("$max([3, 1, 4, 1, 5])", "{}");
        Assert.Equal("5", result);
    }

    [Fact]
    public void MinFunction()
    {
        var result = Evaluator.EvaluateToString("$min([3, 1, 4, 1, 5])", "{}");
        Assert.Equal("1", result);
    }

    [Fact]
    public void AverageFunction()
    {
        var result = Evaluator.EvaluateToString("$average([2, 4, 6])", "{}");
        Assert.Equal("4", result);
    }

    [Fact]
    public void StringFunction()
    {
        var result = Evaluator.EvaluateToString("$string(42)", "{}");
        Assert.Equal("\"42\"", result);
    }

    [Fact]
    public void NumberFunction()
    {
        var result = Evaluator.EvaluateToString("""$number("42")""", "{}");
        Assert.Equal("42", result);
    }

    [Fact]
    public void BooleanFunction()
    {
        var result = Evaluator.EvaluateToString("$boolean(1)", "{}");
        Assert.Equal("true", result);
    }

    [Fact]
    public void NotFunction()
    {
        var result = Evaluator.EvaluateToString("$not(false)", "{}");
        Assert.Equal("true", result);
    }

    [Fact]
    public void ExistsFunction()
    {
        var result = Evaluator.EvaluateToString("$exists(name)", """{"name": "John"}""");
        Assert.Equal("true", result);
    }

    [Fact]
    public void NotExistsFunction()
    {
        var result = Evaluator.EvaluateToString("$exists(missing)", """{"name": "John"}""");
        Assert.Equal("false", result);
    }

    [Fact]
    public void TypeFunctionNumber()
    {
        var result = Evaluator.EvaluateToString("$type(42)", "{}");
        Assert.Equal("\"number\"", result);
    }

    [Fact]
    public void TypeFunctionString()
    {
        var result = Evaluator.EvaluateToString("""$type("hello")""", "{}");
        Assert.Equal("\"string\"", result);
    }

    [Fact]
    public void TypeFunctionBoolean()
    {
        var result = Evaluator.EvaluateToString("$type(true)", "{}");
        Assert.Equal("\"boolean\"", result);
    }

    [Fact]
    public void TypeFunctionNull()
    {
        var result = Evaluator.EvaluateToString("$type(null)", "{}");
        Assert.Equal("\"null\"", result);
    }

    [Fact]
    public void TypeFunctionArray()
    {
        var result = Evaluator.EvaluateToString("$type([1,2])", "{}");
        Assert.Equal("\"array\"", result);
    }

    [Fact]
    public void TypeFunctionObject()
    {
        var result = Evaluator.EvaluateToString("""$type({"a":1})""", "{}");
        Assert.Equal("\"object\"", result);
    }

    [Fact]
    public void LengthFunction()
    {
        var result = Evaluator.EvaluateToString("""$length("hello")""", "{}");
        Assert.Equal("5", result);
    }

    [Fact]
    public void UppercaseFunction()
    {
        var result = Evaluator.EvaluateToString("$uppercase(\"hello\")", "{}");
        Assert.Equal("\"HELLO\"", result);
    }

    [Fact]
    public void LowercaseFunction()
    {
        var result = Evaluator.EvaluateToString("$lowercase(\"HELLO\")", "{}");
        Assert.Equal("\"hello\"", result);
    }

    [Fact]
    public void TrimFunction()
    {
        var result = Evaluator.EvaluateToString("""$trim("  hello  ")""", "{}");
        Assert.Equal("\"hello\"", result);
    }

    [Fact]
    public void SubstringFunction()
    {
        var result = Evaluator.EvaluateToString("$substring(\"hello world\", 0, 5)", "{}");
        Assert.Equal("\"hello\"", result);
    }

    [Fact]
    public void SubstringBeforeFunction()
    {
        var result = Evaluator.EvaluateToString("""$substringBefore("hello world", " ")""", "{}");
        Assert.Equal("\"hello\"", result);
    }

    [Fact]
    public void SubstringAfterFunction()
    {
        var result = Evaluator.EvaluateToString("""$substringAfter("hello world", " ")""", "{}");
        Assert.Equal("\"world\"", result);
    }

    [Fact]
    public void ContainsFunction()
    {
        var result = Evaluator.EvaluateToString("$contains(\"hello world\", \"world\")", "{}");
        Assert.Equal("true", result);
    }

    [Fact]
    public void SplitFunction()
    {
        var result = Evaluator.EvaluateToString("""$split("a,b,c", ",")""", "{}");
        Assert.Equal("[\"a\",\"b\",\"c\"]", result);
    }

    [Fact]
    public void JoinFunction()
    {
        var result = Evaluator.EvaluateToString("$join([\"a\", \"b\", \"c\"], \",\")", "{}");
        Assert.Equal("\"a,b,c\"", result);
    }

    [Fact]
    public void AbsFunction()
    {
        var result = Evaluator.EvaluateToString("$abs(-5)", "{}");
        Assert.Equal("5", result);
    }

    [Fact]
    public void FloorFunction()
    {
        var result = Evaluator.EvaluateToString("$floor(3.7)", "{}");
        Assert.Equal("3", result);
    }

    [Fact]
    public void CeilFunction()
    {
        var result = Evaluator.EvaluateToString("$ceil(3.2)", "{}");
        Assert.Equal("4", result);
    }

    [Fact]
    public void RoundFunction()
    {
        var result = Evaluator.EvaluateToString("$round(3.456, 2)", "{}");
        Assert.Equal("3.46", result);
    }

    [Fact]
    public void PowerFunction()
    {
        var result = Evaluator.EvaluateToString("$power(2, 10)", "{}");
        Assert.Equal("1024", result);
    }

    [Fact]
    public void SqrtFunction()
    {
        var result = Evaluator.EvaluateToString("$sqrt(9)", "{}");
        Assert.Equal("3", result);
    }

    [Fact]
    public void KeysFunction()
    {
        var result = Evaluator.EvaluateToString("$keys({\"a\": 1, \"b\": 2})", "{}");
        Assert.Equal("[\"a\",\"b\"]", result);
    }

    [Fact]
    public void ValuesFunction()
    {
        var result = Evaluator.EvaluateToString("""$values({"a": 1, "b": 2})""", "{}");
        Assert.Equal("[1,2]", result);
    }

    [Fact]
    public void AppendFunction()
    {
        var result = Evaluator.EvaluateToString("$append([1, 2], [3, 4])", "{}");
        Assert.Equal("[1,2,3,4]", result);
    }

    [Fact]
    public void ReverseFunction()
    {
        var result = Evaluator.EvaluateToString("$reverse([1, 2, 3])", "{}");
        Assert.Equal("[3,2,1]", result);
    }

    [Fact]
    public void FlattenFunction()
    {
        var result = Evaluator.EvaluateToString("$flatten([[1, 2], [3, 4]])", "{}");
        Assert.Equal("[1,2,3,4]", result);
    }

    [Fact]
    public void LookupFunction()
    {
        var result = Evaluator.EvaluateToString("""$lookup({"a": 1, "b": 2}, "a")""", "{}");
        Assert.Equal("1", result);
    }

    [Fact]
    public void PathFieldAccess()
    {
        var result = Evaluator.EvaluateToString("Account.Name", """{"Account": {"Name": "Endjin"}}""");
        Assert.Equal("\"Endjin\"", result);
    }

    [Fact]
    public void ComplexPathExpression()
    {
        string data = """
            {
                "Account": {
                    "Account Name": "Firefly",
                    "Order": [
                        {"Product": {"Product Name": "Bowler Hat", "Price": 34.45}},
                        {"Product": {"Product Name": "Trilby hat", "Price": 21.67}}
                    ]
                }
            }
            """;

        var result = Evaluator.EvaluateToString("Account.Order.Product.Price", data);
        Assert.NotNull(result);
    }

    // --- Phase 3: Higher-order function tests ---
    [Fact]
    public void MapFunction()
    {
        var result = Evaluator.EvaluateToString("""$map([1,2,3], function($v) { $v * 2 })""", "{}");
        Assert.Equal("[2,4,6]", result);
    }

    [Fact]
    public void MapWithIndex()
    {
        var result = Evaluator.EvaluateToString("""$map(["a","b","c"], function($v, $i) { $i })""", "{}");
        Assert.Equal("[0,1,2]", result);
    }

    [Fact]
    public void FilterFunction()
    {
        var result = Evaluator.EvaluateToString("""$filter([1,2,3,4,5], function($v) { $v > 2 })""", "{}");
        Assert.Equal("[3,4,5]", result);
    }

    [Fact]
    public void ReduceFunction()
    {
        var result = Evaluator.EvaluateToString("""$reduce([1,2,3,4], function($acc, $v) { $acc + $v })""", "{}");
        Assert.Equal("10", result);
    }

    [Fact]
    public void ReduceWithInit()
    {
        var result = Evaluator.EvaluateToString("""$reduce([1,2,3], function($acc, $v) { $acc + $v }, 10)""", "{}");
        Assert.Equal("16", result);
    }

    [Fact]
    public void EachFunction()
    {
        var result = Evaluator.EvaluateToString("""$each({"a": 1, "b": 2}, function($v, $k) { $v })""", "{}");
        Assert.Equal("[1,2]", result);
    }

    [Fact]
    public void MergeFunction()
    {
        var result = Evaluator.EvaluateToString("""$merge([{"a":1},{"b":2}])""", "{}");
        Assert.Equal("""{"a":1,"b":2}""", result);
    }

    [Fact]
    public void SpreadFunction()
    {
        var result = Evaluator.EvaluateToString("""$spread({"a":1,"b":2})""", "{}");
        Assert.Equal("""[{"a":1},{"b":2}]""", result);
    }

    [Fact]
    public void SortFunctionDefault()
    {
        var result = Evaluator.EvaluateToString("""$sort([3,1,2])""", "{}");
        Assert.Equal("[1,2,3]", result);
    }

    [Fact]
    public void SortFunctionWithComparator()
    {
        var result = Evaluator.EvaluateToString("""$sort([1,3,2], function($a, $b) { $b - $a })""", "{}");
        Assert.Equal("[3,2,1]", result);
    }

    [Fact]
    public void DistinctFunction()
    {
        var result = Evaluator.EvaluateToString("""$distinct([1,2,2,3,3,3])""", "{}");
        Assert.Equal("[1,2,3]", result);
    }

    [Fact]
    public void SingleFunction()
    {
        var result = Evaluator.EvaluateToString("""$single([42])""", "{}");
        Assert.Equal("42", result);
    }

    [Fact]
    public void SingleWithPredicate()
    {
        var result = Evaluator.EvaluateToString("""$single([1,2,3], function($v) { $v = 2 })""", "{}");
        Assert.Equal("2", result);
    }

    [Fact]
    public void SiftFunction()
    {
        var result = Evaluator.EvaluateToString("""$sift({"a":1,"b":2,"c":3}, function($v) { $v > 1 })""", "{}");
        Assert.Equal("""{"b":2,"c":3}""", result);
    }

    // --- Lambda, closure, pipe tests ---
    [Fact]
    public void LambdaInvocation()
    {
        var result = Evaluator.EvaluateToString("""($f := function($x) { $x + 1 }; $f(5))""", "{}");
        Assert.Equal("6", result);
    }

    [Fact]
    public void ClosureCapture()
    {
        var result = Evaluator.EvaluateToString("""($y := 10; $f := function($x) { $x + $y }; $f(5))""", "{}");
        Assert.Equal("15", result);
    }

    [Fact]
    public void PipeOperator()
    {
        var result = Evaluator.EvaluateToString("""($double := function($x) { $x * 2 }; 5 ~> $double)""", "{}");
        Assert.Equal("10", result);
    }

    // --- String function tests ---
    [Fact]
    public void PadRight()
    {
        var result = Evaluator.EvaluateToString("""$pad("hi", 5)""", "{}");
        Assert.Equal("\"hi   \"", result);
    }

    [Fact]
    public void PadLeft()
    {
        var result = Evaluator.EvaluateToString("""$pad("hi", -5, "*")""", "{}");
        Assert.Equal("\"***hi\"", result);
    }

    [Fact]
    public void ReplaceFunction()
    {
        var result = Evaluator.EvaluateToString("""$replace("hello world", "world", "there")""", "{}");
        Assert.Equal("\"hello there\"", result);
    }

    [Fact]
    public void ReplaceWithLimit()
    {
        var result = Evaluator.EvaluateToString("""$replace("aaa", "a", "b", 2)""", "{}");
        Assert.Equal("\"bba\"", result);
    }

    // --- Encoding function tests ---
    [Fact]
    public void Base64Encode()
    {
        var result = Evaluator.EvaluateToString("""$base64encode("hello")""", "{}");
        Assert.Equal("\"aGVsbG8=\"", result);
    }

    [Fact]
    public void Base64Decode()
    {
        var result = Evaluator.EvaluateToString("""$base64decode("aGVsbG8=")""", "{}");
        Assert.Equal("\"hello\"", result);
    }

    [Fact]
    public void EncodeUrlComponent()
    {
        var result = Evaluator.EvaluateToString("""$encodeUrlComponent("hello world")""", "{}");
        Assert.Equal("\"hello%20world\"", result);
    }

    [Fact]
    public void DecodeUrlComponent()
    {
        var result = Evaluator.EvaluateToString("""$decodeUrlComponent("hello%20world")""", "{}");
        Assert.Equal("\"hello world\"", result);
    }

    // --- Misc function tests ---
    [Fact]
    public void ShuffleDoesNotCrash()
    {
        var result = Evaluator.EvaluateToString("""$shuffle([1,2,3])""", "{}");
        Assert.NotNull(result);
        Assert.StartsWith("[", result);
    }

    [Fact]
    public void ShufflePropertyChainActuallyShuffles()
    {
        // a.b produces a multi-element Sequence [1,2,...,10] via auto-flatten.
        // $shuffle must treat this as an array and randomize the element order.
        // With 10 elements the probability of the original order is 1/10! ≈ 0.
        string json = """{"a": [{"b": 1}, {"b": 2}, {"b": 3}, {"b": 4}, {"b": 5}, {"b": 6}, {"b": 7}, {"b": 8}, {"b": 9}, {"b": 10}]}""";
        string original = "[1,2,3,4,5,6,7,8,9,10]";

        bool foundDifferent = false;
        for (int i = 0; i < 5; i++)
        {
            var result = Evaluator.EvaluateToString("$shuffle(a.b)", json);
            if (result != original)
            {
                foundDifferent = true;

                // Also verify all elements are preserved (same values, just reordered)
                var sorted = Evaluator.EvaluateToString("$sort($shuffle(a.b))", json);
                Assert.Equal(original, sorted);
                break;
            }
        }

        Assert.True(foundDifferent, "$shuffle on a property chain through arrays must actually shuffle the elements");
    }

    [Fact]
    public void ZipFunction()
    {
        var result = Evaluator.EvaluateToString("""$zip([1,2],[3,4])""", "{}");
        Assert.Equal("[[1,3],[2,4]]", result);
    }

    [Fact]
    public void ErrorFunctionThrows()
    {
        var ex = Assert.Throws<JsonataException>(() => Evaluator.EvaluateToString("""$error("boom")""", "{}"));
        Assert.Contains("boom", ex.Message);
    }

    [Fact]
    public void AssertPassesOnTrue()
    {
        // $assert returns undefined (no output) on success
        var result = Evaluator.EvaluateToString("""$assert(true)""", "{}");
        Assert.Null(result);
    }

    [Fact]
    public void AssertFailsOnFalse()
    {
        var ex = Assert.Throws<JsonataException>(() => Evaluator.EvaluateToString("""$assert(false, "nope")""", "{}"));
        Assert.Contains("nope", ex.Message);
    }

    [Fact]
    public void EvalFunction()
    {
        var result = Evaluator.EvaluateToString("""$eval("1 + 2")""", "{}");
        Assert.Equal("3", result);
    }

    [Fact]
    public void FormatBase()
    {
        var result = Evaluator.EvaluateToString("""$formatBase(255, 16)""", "{}");
        Assert.Equal("\"ff\"", result);
    }

    [Fact]
    public void NowReturnsString()
    {
        var result = Evaluator.EvaluateToString("""$now()""", "{}");
        Assert.NotNull(result);
        Assert.StartsWith("\"", result);
    }

    [Fact]
    public void MillisReturnsNumber()
    {
        var result = Evaluator.EvaluateToString("""$millis()""", "{}");
        Assert.NotNull(result);
        // Should be a large number (Unix timestamp in ms)
        Assert.True(double.TryParse(result, out double ms));
        Assert.True(ms > 1_000_000_000_000);
    }

    // ── Function binding tests ───────────────────────────────

    [Fact]
    public void FunctionBindingSimple()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"x": 5}"""u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["double"] = JsonataBinding.FromFunction(
                (args, ws) =>
                {
                    double val = args[0].GetDouble();
                    return JsonataCodeGenHelpers.DoubleToElement(val * 2, ws);
                },
                parameterCount: 1),
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();
        var result = evaluator.Evaluate("$double(x)", doc.RootElement, workspace, bindings);
        Assert.Equal(10.0, result.GetDouble());
    }

    [Fact]
    public void FunctionBindingMultipleArgs()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["add"] = JsonataBinding.FromFunction(
                (args, ws) =>
                {
                    double a = args[0].GetDouble();
                    double b = args[1].GetDouble();
                    return JsonataCodeGenHelpers.DoubleToElement(a + b, ws);
                },
                parameterCount: 2),
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();
        var result = evaluator.Evaluate("$add(3, 7)", doc.RootElement, workspace, bindings);
        Assert.Equal(10.0, result.GetDouble());
    }

    [Fact]
    public void FunctionBindingWithValueBinding()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        using JsonWorkspace workspace = JsonWorkspace.Create();

        // Create a JsonElement for the value binding
        var thresholdDoc = ParsedJsonDocument<JsonElement>.Parse("100"u8.ToArray());
        JsonElement thresholdElement = thresholdDoc.RootElement.Clone();
        thresholdDoc.Dispose();

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["threshold"] = thresholdElement,  // implicit conversion
            ["clamp"] = JsonataBinding.FromFunction(
                (args, ws) =>
                {
                    double val = args[0].GetDouble();
                    double max = args[1].GetDouble();
                    double result = val > max ? max : val;
                    return JsonataCodeGenHelpers.DoubleToElement(result, ws);
                },
                parameterCount: 2),
        };

        var result = evaluator.Evaluate("$clamp(150, $threshold)", doc.RootElement, workspace, bindings);
        Assert.Equal(100.0, result.GetDouble());
    }

    [Fact]
    public void FunctionBindingUsedInMapHof()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"items": [1, 2, 3]}"""u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["triple"] = JsonataBinding.FromFunction(
                (args, ws) =>
                {
                    double val = args[0].GetDouble();
                    return JsonataCodeGenHelpers.DoubleToElement(val * 3, ws);
                },
                parameterCount: 1),
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();
        var result = evaluator.Evaluate("$map(items, $triple)", doc.RootElement, workspace, bindings);
        Assert.Equal("[3,6,9]", result.GetRawText());
    }

    [Fact]
    public void FunctionBindingReturnsString()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name": "world"}"""u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["greet"] = JsonataBinding.FromFunction(
                (args, ws) =>
                {
                    string name = args[0].GetString()!;
                    return JsonataHelpers.StringFromString("Hello, " + name + "!", ws);
                },
                parameterCount: 1),
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();
        var result = evaluator.Evaluate("$greet(name)", doc.RootElement, workspace, bindings);
        Assert.Equal("Hello, world!", result.GetString());
    }

    [Fact]
    public void FunctionBindingWithoutWorkspaceOverload()
    {
        // Test the non-workspace Evaluate overload (result is cloned)
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"x": 7}"""u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["square"] = JsonataBinding.FromFunction(
                (args, ws) =>
                {
                    double val = args[0].GetDouble();
                    return JsonataCodeGenHelpers.DoubleToElement(val * val, ws);
                },
                parameterCount: 1),
        };

        var result = evaluator.Evaluate("$square(x)", doc.RootElement, bindings);
        Assert.Equal(49.0, result.GetDouble());
    }

    [Fact]
    public void FunctionBindingTooFewArgsThrows()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["hypot"] = JsonataBinding.FromFunction(
                (args, ws) =>
                {
                    double a = args[0].GetDouble();
                    double b = args[1].GetDouble();
                    return JsonataCodeGenHelpers.DoubleToElement(Math.Sqrt((a * a) + (b * b)), ws);
                },
                parameterCount: 2),
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();
        var ex = Assert.Throws<JsonataException>(() =>
            evaluator.Evaluate("$hypot(3)", doc.RootElement, workspace, bindings));
        Assert.Equal("T0410", ex.Code);
        Assert.Contains("expects 2", ex.Message);
        Assert.Contains("got 1", ex.Message);
    }

    [Fact]
    public void FunctionBindingExtraArgsSlicedSilently()
    {
        var evaluator = new JsonataEvaluator();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["hypot"] = JsonataBinding.FromFunction(
                (args, ws) =>
                {
                    double a = args[0].GetDouble();
                    double b = args[1].GetDouble();
                    return JsonataCodeGenHelpers.DoubleToElement(Math.Sqrt((a * a) + (b * b)), ws);
                },
                parameterCount: 2),
        };

        using JsonWorkspace workspace = JsonWorkspace.Create();

        // Extra 3rd arg is silently sliced away (matches JSONata convention).
        var result = evaluator.Evaluate("$hypot(3, 4, 999)", doc.RootElement, workspace, bindings);
        Assert.Equal(5.0, result.GetDouble());
    }

    [Fact]
    public void KeepArrayOverArrayWithNoMatchingProperties_ReturnsUndefined()
    {
        // path[] over an array of objects where no object has the property should be undefined
        var result = Evaluator.EvaluateToString(
            """items.nonexistent[]""",
            """{"items": [{"a": 1}, {"b": 2}]}""");
        Assert.Null(result);
    }

    [Fact]
    public void KeepArrayOverEmptyArray_ReturnsUndefined()
    {
        // path[] over an empty array should be undefined
        var result = Evaluator.EvaluateToString(
            """items.a[]""",
            """{"items": []}""");
        Assert.Null(result);
    }

    [Fact]
    public void KeepArrayWithDeepNesting_FlattensCorrectly()
    {
        // 3 levels of array nesting: [[[ {a:1} ]]].a[] should flatten to [1]
        var result = Evaluator.EvaluateToString(
            """items.a[]""",
            """{"items": [[[{"a": 1}]]]}""");
        Assert.Equal("[1]", result);
    }

    [Fact]
    public void KeepArrayWithMixedTypes_SkipsNonObjects()
    {
        // Mixed array: only objects with the property contribute; primitives and nulls are skipped
        var result = Evaluator.EvaluateToString(
            """items.x[]""",
            """{"items": [1, "str", {"x": 5}, null, [{"x": 6}]]}""");
        Assert.Equal("[5,6]", result);
    }

    [Fact]
    public void KeepArrayWithSingleMatch_WrapsInArray()
    {
        // Single match with [] should still wrap in array
        var result = Evaluator.EvaluateToString(
            """items.a[]""",
            """{"items": [{"a": 42}]}""");
        Assert.Equal("[42]", result);
    }

    [Fact]
    public void KeepArrayWithArrayPropertyValues_Flattens()
    {
        // Property values that are arrays should be flattened into the result
        var result = Evaluator.EvaluateToString(
            """items.a[]""",
            """{"items": [{"a": [1, 2]}, {"a": [3]}]}""");
        Assert.Equal("[1,2,3]", result);
    }

    [Fact]
    public void KeepArrayOnObjectInput_WrapsPropertyValue()
    {
        // path[] on an object (not array) should wrap the property value in an array
        var result = Evaluator.EvaluateToString(
            """data.name[]""",
            """{"data": {"name": "Alice"}}""");
        Assert.Equal("[\"Alice\"]", result);
    }

    [Fact]
    public void KeepArrayOnObjectWithMissingProperty_ReturnsUndefined()
    {
        // path[] on an object where the property doesn't exist should be undefined
        var result = Evaluator.EvaluateToString(
            """data.missing[]""",
            """{"data": {"name": "Alice"}}""");
        Assert.Null(result);
    }
}
