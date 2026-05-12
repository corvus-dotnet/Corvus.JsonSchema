// <copyright file="ValidatorCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Validator.Tests;

/// <summary>
/// Tests targeting specific uncovered lines in the Validator package.
/// Covers boolean schema overloads, FromStream cache paths,
/// JsonElement validation, and FromStream-without-$id error path.
/// </summary>
[TestClass]
public class ValidatorCoverageTests
{
    private const string TrueSchemaUri = "https://example.com/test/true-coverage";
    private const string FalseSchemaUri = "https://example.com/test/false-coverage";

    // -- Boolean true schema: all Validate overloads --
    // Covers ValidatorPipeline.AlwaysTruePipeline lines 82, 84, 86, 88, 90

    [TestMethod]
    public void BooleanTrue_Validate_ReadOnlyMemoryByte_ReturnsTrue()
    {
        var schema = JsonSchema.FromText("true", TrueSchemaUri + "/rom-byte");
        byte[] utf8 = Encoding.UTF8.GetBytes("42");
        Assert.IsTrue(schema.Validate(new ReadOnlyMemory<byte>(utf8)));
    }

    [TestMethod]
    public void BooleanTrue_Validate_ReadOnlyMemoryChar_ReturnsTrue()
    {
        var schema = JsonSchema.FromText("true", TrueSchemaUri + "/rom-char");
        Assert.IsTrue(schema.Validate("42".AsMemory()));
    }

    [TestMethod]
    public void BooleanTrue_Validate_Stream_ReturnsTrue()
    {
        var schema = JsonSchema.FromText("true", TrueSchemaUri + "/stream");
        using MemoryStream stream = new(Encoding.UTF8.GetBytes("42"));
        Assert.IsTrue(schema.Validate(stream));
    }

    [TestMethod]
    public void BooleanTrue_Validate_ReadOnlySequenceByte_ReturnsTrue()
    {
        var schema = JsonSchema.FromText("true", TrueSchemaUri + "/ros-byte");
        byte[] utf8 = Encoding.UTF8.GetBytes("42");
        Assert.IsTrue(schema.Validate(new ReadOnlySequence<byte>(utf8)));
    }

    [TestMethod]
    public void BooleanTrue_Validate_JsonElement_ReturnsTrue()
    {
        var schema = JsonSchema.FromText("true", TrueSchemaUri + "/element");
        JsonElement element = JsonElement.ParseValue("42"u8.ToArray());
        Assert.IsTrue(schema.Validate(element));
    }

    // -- Boolean false schema: all Validate overloads --
    // Covers ValidatorPipeline.AlwaysFalsePipeline lines 99, 101, 103, 105, 107

    [TestMethod]
    public void BooleanFalse_Validate_ReadOnlyMemoryByte_ReturnsFalse()
    {
        var schema = JsonSchema.FromText("false", FalseSchemaUri + "/rom-byte");
        byte[] utf8 = Encoding.UTF8.GetBytes("42");
        Assert.IsFalse(schema.Validate(new ReadOnlyMemory<byte>(utf8)));
    }

    [TestMethod]
    public void BooleanFalse_Validate_ReadOnlyMemoryChar_ReturnsFalse()
    {
        var schema = JsonSchema.FromText("false", FalseSchemaUri + "/rom-char");
        Assert.IsFalse(schema.Validate("42".AsMemory()));
    }

    [TestMethod]
    public void BooleanFalse_Validate_Stream_ReturnsFalse()
    {
        var schema = JsonSchema.FromText("false", FalseSchemaUri + "/stream");
        using MemoryStream stream = new(Encoding.UTF8.GetBytes("42"));
        Assert.IsFalse(schema.Validate(stream));
    }

    [TestMethod]
    public void BooleanFalse_Validate_ReadOnlySequenceByte_ReturnsFalse()
    {
        var schema = JsonSchema.FromText("false", FalseSchemaUri + "/ros-byte");
        byte[] utf8 = Encoding.UTF8.GetBytes("42");
        Assert.IsFalse(schema.Validate(new ReadOnlySequence<byte>(utf8)));
    }

