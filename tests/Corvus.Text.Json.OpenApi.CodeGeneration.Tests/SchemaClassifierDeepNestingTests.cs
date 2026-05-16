// <copyright file="SchemaClassifierDeepNestingTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi.CodeGeneration;
using JsonElement = Corvus.Text.Json.JsonElement;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

[TestClass]
public class SchemaClassifierDeepNestingTests
{
    [TestMethod]
    public void FlatObjectProperties_NoDeepNesting()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""
            {
                "type": "object",
                "properties": {
                    "name": { "type": "string" },
                    "age": { "type": "integer" }
                }
            }
            """u8.ToArray());

        Assert.IsFalse(SchemaClassifier.HasDeepNesting(doc.RootElement));
    }

    [TestMethod]
    public void SimpleArray_NoDeepNesting()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""
            {
                "type": "array",
                "items": { "type": "string" }
            }
            """u8.ToArray());

        Assert.IsFalse(SchemaClassifier.HasDeepNesting(doc.RootElement));
    }

    [TestMethod]
    public void NoProperties_NoDeepNesting()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""
            {
                "type": "object"
            }
            """u8.ToArray());

        Assert.IsFalse(SchemaClassifier.HasDeepNesting(doc.RootElement));
    }

    [TestMethod]
    public void ScalarSchema_NoDeepNesting()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""
            {
                "type": "string"
            }
            """u8.ToArray());

        Assert.IsFalse(SchemaClassifier.HasDeepNesting(doc.RootElement));
    }

    [TestMethod]
    public void ObjectPropertyWithNestedObject_HasDeepNesting()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""
            {
                "type": "object",
                "properties": {
                    "name": { "type": "string" },
                    "address": { "type": "object", "properties": { "city": { "type": "string" } } }
                }
            }
            """u8.ToArray());

        Assert.IsTrue(SchemaClassifier.HasDeepNesting(doc.RootElement));
    }

    [TestMethod]
    public void ObjectPropertyWithNestedArray_HasDeepNesting()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""
            {
                "type": "object",
                "properties": {
                    "tags": { "type": "array", "items": { "type": "string" } }
                }
            }
            """u8.ToArray());

        Assert.IsTrue(SchemaClassifier.HasDeepNesting(doc.RootElement));
    }

    [TestMethod]
    public void AdditionalPropertiesWithObject_HasDeepNesting()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""
            {
                "type": "object",
                "additionalProperties": {
                    "type": "object",
                    "properties": { "x": { "type": "integer" } }
                }
            }
            """u8.ToArray());

        Assert.IsTrue(SchemaClassifier.HasDeepNesting(doc.RootElement));
    }

    [TestMethod]
    public void AdditionalPropertiesTrue_NoDeepNesting()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""
            {
                "type": "object",
                "additionalProperties": true
            }
            """u8.ToArray());

        Assert.IsFalse(SchemaClassifier.HasDeepNesting(doc.RootElement));
    }

    [TestMethod]
    public void AdditionalPropertiesScalar_NoDeepNesting()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""
            {
                "type": "object",
                "additionalProperties": { "type": "string" }
            }
            """u8.ToArray());

        Assert.IsFalse(SchemaClassifier.HasDeepNesting(doc.RootElement));
    }

    [TestMethod]
    public void ArrayItemsWithObject_HasDeepNesting()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""
            {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": { "id": { "type": "integer" } }
                }
            }
            """u8.ToArray());

        Assert.IsTrue(SchemaClassifier.HasDeepNesting(doc.RootElement));
    }

    [TestMethod]
    public void ArrayItemsWithArray_HasDeepNesting()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""
            {
                "type": "array",
                "items": {
                    "type": "array",
                    "items": { "type": "string" }
                }
            }
            """u8.ToArray());

        Assert.IsTrue(SchemaClassifier.HasDeepNesting(doc.RootElement));
    }
}