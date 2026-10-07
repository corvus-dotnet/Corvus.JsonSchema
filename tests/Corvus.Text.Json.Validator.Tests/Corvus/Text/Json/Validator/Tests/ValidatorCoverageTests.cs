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

    [TestMethod]
    public void CodeGeneration_DefaultsToDisabled_AndIsCarriedByTheOptions()
    {
        Assert.AreEqual(JsonSchemaCodeGeneration.Disabled, JsonSchema.Options.Default.CodeGeneration);
        Assert.AreEqual(JsonSchemaCodeGeneration.Disabled, new JsonSchema.Options(alwaysAssertFormat: false).CodeGeneration);
        Assert.AreEqual(
            JsonSchemaCodeGeneration.Disabled,
            new JsonSchema.Options(null, true, JsonSchemaDialect.Draft202012, true, null).CodeGeneration);
        Assert.AreEqual(
            JsonSchemaCodeGeneration.AfterWarmUp,
            new JsonSchema.Options(codeGeneration: JsonSchemaCodeGeneration.AfterWarmUp).CodeGeneration);
    }

    [TestMethod]
    [DataRow(JsonSchemaCodeGeneration.Eager)]
    [DataRow(JsonSchemaCodeGeneration.AfterWarmUp)]
    public void CodeGeneration_GivesTheSameResultsAsTheInterpreter(JsonSchemaCodeGeneration codeGeneration)
    {
        string schemaUri = $"https://example.com/test/code-generation-{codeGeneration}";
        string schemaText = $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{schemaUri}}",
              "type": "object",
              "required": ["id"],
              "properties": {
                "id": { "type": "integer", "minimum": 1 },
                "tags": { "type": "array", "items": { "type": "string", "maxLength": 4 }, "uniqueItems": true }
              },
              "additionalProperties": false
            }
            """;

        string[] instances =
        [
            """{"id": 2}""",
            """{"id": 0}""",
            """{"id": 3, "tags": ["a", "bc"]}""",
            """{"id": 3, "tags": ["a", "a"]}""",
            """{"id": 3, "tags": ["abcde"]}""",
            """{"id": 3, "other": true}""",
            """{"tags": []}""",
            """[1, 2]""",
        ];

        var interpreted = JsonSchema.FromText(schemaText, refreshCache: true);
        var generated = JsonSchema.FromText(
            schemaText,
            options: new JsonSchema.Options(codeGeneration: codeGeneration),
            refreshCache: true);

        // More than the 1,000 evaluations after which a schema is compiled in the background, and a pause for the
        // compilation, so that the later rounds run the generated code where the runtime generates it.
        for (int round = 0; round < 1200; round++)
        {
            foreach (string instance in instances)
            {
                Assert.AreEqual(interpreted.Validate(instance), generated.Validate(instance), instance);
            }

            if (round == 1100)
            {
                Thread.Sleep(500);
            }
        }

        // A validation that collects results is the interpreter's, with either setting.
        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        Assert.IsFalse(generated.Validate("""{"id": 0}""", collector));
        Assert.IsTrue(collector.GetResultCount() > 0);
    }

    [TestMethod]
    public void CodeGeneration_HasItsOwnCachedEvaluator()
    {
        const string schemaUri = "https://example.com/test/code-generation-cache";
        string stringSchema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/code-generation-cache",
              "type": "string"
            }
            """;

        string intSchema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/code-generation-cache",
              "type": "integer"
            }
            """;

        var interpretedOptions = new JsonSchema.Options(
            allowFileSystemAndHttpResolution: false,
            additionalDocumentResolver: Prepopulated(schemaUri, stringSchema));
        var interpreted = JsonSchema.FromUri(schemaUri, interpretedOptions, refreshCache: true);
        Assert.IsTrue(interpreted.Validate("\"hello\""));

        // The same URI with code generation is not the interpreted schema's cache entry: it is resolved and compiled.
        var generatedOptions = new JsonSchema.Options(
            allowFileSystemAndHttpResolution: false,
            additionalDocumentResolver: Prepopulated(schemaUri, intSchema),
            codeGeneration: JsonSchemaCodeGeneration.Eager);
        var generated = JsonSchema.FromUri(schemaUri, generatedOptions, refreshCache: true);
        Assert.IsTrue(generated.Validate("42"));
        Assert.IsFalse(generated.Validate("\"hello\""));

        // Each is found again by its own setting.
        Assert.IsTrue(JsonSchema.FromUri(schemaUri, interpretedOptions).Validate("\"hello\""));
        Assert.IsTrue(JsonSchema.FromUri(schemaUri, generatedOptions).Validate("42"));
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
