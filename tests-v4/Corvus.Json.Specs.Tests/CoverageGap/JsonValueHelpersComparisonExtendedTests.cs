// <copyright file="JsonValueHelpersComparisonExtendedTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Text;
using Corvus.Json;
using Corvus.Json.Internal;

namespace CoverageGap;

/// <summary>
/// Extended tests for <see cref="JsonValueHelpers"/> comparison methods
/// targeting uncovered switch arms and comparison paths.
/// </summary>
[TestClass]
public class JsonValueHelpersComparisonExtendedTests
{
    [TestMethod]
    public void CompareWithString_MatchingString()
    {
        var item = JsonAny.Parse("\"hello\"");
        Assert.IsTrue(JsonValueHelpers.CompareWithString(in item, "hello".AsSpan()));
    }

    [TestMethod]
    public void CompareWithString_NonMatchingString()
    {
        var item = JsonAny.Parse("\"hello\"");
        Assert.IsFalse(JsonValueHelpers.CompareWithString(in item, "world".AsSpan()));
    }

    [TestMethod]
    public void CompareWithString_NotAString_ReturnsFalse()
    {
        var item = JsonAny.Parse("42");
        Assert.IsFalse(JsonValueHelpers.CompareWithString(in item, "42".AsSpan()));
    }

    [TestMethod]
    public void CompareWithString_NullValue_ReturnsFalse()
    {
        var item = JsonAny.Parse("null");
        Assert.IsFalse(JsonValueHelpers.CompareWithString(in item, "null".AsSpan()));
    }

    [TestMethod]
    public void CompareWithUtf8Bytes_MatchingString()
    {
        var item = JsonAny.Parse("\"hello\"");
        Assert.IsTrue(JsonValueHelpers.CompareWithUtf8Bytes(in item, Encoding.UTF8.GetBytes("hello")));
    }

    [TestMethod]
    public void CompareWithUtf8Bytes_NonMatchingString()
    {
        var item = JsonAny.Parse("\"hello\"");
        Assert.IsFalse(JsonValueHelpers.CompareWithUtf8Bytes(in item, Encoding.UTF8.GetBytes("world")));
    }

    [TestMethod]
    public void CompareWithUtf8Bytes_NotAString_ReturnsFalse()
    {
        var item = JsonAny.Parse("[1]");
        Assert.IsFalse(JsonValueHelpers.CompareWithUtf8Bytes(in item, Encoding.UTF8.GetBytes("[1]")));
    }

    [TestMethod]
    public void CompareValues_Array_Equal()
    {
        var a1 = JsonAny.Parse("[1,2,3]");
        var a2 = JsonAny.Parse("[1,2,3]");
        Assert.IsTrue(JsonValueHelpers.CompareValues(in a1, in a2));
    }

    [TestMethod]
    public void CompareValues_Array_NotEqual()
    {
        var a1 = JsonAny.Parse("[1,2]");
        var a2 = JsonAny.Parse("[1,3]");
        Assert.IsFalse(JsonValueHelpers.CompareValues(in a1, in a2));
    }

    [TestMethod]
    public void CompareValues_BoolTrue_Equal()
    {
        var a = JsonAny.Parse("true");
        var b = JsonAny.Parse("true");
        Assert.IsTrue(JsonValueHelpers.CompareValues(in a, in b));
    }

    [TestMethod]
    public void CompareValues_BoolFalse_Equal()
    {
        var a = JsonAny.Parse("false");
        var b = JsonAny.Parse("false");
        Assert.IsTrue(JsonValueHelpers.CompareValues(in a, in b));
    }

    [TestMethod]
    public void CompareValues_Null_Equal()
    {
        var a = JsonAny.Parse("null");
        var b = JsonAny.Parse("null");
        Assert.IsTrue(JsonValueHelpers.CompareValues(in a, in b));
    }

    [TestMethod]
    public void CompareValues_Undefined_ReturnsFalse()
    {
        // For the generic overload, Undefined returns false
        var a = JsonAny.Undefined;
        var b = JsonAny.Undefined;
        Assert.IsFalse(JsonValueHelpers.CompareValues<JsonAny, JsonAny>(in a, in b));
    }

    [TestMethod]
    public void CompareValues_Number_Equal()
    {
        var a = JsonAny.Parse("42");
        var b = JsonAny.Parse("42");
        Assert.IsTrue(JsonValueHelpers.CompareValues(in a, in b));
    }

