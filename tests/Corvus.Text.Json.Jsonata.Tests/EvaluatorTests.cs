// <copyright file="EvaluatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
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
        using var doc = JsonDocument.Parse("""{"a": 1, "b": 2, "c": 3}""");
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
        using var doc = JsonDocument.Parse("""{"x": 1}""");
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
}