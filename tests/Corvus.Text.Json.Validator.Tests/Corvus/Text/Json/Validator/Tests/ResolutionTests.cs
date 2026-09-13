// <copyright file="ResolutionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.RuntimeEvaluator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Validator.Tests;

/// <summary>
/// Document resolution through the runtime evaluator: embedded metaschemas, fragments, and file-relative references.
/// </summary>
[TestClass]
public class ResolutionTests
{
    [TestMethod]
    public void FromUri_EmbeddedMetaschema_ResolvesWithoutNetwork()
    {
        JsonSchema.Options options = new(allowFileSystemAndHttpResolution: false);

        var schema = JsonSchema.FromUri("https://json-schema.org/draft/2020-12/schema", options);

        Assert.IsTrue(schema.Validate("""{"type":"string"}"""));
        Assert.IsFalse(schema.Validate("""{"type":12}"""));
    }

    [TestMethod]
    public void FromUri_WithFragment_ValidatesAgainstSubschema()
    {
        const string documentUri = "https://example.com/test/fragment-entry-point";
        string schemaText = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/fragment-entry-point",
              "type": "object",
              "$defs": {
                "count": { "type": "integer", "minimum": 1 }
              }
            }
            """;

        JsonSchema.Options options = new(
            allowFileSystemAndHttpResolution: false,
            additionalDocumentResolver: Prepopulated(documentUri, schemaText));

        var schema = JsonSchema.FromUri(documentUri + "#/$defs/count", options, refreshCache: true);

        Assert.IsTrue(schema.Validate("3"));
        Assert.IsFalse(schema.Validate("0"));
        Assert.IsFalse(schema.Validate("{}"));
    }

    [TestMethod]
    public void FromFile_RelativeRef_ResolvesSiblingFile()
    {
        string schemaPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "person-with-relative-address.json");

        var schema = JsonSchema.FromFile(schemaPath);

        Assert.IsTrue(schema.Validate("""{"name":"Alice","address":{"street":"1 Main St","city":"Springfield"}}"""));
        Assert.IsFalse(schema.Validate("""{"name":"Alice","address":{"city":"Springfield"}}"""));
    }

    [TestMethod]
    public void FromFile_RelativeRef_WithoutFileSystemResolution_Throws()
    {
        string schemaPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "person-with-relative-address.json");
        JsonSchema.Options options = new(allowFileSystemAndHttpResolution: false);

        Assert.ThrowsExactly<JsonSchemaCompilationException>(() => JsonSchema.FromFile(schemaPath, options, refreshCache: true));
    }

    [TestMethod]
    public void FromText_DefaultDialect_AppliesWhenSchemaKeywordAbsent()
    {
        // Draft 4 treats exclusiveMaximum as a boolean modifier of maximum.
        string schemaText = """{ "maximum": 10, "exclusiveMaximum": true }""";
        JsonSchema.Options draft4 = new(defaultDialect: JsonSchemaDialect.Draft4);

        var schema = JsonSchema.FromText(schemaText, "https://example.com/test/default-dialect-draft4", draft4);

        Assert.IsTrue(schema.Validate("9"));
        Assert.IsFalse(schema.Validate("10"));
    }

    private static JsonSchemaDocumentResolver Prepopulated(string uri, string schemaText)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(schemaText);
        return (string requested, out ReadOnlyMemory<byte> utf8Json) =>
        {
            if (requested == uri)
            {
                utf8Json = utf8;
                return true;
            }

            utf8Json = default;
            return false;
        };
    }
}
