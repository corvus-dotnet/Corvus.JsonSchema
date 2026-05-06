// <copyright file="JsonIntegerCast_net80Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Collections.Immutable;
using System.Net;
using System.Text.RegularExpressions;
using Corvus.Json;
using NodaTime;
using NodaTime.Text;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.Cast;

/// <summary>
/// Tests for JsonIntegerCast-net80.
/// </summary>
public class JsonIntegerCast_net80Tests
{
    [Fact]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_integer_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("12".AsSpan());
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_integer_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("12".AsSpan());
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_integer_JsonInt128()
    {
        var sut = JsonInt128.Parse("12").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_integer_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("12").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonNumber_for_json_element_backed_value_as_a_integer_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("12".AsSpan());
        var result = (JsonNumber)sut;
        Assert.Equal(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonNumber_for_json_element_backed_value_as_a_integer_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("12".AsSpan());
        var result = (JsonNumber)sut;
        Assert.Equal(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonNumber_for_dotnet_backed_value_as_a_integer_JsonInt128()
    {
        var sut = JsonInt128.Parse("12").AsDotnetBackedValue();
        var result = (JsonNumber)sut;
        Assert.Equal(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonNumber_for_dotnet_backed_value_as_a_integer_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("12").AsDotnetBackedValue();
        var result = (JsonNumber)sut;
        Assert.Equal(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_long_for_json_element_backed_value_as_a_integer_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("12".AsSpan());
        var result = (long)sut;
        Assert.Equal(12L, result);
    }

    [Fact]
    public void Cast_to_long_for_json_element_backed_value_as_a_integer_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("12".AsSpan());
        var result = (long)sut;
        Assert.Equal(12L, result);
    }

    [Fact]
    public void Cast_to_long_for_dotnet_backed_value_as_a_integer_JsonInt128()
    {
        var sut = JsonInt128.Parse("12").AsDotnetBackedValue();
        var result = (long)sut;
        Assert.Equal(12L, result);
    }

    [Fact]
    public void Cast_to_long_for_dotnet_backed_value_as_a_integer_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("12").AsDotnetBackedValue();
        var result = (long)sut;
        Assert.Equal(12L, result);
    }

    [Fact]
    public void Cast_from_long_for_json_element_backed_value_as_a_integer_JsonInt128()
    {
        long sut = 12L;
        var result = (JsonInt128)sut;
        Assert.Equal(JsonInt128.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_long_for_json_element_backed_value_as_a_integer_JsonUInt128()
    {
        long sut = 12L;
        var result = (JsonUInt128)sut;
        Assert.Equal(JsonUInt128.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_double_for_json_element_backed_value_as_a_integer_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("12".AsSpan());
        var result = (double)sut;
        Assert.Equal(12.0d, result, 5);
    }

    [Fact]
    public void Cast_to_double_for_json_element_backed_value_as_a_integer_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("12".AsSpan());
        var result = (double)sut;
        Assert.Equal(12.0d, result, 5);
    }

    [Fact]
    public void Cast_to_double_for_dotnet_backed_value_as_a_integer_JsonInt128()
    {
        var sut = JsonInt128.Parse("12").AsDotnetBackedValue();
        var result = (double)sut;
        Assert.Equal(12.0d, result, 5);
    }

    [Fact]
    public void Cast_to_double_for_dotnet_backed_value_as_a_integer_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("12").AsDotnetBackedValue();
        var result = (double)sut;
        Assert.Equal(12.0d, result, 5);
    }

    [Fact]
    public void Cast_from_double_for_json_element_backed_value_as_a_integer_JsonInt128()
    {
        double sut = 12.0;
        var result = (JsonInt128)sut;
        Assert.Equal(JsonInt128.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_double_for_json_element_backed_value_as_a_integer_JsonUInt128()
    {
        double sut = 12.0;
        var result = (JsonUInt128)sut;
        Assert.Equal(JsonUInt128.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_int_for_json_element_backed_value_as_a_integer_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("12".AsSpan());
        var result = (int)sut;
        Assert.Equal(12, result);
    }

    [Fact]
    public void Cast_to_int_for_json_element_backed_value_as_a_integer_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("12".AsSpan());
        var result = (int)sut;
        Assert.Equal(12, result);
    }

    [Fact]
    public void Cast_to_int_for_dotnet_backed_value_as_a_integer_JsonInt128()
    {
        var sut = JsonInt128.Parse("12").AsDotnetBackedValue();
        var result = (int)sut;
        Assert.Equal(12, result);
    }

    [Fact]
    public void Cast_to_int_for_dotnet_backed_value_as_a_integer_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("12").AsDotnetBackedValue();
        var result = (int)sut;
        Assert.Equal(12, result);
    }

    [Fact]
    public void Cast_from_int_for_json_element_backed_value_as_a_integer_JsonInt128()
    {
        int sut = 12;
        var result = (JsonInt128)sut;
        Assert.Equal(JsonInt128.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_int_for_json_element_backed_value_as_a_integer_JsonUInt128()
    {
        int sut = 12;
        var result = (JsonUInt128)sut;
        Assert.Equal(JsonUInt128.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_float_for_json_element_backed_value_as_a_integer_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("12".AsSpan());
        var result = (float)sut;
        Assert.Equal(12.0f, result, 5);
    }

    [Fact]
    public void Cast_to_float_for_json_element_backed_value_as_a_integer_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("12".AsSpan());
        var result = (float)sut;
        Assert.Equal(12.0f, result, 5);
    }

    [Fact]
    public void Cast_to_float_for_dotnet_backed_value_as_a_integer_JsonInt128()
    {
        var sut = JsonInt128.Parse("12").AsDotnetBackedValue();
        var result = (float)sut;
        Assert.Equal(12.0f, result, 5);
    }

    [Fact]
    public void Cast_to_float_for_dotnet_backed_value_as_a_integer_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("12").AsDotnetBackedValue();
        var result = (float)sut;
        Assert.Equal(12.0f, result, 5);
    }

    [Fact]
    public void Cast_from_float_for_json_element_backed_value_as_a_integer_JsonInt128()
    {
        float sut = 12f;
        var result = (JsonInt128)sut;
        Assert.Equal(JsonInt128.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_float_for_json_element_backed_value_as_a_integer_JsonUInt128()
    {
        float sut = 12f;
        var result = (JsonUInt128)sut;
        Assert.Equal(JsonUInt128.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_short_for_json_element_backed_value_as_a_shorteger_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("12".AsSpan());
        var result = (short)sut;
        Assert.Equal((short)12, result);
    }

    [Fact]
    public void Cast_to_short_for_json_element_backed_value_as_a_shorteger_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("12".AsSpan());
        var result = (short)sut;
        Assert.Equal((short)12, result);
    }

    [Fact]
    public void Cast_to_short_for_dotnet_backed_value_as_a_shorteger_JsonInt128()
    {
        var sut = JsonInt128.Parse("12").AsDotnetBackedValue();
        var result = (short)sut;
        Assert.Equal((short)12, result);
    }

    [Fact]
    public void Cast_to_short_for_dotnet_backed_value_as_a_shorteger_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("12").AsDotnetBackedValue();
        var result = (short)sut;
        Assert.Equal((short)12, result);
    }

    [Fact]
    public void Cast_to_ushort_for_json_element_backed_value_as_a_ushorteger_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("12".AsSpan());
        var result = (ushort)sut;
        Assert.Equal((ushort)12, result);
    }

    [Fact]
    public void Cast_to_ushort_for_json_element_backed_value_as_a_ushorteger_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("12".AsSpan());
        var result = (ushort)sut;
        Assert.Equal((ushort)12, result);
    }

    [Fact]
    public void Cast_to_ushort_for_dotnet_backed_value_as_a_ushorteger_JsonInt128()
    {
        var sut = JsonInt128.Parse("12").AsDotnetBackedValue();
        var result = (ushort)sut;
        Assert.Equal((ushort)12, result);
    }

    [Fact]
    public void Cast_to_ushort_for_dotnet_backed_value_as_a_ushorteger_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("12").AsDotnetBackedValue();
        var result = (ushort)sut;
        Assert.Equal((ushort)12, result);
    }

    [Fact]
    public void Cast_to_byte_for_json_element_backed_value_as_a_byteeger_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("12".AsSpan());
        var result = (byte)sut;
        Assert.Equal((byte)12, result);
    }

    [Fact]
    public void Cast_to_byte_for_json_element_backed_value_as_a_byteeger_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("12".AsSpan());
        var result = (byte)sut;
        Assert.Equal((byte)12, result);
    }

    [Fact]
    public void Cast_to_byte_for_dotnet_backed_value_as_a_byteeger_JsonInt128()
    {
        var sut = JsonInt128.Parse("12").AsDotnetBackedValue();
        var result = (byte)sut;
        Assert.Equal((byte)12, result);
    }

    [Fact]
    public void Cast_to_byte_for_dotnet_backed_value_as_a_byteeger_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("12").AsDotnetBackedValue();
        var result = (byte)sut;
        Assert.Equal((byte)12, result);
    }

    [Fact]
    public void Cast_to_sbyte_for_json_element_backed_value_as_a_sbyteeger_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("12".AsSpan());
        var result = (sbyte)sut;
        Assert.Equal((sbyte)12, result);
    }

