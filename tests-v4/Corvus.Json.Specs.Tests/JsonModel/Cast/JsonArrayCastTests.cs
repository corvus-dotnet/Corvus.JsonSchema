// <copyright file="JsonArrayCastTests.cs" company="Endjin Limited">
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
/// Tests for JsonArrayCast.
/// </summary>
public class JsonArrayCastTests
{
    [Fact]
    public void Cast_to_JsonAny_for_json_element_backed_value_as_an_array()
    {
        var sut = JsonArray.ParseValue("[1,\"2\",3]".AsSpan());
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("[1, \"2\", 3]".AsSpan()), result);
    }

    [Fact]
    public void Cast_to_JsonAny_for_dotnet_backed_value_as_an_array()
    {
        var sut = JsonArray.Parse("[1,\"2\",3]").AsDotnetBackedValue();
        var result = (JsonAny)sut;
        Assert.Equal(JsonAny.ParseValue("[1, \"2\", 3]".AsSpan()), result);
    }
}