    [TestMethod]
    public void CompareValues_Object_Equal()
    {
        var a = JsonAny.Parse("""{"a":1}""");
        var b = JsonAny.Parse("""{"a":1}""");
        Assert.IsTrue(JsonValueHelpers.CompareValues(in a, in b));
    }

    [TestMethod]
    public void CompareValues_String_Equal()
    {
        var a = JsonAny.Parse("\"hello\"");
        var b = JsonAny.Parse("\"hello\"");
        Assert.IsTrue(JsonValueHelpers.CompareValues(in a, in b));
    }

    [TestMethod]
    public void CompareValues_DifferentKinds_ReturnsFalse()
    {
        var a = JsonAny.Parse("42");
        var b = JsonAny.Parse("\"42\"");
        Assert.IsFalse(JsonValueHelpers.CompareValues(in a, in b));
    }

    [TestMethod]
    public void CompareValues_TypedToJsonAny_Array()
    {
        var a = JsonAny.Parse("[1]");
        JsonAny b = JsonAny.Parse("[1]");
        Assert.IsTrue(JsonValueHelpers.CompareValues(in a, in b));
    }

    [TestMethod]
    public void CompareValues_TypedToJsonAny_Object()
    {
        var a = JsonAny.Parse("""{"x":1}""");
        JsonAny b = JsonAny.Parse("""{"x":1}""");
        Assert.IsTrue(JsonValueHelpers.CompareValues(in a, in b));
    }

    [TestMethod]
    public void CompareValues_TypedToJsonAny_String()
    {
        var a = JsonAny.Parse("\"hi\"");
        JsonAny b = JsonAny.Parse("\"hi\"");
        Assert.IsTrue(JsonValueHelpers.CompareValues(in a, in b));
    }

    [TestMethod]
    public void CompareValues_TypedToJsonAny_DifferentKinds()
    {
        var a = JsonAny.Parse("true");
        JsonAny b = JsonAny.Parse("false");
        Assert.IsFalse(JsonValueHelpers.CompareValues(in a, in b));
    }

    [TestMethod]
    public void CompareValues_JsonAnyJsonAny_Undefined_ReturnsTrue()
    {
        // For the JsonAny-JsonAny overload, Undefined returns true
        JsonAny a = JsonAny.Undefined;
        JsonAny b = JsonAny.Undefined;
        Assert.IsTrue(JsonValueHelpers.CompareValues(in a, in b));
    }

    [TestMethod]
    public void CompareValues_JsonAnyJsonAny_Array()
    {
        JsonAny a = JsonAny.Parse("[1,2]");
        JsonAny b = JsonAny.Parse("[1,2]");
        Assert.IsTrue(JsonValueHelpers.CompareValues(in a, in b));
    }

    [TestMethod]
    public void CompareValues_JsonAnyJsonAny_Number()
    {
        JsonAny a = JsonAny.Parse("99");
        JsonAny b = JsonAny.Parse("99");
        Assert.IsTrue(JsonValueHelpers.CompareValues(in a, in b));
    }

    [TestMethod]
    public void CompareObjects_DifferentProperties_ReturnsFalse()
    {
        var a = JsonAny.Parse("""{"a":1}""");
        var b = JsonAny.Parse("""{"a":1,"b":2}""");
        Assert.IsFalse(JsonValueHelpers.CompareObjects(a.AsObject, b.AsObject));
    }

    [TestMethod]
    public void CompareObjects_MissingProperty_ReturnsFalse()
    {
        var a = JsonAny.Parse("""{"a":1,"b":2}""");
        var b = JsonAny.Parse("""{"a":1,"c":3}""");
        Assert.IsFalse(JsonValueHelpers.CompareObjects(a.AsObject, b.AsObject));
    }

    [TestMethod]
    public void CompareObjects_MorePropertiesInSecond_ReturnsFalse()
    {
        var a = JsonAny.Parse("""{"a":1}""");
        var b = JsonAny.Parse("""{"a":1,"b":2,"c":3}""");
        Assert.IsFalse(JsonValueHelpers.CompareObjects(a.AsObject, b.AsObject));
    }

    [TestMethod]
    public void CompareArrays_SecondLonger_ReturnsFalse()
    {
        var a = JsonAny.Parse("[1]");
        var b = JsonAny.Parse("[1,2]");
        Assert.IsFalse(JsonValueHelpers.CompareArrays(a.AsArray, b.AsArray));
    }

