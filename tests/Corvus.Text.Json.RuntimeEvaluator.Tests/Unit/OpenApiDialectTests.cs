// <copyright file="OpenApiDialectTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// The OpenAPI 2.0 and 3.0 Schema Object dialects (draft 4 with every format and <c>nullable</c>), and the formats
/// of later drafts in drafts 4, 6 and 7 for the programs generated models emit.
/// </summary>
[TestClass]
public class OpenApiDialectTests
{
    private const string NotAFormattedValue = "\"not a valid ^ value\"";

    [TestMethod]
    [DataRow(JsonSchemaDialect.OpenApi30, "date", "\"2020-02-29\"")]
    [DataRow(JsonSchemaDialect.OpenApi30, "time", "\"12:34:56Z\"")]
    [DataRow(JsonSchemaDialect.OpenApi30, "date-time", "\"2020-02-29T12:34:56Z\"")]
    [DataRow(JsonSchemaDialect.OpenApi30, "uuid", "\"3fa85f64-5717-4562-b3fc-2c963f66afa6\"")]
    [DataRow(JsonSchemaDialect.OpenApi30, "duration", "\"P1D\"")]
    [DataRow(JsonSchemaDialect.OpenApi30, "uri-reference", "\"/a/b\"")]
    [DataRow(JsonSchemaDialect.OpenApi30, "json-pointer", "\"/a/b\"")]
    [DataRow(JsonSchemaDialect.OpenApi30, "email", "\"a@example.com\"")]
    [DataRow(JsonSchemaDialect.OpenApi20, "date", "\"2020-02-29\"")]
    [DataRow(JsonSchemaDialect.OpenApi20, "uuid", "\"3fa85f64-5717-4562-b3fc-2c963f66afa6\"")]
    public void OpenApiAssertsEveryFormat(JsonSchemaDialect dialect, string format, string validInstance)
    {
        // What a generated program sets: assert format in the drafts that leave it to the implementation.
        var options = new JsonSchemaEvaluatorOptions { DefaultDialect = dialect, AssertFormatInLegacyDrafts = true };
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(FormatSchema(format), options);
        Assert.IsTrue(evaluator.Evaluate(validInstance));
        Assert.IsFalse(evaluator.Evaluate(NotAFormattedValue));
    }

    [TestMethod]
    public void OpenApiFormatIsAnAnnotationUnlessAsserted()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(FormatSchema("uuid"), new JsonSchemaEvaluatorOptions { DefaultDialect = JsonSchemaDialect.OpenApi30 });
        Assert.IsTrue(evaluator.Evaluate(NotAFormattedValue));