    [Fact]
    public void Cast_to_sbyte_for_json_element_backed_value_as_a_sbyteeger_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("12".AsSpan());
        var result = (sbyte)sut;
        Assert.Equal((sbyte)12, result);
    }

    [Fact]
    public void Cast_to_sbyte_for_dotnet_backed_value_as_a_sbyteeger_JsonInt128()
    {
        var sut = JsonInt128.Parse("12").AsDotnetBackedValue();
        var result = (sbyte)sut;
        Assert.Equal((sbyte)12, result);
    }

    [Fact]
    public void Cast_to_sbyte_for_dotnet_backed_value_as_a_sbyteeger_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("12").AsDotnetBackedValue();
        var result = (sbyte)sut;
        Assert.Equal((sbyte)12, result);
    }

    [Fact]
    public void Cast_to_uint_for_json_element_backed_value_as_a_uinteger_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("12".AsSpan());
        var result = (uint)sut;
        Assert.Equal(12U, result);
    }

    [Fact]
    public void Cast_to_uint_for_json_element_backed_value_as_a_uinteger_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("12".AsSpan());
        var result = (uint)sut;
        Assert.Equal(12U, result);
    }

    [Fact]
    public void Cast_to_uint_for_dotnet_backed_value_as_a_uinteger_JsonInt128()
    {
        var sut = JsonInt128.Parse("12").AsDotnetBackedValue();
        var result = (uint)sut;
        Assert.Equal(12U, result);
    }

    [Fact]
    public void Cast_to_uint_for_dotnet_backed_value_as_a_uinteger_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("12").AsDotnetBackedValue();
        var result = (uint)sut;
        Assert.Equal(12U, result);
    }

    [Fact]
    public void Cast_to_ulong_for_json_element_backed_value_as_a_ulongeger_JsonInt128()
    {
        var sut = JsonInt128.ParseValue("12".AsSpan());
        var result = (ulong)sut;
        Assert.Equal(12UL, result);
    }

    [Fact]
    public void Cast_to_ulong_for_json_element_backed_value_as_a_ulongeger_JsonUInt128()
    {
        var sut = JsonUInt128.ParseValue("12".AsSpan());
        var result = (ulong)sut;
        Assert.Equal(12UL, result);
    }

    [Fact]
    public void Cast_to_ulong_for_dotnet_backed_value_as_a_ulongeger_JsonInt128()
    {
        var sut = JsonInt128.Parse("12").AsDotnetBackedValue();
        var result = (ulong)sut;
        Assert.Equal(12UL, result);
    }

    [Fact]
    public void Cast_to_ulong_for_dotnet_backed_value_as_a_ulongeger_JsonUInt128()
    {
        var sut = JsonUInt128.Parse("12").AsDotnetBackedValue();
        var result = (ulong)sut;
        Assert.Equal(12UL, result);
    }
}