// <copyright file="JsonIntegerComparisonTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET
using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.JsonModel.NumericComparison;

[TestClass]
public class JsonIntegerComparisonTests
{
    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_1_false_JsonInteger()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8);
        bool result = sut < new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_2_1_false_JsonInteger()
    {
        JsonInteger sut = JsonInteger.ParseValue("2"u8);
        bool result = sut < new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_3_true_JsonInteger()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8);
        bool result = sut < new JsonInteger(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_long_MaxValue_true_JsonI()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8);
        bool result = sut < new JsonInteger(long.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_long_MinValue_false_Json()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8);
        bool result = sut < new JsonInteger(long.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_null_false_JsonIntege()
    {
        JsonInteger sut = JsonInteger.ParseValue("null"u8);
        bool result = sut < JsonInteger.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_1_false_JsonInteger()
    {
        JsonInteger sut = JsonInteger.ParseValue("null"u8);
        bool result = sut < new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_1_false_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8);
        bool result = sut < new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_2_1_false_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("2"u8);
        bool result = sut < new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_3_true_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8);
        bool result = sut < new JsonInt64(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_long_MaxValue_true_JsonI_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8);
        bool result = sut < new JsonInt64(long.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_long_MinValue_false_Json_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8);
        bool result = sut < new JsonInt64(long.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_null_false_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("null"u8);
        bool result = sut < JsonInt64.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_1_false_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("null"u8);
        bool result = sut < new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_1_false_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8);
        bool result = sut < new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_2_1_false_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("2"u8);
        bool result = sut < new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_3_true_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8);
        bool result = sut < new JsonInt32(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_int_MaxValue_true_JsonIn()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8);
        bool result = sut < new JsonInt32(int.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_int_MinValue_false_JsonI()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8);
        bool result = sut < new JsonInt32(int.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_null_false_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("null"u8);
        bool result = sut < JsonInt32.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_1_false_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("null"u8);
        bool result = sut < new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_1_false_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8);
        bool result = sut < new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_2_1_false_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("2"u8);
        bool result = sut < new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_3_true_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8);
        bool result = sut < new JsonInt16(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_short_MaxValue_true_Json()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8);
        bool result = sut < new JsonInt16(short.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_short_MinValue_false_Jso()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8);
        bool result = sut < new JsonInt16(short.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_null_false_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("null"u8);
        bool result = sut < JsonInt16.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_1_false_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("null"u8);
        bool result = sut < new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_1_false_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8);
        bool result = sut < new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_2_1_false_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("2"u8);
        bool result = sut < new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_3_true_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8);
        bool result = sut < new JsonSByte(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_sbyte_MaxValue_true_Json()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8);
        bool result = sut < new JsonSByte(sbyte.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_sbyte_MinValue_false_Jso()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8);
        bool result = sut < new JsonSByte(sbyte.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_null_false_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("null"u8);
        bool result = sut < JsonSByte.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_1_false_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("null"u8);
        bool result = sut < new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_1_false_JsonUInt64()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8);
        bool result = sut < new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_2_1_false_JsonUInt64()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("2"u8);
        bool result = sut < new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_3_true_JsonUInt64()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8);
        bool result = sut < new JsonUInt64(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_ulong_MaxValue_true_Json()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8);
        bool result = sut < new JsonUInt64(ulong.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_ulong_MinValue_false_Jso()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8);
        bool result = sut < new JsonUInt64(ulong.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_null_false_JsonUInt64()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("null"u8);
        bool result = sut < JsonUInt64.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_1_false_JsonUInt64()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("null"u8);
        bool result = sut < new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_1_false_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8);
        bool result = sut < new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_2_1_false_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("2"u8);
        bool result = sut < new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_3_true_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8);
        bool result = sut < new JsonUInt32(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_uint_MaxValue_true_JsonU()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8);
        bool result = sut < new JsonUInt32(uint.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_uint_MinValue_false_Json()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8);
        bool result = sut < new JsonUInt32(uint.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_null_false_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("null"u8);
        bool result = sut < JsonUInt32.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_1_false_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("null"u8);
        bool result = sut < new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_1_false_JsonUInt16()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8);
        bool result = sut < new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_2_1_false_JsonUInt16()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("2"u8);
        bool result = sut < new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_3_true_JsonUInt16()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8);
        bool result = sut < new JsonUInt16(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_ushort_MaxValue_true_Jso()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8);
        bool result = sut < new JsonUInt16(ushort.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_ushort_MinValue_false_Js()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8);
        bool result = sut < new JsonUInt16(ushort.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_null_false_JsonUInt16()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("null"u8);
        bool result = sut < JsonUInt16.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_1_false_JsonUInt16()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("null"u8);
        bool result = sut < new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_1_false_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8);
        bool result = sut < new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_2_1_false_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("2"u8);
        bool result = sut < new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_3_true_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8);
        bool result = sut < new JsonByte(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_byte_MaxValue_true_JsonB()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8);
        bool result = sut < new JsonByte(byte.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_byte_MinValue_false_Json()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8);
        bool result = sut < new JsonByte(byte.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_null_false_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("null"u8);
        bool result = sut < JsonByte.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_1_false_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("null"u8);
        bool result = sut < new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_1_false_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8);
        bool result = sut < new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_2_1_false_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.ParseValue("2"u8);
        bool result = sut < new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_3_true_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8);
        bool result = sut < new JsonInt128(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_Int128_MaxValue_true_Jso()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8);
        bool result = sut < new JsonInt128(Int128.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_Int128_MinValue_false_Js()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8);
        bool result = sut < new JsonInt128(Int128.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_null_false_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.ParseValue("null"u8);
        bool result = sut < JsonInt128.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_1_false_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.ParseValue("null"u8);
        bool result = sut < new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_1_false_JsonUInt128()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8);
        bool result = sut < new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_2_1_false_JsonUInt128()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("2"u8);
        bool result = sut < new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_3_true_JsonUInt128()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8);
        bool result = sut < new JsonUInt128(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_UInt128_MaxValue_true_Js()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8);
        bool result = sut < new JsonUInt128(UInt128.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_1_UInt128_MinValue_false_J()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8);
        bool result = sut < new JsonUInt128(UInt128.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_null_false_JsonUInt12()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("null"u8);
        bool result = sut < JsonUInt128.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_an_integer_null_1_false_JsonUInt128()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("null"u8);
        bool result = sut < new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_1_false_JsonInteger()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_2_1_false_JsonInteger()
    {
        JsonInteger sut = JsonInteger.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_3_true_JsonInteger()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInteger(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_long_MaxValue_true_JsonInteger()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInteger(long.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_long_MinValue_false_JsonIntege()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInteger(long.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_1_false_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_2_1_false_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_3_true_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt64(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_long_MaxValue_true_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt64(long.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_long_MinValue_false_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt64(long.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_1_false_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_2_1_false_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_3_true_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt32(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_int_MaxValue_true_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt32(int.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_int_MinValue_false_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt32(int.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_1_false_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_2_1_false_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_3_true_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt16(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_short_MaxValue_true_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt16(short.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_short_MinValue_false_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt16(short.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_1_false_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_2_1_false_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut < new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_3_true_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonSByte(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_sbyte_MaxValue_true_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonSByte(sbyte.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_sbyte_MinValue_false_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonSByte(sbyte.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_1_false_JsonUInt64()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_2_1_false_JsonUInt64()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_3_true_JsonUInt64()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt64(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_ulong_MaxValue_true_JsonUInt64()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt64(ulong.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_ulong_MinValue_false_JsonUInt6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt64(ulong.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_1_false_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_2_1_false_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_3_true_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt32(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_uint_MaxValue_true_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt32(uint.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_uint_MinValue_false_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt32(uint.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_1_false_JsonUInt16()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_2_1_false_JsonUInt16()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_3_true_JsonUInt16()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt16(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_ushort_MaxValue_true_JsonUInt1()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt16(ushort.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_ushort_MinValue_false_JsonUInt()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt16(ushort.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_1_false_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_2_1_false_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut < new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_3_true_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonByte(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_byte_MaxValue_true_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonByte(byte.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_byte_MinValue_false_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonByte(byte.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_1_false_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_2_1_false_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_3_true_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt128(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_Int128_MaxValue_true_JsonInt12()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt128(Int128.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_Int128_MinValue_false_JsonInt1()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonInt128(Int128.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_1_false_JsonUInt128()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_2_1_false_JsonUInt128()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_3_true_JsonUInt128()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt128(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_UInt128_MaxValue_true_JsonUInt()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt128(UInt128.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_an_integer_1_UInt128_MinValue_false_JsonUIn()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < new JsonUInt128(UInt128.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_1_false_JsonInteger()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8);
        bool result = sut > new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_2_1_true_JsonInteger()
    {
        JsonInteger sut = JsonInteger.ParseValue("2"u8);
        bool result = sut > new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_3_false_JsonInteger()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8);
        bool result = sut > new JsonInteger(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_long_MaxValue_false_Js()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8);
        bool result = sut > new JsonInteger(long.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_long_MinValue_true_Jso()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8);
        bool result = sut > new JsonInteger(long.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_null_false_JsonInte()
    {
        JsonInteger sut = JsonInteger.ParseValue("null"u8);
        bool result = sut > JsonInteger.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_1_false_JsonInteger()
    {
        JsonInteger sut = JsonInteger.ParseValue("null"u8);
        bool result = sut > new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_1_false_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8);
        bool result = sut > new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_2_1_true_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("2"u8);
        bool result = sut > new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_3_false_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8);
        bool result = sut > new JsonInt64(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_long_MaxValue_false_Js_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8);
        bool result = sut > new JsonInt64(long.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_long_MinValue_true_Jso_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8);
        bool result = sut > new JsonInt64(long.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_null_false_JsonInt6()
    {
        JsonInt64 sut = JsonInt64.ParseValue("null"u8);
        bool result = sut > JsonInt64.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_1_false_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("null"u8);
        bool result = sut > new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_1_false_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8);
        bool result = sut > new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_2_1_true_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("2"u8);
        bool result = sut > new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_3_false_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8);
        bool result = sut > new JsonInt32(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_int_MaxValue_false_Jso()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8);
        bool result = sut > new JsonInt32(int.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_int_MinValue_true_Json()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8);
        bool result = sut > new JsonInt32(int.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_null_false_JsonInt3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("null"u8);
        bool result = sut > JsonInt32.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_1_false_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("null"u8);
        bool result = sut > new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_1_false_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8);
        bool result = sut > new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_2_1_true_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("2"u8);
        bool result = sut > new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_3_false_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8);
        bool result = sut > new JsonInt16(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_short_MaxValue_false_J()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8);
        bool result = sut > new JsonInt16(short.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_short_MinValue_true_Js()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8);
        bool result = sut > new JsonInt16(short.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_null_false_JsonInt1()
    {
        JsonInt16 sut = JsonInt16.ParseValue("null"u8);
        bool result = sut > JsonInt16.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_1_false_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("null"u8);
        bool result = sut > new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_1_false_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8);
        bool result = sut > new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_2_1_true_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("2"u8);
        bool result = sut > new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_3_false_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8);
        bool result = sut > new JsonSByte(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_sbyte_MaxValue_false_J()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8);
        bool result = sut > new JsonSByte(sbyte.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_sbyte_MinValue_true_Js()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8);
        bool result = sut > new JsonSByte(sbyte.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_null_false_JsonSByt()
    {
        JsonSByte sut = JsonSByte.ParseValue("null"u8);
        bool result = sut > JsonSByte.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_1_false_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("null"u8);
        bool result = sut > new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_1_false_JsonUInt64()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8);
        bool result = sut > new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_2_1_true_JsonUInt64()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("2"u8);
        bool result = sut > new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_3_false_JsonUInt64()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8);
        bool result = sut > new JsonUInt64(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_ulong_MaxValue_false_J()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8);
        bool result = sut > new JsonUInt64(ulong.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_ulong_MinValue_true_Js()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8);
        bool result = sut > new JsonUInt64(ulong.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_null_false_JsonUInt()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("null"u8);
        bool result = sut > JsonUInt64.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_1_false_JsonUInt64()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("null"u8);
        bool result = sut > new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_1_false_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8);
        bool result = sut > new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_2_1_true_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("2"u8);
        bool result = sut > new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_3_false_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8);
        bool result = sut > new JsonUInt32(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_uint_MaxValue_false_Js()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8);
        bool result = sut > new JsonUInt32(uint.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_uint_MinValue_true_Jso()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8);
        bool result = sut > new JsonUInt32(uint.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_null_false_JsonUInt_2()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("null"u8);
        bool result = sut > JsonUInt32.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_1_false_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("null"u8);
        bool result = sut > new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_1_false_JsonUInt16()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8);
        bool result = sut > new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_2_1_true_JsonUInt16()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("2"u8);
        bool result = sut > new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_3_false_JsonUInt16()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8);
        bool result = sut > new JsonUInt16(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_ushort_MaxValue_false_()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8);
        bool result = sut > new JsonUInt16(ushort.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_ushort_MinValue_true_J()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8);
        bool result = sut > new JsonUInt16(ushort.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_null_false_JsonUInt_3()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("null"u8);
        bool result = sut > JsonUInt16.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_1_false_JsonUInt16()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("null"u8);
        bool result = sut > new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_1_false_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8);
        bool result = sut > new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_2_1_true_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("2"u8);
        bool result = sut > new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_3_false_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8);
        bool result = sut > new JsonByte(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_byte_MaxValue_false_Js()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8);
        bool result = sut > new JsonByte(byte.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_byte_MinValue_true_Jso()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8);
        bool result = sut > new JsonByte(byte.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_null_false_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("null"u8);
        bool result = sut > JsonByte.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_1_false_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("null"u8);
        bool result = sut > new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_1_false_JsonUInt128()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8);
        bool result = sut > new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_2_1_true_JsonUInt128()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("2"u8);
        bool result = sut > new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_3_false_JsonUInt128()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8);
        bool result = sut > new JsonUInt128(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_UInt128_MaxValue_false()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8);
        bool result = sut > new JsonUInt128(UInt128.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_UInt128_MinValue_true_()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8);
        bool result = sut > new JsonUInt128(UInt128.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_null_false_JsonUInt_4()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("null"u8);
        bool result = sut > JsonUInt128.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_1_false_JsonUInt128()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("null"u8);
        bool result = sut > new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_1_false_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8);
        bool result = sut > new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_2_1_true_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.ParseValue("2"u8);
        bool result = sut > new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_3_false_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8);
        bool result = sut > new JsonInt128(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_Int128_MaxValue_false_()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8);
        bool result = sut > new JsonInt128(Int128.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_1_Int128_MinValue_true_J()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8);
        bool result = sut > new JsonInt128(Int128.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_null_false_JsonInt1_2()
    {
        JsonInt128 sut = JsonInt128.ParseValue("null"u8);
        bool result = sut > JsonInt128.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_integer_null_1_false_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.ParseValue("null"u8);
        bool result = sut > new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_1_false_JsonInteger()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_2_1_true_JsonInteger()
    {
        JsonInteger sut = JsonInteger.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_3_false_JsonInteger()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInteger(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_long_MaxValue_false_JsonInte()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInteger(long.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_long_MinValue_true_JsonInteg()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInteger(long.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_1_false_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_2_1_true_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_3_false_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt64(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_long_MaxValue_false_JsonInt6()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt64(long.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_long_MinValue_true_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt64(long.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_1_false_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_2_1_true_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_3_false_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt32(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_int_MaxValue_false_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt32(int.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_int_MinValue_true_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt32(int.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_1_false_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_2_1_true_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_3_false_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt16(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_short_MaxValue_false_JsonInt()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt16(short.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_short_MinValue_true_JsonInt1()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt16(short.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_1_false_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_2_1_true_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut > new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_3_false_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonSByte(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_sbyte_MaxValue_false_JsonSBy()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonSByte(sbyte.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_sbyte_MinValue_true_JsonSByt()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonSByte(sbyte.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_1_false_JsonUInt64()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_2_1_true_JsonUInt64()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_3_false_JsonUInt64()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt64(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_ulong_MaxValue_false_JsonUIn()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt64(ulong.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_ulong_MinValue_true_JsonUInt()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt64(ulong.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_1_false_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_2_1_true_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_3_false_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt32(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_uint_MaxValue_false_JsonUInt()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt32(uint.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_uint_MinValue_true_JsonUInt3()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt32(uint.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_1_false_JsonUInt16()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_2_1_true_JsonUInt16()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_3_false_JsonUInt16()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt16(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_ushort_MaxValue_false_JsonUI()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt16(ushort.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_ushort_MinValue_true_JsonUIn()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt16(ushort.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_1_false_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_2_1_true_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut > new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_3_false_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonByte(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_byte_MaxValue_false_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonByte(byte.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_byte_MinValue_true_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonByte(byte.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_1_false_JsonUInt128()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_2_1_true_JsonUInt128()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_3_false_JsonUInt128()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt128(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_UInt128_MaxValue_false_JsonU()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt128(UInt128.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_UInt128_MinValue_true_JsonUI()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonUInt128(UInt128.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_null_null_false_JsonUInt128()
    {
        JsonUInt128 sut = JsonUInt128.Null;
        bool result = sut > JsonUInt128.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_null_1_false_JsonUInt128()
    {
        JsonUInt128 sut = JsonUInt128.Null;
        bool result = sut > new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_1_false_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_2_1_true_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_3_false_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt128(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_Int128_MaxValue_false_JsonIn()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt128(Int128.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_1_Int128_MinValue_true_JsonInt()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > new JsonInt128(Int128.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_null_null_false_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.Null;
        bool result = sut > JsonInt128.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_integer_null_1_false_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.Null;
        bool result = sut > new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_1_true_JsonI()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8);
        bool result = sut <= new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_2_1_false_Json()
    {
        JsonInteger sut = JsonInteger.ParseValue("2"u8);
        bool result = sut <= new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_3_true_JsonI()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8);
        bool result = sut <= new JsonInteger(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_long_MaxValu()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8);
        bool result = sut <= new JsonInteger(long.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_long_MinValu()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8);
        bool result = sut <= new JsonInteger(long.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_null_fals()
    {
        JsonInteger sut = JsonInteger.ParseValue("null"u8);
        bool result = sut <= JsonInteger.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_1_false_J()
    {
        JsonInteger sut = JsonInteger.ParseValue("null"u8);
        bool result = sut <= new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_1_true_JsonI_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8);
        bool result = sut <= new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_2_1_false_Json_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("2"u8);
        bool result = sut <= new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_3_true_JsonI_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8);
        bool result = sut <= new JsonInt64(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_long_MaxValu_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8);
        bool result = sut <= new JsonInt64(long.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_long_MinValu_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8);
        bool result = sut <= new JsonInt64(long.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_null_fals_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("null"u8);
        bool result = sut <= JsonInt64.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_1_false_J_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("null"u8);
        bool result = sut <= new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_1_true_JsonI_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8);
        bool result = sut <= new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_2_1_false_Json_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("2"u8);
        bool result = sut <= new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_3_true_JsonI_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8);
        bool result = sut <= new JsonInt32(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_int_MaxValue()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8);
        bool result = sut <= new JsonInt32(int.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_int_MinValue()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8);
        bool result = sut <= new JsonInt32(int.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_null_fals_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("null"u8);
        bool result = sut <= JsonInt32.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_1_false_J_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("null"u8);
        bool result = sut <= new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_1_true_JsonI_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8);
        bool result = sut <= new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_2_1_false_Json_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("2"u8);
        bool result = sut <= new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_3_true_JsonI_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8);
        bool result = sut <= new JsonInt16(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_short_MaxVal()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8);
        bool result = sut <= new JsonInt16(short.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_short_MinVal()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8);
        bool result = sut <= new JsonInt16(short.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_null_fals_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("null"u8);
        bool result = sut <= JsonInt16.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_1_false_J_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("null"u8);
        bool result = sut <= new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_1_true_JsonS()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8);
        bool result = sut <= new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_2_1_false_Json_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("2"u8);
        bool result = sut <= new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_3_true_JsonS()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8);
        bool result = sut <= new JsonSByte(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_sbyte_MaxVal()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8);
        bool result = sut <= new JsonSByte(sbyte.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_sbyte_MinVal()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8);
        bool result = sut <= new JsonSByte(sbyte.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_null_fals_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("null"u8);
        bool result = sut <= JsonSByte.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_1_false_J_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("null"u8);
        bool result = sut <= new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_1_true_JsonU()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8);
        bool result = sut <= new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_2_1_false_Json_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("2"u8);
        bool result = sut <= new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_3_true_JsonU()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8);
        bool result = sut <= new JsonUInt64(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_ulong_MaxVal()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8);
        bool result = sut <= new JsonUInt64(ulong.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_ulong_MinVal()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8);
        bool result = sut <= new JsonUInt64(ulong.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_null_fals_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("null"u8);
        bool result = sut <= JsonUInt64.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_1_false_J_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("null"u8);
        bool result = sut <= new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_1_true_JsonU_2()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8);
        bool result = sut <= new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_2_1_false_Json_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("2"u8);
        bool result = sut <= new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_3_true_JsonU_2()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8);
        bool result = sut <= new JsonUInt32(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_uint_MaxValu()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8);
        bool result = sut <= new JsonUInt32(uint.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_uint_MinValu()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8);
        bool result = sut <= new JsonUInt32(uint.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_null_fals_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("null"u8);
        bool result = sut <= JsonUInt32.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_1_false_J_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("null"u8);
        bool result = sut <= new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_1_true_JsonU_3()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8);
        bool result = sut <= new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_2_1_false_Json_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("2"u8);
        bool result = sut <= new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_3_true_JsonU_3()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8);
        bool result = sut <= new JsonUInt16(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_ushort_MaxVa()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8);
        bool result = sut <= new JsonUInt16(ushort.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_ushort_MinVa()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8);
        bool result = sut <= new JsonUInt16(ushort.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_null_fals_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("null"u8);
        bool result = sut <= JsonUInt16.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_1_false_J_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("null"u8);
        bool result = sut <= new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_1_true_JsonB()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8);
        bool result = sut <= new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_2_1_false_Json_9()
    {
        JsonByte sut = JsonByte.ParseValue("2"u8);
        bool result = sut <= new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_3_true_JsonB()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8);
        bool result = sut <= new JsonByte(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_byte_MaxValu()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8);
        bool result = sut <= new JsonByte(byte.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_byte_MinValu()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8);
        bool result = sut <= new JsonByte(byte.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_null_fals_9()
    {
        JsonByte sut = JsonByte.ParseValue("null"u8);
        bool result = sut <= JsonByte.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_1_false_J_9()
    {
        JsonByte sut = JsonByte.ParseValue("null"u8);
        bool result = sut <= new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_1_true_JsonU_4()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8);
        bool result = sut <= new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_2_1_false_Json_10()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("2"u8);
        bool result = sut <= new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_3_true_JsonU_4()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8);
        bool result = sut <= new JsonUInt128(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_UInt128_MaxV()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8);
        bool result = sut <= new JsonUInt128(UInt128.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_UInt128_MinV()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8);
        bool result = sut <= new JsonUInt128(UInt128.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_null_fals_10()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("null"u8);
        bool result = sut <= JsonUInt128.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_1_false_J_10()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("null"u8);
        bool result = sut <= new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_1_true_JsonI_5()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8);
        bool result = sut <= new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_2_1_false_Json_11()
    {
        JsonInt128 sut = JsonInt128.ParseValue("2"u8);
        bool result = sut <= new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_3_true_JsonI_5()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8);
        bool result = sut <= new JsonInt128(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_Int128_MaxVa()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8);
        bool result = sut <= new JsonInt128(Int128.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_1_Int128_MinVa()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8);
        bool result = sut <= new JsonInt128(Int128.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_null_fals_11()
    {
        JsonInt128 sut = JsonInt128.ParseValue("null"u8);
        bool result = sut <= JsonInt128.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_an_integer_null_1_false_J_11()
    {
        JsonInt128 sut = JsonInt128.ParseValue("null"u8);
        bool result = sut <= new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_1_true_JsonInteger()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_2_1_false_JsonIntege()
    {
        JsonInteger sut = JsonInteger.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_3_true_JsonInteger()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInteger(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_long_MaxValue_true()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInteger(long.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_long_MinValue_fals()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInteger(long.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_1_true_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_2_1_false_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_3_true_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt64(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_long_MaxValue_true_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt64(long.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_long_MinValue_fals_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt64(long.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_1_true_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_2_1_false_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_3_true_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt32(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_int_MaxValue_true_()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt32(int.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_int_MinValue_false()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt32(int.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_1_true_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_2_1_false_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_3_true_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt16(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_short_MaxValue_tru()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt16(short.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_short_MinValue_fal()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt16(short.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_1_true_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_2_1_false_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_3_true_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonSByte(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_sbyte_MaxValue_tru()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonSByte(sbyte.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_sbyte_MinValue_fal()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonSByte(sbyte.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_1_true_JsonUInt64()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_2_1_false_JsonUInt64()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_3_true_JsonUInt64()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt64(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_ulong_MaxValue_tru()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt64(ulong.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_ulong_MinValue_fal()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt64(ulong.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_1_true_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_2_1_false_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_3_true_JsonUInt32()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt32(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_uint_MaxValue_true()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt32(uint.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_uint_MinValue_fals()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt32(uint.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_1_true_JsonUInt16()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_2_1_false_JsonUInt16()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_3_true_JsonUInt16()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt16(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_ushort_MaxValue_tr()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt16(ushort.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_ushort_MinValue_fa()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt16(ushort.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_1_true_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_2_1_false_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_3_true_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonByte(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_byte_MaxValue_true()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonByte(byte.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_byte_MinValue_fals()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonByte(byte.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_1_true_JsonUInt128()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_2_1_false_JsonUInt12()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_3_true_JsonUInt128()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt128(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_UInt128_MaxValue_t()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt128(UInt128.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_UInt128_MinValue_f()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonUInt128(UInt128.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_null_null_false_Json()
    {
        JsonUInt128 sut = JsonUInt128.Null;
        bool result = sut <= JsonUInt128.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_null_1_false_JsonUIn()
    {
        JsonUInt128 sut = JsonUInt128.Null;
        bool result = sut <= new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_1_true_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_2_1_false_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_3_true_JsonInt128()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt128(3).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_Int128_MaxValue_tr()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt128(Int128.MaxValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_1_Int128_MinValue_fa()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= new JsonInt128(Int128.MinValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_null_null_false_Json_2()
    {
        JsonInt128 sut = JsonInt128.Null;
        bool result = sut <= JsonInt128.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_an_integer_null_1_false_JsonInt()
    {
        JsonInt128 sut = JsonInt128.Null;
        bool result = sut <= new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_1_true_Jso()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8);
        bool result = sut >= new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_2_1_true_Jso()
    {
        JsonInteger sut = JsonInteger.ParseValue("2"u8);
        bool result = sut >= new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_3_false_Js()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8);
        bool result = sut >= new JsonInteger(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_long_MaxVa()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8);
        bool result = sut >= new JsonInteger(long.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_long_MinVa()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8);
        bool result = sut >= new JsonInteger(long.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_null_fa()
    {
        JsonInteger sut = JsonInteger.ParseValue("null"u8);
        bool result = sut >= JsonInteger.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_1_false()
    {
        JsonInteger sut = JsonInteger.ParseValue("null"u8);
        bool result = sut >= new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_1_true_Jso_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8);
        bool result = sut >= new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_2_1_true_Jso_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("2"u8);
        bool result = sut >= new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_3_false_Js_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8);
        bool result = sut >= new JsonInt64(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_long_MaxVa_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8);
        bool result = sut >= new JsonInt64(long.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_long_MinVa_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8);
        bool result = sut >= new JsonInt64(long.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_null_fa_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("null"u8);
        bool result = sut >= JsonInt64.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_1_false_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("null"u8);
        bool result = sut >= new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_1_true_Jso_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8);
        bool result = sut >= new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_2_1_true_Jso_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("2"u8);
        bool result = sut >= new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_3_false_Js_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8);
        bool result = sut >= new JsonInt32(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_int_MaxVal()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8);
        bool result = sut >= new JsonInt32(int.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_int_MinVal()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8);
        bool result = sut >= new JsonInt32(int.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_null_fa_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("null"u8);
        bool result = sut >= JsonInt32.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_1_false_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("null"u8);
        bool result = sut >= new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_1_true_Jso_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8);
        bool result = sut >= new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_2_1_true_Jso_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("2"u8);
        bool result = sut >= new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_3_false_Js_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8);
        bool result = sut >= new JsonInt16(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_short_MaxV()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8);
        bool result = sut >= new JsonInt16(short.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_short_MinV()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8);
        bool result = sut >= new JsonInt16(short.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_null_fa_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("null"u8);
        bool result = sut >= JsonInt16.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_1_false_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("null"u8);
        bool result = sut >= new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_1_true_Jso_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8);
        bool result = sut >= new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_2_1_true_Jso_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("2"u8);
        bool result = sut >= new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_3_false_Js_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8);
        bool result = sut >= new JsonSByte(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_sbyte_MaxV()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8);
        bool result = sut >= new JsonSByte(sbyte.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_sbyte_MinV()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8);
        bool result = sut >= new JsonSByte(sbyte.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_null_fa_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("null"u8);
        bool result = sut >= JsonSByte.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_1_false_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("null"u8);
        bool result = sut >= new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_1_true_Jso_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8);
        bool result = sut >= new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_2_1_true_Jso_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("2"u8);
        bool result = sut >= new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_3_false_Js_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8);
        bool result = sut >= new JsonUInt64(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_ulong_MaxV()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8);
        bool result = sut >= new JsonUInt64(ulong.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_ulong_MinV()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8);
        bool result = sut >= new JsonUInt64(ulong.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_null_fa_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("null"u8);
        bool result = sut >= JsonUInt64.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_1_false_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("null"u8);
        bool result = sut >= new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_1_true_Jso_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8);
        bool result = sut >= new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_2_1_true_Jso_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("2"u8);
        bool result = sut >= new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_3_false_Js_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8);
        bool result = sut >= new JsonUInt32(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_uint_MaxVa()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8);
        bool result = sut >= new JsonUInt32(uint.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_uint_MinVa()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8);
        bool result = sut >= new JsonUInt32(uint.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_null_fa_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("null"u8);
        bool result = sut >= JsonUInt32.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_1_false_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("null"u8);
        bool result = sut >= new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_1_true_Jso_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8);
        bool result = sut >= new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_2_1_true_Jso_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("2"u8);
        bool result = sut >= new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_3_false_Js_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8);
        bool result = sut >= new JsonUInt16(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_ushort_Max()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8);
        bool result = sut >= new JsonUInt16(ushort.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_ushort_Min()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8);
        bool result = sut >= new JsonUInt16(ushort.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_null_fa_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("null"u8);
        bool result = sut >= JsonUInt16.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_1_false_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("null"u8);
        bool result = sut >= new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_1_true_Jso_9()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8);
        bool result = sut >= new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_2_1_true_Jso_9()
    {
        JsonByte sut = JsonByte.ParseValue("2"u8);
        bool result = sut >= new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_3_false_Js_9()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8);
        bool result = sut >= new JsonByte(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_byte_MaxVa()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8);
        bool result = sut >= new JsonByte(byte.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_byte_MinVa()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8);
        bool result = sut >= new JsonByte(byte.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_null_fa_9()
    {
        JsonByte sut = JsonByte.ParseValue("null"u8);
        bool result = sut >= JsonByte.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_1_false_9()
    {
        JsonByte sut = JsonByte.ParseValue("null"u8);
        bool result = sut >= new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_1_true_Jso_10()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8);
        bool result = sut >= new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_2_1_true_Jso_10()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("2"u8);
        bool result = sut >= new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_3_false_Js_10()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8);
        bool result = sut >= new JsonUInt128(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_UInt128_Ma()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8);
        bool result = sut >= new JsonUInt128(UInt128.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_UInt128_Mi()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8);
        bool result = sut >= new JsonUInt128(UInt128.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_null_fa_10()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("null"u8);
        bool result = sut >= JsonUInt128.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_1_false_10()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("null"u8);
        bool result = sut >= new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_1_true_Jso_11()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8);
        bool result = sut >= new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_2_1_true_Jso_11()
    {
        JsonInt128 sut = JsonInt128.ParseValue("2"u8);
        bool result = sut >= new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_3_false_Js_11()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8);
        bool result = sut >= new JsonInt128(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_Int128_Max()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8);
        bool result = sut >= new JsonInt128(Int128.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_1_Int128_Min()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8);
        bool result = sut >= new JsonInt128(Int128.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_null_fa_11()
    {
        JsonInt128 sut = JsonInt128.ParseValue("null"u8);
        bool result = sut >= JsonInt128.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_integer_null_1_false_11()
    {
        JsonInt128 sut = JsonInt128.ParseValue("null"u8);
        bool result = sut >= new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_1_true_JsonInteg()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_2_1_true_JsonInteg()
    {
        JsonInteger sut = JsonInteger.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInteger(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_3_false_JsonInte()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInteger(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_long_MaxValue_fa()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInteger(long.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_long_MinValue_tr()
    {
        JsonInteger sut = JsonInteger.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInteger(long.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_1_true_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_2_1_true_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt64(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_3_false_JsonInt6()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt64(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_long_MaxValue_fa_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt64(long.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_long_MinValue_tr_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt64(long.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_1_true_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_2_1_true_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt32(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_3_false_JsonInt3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt32(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_int_MaxValue_fal()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt32(int.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_int_MinValue_tru()
    {
        JsonInt32 sut = JsonInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt32(int.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_1_true_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_2_1_true_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt16(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_3_false_JsonInt1()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt16(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_short_MaxValue_f()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt16(short.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_short_MinValue_t()
    {
        JsonInt16 sut = JsonInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt16(short.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_1_true_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_2_1_true_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonSByte(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_3_false_JsonSByt()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonSByte(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_sbyte_MaxValue_f()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonSByte(sbyte.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_sbyte_MinValue_t()
    {
        JsonSByte sut = JsonSByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonSByte(sbyte.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_1_true_JsonUInt6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_2_1_true_JsonUInt6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt64(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_3_false_JsonUInt()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt64(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_ulong_MaxValue_f()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt64(ulong.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_ulong_MinValue_t()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt64(ulong.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_1_true_JsonUInt3()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_2_1_true_JsonUInt3()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt32(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_3_false_JsonUInt_2()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt32(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_uint_MaxValue_fa()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt32(uint.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_uint_MinValue_tr()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt32(uint.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_1_true_JsonUInt1()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_2_1_true_JsonUInt1()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt16(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_3_false_JsonUInt_3()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt16(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_ushort_MaxValue_()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt16(ushort.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_ushort_MinValue_()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt16(ushort.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_1_true_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_2_1_true_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonByte(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_3_false_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonByte(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_byte_MaxValue_fa()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonByte(byte.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_byte_MinValue_tr()
    {
        JsonByte sut = JsonByte.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonByte(byte.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_1_true_JsonUInt1_2()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_2_1_true_JsonUInt1_2()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_3_false_JsonUInt_4()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt128(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_UInt128_MaxValue()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt128(UInt128.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_UInt128_MinValue()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonUInt128(UInt128.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_null_null_false_Js()
    {
        JsonUInt128 sut = JsonUInt128.Null;
        bool result = sut >= JsonUInt128.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_null_1_false_JsonU()
    {
        JsonUInt128 sut = JsonUInt128.Null;
        bool result = sut >= new JsonUInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_1_true_JsonInt12()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_2_1_true_JsonInt12()
    {
        JsonInt128 sut = JsonInt128.ParseValue("2"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_3_false_JsonInt1_2()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt128(3).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_Int128_MaxValue_()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt128(Int128.MaxValue).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_1_Int128_MinValue_()
    {
        JsonInt128 sut = JsonInt128.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= new JsonInt128(Int128.MinValue).AsJsonElementBackedValue();
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_null_null_false_Js_2()
    {
        JsonInt128 sut = JsonInt128.Null;
        bool result = sut >= JsonInt128.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_dotnet_backed_value_as_a_integer_null_1_false_JsonI()
    {
        JsonInt128 sut = JsonInt128.Null;
        bool result = sut >= new JsonInt128(1).AsJsonElementBackedValue();
        Assert.IsFalse(result);
    }
}
#endif