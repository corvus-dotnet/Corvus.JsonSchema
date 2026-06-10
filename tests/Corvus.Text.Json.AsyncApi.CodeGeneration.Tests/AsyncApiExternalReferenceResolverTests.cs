// <copyright file="AsyncApiExternalReferenceResolverTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi.CodeGeneration;

namespace Corvus.Text.Json.AsyncApi.CodeGeneration.Tests;

[TestClass]
public class AsyncApiExternalReferenceResolverTests
{
    private static readonly string EntryDoc = """
        {
          "components": {
            "schemas": {
              "Pet": { "type": "object", "properties": { "name": { "type": "string" } } }
            }
          }
        }
        """;

    private static readonly string ExternalSchemaDoc = """
        {
          "definitions": {
            "Error": { "type": "object", "properties": { "code": { "type": "integer" } } }
          },
          "type": "object",
          "properties": { "id": { "type": "integer" } }
        }
        """;

    [TestMethod]
    public void Constructor_NullBaseUri_ThrowsArgumentNullException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new AsyncApiExternalReferenceResolver(doc.RootElement, (Uri)null!));
    }

    [TestMethod]
    public void UriBase_FragmentOnly_ResolvesInEntryDoc()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using AsyncApiExternalReferenceResolver resolver = new(doc.RootElement, new Uri("https://example.com/api/asyncapi.json"));

        Assert.IsTrue(resolver.TryResolve("#/components/schemas/Pet", out JsonElement pet));
        Assert.IsTrue(pet.TryGetProperty("properties"u8, out _));
    }

    [TestMethod]
    public void ExternalDocumentLoader_ResolvesRelativeRefAgainstNonFileBase()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        byte[] externalBytes = Encoding.UTF8.GetBytes(ExternalSchemaDoc);

        using AsyncApiExternalReferenceResolver resolver = new(
            doc.RootElement,
            new Uri("https://example.com/api/asyncapi.json"),
            uri => uri.AbsoluteUri == "https://example.com/api/common.json" ? externalBytes : null);

        Assert.IsTrue(resolver.TryResolve("./common.json#/definitions/Error", out JsonElement error));
        Assert.IsTrue(error.TryGetProperty("properties"u8, out _));
    }

    [TestMethod]
    public void ExternalDocumentLoader_ReturningNull_DoesNotResolve()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using AsyncApiExternalReferenceResolver resolver = new(
            doc.RootElement, new Uri("https://example.com/api/asyncapi.json"), _ => null);

        Assert.IsFalse(resolver.TryResolve("./missing.json#/definitions/Error", out _));
    }
}