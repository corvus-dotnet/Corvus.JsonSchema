// <copyright file="JsonValueHelpersComparisonTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600

using System.Text;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Internal;

namespace Corvus.Json.Specs.Tests.CoverageGap;

/// <summary>
/// Tests for <see cref="JsonValueHelpers"/> comparison methods.
/// </summary>
[TestClass]
public class JsonValueHelpersComparisonTests
{
    [TestMethod]
    public void CompareWithString_MatchingString()
    {
        JsonString str = JsonString.ParseValue("\"hello\""u8);
        Assert.IsTrue(JsonValueHelpers.CompareWithString(str, "hello".AsSpan()));
    }

    [TestMethod]
    public void CompareWithString_NonMatchingString()
    {
        JsonString str = JsonString.ParseValue("\"hello\""u8);
        Assert.IsFalse(JsonValueHelpers.CompareWithString(str, "world".AsSpan()));
    }

    [TestMethod]
    public void CompareWithString_NonStringValue()
    {
        JsonNumber num = JsonNumber.ParseValue("42"u8);
        Assert.IsFalse(JsonValueHelpers.CompareWithString(num, "42".AsSpan()));
    }

    [TestMethod]
    public void CompareWithUtf8Bytes_MatchingBytes()
    {
        JsonString str = JsonString.ParseValue("\"hello\""u8);
        Assert.IsTrue(JsonValueHelpers.CompareWithUtf8Bytes(str, "hello"u8));
    }

    [TestMethod]
    public void CompareWithUtf8Bytes_NonMatchingBytes()
    {
        JsonString str = JsonString.ParseValue("\"hello\""u8);
        Assert.IsFalse(JsonValueHelpers.CompareWithUtf8Bytes(str, "world"u8));
    }

    [TestMethod]
    public void CompareWithUtf8Bytes_NonStringValue()
    {
        JsonNumber num = JsonNumber.ParseValue("42"u8);
        Assert.IsFalse(JsonValueHelpers.CompareWithUtf8Bytes(num, "42"u8));
    }

    [TestMethod]
    public void CompareValues_Generic_SameStringValues()
    {
        JsonString str1 = JsonString.ParseValue("\"hello\""u8);
        JsonString str2 = JsonString.ParseValue("\"hello\""u8);
        Assert.IsTrue(JsonValueHelpers.CompareValues(str1, str2));
    }

    [TestMethod]
    public void CompareValues_Generic_DifferentStringValues()
    {
        JsonString str1 = JsonString.ParseValue("\"hello\""u8);
        JsonString str2 = JsonString.ParseValue("\"world\""u8);
        Assert.IsFalse(JsonValueHelpers.CompareValues(str1, str2));
    }

    [TestMethod]
    public void CompareValues_Generic_SameNumberValues()
    {
        JsonNumber num1 = JsonNumber.ParseValue("42"u8);
        JsonNumber num2 = JsonNumber.ParseValue("42"u8);
        Assert.IsTrue(JsonValueHelpers.CompareValues(num1, num2));
    }

    [TestMethod]
    public void CompareValues_Generic_DifferentKinds()
    {
        JsonString str = JsonString.ParseValue("\"42\""u8);
        JsonNumber num = JsonNumber.ParseValue("42"u8);
        Assert.IsFalse(JsonValueHelpers.CompareValues(str, num));
    }

    [TestMethod]
    public void CompareValues_Generic_BoolValues()
    {
        JsonBoolean t1 = JsonBoolean.ParseValue("true"u8);
        JsonBoolean t2 = JsonBoolean.ParseValue("true"u8);
        Assert.IsTrue(JsonValueHelpers.CompareValues(t1, t2));
    }

    [TestMethod]
    public void CompareValues_Generic_NullValues()
    {
        JsonNull n1 = JsonNull.ParseValue("null"u8);
        JsonNull n2 = JsonNull.ParseValue("null"u8);
        Assert.IsTrue(JsonValueHelpers.CompareValues(n1, n2));
    }

    [TestMethod]
    public void CompareValues_Generic_ArrayValues()
    {
        JsonArray arr1 = JsonArray.ParseValue("[1,2,3]"u8);
        JsonArray arr2 = JsonArray.ParseValue("[1,2,3]"u8);
        Assert.IsTrue(JsonValueHelpers.CompareValues(arr1, arr2));
    }

    [TestMethod]
    public void CompareValues_Generic_DifferentArrayValues()
    {
        JsonArray arr1 = JsonArray.ParseValue("[1,2,3]"u8);
        JsonArray arr2 = JsonArray.ParseValue("[1,2,4]"u8);
        Assert.IsFalse(JsonValueHelpers.CompareValues(arr1, arr2));
    }

