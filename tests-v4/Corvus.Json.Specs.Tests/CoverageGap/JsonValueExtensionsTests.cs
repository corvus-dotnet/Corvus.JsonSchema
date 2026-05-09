// <copyright file="JsonValueExtensionsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600

using System.Text.Json;
using Corvus.Json;

namespace Corvus.Json.Specs.Tests.CoverageGap;

[TestClass]
public class JsonValueExtensionsTests
{
    [TestMethod]
    public void AsNullableJsonString_NonNull()
    {
        string input = "hello";
        JsonString? result = input.AsNullableJsonString();
        Assert.IsNotNull(result);
        Assert.AreEqual("hello", (string)result.Value);
    }

    [TestMethod]
    public void AsNullableJsonString_Null()
    {
        string? input = null;
        JsonString? result = input.AsNullableJsonString();
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Clone_ElementBacked()
    {
        JsonAny original = JsonAny.Parse("""{"a":1}""");
        JsonAny cloned = original.Clone();
        Assert.AreEqual(JsonValueKind.Object, cloned.ValueKind);
    }

    [TestMethod]
    public void Clone_DotnetBacked()
    {
        // Dotnet-backed value should be returned as-is
        JsonAny original = new("test");
        Assert.IsFalse(original.HasJsonElementBacking);
        JsonAny cloned = original.Clone();
        Assert.AreEqual("test", (string)cloned.AsString);
    }

    [TestMethod]
    public void Serialize_ElementBacked()
    {
        JsonAny value = JsonAny.Parse("42");
        string result = value.Serialize();
        Assert.AreEqual("42", result);
    }

    [TestMethod]
    public void Serialize_DotnetBacked()
    {
        JsonAny value = new("hello");
        string result = value.Serialize();
        Assert.AreEqual("\"hello\"", result);
    }

    [TestMethod]
    public void Serialize_WithOptions()
    {
        JsonAny value = new("hello");
        var options = new JsonSerializerOptions { WriteIndented = false };
        string result = value.Serialize(options);
        Assert.AreEqual("\"hello\"", result);
    }

    [TestMethod]
    public void AsDotnetBackedValue_Object()
    {
        JsonAny value = JsonAny.Parse("""{"x":1}""");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonAny dotnetBacked = value.AsDotnetBackedValue();
        Assert.IsFalse(dotnetBacked.HasJsonElementBacking);
        Assert.AreEqual(JsonValueKind.Object, dotnetBacked.ValueKind);
    }

    [TestMethod]
    public void AsDotnetBackedValue_Array()
    {
        JsonAny value = JsonAny.Parse("[1,2,3]");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonAny dotnetBacked = value.AsDotnetBackedValue();
        Assert.IsFalse(dotnetBacked.HasJsonElementBacking);
        Assert.AreEqual(JsonValueKind.Array, dotnetBacked.ValueKind);
    }

    [TestMethod]
    public void AsDotnetBackedValue_String()
    {
        JsonAny value = JsonAny.Parse("\"hello\"");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonAny dotnetBacked = value.AsDotnetBackedValue();
        Assert.IsFalse(dotnetBacked.HasJsonElementBacking);
        Assert.AreEqual("hello", (string)dotnetBacked.AsString);
    }

    [TestMethod]
    public void AsDotnetBackedValue_Number()
    {
        JsonAny value = JsonAny.Parse("42");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonAny dotnetBacked = value.AsDotnetBackedValue();
        Assert.IsFalse(dotnetBacked.HasJsonElementBacking);
        Assert.AreEqual(JsonValueKind.Number, dotnetBacked.ValueKind);
    }

    [TestMethod]
    public void AsDotnetBackedValue_True()
    {
        JsonAny value = JsonAny.Parse("true");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonAny dotnetBacked = value.AsDotnetBackedValue();
        Assert.IsFalse(dotnetBacked.HasJsonElementBacking);
        Assert.AreEqual(JsonValueKind.True, dotnetBacked.ValueKind);
    }

    [TestMethod]
    public void AsDotnetBackedValue_False()
    {
        JsonAny value = JsonAny.Parse("false");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonAny dotnetBacked = value.AsDotnetBackedValue();
        Assert.IsFalse(dotnetBacked.HasJsonElementBacking);
        Assert.AreEqual(JsonValueKind.False, dotnetBacked.ValueKind);
    }

    [TestMethod]
    public void AsDotnetBackedValue_Null()
    {
        JsonAny value = JsonAny.Parse("null");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonAny dotnetBacked = value.AsDotnetBackedValue();
        Assert.AreEqual(JsonValueKind.Null, dotnetBacked.ValueKind);
    }

    [TestMethod]
    public void AsDotnetBackedValue_AlreadyDotnet_ReturnsUnchanged()
    {
        JsonAny value = new("test");
        Assert.IsFalse(value.HasJsonElementBacking);
        JsonAny result = value.AsDotnetBackedValue();
        Assert.AreEqual("test", (string)result.AsString);
    }

#if NET8_0_OR_GREATER
    [TestMethod]
    public void AsDotnetBackedValue_JsonDouble()
    {
        JsonDouble value = JsonDouble.Parse("3.14");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonDouble result = value.AsDotnetBackedValue();
        Assert.IsFalse(result.HasJsonElementBacking);
    }

    [TestMethod]
    public void AsDotnetBackedValue_JsonInt32()
    {
        JsonInt32 value = JsonInt32.Parse("42");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonInt32 result = value.AsDotnetBackedValue();
        Assert.IsFalse(result.HasJsonElementBacking);
    }

    [TestMethod]
    public void AsDotnetBackedValue_JsonInt64()
    {
        JsonInt64 value = JsonInt64.Parse("9999999999");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonInt64 result = value.AsDotnetBackedValue();
        Assert.IsFalse(result.HasJsonElementBacking);
    }

    [TestMethod]
    public void AsDotnetBackedValue_JsonByte()
    {
        JsonByte value = JsonByte.Parse("255");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonByte result = value.AsDotnetBackedValue();
        Assert.IsFalse(result.HasJsonElementBacking);
    }

    [TestMethod]
    public void AsDotnetBackedValue_JsonSByte()
    {
        JsonSByte value = JsonSByte.Parse("-1");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonSByte result = value.AsDotnetBackedValue();
        Assert.IsFalse(result.HasJsonElementBacking);
    }

    [TestMethod]
    public void AsDotnetBackedValue_JsonInt16()
    {
        JsonInt16 value = JsonInt16.Parse("1234");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonInt16 result = value.AsDotnetBackedValue();
        Assert.IsFalse(result.HasJsonElementBacking);
    }

    [TestMethod]
    public void AsDotnetBackedValue_JsonUInt16()
    {
        JsonUInt16 value = JsonUInt16.Parse("5678");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonUInt16 result = value.AsDotnetBackedValue();
        Assert.IsFalse(result.HasJsonElementBacking);
    }

    [TestMethod]
    public void AsDotnetBackedValue_JsonUInt32()
    {
        JsonUInt32 value = JsonUInt32.Parse("42");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonUInt32 result = value.AsDotnetBackedValue();
        Assert.IsFalse(result.HasJsonElementBacking);
    }

    [TestMethod]
    public void AsDotnetBackedValue_JsonUInt64()
    {
        JsonUInt64 value = JsonUInt64.Parse("42");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonUInt64 result = value.AsDotnetBackedValue();
        Assert.IsFalse(result.HasJsonElementBacking);
    }

    [TestMethod]
    public void AsDotnetBackedValue_JsonDecimal()
    {
        JsonDecimal value = JsonDecimal.Parse("1.23");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonDecimal result = value.AsDotnetBackedValue();
        Assert.IsFalse(result.HasJsonElementBacking);
    }

    [TestMethod]
    public void AsDotnetBackedValue_JsonSingle()
    {
        JsonSingle value = JsonSingle.Parse("1.5");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonSingle result = value.AsDotnetBackedValue();
        Assert.IsFalse(result.HasJsonElementBacking);
    }

    [TestMethod]
    public void AsDotnetBackedValue_JsonHalf()
    {
        JsonHalf value = JsonHalf.Parse("1.5");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonHalf result = value.AsDotnetBackedValue();
        Assert.IsFalse(result.HasJsonElementBacking);
    }

    [TestMethod]
    public void AsDotnetBackedValue_JsonInt128()
    {
        JsonInt128 value = JsonInt128.Parse("42");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonInt128 result = value.AsDotnetBackedValue();
        Assert.IsFalse(result.HasJsonElementBacking);
    }

    [TestMethod]
    public void AsDotnetBackedValue_JsonUInt128()
    {
        JsonUInt128 value = JsonUInt128.Parse("42");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonUInt128 result = value.AsDotnetBackedValue();
        Assert.IsFalse(result.HasJsonElementBacking);
    }
#endif

    [TestMethod]
    public void AsJsonElementBackedValue_DotnetBacked()
    {
        JsonAny value = new("test");
        Assert.IsFalse(value.HasJsonElementBacking);
        JsonAny result = value.AsJsonElementBackedValue();
        Assert.IsTrue(result.HasJsonElementBacking);
    }

    [TestMethod]
    public void AsJsonElementBackedValue_AlreadyElementBacked()
    {
        JsonAny value = JsonAny.Parse("42");
        Assert.IsTrue(value.HasJsonElementBacking);
        JsonAny result = value.AsJsonElementBackedValue();
        Assert.IsTrue(result.HasJsonElementBacking);
    }

    [TestMethod]
    public void IsValid_ValidValue()
    {
        JsonAny value = JsonAny.Parse("42");
        Assert.IsTrue(value.IsValid());
    }
}