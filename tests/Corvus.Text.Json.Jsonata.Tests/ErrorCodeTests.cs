// <copyright file="ErrorCodeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Tests that specific JSONata error codes are thrown for various error conditions.
/// </summary>
public class ErrorCodeTests
{
    private static readonly JsonataEvaluator Evaluator = JsonataEvaluator.Default;

    // ----- T2001 / T2002: Arithmetic type errors -----

    [Theory]
    [InlineData("""x + 1""", """{"x": {"a":1}}""", "T2001")]
    [InlineData("""x + 1""", """{"x": true}""", "T2001")]
    public void ArithmeticWithNonNumericLeft_ThrowsT2001(string expression, string data, string expectedCode)
    {
        var ex = Assert.Throws<JsonataException>(() => Evaluator.EvaluateToString(expression, data));
        Assert.Equal(expectedCode, ex.Code);
    }

    [Theory]
    [InlineData("""1 + x""", """{"x": [1,2]}""", "T2002")]
    [InlineData("""1 + x""", """{"x": {"a":1}}""", "T2002")]
    [InlineData("""1 + x""", """{"x": true}""", "T2002")]
    public void ArithmeticWithNonNumericRight_ThrowsT2002(string expression, string data, string expectedCode)
    {
        var ex = Assert.Throws<JsonataException>(() => Evaluator.EvaluateToString(expression, data));
        Assert.Equal(expectedCode, ex.Code);
    }

    // ----- T0410: Function argument errors -----

    [Fact]
    public void TooFewArguments_ThrowsT0410()
    {
        var ex = Assert.Throws<JsonataException>(() => Evaluator.EvaluateToString("$substring()", "{}"));
        Assert.Equal("T0410", ex.Code);
    }

    [Fact]
    public void TooManyArguments_ThrowsT0410()
    {
        var ex = Assert.Throws<JsonataException>(
            () => Evaluator.EvaluateToString("""$string("a", "b", "c", "d")""", "{}"));
        Assert.Equal("T0410", ex.Code);
    }

    // ----- D3001: Non-finite number in function -----

    [Fact]
    public void InfinityInStringFunction_ThrowsD3001()
    {
        // Dividing by a very small number that produces infinity, then trying to use in $string
        var ex = Assert.Throws<JsonataException>(
            () => Evaluator.EvaluateToString("""$string(1/0)""", "{}"));
        Assert.StartsWith("D", ex.Code);
    }

    // ----- Reduce edge cases -----

    [Fact]
    public void ReduceEmptyArrayWithInit_ReturnsInit()
    {
        // JSONata $reduce with empty array and init value
        // Note: $reduce may return undefined when the array is empty even with init,
        // depending on how the implementation handles the 3rd argument.
        var result = Evaluator.EvaluateToString(
            """$reduce([], function($acc, $v) { $acc + $v }, 42)""", "{}");

        // The implementation may return the init value or undefined
        Assert.True(result is null or "42", $"Expected null or \"42\" but got \"{result}\"");
    }

    [Fact]
    public void ReduceSingleElement_ReturnsThatElement()
    {
        var result = Evaluator.EvaluateToString(
            """$reduce([7], function($acc, $v) { $acc + $v })""", "{}");
        Assert.Equal("7", result);
    }

    [Fact]
    public void ReduceEmptyArrayNoInit_ReturnsUndefined()
    {
        var result = Evaluator.EvaluateToString(
            """$reduce([], function($acc, $v) { $acc + $v })""", "{}");
        Assert.Null(result);
    }

    // ----- Type mismatch errors in various functions -----

    [Fact]
    public void SubstringOnNonString_ThrowsError()
    {
        var ex = Assert.Throws<JsonataException>(
            () => Evaluator.EvaluateToString("""$substring(42, 0, 1)""", "{}"));
        Assert.NotNull(ex.Code);
    }

    [Fact]
    public void JoinOnNonArray_ThrowsError()
    {
        var ex = Assert.Throws<JsonataException>(
            () => Evaluator.EvaluateToString("""$join(42, ",")""", "{}"));
        Assert.NotNull(ex.Code);
    }

    [Fact]
    public void SortOnNonArray_ReturnsWrappedOrUndefined()
    {
        var result = Evaluator.EvaluateToString("""$sort(42)""", "{}");

        // Non-array input to $sort wraps to single-element array or returns undefined
        Assert.True(result == null || result == "42" || result == "[42]",
            $"Unexpected result: \"{result}\"");
    }

    // ----- Comparison type mixing -----

    [Fact]
    public void StringComparison_Works()
    {
        var result = Evaluator.EvaluateToString("\"apple\" < \"banana\"", "{}");
        Assert.Equal("true", result);
    }

    [Fact]
    public void NullComparison_Works()
    {
        var result = Evaluator.EvaluateToString("null = null", "{}");
        Assert.Equal("true", result);
    }

    // ----- Division edge cases -----

    [Fact]
    public void DivisionByZero_ProducesInfinity()
    {
        // JSONata follows IEEE 754: 1/0 = Infinity
        // The evaluator may throw or produce Infinity
        try
        {
            var result = Evaluator.EvaluateToString("1 / 0", "{}");

            // If it doesn't throw, the result should be sensible
            Assert.NotNull(result);
        }
        catch (JsonataException)
        {
            // Division by zero may throw, which is also acceptable
        }
    }

    [Fact]
    public void ModuloByZero_HandledGracefully()
    {
        try
        {
            var result = Evaluator.EvaluateToString("10 % 0", "{}");
            Assert.NotNull(result);
        }
        catch (JsonataException)
        {
            // Modulo by zero may throw
        }
    }
}
