// <copyright file="JsonNumberCast_net80Tests.cs" company="Endjin Limited">
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
/// Tests for JsonNumberCast-net80.
/// </summary>
public class JsonNumberCast_net80Tests
{
    [Fact]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.ParseValue("1.2".AsSpan());
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("1.2".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.Parse("1.2").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.Equal(1.2d, (double)result.AsNumber, 3);
    }

    [Fact]
    public void Cast_to_long_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.ParseValue("12".AsSpan());
        var result = (long)sut;
        Assert.Equal(12L, result);
    }

    [Fact]
    public void Cast_to_long_for_dotnet_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.Parse("12").AsDotnetBackedValue();
        var result = (long)sut;
        Assert.Equal(12L, result);
    }

    [Fact]
    public void Cast_from_long_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        long sut = 12L;
        var result = (JsonHalf)sut;
        Assert.Equal(JsonHalf.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_double_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.ParseValue("1.2".AsSpan());
        var result = (double)sut;
        Assert.Equal(1.2d, (double)result, 3);
    }

    [Fact]
    public void Cast_to_double_for_dotnet_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.Parse("1.2").AsDotnetBackedValue();
        var result = (double)sut;
        Assert.Equal(1.2d, (double)result, 3);
    }

    [Fact]
    public void Cast_from_double_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        double sut = 1.2;
        var result = (JsonHalf)sut;
        Assert.Equal(JsonHalf.ParseValue("1.2".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_decimal_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.ParseValue("1.2".AsSpan());
        var result = (decimal)sut;
        Assert.Equal(1.2d, (double)result, 3);
    }

    [Fact]
    public void Cast_to_decimal_for_dotnet_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.Parse("1.2").AsDotnetBackedValue();
        var result = (decimal)sut;
        Assert.Equal(1.2d, (double)result, 3);
    }

    [Fact]
    public void Cast_from_decimal_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        decimal sut = 1.2m;
        var result = (JsonHalf)sut;
        Assert.Equal(JsonHalf.ParseValue("1.2".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_int_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.ParseValue("12".AsSpan());
        var result = (int)sut;
        Assert.Equal(12, result);
    }

    [Fact]
    public void Cast_to_int_for_dotnet_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.Parse("12").AsDotnetBackedValue();
        var result = (int)sut;
        Assert.Equal(12, result);
    }

    [Fact]
    public void Cast_from_int_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        int sut = 12;
        var result = (JsonHalf)sut;
        Assert.Equal(JsonHalf.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_float_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.ParseValue("1.2".AsSpan());
        var result = (float)sut;
        Assert.Equal(1.2f, result, 5);
    }

    [Fact]
    public void Cast_to_float_for_dotnet_backed_value_as_a_number_JsonHalf()
    {
        var sut = JsonHalf.Parse("1.2").AsDotnetBackedValue();
        var result = (float)sut;
        Assert.Equal(1.2d, (double)result, 3);
    }

    [Fact]
    public void Cast_from_float_for_json_element_backed_value_as_a_number_JsonHalf()
    {
        float sut = 1.2f;
        var result = (JsonHalf)sut;
        Assert.Equal(JsonHalf.ParseValue("1.2".AsSpan()), result);
    }
}