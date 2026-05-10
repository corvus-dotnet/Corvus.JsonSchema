// <copyright file="JsonValueHelpersHashcodeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using Corvus.Json;
using Corvus.Json.Internal;

namespace CoverageGap;

/// <summary>
/// Tests for <see cref="JsonValueHelpers"/> hash code methods
/// targeting uncovered switch arms in GetHashCode{T} and GetHashCode(JsonAny).
/// </summary>
[TestClass]
public class JsonValueHelpersHashcodeTests
{
    [TestMethod]
    public void GetHashCode_Generic_Array()
    {
        var value = JsonAny.Parse("[1,2,3]");
        int hash = JsonValueHelpers.GetHashCode(value);
        Assert.AreNotEqual(0, hash);
    }

    [TestMethod]
    public void GetHashCode_Generic_Object()
    {
        var value = JsonAny.Parse("""{"a":1}""");
        int hash = JsonValueHelpers.GetHashCode(value);
        Assert.AreNotEqual(0, hash);
    }

    [TestMethod]
    public void GetHashCode_Generic_Number()
    {
        var value = JsonAny.Parse("42");
        int hash = JsonValueHelpers.GetHashCode(value);
        Assert.AreNotEqual(0, hash);
    }

    [TestMethod]
    public void GetHashCode_Generic_String()
    {
        var value = JsonAny.Parse("\"hello\"");
        int hash = JsonValueHelpers.GetHashCode(value);
        Assert.AreNotEqual(0, hash);
    }

    [TestMethod]
    public void GetHashCode_Generic_True()
    {
        var value = JsonAny.Parse("true");
        int hash = JsonValueHelpers.GetHashCode(value);
        Assert.AreEqual(true.GetHashCode(), hash);
    }

    [TestMethod]
    public void GetHashCode_Generic_False()
    {
        var value = JsonAny.Parse("false");
        int hash = JsonValueHelpers.GetHashCode(value);
        Assert.AreEqual(false.GetHashCode(), hash);
    }

    [TestMethod]
    public void GetHashCode_Generic_Null()
    {
        var value = JsonAny.Parse("null");
        int hash = JsonValueHelpers.GetHashCode(value);
        Assert.AreEqual(JsonValueHelpers.NullHashCode, hash);
    }

    [TestMethod]
    public void GetHashCode_Generic_Undefined()
    {
        var value = JsonAny.Undefined;
        int hash = JsonValueHelpers.GetHashCode(value);
        Assert.AreEqual(JsonValueHelpers.UndefinedHashCode, hash);
    }

    [TestMethod]
    public void GetHashCode_JsonAny_Array()
    {
        JsonAny value = JsonAny.Parse("[10,20]");
        int hash = JsonValueHelpers.GetHashCode(in value);
        Assert.AreNotEqual(0, hash);
    }

    [TestMethod]
    public void GetHashCode_JsonAny_Object()
    {
        JsonAny value = JsonAny.Parse("""{"x":"y"}""");
        int hash = JsonValueHelpers.GetHashCode(in value);
        Assert.AreNotEqual(0, hash);
    }

    [TestMethod]
    public void GetHashCode_JsonAny_Number()
    {
        JsonAny value = JsonAny.Parse("99");
        int hash = JsonValueHelpers.GetHashCode(in value);
        Assert.AreNotEqual(0, hash);
    }

    [TestMethod]
    public void GetHashCode_JsonAny_String()
    {
        JsonAny value = JsonAny.Parse("\"world\"");
        int hash = JsonValueHelpers.GetHashCode(in value);
        Assert.AreNotEqual(0, hash);
    }

    [TestMethod]
    public void GetHashCode_JsonAny_True()
    {
        JsonAny value = JsonAny.Parse("true");
        int hash = JsonValueHelpers.GetHashCode(in value);
        Assert.AreEqual(true.GetHashCode(), hash);
    }

    [TestMethod]
    public void GetHashCode_JsonAny_False()
    {
        JsonAny value = JsonAny.Parse("false");
        int hash = JsonValueHelpers.GetHashCode(in value);
        Assert.AreEqual(false.GetHashCode(), hash);
    }

    [TestMethod]
    public void GetHashCode_JsonAny_Null()
    {
        JsonAny value = JsonAny.Parse("null");
        int hash = JsonValueHelpers.GetHashCode(in value);
        Assert.AreEqual(JsonValueHelpers.NullHashCode, hash);
    }

    [TestMethod]
    public void GetHashCode_JsonAny_Undefined()
    {
        JsonAny value = JsonAny.Undefined;
        int hash = JsonValueHelpers.GetHashCode(in value);
        Assert.AreEqual(JsonValueHelpers.UndefinedHashCode, hash);
    }

    [TestMethod]
    public void GetArrayHashCode_EmptyArray()
    {
        var value = JsonAny.Parse("[]");
        int hash = JsonValueHelpers.GetArrayHashCode(value.AsArray);
        int hash2 = JsonValueHelpers.GetArrayHashCode(value.AsArray);
        Assert.AreEqual(hash, hash2);
    }

    [TestMethod]
    public void GetArrayHashCode_DifferentArraysDifferentHash()
    {
        var a1 = JsonAny.Parse("[1,2]");
        var a2 = JsonAny.Parse("[3,4]");
        int hash1 = JsonValueHelpers.GetArrayHashCode(a1.AsArray);
        int hash2 = JsonValueHelpers.GetArrayHashCode(a2.AsArray);
        Assert.AreNotEqual(hash1, hash2);
    }

    [TestMethod]
    public void GetObjectHashCode_SamePropertiesDifferentOrder()
    {
        var o1 = JsonAny.Parse("""{"a":1,"b":2}""");
        var o2 = JsonAny.Parse("""{"b":2,"a":1}""");
        int hash1 = JsonValueHelpers.GetObjectHashCode(o1.AsObject);
        int hash2 = JsonValueHelpers.GetObjectHashCode(o2.AsObject);
        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void GetHashCodeForString_ProducesDeterministicHash()
    {
        var s1 = JsonAny.Parse("\"test\"");
        var s2 = JsonAny.Parse("\"test\"");
        int hash1 = JsonValueHelpers.GetHashCodeForString(s1.AsString);
        int hash2 = JsonValueHelpers.GetHashCodeForString(s2.AsString);
        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void GetHashCodeForNumber_ProducesDeterministicHash()
    {
        var n1 = JsonAny.Parse("3.14");
        var n2 = JsonAny.Parse("3.14");
        int hash1 = JsonValueHelpers.GetHashCodeForNumber(n1.AsNumber);
        int hash2 = JsonValueHelpers.GetHashCodeForNumber(n2.AsNumber);
        Assert.AreEqual(hash1, hash2);
    }
}