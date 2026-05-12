// Copyright (c) Matthew Adams. All rights reserved.
// Licensed under the Apache-2.0 license.

using System.Text.Json;
using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests that verify strongly-typed property getters return schema default values
/// when the property is not present in the JSON document.
/// </summary>
[TestClass]
public class GeneratedDefaultPropertyTests
{
    [TestMethod]
    public void MissingStringPropertyWithDefault_ReturnsDefaultValue()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test"}""");

        ObjectWithDefaultProperties instance = doc.RootElement;
        ObjectWithDefaultProperties.StatusEntity status = instance.Status;

        Assert.IsTrue(status.IsNotUndefined());
        Assert.AreEqual(JsonValueKind.String, status.ValueKind);
        Assert.AreEqual("active", (string)status);
    }

    [TestMethod]
    public void MissingIntegerPropertyWithDefault_ReturnsDefaultValue()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test"}""");

        ObjectWithDefaultProperties instance = doc.RootElement;
        ObjectWithDefaultProperties.CountEntity count = instance.Count;

        Assert.IsTrue(count.IsNotUndefined());
        Assert.AreEqual(JsonValueKind.Number, count.ValueKind);
        Assert.AreEqual(0, (int)count);
    }

    [TestMethod]
    public void PresentStringPropertyWithDefault_ReturnsActualValue()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test","status":"inactive"}""");

        ObjectWithDefaultProperties instance = doc.RootElement;
        ObjectWithDefaultProperties.StatusEntity status = instance.Status;

        Assert.IsTrue(status.IsNotUndefined());
        Assert.AreEqual("inactive", (string)status);
    }

    [TestMethod]
    public void PresentIntegerPropertyWithDefault_ReturnsActualValue()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test","count":42}""");

        ObjectWithDefaultProperties instance = doc.RootElement;
        ObjectWithDefaultProperties.CountEntity count = instance.Count;

        Assert.IsTrue(count.IsNotUndefined());
        Assert.AreEqual(42, (int)count);
    }

    [TestMethod]
    public void MissingPropertyWithoutDefault_ReturnsNull()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test"}""");

        ObjectWithDefaultProperties instance = doc.RootElement;
        JsonString label = instance.Label;

        Assert.IsTrue(label.IsUndefined());
    }

    [TestMethod]
    public void PresentPropertyWithoutDefault_ReturnsActualValue()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test","label":"hello"}""");

        ObjectWithDefaultProperties instance = doc.RootElement;
        JsonString label = instance.Label;

        Assert.IsTrue(label.IsNotUndefined());
        Assert.AreEqual("hello", (string)label);
    }

    [TestMethod]
    public void RequiredPropertyPresent_ReturnsActualValue()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test"}""");

        ObjectWithDefaultProperties instance = doc.RootElement;
        JsonString name = instance.Name;

        Assert.AreEqual(JsonValueKind.String, name.ValueKind);
        Assert.AreEqual("test", (string)name);
    }

    [TestMethod]
    public void AllPropertiesPresent_ReturnsActualValues()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"Alice","status":"pending","count":5,"label":"important"}""");

        ObjectWithDefaultProperties instance = doc.RootElement;

        Assert.AreEqual("Alice", (string)instance.Name);
        Assert.AreEqual("pending", (string)instance.Status);
        Assert.AreEqual(5, (int)instance.Count);
        Assert.AreEqual("important", (string)instance.Label);
    }
}