        using JsonSchemaEvaluator asserted = JsonSchemaEvaluator.Compile(FormatSchema("uuid"), new JsonSchemaEvaluatorOptions { DefaultDialect = JsonSchemaDialect.OpenApi30, AssertFormat = true });
        Assert.IsFalse(asserted.Evaluate(NotAFormattedValue));
    }

    [TestMethod]
    [DataRow(JsonSchemaDialect.Draft4, "date", "\"2020-02-29\"")]
    [DataRow(JsonSchemaDialect.Draft4, "uuid", "\"3fa85f64-5717-4562-b3fc-2c963f66afa6\"")]
    [DataRow(JsonSchemaDialect.Draft4, "uri-reference", "\"/a/b\"")]
    [DataRow(JsonSchemaDialect.Draft6, "date", "\"2020-02-29\"")]
    [DataRow(JsonSchemaDialect.Draft6, "time", "\"12:34:56Z\"")]
    [DataRow(JsonSchemaDialect.Draft7, "uuid", "\"3fa85f64-5717-4562-b3fc-2c963f66afa6\"")]
    [DataRow(JsonSchemaDialect.Draft7, "duration", "\"P1D\"")]
    public void LegacyDraftsHaveLaterFormatsWhenOptedIn(JsonSchemaDialect dialect, string format, string validInstance)
    {
        var options = new JsonSchemaEvaluatorOptions { DefaultDialect = dialect, AssertFormatInLegacyDrafts = true };
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(FormatSchema(format), options);
        Assert.IsTrue(evaluator.Evaluate(validInstance));
        Assert.IsFalse(evaluator.Evaluate(NotAFormattedValue));
    }

    [TestMethod]
    [DataRow(JsonSchemaDialect.Draft4, "date")]
    [DataRow(JsonSchemaDialect.Draft4, "uuid")]
    [DataRow(JsonSchemaDialect.Draft6, "date")]
    [DataRow(JsonSchemaDialect.Draft7, "uuid")]
    [DataRow(JsonSchemaDialect.Draft7, "duration")]
    public void LegacyDraftsHaveOnlyTheirOwnFormatsOtherwise(JsonSchemaDialect dialect, string format)
    {
        // Asserting format does not add formats: a draft has the ones it defines unless the legacy opt-in is set.
        var options = new JsonSchemaEvaluatorOptions { DefaultDialect = dialect, AssertFormat = true };
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(FormatSchema(format), options);
        Assert.IsTrue(evaluator.Evaluate(NotAFormattedValue));
    }

    [TestMethod]
    [DataRow(JsonSchemaDialect.OpenApi30, """{"type": "string", "nullable": true}""")]
    [DataRow(JsonSchemaDialect.OpenApi30, """{"nullable": true, "type": "string"}""")]
    [DataRow(JsonSchemaDialect.OpenApi20, """{"type": "string", "nullable": true}""")]
    [DataRow(JsonSchemaDialect.OpenApi20, """{"type": "string", "x-nullable": true}""")]
    public void NullableAddsNullToTheType(JsonSchemaDialect dialect, string schema)
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema, new JsonSchemaEvaluatorOptions { DefaultDialect = dialect });
        Assert.IsTrue(evaluator.Evaluate("null"));
        Assert.IsTrue(evaluator.Evaluate("\"text\""));
        Assert.IsFalse(evaluator.Evaluate("1"));
    }

    [TestMethod]
    [DataRow(JsonSchemaDialect.OpenApi30, """{"type": "string"}""")]
    [DataRow(JsonSchemaDialect.OpenApi30, """{"type": "string", "nullable": false}""")]
    [DataRow(JsonSchemaDialect.OpenApi30, """{"type": "string", "x-nullable": true}""")]
    [DataRow(JsonSchemaDialect.Draft4, """{"type": "string", "nullable": true}""")]
    [DataRow(JsonSchemaDialect.Draft4, """{"type": "string", "x-nullable": true}""")]
    [DataRow(JsonSchemaDialect.Draft202012, """{"type": "string", "nullable": true}""")]
    public void NullIsRejectedWithoutNullable(JsonSchemaDialect dialect, string schema)
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema, new JsonSchemaEvaluatorOptions { DefaultDialect = dialect });
        Assert.IsFalse(evaluator.Evaluate("null"));
        Assert.IsTrue(evaluator.Evaluate("\"text\""));
    }

    [TestMethod]
    public void NullableDoesNotOverrideOtherKeywords()
    {
        // OAS 3.0.3: other constraints keep their behaviour, so an enum without null still disallows it.
        var options = new JsonSchemaEvaluatorOptions { DefaultDialect = JsonSchemaDialect.OpenApi30 };
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile("""{"type": "string", "nullable": true, "enum": ["a", "b"]}""", options);
        Assert.IsFalse(evaluator.Evaluate("null"));
        Assert.IsTrue(evaluator.Evaluate("\"a\""));

        using JsonSchemaEvaluator withNull = JsonSchemaEvaluator.Compile("""{"type": "string", "nullable": true, "enum": ["a", null]}""", options);
        Assert.IsTrue(withNull.Evaluate("null"));
    }

    [TestMethod]
    public void NullableAppliesThroughReferencesAndProperties()
    {
        const string Schema = """
            {
              "type": "object",
              "properties": {
                "id": { "type": "string", "format": "uuid" },
                "born": { "$ref": "#/definitions/NullableDate" }
              },
              "definitions": {
                "NullableDate": { "type": "string", "format": "date", "nullable": true }
              }
            }
            """;
        var options = new JsonSchemaEvaluatorOptions { DefaultDialect = JsonSchemaDialect.OpenApi30, AssertFormatInLegacyDrafts = true };
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(Schema, options);
        Assert.IsTrue(evaluator.Evaluate("""{"id": "3fa85f64-5717-4562-b3fc-2c963f66afa6", "born": null}"""));
        Assert.IsTrue(evaluator.Evaluate("""{"id": "3fa85f64-5717-4562-b3fc-2c963f66afa6", "born": "2020-02-29"}"""));
        Assert.IsFalse(evaluator.Evaluate("""{"id": "nope", "born": null}"""));
        Assert.IsFalse(evaluator.Evaluate("""{"id": "3fa85f64-5717-4562-b3fc-2c963f66afa6", "born": "yesterday"}"""));
        Assert.IsFalse(evaluator.Evaluate("""{"id": null}"""));
    }

    [TestMethod]
    public void OpenApiKeepsDraft4Rules()
    {
        // The boolean exclusiveMinimum of draft 4, and a $ref that hides its siblings.
        var options = new JsonSchemaEvaluatorOptions { DefaultDialect = JsonSchemaDialect.OpenApi30 };
        using JsonSchemaEvaluator bounds = JsonSchemaEvaluator.Compile("""{"type": "number", "minimum": 1, "exclusiveMinimum": true}""", options);
        Assert.IsFalse(bounds.Evaluate("1"));
        Assert.IsTrue(bounds.Evaluate("2"));

        using JsonSchemaEvaluator reference = JsonSchemaEvaluator.Compile("""{"definitions": {"S": {"type": "string"}}, "$ref": "#/definitions/S", "maxLength": 1}""", options);
        Assert.IsTrue(reference.Evaluate("\"longer\""));
    }

    [TestMethod]
    public void NullableSurvivesAProgramImage()
    {
        var options = new JsonSchemaEvaluatorOptions { DefaultDialect = JsonSchemaDialect.OpenApi30, AssertFormatInLegacyDrafts = true };
        byte[] image;
        using (JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile("""{"type": "string", "format": "uuid", "nullable": true}""", options))
        {
            image = evaluator.ToProgramImage();
        }

        using JsonSchemaEvaluator loaded = JsonSchemaEvaluator.FromProgramImage(image);
        Assert.IsTrue(loaded.Evaluate("null"));
        Assert.IsTrue(loaded.Evaluate("\"3fa85f64-5717-4562-b3fc-2c963f66afa6\""));
        Assert.IsFalse(loaded.Evaluate(NotAFormattedValue));
    }

    private static string FormatSchema(string format) => "{\"type\": \"string\", \"format\": \"" + format + "\"}";
}