    [TestMethod]
    public void CompareValues_Generic_ObjectValues()
    {
        JsonObject obj1 = JsonObject.ParseValue("""{"a":1}"""u8);
        JsonObject obj2 = JsonObject.ParseValue("""{"a":1}"""u8);
        Assert.IsTrue(JsonValueHelpers.CompareValues(obj1, obj2));
    }

    [TestMethod]
    public void CompareValues_Generic_DifferentObjectValues()
    {
        JsonObject obj1 = JsonObject.ParseValue("""{"a":1}"""u8);
        JsonObject obj2 = JsonObject.ParseValue("""{"a":2}"""u8);
        Assert.IsFalse(JsonValueHelpers.CompareValues(obj1, obj2));
    }

    [TestMethod]
    public void CompareValues_JsonAny_Generic_SameStringValues()
    {
        JsonAny any = JsonAny.ParseValue("\"hello\""u8);
        JsonString str = JsonString.ParseValue("\"hello\""u8);
        Assert.IsTrue(JsonValueHelpers.CompareValues(any, str));
    }

    [TestMethod]
    public void CompareValues_JsonAny_Generic_DifferentKinds()
    {
        JsonAny any = JsonAny.ParseValue("\"hello\""u8);
        JsonNumber num = JsonNumber.ParseValue("42"u8);
        Assert.IsFalse(JsonValueHelpers.CompareValues(any, num));
    }

    [TestMethod]
    public void CompareValues_JsonAny_Generic_ArrayValues()
    {
        JsonAny any = JsonAny.ParseValue("[1,2]"u8);
        JsonArray arr = JsonArray.ParseValue("[1,2]"u8);
        Assert.IsTrue(JsonValueHelpers.CompareValues(any, arr));
    }

    [TestMethod]
    public void CompareValues_JsonAny_Generic_ObjectValues()
    {
        JsonAny any = JsonAny.ParseValue("""{"a":1}"""u8);
        JsonObject obj = JsonObject.ParseValue("""{"a":1}"""u8);
        Assert.IsTrue(JsonValueHelpers.CompareValues(any, obj));
    }

    [TestMethod]
    public void CompareValues_JsonAny_Generic_NumberValues()
    {
        JsonAny any = JsonAny.ParseValue("42"u8);
        JsonNumber num = JsonNumber.ParseValue("42"u8);
        Assert.IsTrue(JsonValueHelpers.CompareValues(any, num));
    }

    [TestMethod]
    public void CompareValues_JsonAny_Generic_NullValues()
    {
        JsonAny any = JsonAny.ParseValue("null"u8);
        JsonNull n = JsonNull.ParseValue("null"u8);
        Assert.IsTrue(JsonValueHelpers.CompareValues(any, n));
    }

    [TestMethod]
    public void CompareValues_JsonAny_JsonAny_SameValues()
    {
        JsonAny a = JsonAny.ParseValue("\"hello\""u8);
        JsonAny b = JsonAny.ParseValue("\"hello\""u8);
        Assert.IsTrue(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_JsonAny_JsonAny_DifferentValues()
    {
        JsonAny a = JsonAny.ParseValue("\"hello\""u8);
        JsonAny b = JsonAny.ParseValue("\"world\""u8);
        Assert.IsFalse(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_JsonAny_JsonAny_Numbers()
    {
        JsonAny a = JsonAny.ParseValue("42"u8);
        JsonAny b = JsonAny.ParseValue("42"u8);
        Assert.IsTrue(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_JsonAny_JsonAny_Arrays()
    {
        JsonAny a = JsonAny.ParseValue("[1,2,3]"u8);
        JsonAny b = JsonAny.ParseValue("[1,2,3]"u8);
        Assert.IsTrue(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_JsonAny_JsonAny_Objects()
    {
        JsonAny a = JsonAny.ParseValue("""{"k":"v"}"""u8);
        JsonAny b = JsonAny.ParseValue("""{"k":"v"}"""u8);
        Assert.IsTrue(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_JsonAny_JsonAny_Null()
    {
        JsonAny a = JsonAny.ParseValue("null"u8);
        JsonAny b = JsonAny.ParseValue("null"u8);
        Assert.IsTrue(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_JsonAny_JsonAny_Booleans()
    {
        JsonAny a = JsonAny.ParseValue("true"u8);
        JsonAny b = JsonAny.ParseValue("true"u8);
        Assert.IsTrue(JsonValueHelpers.CompareValues(a, b));
    }

    [TestMethod]
    public void CompareValues_JsonAny_JsonAny_DifferentKinds()
    {
        JsonAny a = JsonAny.ParseValue("42"u8);
        JsonAny b = JsonAny.ParseValue("\"42\""u8);
        Assert.IsFalse(JsonValueHelpers.CompareValues(a, b));
    }
}