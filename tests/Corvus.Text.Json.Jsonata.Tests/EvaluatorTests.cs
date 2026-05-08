// <copyright file="EvaluatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Jsonata.Tests;

[TestClass]
public class EvaluatorTests
{
    private static readonly JsonataEvaluator Evaluator = JsonataEvaluator.Default;

    [TestMethod]
    public void SimpleFieldAccess()
    {
        var result = Evaluator.EvaluateToString("name", """{"name": "John"}""");
        Assert.AreEqual("\"John\"", result);
    }

    [TestMethod]
    public void DottedPath()
    {
        var result = Evaluator.EvaluateToString("Address.City", """{"Address": {"City": "London"}}""");
        Assert.AreEqual("\"London\"", result);
    }

    [TestMethod]
    public void NumericLiteral()
    {
        var result = Evaluator.EvaluateToString("42", "{}");
        Assert.AreEqual("42", result);
    }

    [TestMethod]
    public void StringLiteral()
    {
        var result = Evaluator.EvaluateToString("\"hello\"", "{}");
        Assert.AreEqual("\"hello\"", result);
    }

    [TestMethod]
    public void Addition()
    {
        var result = Evaluator.EvaluateToString("1 + 2", "{}");
        Assert.AreEqual("3", result);
    }

    [TestMethod]
    public void Subtraction()
    {
        var result = Evaluator.EvaluateToString("10 - 3", "{}");
        Assert.AreEqual("7", result);
    }

    [TestMethod]
    public void Multiplication()
    {
        var result = Evaluator.EvaluateToString("6 * 7", "{}");
        Assert.AreEqual("42", result);
    }

    [TestMethod]
    public void Division()
    {
        var result = Evaluator.EvaluateToString("10 / 4", "{}");
        Assert.AreEqual("2.5", result);
    }

    [TestMethod]
    public void Modulo()
    {
        var result = Evaluator.EvaluateToString("10 % 3", "{}");
        Assert.AreEqual("1", result);
    }

    [TestMethod]
    public void UnaryNegation()
    {
        var result = Evaluator.EvaluateToString("-5", "{}");
        Assert.AreEqual("-5", result);
    }

    [TestMethod]
    public void BooleanTrue()
    {
        var result = Evaluator.EvaluateToString("true", "{}");
        Assert.AreEqual("true", result);
    }

    [TestMethod]
    public void BooleanFalse()
    {
        var result = Evaluator.EvaluateToString("false", "{}");
        Assert.AreEqual("false", result);
    }

    [TestMethod]
    public void NullLiteral()
    {
        var result = Evaluator.EvaluateToString("null", "{}");
        Assert.AreEqual("null", result);
    }

    [TestMethod]
    public void Comparison()
    {
        var result = Evaluator.EvaluateToString("5 > 3", "{}");
        Assert.AreEqual("true", result);
    }

    [TestMethod]
    public void ComparisonLessThan()
    {
        var result = Evaluator.EvaluateToString("3 < 5", "{}");
        Assert.AreEqual("true", result);
    }

    [TestMethod]
    public void ComparisonGreaterThanOrEqual()
    {
        var result = Evaluator.EvaluateToString("5 >= 5", "{}");
        Assert.AreEqual("true", result);
    }

    [TestMethod]
    public void ComparisonLessThanOrEqual()
    {
        var result = Evaluator.EvaluateToString("3 <= 5", "{}");
        Assert.AreEqual("true", result);
    }

    [TestMethod]
    public void StringConcat()
    {
        var result = Evaluator.EvaluateToString("\"hello\" & \" \" & \"world\"", "{}");
        Assert.AreEqual("\"hello world\"", result);
    }

