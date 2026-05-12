// <copyright file="PropertyBackingBuildersTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600

using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Internal;

namespace Corvus.Json.Specs.Tests.CoverageGap;

[TestClass]
public class PropertyBackingBuildersTests
{
    [TestMethod]
    public void GetPropertyBackingBuilder_FromObject()
    {
        using var doc = JsonDocument.Parse("""{"a":1,"b":2}""");
        var builder = PropertyBackingBuilders.GetPropertyBackingBuilder(doc.RootElement);
        Assert.AreEqual(2, builder.Count);
    }

    [TestMethod]
    public void GetPropertyBackingBuilder_EmptyObject()
    {
        using var doc = JsonDocument.Parse("{}");
        var builder = PropertyBackingBuilders.GetPropertyBackingBuilder(doc.RootElement);
        Assert.AreEqual(0, builder.Count);
    }

    [TestMethod]
    public void GetPropertyBackingBuilder_NonObject_Throws()
    {
        using var doc = JsonDocument.Parse("[1,2]");
        Assert.ThrowsExactly<InvalidOperationException>(
            () => PropertyBackingBuilders.GetPropertyBackingBuilder(doc.RootElement));
    }

    [TestMethod]
    public void GetPropertyBackingBuilderWithout_JsonPropertyName_RemovesFirst()
    {
        using var doc = JsonDocument.Parse("""{"a":1,"b":2,"c":3}""");
        var name = new JsonPropertyName("b");
        var builder = PropertyBackingBuilders.GetPropertyBackingBuilderWithout(doc.RootElement, name);
        Assert.AreEqual(2, builder.Count);
    }

    [TestMethod]
    public void GetPropertyBackingBuilderWithout_JsonPropertyName_RemovesLast()
    {
        using var doc = JsonDocument.Parse("""{"a":1,"b":2,"c":3}""");
        var name = new JsonPropertyName("c");
        var builder = PropertyBackingBuilders.GetPropertyBackingBuilderWithout(doc.RootElement, name);
        Assert.AreEqual(2, builder.Count);
    }

    [TestMethod]
    public void GetPropertyBackingBuilderWithout_JsonPropertyName_MissingProperty()
    {
        using var doc = JsonDocument.Parse("""{"a":1,"b":2}""");
        var name = new JsonPropertyName("z");
        var builder = PropertyBackingBuilders.GetPropertyBackingBuilderWithout(doc.RootElement, name);
        Assert.AreEqual(2, builder.Count);
    }

    [TestMethod]
    public void GetPropertyBackingBuilderWithout_CharSpan_RemovesMiddle()
    {
        using var doc = JsonDocument.Parse("""{"a":1,"b":2,"c":3}""");
        var builder = PropertyBackingBuilders.GetPropertyBackingBuilderWithout(doc.RootElement, "b".AsSpan());
        Assert.AreEqual(2, builder.Count);
    }

    [TestMethod]
    public void GetPropertyBackingBuilderWithout_CharSpan_NonObject_Throws()
    {
        using var doc = JsonDocument.Parse("42");
        Assert.ThrowsExactly<InvalidOperationException>(
            () => PropertyBackingBuilders.GetPropertyBackingBuilderWithout(doc.RootElement, "a".AsSpan()));
    }

    [TestMethod]
    public void GetPropertyBackingBuilderWithout_Utf8_RemovesMiddle()
    {
        using var doc = JsonDocument.Parse("""{"a":1,"b":2,"c":3}""");
        byte[] utf8Name = "b"u8.ToArray();
        var builder = PropertyBackingBuilders.GetPropertyBackingBuilderWithout(doc.RootElement, utf8Name);
        Assert.AreEqual(2, builder.Count);
    }

    [TestMethod]
    public void GetPropertyBackingBuilderWithout_Utf8_NonObject_Throws()
    {
        using var doc = JsonDocument.Parse("null");
        Assert.ThrowsExactly<InvalidOperationException>(
            () => PropertyBackingBuilders.GetPropertyBackingBuilderWithout(doc.RootElement, "a"u8));
    }

    [TestMethod]
    public void GetPropertyBackingBuilderWithout_String_RemovesMiddle()
    {
        using var doc = JsonDocument.Parse("""{"a":1,"b":2,"c":3}""");
        var builder = PropertyBackingBuilders.GetPropertyBackingBuilderWithout(doc.RootElement, "b");
        Assert.AreEqual(2, builder.Count);
    }

    [TestMethod]
    public void GetPropertyBackingBuilderWithout_String_RemovesFirst()
    {
        using var doc = JsonDocument.Parse("""{"a":1,"b":2,"c":3}""");
        var builder = PropertyBackingBuilders.GetPropertyBackingBuilderWithout(doc.RootElement, "a");
        Assert.AreEqual(2, builder.Count);
    }

    [TestMethod]
    public void GetPropertyBackingBuilderWithout_String_NonObject_Throws()
    {
        using var doc = JsonDocument.Parse("true");
        Assert.ThrowsExactly<InvalidOperationException>(
            () => PropertyBackingBuilders.GetPropertyBackingBuilderWithout(doc.RootElement, "a"));
    }

    [TestMethod]
    public void GetPropertyBackingBuilderReplacing_ExistingProperty()
    {
        using var doc = JsonDocument.Parse("""{"a":1,"b":2,"c":3}""");
        var name = new JsonPropertyName("b");
        JsonAny newValue = JsonAny.Parse("99");
        var builder = PropertyBackingBuilders.GetPropertyBackingBuilderReplacing(doc.RootElement, name, newValue);
        Assert.AreEqual(3, builder.Count);
    }

    [TestMethod]
    public void GetPropertyBackingBuilderReplacing_MissingProperty_Appends()
    {
        using var doc = JsonDocument.Parse("""{"a":1,"b":2}""");
        var name = new JsonPropertyName("c");
        JsonAny newValue = JsonAny.Parse("3");
        var builder = PropertyBackingBuilders.GetPropertyBackingBuilderReplacing(doc.RootElement, name, newValue);
        Assert.AreEqual(3, builder.Count);
    }

    [TestMethod]
    public void GetPropertyBackingBuilderReplacing_NonObject_Throws()
    {
        using var doc = JsonDocument.Parse("[1]");
        Assert.ThrowsExactly<InvalidOperationException>(
            () => PropertyBackingBuilders.GetPropertyBackingBuilderReplacing(
                doc.RootElement, new JsonPropertyName("a"), JsonAny.Parse("1")));
    }
}