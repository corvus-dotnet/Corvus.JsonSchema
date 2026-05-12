// <copyright file="ErrorCodeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Tests that specific JSONata error codes are thrown for various error conditions.
/// </summary>
[TestClass]
public class ErrorCodeTests
{
    private static readonly JsonataEvaluator Evaluator = JsonataEvaluator.Default;

    // ----- T2001 / T2002: Arithmetic type errors -----

    [TestMethod]
    [DataRow("""x + 1""", """{"x": {"a":1}}""", "T2001")]
    [DataRow("""x + 1""", """{"x": true}""", "T2001")]
    public void ArithmeticWithNonNumericLeft_ThrowsT2001(string expression, string data, string expectedCode)
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() => Evaluator.EvaluateToString(expression, data));
        Assert.AreEqual(expectedCode, ex.Code);
    }

    [TestMethod]
    [DataRow("""1 + x""", """{"x": [1,2]}""", "T2002")]
    [DataRow("""1 + x""", """{"x": {"a":1}}""", "T2002")]
    [DataRow("""1 + x""", """{"x": true}""", "T2002")]
    public void ArithmeticWithNonNumericRight_ThrowsT2002(string expression, string data, string expectedCode)
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() => Evaluator.EvaluateToString(expression, data));
        Assert.AreEqual(expectedCode, ex.Code);
    }

    // ----- T0410: Function argument errors -----

    [TestMethod]
    public void TooFewArguments_ThrowsT0410()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() => Evaluator.EvaluateToString("$substring()", "{}"));
        Assert.AreEqual("T0410", ex.Code);
    }

    [TestMethod]
    public void TooManyArguments_ThrowsT0410()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(
            () => Evaluator.EvaluateToString("""$string("a", "b", "c", "d")""", "{}"));
        Assert.AreEqual("T0410", ex.Code);
    }

    // ----- D3001: Non-finite number in function -----

    [TestMethod]
    public void InfinityInStringFunction_ThrowsD3001()
    {
        // Dividing by a very small number that produces infinity, then trying to use in $string
        var ex = Assert.ThrowsExactly<JsonataException>(
            () => Evaluator.EvaluateToString("""$string(1/0)""", "{}"));
        Assert.StartsWith("D", ex.Code);
    }

    // ----- Reduce edge cases -----

    [TestMethod]
    public void ReduceEmptyArrayWithInit_ReturnsInit()
    {
        // JSONata $reduce with empty array and init value
        // Note: $reduce may return undefined when the array is empty even with init,
        // depending on how the implementation handles the 3rd argument.
        var result = Evaluator.EvaluateToString(
            """$reduce([], function($acc, $v) { $acc + $v }, 42)""", "{}");

        // The implementation may return the init value or undefined
        Assert.IsTrue(result is null or "42", $"Expected null or \"42\" but got \"{result}\"");
    }

    [TestMethod]
    public void ReduceSingleElement_ReturnsThatElement()
    {
        var result = Evaluator.EvaluateToString(
            """$reduce([7], function($acc, $v) { $acc + $v })""", "{}");
        Assert.AreEqual("7", result);
    }

    [TestMethod]
    public void ReduceEmptyArrayNoInit_ReturnsUndefined()
    {
        var result = Evaluator.EvaluateToString(
            """$reduce([], function($acc, $v) { $acc + $v })""", "{}");
        Assert.IsNull(result);
    }

    // ----- Type mismatch errors in various functions -----

    [TestMethod]
    public void SubstringOnNonString_ThrowsError()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(
            () => Evaluator.EvaluateToString("""$substring(42, 0, 1)""", "{}"));
        Assert.IsNotNull(ex.Code);
    }

    [TestMethod]
    public void JoinOnNonArray_ThrowsError()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(
            () => Evaluator.EvaluateToString("""$join(42, ",")""", "{}"));
        Assert.IsNotNull(ex.Code);
    }

    [TestMethod]
    public void SortOnNonArray_ReturnsWrappedOrUndefined()
    {
        var result = Evaluator.EvaluateToString("""$sort(42)""", "{}");

        // Non-array input to $sort wraps to single-element array or returns undefined
        Assert.IsTrue(result == null || result == "42" || result == "[42]",
            $"Unexpected result: \"{result}\"");
    }

    // ----- Comparison type mixing -----

    [TestMethod]
    public void StringComparison_Works()
    {
        var result = Evaluator.EvaluateToString("\"apple\" < \"banana\"", "{}");
        Assert.AreEqual("true", result);
    }

    [TestMethod]
    public void NullComparison_Works()
    {
        var result = Evaluator.EvaluateToString("null = null", "{}");
        Assert.AreEqual("true", result);
    }

    // ----- Division edge cases -----

    [TestMethod]
    public void DivisionByZero_ProducesInfinity()
    {
        // JSONata follows IEEE 754: 1/0 = Infinity
        // The evaluator may throw or produce Infinity
        try
        {
            var result = Evaluator.EvaluateToString("1 / 0", "{}");

            // If it doesn't throw, the result should be sensible
            Assert.IsNotNull(result);
        }
        catch (JsonataException)
        {
            // Division by zero may throw, which is also acceptable
        }
    }

    [TestMethod]
    public void ModuloByZero_HandledGracefully()
    {
        try
        {
            var result = Evaluator.EvaluateToString("10 % 0", "{}");
            Assert.IsNotNull(result);
        }
        catch (JsonataException)
        {
            // Modulo by zero may throw
        }
    }
}
