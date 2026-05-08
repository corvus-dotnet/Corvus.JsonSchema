// <copyright file="JsonIntegerCastTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Collections.Immutable;
using System.Net;
using System.Text.RegularExpressions;
using Corvus.Json;
using NodaTime;
using NodaTime.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.JsonModel.Cast;

/// <summary>
/// Tests for JsonIntegerCast.
/// </summary>
[TestClass]
public class JsonIntegerCastTests
{
    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_integer_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("12".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_integer_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("12".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_integer_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("12".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_integer_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("12".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_integer_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("12".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_integer_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("12".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_integer_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("12".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_integer_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("12".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_a_integer_JsonByte()
    {
        var sut = JsonByte.ParseValue("12".AsSpan());
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_integer_JsonInteger()
    {
        var sut = JsonInteger.Parse("12").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_integer_JsonInt64()
    {
        var sut = JsonInt64.Parse("12").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_integer_JsonInt32()
    {
        var sut = JsonInt32.Parse("12").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_integer_JsonInt16()
    {
        var sut = JsonInt16.Parse("12").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_integer_JsonSByte()
    {
        var sut = JsonSByte.Parse("12").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_integer_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("12").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_integer_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("12").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_integer_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("12").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_a_integer_JsonByte()
    {
        var sut = JsonByte.Parse("12").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.AreEqual(JsonAny.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonNumber_for_json_element_backed_value_as_a_integer_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("12".AsSpan());
        var result = (JsonNumber)sut;
        Assert.AreEqual(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonNumber_for_json_element_backed_value_as_a_integer_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("12".AsSpan());
        var result = (JsonNumber)sut;
        Assert.AreEqual(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonNumber_for_json_element_backed_value_as_a_integer_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("12".AsSpan());
        var result = (JsonNumber)sut;
        Assert.AreEqual(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonNumber_for_json_element_backed_value_as_a_integer_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("12".AsSpan());
        var result = (JsonNumber)sut;
        Assert.AreEqual(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonNumber_for_json_element_backed_value_as_a_integer_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("12".AsSpan());
        var result = (JsonNumber)sut;
        Assert.AreEqual(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonNumber_for_json_element_backed_value_as_a_integer_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("12".AsSpan());
        var result = (JsonNumber)sut;
        Assert.AreEqual(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonNumber_for_json_element_backed_value_as_a_integer_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("12".AsSpan());
        var result = (JsonNumber)sut;
        Assert.AreEqual(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonNumber_for_json_element_backed_value_as_a_integer_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("12".AsSpan());
        var result = (JsonNumber)sut;
        Assert.AreEqual(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonNumber_for_json_element_backed_value_as_a_integer_JsonByte()
    {
        var sut = JsonByte.ParseValue("12".AsSpan());
        var result = (JsonNumber)sut;
        Assert.AreEqual(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonNumber_for_dotnet_backed_value_as_a_integer_JsonInteger()
    {
        var sut = JsonInteger.Parse("12").AsDotnetBackedValue();
        var result = (JsonNumber)sut;
        Assert.AreEqual(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonNumber_for_dotnet_backed_value_as_a_integer_JsonInt64()
    {
        var sut = JsonInt64.Parse("12").AsDotnetBackedValue();
        var result = (JsonNumber)sut;
        Assert.AreEqual(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonNumber_for_dotnet_backed_value_as_a_integer_JsonInt32()
    {
        var sut = JsonInt32.Parse("12").AsDotnetBackedValue();
        var result = (JsonNumber)sut;
        Assert.AreEqual(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonNumber_for_dotnet_backed_value_as_a_integer_JsonInt16()
    {
        var sut = JsonInt16.Parse("12").AsDotnetBackedValue();
        var result = (JsonNumber)sut;
        Assert.AreEqual(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonNumber_for_dotnet_backed_value_as_a_integer_JsonSByte()
    {
        var sut = JsonSByte.Parse("12").AsDotnetBackedValue();
        var result = (JsonNumber)sut;
        Assert.AreEqual(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonNumber_for_dotnet_backed_value_as_a_integer_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("12").AsDotnetBackedValue();
        var result = (JsonNumber)sut;
        Assert.AreEqual(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonNumber_for_dotnet_backed_value_as_a_integer_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("12").AsDotnetBackedValue();
        var result = (JsonNumber)sut;
        Assert.AreEqual(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonNumber_for_dotnet_backed_value_as_a_integer_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("12").AsDotnetBackedValue();
        var result = (JsonNumber)sut;
        Assert.AreEqual(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_JsonNumber_for_dotnet_backed_value_as_a_integer_JsonByte()
    {
        var sut = JsonByte.Parse("12").AsDotnetBackedValue();
        var result = (JsonNumber)sut;
        Assert.AreEqual(JsonNumber.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_long_for_json_element_backed_value_as_a_integer_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("12".AsSpan());
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_to_long_for_json_element_backed_value_as_a_integer_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("12".AsSpan());
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_to_long_for_json_element_backed_value_as_a_integer_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("12".AsSpan());
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_to_long_for_json_element_backed_value_as_a_integer_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("12".AsSpan());
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_to_long_for_json_element_backed_value_as_a_integer_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("12".AsSpan());
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_to_long_for_json_element_backed_value_as_a_integer_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("12".AsSpan());
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_to_long_for_json_element_backed_value_as_a_integer_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("12".AsSpan());
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_to_long_for_json_element_backed_value_as_a_integer_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("12".AsSpan());
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_to_long_for_json_element_backed_value_as_a_integer_JsonByte()
    {
        var sut = JsonByte.ParseValue("12".AsSpan());
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_to_long_for_dotnet_backed_value_as_a_integer_JsonInteger()
    {
        var sut = JsonInteger.Parse("12").AsDotnetBackedValue();
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_to_long_for_dotnet_backed_value_as_a_integer_JsonInt64()
    {
        var sut = JsonInt64.Parse("12").AsDotnetBackedValue();
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_to_long_for_dotnet_backed_value_as_a_integer_JsonInt32()
    {
        var sut = JsonInt32.Parse("12").AsDotnetBackedValue();
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_to_long_for_dotnet_backed_value_as_a_integer_JsonInt16()
    {
        var sut = JsonInt16.Parse("12").AsDotnetBackedValue();
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_to_long_for_dotnet_backed_value_as_a_integer_JsonSByte()
    {
        var sut = JsonSByte.Parse("12").AsDotnetBackedValue();
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_to_long_for_dotnet_backed_value_as_a_integer_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("12").AsDotnetBackedValue();
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_to_long_for_dotnet_backed_value_as_a_integer_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("12").AsDotnetBackedValue();
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_to_long_for_dotnet_backed_value_as_a_integer_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("12").AsDotnetBackedValue();
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_to_long_for_dotnet_backed_value_as_a_integer_JsonByte()
    {
        var sut = JsonByte.Parse("12").AsDotnetBackedValue();
        var result = (long)sut;
        Assert.AreEqual(12L, result);
    }

    [TestMethod]
    public void Cast_from_long_for_json_element_backed_value_as_a_integer_JsonInteger()
    {
        long sut = 12L;
        var result = (JsonInteger)sut;
        Assert.AreEqual(JsonInteger.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_long_for_json_element_backed_value_as_a_integer_JsonInt64()
    {
        long sut = 12L;
        var result = (JsonInt64)sut;
        Assert.AreEqual(JsonInt64.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_long_for_json_element_backed_value_as_a_integer_JsonInt32()
    {
        long sut = 12L;
        var result = (JsonInt32)sut;
        Assert.AreEqual(JsonInt32.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_long_for_json_element_backed_value_as_a_integer_JsonInt16()
    {
        long sut = 12L;
        var result = (JsonInt16)sut;
        Assert.AreEqual(JsonInt16.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_long_for_json_element_backed_value_as_a_integer_JsonSByte()
    {
        long sut = 12L;
        var result = (JsonSByte)sut;
        Assert.AreEqual(JsonSByte.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_long_for_json_element_backed_value_as_a_integer_JsonUInt64()
    {
        long sut = 12L;
        var result = (JsonUInt64)sut;
        Assert.AreEqual(JsonUInt64.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_long_for_json_element_backed_value_as_a_integer_JsonUInt32()
    {
        long sut = 12L;
        var result = (JsonUInt32)sut;
        Assert.AreEqual(JsonUInt32.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_long_for_json_element_backed_value_as_a_integer_JsonUInt16()
    {
        long sut = 12L;
        var result = (JsonUInt16)sut;
        Assert.AreEqual(JsonUInt16.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_long_for_json_element_backed_value_as_a_integer_JsonByte()
    {
        long sut = 12L;
        var result = (JsonByte)sut;
        Assert.AreEqual(JsonByte.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_double_for_json_element_backed_value_as_a_integer_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("12".AsSpan());
        var result = (double)sut;
        Assert.AreEqual(12.0d, result, 5);
    }

    [TestMethod]
    public void Cast_to_double_for_json_element_backed_value_as_a_integer_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("12".AsSpan());
        var result = (double)sut;
        Assert.AreEqual(12.0d, result, 5);
    }

    [TestMethod]
    public void Cast_to_double_for_json_element_backed_value_as_a_integer_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("12".AsSpan());
        var result = (double)sut;
        Assert.AreEqual(12.0d, result, 5);
    }

    [TestMethod]
    public void Cast_to_double_for_json_element_backed_value_as_a_integer_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("12".AsSpan());
        var result = (double)sut;
        Assert.AreEqual(12.0d, result, 5);
    }

    [TestMethod]
    public void Cast_to_double_for_json_element_backed_value_as_a_integer_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("12".AsSpan());
        var result = (double)sut;
        Assert.AreEqual(12.0d, result, 5);
    }

    [TestMethod]
    public void Cast_to_double_for_json_element_backed_value_as_a_integer_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("12".AsSpan());
        var result = (double)sut;
        Assert.AreEqual(12.0d, result, 5);
    }

    [TestMethod]
    public void Cast_to_double_for_json_element_backed_value_as_a_integer_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("12".AsSpan());
        var result = (double)sut;
        Assert.AreEqual(12.0d, result, 5);
    }

    [TestMethod]
    public void Cast_to_double_for_json_element_backed_value_as_a_integer_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("12".AsSpan());
        var result = (double)sut;
        Assert.AreEqual(12.0d, result, 5);
    }

    [TestMethod]
    public void Cast_to_double_for_json_element_backed_value_as_a_integer_JsonByte()
    {
        var sut = JsonByte.ParseValue("12".AsSpan());
        var result = (double)sut;
        Assert.AreEqual(12.0d, result, 5);
    }

    [TestMethod]
    public void Cast_to_double_for_dotnet_backed_value_as_a_integer_JsonInteger()
    {
        var sut = JsonInteger.Parse("12").AsDotnetBackedValue();
        var result = (double)sut;
        Assert.AreEqual(12.0d, result, 5);
    }

    [TestMethod]
    public void Cast_to_double_for_dotnet_backed_value_as_a_integer_JsonInt64()
    {
        var sut = JsonInt64.Parse("12").AsDotnetBackedValue();
        var result = (double)sut;
        Assert.AreEqual(12.0d, result, 5);
    }

    [TestMethod]
    public void Cast_to_double_for_dotnet_backed_value_as_a_integer_JsonInt32()
    {
        var sut = JsonInt32.Parse("12").AsDotnetBackedValue();
        var result = (double)sut;
        Assert.AreEqual(12.0d, result, 5);
    }

    [TestMethod]
    public void Cast_to_double_for_dotnet_backed_value_as_a_integer_JsonInt16()
    {
        var sut = JsonInt16.Parse("12").AsDotnetBackedValue();
        var result = (double)sut;
        Assert.AreEqual(12.0d, result, 5);
    }

    [TestMethod]
    public void Cast_to_double_for_dotnet_backed_value_as_a_integer_JsonSByte()
    {
        var sut = JsonSByte.Parse("12").AsDotnetBackedValue();
        var result = (double)sut;
        Assert.AreEqual(12.0d, result, 5);
    }

    [TestMethod]
    public void Cast_to_double_for_dotnet_backed_value_as_a_integer_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("12").AsDotnetBackedValue();
        var result = (double)sut;
        Assert.AreEqual(12.0d, result, 5);
    }

    [TestMethod]
    public void Cast_to_double_for_dotnet_backed_value_as_a_integer_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("12").AsDotnetBackedValue();
        var result = (double)sut;
        Assert.AreEqual(12.0d, result, 5);
    }

    [TestMethod]
    public void Cast_to_double_for_dotnet_backed_value_as_a_integer_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("12").AsDotnetBackedValue();
        var result = (double)sut;
        Assert.AreEqual(12.0d, result, 5);
    }

    [TestMethod]
    public void Cast_to_double_for_dotnet_backed_value_as_a_integer_JsonByte()
    {
        var sut = JsonByte.Parse("12").AsDotnetBackedValue();
        var result = (double)sut;
        Assert.AreEqual(12.0d, result, 5);
    }

    [TestMethod]
    public void Cast_from_double_for_json_element_backed_value_as_a_integer_JsonInteger()
    {
        double sut = 12.0;
        var result = (JsonInteger)sut;
        Assert.AreEqual(JsonInteger.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_double_for_json_element_backed_value_as_a_integer_JsonInt64()
    {
        double sut = 12.0;
        var result = (JsonInt64)sut;
        Assert.AreEqual(JsonInt64.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_double_for_json_element_backed_value_as_a_integer_JsonInt32()
    {
        double sut = 12.0;
        var result = (JsonInt32)sut;
        Assert.AreEqual(JsonInt32.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_double_for_json_element_backed_value_as_a_integer_JsonInt16()
    {
        double sut = 12.0;
        var result = (JsonInt16)sut;
        Assert.AreEqual(JsonInt16.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_double_for_json_element_backed_value_as_a_integer_JsonSByte()
    {
        double sut = 12.0;
        var result = (JsonSByte)sut;
        Assert.AreEqual(JsonSByte.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_double_for_json_element_backed_value_as_a_integer_JsonUInt64()
    {
        double sut = 12.0;
        var result = (JsonUInt64)sut;
        Assert.AreEqual(JsonUInt64.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_double_for_json_element_backed_value_as_a_integer_JsonUInt32()
    {
        double sut = 12.0;
        var result = (JsonUInt32)sut;
        Assert.AreEqual(JsonUInt32.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_double_for_json_element_backed_value_as_a_integer_JsonUInt16()
    {
        double sut = 12.0;
        var result = (JsonUInt16)sut;
        Assert.AreEqual(JsonUInt16.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_double_for_json_element_backed_value_as_a_integer_JsonByte()
    {
        double sut = 12.0;
        var result = (JsonByte)sut;
        Assert.AreEqual(JsonByte.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_int_for_json_element_backed_value_as_a_integer_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("12".AsSpan());
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_to_int_for_json_element_backed_value_as_a_integer_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("12".AsSpan());
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_to_int_for_json_element_backed_value_as_a_integer_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("12".AsSpan());
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_to_int_for_json_element_backed_value_as_a_integer_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("12".AsSpan());
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_to_int_for_json_element_backed_value_as_a_integer_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("12".AsSpan());
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_to_int_for_json_element_backed_value_as_a_integer_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("12".AsSpan());
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_to_int_for_json_element_backed_value_as_a_integer_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("12".AsSpan());
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_to_int_for_json_element_backed_value_as_a_integer_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("12".AsSpan());
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_to_int_for_json_element_backed_value_as_a_integer_JsonByte()
    {
        var sut = JsonByte.ParseValue("12".AsSpan());
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_to_int_for_dotnet_backed_value_as_a_integer_JsonInteger()
    {
        var sut = JsonInteger.Parse("12").AsDotnetBackedValue();
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_to_int_for_dotnet_backed_value_as_a_integer_JsonInt64()
    {
        var sut = JsonInt64.Parse("12").AsDotnetBackedValue();
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_to_int_for_dotnet_backed_value_as_a_integer_JsonInt32()
    {
        var sut = JsonInt32.Parse("12").AsDotnetBackedValue();
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_to_int_for_dotnet_backed_value_as_a_integer_JsonInt16()
    {
        var sut = JsonInt16.Parse("12").AsDotnetBackedValue();
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_to_int_for_dotnet_backed_value_as_a_integer_JsonSByte()
    {
        var sut = JsonSByte.Parse("12").AsDotnetBackedValue();
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_to_int_for_dotnet_backed_value_as_a_integer_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("12").AsDotnetBackedValue();
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_to_int_for_dotnet_backed_value_as_a_integer_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("12").AsDotnetBackedValue();
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_to_int_for_dotnet_backed_value_as_a_integer_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("12").AsDotnetBackedValue();
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_to_int_for_dotnet_backed_value_as_a_integer_JsonByte()
    {
        var sut = JsonByte.Parse("12").AsDotnetBackedValue();
        var result = (int)sut;
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public void Cast_from_int_for_json_element_backed_value_as_a_integer_JsonInteger()
    {
        int sut = 12;
        var result = (JsonInteger)sut;
        Assert.AreEqual(JsonInteger.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_int_for_json_element_backed_value_as_a_integer_JsonInt64()
    {
        int sut = 12;
        var result = (JsonInt64)sut;
        Assert.AreEqual(JsonInt64.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_int_for_json_element_backed_value_as_a_integer_JsonInt32()
    {
        int sut = 12;
        var result = (JsonInt32)sut;
        Assert.AreEqual(JsonInt32.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_int_for_json_element_backed_value_as_a_integer_JsonInt16()
    {
        int sut = 12;
        var result = (JsonInt16)sut;
        Assert.AreEqual(JsonInt16.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_int_for_json_element_backed_value_as_a_integer_JsonSByte()
    {
        int sut = 12;
        var result = (JsonSByte)sut;
        Assert.AreEqual(JsonSByte.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_int_for_json_element_backed_value_as_a_integer_JsonUInt64()
    {
        int sut = 12;
        var result = (JsonUInt64)sut;
        Assert.AreEqual(JsonUInt64.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_int_for_json_element_backed_value_as_a_integer_JsonUInt32()
    {
        int sut = 12;
        var result = (JsonUInt32)sut;
        Assert.AreEqual(JsonUInt32.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_int_for_json_element_backed_value_as_a_integer_JsonUInt16()
    {
        int sut = 12;
        var result = (JsonUInt16)sut;
        Assert.AreEqual(JsonUInt16.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_int_for_json_element_backed_value_as_a_integer_JsonByte()
    {
        int sut = 12;
        var result = (JsonByte)sut;
        Assert.AreEqual(JsonByte.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_float_for_json_element_backed_value_as_a_integer_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("12".AsSpan());
        var result = (float)sut;
        Assert.AreEqual(12.0f, result, 5);
    }

    [TestMethod]
    public void Cast_to_float_for_json_element_backed_value_as_a_integer_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("12".AsSpan());
        var result = (float)sut;
        Assert.AreEqual(12.0f, result, 5);
    }

    [TestMethod]
    public void Cast_to_float_for_json_element_backed_value_as_a_integer_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("12".AsSpan());
        var result = (float)sut;
        Assert.AreEqual(12.0f, result, 5);
    }

    [TestMethod]
    public void Cast_to_float_for_json_element_backed_value_as_a_integer_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("12".AsSpan());
        var result = (float)sut;
        Assert.AreEqual(12.0f, result, 5);
    }

    [TestMethod]
    public void Cast_to_float_for_json_element_backed_value_as_a_integer_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("12".AsSpan());
        var result = (float)sut;
        Assert.AreEqual(12.0f, result, 5);
    }

    [TestMethod]
    public void Cast_to_float_for_json_element_backed_value_as_a_integer_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("12".AsSpan());
        var result = (float)sut;
        Assert.AreEqual(12.0f, result, 5);
    }

    [TestMethod]
    public void Cast_to_float_for_json_element_backed_value_as_a_integer_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("12".AsSpan());
        var result = (float)sut;
        Assert.AreEqual(12.0f, result, 5);
    }

    [TestMethod]
    public void Cast_to_float_for_json_element_backed_value_as_a_integer_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("12".AsSpan());
        var result = (float)sut;
        Assert.AreEqual(12.0f, result, 5);
    }

    [TestMethod]
    public void Cast_to_float_for_json_element_backed_value_as_a_integer_JsonByte()
    {
        var sut = JsonByte.ParseValue("12".AsSpan());
        var result = (float)sut;
        Assert.AreEqual(12.0f, result, 5);
    }

    [TestMethod]
    public void Cast_to_float_for_dotnet_backed_value_as_a_integer_JsonInteger()
    {
        var sut = JsonInteger.Parse("12").AsDotnetBackedValue();
        var result = (float)sut;
        Assert.AreEqual(12.0f, result, 5);
    }

    [TestMethod]
    public void Cast_to_float_for_dotnet_backed_value_as_a_integer_JsonInt64()
    {
        var sut = JsonInt64.Parse("12").AsDotnetBackedValue();
        var result = (float)sut;
        Assert.AreEqual(12.0f, result, 5);
    }

    [TestMethod]
    public void Cast_to_float_for_dotnet_backed_value_as_a_integer_JsonInt32()
    {
        var sut = JsonInt32.Parse("12").AsDotnetBackedValue();
        var result = (float)sut;
        Assert.AreEqual(12.0f, result, 5);
    }

    [TestMethod]
    public void Cast_to_float_for_dotnet_backed_value_as_a_integer_JsonInt16()
    {
        var sut = JsonInt16.Parse("12").AsDotnetBackedValue();
        var result = (float)sut;
        Assert.AreEqual(12.0f, result, 5);
    }

    [TestMethod]
    public void Cast_to_float_for_dotnet_backed_value_as_a_integer_JsonSByte()
    {
        var sut = JsonSByte.Parse("12").AsDotnetBackedValue();
        var result = (float)sut;
        Assert.AreEqual(12.0f, result, 5);
    }

    [TestMethod]
    public void Cast_to_float_for_dotnet_backed_value_as_a_integer_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("12").AsDotnetBackedValue();
        var result = (float)sut;
        Assert.AreEqual(12.0f, result, 5);
    }

    [TestMethod]
    public void Cast_to_float_for_dotnet_backed_value_as_a_integer_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("12").AsDotnetBackedValue();
        var result = (float)sut;
        Assert.AreEqual(12.0f, result, 5);
    }

    [TestMethod]
    public void Cast_to_float_for_dotnet_backed_value_as_a_integer_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("12").AsDotnetBackedValue();
        var result = (float)sut;
        Assert.AreEqual(12.0f, result, 5);
    }

    [TestMethod]
    public void Cast_to_float_for_dotnet_backed_value_as_a_integer_JsonByte()
    {
        var sut = JsonByte.Parse("12").AsDotnetBackedValue();
        var result = (float)sut;
        Assert.AreEqual(12.0f, result, 5);
    }

    [TestMethod]
    public void Cast_from_float_for_json_element_backed_value_as_a_integer_JsonInteger()
    {
        float sut = 12f;
        var result = (JsonInteger)sut;
        Assert.AreEqual(JsonInteger.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_float_for_json_element_backed_value_as_a_integer_JsonInt64()
    {
        float sut = 12f;
        var result = (JsonInt64)sut;
        Assert.AreEqual(JsonInt64.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_float_for_json_element_backed_value_as_a_integer_JsonInt32()
    {
        float sut = 12f;
        var result = (JsonInt32)sut;
        Assert.AreEqual(JsonInt32.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_float_for_json_element_backed_value_as_a_integer_JsonInt16()
    {
        float sut = 12f;
        var result = (JsonInt16)sut;
        Assert.AreEqual(JsonInt16.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_float_for_json_element_backed_value_as_a_integer_JsonSByte()
    {
        float sut = 12f;
        var result = (JsonSByte)sut;
        Assert.AreEqual(JsonSByte.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_float_for_json_element_backed_value_as_a_integer_JsonUInt64()
    {
        float sut = 12f;
        var result = (JsonUInt64)sut;
        Assert.AreEqual(JsonUInt64.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_float_for_json_element_backed_value_as_a_integer_JsonUInt32()
    {
        float sut = 12f;
        var result = (JsonUInt32)sut;
        Assert.AreEqual(JsonUInt32.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_float_for_json_element_backed_value_as_a_integer_JsonUInt16()
    {
        float sut = 12f;
        var result = (JsonUInt16)sut;
        Assert.AreEqual(JsonUInt16.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_from_float_for_json_element_backed_value_as_a_integer_JsonByte()
    {
        float sut = 12f;
        var result = (JsonByte)sut;
        Assert.AreEqual(JsonByte.ParseValue("12".AsSpan()), result);
    }

    [TestMethod]
    public void Cast_to_short_for_json_element_backed_value_as_a_shorteger_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("12".AsSpan());
        var result = (short)sut;
        Assert.AreEqual((short)12, result);
    }

    [TestMethod]
    public void Cast_to_short_for_json_element_backed_value_as_a_shorteger_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("12".AsSpan());
        var result = (short)sut;
        Assert.AreEqual((short)12, result);
    }

    [TestMethod]
    public void Cast_to_short_for_json_element_backed_value_as_a_shorteger_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("12".AsSpan());
        var result = (short)sut;
        Assert.AreEqual((short)12, result);
    }

    [TestMethod]
    public void Cast_to_short_for_json_element_backed_value_as_a_shorteger_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("12".AsSpan());
        var result = (short)sut;
        Assert.AreEqual((short)12, result);
    }

    [TestMethod]
    public void Cast_to_short_for_json_element_backed_value_as_a_shorteger_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("12".AsSpan());
        var result = (short)sut;
        Assert.AreEqual((short)12, result);
    }

    [TestMethod]
    public void Cast_to_short_for_json_element_backed_value_as_a_shorteger_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("12".AsSpan());
        var result = (short)sut;
        Assert.AreEqual((short)12, result);
    }

    [TestMethod]
    public void Cast_to_short_for_json_element_backed_value_as_a_shorteger_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("12".AsSpan());
        var result = (short)sut;
        Assert.AreEqual((short)12, result);
    }

    [TestMethod]
    public void Cast_to_short_for_json_element_backed_value_as_a_shorteger_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("12".AsSpan());
        var result = (short)sut;
        Assert.AreEqual((short)12, result);
    }

    [TestMethod]
    public void Cast_to_short_for_json_element_backed_value_as_a_shorteger_JsonByte()
    {
        var sut = JsonByte.ParseValue("12".AsSpan());
        var result = (short)sut;
        Assert.AreEqual((short)12, result);
    }

    [TestMethod]
    public void Cast_to_short_for_dotnet_backed_value_as_a_shorteger_JsonInteger()
    {
        var sut = JsonInteger.Parse("12").AsDotnetBackedValue();
        var result = (short)sut;
        Assert.AreEqual((short)12, result);
    }

    [TestMethod]
    public void Cast_to_short_for_dotnet_backed_value_as_a_shorteger_JsonInt64()
    {
        var sut = JsonInt64.Parse("12").AsDotnetBackedValue();
        var result = (short)sut;
        Assert.AreEqual((short)12, result);
    }

    [TestMethod]
    public void Cast_to_short_for_dotnet_backed_value_as_a_shorteger_JsonInt32()
    {
        var sut = JsonInt32.Parse("12").AsDotnetBackedValue();
        var result = (short)sut;
        Assert.AreEqual((short)12, result);
    }

    [TestMethod]
    public void Cast_to_short_for_dotnet_backed_value_as_a_shorteger_JsonInt16()
    {
        var sut = JsonInt16.Parse("12").AsDotnetBackedValue();
        var result = (short)sut;
        Assert.AreEqual((short)12, result);
    }

    [TestMethod]
    public void Cast_to_short_for_dotnet_backed_value_as_a_shorteger_JsonSByte()
    {
        var sut = JsonSByte.Parse("12").AsDotnetBackedValue();
        var result = (short)sut;
        Assert.AreEqual((short)12, result);
    }

    [TestMethod]
    public void Cast_to_short_for_dotnet_backed_value_as_a_shorteger_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("12").AsDotnetBackedValue();
        var result = (short)sut;
        Assert.AreEqual((short)12, result);
    }

    [TestMethod]
    public void Cast_to_short_for_dotnet_backed_value_as_a_shorteger_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("12").AsDotnetBackedValue();
        var result = (short)sut;
        Assert.AreEqual((short)12, result);
    }

    [TestMethod]
    public void Cast_to_short_for_dotnet_backed_value_as_a_shorteger_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("12").AsDotnetBackedValue();
        var result = (short)sut;
        Assert.AreEqual((short)12, result);
    }

    [TestMethod]
    public void Cast_to_short_for_dotnet_backed_value_as_a_shorteger_JsonByte()
    {
        var sut = JsonByte.Parse("12").AsDotnetBackedValue();
        var result = (short)sut;
        Assert.AreEqual((short)12, result);
    }

    [TestMethod]
    public void Cast_to_ushort_for_json_element_backed_value_as_a_ushorteger_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("12".AsSpan());
        var result = (ushort)sut;
        Assert.AreEqual((ushort)12, result);
    }

    [TestMethod]
    public void Cast_to_ushort_for_json_element_backed_value_as_a_ushorteger_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("12".AsSpan());
        var result = (ushort)sut;
        Assert.AreEqual((ushort)12, result);
    }

    [TestMethod]
    public void Cast_to_ushort_for_json_element_backed_value_as_a_ushorteger_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("12".AsSpan());
        var result = (ushort)sut;
        Assert.AreEqual((ushort)12, result);
    }

    [TestMethod]
    public void Cast_to_ushort_for_json_element_backed_value_as_a_ushorteger_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("12".AsSpan());
        var result = (ushort)sut;
        Assert.AreEqual((ushort)12, result);
    }

    [TestMethod]
    public void Cast_to_ushort_for_json_element_backed_value_as_a_ushorteger_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("12".AsSpan());
        var result = (ushort)sut;
        Assert.AreEqual((ushort)12, result);
    }

    [TestMethod]
    public void Cast_to_ushort_for_json_element_backed_value_as_a_ushorteger_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("12".AsSpan());
        var result = (ushort)sut;
        Assert.AreEqual((ushort)12, result);
    }

    [TestMethod]
    public void Cast_to_ushort_for_json_element_backed_value_as_a_ushorteger_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("12".AsSpan());
        var result = (ushort)sut;
        Assert.AreEqual((ushort)12, result);
    }

    [TestMethod]
    public void Cast_to_ushort_for_json_element_backed_value_as_a_ushorteger_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("12".AsSpan());
        var result = (ushort)sut;
        Assert.AreEqual((ushort)12, result);
    }

    [TestMethod]
    public void Cast_to_ushort_for_json_element_backed_value_as_a_ushorteger_JsonByte()
    {
        var sut = JsonByte.ParseValue("12".AsSpan());
        var result = (ushort)sut;
        Assert.AreEqual((ushort)12, result);
    }

    [TestMethod]
    public void Cast_to_ushort_for_dotnet_backed_value_as_a_ushorteger_JsonInteger()
    {
        var sut = JsonInteger.Parse("12").AsDotnetBackedValue();
        var result = (ushort)sut;
        Assert.AreEqual((ushort)12, result);
    }

    [TestMethod]
    public void Cast_to_ushort_for_dotnet_backed_value_as_a_ushorteger_JsonInt64()
    {
        var sut = JsonInt64.Parse("12").AsDotnetBackedValue();
        var result = (ushort)sut;
        Assert.AreEqual((ushort)12, result);
    }

    [TestMethod]
    public void Cast_to_ushort_for_dotnet_backed_value_as_a_ushorteger_JsonInt32()
    {
        var sut = JsonInt32.Parse("12").AsDotnetBackedValue();
        var result = (ushort)sut;
        Assert.AreEqual((ushort)12, result);
    }

    [TestMethod]
    public void Cast_to_ushort_for_dotnet_backed_value_as_a_ushorteger_JsonInt16()
    {
        var sut = JsonInt16.Parse("12").AsDotnetBackedValue();
        var result = (ushort)sut;
        Assert.AreEqual((ushort)12, result);
    }

    [TestMethod]
    public void Cast_to_ushort_for_dotnet_backed_value_as_a_ushorteger_JsonSByte()
    {
        var sut = JsonSByte.Parse("12").AsDotnetBackedValue();
        var result = (ushort)sut;
        Assert.AreEqual((ushort)12, result);
    }

    [TestMethod]
    public void Cast_to_ushort_for_dotnet_backed_value_as_a_ushorteger_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("12").AsDotnetBackedValue();
        var result = (ushort)sut;
        Assert.AreEqual((ushort)12, result);
    }

    [TestMethod]
    public void Cast_to_ushort_for_dotnet_backed_value_as_a_ushorteger_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("12").AsDotnetBackedValue();
        var result = (ushort)sut;
        Assert.AreEqual((ushort)12, result);
    }

    [TestMethod]
    public void Cast_to_ushort_for_dotnet_backed_value_as_a_ushorteger_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("12").AsDotnetBackedValue();
        var result = (ushort)sut;
        Assert.AreEqual((ushort)12, result);
    }

    [TestMethod]
    public void Cast_to_ushort_for_dotnet_backed_value_as_a_ushorteger_JsonByte()
    {
        var sut = JsonByte.Parse("12").AsDotnetBackedValue();
        var result = (ushort)sut;
        Assert.AreEqual((ushort)12, result);
    }

    [TestMethod]
    public void Cast_to_byte_for_json_element_backed_value_as_a_byteeger_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("12".AsSpan());
        var result = (byte)sut;
        Assert.AreEqual((byte)12, result);
    }

    [TestMethod]
    public void Cast_to_byte_for_json_element_backed_value_as_a_byteeger_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("12".AsSpan());
        var result = (byte)sut;
        Assert.AreEqual((byte)12, result);
    }

    [TestMethod]
    public void Cast_to_byte_for_json_element_backed_value_as_a_byteeger_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("12".AsSpan());
        var result = (byte)sut;
        Assert.AreEqual((byte)12, result);
    }

    [TestMethod]
    public void Cast_to_byte_for_json_element_backed_value_as_a_byteeger_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("12".AsSpan());
        var result = (byte)sut;
        Assert.AreEqual((byte)12, result);
    }

    [TestMethod]
    public void Cast_to_byte_for_json_element_backed_value_as_a_byteeger_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("12".AsSpan());
        var result = (byte)sut;
        Assert.AreEqual((byte)12, result);
    }

    [TestMethod]
    public void Cast_to_byte_for_json_element_backed_value_as_a_byteeger_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("12".AsSpan());
        var result = (byte)sut;
        Assert.AreEqual((byte)12, result);
    }

    [TestMethod]
    public void Cast_to_byte_for_json_element_backed_value_as_a_byteeger_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("12".AsSpan());
        var result = (byte)sut;
        Assert.AreEqual((byte)12, result);
    }

    [TestMethod]
    public void Cast_to_byte_for_json_element_backed_value_as_a_byteeger_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("12".AsSpan());
        var result = (byte)sut;
        Assert.AreEqual((byte)12, result);
    }

    [TestMethod]
    public void Cast_to_byte_for_json_element_backed_value_as_a_byteeger_JsonByte()
    {
        var sut = JsonByte.ParseValue("12".AsSpan());
        var result = (byte)sut;
        Assert.AreEqual((byte)12, result);
    }

    [TestMethod]
    public void Cast_to_byte_for_dotnet_backed_value_as_a_byteeger_JsonInteger()
    {
        var sut = JsonInteger.Parse("12").AsDotnetBackedValue();
        var result = (byte)sut;
        Assert.AreEqual((byte)12, result);
    }

    [TestMethod]
    public void Cast_to_byte_for_dotnet_backed_value_as_a_byteeger_JsonInt64()
    {
        var sut = JsonInt64.Parse("12").AsDotnetBackedValue();
        var result = (byte)sut;
        Assert.AreEqual((byte)12, result);
    }

    [TestMethod]
    public void Cast_to_byte_for_dotnet_backed_value_as_a_byteeger_JsonInt32()
    {
        var sut = JsonInt32.Parse("12").AsDotnetBackedValue();
        var result = (byte)sut;
        Assert.AreEqual((byte)12, result);
    }

    [TestMethod]
    public void Cast_to_byte_for_dotnet_backed_value_as_a_byteeger_JsonInt16()
    {
        var sut = JsonInt16.Parse("12").AsDotnetBackedValue();
        var result = (byte)sut;
        Assert.AreEqual((byte)12, result);
    }

    [TestMethod]
    public void Cast_to_byte_for_dotnet_backed_value_as_a_byteeger_JsonSByte()
    {
        var sut = JsonSByte.Parse("12").AsDotnetBackedValue();
        var result = (byte)sut;
        Assert.AreEqual((byte)12, result);
    }

    [TestMethod]
    public void Cast_to_byte_for_dotnet_backed_value_as_a_byteeger_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("12").AsDotnetBackedValue();
        var result = (byte)sut;
        Assert.AreEqual((byte)12, result);
    }

    [TestMethod]
    public void Cast_to_byte_for_dotnet_backed_value_as_a_byteeger_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("12").AsDotnetBackedValue();
        var result = (byte)sut;
        Assert.AreEqual((byte)12, result);
    }

    [TestMethod]
    public void Cast_to_byte_for_dotnet_backed_value_as_a_byteeger_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("12").AsDotnetBackedValue();
        var result = (byte)sut;
        Assert.AreEqual((byte)12, result);
    }

    [TestMethod]
    public void Cast_to_byte_for_dotnet_backed_value_as_a_byteeger_JsonByte()
    {
        var sut = JsonByte.Parse("12").AsDotnetBackedValue();
        var result = (byte)sut;
        Assert.AreEqual((byte)12, result);
    }

    [TestMethod]
    public void Cast_to_sbyte_for_json_element_backed_value_as_a_sbyteeger_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("12".AsSpan());
        var result = (sbyte)sut;
        Assert.AreEqual((sbyte)12, result);
    }

    [TestMethod]
    public void Cast_to_sbyte_for_json_element_backed_value_as_a_sbyteeger_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("12".AsSpan());
        var result = (sbyte)sut;
        Assert.AreEqual((sbyte)12, result);
    }

    [TestMethod]
    public void Cast_to_sbyte_for_json_element_backed_value_as_a_sbyteeger_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("12".AsSpan());
        var result = (sbyte)sut;
        Assert.AreEqual((sbyte)12, result);
    }

    [TestMethod]
    public void Cast_to_sbyte_for_json_element_backed_value_as_a_sbyteeger_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("12".AsSpan());
        var result = (sbyte)sut;
        Assert.AreEqual((sbyte)12, result);
    }

    [TestMethod]
    public void Cast_to_sbyte_for_json_element_backed_value_as_a_sbyteeger_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("12".AsSpan());
        var result = (sbyte)sut;
        Assert.AreEqual((sbyte)12, result);
    }

    [TestMethod]
    public void Cast_to_sbyte_for_json_element_backed_value_as_a_sbyteeger_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("12".AsSpan());
        var result = (sbyte)sut;
        Assert.AreEqual((sbyte)12, result);
    }

    [TestMethod]
    public void Cast_to_sbyte_for_json_element_backed_value_as_a_sbyteeger_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("12".AsSpan());
        var result = (sbyte)sut;
        Assert.AreEqual((sbyte)12, result);
    }

    [TestMethod]
    public void Cast_to_sbyte_for_json_element_backed_value_as_a_sbyteeger_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("12".AsSpan());
        var result = (sbyte)sut;
        Assert.AreEqual((sbyte)12, result);
    }

    [TestMethod]
    public void Cast_to_sbyte_for_json_element_backed_value_as_a_sbyteeger_JsonByte()
    {
        var sut = JsonByte.ParseValue("12".AsSpan());
        var result = (sbyte)sut;
        Assert.AreEqual((sbyte)12, result);
    }

    [TestMethod]
    public void Cast_to_sbyte_for_dotnet_backed_value_as_a_sbyteeger_JsonInteger()
    {
        var sut = JsonInteger.Parse("12").AsDotnetBackedValue();
        var result = (sbyte)sut;
        Assert.AreEqual((sbyte)12, result);
    }

    [TestMethod]
    public void Cast_to_sbyte_for_dotnet_backed_value_as_a_sbyteeger_JsonInt64()
    {
        var sut = JsonInt64.Parse("12").AsDotnetBackedValue();
        var result = (sbyte)sut;
        Assert.AreEqual((sbyte)12, result);
    }

    [TestMethod]
    public void Cast_to_sbyte_for_dotnet_backed_value_as_a_sbyteeger_JsonInt32()
    {
        var sut = JsonInt32.Parse("12").AsDotnetBackedValue();
        var result = (sbyte)sut;
        Assert.AreEqual((sbyte)12, result);
    }

    [TestMethod]
    public void Cast_to_sbyte_for_dotnet_backed_value_as_a_sbyteeger_JsonInt16()
    {
        var sut = JsonInt16.Parse("12").AsDotnetBackedValue();
        var result = (sbyte)sut;
        Assert.AreEqual((sbyte)12, result);
    }

    [TestMethod]
    public void Cast_to_sbyte_for_dotnet_backed_value_as_a_sbyteeger_JsonSByte()
    {
        var sut = JsonSByte.Parse("12").AsDotnetBackedValue();
        var result = (sbyte)sut;
        Assert.AreEqual((sbyte)12, result);
    }

    [TestMethod]
    public void Cast_to_sbyte_for_dotnet_backed_value_as_a_sbyteeger_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("12").AsDotnetBackedValue();
        var result = (sbyte)sut;
        Assert.AreEqual((sbyte)12, result);
    }

    [TestMethod]
    public void Cast_to_sbyte_for_dotnet_backed_value_as_a_sbyteeger_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("12").AsDotnetBackedValue();
        var result = (sbyte)sut;
        Assert.AreEqual((sbyte)12, result);
    }

    [TestMethod]
    public void Cast_to_sbyte_for_dotnet_backed_value_as_a_sbyteeger_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("12").AsDotnetBackedValue();
        var result = (sbyte)sut;
        Assert.AreEqual((sbyte)12, result);
    }

    [TestMethod]
    public void Cast_to_sbyte_for_dotnet_backed_value_as_a_sbyteeger_JsonByte()
    {
        var sut = JsonByte.Parse("12").AsDotnetBackedValue();
        var result = (sbyte)sut;
        Assert.AreEqual((sbyte)12, result);
    }

    [TestMethod]
    public void Cast_to_uint_for_json_element_backed_value_as_a_uinteger_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("12".AsSpan());
        var result = (uint)sut;
        Assert.AreEqual(12U, result);
    }

    [TestMethod]
    public void Cast_to_uint_for_json_element_backed_value_as_a_uinteger_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("12".AsSpan());
        var result = (uint)sut;
        Assert.AreEqual(12U, result);
    }

    [TestMethod]
    public void Cast_to_uint_for_json_element_backed_value_as_a_uinteger_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("12".AsSpan());
        var result = (uint)sut;
        Assert.AreEqual(12U, result);
    }

    [TestMethod]
    public void Cast_to_uint_for_json_element_backed_value_as_a_uinteger_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("12".AsSpan());
        var result = (uint)sut;
        Assert.AreEqual(12U, result);
    }

    [TestMethod]
    public void Cast_to_uint_for_json_element_backed_value_as_a_uinteger_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("12".AsSpan());
        var result = (uint)sut;
        Assert.AreEqual(12U, result);
    }

    [TestMethod]
    public void Cast_to_uint_for_json_element_backed_value_as_a_uinteger_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("12".AsSpan());
        var result = (uint)sut;
        Assert.AreEqual(12U, result);
    }

    [TestMethod]
    public void Cast_to_uint_for_json_element_backed_value_as_a_uinteger_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("12".AsSpan());
        var result = (uint)sut;
        Assert.AreEqual(12U, result);
    }

    [TestMethod]
    public void Cast_to_uint_for_json_element_backed_value_as_a_uinteger_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("12".AsSpan());
        var result = (uint)sut;
        Assert.AreEqual(12U, result);
    }

    [TestMethod]
    public void Cast_to_uint_for_json_element_backed_value_as_a_uinteger_JsonByte()
    {
        var sut = JsonByte.ParseValue("12".AsSpan());
        var result = (uint)sut;
        Assert.AreEqual(12U, result);
    }

    [TestMethod]
    public void Cast_to_uint_for_dotnet_backed_value_as_a_uinteger_JsonInteger()
    {
        var sut = JsonInteger.Parse("12").AsDotnetBackedValue();
        var result = (uint)sut;
        Assert.AreEqual(12U, result);
    }

    [TestMethod]
    public void Cast_to_uint_for_dotnet_backed_value_as_a_uinteger_JsonInt64()
    {
        var sut = JsonInt64.Parse("12").AsDotnetBackedValue();
        var result = (uint)sut;
        Assert.AreEqual(12U, result);
    }

    [TestMethod]
    public void Cast_to_uint_for_dotnet_backed_value_as_a_uinteger_JsonInt32()
    {
        var sut = JsonInt32.Parse("12").AsDotnetBackedValue();
        var result = (uint)sut;
        Assert.AreEqual(12U, result);
    }

    [TestMethod]
    public void Cast_to_uint_for_dotnet_backed_value_as_a_uinteger_JsonInt16()
    {
        var sut = JsonInt16.Parse("12").AsDotnetBackedValue();
        var result = (uint)sut;
        Assert.AreEqual(12U, result);
    }

    [TestMethod]
    public void Cast_to_uint_for_dotnet_backed_value_as_a_uinteger_JsonSByte()
    {
        var sut = JsonSByte.Parse("12").AsDotnetBackedValue();
        var result = (uint)sut;
        Assert.AreEqual(12U, result);
    }

    [TestMethod]
    public void Cast_to_uint_for_dotnet_backed_value_as_a_uinteger_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("12").AsDotnetBackedValue();
        var result = (uint)sut;
        Assert.AreEqual(12U, result);
    }

    [TestMethod]
    public void Cast_to_uint_for_dotnet_backed_value_as_a_uinteger_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("12").AsDotnetBackedValue();
        var result = (uint)sut;
        Assert.AreEqual(12U, result);
    }

    [TestMethod]
    public void Cast_to_uint_for_dotnet_backed_value_as_a_uinteger_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("12").AsDotnetBackedValue();
        var result = (uint)sut;
        Assert.AreEqual(12U, result);
    }

    [TestMethod]
    public void Cast_to_uint_for_dotnet_backed_value_as_a_uinteger_JsonByte()
    {
        var sut = JsonByte.Parse("12").AsDotnetBackedValue();
        var result = (uint)sut;
        Assert.AreEqual(12U, result);
    }

    [TestMethod]
    public void Cast_to_ulong_for_json_element_backed_value_as_a_ulongeger_JsonInteger()
    {
        var sut = JsonInteger.ParseValue("12".AsSpan());
        var result = (ulong)sut;
        Assert.AreEqual(12UL, result);
    }

    [TestMethod]
    public void Cast_to_ulong_for_json_element_backed_value_as_a_ulongeger_JsonInt64()
    {
        var sut = JsonInt64.ParseValue("12".AsSpan());
        var result = (ulong)sut;
        Assert.AreEqual(12UL, result);
    }

    [TestMethod]
    public void Cast_to_ulong_for_json_element_backed_value_as_a_ulongeger_JsonInt32()
    {
        var sut = JsonInt32.ParseValue("12".AsSpan());
        var result = (ulong)sut;
        Assert.AreEqual(12UL, result);
    }

    [TestMethod]
    public void Cast_to_ulong_for_json_element_backed_value_as_a_ulongeger_JsonInt16()
    {
        var sut = JsonInt16.ParseValue("12".AsSpan());
        var result = (ulong)sut;
        Assert.AreEqual(12UL, result);
    }

    [TestMethod]
    public void Cast_to_ulong_for_json_element_backed_value_as_a_ulongeger_JsonSByte()
    {
        var sut = JsonSByte.ParseValue("12".AsSpan());
        var result = (ulong)sut;
        Assert.AreEqual(12UL, result);
    }

    [TestMethod]
    public void Cast_to_ulong_for_json_element_backed_value_as_a_ulongeger_JsonUInt64()
    {
        var sut = JsonUInt64.ParseValue("12".AsSpan());
        var result = (ulong)sut;
        Assert.AreEqual(12UL, result);
    }

    [TestMethod]
    public void Cast_to_ulong_for_json_element_backed_value_as_a_ulongeger_JsonUInt32()
    {
        var sut = JsonUInt32.ParseValue("12".AsSpan());
        var result = (ulong)sut;
        Assert.AreEqual(12UL, result);
    }

    [TestMethod]
    public void Cast_to_ulong_for_json_element_backed_value_as_a_ulongeger_JsonUInt16()
    {
        var sut = JsonUInt16.ParseValue("12".AsSpan());
        var result = (ulong)sut;
        Assert.AreEqual(12UL, result);
    }

    [TestMethod]
    public void Cast_to_ulong_for_json_element_backed_value_as_a_ulongeger_JsonByte()
    {
        var sut = JsonByte.ParseValue("12".AsSpan());
        var result = (ulong)sut;
        Assert.AreEqual(12UL, result);
    }

    [TestMethod]
    public void Cast_to_ulong_for_dotnet_backed_value_as_a_ulongeger_JsonInteger()
    {
        var sut = JsonInteger.Parse("12").AsDotnetBackedValue();
        var result = (ulong)sut;
        Assert.AreEqual(12UL, result);
    }

    [TestMethod]
    public void Cast_to_ulong_for_dotnet_backed_value_as_a_ulongeger_JsonInt64()
    {
        var sut = JsonInt64.Parse("12").AsDotnetBackedValue();
        var result = (ulong)sut;
        Assert.AreEqual(12UL, result);
    }

    [TestMethod]
    public void Cast_to_ulong_for_dotnet_backed_value_as_a_ulongeger_JsonInt32()
    {
        var sut = JsonInt32.Parse("12").AsDotnetBackedValue();
        var result = (ulong)sut;
        Assert.AreEqual(12UL, result);
    }

    [TestMethod]
    public void Cast_to_ulong_for_dotnet_backed_value_as_a_ulongeger_JsonInt16()
    {
        var sut = JsonInt16.Parse("12").AsDotnetBackedValue();
        var result = (ulong)sut;
        Assert.AreEqual(12UL, result);
    }

    [TestMethod]
    public void Cast_to_ulong_for_dotnet_backed_value_as_a_ulongeger_JsonSByte()
    {
        var sut = JsonSByte.Parse("12").AsDotnetBackedValue();
        var result = (ulong)sut;
        Assert.AreEqual(12UL, result);
    }

    [TestMethod]
    public void Cast_to_ulong_for_dotnet_backed_value_as_a_ulongeger_JsonUInt64()
    {
        var sut = JsonUInt64.Parse("12").AsDotnetBackedValue();
        var result = (ulong)sut;
        Assert.AreEqual(12UL, result);
    }

    [TestMethod]
    public void Cast_to_ulong_for_dotnet_backed_value_as_a_ulongeger_JsonUInt32()
    {
        var sut = JsonUInt32.Parse("12").AsDotnetBackedValue();
        var result = (ulong)sut;
        Assert.AreEqual(12UL, result);
    }

    [TestMethod]
    public void Cast_to_ulong_for_dotnet_backed_value_as_a_ulongeger_JsonUInt16()
    {
        var sut = JsonUInt16.Parse("12").AsDotnetBackedValue();
        var result = (ulong)sut;
        Assert.AreEqual(12UL, result);
    }

    [TestMethod]
    public void Cast_to_ulong_for_dotnet_backed_value_as_a_ulongeger_JsonByte()
    {
        var sut = JsonByte.Parse("12").AsDotnetBackedValue();
        var result = (ulong)sut;
        Assert.AreEqual(12UL, result);
    }
}