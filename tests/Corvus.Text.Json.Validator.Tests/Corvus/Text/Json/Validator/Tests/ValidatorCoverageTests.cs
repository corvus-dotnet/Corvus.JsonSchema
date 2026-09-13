// <copyright file="ValidatorCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json.RuntimeEvaluator;
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
    // Every Validate overload against a boolean true root.

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
    // Every Validate overload against a boolean false root.

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

    // -- Validate(in JsonElement) --

    [TestMethod]
    public void Validate_JsonElement_Valid()
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
    public void Validate_JsonElement_Invalid()
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
        string schemaWithoutId = """{ "type": "string" }""";
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(schemaWithoutId));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            JsonSchema.FromStream(stream));
    }

    // -- Round 2: FromUri and From with AdditionalDocumentResolver --

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

        JsonSchemaDocumentResolver resolver = Prepopulated(schemaUri, schemaText);

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

        JsonSchemaDocumentResolver resolver = Prepopulated(schemaUri, schemaText);

        var options = new JsonSchema.Options(
            allowFileSystemAndHttpResolution: false,
            additionalDocumentResolver: resolver);

        // First call populates cache
        var schema1 = JsonSchema.FromUri(schemaUri, options, refreshCache: true);
        Assert.IsTrue(schema1.Validate("\"hello\""));

        // Second call should hit the cache
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

        JsonSchemaDocumentResolver resolver1 = Prepopulated(schemaUri, stringSchema);
        var options1 = new JsonSchema.Options(
            allowFileSystemAndHttpResolution: false,
            additionalDocumentResolver: resolver1);

        // First call with string schema
        var schema1 = JsonSchema.FromUri(schemaUri, options1, refreshCache: true);
        Assert.IsTrue(schema1.Validate("\"hello\""));

        // Refresh with the integer schema
        JsonSchemaDocumentResolver resolver2 = Prepopulated(schemaUri, intSchema);
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
        const string schemaUri = "https://example.com/test/from-delegate";
        string schemaText = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/from-delegate",
              "type": "integer"
            }
            """;

        JsonSchemaDocumentResolver resolver = Prepopulated(schemaUri, schemaText);

        var options = new JsonSchema.Options(
            allowFileSystemAndHttpResolution: false,
            additionalDocumentResolver: resolver);

        var schema = JsonSchema.From(schemaUri, options, refreshCache: true);
        Assert.IsTrue(schema.Validate("42"));
        Assert.IsFalse(schema.Validate("\"hello\""));
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
