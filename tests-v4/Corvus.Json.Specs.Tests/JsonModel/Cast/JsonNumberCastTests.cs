// <copyright file="JsonNumberCastTests.cs" company="Endjin Limited">
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
/// Tests for JsonNumberCast.
/// </summary>
public class JsonNumberCastTests
{
    [Fact]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_number_JsonNumber()
    {
        var sut = JsonNumber.ParseValue("1.2".AsSpan());
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("1.2".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_number_JsonDouble()
    {
        var sut = JsonDouble.ParseValue("1.2".AsSpan());
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("1.2".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_number_JsonSingle()
    {
        var sut = JsonSingle.ParseValue("1.2".AsSpan());
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("1.2".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_number_JsonDecimal()
    {
        var sut = JsonDecimal.ParseValue("1.2".AsSpan());
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("1.2".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_number_JsonNumber()
    {
        var sut = JsonNumber.Parse("1.2").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.Equal(1.2d, (double)result.AsNumber, 5);
    }

    [Fact]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_number_JsonDouble()
    {
        var sut = JsonDouble.Parse("1.2").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.Equal(1.2d, (double)result.AsNumber, 5);
    }

    [Fact]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_number_JsonSingle()
    {
        var sut = JsonSingle.Parse("1.2").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.Equal(1.2d, (double)result.AsNumber, 5);
    }

    [Fact]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_number_JsonDecimal()
    {
        var sut = JsonDecimal.Parse("1.2").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.Equal(1.2d, (double)result.AsNumber, 5);
    }

    [Fact]
    public void Cast_to_long_for_json_element_backed_value_as_a_number_JsonNumber()
    {
        var sut = JsonNumber.ParseValue("12".AsSpan());
        var result = (long)sut;
        Assert.Equal(12L, result);
    }

    [Fact]
    public void Cast_to_long_for_json_element_backed_value_as_a_number_JsonDouble()
    {
        var sut = JsonDouble.ParseValue("12".AsSpan());
        var result = (long)sut;
        Assert.Equal(12L, result);
    }

    [Fact]
    public void Cast_to_long_for_json_element_backed_value_as_a_number_JsonSingle()
    {
        var sut = JsonSingle.ParseValue("12".AsSpan());
        var result = (long)sut;
        Assert.Equal(12L, result);
    }

    [Fact]
    public void Cast_to_long_for_json_element_backed_value_as_a_number_JsonDecimal()
    {
        var sut = JsonDecimal.ParseValue("12".AsSpan());
        var result = (long)sut;
        Assert.Equal(12L, result);
    }

    [Fact]
    public void Cast_to_long_for_dotnet_backed_value_as_a_number_JsonNumber()
    {
        var sut = JsonNumber.Parse("12").AsDotnetBackedValue();
        var result = (long)sut;
        Assert.Equal(12L, result);
    }

    [Fact]
    public void Cast_to_long_for_dotnet_backed_value_as_a_number_JsonDouble()
    {
        var sut = JsonDouble.Parse("12").AsDotnetBackedValue();
        var result = (long)sut;
        Assert.Equal(12L, result);
    }

    [Fact]
    public void Cast_to_long_for_dotnet_backed_value_as_a_number_JsonSingle()
    {
        var sut = JsonSingle.Parse("12").AsDotnetBackedValue();
        var result = (long)sut;
        Assert.Equal(12L, result);
    }

    [Fact]
    public void Cast_to_long_for_dotnet_backed_value_as_a_number_JsonDecimal()
    {
        var sut = JsonDecimal.Parse("12").AsDotnetBackedValue();
        var result = (long)sut;
        Assert.Equal(12L, result);
    }

    [Fact]
    public void Cast_from_long_for_json_element_backed_value_as_a_number_JsonNumber()
    {
        long sut = 12L;
        var result = (JsonNumber)sut;
        Assert.Equal(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_long_for_json_element_backed_value_as_a_number_JsonDouble()
    {
        long sut = 12L;
        var result = (JsonDouble)sut;
        Assert.Equal(JsonDouble.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_long_for_json_element_backed_value_as_a_number_JsonSingle()
    {
        long sut = 12L;
        var result = (JsonSingle)sut;
        Assert.Equal(JsonSingle.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_long_for_json_element_backed_value_as_a_number_JsonDecimal()
    {
        long sut = 12L;
        var result = (JsonDecimal)sut;
        Assert.Equal(JsonDecimal.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_double_for_json_element_backed_value_as_a_number_JsonNumber()
    {
        var sut = JsonNumber.ParseValue("1.2".AsSpan());
        var result = (double)sut;
        Assert.Equal(1.2d, result, 5);
    }

    [Fact]
    public void Cast_to_double_for_json_element_backed_value_as_a_number_JsonDouble()
    {
        var sut = JsonDouble.ParseValue("1.2".AsSpan());
        var result = (double)sut;
        Assert.Equal(1.2d, result, 5);
    }

    [Fact]
    public void Cast_to_double_for_json_element_backed_value_as_a_number_JsonSingle()
    {
        var sut = JsonSingle.ParseValue("1.2".AsSpan());
        var result = (double)sut;
        Assert.Equal(1.2d, result, 5);
    }

    [Fact]
    public void Cast_to_double_for_json_element_backed_value_as_a_number_JsonDecimal()
    {
        var sut = JsonDecimal.ParseValue("1.2".AsSpan());
        var result = (double)sut;
        Assert.Equal(1.2d, result, 5);
    }

    [Fact]
    public void Cast_to_double_for_dotnet_backed_value_as_a_number_JsonNumber()
    {
        var sut = JsonNumber.Parse("1.2").AsDotnetBackedValue();
        var result = (double)sut;
        Assert.Equal(1.2d, result, 5);
    }

    [Fact]
    public void Cast_to_double_for_dotnet_backed_value_as_a_number_JsonDouble()
    {
        var sut = JsonDouble.Parse("1.2").AsDotnetBackedValue();
        var result = (double)sut;
        Assert.Equal(1.2d, result, 5);
    }

    [Fact]
    public void Cast_to_double_for_dotnet_backed_value_as_a_number_JsonSingle()
    {
        var sut = JsonSingle.Parse("1.2").AsDotnetBackedValue();
        var result = (double)sut;
        Assert.Equal(1.2d, result, 5);
    }

    [Fact]
    public void Cast_to_double_for_dotnet_backed_value_as_a_number_JsonDecimal()
    {
        var sut = JsonDecimal.Parse("1.2").AsDotnetBackedValue();
        var result = (double)sut;
        Assert.Equal(1.2d, result, 5);
    }

    [Fact]
    public void Cast_from_double_for_json_element_backed_value_as_a_number_JsonNumber()
    {
        double sut = 1.2;
        var result = (JsonNumber)sut;
        Assert.Equal(JsonNumber.ParseValue("1.2".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_double_for_json_element_backed_value_as_a_number_JsonDouble()
    {
        double sut = 1.2;
        var result = (JsonDouble)sut;
        Assert.Equal(JsonDouble.ParseValue("1.2".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_double_for_json_element_backed_value_as_a_number_JsonSingle()
    {
        double sut = 1.2;
        var result = (JsonSingle)sut;
        Assert.Equal(JsonSingle.ParseValue("1.2".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_double_for_json_element_backed_value_as_a_number_JsonDecimal()
    {
        double sut = 1.2;
        var result = (JsonDecimal)sut;
        Assert.Equal(JsonDecimal.ParseValue("1.2".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_decimal_for_json_element_backed_value_as_a_number_JsonNumber()
    {
        var sut = JsonNumber.ParseValue("1.2".AsSpan());
        var result = (decimal)sut;
        Assert.Equal(1.2m, result);
    }

    [Fact]
    public void Cast_to_decimal_for_json_element_backed_value_as_a_number_JsonDecimal()
    {
        var sut = JsonDecimal.ParseValue("1.2".AsSpan());
        var result = (decimal)sut;
        Assert.Equal(1.2m, result);
    }

    [Fact]
    public void Cast_to_decimal_for_json_element_backed_value_as_a_number_JsonSingle()
    {
        var sut = JsonSingle.ParseValue("1.2".AsSpan());
        var result = (decimal)sut;
        Assert.Equal(1.2m, result);
    }

    [Fact]
    public void Cast_to_decimal_for_json_element_backed_value_as_a_number_JsonDecimal_2()
    {
        var sut = JsonDecimal.ParseValue("1.2".AsSpan());
        var result = (decimal)sut;
        Assert.Equal(1.2m, result);
    }

    [Fact]
    public void Cast_to_decimal_for_dotnet_backed_value_as_a_number_JsonNumber()
    {
        var sut = JsonNumber.Parse("1.2").AsDotnetBackedValue();
        var result = (decimal)sut;
        Assert.Equal(1.2m, result);
    }

    [Fact]
    public void Cast_to_decimal_for_dotnet_backed_value_as_a_number_JsonDecimal()
    {
        var sut = JsonDecimal.Parse("1.2").AsDotnetBackedValue();
        var result = (decimal)sut;
        Assert.Equal(1.2m, result);
    }

    [Fact]
    public void Cast_to_decimal_for_dotnet_backed_value_as_a_number_JsonSingle()
    {
        var sut = JsonSingle.Parse("1.2").AsDotnetBackedValue();
        var result = (decimal)sut;
        Assert.Equal(1.2m, result);
    }

    [Fact]
    public void Cast_to_decimal_for_dotnet_backed_value_as_a_number_JsonDecimal_2()
    {
        var sut = JsonDecimal.Parse("1.2").AsDotnetBackedValue();
        var result = (decimal)sut;
        Assert.Equal(1.2m, result);
    }

    [Fact]
    public void Cast_from_decimal_for_json_element_backed_value_as_a_number_JsonNumber()
    {
        decimal sut = 1.2m;
        var result = (JsonNumber)sut;
        Assert.Equal(JsonNumber.ParseValue("1.2".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_decimal_for_json_element_backed_value_as_a_number_JsonDecimal()
    {
        decimal sut = 1.2m;
        var result = (JsonDecimal)sut;
        Assert.Equal(JsonDecimal.ParseValue("1.2".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_decimal_for_json_element_backed_value_as_a_number_JsonSingle()
    {
        decimal sut = 1.2m;
        var result = (JsonSingle)sut;
        Assert.Equal(JsonSingle.ParseValue("1.2".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_decimal_for_json_element_backed_value_as_a_number_JsonDecimal_2()
    {
        decimal sut = 1.2m;
        var result = (JsonDecimal)sut;
        Assert.Equal(JsonDecimal.ParseValue("1.2".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_int_for_json_element_backed_value_as_a_number_JsonNumber()
    {
        var sut = JsonNumber.ParseValue("12".AsSpan());
        var result = (int)sut;
        Assert.Equal(12, result);
    }

    [Fact]
    public void Cast_to_int_for_json_element_backed_value_as_a_number_JsonDouble()
    {
        var sut = JsonDouble.ParseValue("12".AsSpan());
        var result = (int)sut;
        Assert.Equal(12, result);
    }

    [Fact]
    public void Cast_to_int_for_json_element_backed_value_as_a_number_JsonSingle()
    {
        var sut = JsonSingle.ParseValue("12".AsSpan());
        var result = (int)sut;
        Assert.Equal(12, result);
    }

    [Fact]
    public void Cast_to_int_for_json_element_backed_value_as_a_number_JsonDecimal()
    {
        var sut = JsonDecimal.ParseValue("12".AsSpan());
        var result = (int)sut;
        Assert.Equal(12, result);
    }

    [Fact]
    public void Cast_to_int_for_dotnet_backed_value_as_a_number_JsonNumber()
    {
        var sut = JsonNumber.Parse("12").AsDotnetBackedValue();
        var result = (int)sut;
        Assert.Equal(12, result);
    }

    [Fact]
    public void Cast_to_int_for_dotnet_backed_value_as_a_number_JsonDouble()
    {
        var sut = JsonDouble.Parse("12").AsDotnetBackedValue();
        var result = (int)sut;
        Assert.Equal(12, result);
    }

    [Fact]
    public void Cast_to_int_for_dotnet_backed_value_as_a_number_JsonSingle()
    {
        var sut = JsonSingle.Parse("12").AsDotnetBackedValue();
        var result = (int)sut;
        Assert.Equal(12, result);
    }

    [Fact]
    public void Cast_to_int_for_dotnet_backed_value_as_a_number_JsonDecimal()
    {
        var sut = JsonDecimal.Parse("12").AsDotnetBackedValue();
        var result = (int)sut;
        Assert.Equal(12, result);
    }

    [Fact]
    public void Cast_from_int_for_json_element_backed_value_as_a_number_JsonNumber()
    {
        int sut = 12;
        var result = (JsonNumber)sut;
        Assert.Equal(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_int_for_json_element_backed_value_as_a_number_JsonDouble()
    {
        int sut = 12;
        var result = (JsonDouble)sut;
        Assert.Equal(JsonDouble.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_int_for_json_element_backed_value_as_a_number_JsonSingle()
    {
        int sut = 12;
        var result = (JsonSingle)sut;
        Assert.Equal(JsonSingle.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_int_for_json_element_backed_value_as_a_number_JsonDecimal()
    {
        int sut = 12;
        var result = (JsonDecimal)sut;
        Assert.Equal(JsonDecimal.ParseValue("12".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_float_for_json_element_backed_value_as_a_number_JsonNumber()
    {
        var sut = JsonNumber.ParseValue("1.2".AsSpan());
        var result = (float)sut;
        Assert.Equal(1.2f, result, 5);
    }

    [Fact]
    public void Cast_to_float_for_json_element_backed_value_as_a_number_JsonDouble()
    {
        var sut = JsonDouble.ParseValue("1.2".AsSpan());
        var result = (float)sut;
        Assert.Equal(1.2f, result, 5);
    }

    [Fact]
    public void Cast_to_float_for_json_element_backed_value_as_a_number_JsonSingle()
    {
        var sut = JsonSingle.ParseValue("1.2".AsSpan());
        var result = (float)sut;
        Assert.Equal(1.2f, result, 5);
    }

    [Fact]
    public void Cast_to_float_for_json_element_backed_value_as_a_number_JsonDecimal()
    {
        var sut = JsonDecimal.ParseValue("1.2".AsSpan());
        var result = (float)sut;
        Assert.Equal(1.2f, result, 5);
    }

    [Fact]
    public void Cast_to_float_for_dotnet_backed_value_as_a_number_JsonNumber()
    {
        var sut = JsonNumber.Parse("1.2").AsDotnetBackedValue();
        var result = (float)sut;
        Assert.Equal(1.2f, result, 5);
    }

    [Fact]
    public void Cast_to_float_for_dotnet_backed_value_as_a_number_JsonDouble()
    {
        var sut = JsonDouble.Parse("1.2").AsDotnetBackedValue();
        var result = (float)sut;
        Assert.Equal(1.2f, result, 5);
    }

    [Fact]
    public void Cast_to_float_for_dotnet_backed_value_as_a_number_JsonSingle()
    {
        var sut = JsonSingle.Parse("1.2").AsDotnetBackedValue();
        var result = (float)sut;
        Assert.Equal(1.2f, result, 5);
    }

    [Fact]
    public void Cast_to_float_for_dotnet_backed_value_as_a_number_JsonDecimal()
    {
        var sut = JsonDecimal.Parse("1.2").AsDotnetBackedValue();
        var result = (float)sut;
        Assert.Equal(1.2f, result, 5);
    }

    [Fact]
    public void Cast_from_float_for_json_element_backed_value_as_a_number_JsonNumber()
    {
        float sut = 1.2f;
        var result = (JsonNumber)sut;
        Assert.Equal(JsonNumber.ParseValue("1.2".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_float_for_json_element_backed_value_as_a_number_JsonDouble()
    {
        float sut = 1.2f;
        var result = (JsonDouble)sut;
        Assert.Equal(JsonDouble.ParseValue("1.2".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_float_for_json_element_backed_value_as_a_number_JsonSingle()
    {
        float sut = 1.2f;
        var result = (JsonSingle)sut;
        Assert.Equal(JsonSingle.ParseValue("1.2".AsSpan()), result);
    }

    [Fact]
    public void Cast_from_float_for_json_element_backed_value_as_a_number_JsonDecimal()
    {
        float sut = 1.2f;
        var result = (JsonDecimal)sut;
        Assert.Equal(JsonDecimal.ParseValue("1.2".AsSpan()), result);
    }
}