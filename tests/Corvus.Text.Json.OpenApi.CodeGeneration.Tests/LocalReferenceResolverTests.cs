// <copyright file="LocalReferenceResolverTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

[TestClass]
public class LocalReferenceResolverTests
{
    private static readonly string TestDoc = """
        {
          "components": {
            "schemas": {
              "Pet": { "type": "object", "properties": { "name": { "type": "string" } } }
            },
            "parameters": {
              "PetId": { "name": "petId", "in": "path", "schema": { "type": "integer" } }
            }
          }
        }
        """;

    [TestMethod]
    public void TryResolve_String_ValidRef_ResolvesCorrectly()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(TestDoc);
        LocalReferenceResolver resolver = new(doc.RootElement);

        bool found = resolver.TryResolve("#/components/schemas/Pet", out JsonElement result);

        Assert.IsTrue(found);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void TryResolve_String_EmptyRef_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(TestDoc);
        LocalReferenceResolver resolver = new(doc.RootElement);

        bool found = resolver.TryResolve(string.Empty, out JsonElement result);

        Assert.IsFalse(found);
        Assert.AreEqual(JsonValueKind.Undefined, result.ValueKind);
    }

    [TestMethod]
    public void TryResolve_String_NullRef_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(TestDoc);
        LocalReferenceResolver resolver = new(doc.RootElement);

        bool found = resolver.TryResolve((string)null!, out JsonElement result);

        Assert.IsFalse(found);
        Assert.AreEqual(JsonValueKind.Undefined, result.ValueKind);
    }

    [TestMethod]
    public void TryResolve_String_NoHashPrefix_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(TestDoc);
        LocalReferenceResolver resolver = new(doc.RootElement);

        bool found = resolver.TryResolve("/components/schemas/Pet", out JsonElement result);

        Assert.IsFalse(found);
        Assert.AreEqual(JsonValueKind.Undefined, result.ValueKind);
    }

    [TestMethod]
    public void TryResolve_String_NonexistentPath_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(TestDoc);
        LocalReferenceResolver resolver = new(doc.RootElement);

        bool found = resolver.TryResolve("#/components/schemas/Dog", out JsonElement result);

        Assert.IsFalse(found);
    }

    [TestMethod]
    public void TryResolve_Utf8_ValidRef_ResolvesCorrectly()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(TestDoc);
        LocalReferenceResolver resolver = new(doc.RootElement);

        bool found = resolver.TryResolve(
            "#/components/parameters/PetId"u8, out JsonElement result);

        Assert.IsTrue(found);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void TryResolve_Utf8_EmptySpan_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(TestDoc);
        LocalReferenceResolver resolver = new(doc.RootElement);

        bool found = resolver.TryResolve(
            ReadOnlySpan<byte>.Empty, out JsonElement result);

        Assert.IsFalse(found);
        Assert.AreEqual(JsonValueKind.Undefined, result.ValueKind);
    }

    [TestMethod]
    public void TryResolve_Utf8_NoHashPrefix_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(TestDoc);
        LocalReferenceResolver resolver = new(doc.RootElement);

        bool found = resolver.TryResolve(
            "/components/schemas/Pet"u8, out JsonElement result);

        Assert.IsFalse(found);
        Assert.AreEqual(JsonValueKind.Undefined, result.ValueKind);
    }

    [TestMethod]
    public void TryResolve_Generic_String_ValidRef_ResolvesCorrectly()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(TestDoc);
        LocalReferenceResolver resolver = new(doc.RootElement);

        bool found = resolver.TryResolve<JsonElement>(
            "#/components/schemas/Pet", out JsonElement result);

        Assert.IsTrue(found);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void TryResolve_Generic_String_InvalidRef_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(TestDoc);
        LocalReferenceResolver resolver = new(doc.RootElement);

        bool found = resolver.TryResolve<JsonElement>(
            "#/no/such/path", out JsonElement result);

        Assert.IsFalse(found);
    }

    [TestMethod]
    public void TryResolve_Generic_String_EmptyRef_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(TestDoc);
        LocalReferenceResolver resolver = new(doc.RootElement);

        bool found = resolver.TryResolve<JsonElement>(
            string.Empty, out JsonElement result);

        Assert.IsFalse(found);
    }

    [TestMethod]
    public void TryResolve_Generic_Utf8_ValidRef_ResolvesCorrectly()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(TestDoc);
        LocalReferenceResolver resolver = new(doc.RootElement);

        bool found = resolver.TryResolve<JsonElement>(
            "#/components/schemas/Pet"u8, out JsonElement result);

        Assert.IsTrue(found);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void TryResolve_Generic_Utf8_InvalidRef_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(TestDoc);
        LocalReferenceResolver resolver = new(doc.RootElement);

        bool found = resolver.TryResolve<JsonElement>(
            "#/no/such/path"u8, out JsonElement result);

        Assert.IsFalse(found);
    }
}