    [TestMethod]
    public void BooleanFalse_Validate_JsonElement_ReturnsFalse()
    {
        var schema = JsonSchema.FromText("false", FalseSchemaUri + "/element");
        JsonElement element = JsonElement.ParseValue("42"u8.ToArray());
        Assert.IsFalse(schema.Validate(element));
    }

    // -- DynamicTypePipeline.Validate(in JsonElement) --
    // Covers ValidatorPipeline.DynamicTypePipeline lines 143-146

    [TestMethod]
    public void DynamicType_Validate_JsonElement_Valid()
    {
        var schema = JsonSchema.FromText(
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/element-valid",
              "type": "object",
              "required": ["name"],
              "properties": { "name": { "type": "string" } }
            }
            """);

        JsonElement element = JsonElement.ParseValue("""{"name":"Alice"}"""u8.ToArray());
        Assert.IsTrue(schema.Validate(element));
    }

    [TestMethod]
    public void DynamicType_Validate_JsonElement_Invalid()
    {
        var schema = JsonSchema.FromText(
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/element-invalid",
              "type": "object",
              "required": ["name"],
              "properties": { "name": { "type": "string" } }
            }
            """);

        JsonElement element = JsonElement.ParseValue("{}"u8.ToArray());
        Assert.IsFalse(schema.Validate(element));
    }

    // -- FromStream cache paths --
    // Covers JsonSchema.cs lines 97-105

    [TestMethod]
    public void FromStream_SecondCall_ReturnsCached()
    {
        const string uri = "https://example.com/test/stream-cache-hit";

        string stringSchema = """{ "type": "string" }""";
        string integerSchema = """{ "type": "integer" }""";

        // First call: create from string schema
        using MemoryStream stream1 = new(Encoding.UTF8.GetBytes(stringSchema));
        var schema1 = JsonSchema.FromStream(stream1, uri);
        Assert.IsTrue(schema1.Validate("\"hello\""));

        // Second call with DIFFERENT content but same URI → should use cache (still validates as string)
        using MemoryStream stream2 = new(Encoding.UTF8.GetBytes(integerSchema));
        var schema2 = JsonSchema.FromStream(stream2, uri);
        Assert.IsTrue(schema2.Validate("\"hello\""));
    }

    [TestMethod]
    public void FromStream_RefreshCache_Recompiles()
    {
        const string uri = "https://example.com/test/stream-cache-refresh";

        string stringSchema = """{ "type": "string" }""";
        string integerSchema = """{ "type": "integer" }""";

        // First call
        using MemoryStream stream1 = new(Encoding.UTF8.GetBytes(stringSchema));
        var schema1 = JsonSchema.FromStream(stream1, uri);
        Assert.IsTrue(schema1.Validate("\"hello\""));

        // Refresh cache with integer schema
        using MemoryStream stream2 = new(Encoding.UTF8.GetBytes(integerSchema));
        var schema2 = JsonSchema.FromStream(stream2, uri, refreshCache: true);

        // Now "hello" should fail (integer schema) and 42 should pass
        Assert.IsFalse(schema2.Validate("\"hello\""));
        Assert.IsTrue(schema2.Validate("42"));
    }

    [TestMethod]
    public void FromStream_WithoutCanonicalUri_NoSchemaId_Throws()
    {
        // Covers JsonSchema.cs lines 91-92
        string schemaWithoutId = """{ "type": "string" }""";
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(schemaWithoutId));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            JsonSchema.FromStream(stream));
    }

    // -- DynamicJsonType.FromElement null guard --
    // Covers DynamicJsonType.cs lines 125-129 — this is defensive code
    // that only triggers if a generated type's From<T> method returns null.
    // We cannot trigger this through normal usage.

    // -- Round 2: FromUri and From with AdditionalDocumentResolver --
    // Covers JsonSchema.cs lines 153-169 (FromUri), 179-181 (From), 350-352 (AdditionalDocumentResolver)

    [TestMethod]
    public void FromUri_WithAdditionalDocumentResolver_ValidatesCorrectly()
    {
        const string schemaUri = "https://example.com/test/from-uri-resolver";
        string schemaText = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/from-uri-resolver",
              "type": "string"
            }
            """;

        var resolver = new Corvus.Json.PrepopulatedDocumentResolver();
        resolver.AddDocument(schemaUri, System.Text.Json.JsonDocument.Parse(schemaText));

        var options = new JsonSchema.Options(
            allowFileSystemAndHttpResolution: false,
            additionalDocumentResolver: resolver);

        var schema = JsonSchema.FromUri(schemaUri, options, refreshCache: true);
        Assert.IsTrue(schema.Validate("\"hello\""));
        Assert.IsFalse(schema.Validate("42"));
    }

    [TestMethod]
    public void FromUri_CacheHit_ReturnsWithoutReresolution()
    {
        const string schemaUri = "https://example.com/test/from-uri-cache-hit";
        string schemaText = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/from-uri-cache-hit",
              "type": "string"
            }
            """;

        var resolver = new Corvus.Json.PrepopulatedDocumentResolver();
        resolver.AddDocument(schemaUri, System.Text.Json.JsonDocument.Parse(schemaText));

        var options = new JsonSchema.Options(
            allowFileSystemAndHttpResolution: false,
            additionalDocumentResolver: resolver);

        // First call populates cache
        var schema1 = JsonSchema.FromUri(schemaUri, options, refreshCache: true);
        Assert.IsTrue(schema1.Validate("\"hello\""));

        // Second call should hit cache (L158-160)
        var schema2 = JsonSchema.FromUri(schemaUri, options);
        Assert.IsTrue(schema2.Validate("\"hello\""));
    }

    [TestMethod]
    public void FromUri_RefreshCache_RecompilesSchema()
    {
        const string schemaUri = "https://example.com/test/from-uri-cache-refresh";

        string stringSchema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/from-uri-cache-refresh",
              "type": "string"
            }
            """;
        string intSchema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/from-uri-cache-refresh",
              "type": "integer"
            }
            """;

        var resolver1 = new Corvus.Json.PrepopulatedDocumentResolver();
        resolver1.AddDocument(schemaUri, System.Text.Json.JsonDocument.Parse(stringSchema));
        var options1 = new JsonSchema.Options(
            allowFileSystemAndHttpResolution: false,
            additionalDocumentResolver: resolver1);

        // First call with string schema
        var schema1 = JsonSchema.FromUri(schemaUri, options1, refreshCache: true);
        Assert.IsTrue(schema1.Validate("\"hello\""));

        // Refresh with integer schema (L163-166)
        var resolver2 = new Corvus.Json.PrepopulatedDocumentResolver();
        resolver2.AddDocument(schemaUri, System.Text.Json.JsonDocument.Parse(intSchema));
        var options2 = new JsonSchema.Options(
            allowFileSystemAndHttpResolution: false,
            additionalDocumentResolver: resolver2);

        var schema2 = JsonSchema.FromUri(schemaUri, options2, refreshCache: true);
        Assert.IsFalse(schema2.Validate("\"hello\""));
        Assert.IsTrue(schema2.Validate("42"));
    }

    [TestMethod]
    public void From_DelegatesToFromUri()
    {
        // Covers line 179-181 (From delegates to FromUri)
        const string schemaUri = "https://example.com/test/from-delegate";
        string schemaText = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/from-delegate",
              "type": "integer"
            }
            """;

        var resolver = new Corvus.Json.PrepopulatedDocumentResolver();
        resolver.AddDocument(schemaUri, System.Text.Json.JsonDocument.Parse(schemaText));

        var options = new JsonSchema.Options(
            allowFileSystemAndHttpResolution: false,
            additionalDocumentResolver: resolver);

        var schema = JsonSchema.From(schemaUri, options, refreshCache: true);
        Assert.IsTrue(schema.Validate("42"));
        Assert.IsFalse(schema.Validate("\"hello\""));
    }
}
