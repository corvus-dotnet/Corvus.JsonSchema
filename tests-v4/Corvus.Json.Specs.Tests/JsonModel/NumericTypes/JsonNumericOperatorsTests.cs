// <copyright file="JsonNumericOperatorsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET
using Corvus.Json;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.NumericTypes;

public class JsonNumericOperatorsTests
{
    [Fact]
    public void JsonEement_backed_Binary_operators_JsonNumber_2_add_3_1()
    {
        JsonNumber sut = JsonNumber.ParseValue("-2"u8);
        JsonNumber opResult = sut + JsonNumber.ParseValue("3"u8);
        Assert.Equal(JsonNumber.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonNumber_2_add_3_5()
    {
        JsonNumber sut = JsonNumber.ParseValue("2"u8);
        JsonNumber opResult = sut + JsonNumber.ParseValue("3"u8);
        Assert.Equal(JsonNumber.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonNumber_2_sub_3_1()
    {
        JsonNumber sut = JsonNumber.ParseValue("2"u8);
        JsonNumber opResult = sut - JsonNumber.ParseValue("3"u8);
        Assert.Equal(JsonNumber.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonNumber_2_sub_3_5()
    {
        JsonNumber sut = JsonNumber.ParseValue("-2"u8);
        JsonNumber opResult = sut - JsonNumber.ParseValue("3"u8);
        Assert.Equal(JsonNumber.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonNumber_2_mul_3_6()
    {
        JsonNumber sut = JsonNumber.ParseValue("-2"u8);
        JsonNumber opResult = sut * JsonNumber.ParseValue("3"u8);
        Assert.Equal(JsonNumber.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonNumber_2_mul_3_6_2()
    {
        JsonNumber sut = JsonNumber.ParseValue("2"u8);
        JsonNumber opResult = sut * JsonNumber.ParseValue("3"u8);
        Assert.Equal(JsonNumber.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonNumber_15_div_3_5()
    {
        JsonNumber sut = JsonNumber.ParseValue("-15"u8);
        JsonNumber opResult = sut / JsonNumber.ParseValue("3"u8);
        Assert.Equal(JsonNumber.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonNumber_15_div_3_5_2()
    {
        JsonNumber sut = JsonNumber.ParseValue("15"u8);
        JsonNumber opResult = sut / JsonNumber.ParseValue("3"u8);
        Assert.Equal(JsonNumber.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInteger_2_add_3_1()
    {
        JsonInteger sut = JsonInteger.ParseValue("-2"u8);
        JsonInteger opResult = sut + JsonInteger.ParseValue("3"u8);
        Assert.Equal(JsonInteger.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInteger_2_add_3_5()
    {
        JsonInteger sut = JsonInteger.ParseValue("2"u8);
        JsonInteger opResult = sut + JsonInteger.ParseValue("3"u8);
        Assert.Equal(JsonInteger.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInteger_2_sub_3_1()
    {
        JsonInteger sut = JsonInteger.ParseValue("2"u8);
        JsonInteger opResult = sut - JsonInteger.ParseValue("3"u8);
        Assert.Equal(JsonInteger.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInteger_2_sub_3_5()
    {
        JsonInteger sut = JsonInteger.ParseValue("-2"u8);
        JsonInteger opResult = sut - JsonInteger.ParseValue("3"u8);
        Assert.Equal(JsonInteger.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInteger_2_mul_3_6()
    {
        JsonInteger sut = JsonInteger.ParseValue("-2"u8);
        JsonInteger opResult = sut * JsonInteger.ParseValue("3"u8);
        Assert.Equal(JsonInteger.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInteger_2_mul_3_6_2()
    {
        JsonInteger sut = JsonInteger.ParseValue("2"u8);
        JsonInteger opResult = sut * JsonInteger.ParseValue("3"u8);
        Assert.Equal(JsonInteger.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInteger_15_div_3_5()
    {
        JsonInteger sut = JsonInteger.ParseValue("-15"u8);
        JsonInteger opResult = sut / JsonInteger.ParseValue("3"u8);
        Assert.Equal(JsonInteger.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInteger_15_div_3_5_2()
    {
        JsonInteger sut = JsonInteger.ParseValue("15"u8);
        JsonInteger opResult = sut / JsonInteger.ParseValue("3"u8);
        Assert.Equal(JsonInteger.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonHalf_2_add_3_1()
    {
        JsonHalf sut = JsonHalf.ParseValue("-2"u8);
        JsonHalf opResult = sut + JsonHalf.ParseValue("3"u8);
        Assert.Equal(JsonHalf.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonHalf_2_add_3_5()
    {
        JsonHalf sut = JsonHalf.ParseValue("2"u8);
        JsonHalf opResult = sut + JsonHalf.ParseValue("3"u8);
        Assert.Equal(JsonHalf.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonHalf_2_sub_3_1()
    {
        JsonHalf sut = JsonHalf.ParseValue("2"u8);
        JsonHalf opResult = sut - JsonHalf.ParseValue("3"u8);
        Assert.Equal(JsonHalf.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonHalf_2_sub_3_5()
    {
        JsonHalf sut = JsonHalf.ParseValue("-2"u8);
        JsonHalf opResult = sut - JsonHalf.ParseValue("3"u8);
        Assert.Equal(JsonHalf.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonHalf_2_mul_3_6()
    {
        JsonHalf sut = JsonHalf.ParseValue("-2"u8);
        JsonHalf opResult = sut * JsonHalf.ParseValue("3"u8);
        Assert.Equal(JsonHalf.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonHalf_2_mul_3_6_2()
    {
        JsonHalf sut = JsonHalf.ParseValue("2"u8);
        JsonHalf opResult = sut * JsonHalf.ParseValue("3"u8);
        Assert.Equal(JsonHalf.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonHalf_15_div_3_5()
    {
        JsonHalf sut = JsonHalf.ParseValue("-15"u8);
        JsonHalf opResult = sut / JsonHalf.ParseValue("3"u8);
        Assert.Equal(JsonHalf.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonHalf_15_div_3_5_2()
    {
        JsonHalf sut = JsonHalf.ParseValue("15"u8);
        JsonHalf opResult = sut / JsonHalf.ParseValue("3"u8);
        Assert.Equal(JsonHalf.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonSingle_2_add_3_1()
    {
        JsonSingle sut = JsonSingle.ParseValue("-2"u8);
        JsonSingle opResult = sut + JsonSingle.ParseValue("3"u8);
        Assert.Equal(JsonSingle.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonSingle_2_add_3_5()
    {
        JsonSingle sut = JsonSingle.ParseValue("2"u8);
        JsonSingle opResult = sut + JsonSingle.ParseValue("3"u8);
        Assert.Equal(JsonSingle.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonSingle_2_sub_3_1()
    {
        JsonSingle sut = JsonSingle.ParseValue("2"u8);
        JsonSingle opResult = sut - JsonSingle.ParseValue("3"u8);
        Assert.Equal(JsonSingle.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonSingle_2_sub_3_5()
    {
        JsonSingle sut = JsonSingle.ParseValue("-2"u8);
        JsonSingle opResult = sut - JsonSingle.ParseValue("3"u8);
        Assert.Equal(JsonSingle.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonSingle_2_mul_3_6()
    {
        JsonSingle sut = JsonSingle.ParseValue("-2"u8);
        JsonSingle opResult = sut * JsonSingle.ParseValue("3"u8);
        Assert.Equal(JsonSingle.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonSingle_2_mul_3_6_2()
    {
        JsonSingle sut = JsonSingle.ParseValue("2"u8);
        JsonSingle opResult = sut * JsonSingle.ParseValue("3"u8);
        Assert.Equal(JsonSingle.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonSingle_15_div_3_5()
    {
        JsonSingle sut = JsonSingle.ParseValue("-15"u8);
        JsonSingle opResult = sut / JsonSingle.ParseValue("3"u8);
        Assert.Equal(JsonSingle.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonSingle_15_div_3_5_2()
    {
        JsonSingle sut = JsonSingle.ParseValue("15"u8);
        JsonSingle opResult = sut / JsonSingle.ParseValue("3"u8);
        Assert.Equal(JsonSingle.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonDouble_2_add_3_1()
    {
        JsonDouble sut = JsonDouble.ParseValue("-2"u8);
        JsonDouble opResult = sut + JsonDouble.ParseValue("3"u8);
        Assert.Equal(JsonDouble.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonDouble_2_add_3_5()
    {
        JsonDouble sut = JsonDouble.ParseValue("2"u8);
        JsonDouble opResult = sut + JsonDouble.ParseValue("3"u8);
        Assert.Equal(JsonDouble.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonDouble_2_sub_3_1()
    {
        JsonDouble sut = JsonDouble.ParseValue("2"u8);
        JsonDouble opResult = sut - JsonDouble.ParseValue("3"u8);
        Assert.Equal(JsonDouble.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonDouble_2_sub_3_5()
    {
        JsonDouble sut = JsonDouble.ParseValue("-2"u8);
        JsonDouble opResult = sut - JsonDouble.ParseValue("3"u8);
        Assert.Equal(JsonDouble.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonDouble_2_mul_3_6()
    {
        JsonDouble sut = JsonDouble.ParseValue("-2"u8);
        JsonDouble opResult = sut * JsonDouble.ParseValue("3"u8);
        Assert.Equal(JsonDouble.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonDouble_2_mul_3_6_2()
    {
        JsonDouble sut = JsonDouble.ParseValue("2"u8);
        JsonDouble opResult = sut * JsonDouble.ParseValue("3"u8);
        Assert.Equal(JsonDouble.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonDouble_15_div_3_5()
    {
        JsonDouble sut = JsonDouble.ParseValue("-15"u8);
        JsonDouble opResult = sut / JsonDouble.ParseValue("3"u8);
        Assert.Equal(JsonDouble.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonDouble_15_div_3_5_2()
    {
        JsonDouble sut = JsonDouble.ParseValue("15"u8);
        JsonDouble opResult = sut / JsonDouble.ParseValue("3"u8);
        Assert.Equal(JsonDouble.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonDecimal_2_add_3_1()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("-2"u8);
        JsonDecimal opResult = sut + JsonDecimal.ParseValue("3"u8);
        Assert.Equal(JsonDecimal.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonDecimal_2_add_3_5()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("2"u8);
        JsonDecimal opResult = sut + JsonDecimal.ParseValue("3"u8);
        Assert.Equal(JsonDecimal.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonDecimal_2_sub_3_1()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("2"u8);
        JsonDecimal opResult = sut - JsonDecimal.ParseValue("3"u8);
        Assert.Equal(JsonDecimal.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonDecimal_2_sub_3_5()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("-2"u8);
        JsonDecimal opResult = sut - JsonDecimal.ParseValue("3"u8);
        Assert.Equal(JsonDecimal.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonDecimal_2_mul_3_6()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("-2"u8);
        JsonDecimal opResult = sut * JsonDecimal.ParseValue("3"u8);
        Assert.Equal(JsonDecimal.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonDecimal_2_mul_3_6_2()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("2"u8);
        JsonDecimal opResult = sut * JsonDecimal.ParseValue("3"u8);
        Assert.Equal(JsonDecimal.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonDecimal_15_div_3_5()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("-15"u8);
        JsonDecimal opResult = sut / JsonDecimal.ParseValue("3"u8);
        Assert.Equal(JsonDecimal.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonDecimal_15_div_3_5_2()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("15"u8);
        JsonDecimal opResult = sut / JsonDecimal.ParseValue("3"u8);
        Assert.Equal(JsonDecimal.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt128_2_add_3_1()
    {
        JsonInt128 sut = JsonInt128.ParseValue("-2"u8);
        JsonInt128 opResult = sut + JsonInt128.ParseValue("3"u8);
        Assert.Equal(JsonInt128.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt128_2_add_3_5()
    {
        JsonInt128 sut = JsonInt128.ParseValue("2"u8);
        JsonInt128 opResult = sut + JsonInt128.ParseValue("3"u8);
        Assert.Equal(JsonInt128.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt128_2_sub_3_1()
    {
        JsonInt128 sut = JsonInt128.ParseValue("2"u8);
        JsonInt128 opResult = sut - JsonInt128.ParseValue("3"u8);
        Assert.Equal(JsonInt128.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt128_2_sub_3_5()
    {
        JsonInt128 sut = JsonInt128.ParseValue("-2"u8);
        JsonInt128 opResult = sut - JsonInt128.ParseValue("3"u8);
        Assert.Equal(JsonInt128.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt128_2_mul_3_6()
    {
        JsonInt128 sut = JsonInt128.ParseValue("-2"u8);
        JsonInt128 opResult = sut * JsonInt128.ParseValue("3"u8);
        Assert.Equal(JsonInt128.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt128_2_mul_3_6_2()
    {
        JsonInt128 sut = JsonInt128.ParseValue("2"u8);
        JsonInt128 opResult = sut * JsonInt128.ParseValue("3"u8);
        Assert.Equal(JsonInt128.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt128_15_div_3_5()
    {
        JsonInt128 sut = JsonInt128.ParseValue("-15"u8);
        JsonInt128 opResult = sut / JsonInt128.ParseValue("3"u8);
        Assert.Equal(JsonInt128.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt128_15_div_3_5_2()
    {
        JsonInt128 sut = JsonInt128.ParseValue("15"u8);
        JsonInt128 opResult = sut / JsonInt128.ParseValue("3"u8);
        Assert.Equal(JsonInt128.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt64_2_add_3_1()
    {
        JsonInt64 sut = JsonInt64.ParseValue("-2"u8);
        JsonInt64 opResult = sut + JsonInt64.ParseValue("3"u8);
        Assert.Equal(JsonInt64.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt64_2_add_3_5()
    {
        JsonInt64 sut = JsonInt64.ParseValue("2"u8);
        JsonInt64 opResult = sut + JsonInt64.ParseValue("3"u8);
        Assert.Equal(JsonInt64.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt64_2_sub_3_1()
    {
        JsonInt64 sut = JsonInt64.ParseValue("2"u8);
        JsonInt64 opResult = sut - JsonInt64.ParseValue("3"u8);
        Assert.Equal(JsonInt64.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt64_2_sub_3_5()
    {
        JsonInt64 sut = JsonInt64.ParseValue("-2"u8);
        JsonInt64 opResult = sut - JsonInt64.ParseValue("3"u8);
        Assert.Equal(JsonInt64.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt64_2_mul_3_6()
    {
        JsonInt64 sut = JsonInt64.ParseValue("-2"u8);
        JsonInt64 opResult = sut * JsonInt64.ParseValue("3"u8);
        Assert.Equal(JsonInt64.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt64_2_mul_3_6_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("2"u8);
        JsonInt64 opResult = sut * JsonInt64.ParseValue("3"u8);
        Assert.Equal(JsonInt64.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt64_15_div_3_5()
    {
        JsonInt64 sut = JsonInt64.ParseValue("-15"u8);
        JsonInt64 opResult = sut / JsonInt64.ParseValue("3"u8);
        Assert.Equal(JsonInt64.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt64_15_div_3_5_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("15"u8);
        JsonInt64 opResult = sut / JsonInt64.ParseValue("3"u8);
        Assert.Equal(JsonInt64.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt32_2_add_3_1()
    {
        JsonInt32 sut = JsonInt32.ParseValue("-2"u8);
        JsonInt32 opResult = sut + JsonInt32.ParseValue("3"u8);
        Assert.Equal(JsonInt32.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt32_2_add_3_5()
    {
        JsonInt32 sut = JsonInt32.ParseValue("2"u8);
        JsonInt32 opResult = sut + JsonInt32.ParseValue("3"u8);
        Assert.Equal(JsonInt32.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt32_2_sub_3_1()
    {
        JsonInt32 sut = JsonInt32.ParseValue("2"u8);
        JsonInt32 opResult = sut - JsonInt32.ParseValue("3"u8);
        Assert.Equal(JsonInt32.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt32_2_sub_3_5()
    {
        JsonInt32 sut = JsonInt32.ParseValue("-2"u8);
        JsonInt32 opResult = sut - JsonInt32.ParseValue("3"u8);
        Assert.Equal(JsonInt32.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt32_2_mul_3_6()
    {
        JsonInt32 sut = JsonInt32.ParseValue("-2"u8);
        JsonInt32 opResult = sut * JsonInt32.ParseValue("3"u8);
        Assert.Equal(JsonInt32.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt32_2_mul_3_6_2()
    {
        JsonInt32 sut = JsonInt32.ParseValue("2"u8);
        JsonInt32 opResult = sut * JsonInt32.ParseValue("3"u8);
        Assert.Equal(JsonInt32.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt32_15_div_3_5()
    {
        JsonInt32 sut = JsonInt32.ParseValue("-15"u8);
        JsonInt32 opResult = sut / JsonInt32.ParseValue("3"u8);
        Assert.Equal(JsonInt32.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt32_15_div_3_5_2()
    {
        JsonInt32 sut = JsonInt32.ParseValue("15"u8);
        JsonInt32 opResult = sut / JsonInt32.ParseValue("3"u8);
        Assert.Equal(JsonInt32.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt16_2_add_3_1()
    {
        JsonInt16 sut = JsonInt16.ParseValue("-2"u8);
        JsonInt16 opResult = sut + JsonInt16.ParseValue("3"u8);
        Assert.Equal(JsonInt16.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt16_2_add_3_5()
    {
        JsonInt16 sut = JsonInt16.ParseValue("2"u8);
        JsonInt16 opResult = sut + JsonInt16.ParseValue("3"u8);
        Assert.Equal(JsonInt16.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt16_2_sub_3_1()
    {
        JsonInt16 sut = JsonInt16.ParseValue("2"u8);
        JsonInt16 opResult = sut - JsonInt16.ParseValue("3"u8);
        Assert.Equal(JsonInt16.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt16_2_sub_3_5()
    {
        JsonInt16 sut = JsonInt16.ParseValue("-2"u8);
        JsonInt16 opResult = sut - JsonInt16.ParseValue("3"u8);
        Assert.Equal(JsonInt16.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt16_2_mul_3_6()
    {
        JsonInt16 sut = JsonInt16.ParseValue("-2"u8);
        JsonInt16 opResult = sut * JsonInt16.ParseValue("3"u8);
        Assert.Equal(JsonInt16.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt16_2_mul_3_6_2()
    {
        JsonInt16 sut = JsonInt16.ParseValue("2"u8);
        JsonInt16 opResult = sut * JsonInt16.ParseValue("3"u8);
        Assert.Equal(JsonInt16.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt16_15_div_3_5()
    {
        JsonInt16 sut = JsonInt16.ParseValue("-15"u8);
        JsonInt16 opResult = sut / JsonInt16.ParseValue("3"u8);
        Assert.Equal(JsonInt16.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonInt16_15_div_3_5_2()
    {
        JsonInt16 sut = JsonInt16.ParseValue("15"u8);
        JsonInt16 opResult = sut / JsonInt16.ParseValue("3"u8);
        Assert.Equal(JsonInt16.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonSByte_2_add_3_1()
    {
        JsonSByte sut = JsonSByte.ParseValue("-2"u8);
        JsonSByte opResult = sut + JsonSByte.ParseValue("3"u8);
        Assert.Equal(JsonSByte.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonSByte_2_add_3_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("2"u8);
        JsonSByte opResult = sut + JsonSByte.ParseValue("3"u8);
        Assert.Equal(JsonSByte.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonSByte_2_sub_3_1()
    {
        JsonSByte sut = JsonSByte.ParseValue("2"u8);
        JsonSByte opResult = sut - JsonSByte.ParseValue("3"u8);
        Assert.Equal(JsonSByte.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonSByte_2_sub_3_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("-2"u8);
        JsonSByte opResult = sut - JsonSByte.ParseValue("3"u8);
        Assert.Equal(JsonSByte.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonSByte_2_mul_3_6()
    {
        JsonSByte sut = JsonSByte.ParseValue("-2"u8);
        JsonSByte opResult = sut * JsonSByte.ParseValue("3"u8);
        Assert.Equal(JsonSByte.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonSByte_2_mul_3_6_2()
    {
        JsonSByte sut = JsonSByte.ParseValue("2"u8);
        JsonSByte opResult = sut * JsonSByte.ParseValue("3"u8);
        Assert.Equal(JsonSByte.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonSByte_15_div_3_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("-15"u8);
        JsonSByte opResult = sut / JsonSByte.ParseValue("3"u8);
        Assert.Equal(JsonSByte.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonSByte_15_div_3_5_2()
    {
        JsonSByte sut = JsonSByte.ParseValue("15"u8);
        JsonSByte opResult = sut / JsonSByte.ParseValue("3"u8);
        Assert.Equal(JsonSByte.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonUInt128_2_add_3_5()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("2"u8);
        JsonUInt128 opResult = sut + JsonUInt128.ParseValue("3"u8);
        Assert.Equal(JsonUInt128.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonUInt128_3_sub_2_1()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("3"u8);
        JsonUInt128 opResult = sut - JsonUInt128.ParseValue("2"u8);
        Assert.Equal(JsonUInt128.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonUInt128_2_mul_3_6()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("2"u8);
        JsonUInt128 opResult = sut * JsonUInt128.ParseValue("3"u8);
        Assert.Equal(JsonUInt128.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonUInt128_15_div_3_5()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("15"u8);
        JsonUInt128 opResult = sut / JsonUInt128.ParseValue("3"u8);
        Assert.Equal(JsonUInt128.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonUInt64_2_add_3_5()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("2"u8);
        JsonUInt64 opResult = sut + JsonUInt64.ParseValue("3"u8);
        Assert.Equal(JsonUInt64.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonUInt64_3_sub_2_1()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("3"u8);
        JsonUInt64 opResult = sut - JsonUInt64.ParseValue("2"u8);
        Assert.Equal(JsonUInt64.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonUInt64_2_mul_3_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("2"u8);
        JsonUInt64 opResult = sut * JsonUInt64.ParseValue("3"u8);
        Assert.Equal(JsonUInt64.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonUInt64_15_div_3_5()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("15"u8);
        JsonUInt64 opResult = sut / JsonUInt64.ParseValue("3"u8);
        Assert.Equal(JsonUInt64.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonUInt32_2_add_3_5()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("2"u8);
        JsonUInt32 opResult = sut + JsonUInt32.ParseValue("3"u8);
        Assert.Equal(JsonUInt32.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonUInt32_3_sub_2_1()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("3"u8);
        JsonUInt32 opResult = sut - JsonUInt32.ParseValue("2"u8);
        Assert.Equal(JsonUInt32.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonUInt32_2_mul_3_6()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("2"u8);
        JsonUInt32 opResult = sut * JsonUInt32.ParseValue("3"u8);
        Assert.Equal(JsonUInt32.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonUInt32_15_div_3_5()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("15"u8);
        JsonUInt32 opResult = sut / JsonUInt32.ParseValue("3"u8);
        Assert.Equal(JsonUInt32.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonUInt16_2_add_3_5()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("2"u8);
        JsonUInt16 opResult = sut + JsonUInt16.ParseValue("3"u8);
        Assert.Equal(JsonUInt16.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonUInt16_3_sub_2_1()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("3"u8);
        JsonUInt16 opResult = sut - JsonUInt16.ParseValue("2"u8);
        Assert.Equal(JsonUInt16.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonUInt16_2_mul_3_6()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("2"u8);
        JsonUInt16 opResult = sut * JsonUInt16.ParseValue("3"u8);
        Assert.Equal(JsonUInt16.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonUInt16_15_div_3_5()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("15"u8);
        JsonUInt16 opResult = sut / JsonUInt16.ParseValue("3"u8);
        Assert.Equal(JsonUInt16.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonByte_2_add_3_5()
    {
        JsonByte sut = JsonByte.ParseValue("2"u8);
        JsonByte opResult = sut + JsonByte.ParseValue("3"u8);
        Assert.Equal(JsonByte.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonByte_3_sub_2_1()
    {
        JsonByte sut = JsonByte.ParseValue("3"u8);
        JsonByte opResult = sut - JsonByte.ParseValue("2"u8);
        Assert.Equal(JsonByte.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonByte_2_mul_3_6()
    {
        JsonByte sut = JsonByte.ParseValue("2"u8);
        JsonByte opResult = sut * JsonByte.ParseValue("3"u8);
        Assert.Equal(JsonByte.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void JsonEement_backed_Binary_operators_JsonByte_15_div_3_5()
    {
        JsonByte sut = JsonByte.ParseValue("15"u8);
        JsonByte opResult = sut / JsonByte.ParseValue("3"u8);
        Assert.Equal(JsonByte.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonNumber_2_add_3_1()
    {
        JsonNumber sut = JsonNumber.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonNumber opResult = sut + JsonNumber.ParseValue("3"u8);
        Assert.Equal(JsonNumber.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonNumber_2_add_3_5()
    {
        JsonNumber sut = JsonNumber.ParseValue("2"u8).AsDotnetBackedValue();
        JsonNumber opResult = sut + JsonNumber.ParseValue("3"u8);
        Assert.Equal(JsonNumber.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonNumber_2_sub_3_1()
    {
        JsonNumber sut = JsonNumber.ParseValue("2"u8).AsDotnetBackedValue();
        JsonNumber opResult = sut - JsonNumber.ParseValue("3"u8);
        Assert.Equal(JsonNumber.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonNumber_2_sub_3_5()
    {
        JsonNumber sut = JsonNumber.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonNumber opResult = sut - JsonNumber.ParseValue("3"u8);
        Assert.Equal(JsonNumber.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonNumber_2_mul_3_6()
    {
        JsonNumber sut = JsonNumber.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonNumber opResult = sut * JsonNumber.ParseValue("3"u8);
        Assert.Equal(JsonNumber.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonNumber_2_mul_3_6_2()
    {
        JsonNumber sut = JsonNumber.ParseValue("2"u8).AsDotnetBackedValue();
        JsonNumber opResult = sut * JsonNumber.ParseValue("3"u8);
        Assert.Equal(JsonNumber.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonNumber_15_div_3_5()
    {
        JsonNumber sut = JsonNumber.ParseValue("-15"u8).AsDotnetBackedValue();
        JsonNumber opResult = sut / JsonNumber.ParseValue("3"u8);
        Assert.Equal(JsonNumber.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonNumber_15_div_3_5_2()
    {
        JsonNumber sut = JsonNumber.ParseValue("15"u8).AsDotnetBackedValue();
        JsonNumber opResult = sut / JsonNumber.ParseValue("3"u8);
        Assert.Equal(JsonNumber.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInteger_2_add_3_1()
    {
        JsonInteger sut = JsonInteger.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonInteger opResult = sut + JsonInteger.ParseValue("3"u8);
        Assert.Equal(JsonInteger.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInteger_2_add_3_5()
    {
        JsonInteger sut = JsonInteger.ParseValue("2"u8).AsDotnetBackedValue();
        JsonInteger opResult = sut + JsonInteger.ParseValue("3"u8);
        Assert.Equal(JsonInteger.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInteger_2_sub_3_1()
    {
        JsonInteger sut = JsonInteger.ParseValue("2"u8).AsDotnetBackedValue();
        JsonInteger opResult = sut - JsonInteger.ParseValue("3"u8);
        Assert.Equal(JsonInteger.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInteger_2_sub_3_5()
    {
        JsonInteger sut = JsonInteger.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonInteger opResult = sut - JsonInteger.ParseValue("3"u8);
        Assert.Equal(JsonInteger.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInteger_2_mul_3_6()
    {
        JsonInteger sut = JsonInteger.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonInteger opResult = sut * JsonInteger.ParseValue("3"u8);
        Assert.Equal(JsonInteger.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInteger_2_mul_3_6_2()
    {
        JsonInteger sut = JsonInteger.ParseValue("2"u8).AsDotnetBackedValue();
        JsonInteger opResult = sut * JsonInteger.ParseValue("3"u8);
        Assert.Equal(JsonInteger.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInteger_15_div_3_5()
    {
        JsonInteger sut = JsonInteger.ParseValue("-15"u8).AsDotnetBackedValue();
        JsonInteger opResult = sut / JsonInteger.ParseValue("3"u8);
        Assert.Equal(JsonInteger.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInteger_15_div_3_5_2()
    {
        JsonInteger sut = JsonInteger.ParseValue("15"u8).AsDotnetBackedValue();
        JsonInteger opResult = sut / JsonInteger.ParseValue("3"u8);
        Assert.Equal(JsonInteger.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonHalf_2_add_3_1()
    {
        JsonHalf sut = JsonHalf.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonHalf opResult = sut + JsonHalf.ParseValue("3"u8);
        Assert.Equal(JsonHalf.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonHalf_2_add_3_5()
    {
        JsonHalf sut = JsonHalf.ParseValue("2"u8).AsDotnetBackedValue();
        JsonHalf opResult = sut + JsonHalf.ParseValue("3"u8);
        Assert.Equal(JsonHalf.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonHalf_2_sub_3_1()
    {
        JsonHalf sut = JsonHalf.ParseValue("2"u8).AsDotnetBackedValue();
        JsonHalf opResult = sut - JsonHalf.ParseValue("3"u8);
        Assert.Equal(JsonHalf.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonHalf_2_sub_3_5()
    {
        JsonHalf sut = JsonHalf.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonHalf opResult = sut - JsonHalf.ParseValue("3"u8);
        Assert.Equal(JsonHalf.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonHalf_2_mul_3_6()
    {
        JsonHalf sut = JsonHalf.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonHalf opResult = sut * JsonHalf.ParseValue("3"u8);
        Assert.Equal(JsonHalf.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonHalf_2_mul_3_6_2()
    {
        JsonHalf sut = JsonHalf.ParseValue("2"u8).AsDotnetBackedValue();
        JsonHalf opResult = sut * JsonHalf.ParseValue("3"u8);
        Assert.Equal(JsonHalf.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonHalf_15_div_3_5()
    {
        JsonHalf sut = JsonHalf.ParseValue("-15"u8).AsDotnetBackedValue();
        JsonHalf opResult = sut / JsonHalf.ParseValue("3"u8);
        Assert.Equal(JsonHalf.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonHalf_15_div_3_5_2()
    {
        JsonHalf sut = JsonHalf.ParseValue("15"u8).AsDotnetBackedValue();
        JsonHalf opResult = sut / JsonHalf.ParseValue("3"u8);
        Assert.Equal(JsonHalf.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonSingle_2_add_3_1()
    {
        JsonSingle sut = JsonSingle.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonSingle opResult = sut + JsonSingle.ParseValue("3"u8);
        Assert.Equal(JsonSingle.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonSingle_2_add_3_5()
    {
        JsonSingle sut = JsonSingle.ParseValue("2"u8).AsDotnetBackedValue();
        JsonSingle opResult = sut + JsonSingle.ParseValue("3"u8);
        Assert.Equal(JsonSingle.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonSingle_2_sub_3_1()
    {
        JsonSingle sut = JsonSingle.ParseValue("2"u8).AsDotnetBackedValue();
        JsonSingle opResult = sut - JsonSingle.ParseValue("3"u8);
        Assert.Equal(JsonSingle.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonSingle_2_sub_3_5()
    {
        JsonSingle sut = JsonSingle.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonSingle opResult = sut - JsonSingle.ParseValue("3"u8);
        Assert.Equal(JsonSingle.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonSingle_2_mul_3_6()
    {
        JsonSingle sut = JsonSingle.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonSingle opResult = sut * JsonSingle.ParseValue("3"u8);
        Assert.Equal(JsonSingle.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonSingle_2_mul_3_6_2()
    {
        JsonSingle sut = JsonSingle.ParseValue("2"u8).AsDotnetBackedValue();
        JsonSingle opResult = sut * JsonSingle.ParseValue("3"u8);
        Assert.Equal(JsonSingle.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonSingle_15_div_3_5()
    {
        JsonSingle sut = JsonSingle.ParseValue("-15"u8).AsDotnetBackedValue();
        JsonSingle opResult = sut / JsonSingle.ParseValue("3"u8);
        Assert.Equal(JsonSingle.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonSingle_15_div_3_5_2()
    {
        JsonSingle sut = JsonSingle.ParseValue("15"u8).AsDotnetBackedValue();
        JsonSingle opResult = sut / JsonSingle.ParseValue("3"u8);
        Assert.Equal(JsonSingle.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonDouble_2_add_3_1()
    {
        JsonDouble sut = JsonDouble.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonDouble opResult = sut + JsonDouble.ParseValue("3"u8);
        Assert.Equal(JsonDouble.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonDouble_2_add_3_5()
    {
        JsonDouble sut = JsonDouble.ParseValue("2"u8).AsDotnetBackedValue();
        JsonDouble opResult = sut + JsonDouble.ParseValue("3"u8);
        Assert.Equal(JsonDouble.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonDouble_2_sub_3_1()
    {
        JsonDouble sut = JsonDouble.ParseValue("2"u8).AsDotnetBackedValue();
        JsonDouble opResult = sut - JsonDouble.ParseValue("3"u8);
        Assert.Equal(JsonDouble.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonDouble_2_sub_3_5()
    {
        JsonDouble sut = JsonDouble.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonDouble opResult = sut - JsonDouble.ParseValue("3"u8);
        Assert.Equal(JsonDouble.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonDouble_2_mul_3_6()
    {
        JsonDouble sut = JsonDouble.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonDouble opResult = sut * JsonDouble.ParseValue("3"u8);
        Assert.Equal(JsonDouble.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonDouble_2_mul_3_6_2()
    {
        JsonDouble sut = JsonDouble.ParseValue("2"u8).AsDotnetBackedValue();
        JsonDouble opResult = sut * JsonDouble.ParseValue("3"u8);
        Assert.Equal(JsonDouble.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonDouble_15_div_3_5()
    {
        JsonDouble sut = JsonDouble.ParseValue("-15"u8).AsDotnetBackedValue();
        JsonDouble opResult = sut / JsonDouble.ParseValue("3"u8);
        Assert.Equal(JsonDouble.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonDouble_15_div_3_5_2()
    {
        JsonDouble sut = JsonDouble.ParseValue("15"u8).AsDotnetBackedValue();
        JsonDouble opResult = sut / JsonDouble.ParseValue("3"u8);
        Assert.Equal(JsonDouble.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonDecimal_2_add_3_1()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonDecimal opResult = sut + JsonDecimal.ParseValue("3"u8);
        Assert.Equal(JsonDecimal.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonDecimal_2_add_3_5()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("2"u8).AsDotnetBackedValue();
        JsonDecimal opResult = sut + JsonDecimal.ParseValue("3"u8);
        Assert.Equal(JsonDecimal.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonDecimal_2_sub_3_1()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("2"u8).AsDotnetBackedValue();
        JsonDecimal opResult = sut - JsonDecimal.ParseValue("3"u8);
        Assert.Equal(JsonDecimal.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonDecimal_2_sub_3_5()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonDecimal opResult = sut - JsonDecimal.ParseValue("3"u8);
        Assert.Equal(JsonDecimal.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonDecimal_2_mul_3_6()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonDecimal opResult = sut * JsonDecimal.ParseValue("3"u8);
        Assert.Equal(JsonDecimal.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonDecimal_2_mul_3_6_2()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("2"u8).AsDotnetBackedValue();
        JsonDecimal opResult = sut * JsonDecimal.ParseValue("3"u8);
        Assert.Equal(JsonDecimal.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonDecimal_15_div_3_5()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("-15"u8).AsDotnetBackedValue();
        JsonDecimal opResult = sut / JsonDecimal.ParseValue("3"u8);
        Assert.Equal(JsonDecimal.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonDecimal_15_div_3_5_2()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("15"u8).AsDotnetBackedValue();
        JsonDecimal opResult = sut / JsonDecimal.ParseValue("3"u8);
        Assert.Equal(JsonDecimal.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt128_2_add_3_1()
    {
        JsonInt128 sut = JsonInt128.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonInt128 opResult = sut + JsonInt128.ParseValue("3"u8);
        Assert.Equal(JsonInt128.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt128_2_add_3_5()
    {
        JsonInt128 sut = JsonInt128.ParseValue("2"u8).AsDotnetBackedValue();
        JsonInt128 opResult = sut + JsonInt128.ParseValue("3"u8);
        Assert.Equal(JsonInt128.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt128_2_sub_3_1()
    {
        JsonInt128 sut = JsonInt128.ParseValue("2"u8).AsDotnetBackedValue();
        JsonInt128 opResult = sut - JsonInt128.ParseValue("3"u8);
        Assert.Equal(JsonInt128.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt128_2_sub_3_5()
    {
        JsonInt128 sut = JsonInt128.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonInt128 opResult = sut - JsonInt128.ParseValue("3"u8);
        Assert.Equal(JsonInt128.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt128_2_mul_3_6()
    {
        JsonInt128 sut = JsonInt128.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonInt128 opResult = sut * JsonInt128.ParseValue("3"u8);
        Assert.Equal(JsonInt128.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt128_2_mul_3_6_2()
    {
        JsonInt128 sut = JsonInt128.ParseValue("2"u8).AsDotnetBackedValue();
        JsonInt128 opResult = sut * JsonInt128.ParseValue("3"u8);
        Assert.Equal(JsonInt128.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt128_15_div_3_5()
    {
        JsonInt128 sut = JsonInt128.ParseValue("-15"u8).AsDotnetBackedValue();
        JsonInt128 opResult = sut / JsonInt128.ParseValue("3"u8);
        Assert.Equal(JsonInt128.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt128_15_div_3_5_2()
    {
        JsonInt128 sut = JsonInt128.ParseValue("15"u8).AsDotnetBackedValue();
        JsonInt128 opResult = sut / JsonInt128.ParseValue("3"u8);
        Assert.Equal(JsonInt128.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt64_2_add_3_1()
    {
        JsonInt64 sut = JsonInt64.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonInt64 opResult = sut + JsonInt64.ParseValue("3"u8);
        Assert.Equal(JsonInt64.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt64_2_add_3_5()
    {
        JsonInt64 sut = JsonInt64.ParseValue("2"u8).AsDotnetBackedValue();
        JsonInt64 opResult = sut + JsonInt64.ParseValue("3"u8);
        Assert.Equal(JsonInt64.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt64_2_sub_3_1()
    {
        JsonInt64 sut = JsonInt64.ParseValue("2"u8).AsDotnetBackedValue();
        JsonInt64 opResult = sut - JsonInt64.ParseValue("3"u8);
        Assert.Equal(JsonInt64.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt64_2_sub_3_5()
    {
        JsonInt64 sut = JsonInt64.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonInt64 opResult = sut - JsonInt64.ParseValue("3"u8);
        Assert.Equal(JsonInt64.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt64_2_mul_3_6()
    {
        JsonInt64 sut = JsonInt64.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonInt64 opResult = sut * JsonInt64.ParseValue("3"u8);
        Assert.Equal(JsonInt64.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt64_2_mul_3_6_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("2"u8).AsDotnetBackedValue();
        JsonInt64 opResult = sut * JsonInt64.ParseValue("3"u8);
        Assert.Equal(JsonInt64.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt64_15_div_3_5()
    {
        JsonInt64 sut = JsonInt64.ParseValue("-15"u8).AsDotnetBackedValue();
        JsonInt64 opResult = sut / JsonInt64.ParseValue("3"u8);
        Assert.Equal(JsonInt64.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt64_15_div_3_5_2()
    {
        JsonInt64 sut = JsonInt64.ParseValue("15"u8).AsDotnetBackedValue();
        JsonInt64 opResult = sut / JsonInt64.ParseValue("3"u8);
        Assert.Equal(JsonInt64.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt32_2_add_3_1()
    {
        JsonInt32 sut = JsonInt32.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonInt32 opResult = sut + JsonInt32.ParseValue("3"u8);
        Assert.Equal(JsonInt32.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt32_2_add_3_5()
    {
        JsonInt32 sut = JsonInt32.ParseValue("2"u8).AsDotnetBackedValue();
        JsonInt32 opResult = sut + JsonInt32.ParseValue("3"u8);
        Assert.Equal(JsonInt32.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt32_2_sub_3_1()
    {
        JsonInt32 sut = JsonInt32.ParseValue("2"u8).AsDotnetBackedValue();
        JsonInt32 opResult = sut - JsonInt32.ParseValue("3"u8);
        Assert.Equal(JsonInt32.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt32_2_sub_3_5()
    {
        JsonInt32 sut = JsonInt32.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonInt32 opResult = sut - JsonInt32.ParseValue("3"u8);
        Assert.Equal(JsonInt32.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt32_2_mul_3_6()
    {
        JsonInt32 sut = JsonInt32.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonInt32 opResult = sut * JsonInt32.ParseValue("3"u8);
        Assert.Equal(JsonInt32.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt32_2_mul_3_6_2()
    {
        JsonInt32 sut = JsonInt32.ParseValue("2"u8).AsDotnetBackedValue();
        JsonInt32 opResult = sut * JsonInt32.ParseValue("3"u8);
        Assert.Equal(JsonInt32.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt32_15_div_3_5()
    {
        JsonInt32 sut = JsonInt32.ParseValue("-15"u8).AsDotnetBackedValue();
        JsonInt32 opResult = sut / JsonInt32.ParseValue("3"u8);
        Assert.Equal(JsonInt32.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt32_15_div_3_5_2()
    {
        JsonInt32 sut = JsonInt32.ParseValue("15"u8).AsDotnetBackedValue();
        JsonInt32 opResult = sut / JsonInt32.ParseValue("3"u8);
        Assert.Equal(JsonInt32.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt16_2_add_3_1()
    {
        JsonInt16 sut = JsonInt16.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonInt16 opResult = sut + JsonInt16.ParseValue("3"u8);
        Assert.Equal(JsonInt16.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt16_2_add_3_5()
    {
        JsonInt16 sut = JsonInt16.ParseValue("2"u8).AsDotnetBackedValue();
        JsonInt16 opResult = sut + JsonInt16.ParseValue("3"u8);
        Assert.Equal(JsonInt16.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt16_2_sub_3_1()
    {
        JsonInt16 sut = JsonInt16.ParseValue("2"u8).AsDotnetBackedValue();
        JsonInt16 opResult = sut - JsonInt16.ParseValue("3"u8);
        Assert.Equal(JsonInt16.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt16_2_sub_3_5()
    {
        JsonInt16 sut = JsonInt16.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonInt16 opResult = sut - JsonInt16.ParseValue("3"u8);
        Assert.Equal(JsonInt16.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt16_2_mul_3_6()
    {
        JsonInt16 sut = JsonInt16.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonInt16 opResult = sut * JsonInt16.ParseValue("3"u8);
        Assert.Equal(JsonInt16.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt16_2_mul_3_6_2()
    {
        JsonInt16 sut = JsonInt16.ParseValue("2"u8).AsDotnetBackedValue();
        JsonInt16 opResult = sut * JsonInt16.ParseValue("3"u8);
        Assert.Equal(JsonInt16.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt16_15_div_3_5()
    {
        JsonInt16 sut = JsonInt16.ParseValue("-15"u8).AsDotnetBackedValue();
        JsonInt16 opResult = sut / JsonInt16.ParseValue("3"u8);
        Assert.Equal(JsonInt16.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonInt16_15_div_3_5_2()
    {
        JsonInt16 sut = JsonInt16.ParseValue("15"u8).AsDotnetBackedValue();
        JsonInt16 opResult = sut / JsonInt16.ParseValue("3"u8);
        Assert.Equal(JsonInt16.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonSByte_2_add_3_1()
    {
        JsonSByte sut = JsonSByte.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonSByte opResult = sut + JsonSByte.ParseValue("3"u8);
        Assert.Equal(JsonSByte.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonSByte_2_add_3_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("2"u8).AsDotnetBackedValue();
        JsonSByte opResult = sut + JsonSByte.ParseValue("3"u8);
        Assert.Equal(JsonSByte.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonSByte_2_sub_3_1()
    {
        JsonSByte sut = JsonSByte.ParseValue("2"u8).AsDotnetBackedValue();
        JsonSByte opResult = sut - JsonSByte.ParseValue("3"u8);
        Assert.Equal(JsonSByte.ParseValue("-1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonSByte_2_sub_3_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonSByte opResult = sut - JsonSByte.ParseValue("3"u8);
        Assert.Equal(JsonSByte.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonSByte_2_mul_3_6()
    {
        JsonSByte sut = JsonSByte.ParseValue("-2"u8).AsDotnetBackedValue();
        JsonSByte opResult = sut * JsonSByte.ParseValue("3"u8);
        Assert.Equal(JsonSByte.ParseValue("-6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonSByte_2_mul_3_6_2()
    {
        JsonSByte sut = JsonSByte.ParseValue("2"u8).AsDotnetBackedValue();
        JsonSByte opResult = sut * JsonSByte.ParseValue("3"u8);
        Assert.Equal(JsonSByte.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonSByte_15_div_3_5()
    {
        JsonSByte sut = JsonSByte.ParseValue("-15"u8).AsDotnetBackedValue();
        JsonSByte opResult = sut / JsonSByte.ParseValue("3"u8);
        Assert.Equal(JsonSByte.ParseValue("-5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonSByte_15_div_3_5_2()
    {
        JsonSByte sut = JsonSByte.ParseValue("15"u8).AsDotnetBackedValue();
        JsonSByte opResult = sut / JsonSByte.ParseValue("3"u8);
        Assert.Equal(JsonSByte.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonUInt128_2_add_3_5()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("2"u8).AsDotnetBackedValue();
        JsonUInt128 opResult = sut + JsonUInt128.ParseValue("3"u8);
        Assert.Equal(JsonUInt128.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonUInt128_3_sub_2_1()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("3"u8).AsDotnetBackedValue();
        JsonUInt128 opResult = sut - JsonUInt128.ParseValue("2"u8);
        Assert.Equal(JsonUInt128.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonUInt128_2_mul_3_6()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("2"u8).AsDotnetBackedValue();
        JsonUInt128 opResult = sut * JsonUInt128.ParseValue("3"u8);
        Assert.Equal(JsonUInt128.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonUInt128_15_div_3_5()
    {
        JsonUInt128 sut = JsonUInt128.ParseValue("15"u8).AsDotnetBackedValue();
        JsonUInt128 opResult = sut / JsonUInt128.ParseValue("3"u8);
        Assert.Equal(JsonUInt128.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonUInt64_2_add_3_5()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("2"u8).AsDotnetBackedValue();
        JsonUInt64 opResult = sut + JsonUInt64.ParseValue("3"u8);
        Assert.Equal(JsonUInt64.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonUInt64_3_sub_2_1()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("3"u8).AsDotnetBackedValue();
        JsonUInt64 opResult = sut - JsonUInt64.ParseValue("2"u8);
        Assert.Equal(JsonUInt64.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonUInt64_2_mul_3_6()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("2"u8).AsDotnetBackedValue();
        JsonUInt64 opResult = sut * JsonUInt64.ParseValue("3"u8);
        Assert.Equal(JsonUInt64.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonUInt64_15_div_3_5()
    {
        JsonUInt64 sut = JsonUInt64.ParseValue("15"u8).AsDotnetBackedValue();
        JsonUInt64 opResult = sut / JsonUInt64.ParseValue("3"u8);
        Assert.Equal(JsonUInt64.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonUInt32_2_add_3_5()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("2"u8).AsDotnetBackedValue();
        JsonUInt32 opResult = sut + JsonUInt32.ParseValue("3"u8);
        Assert.Equal(JsonUInt32.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonUInt32_3_sub_2_1()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("3"u8).AsDotnetBackedValue();
        JsonUInt32 opResult = sut - JsonUInt32.ParseValue("2"u8);
        Assert.Equal(JsonUInt32.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonUInt32_2_mul_3_6()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("2"u8).AsDotnetBackedValue();
        JsonUInt32 opResult = sut * JsonUInt32.ParseValue("3"u8);
        Assert.Equal(JsonUInt32.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonUInt32_15_div_3_5()
    {
        JsonUInt32 sut = JsonUInt32.ParseValue("15"u8).AsDotnetBackedValue();
        JsonUInt32 opResult = sut / JsonUInt32.ParseValue("3"u8);
        Assert.Equal(JsonUInt32.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonUInt16_2_add_3_5()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("2"u8).AsDotnetBackedValue();
        JsonUInt16 opResult = sut + JsonUInt16.ParseValue("3"u8);
        Assert.Equal(JsonUInt16.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonUInt16_3_sub_2_1()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("3"u8).AsDotnetBackedValue();
        JsonUInt16 opResult = sut - JsonUInt16.ParseValue("2"u8);
        Assert.Equal(JsonUInt16.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonUInt16_2_mul_3_6()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("2"u8).AsDotnetBackedValue();
        JsonUInt16 opResult = sut * JsonUInt16.ParseValue("3"u8);
        Assert.Equal(JsonUInt16.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonUInt16_15_div_3_5()
    {
        JsonUInt16 sut = JsonUInt16.ParseValue("15"u8).AsDotnetBackedValue();
        JsonUInt16 opResult = sut / JsonUInt16.ParseValue("3"u8);
        Assert.Equal(JsonUInt16.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonByte_2_add_3_5()
    {
        JsonByte sut = JsonByte.ParseValue("2"u8).AsDotnetBackedValue();
        JsonByte opResult = sut + JsonByte.ParseValue("3"u8);
        Assert.Equal(JsonByte.ParseValue("5"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonByte_3_sub_2_1()
    {
        JsonByte sut = JsonByte.ParseValue("3"u8).AsDotnetBackedValue();
        JsonByte opResult = sut - JsonByte.ParseValue("2"u8);
        Assert.Equal(JsonByte.ParseValue("1"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonByte_2_mul_3_6()
    {
        JsonByte sut = JsonByte.ParseValue("2"u8).AsDotnetBackedValue();
        JsonByte opResult = sut * JsonByte.ParseValue("3"u8);
        Assert.Equal(JsonByte.ParseValue("6"u8), opResult);
    }

    [Fact]
    public void Dotnet_backed_Binary_operators_JsonByte_15_div_3_5()
    {
        JsonByte sut = JsonByte.ParseValue("15"u8).AsDotnetBackedValue();
        JsonByte opResult = sut / JsonByte.ParseValue("3"u8);
        Assert.Equal(JsonByte.ParseValue("5"u8), opResult);
    }
}
#endif