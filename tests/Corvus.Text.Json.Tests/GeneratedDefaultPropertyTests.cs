// Copyright (c) Matthew Adams. All rights reserved.
// Licensed under the Apache-2.0 license.

using System.Text.Json;
using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests that verify strongly-typed property getters return schema default values
/// when the property is not present in the JSON document.
/// </summary>
public class GeneratedDefaultPropertyTests
{
    [Fact]
    public void MissingStringPropertyWithDefault_ReturnsDefaultValue()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test"}""");

        ObjectWithDefaultProperties instance = doc.RootElement;
        ObjectWithDefaultProperties.StatusEntity status = instance.Status;

        Assert.True(status.IsNotUndefined());
        Assert.Equal(JsonValueKind.String, status.ValueKind);
        Assert.Equal("active", (string)status);
    }

    [Fact]
    public void MissingIntegerPropertyWithDefault_ReturnsDefaultValue()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test"}""");

        ObjectWithDefaultProperties instance = doc.RootElement;
        ObjectWithDefaultProperties.CountEntity count = instance.Count;

        Assert.True(count.IsNotUndefined());
        Assert.Equal(JsonValueKind.Number, count.ValueKind);
        Assert.Equal(0, (int)count);
    }

    [Fact]
    public void PresentStringPropertyWithDefault_ReturnsActualValue()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test","status":"inactive"}""");

        ObjectWithDefaultProperties instance = doc.RootElement;
        ObjectWithDefaultProperties.StatusEntity status = instance.Status;

        Assert.True(status.IsNotUndefined());
        Assert.Equal("inactive", (string)status);
    }

    [Fact]
    public void PresentIntegerPropertyWithDefault_ReturnsActualValue()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test","count":42}""");

        ObjectWithDefaultProperties instance = doc.RootElement;
        ObjectWithDefaultProperties.CountEntity count = instance.Count;

        Assert.True(count.IsNotUndefined());
        Assert.Equal(42, (int)count);
    }

    [Fact]
    public void MissingPropertyWithoutDefault_ReturnsNull()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test"}""");

        ObjectWithDefaultProperties instance = doc.RootElement;
        JsonString label = instance.Label;

        Assert.True(label.IsUndefined());
    }

    [Fact]
    public void PresentPropertyWithoutDefault_ReturnsActualValue()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test","label":"hello"}""");

        ObjectWithDefaultProperties instance = doc.RootElement;
        JsonString label = instance.Label;

        Assert.True(label.IsNotUndefined());
        Assert.Equal("hello", (string)label);
    }

    [Fact]
    public void RequiredPropertyPresent_ReturnsActualValue()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test"}""");

        ObjectWithDefaultProperties instance = doc.RootElement;
        JsonString name = instance.Name;

        Assert.Equal(JsonValueKind.String, name.ValueKind);
        Assert.Equal("test", (string)name);
    }

    [Fact]
    public void AllPropertiesPresent_ReturnsActualValues()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"Alice","status":"pending","count":5,"label":"important"}""");

        ObjectWithDefaultProperties instance = doc.RootElement;

        Assert.Equal("Alice", (string)instance.Name);
        Assert.Equal("pending", (string)instance.Status);
        Assert.Equal(5, (int)instance.Count);
        Assert.Equal("important", (string)instance.Label);
    }
}