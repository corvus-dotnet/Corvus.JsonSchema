// <copyright file="JsonCompareMethodTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET
using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.JsonModel.NumericTypes;

[TestClass]
public class JsonCompareMethodTests
{
    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_10_20_1_JsonInteg()
    {
        JsonInteger sut = JsonInteger.ParseValue("10"u8);
        int compareResult = JsonInteger.Compare(sut, JsonInteger.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_20_10_1_JsonInteg()
    {
        JsonInteger sut = JsonInteger.ParseValue("20"u8);
        int compareResult = JsonInteger.Compare(sut, JsonInteger.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_15_15_0_JsonInteg()
    {
        JsonInteger sut = JsonInteger.ParseValue("15"u8);
        int compareResult = JsonInteger.Compare(sut, JsonInteger.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_10_20_1_JsonInt12()
    {
        JsonInt128 sut = JsonInt128.ParseValue("10"u8);
        int compareResult = JsonInt128.Compare(sut, JsonInt128.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_20_10_1_JsonInt12()
    {
        JsonInt128 sut = JsonInt128.ParseValue("20"u8);
        int compareResult = JsonInt128.Compare(sut, JsonInt128.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_15_15_0_JsonInt12()
    {
        JsonInt128 sut = JsonInt128.ParseValue("15"u8);
        int compareResult = JsonInt128.Compare(sut, JsonInt128.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_10_20_1_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("10"u8);
        int compareResult = JsonInt64.Compare(sut, JsonInt64.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_20_10_1_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("20"u8);
        int compareResult = JsonInt64.Compare(sut, JsonInt64.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_15_15_0_JsonInt64()
    {
        JsonInt64 sut = JsonInt64.ParseValue("15"u8);
        int compareResult = JsonInt64.Compare(sut, JsonInt64.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_10_20_1_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("10"u8);
        int compareResult = JsonInt32.Compare(sut, JsonInt32.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_20_10_1_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("20"u8);
        int compareResult = JsonInt32.Compare(sut, JsonInt32.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_15_15_0_JsonInt32()
    {
        JsonInt32 sut = JsonInt32.ParseValue("15"u8);
        int compareResult = JsonInt32.Compare(sut, JsonInt32.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_10_20_1_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("10"u8);
        int compareResult = JsonInt16.Compare(sut, JsonInt16.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_20_10_1_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("20"u8);
        int compareResult = JsonInt16.Compare(sut, JsonInt16.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_15_15_0_JsonInt16()
    {
        JsonInt16 sut = JsonInt16.ParseValue("15"u8);
        int compareResult = JsonInt16.Compare(sut, JsonInt16.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_10_20_1_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("10"u8);
        int compareResult = JsonSByte.Compare(sut, JsonSByte.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_20_10_1_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("20"u8);
        int compareResult = JsonSByte.Compare(sut, JsonSByte.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_15_15_0_JsonSByte()
    {
        JsonSByte sut = JsonSByte.ParseValue("15"u8);
        int compareResult = JsonSByte.Compare(sut, JsonSByte.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_10_20_1_JsonUInt6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("10"u8);
        int compareResult = JsonUInt64.Compare(sut, JsonUInt64.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_20_10_1_JsonUInt6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("20"u8);
        int compareResult = JsonUInt64.Compare(sut, JsonUInt64.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_15_15_0_JsonUInt6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("15"u8);
        int compareResult = JsonUInt64.Compare(sut, JsonUInt64.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_10_20_1_JsonUInt1()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("10"u8);
        int compareResult = JsonUInt128.Compare(sut, JsonUInt128.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_20_10_1_JsonUInt1()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("20"u8);
        int compareResult = JsonUInt128.Compare(sut, JsonUInt128.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_15_15_0_JsonUInt1()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("15"u8);
        int compareResult = JsonUInt128.Compare(sut, JsonUInt128.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_10_20_1_JsonUInt3()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("10"u8);
        int compareResult = JsonUInt32.Compare(sut, JsonUInt32.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_20_10_1_JsonUInt3()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("20"u8);
        int compareResult = JsonUInt32.Compare(sut, JsonUInt32.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_15_15_0_JsonUInt3()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("15"u8);
        int compareResult = JsonUInt32.Compare(sut, JsonUInt32.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_10_20_1_JsonUInt1_2()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("10"u8);
        int compareResult = JsonUInt16.Compare(sut, JsonUInt16.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_20_10_1_JsonUInt1_2()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("20"u8);
        int compareResult = JsonUInt16.Compare(sut, JsonUInt16.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_15_15_0_JsonUInt1_2()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("15"u8);
        int compareResult = JsonUInt16.Compare(sut, JsonUInt16.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_10_20_1_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("10"u8);
        int compareResult = JsonByte.Compare(sut, JsonByte.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_20_10_1_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("20"u8);
        int compareResult = JsonByte.Compare(sut, JsonByte.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_15_15_0_JsonByte()
    {
        JsonByte sut = JsonByte.ParseValue("15"u8);
        int compareResult = JsonByte.Compare(sut, JsonByte.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_10_1_20_1_1_JsonN()
    {
        JsonNumber sut = JsonNumber.ParseValue("10.1"u8);
        int compareResult = JsonNumber.Compare(sut, JsonNumber.ParseValue("20.1"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_20_1_10_1_1_JsonN()
    {
        JsonNumber sut = JsonNumber.ParseValue("20.1"u8);
        int compareResult = JsonNumber.Compare(sut, JsonNumber.ParseValue("10.1"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_15_1_15_1_0_JsonN()
    {
        JsonNumber sut = JsonNumber.ParseValue("15.1"u8);
        int compareResult = JsonNumber.Compare(sut, JsonNumber.ParseValue("15.1"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_10_1_20_1_1_JsonS()
    {
        JsonSingle sut = JsonSingle.ParseValue("10.1"u8);
        int compareResult = JsonSingle.Compare(sut, JsonSingle.ParseValue("20.1"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_20_1_10_1_1_JsonS()
    {
        JsonSingle sut = JsonSingle.ParseValue("20.1"u8);
        int compareResult = JsonSingle.Compare(sut, JsonSingle.ParseValue("10.1"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_15_1_15_1_0_JsonS()
    {
        JsonSingle sut = JsonSingle.ParseValue("15.1"u8);
        int compareResult = JsonSingle.Compare(sut, JsonSingle.ParseValue("15.1"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_10_1_20_1_1_JsonH()
    {
        JsonHalf sut = JsonHalf.ParseValue("10.1"u8);
        int compareResult = JsonHalf.Compare(sut, JsonHalf.ParseValue("20.1"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_20_1_10_1_1_JsonH()
    {
        JsonHalf sut = JsonHalf.ParseValue("20.1"u8);
        int compareResult = JsonHalf.Compare(sut, JsonHalf.ParseValue("10.1"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_15_1_15_1_0_JsonH()
    {
        JsonHalf sut = JsonHalf.ParseValue("15.1"u8);
        int compareResult = JsonHalf.Compare(sut, JsonHalf.ParseValue("15.1"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_10_1_20_1_1_JsonD()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("10.1"u8);
        int compareResult = JsonDecimal.Compare(sut, JsonDecimal.ParseValue("20.1"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_20_1_10_1_1_JsonD()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("20.1"u8);
        int compareResult = JsonDecimal.Compare(sut, JsonDecimal.ParseValue("10.1"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_15_1_15_1_0_JsonD()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("15.1"u8);
        int compareResult = JsonDecimal.Compare(sut, JsonDecimal.ParseValue("15.1"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_10_1_20_1_1_JsonD_2()
    {
        JsonDouble sut = JsonDouble.ParseValue("10.1"u8);
        int compareResult = JsonDouble.Compare(sut, JsonDouble.ParseValue("20.1"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_20_1_10_1_1_JsonD_2()
    {
        JsonDouble sut = JsonDouble.ParseValue("20.1"u8);
        int compareResult = JsonDouble.Compare(sut, JsonDouble.ParseValue("10.1"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_json_backed_values_using_the_static_Compare_method_15_1_15_1_0_JsonD_2()
    {
        JsonDouble sut = JsonDouble.ParseValue("15.1"u8);
        int compareResult = JsonDouble.Compare(sut, JsonDouble.ParseValue("15.1"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_10_20_1_JsonInt()
    {
        JsonInteger sut = JsonInteger.ParseValue("10"u8).AsDotnetBackedValue();
        int compareResult = JsonInteger.Compare(sut, JsonInteger.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_20_10_1_JsonInt()
    {
        JsonInteger sut = JsonInteger.ParseValue("20"u8).AsDotnetBackedValue();
        int compareResult = JsonInteger.Compare(sut, JsonInteger.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_15_15_0_JsonInt()
    {
        JsonInteger sut = JsonInteger.ParseValue("15"u8).AsDotnetBackedValue();
        int compareResult = JsonInteger.Compare(sut, JsonInteger.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_10_20_1_JsonInt_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("10"u8).AsDotnetBackedValue();
        int compareResult = JsonInt64.Compare(sut, JsonInt64.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_20_10_1_JsonInt_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("20"u8).AsDotnetBackedValue();
        int compareResult = JsonInt64.Compare(sut, JsonInt64.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_15_15_0_JsonInt_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("15"u8).AsDotnetBackedValue();
        int compareResult = JsonInt64.Compare(sut, JsonInt64.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_10_20_1_JsonInt_3()
    {
        JsonInt128 sut = JsonInt128.ParseValue("10"u8).AsDotnetBackedValue();
        int compareResult = JsonInt128.Compare(sut, JsonInt128.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_20_10_1_JsonInt_3()
    {
        JsonInt128 sut = JsonInt128.ParseValue("20"u8).AsDotnetBackedValue();
        int compareResult = JsonInt128.Compare(sut, JsonInt128.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_15_15_0_JsonInt_3()
    {
        JsonInt128 sut = JsonInt128.ParseValue("15"u8).AsDotnetBackedValue();
        int compareResult = JsonInt128.Compare(sut, JsonInt128.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_10_20_1_JsonInt_4()
    {
        JsonInt32 sut = JsonInt32.ParseValue("10"u8).AsDotnetBackedValue();
        int compareResult = JsonInt32.Compare(sut, JsonInt32.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_20_10_1_JsonInt_4()
    {
        JsonInt32 sut = JsonInt32.ParseValue("20"u8).AsDotnetBackedValue();
        int compareResult = JsonInt32.Compare(sut, JsonInt32.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_15_15_0_JsonInt_4()
    {
        JsonInt32 sut = JsonInt32.ParseValue("15"u8).AsDotnetBackedValue();
        int compareResult = JsonInt32.Compare(sut, JsonInt32.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_10_20_1_JsonInt_5()
    {
        JsonInt16 sut = JsonInt16.ParseValue("10"u8).AsDotnetBackedValue();
        int compareResult = JsonInt16.Compare(sut, JsonInt16.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_20_10_1_JsonInt_5()
    {
        JsonInt16 sut = JsonInt16.ParseValue("20"u8).AsDotnetBackedValue();
        int compareResult = JsonInt16.Compare(sut, JsonInt16.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_15_15_0_JsonInt_5()
    {
        JsonInt16 sut = JsonInt16.ParseValue("15"u8).AsDotnetBackedValue();
        int compareResult = JsonInt16.Compare(sut, JsonInt16.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_10_20_1_JsonSBy()
    {
        JsonSByte sut = JsonSByte.ParseValue("10"u8).AsDotnetBackedValue();
        int compareResult = JsonSByte.Compare(sut, JsonSByte.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_20_10_1_JsonSBy()
    {
        JsonSByte sut = JsonSByte.ParseValue("20"u8).AsDotnetBackedValue();
        int compareResult = JsonSByte.Compare(sut, JsonSByte.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_15_15_0_JsonSBy()
    {
        JsonSByte sut = JsonSByte.ParseValue("15"u8).AsDotnetBackedValue();
        int compareResult = JsonSByte.Compare(sut, JsonSByte.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_10_20_1_JsonUIn()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("10"u8).AsDotnetBackedValue();
        int compareResult = JsonUInt64.Compare(sut, JsonUInt64.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_20_10_1_JsonUIn()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("20"u8).AsDotnetBackedValue();
        int compareResult = JsonUInt64.Compare(sut, JsonUInt64.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_15_15_0_JsonUIn()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("15"u8).AsDotnetBackedValue();
        int compareResult = JsonUInt64.Compare(sut, JsonUInt64.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_10_20_1_JsonUIn_2()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("10"u8).AsDotnetBackedValue();
        int compareResult = JsonUInt128.Compare(sut, JsonUInt128.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_20_10_1_JsonUIn_2()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("20"u8).AsDotnetBackedValue();
        int compareResult = JsonUInt128.Compare(sut, JsonUInt128.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_15_15_0_JsonUIn_2()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("15"u8).AsDotnetBackedValue();
        int compareResult = JsonUInt128.Compare(sut, JsonUInt128.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_10_20_1_JsonUIn_3()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("10"u8).AsDotnetBackedValue();
        int compareResult = JsonUInt32.Compare(sut, JsonUInt32.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_20_10_1_JsonUIn_3()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("20"u8).AsDotnetBackedValue();
        int compareResult = JsonUInt32.Compare(sut, JsonUInt32.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_15_15_0_JsonUIn_3()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("15"u8).AsDotnetBackedValue();
        int compareResult = JsonUInt32.Compare(sut, JsonUInt32.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_10_20_1_JsonUIn_4()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("10"u8).AsDotnetBackedValue();
        int compareResult = JsonUInt16.Compare(sut, JsonUInt16.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_20_10_1_JsonUIn_4()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("20"u8).AsDotnetBackedValue();
        int compareResult = JsonUInt16.Compare(sut, JsonUInt16.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_15_15_0_JsonUIn_4()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("15"u8).AsDotnetBackedValue();
        int compareResult = JsonUInt16.Compare(sut, JsonUInt16.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_10_20_1_JsonByt()
    {
        JsonByte sut = JsonByte.ParseValue("10"u8).AsDotnetBackedValue();
        int compareResult = JsonByte.Compare(sut, JsonByte.ParseValue("20"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_20_10_1_JsonByt()
    {
        JsonByte sut = JsonByte.ParseValue("20"u8).AsDotnetBackedValue();
        int compareResult = JsonByte.Compare(sut, JsonByte.ParseValue("10"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_15_15_0_JsonByt()
    {
        JsonByte sut = JsonByte.ParseValue("15"u8).AsDotnetBackedValue();
        int compareResult = JsonByte.Compare(sut, JsonByte.ParseValue("15"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_10_1_20_1_1_Jso()
    {
        JsonNumber sut = JsonNumber.ParseValue("10.1"u8).AsDotnetBackedValue();
        int compareResult = JsonNumber.Compare(sut, JsonNumber.ParseValue("20.1"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_20_1_10_1_1_Jso()
    {
        JsonNumber sut = JsonNumber.ParseValue("20.1"u8).AsDotnetBackedValue();
        int compareResult = JsonNumber.Compare(sut, JsonNumber.ParseValue("10.1"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_15_1_15_1_0_Jso()
    {
        JsonNumber sut = JsonNumber.ParseValue("15.1"u8).AsDotnetBackedValue();
        int compareResult = JsonNumber.Compare(sut, JsonNumber.ParseValue("15.1"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_10_1_20_1_1_Jso_2()
    {
        JsonSingle sut = JsonSingle.ParseValue("10.1"u8).AsDotnetBackedValue();
        int compareResult = JsonSingle.Compare(sut, JsonSingle.ParseValue("20.1"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_20_1_10_1_1_Jso_2()
    {
        JsonSingle sut = JsonSingle.ParseValue("20.1"u8).AsDotnetBackedValue();
        int compareResult = JsonSingle.Compare(sut, JsonSingle.ParseValue("10.1"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_15_1_15_1_0_Jso_2()
    {
        JsonSingle sut = JsonSingle.ParseValue("15.1"u8).AsDotnetBackedValue();
        int compareResult = JsonSingle.Compare(sut, JsonSingle.ParseValue("15.1"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_10_1_20_1_1_Jso_3()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("10.1"u8).AsDotnetBackedValue();
        int compareResult = JsonDecimal.Compare(sut, JsonDecimal.ParseValue("20.1"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_20_1_10_1_1_Jso_3()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("20.1"u8).AsDotnetBackedValue();
        int compareResult = JsonDecimal.Compare(sut, JsonDecimal.ParseValue("10.1"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_15_1_15_1_0_Jso_3()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("15.1"u8).AsDotnetBackedValue();
        int compareResult = JsonDecimal.Compare(sut, JsonDecimal.ParseValue("15.1"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_10_1_20_1_1_Jso_4()
    {
        JsonDouble sut = JsonDouble.ParseValue("10.1"u8).AsDotnetBackedValue();
        int compareResult = JsonDouble.Compare(sut, JsonDouble.ParseValue("20.1"u8));
        Assert.AreEqual(Math.Sign(-1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_20_1_10_1_1_Jso_4()
    {
        JsonDouble sut = JsonDouble.ParseValue("20.1"u8).AsDotnetBackedValue();
        int compareResult = JsonDouble.Compare(sut, JsonDouble.ParseValue("10.1"u8));
        Assert.AreEqual(Math.Sign(1), Math.Sign(compareResult));
    }

    [TestMethod]
    public void Compare_two_dotnet_backed_values_using_the_static_Compare_method_15_1_15_1_0_Jso_4()
    {
        JsonDouble sut = JsonDouble.ParseValue("15.1"u8).AsDotnetBackedValue();
        int compareResult = JsonDouble.Compare(sut, JsonDouble.ParseValue("15.1"u8));
        Assert.AreEqual(Math.Sign(0), Math.Sign(compareResult));
    }
}
#endif