    [TestMethod]
    public void UndefinedField()
    {
        var result = Evaluator.EvaluateToString("missing", """{"name": "John"}""");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ContextVariable()
    {
        var result = Evaluator.EvaluateToString("$", """42""");
        Assert.AreEqual("42", result);
    }

    [TestMethod]
    public void RootVariable()
    {
        var result = Evaluator.EvaluateToString("$$", """{"x":1}""");
        Assert.AreEqual("{\"x\":1}", result);
    }

    [TestMethod]
    public void VariableBinding()
    {
        var result = Evaluator.EvaluateToString("($x := 5; $x + 3)", "{}");
        Assert.AreEqual("8", result);
    }

    [TestMethod]
    public void MultipleVariableBindings()
    {
        var result = Evaluator.EvaluateToString(
            """($tax := 0.08; $price := 100; $price * (1 + $tax))""",
            "{}");
        Assert.AreEqual("108", result);
    }

    [TestMethod]
    public void BlockExpression()
    {
        var result = Evaluator.EvaluateToString("($a := 1; $b := 2; $a + $b)", "{}");
        Assert.AreEqual("3", result);
    }

    [TestMethod]
    public void TernaryConditionTrue()
    {
        var result = Evaluator.EvaluateToString("5 > 3 ? \"yes\" : \"no\"", "{}");
        Assert.AreEqual("\"yes\"", result);
    }

    [TestMethod]
    public void TernaryConditionFalse()
    {
        var result = Evaluator.EvaluateToString("5 < 3 ? \"yes\" : \"no\"", "{}");
        Assert.AreEqual("\"no\"", result);
    }

    [TestMethod]
    public void Equality()
    {
        var result = Evaluator.EvaluateToString("1 = 1", "{}");
        Assert.AreEqual("true", result);
    }

    [TestMethod]
    public void Inequality()
    {
        var result = Evaluator.EvaluateToString("1 != 2", "{}");
        Assert.AreEqual("true", result);
    }

    [TestMethod]
    public void AndOperator()
    {
        var result = Evaluator.EvaluateToString("true and false", "{}");
        Assert.AreEqual("false", result);
    }

    [TestMethod]
    public void OrOperator()
    {
        var result = Evaluator.EvaluateToString("true or false", "{}");
        Assert.AreEqual("true", result);
    }

    [TestMethod]
    public void InOperator()
    {
        var result = Evaluator.EvaluateToString("2 in [1, 2, 3]", "{}");
        Assert.AreEqual("true", result);
    }

    [TestMethod]
    public void ArrayConstructor()
    {
        var result = Evaluator.EvaluateToString("[1, 2, 3]", "{}");
        Assert.AreEqual("[1,2,3]", result);
    }

    [TestMethod]
    public void ObjectConstructor()
    {
        var result = Evaluator.EvaluateToString("{\"name\": \"John\"}", "{}");
        Assert.AreEqual("{\"name\":\"John\"}", result);
    }

    [TestMethod]
    public void RangeOperator()
    {
        var result = Evaluator.EvaluateToString("[1..3]", "{}");
        Assert.AreEqual("[1,2,3]", result);
    }

    [TestMethod]
    public void WildcardAccess()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"a": 1, "b": 2, "c": 3}"""u8.ToArray());
        var result = Evaluator.Evaluate("*", doc.RootElement);
        Assert.AreNotEqual(JsonValueKind.Undefined, result.ValueKind);
    }

    [TestMethod]
    public void NestedPathWithArrayAutoFlatten()
    {
        var result = Evaluator.EvaluateToString(
            "Account.Order.Product.Price",
            """{"Account": {"Order": [{"Product": {"Price": 10}}, {"Product": {"Price": 20}}]}}""");
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void ArithmeticWithFieldValues()
    {
        var result = Evaluator.EvaluateToString("price * quantity", """{"price": 10, "quantity": 5}""");
        Assert.AreEqual("50", result);
    }

    [TestMethod]
    public void ArithmeticWithArrayLeftOperandThrowsT2001()
    {
        // Account.Order.Product.Price collects [34.45, 21.67] — an array, not a number.
        // Arithmetic on an array operand must throw T2001.
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Evaluator.EvaluateToString(
                "Account.Order.Product.Price + 1",
                """{"Account": {"Order": [{"Product": {"Price": 34.45}}, {"Product": {"Price": 21.67}}]}}"""));
        Assert.AreEqual("T2001", ex.Code);
    }

    [TestMethod]
    public void ArithmeticWithArrayRightOperandThrowsT2002()
    {
        // Same pattern but array on the right side — must throw T2002.
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Evaluator.EvaluateToString(
                "1 + Account.Order.Product.Price",
                """{"Account": {"Order": [{"Product": {"Price": 34.45}}, {"Product": {"Price": 21.67}}]}}"""));
        Assert.AreEqual("T2002", ex.Code);
    }

    [TestMethod]
    public void ArithmeticWithObjectLeftOperandThrowsT2001()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Evaluator.EvaluateToString(
                "item + 1",
                """{"item": {"price": 34.45}}"""));
        Assert.AreEqual("T2001", ex.Code);
    }

    [TestMethod]
    public void ArithmeticWithObjectRightOperandThrowsT2002()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            Evaluator.EvaluateToString(
                "1 + item",
                """{"item": {"price": 34.45}}"""));
        Assert.AreEqual("T2002", ex.Code);
    }

    [TestMethod]
    public void EvaluateReturnsUndefinedForMissingField()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"x": 1}"""u8.ToArray());
        var result = Evaluator.Evaluate("y", doc.RootElement);
        Assert.AreEqual(JsonValueKind.Undefined, result.ValueKind);
    }

    [TestMethod]
    public void EvaluateCaching()
    {
        var result1 = Evaluator.EvaluateToString("1 + 1", "{}");
        var result2 = Evaluator.EvaluateToString("1 + 1", "{}");
        Assert.AreEqual(result1, result2);
        Assert.AreEqual("2", result1);
    }

    [TestMethod]
    public void SumFunction()
    {
        var result = Evaluator.EvaluateToString("$sum([1, 2, 3])", "{}");
        Assert.AreEqual("6", result);
    }

    [TestMethod]
    public void CountFunction()
    {
        var result = Evaluator.EvaluateToString("$count([10, 20, 30])", "{}");
        Assert.AreEqual("3", result);
    }

    [TestMethod]
    public void MaxFunction()
    {
        var result = Evaluator.EvaluateToString("$max([3, 1, 4, 1, 5])", "{}");
        Assert.AreEqual("5", result);
    }

    [TestMethod]
    public void MinFunction()
    {
        var result = Evaluator.EvaluateToString("$min([3, 1, 4, 1, 5])", "{}");
        Assert.AreEqual("1", result);
    }

    [TestMethod]
    public void AverageFunction()
    {
        var result = Evaluator.EvaluateToString("$average([2, 4, 6])", "{}");
        Assert.AreEqual("4", result);
    }

    [TestMethod]
    public void StringFunction()
    {
        var result = Evaluator.EvaluateToString("$string(42)", "{}");
        Assert.AreEqual("\"42\"", result);
    }

    [TestMethod]
    public void NumberFunction()
    {
        var result = Evaluator.EvaluateToString("""$number("42")""", "{}");
        Assert.AreEqual("42", result);
    }

    [TestMethod]
    public void BooleanFunction()
    {
        var result = Evaluator.EvaluateToString("$boolean(1)", "{}");
        Assert.AreEqual("true", result);
    }

    [TestMethod]
    public void NotFunction()
    {
        var result = Evaluator.EvaluateToString("$not(false)", "{}");
        Assert.AreEqual("true", result);
    }

    [TestMethod]
    public void ExistsFunction()
    {
        var result = Evaluator.EvaluateToString("$exists(name)", """{"name": "John"}""");
        Assert.AreEqual("true", result);
    }

    [TestMethod]
    public void NotExistsFunction()
    {
        var result = Evaluator.EvaluateToString("$exists(missing)", """{"name": "John"}""");
        Assert.AreEqual("false", result);
    }

    [TestMethod]
    public void TypeFunctionNumber()
    {
        var result = Evaluator.EvaluateToString("$type(42)", "{}");
        Assert.AreEqual("\"number\"", result);
    }

    [TestMethod]
    public void TypeFunctionString()
    {
        var result = Evaluator.EvaluateToString("""$type("hello")""", "{}");
        Assert.AreEqual("\"string\"", result);
    }

    [TestMethod]
    public void TypeFunctionBoolean()
    {
        var result = Evaluator.EvaluateToString("$type(true)", "{}");
        Assert.AreEqual("\"boolean\"", result);
    }

    [TestMethod]
    public void TypeFunctionNull()
    {
        var result = Evaluator.EvaluateToString("$type(null)", "{}");
        Assert.AreEqual("\"null\"", result);
    }

    [TestMethod]
    public void TypeFunctionArray()
    {
        var result = Evaluator.EvaluateToString("$type([1,2])", "{}");
        Assert.AreEqual("\"array\"", result);
    }

    [TestMethod]
    public void TypeFunctionObject()
    {
        var result = Evaluator.EvaluateToString("""$type({"a":1})""", "{}");
        Assert.AreEqual("\"object\"", result);
    }

    [TestMethod]
    public void LengthFunction()
    {
        var result = Evaluator.EvaluateToString("""$length("hello")""", "{}");
        Assert.AreEqual("5", result);
    }

    [TestMethod]
    public void UppercaseFunction()
    {
        var result = Evaluator.EvaluateToString("$uppercase(\"hello\")", "{}");
        Assert.AreEqual("\"HELLO\"", result);
    }

    [TestMethod]
    public void LowercaseFunction()
    {
        var result = Evaluator.EvaluateToString("$lowercase(\"HELLO\")", "{}");
        Assert.AreEqual("\"hello\"", result);
    }

    [TestMethod]
    public void TrimFunction()
    {
        var result = Evaluator.EvaluateToString("""$trim("  hello  ")""", "{}");
        Assert.AreEqual("\"hello\"", result);
    }

    [TestMethod]
    public void SubstringFunction()
    {
        var result = Evaluator.EvaluateToString("$substring(\"hello world\", 0, 5)", "{}");
        Assert.AreEqual("\"hello\"", result);
    }

    [TestMethod]
    public void SubstringBeforeFunction()
    {
        var result = Evaluator.EvaluateToString("""$substringBefore("hello world", " ")""", "{}");
        Assert.AreEqual("\"hello\"", result);
    }

    [TestMethod]
    public void SubstringAfterFunction()
    {
        var result = Evaluator.EvaluateToString("""$substringAfter("hello world", " ")""", "{}");
        Assert.AreEqual("\"world\"", result);
    }

    [TestMethod]
    public void ContainsFunction()
    {
        var result = Evaluator.EvaluateToString("$contains(\"hello world\", \"world\")", "{}");
        Assert.AreEqual("true", result);
    }

    [TestMethod]
    public void SplitFunction()
    {
        var result = Evaluator.EvaluateToString("""$split("a,b,c", ",")""", "{}");
        Assert.AreEqual("[\"a\",\"b\",\"c\"]", result);
    }

    [TestMethod]
    public void JoinFunction()
    {
        var result = Evaluator.EvaluateToString("$join([\"a\", \"b\", \"c\"], \",\")", "{}");
        Assert.AreEqual("\"a,b,c\"", result);
    }

    [TestMethod]
    public void AbsFunction()
    {
        var result = Evaluator.EvaluateToString("$abs(-5)", "{}");
        Assert.AreEqual("5", result);
    }

    [TestMethod]
    public void FloorFunction()
    {
        var result = Evaluator.EvaluateToString("$floor(3.7)", "{}");
        Assert.AreEqual("3", result);
    }

    [TestMethod]
    public void CeilFunction()
    {
        var result = Evaluator.EvaluateToString("$ceil(3.2)", "{}");
        Assert.AreEqual("4", result);
    }

    [TestMethod]
    public void RoundFunction()
    {
        var result = Evaluator.EvaluateToString("$round(3.456, 2)", "{}");
        Assert.AreEqual("3.46", result);
    }

    [TestMethod]
    public void PowerFunction()
    {
        var result = Evaluator.EvaluateToString("$power(2, 10)", "{}");
        Assert.AreEqual("1024", result);
    }

    [TestMethod]
    public void SqrtFunction()
    {
        var result = Evaluator.EvaluateToString("$sqrt(9)", "{}");
        Assert.AreEqual("3", result);
    }

    [TestMethod]
    public void KeysFunction()
    {
        var result = Evaluator.EvaluateToString("$keys({\"a\": 1, \"b\": 2})", "{}");
        Assert.AreEqual("[\"a\",\"b\"]", result);
    }

    [TestMethod]
    public void ValuesFunction()
    {
        var result = Evaluator.EvaluateToString("""$values({"a": 1, "b": 2})""", "{}");
        Assert.AreEqual("[1,2]", result);
    }

    [TestMethod]
    public void AppendFunction()
    {
        var result = Evaluator.EvaluateToString("$append([1, 2], [3, 4])", "{}");
        Assert.AreEqual("[1,2,3,4]", result);
    }

    [TestMethod]
    public void ReverseFunction()
    {
        var result = Evaluator.EvaluateToString("$reverse([1, 2, 3])", "{}");
        Assert.AreEqual("[3,2,1]", result);
    }

    [TestMethod]
    public void FlattenFunction()
    {
        var result = Evaluator.EvaluateToString("$flatten([[1, 2], [3, 4]])", "{}");
        Assert.AreEqual("[1,2,3,4]", result);
    }

    [TestMethod]
    public void LookupFunction()
    {
        var result = Evaluator.EvaluateToString("""$lookup({"a": 1, "b": 2}, "a")""", "{}");
        Assert.AreEqual("1", result);
    }

    [TestMethod]
    public void PathFieldAccess()
    {
        var result = Evaluator.EvaluateToString("Account.Name", """{"Account": {"Name": "Endjin"}}""");
        Assert.AreEqual("\"Endjin\"", result);
    }

    [TestMethod]
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
        Assert.IsNotNull(result);
    }

    // --- Phase 3: Higher-order function tests ---
    [TestMethod]
    public void MapFunction()
    {
        var result = Evaluator.EvaluateToString("""$map([1,2,3], function($v) { $v * 2 })""", "{}");
        Assert.AreEqual("[2,4,6]", result);
    }

    [TestMethod]
    public void MapWithIndex()
    {
        var result = Evaluator.EvaluateToString("""$map(["a","b","c"], function($v, $i) { $i })""", "{}");
        Assert.AreEqual("[0,1,2]", result);
    }

    [TestMethod]
    public void FilterFunction()
    {
        var result = Evaluator.EvaluateToString("""$filter([1,2,3,4,5], function($v) { $v > 2 })""", "{}");
        Assert.AreEqual("[3,4,5]", result);
    }

    [TestMethod]
    public void ReduceFunction()
    {
        var result = Evaluator.EvaluateToString("""$reduce([1,2,3,4], function($acc, $v) { $acc + $v })""", "{}");
        Assert.AreEqual("10", result);
    }

    [TestMethod]
    public void ReduceWithInit()
    {
        var result = Evaluator.EvaluateToString("""$reduce([1,2,3], function($acc, $v) { $acc + $v }, 10)""", "{}");
        Assert.AreEqual("16", result);
    }

    [TestMethod]
    public void EachFunction()
    {
        var result = Evaluator.EvaluateToString("""$each({"a": 1, "b": 2}, function($v, $k) { $v })""", "{}");
        Assert.AreEqual("[1,2]", result);
    }

    [TestMethod]
    public void MergeFunction()
    {
        var result = Evaluator.EvaluateToString("""$merge([{"a":1},{"b":2}])""", "{}");
        // JSON objects are unordered; check semantic equality
        Assert.IsNotNull(result);
        using var parsed = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(result));
        var root = parsed.RootElement;
        Assert.AreEqual(JsonValueKind.Object, root.ValueKind);
        Assert.AreEqual(1, root.GetProperty("a").GetInt32());
        Assert.AreEqual(2, root.GetProperty("b").GetInt32());
    }

    [TestMethod]
    public void SpreadFunction()
    {
        var result = Evaluator.EvaluateToString("""$spread({"a":1,"b":2})""", "{}");
        Assert.AreEqual("""[{"a":1},{"b":2}]""", result);
    }

    [TestMethod]
    public void SortFunctionDefault()
    {
        var result = Evaluator.EvaluateToString("""$sort([3,1,2])""", "{}");
        Assert.AreEqual("[1,2,3]", result);
    }

    [TestMethod]
    public void SortFunctionWithComparator()
    {
        // The reference uses boolean semantics: $a < $b means "swap if a < b" → descending
        var result = Evaluator.EvaluateToString("""$sort([1,3,2], function($a, $b) { $a < $b })""", "{}");
        Assert.AreEqual("[3,2,1]", result);
    }

    [TestMethod]
    public void DistinctFunction()
    {
        var result = Evaluator.EvaluateToString("""$distinct([1,2,2,3,3,3])""", "{}");
        Assert.AreEqual("[1,2,3]", result);
    }

    [TestMethod]
    public void SingleFunction()
    {
        var result = Evaluator.EvaluateToString("""$single([42])""", "{}");
        Assert.AreEqual("42", result);
    }

    [TestMethod]
    public void SingleWithPredicate()
    {
        var result = Evaluator.EvaluateToString("""$single([1,2,3], function($v) { $v = 2 })""", "{}");
        Assert.AreEqual("2", result);
    }

    [TestMethod]
    public void SiftFunction()
    {
        var result = Evaluator.EvaluateToString("""$sift({"a":1,"b":2,"c":3}, function($v) { $v > 1 })""", "{}");
        Assert.AreEqual("""{"b":2,"c":3}""", result);
    }

    // --- Lambda, closure, pipe tests ---
    [TestMethod]
    public void LambdaInvocation()
    {
        var result = Evaluator.EvaluateToString("""($f := function($x) { $x + 1 }; $f(5))""", "{}");
        Assert.AreEqual("6", result);
    }

    [TestMethod]
    public void ClosureCapture()
    {
        var result = Evaluator.EvaluateToString("""($y := 10; $f := function($x) { $x + $y }; $f(5))""", "{}");
        Assert.AreEqual("15", result);
    }

    [TestMethod]
    public void PipeOperator()
    {
        var result = Evaluator.EvaluateToString("""($double := function($x) { $x * 2 }; 5 ~> $double)""", "{}");
        Assert.AreEqual("10", result);
    }

    // --- String function tests ---
    [TestMethod]
    public void PadRight()
    {
        var result = Evaluator.EvaluateToString("""$pad("hi", 5)""", "{}");
        Assert.AreEqual("\"hi   \"", result);
    }

    [TestMethod]
    public void PadLeft()
    {
        var result = Evaluator.EvaluateToString("""$pad("hi", -5, "*")""", "{}");
        Assert.AreEqual("\"***hi\"", result);
    }

    [TestMethod]
    public void ReplaceFunction()
    {
        var result = Evaluator.EvaluateToString("""$replace("hello world", "world", "there")""", "{}");
        Assert.AreEqual("\"hello there\"", result);
    }

    [TestMethod]
    public void ReplaceWithLimit()
    {
        var result = Evaluator.EvaluateToString("""$replace("aaa", "a", "b", 2)""", "{}");
        Assert.AreEqual("\"bba\"", result);
    }

    // --- Encoding function tests ---
    [TestMethod]
    public void Base64Encode()
    {
        var result = Evaluator.EvaluateToString("""$base64encode("hello")""", "{}");
        Assert.AreEqual("\"aGVsbG8=\"", result);
    }

    [TestMethod]
    public void Base64Decode()
    {
        var result = Evaluator.EvaluateToString("""$base64decode("aGVsbG8=")""", "{}");
        Assert.AreEqual("\"hello\"", result);
    }

    [TestMethod]
    public void EncodeUrlComponent()
    {
        var result = Evaluator.EvaluateToString("""$encodeUrlComponent("hello world")""", "{}");
        Assert.AreEqual("\"hello%20world\"", result);
    }

    [TestMethod]
    public void DecodeUrlComponent()
    {
        var result = Evaluator.EvaluateToString("""$decodeUrlComponent("hello%20world")""", "{}");
        Assert.AreEqual("\"hello world\"", result);
    }

    // --- Misc function tests ---
    [TestMethod]
    public void ShuffleDoesNotCrash()
    {
        var result = Evaluator.EvaluateToString("""$shuffle([1,2,3])""", "{}");
        Assert.IsNotNull(result);
        Assert.StartsWith("[", result);
    }

    [TestMethod]
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
                Assert.AreEqual(original, sorted);
                break;
            }
        }

        Assert.IsTrue(foundDifferent, "$shuffle on a property chain through arrays must actually shuffle the elements");
    }

    [TestMethod]
    public void ZipFunction()
    {
        var result = Evaluator.EvaluateToString("""$zip([1,2],[3,4])""", "{}");
        Assert.AreEqual("[[1,3],[2,4]]", result);
    }

    [TestMethod]
    public void ErrorFunctionThrows()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() => Evaluator.EvaluateToString("""$error("boom")""", "{}"));
        StringAssert.Contains(ex.Message, "boom");
    }

    [TestMethod]
    public void AssertPassesOnTrue()
    {
        // $assert returns undefined (no output) on success
        var result = Evaluator.EvaluateToString("""$assert(true)""", "{}");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void AssertFailsOnFalse()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() => Evaluator.EvaluateToString("""$assert(false, "nope")""", "{}"));
        StringAssert.Contains(ex.Message, "nope");
    }

    [TestMethod]
    public void EvalFunction()
    {
        var result = Evaluator.EvaluateToString("""$eval("1 + 2")""", "{}");
        Assert.AreEqual("3", result);
    }

    [TestMethod]
    public void FormatBase()
    {
        var result = Evaluator.EvaluateToString("""$formatBase(255, 16)""", "{}");
        Assert.AreEqual("\"ff\"", result);
    }

    [TestMethod]
    public void NowReturnsString()
    {
        var result = Evaluator.EvaluateToString("""$now()""", "{}");
        Assert.IsNotNull(result);
        Assert.StartsWith("\"", result);
    }

    [TestMethod]
    public void MillisReturnsNumber()
    {
        var result = Evaluator.EvaluateToString("""$millis()""", "{}");
        Assert.IsNotNull(result);
        // Should be a large number (Unix timestamp in ms)
        Assert.IsTrue(double.TryParse(result, out double ms));
        Assert.IsTrue(ms > 1_000_000_000_000);
    }

    // ── Function binding tests ───────────────────────────────

    [TestMethod]
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
        Assert.AreEqual(10.0, result.GetDouble());
    }

    [TestMethod]
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
        Assert.AreEqual(10.0, result.GetDouble());
    }

    [TestMethod]
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
        Assert.AreEqual(100.0, result.GetDouble());
    }

    [TestMethod]
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
        Assert.AreEqual("[3,6,9]", result.GetRawText());
    }

    [TestMethod]
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
        Assert.AreEqual("Hello, world!", result.GetString());
    }

    [TestMethod]
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
        Assert.AreEqual(49.0, result.GetDouble());
    }

    [TestMethod]
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
        var ex = Assert.ThrowsExactly<JsonataException>(() =>
            evaluator.Evaluate("$hypot(3)", doc.RootElement, workspace, bindings));
        Assert.AreEqual("T0410", ex.Code);
        StringAssert.Contains(ex.Message, "expects 2");
        StringAssert.Contains(ex.Message, "got 1");
    }

    [TestMethod]
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
        Assert.AreEqual(5.0, result.GetDouble());
    }

    [TestMethod]
    public void KeepArrayOverArrayWithNoMatchingProperties_ReturnsUndefined()
    {
        // path[] over an array of objects where no object has the property should be undefined
        var result = Evaluator.EvaluateToString(
            """items.nonexistent[]""",
            """{"items": [{"a": 1}, {"b": 2}]}""");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void KeepArrayOverEmptyArray_ReturnsUndefined()
    {
        // path[] over an empty array should be undefined
        var result = Evaluator.EvaluateToString(
            """items.a[]""",
            """{"items": []}""");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void KeepArrayWithDeepNesting_FlattensCorrectly()
    {
        // 3 levels of array nesting: [[[ {a:1} ]]].a[] should flatten to [1]
        var result = Evaluator.EvaluateToString(
            """items.a[]""",
            """{"items": [[[{"a": 1}]]]}""");
        Assert.AreEqual("[1]", result);
    }

    [TestMethod]
    public void KeepArrayWithMixedTypes_SkipsNonObjects()
    {
        // Mixed array: only objects with the property contribute; primitives and nulls are skipped
        var result = Evaluator.EvaluateToString(
            """items.x[]""",
            """{"items": [1, "str", {"x": 5}, null, [{"x": 6}]]}""");
        Assert.AreEqual("[5,6]", result);
    }

    [TestMethod]
    public void KeepArrayWithSingleMatch_WrapsInArray()
    {
        // Single match with [] should still wrap in array
        var result = Evaluator.EvaluateToString(
            """items.a[]""",
            """{"items": [{"a": 42}]}""");
        Assert.AreEqual("[42]", result);
    }

    [TestMethod]
    public void KeepArrayWithArrayPropertyValues_Flattens()
    {
        // Property values that are arrays should be flattened into the result
        var result = Evaluator.EvaluateToString(
            """items.a[]""",
            """{"items": [{"a": [1, 2]}, {"a": [3]}]}""");
        Assert.AreEqual("[1,2,3]", result);
    }

    [TestMethod]
    public void KeepArrayOnObjectInput_WrapsPropertyValue()
    {
        // path[] on an object (not array) should wrap the property value in an array
        var result = Evaluator.EvaluateToString(
            """data.name[]""",
            """{"data": {"name": "Alice"}}""");
        Assert.AreEqual("[\"Alice\"]", result);
    }

    [TestMethod]
    public void KeepArrayOnObjectWithMissingProperty_ReturnsUndefined()
    {
        // path[] on an object where the property doesn't exist should be undefined
        var result = Evaluator.EvaluateToString(
            """data.missing[]""",
            """{"data": {"name": "Alice"}}""");
        Assert.IsNull(result);
    }
}
