// <copyright file="JsonComparisonOperatorsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET
using Corvus.Json;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.NumericTypes;

public class JsonComparisonOperatorsTests
{
    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_10_20_true_JsonInt()
    {
        JsonInteger sut = JsonInteger.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut < JsonInteger.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_20_10_false_JsonIn()
    {
        JsonInteger sut = JsonInteger.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut < JsonInteger.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_15_15_false_JsonIn()
    {
        JsonInteger sut = JsonInteger.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut < JsonInteger.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_10_20_true_JsonInt_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut < JsonInt64.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_20_10_false_JsonIn_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut < JsonInt64.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_15_15_false_JsonIn_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut < JsonInt64.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_10_20_true_JsonInt_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut < JsonInt32.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_20_10_false_JsonIn_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut < JsonInt32.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_15_15_false_JsonIn_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut < JsonInt32.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_10_20_true_JsonInt_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut < JsonInt16.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_20_10_false_JsonIn_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut < JsonInt16.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_15_15_false_JsonIn_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut < JsonInt16.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_10_20_true_JsonSBy()
    {
        JsonSByte sut = JsonSByte.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut < JsonSByte.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_20_10_false_JsonSB()
    {
        JsonSByte sut = JsonSByte.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut < JsonSByte.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_15_15_false_JsonSB()
    {
        JsonSByte sut = JsonSByte.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut < JsonSByte.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_10_20_true_JsonUIn()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut < JsonUInt64.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_20_10_false_JsonUI()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut < JsonUInt64.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_15_15_false_JsonUI()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut < JsonUInt64.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_10_20_true_JsonUIn_2()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut < JsonUInt32.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_20_10_false_JsonUI_2()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut < JsonUInt32.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_15_15_false_JsonUI_2()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut < JsonUInt32.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_10_20_true_JsonUIn_3()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut < JsonUInt16.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_20_10_false_JsonUI_3()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut < JsonUInt16.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_15_15_false_JsonUI_3()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut < JsonUInt16.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_10_20_true_JsonByt()
    {
        JsonByte sut = JsonByte.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut < JsonByte.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_20_10_false_JsonBy()
    {
        JsonByte sut = JsonByte.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut < JsonByte.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_operator_15_15_false_JsonBy()
    {
        JsonByte sut = JsonByte.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut < JsonByte.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_10_20_tru()
    {
        JsonInteger sut = JsonInteger.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut <= JsonInteger.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_20_10_fal()
    {
        JsonInteger sut = JsonInteger.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut <= JsonInteger.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_15_15_tru()
    {
        JsonInteger sut = JsonInteger.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut <= JsonInteger.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_10_20_tru_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut <= JsonInt64.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_20_10_fal_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut <= JsonInt64.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_15_15_tru_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut <= JsonInt64.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_10_20_tru_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut <= JsonInt32.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_20_10_fal_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut <= JsonInt32.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_15_15_tru_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut <= JsonInt32.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_10_20_tru_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut <= JsonInt16.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_20_10_fal_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut <= JsonInt16.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_15_15_tru_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut <= JsonInt16.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_10_20_tru_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut <= JsonSByte.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_20_10_fal_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut <= JsonSByte.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_15_15_tru_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut <= JsonSByte.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_10_20_tru_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut <= JsonUInt64.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_20_10_fal_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut <= JsonUInt64.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_15_15_tru_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut <= JsonUInt64.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_10_20_tru_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut <= JsonUInt32.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_20_10_fal_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut <= JsonUInt32.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_15_15_tru_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut <= JsonUInt32.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_10_20_tru_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut <= JsonUInt16.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_20_10_fal_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut <= JsonUInt16.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_15_15_tru_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut <= JsonUInt16.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_10_20_tru_9()
    {
        JsonByte sut = JsonByte.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut <= JsonByte.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_20_10_fal_9()
    {
        JsonByte sut = JsonByte.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut <= JsonByte.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_less_than_or_equal_operator_15_15_tru_9()
    {
        JsonByte sut = JsonByte.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut <= JsonByte.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_10_20_false_Jso()
    {
        JsonInteger sut = JsonInteger.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut > JsonInteger.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_20_10_true_Json()
    {
        JsonInteger sut = JsonInteger.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut > JsonInteger.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_15_15_false_Jso()
    {
        JsonInteger sut = JsonInteger.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut > JsonInteger.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_10_20_false_Jso_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut > JsonInt64.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_20_10_true_Json_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut > JsonInt64.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_15_15_false_Jso_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut > JsonInt64.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_10_20_false_Jso_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut > JsonInt32.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_20_10_true_Json_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut > JsonInt32.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_15_15_false_Jso_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut > JsonInt32.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_10_20_false_Jso_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut > JsonInt16.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_20_10_true_Json_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut > JsonInt16.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_15_15_false_Jso_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut > JsonInt16.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_10_20_false_Jso_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut > JsonSByte.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_20_10_true_Json_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut > JsonSByte.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_15_15_false_Jso_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut > JsonSByte.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_10_20_false_Jso_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut > JsonUInt64.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_20_10_true_Json_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut > JsonUInt64.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_15_15_false_Jso_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut > JsonUInt64.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_10_20_false_Jso_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut > JsonUInt32.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_20_10_true_Json_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut > JsonUInt32.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_15_15_false_Jso_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut > JsonUInt32.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_10_20_false_Jso_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut > JsonUInt16.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_20_10_true_Json_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut > JsonUInt16.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_15_15_false_Jso_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut > JsonUInt16.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_10_20_false_Jso_9()
    {
        JsonByte sut = JsonByte.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut > JsonByte.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_20_10_true_Json_9()
    {
        JsonByte sut = JsonByte.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut > JsonByte.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_operator_15_15_false_Jso_9()
    {
        JsonByte sut = JsonByte.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut > JsonByte.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_10_20_()
    {
        JsonInteger sut = JsonInteger.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut >= JsonInteger.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_20_10_()
    {
        JsonInteger sut = JsonInteger.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut >= JsonInteger.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_15_15_()
    {
        JsonInteger sut = JsonInteger.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut >= JsonInteger.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_10_20__2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut >= JsonInt64.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_20_10__2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut >= JsonInt64.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_15_15__2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut >= JsonInt64.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_10_20__3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut >= JsonInt32.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_20_10__3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut >= JsonInt32.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_15_15__3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut >= JsonInt32.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_10_20__4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut >= JsonInt16.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_20_10__4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut >= JsonInt16.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_15_15__4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut >= JsonInt16.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_10_20__5()
    {
        JsonSByte sut = JsonSByte.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut >= JsonSByte.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_20_10__5()
    {
        JsonSByte sut = JsonSByte.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut >= JsonSByte.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_15_15__5()
    {
        JsonSByte sut = JsonSByte.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut >= JsonSByte.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_10_20__6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut >= JsonUInt64.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_20_10__6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut >= JsonUInt64.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_15_15__6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut >= JsonUInt64.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_10_20__7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut >= JsonUInt32.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_20_10__7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut >= JsonUInt32.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_15_15__7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut >= JsonUInt32.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_10_20__8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut >= JsonUInt16.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_20_10__8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut >= JsonUInt16.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_15_15__8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut >= JsonUInt16.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_10_20__9()
    {
        JsonByte sut = JsonByte.ParseValue("10"u8).AsDotnetBackedValue();
        bool result = sut >= JsonByte.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_20_10__9()
    {
        JsonByte sut = JsonByte.ParseValue("20"u8).AsDotnetBackedValue();
        bool result = sut >= JsonByte.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_dotnet_backed_values_using_the_greater_than_or_equal_operator_15_15__9()
    {
        JsonByte sut = JsonByte.ParseValue("15"u8).AsDotnetBackedValue();
        bool result = sut >= JsonByte.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_10_20_true_Js()
    {
        JsonInteger sut = JsonInteger.ParseValue("10"u8);
        bool result = sut < JsonInteger.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_20_10_false_J()
    {
        JsonInteger sut = JsonInteger.ParseValue("20"u8);
        bool result = sut < JsonInteger.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_15_15_false_J()
    {
        JsonInteger sut = JsonInteger.ParseValue("15"u8);
        bool result = sut < JsonInteger.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_10_20_true_Js_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("10"u8);
        bool result = sut < JsonInt64.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_20_10_false_J_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("20"u8);
        bool result = sut < JsonInt64.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_15_15_false_J_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("15"u8);
        bool result = sut < JsonInt64.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_10_20_true_Js_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("10"u8);
        bool result = sut < JsonInt32.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_20_10_false_J_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("20"u8);
        bool result = sut < JsonInt32.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_15_15_false_J_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("15"u8);
        bool result = sut < JsonInt32.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_10_20_true_Js_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("10"u8);
        bool result = sut < JsonInt16.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_20_10_false_J_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("20"u8);
        bool result = sut < JsonInt16.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_15_15_false_J_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("15"u8);
        bool result = sut < JsonInt16.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_10_20_true_Js_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("10"u8);
        bool result = sut < JsonSByte.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_20_10_false_J_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("20"u8);
        bool result = sut < JsonSByte.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_15_15_false_J_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("15"u8);
        bool result = sut < JsonSByte.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_10_20_true_Js_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("10"u8);
        bool result = sut < JsonUInt64.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_20_10_false_J_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("20"u8);
        bool result = sut < JsonUInt64.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_15_15_false_J_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("15"u8);
        bool result = sut < JsonUInt64.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_10_20_true_Js_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("10"u8);
        bool result = sut < JsonUInt32.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_20_10_false_J_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("20"u8);
        bool result = sut < JsonUInt32.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_15_15_false_J_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("15"u8);
        bool result = sut < JsonUInt32.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_10_20_true_Js_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("10"u8);
        bool result = sut < JsonUInt16.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_20_10_false_J_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("20"u8);
        bool result = sut < JsonUInt16.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_15_15_false_J_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("15"u8);
        bool result = sut < JsonUInt16.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_10_20_true_Js_9()
    {
        JsonByte sut = JsonByte.ParseValue("10"u8);
        bool result = sut < JsonByte.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_20_10_false_J_9()
    {
        JsonByte sut = JsonByte.ParseValue("20"u8);
        bool result = sut < JsonByte.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_operator_15_15_false_J_9()
    {
        JsonByte sut = JsonByte.ParseValue("15"u8);
        bool result = sut < JsonByte.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_10_2()
    {
        JsonInteger sut = JsonInteger.ParseValue("10"u8);
        bool result = sut <= JsonInteger.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_20_1()
    {
        JsonInteger sut = JsonInteger.ParseValue("20"u8);
        bool result = sut <= JsonInteger.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_15_1()
    {
        JsonInteger sut = JsonInteger.ParseValue("15"u8);
        bool result = sut <= JsonInteger.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_10_2_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("10"u8);
        bool result = sut <= JsonInt64.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_20_1_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("20"u8);
        bool result = sut <= JsonInt64.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_15_1_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("15"u8);
        bool result = sut <= JsonInt64.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_10_2_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("10"u8);
        bool result = sut <= JsonInt32.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_20_1_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("20"u8);
        bool result = sut <= JsonInt32.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_15_1_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("15"u8);
        bool result = sut <= JsonInt32.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_10_2_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("10"u8);
        bool result = sut <= JsonInt16.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_20_1_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("20"u8);
        bool result = sut <= JsonInt16.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_15_1_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("15"u8);
        bool result = sut <= JsonInt16.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_10_2_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("10"u8);
        bool result = sut <= JsonSByte.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_20_1_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("20"u8);
        bool result = sut <= JsonSByte.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_15_1_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("15"u8);
        bool result = sut <= JsonSByte.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_10_2_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("10"u8);
        bool result = sut <= JsonUInt64.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_20_1_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("20"u8);
        bool result = sut <= JsonUInt64.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_15_1_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("15"u8);
        bool result = sut <= JsonUInt64.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_10_2_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("10"u8);
        bool result = sut <= JsonUInt32.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_20_1_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("20"u8);
        bool result = sut <= JsonUInt32.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_15_1_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("15"u8);
        bool result = sut <= JsonUInt32.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_10_2_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("10"u8);
        bool result = sut <= JsonUInt16.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_20_1_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("20"u8);
        bool result = sut <= JsonUInt16.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_15_1_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("15"u8);
        bool result = sut <= JsonUInt16.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_10_2_9()
    {
        JsonByte sut = JsonByte.ParseValue("10"u8);
        bool result = sut <= JsonByte.ParseValue("20"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_20_1_9()
    {
        JsonByte sut = JsonByte.ParseValue("20"u8);
        bool result = sut <= JsonByte.ParseValue("10"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_less_than_or_equal_operator_15_1_9()
    {
        JsonByte sut = JsonByte.ParseValue("15"u8);
        bool result = sut <= JsonByte.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_10_20_fals()
    {
        JsonInteger sut = JsonInteger.ParseValue("10"u8);
        bool result = sut > JsonInteger.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_20_10_true()
    {
        JsonInteger sut = JsonInteger.ParseValue("20"u8);
        bool result = sut > JsonInteger.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_15_15_fals()
    {
        JsonInteger sut = JsonInteger.ParseValue("15"u8);
        bool result = sut > JsonInteger.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_10_20_fals_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("10"u8);
        bool result = sut > JsonInt64.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_20_10_true_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("20"u8);
        bool result = sut > JsonInt64.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_15_15_fals_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("15"u8);
        bool result = sut > JsonInt64.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_10_20_fals_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("10"u8);
        bool result = sut > JsonInt32.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_20_10_true_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("20"u8);
        bool result = sut > JsonInt32.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_15_15_fals_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("15"u8);
        bool result = sut > JsonInt32.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_10_20_fals_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("10"u8);
        bool result = sut > JsonInt16.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_20_10_true_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("20"u8);
        bool result = sut > JsonInt16.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_15_15_fals_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("15"u8);
        bool result = sut > JsonInt16.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_10_20_fals_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("10"u8);
        bool result = sut > JsonSByte.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_20_10_true_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("20"u8);
        bool result = sut > JsonSByte.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_15_15_fals_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("15"u8);
        bool result = sut > JsonSByte.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_10_20_fals_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("10"u8);
        bool result = sut > JsonUInt64.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_20_10_true_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("20"u8);
        bool result = sut > JsonUInt64.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_15_15_fals_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("15"u8);
        bool result = sut > JsonUInt64.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_10_20_fals_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("10"u8);
        bool result = sut > JsonUInt32.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_20_10_true_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("20"u8);
        bool result = sut > JsonUInt32.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_15_15_fals_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("15"u8);
        bool result = sut > JsonUInt32.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_10_20_fals_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("10"u8);
        bool result = sut > JsonUInt16.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_20_10_true_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("20"u8);
        bool result = sut > JsonUInt16.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_15_15_fals_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("15"u8);
        bool result = sut > JsonUInt16.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_10_20_fals_9()
    {
        JsonByte sut = JsonByte.ParseValue("10"u8);
        bool result = sut > JsonByte.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_20_10_true_9()
    {
        JsonByte sut = JsonByte.ParseValue("20"u8);
        bool result = sut > JsonByte.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_operator_15_15_fals_9()
    {
        JsonByte sut = JsonByte.ParseValue("15"u8);
        bool result = sut > JsonByte.ParseValue("15"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_1()
    {
        JsonInteger sut = JsonInteger.ParseValue("10"u8);
        bool result = sut >= JsonInteger.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_2()
    {
        JsonInteger sut = JsonInteger.ParseValue("20"u8);
        bool result = sut >= JsonInteger.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_1_2()
    {
        JsonInteger sut = JsonInteger.ParseValue("15"u8);
        bool result = sut >= JsonInteger.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_1_3()
    {
        JsonInt64 sut = JsonInt64.ParseValue("10"u8);
        bool result = sut >= JsonInt64.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_2_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("20"u8);
        bool result = sut >= JsonInt64.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_1_4()
    {
        JsonInt64 sut = JsonInt64.ParseValue("15"u8);
        bool result = sut >= JsonInt64.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_1_5()
    {
        JsonInt32 sut = JsonInt32.ParseValue("10"u8);
        bool result = sut >= JsonInt32.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_2_3()
    {
        JsonInt32 sut = JsonInt32.ParseValue("20"u8);
        bool result = sut >= JsonInt32.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_1_6()
    {
        JsonInt32 sut = JsonInt32.ParseValue("15"u8);
        bool result = sut >= JsonInt32.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_1_7()
    {
        JsonInt16 sut = JsonInt16.ParseValue("10"u8);
        bool result = sut >= JsonInt16.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_2_4()
    {
        JsonInt16 sut = JsonInt16.ParseValue("20"u8);
        bool result = sut >= JsonInt16.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_1_8()
    {
        JsonInt16 sut = JsonInt16.ParseValue("15"u8);
        bool result = sut >= JsonInt16.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_1_9()
    {
        JsonSByte sut = JsonSByte.ParseValue("10"u8);
        bool result = sut >= JsonSByte.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_2_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("20"u8);
        bool result = sut >= JsonSByte.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_1_10()
    {
        JsonSByte sut = JsonSByte.ParseValue("15"u8);
        bool result = sut >= JsonSByte.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_1_11()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("10"u8);
        bool result = sut >= JsonUInt64.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_2_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("20"u8);
        bool result = sut >= JsonUInt64.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_1_12()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("15"u8);
        bool result = sut >= JsonUInt64.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_1_13()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("10"u8);
        bool result = sut >= JsonUInt32.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_2_7()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("20"u8);
        bool result = sut >= JsonUInt32.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_1_14()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("15"u8);
        bool result = sut >= JsonUInt32.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_1_15()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("10"u8);
        bool result = sut >= JsonUInt16.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_2_8()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("20"u8);
        bool result = sut >= JsonUInt16.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_1_16()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("15"u8);
        bool result = sut >= JsonUInt16.ParseValue("15"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_1_17()
    {
        JsonByte sut = JsonByte.ParseValue("10"u8);
        bool result = sut >= JsonByte.ParseValue("20"u8);
        Assert.False(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_2_9()
    {
        JsonByte sut = JsonByte.ParseValue("20"u8);
        bool result = sut >= JsonByte.ParseValue("10"u8);
        Assert.True(result);
    }

    [Fact]
    public void Compare_two_JsonElement_backed_values_using_the_greater_than_or_equal_operator_1_18()
    {
        JsonByte sut = JsonByte.ParseValue("15"u8);
        bool result = sut >= JsonByte.ParseValue("15"u8);
        Assert.True(result);
    }
}
#endif