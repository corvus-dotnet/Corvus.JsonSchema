// <copyright file="NamespaceMappingTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Frozen;
using Corvus.Json;
using Corvus.Json.CodeGeneration.CSharp;
using Xunit;

namespace Corvus.Json.Specs.Tests.CodeGeneration;

public class NamespaceMappingTests
{
    [Theory]
    [InlineData("https://example.com/schemas/", "Example", "https://example.com/schemas/", "Example", true)]
    [InlineData("https://example.com/schemas/person.json", "Person", "https://example.com/schemas/person.json", "Person", true)]
    [InlineData("https://example.com/schemas/", "Example", "https://example.com/other/", null, false)]
    public void ExactMatchNamespaceMapping(string baseUri, string ns, string schemaUri, string? expectedNamespace, bool expectedFound)
    {
        var namespaceMap = new Dictionary<string, string>
        {
            { baseUri, ns },
        }.ToFrozenDictionary();

        var jsonReference = new JsonReference(schemaUri);
        bool found = CSharpLanguageProvider.Options.TryGetNamespace(jsonReference, namespaceMap, out string? actualNamespace);

        Assert.Equal(expectedFound, found);

        if (expectedFound)
        {
            Assert.Equal(expectedNamespace, actualNamespace);
        }
        else
        {
            Assert.Null(actualNamespace);
        }
    }

    [Theory]
    [InlineData("https://myschema.io/contracts/v2/messages", "Messages", "https://myschema.io/contracts/v2/messages/helloWorld.yml", "Messages", true)]
    [InlineData("https://example.com/schemas/", "Example", "https://example.com/schemas/person.json", "Example", true)]
    [InlineData("https://example.com/schemas/", "Example", "https://example.com/schemas/nested/deep/type.json", "Example", true)]
    [InlineData("https://example.com/", "Root", "https://example.com/schemas/type.json", "Root", true)]
    public void PrefixMatchNamespaceMapping(string baseUri, string ns, string schemaUri, string? expectedNamespace, bool expectedFound)
    {
        var namespaceMap = new Dictionary<string, string>
        {
            { baseUri, ns },
        }.ToFrozenDictionary();

        var jsonReference = new JsonReference(schemaUri);
        bool found = CSharpLanguageProvider.Options.TryGetNamespace(jsonReference, namespaceMap, out string? actualNamespace);

        Assert.Equal(expectedFound, found);

        if (expectedFound)
        {
            Assert.Equal(expectedNamespace, actualNamespace);
        }
        else
        {
            Assert.Null(actualNamespace);
        }
    }

    [Theory]
    [InlineData("https://example.com/other/type.json", "Root", true)]
    [InlineData("https://example.com/schemas/type.json", "Schemas", true)]
    [InlineData("https://example.com/schemas/v2/type.json", "SchemasV2", true)]
    [InlineData("https://example.com/schemas/v2/nested/a.json", "SchemasV2", true)]
    [InlineData("https://other.com/schemas/type.json", null, false)]
    public void LongestPrefixWinsWhenMultipleMappingsMatch(string schemaUri, string? expectedNamespace, bool expectedFound)
    {
        var namespaceMap = new Dictionary<string, string>
        {
            { "https://example.com/", "Root" },
            { "https://example.com/schemas/", "Schemas" },
            { "https://example.com/schemas/v2/", "SchemasV2" },
        }.ToFrozenDictionary();

        var jsonReference = new JsonReference(schemaUri);
        bool found = CSharpLanguageProvider.Options.TryGetNamespace(jsonReference, namespaceMap, out string? actualNamespace);

        Assert.Equal(expectedFound, found);

        if (expectedFound)
        {
            Assert.Equal(expectedNamespace, actualNamespace);
        }
        else
        {
            Assert.Null(actualNamespace);
        }
    }

    [Fact]
    public void NonAbsoluteUriReturnsFalse()
    {
        var namespaceMap = new Dictionary<string, string>
        {
            { "https://example.com/", "Example" },
        }.ToFrozenDictionary();

        var jsonReference = new JsonReference("schemas/type.json");
        bool found = CSharpLanguageProvider.Options.TryGetNamespace(jsonReference, namespaceMap, out string? actualNamespace);

        Assert.False(found);
        Assert.Null(actualNamespace);
    }
}