    [TestMethod]
    public void CompareArrays_FirstLonger_ReturnsFalse()
    {
        var a = JsonAny.Parse("[1,2,3]");
        var b = JsonAny.Parse("[1,2]");
        Assert.IsFalse(JsonValueHelpers.CompareArrays(a.AsArray, b.AsArray));
    }

    [TestMethod]
    public void CompareArrays_ElementMismatch_ReturnsFalse()
    {
        var a = JsonAny.Parse("[1,2]");
        var b = JsonAny.Parse("[1,3]");
        Assert.IsFalse(JsonValueHelpers.CompareArrays(a.AsArray, b.AsArray));
    }

    [TestMethod]
    public void CompareStrings_BothDotnetBacked()
    {
        // JsonString created from .NET string (has dotnet backing)
        var s1 = new JsonString("hello");
        var s2 = new JsonString("hello");
        Assert.IsTrue(JsonValueHelpers.CompareStrings(in s1, in s2));
    }

    [TestMethod]
    public void CompareStrings_BothDotnetBacked_NotEqual()
    {
        var s1 = new JsonString("hello");
        var s2 = new JsonString("world");
        Assert.IsFalse(JsonValueHelpers.CompareStrings(in s1, in s2));
    }

    [TestMethod]
    public void CompareStrings_JsonElementBacked_Vs_DotnetBacked()
    {
        // One from parse (JsonElement backed), one from .NET string
        var s1 = JsonAny.Parse("\"hello\"").AsString;
        var s2 = new JsonString("hello");
        Assert.IsTrue(JsonValueHelpers.CompareStrings(in s1, in s2));
    }

    [TestMethod]
    public void CompareStrings_DotnetBacked_Vs_JsonElementBacked()
    {
        var s1 = new JsonString("hello");
        var s2 = JsonAny.Parse("\"hello\"").AsString;
        Assert.IsTrue(JsonValueHelpers.CompareStrings(in s1, in s2));
    }

    [TestMethod]
    public void CompareStrings_BothJsonElementBacked()
    {
        var s1 = JsonAny.Parse("\"hello\"").AsString;
        var s2 = JsonAny.Parse("\"hello\"").AsString;
        Assert.IsTrue(JsonValueHelpers.CompareStrings(in s1, in s2));
    }

    [TestMethod]
    public void CompareStrings_BothJsonElementBacked_NotEqual()
    {
        var s1 = JsonAny.Parse("\"hello\"").AsString;
        var s2 = JsonAny.Parse("\"world\"").AsString;
        Assert.IsFalse(JsonValueHelpers.CompareStrings(in s1, in s2));
    }

    [TestMethod]
    public void CompareNumbers_BothDotnetBacked()
    {
        var n1 = new JsonNumber(new BinaryJsonNumber(42));
        var n2 = new JsonNumber(new BinaryJsonNumber(42));
        Assert.IsTrue(JsonValueHelpers.CompareNumbers(in n1, in n2));
    }

    [TestMethod]
    public void CompareNumbers_DotnetVsJsonElement()
    {
        var n1 = new JsonNumber(new BinaryJsonNumber(42));
        var n2 = JsonAny.Parse("42").AsNumber;
        Assert.IsTrue(JsonValueHelpers.CompareNumbers(in n1, in n2));
    }

    [TestMethod]
    public void CompareNumbers_JsonElementVsDotnet()
    {
        var n1 = JsonAny.Parse("42").AsNumber;
        var n2 = new JsonNumber(new BinaryJsonNumber(42));
        Assert.IsTrue(JsonValueHelpers.CompareNumbers(in n1, in n2));
    }

    [TestMethod]
    public void CompareNumbers_BothJsonElement()
    {
        var n1 = JsonAny.Parse("42").AsNumber;
        var n2 = JsonAny.Parse("42").AsNumber;
        Assert.IsTrue(JsonValueHelpers.CompareNumbers(in n1, in n2));
    }

    [TestMethod]
    public void CompareNumbers_DifferentKinds_ReturnsFalse()
    {
        var n1 = JsonAny.Parse("42").AsNumber;
        var n2 = JsonAny.Null.AsNumber;
        Assert.IsFalse(JsonValueHelpers.CompareNumbers(in n1, in n2));
    }

    [TestMethod]
    public void CompareNumbers_BothNull_ReturnsTrue()
    {
        var n1 = JsonAny.Null.AsNumber;
        var n2 = JsonAny.Null.AsNumber;
        Assert.IsTrue(JsonValueHelpers.CompareNumbers(in n1, in n2));
    }
}