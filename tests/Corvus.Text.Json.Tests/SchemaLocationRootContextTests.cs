// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Issue #957. A generated type's schema location is pushed when the root evaluation context begins,
/// so a result reported against the root schema carries the same root-document pointer as a result
/// reported against the same schema reached through a property. Every text accessor for a schema
/// location is a plain JSON Pointer; only the annotation JSON output uses the '#' fragment form.
/// </summary>
[TestClass]
public class SchemaLocationRootContextTests
{
    private const string FooIdSchemaLocation = "/$defs/fooId";

    private const string Schema =
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$defs": {
            "fooId": {
              "title": "Foo identifier",
              "type": "integer",
              "minimum": 0
            }
          }
        }
        """;

    private static CompiledEvaluator s_fooIdEvaluator = null!;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        string repoRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", ".."));
        string remotes = Path.Combine(repoRoot, "JSON-Schema-Test-Suite", "remotes");

        // The evaluator is generated for the schema at #/$defs/fooId without rebasing it as the root
        // of its document. The evaluator generator wraps such a root in a $ref to the target schema,
        // so results for the target carry its root-document pointer "/$defs/fooId".
        s_fooIdEvaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
            "issue957/schema-location.json#/$defs/fooId",
            Schema,
            "Corvus.Text.Json.Tests.Issue957.FooId",
            remotes,
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: false,
            formatModeOverrides: null,
            Assembly.GetExecutingAssembly(),
            rebaseAsRoot: false);
    }

    [TestMethod]
    public void GeneratedType_SchemaLocation_IsRootDocumentPointer()
    {
        Assert.AreEqual(FooIdSchemaLocation, Issue957FooId.JsonSchema.SchemaLocation);
        Assert.AreEqual("/$defs/holder", Issue957Holder.JsonSchema.SchemaLocation);
        Assert.AreEqual("/$defs/sub/properties/x", Issue957SubResourceX.JsonSchema.SchemaLocation);
    }

    [TestMethod]
    public void GeneratedType_SchemaDocument_IsTheRootDocumentThePointerIsRelativeTo()
    {
        // The schema at #/$defs/sub is a sub-resource with its own $id. Its types still report the
        // file that contains the sub-resource, so SchemaDocument + "#" + SchemaLocation locates them.
        Assert.AreEqual("issue957-schema-location-2020-12.json", Issue957FooId.JsonSchema.SchemaDocument);
        Assert.AreEqual("issue957-schema-location-2020-12.json", Issue957Holder.JsonSchema.SchemaDocument);
        Assert.AreEqual("issue957-schema-location-2020-12.json", Issue957SubResourceX.JsonSchema.SchemaDocument);
        Assert.AreEqual("issue957-schema-location-2020-12.json#/$defs/sub/properties/x", Issue957SubResourceX.JsonSchema.SchemaDocument + "#" + Issue957SubResourceX.JsonSchema.SchemaLocation);
        Assert.IsTrue(Issue957SubResourceX.JsonSchema.SchemaDocumentUtf8.SequenceEqual("issue957-schema-location-2020-12.json"u8));
    }

    [TestMethod]
    public void SubResourceFailure_ReportsRootDocumentPointerOfTheFailingKeyword()
    {
        using var doc = ParsedJsonDocument<Issue957SubResourceX>.Parse("\"\"");
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);

        Assert.IsFalse(doc.RootElement.EvaluateSchema(collector));

        AssertEqualLines(
            """
            fail|/$defs/sub/properties/x|||The value was expected to match the subschema.
            fail|/$defs/sub/properties/x/minLength|/minLength||Expected the length of the value to be greater than or equal to '1'
            """,
            Dump(collector));
    }

    [TestMethod]
    public void RootTypeFailure_ReportsRootDocumentPointerOfTheFailingKeyword()
    {
        using var doc = ParsedJsonDocument<Issue957FooId>.Parse("\"notAnInteger\"");
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);

        Assert.IsFalse(doc.RootElement.EvaluateSchema(collector));

        AssertEqualLines(
            """
            fail|/$defs/fooId|||The value was expected to match the subschema.
            fail|/$defs/fooId/type|/type||The value was expected to be of type 'integer'
            """,
            Dump(collector));
    }

    [TestMethod]
    public void PropertyFailure_ReportsTheSameRootDocumentPointer()
    {
        using var doc = ParsedJsonDocument<Issue957Holder>.Parse("""{ "fooId": "notAnInteger" }""");
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);

        Assert.IsFalse(doc.RootElement.EvaluateSchema(collector));

        AssertEqualLines(
            """
            fail|/$defs/fooId|/properties/fooId/$ref|/fooId|The value was expected to match the subschema.
            fail|/$defs/fooId/type|/properties/fooId/$ref/type|/fooId|The value was expected to be of type 'integer'
            fail|/$defs/holder|||The value was expected to match the subschema.
            """,
            Dump(collector));
    }

    [TestMethod]
    public void SourceGeneratedEvaluator_RootTypeFailure_ReportsRootDocumentPointerOfTheFailingKeyword()
    {
        // The source generator wraps a fragment-rooted evaluator in a $ref to the target schema,
        // so the failing keyword is reached through that $ref and still carries the root-document pointer.
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"notAnInteger\"");
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);

        Assert.IsFalse(Issue957FooIdEvaluator.Evaluate(doc.RootElement, collector));

        AssertEqualLines(
            """
            fail|/$defs/fooId|/$ref/0||The value was expected to match the subschema.
            fail|/$defs/fooId/type|/$ref/0/type||The value was expected to be of type 'integer'
            fail||||The value was expected to match the subschema.
            fail|/$ref|/$ref||The value did not match all subschema.
            """,
            Dump(collector));
    }

    [TestMethod]
    public void StandaloneEvaluator_RootTypeFailure_ReportsRootDocumentPointerOfTheFailingKeyword()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"notAnInteger\"");
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);

        Assert.IsFalse(s_fooIdEvaluator.Evaluate(doc.RootElement, collector));

        AssertEqualLines(
            """
            fail|/$defs/fooId|/$ref/0||The value was expected to match the subschema.
            fail|/$defs/fooId/type|/$ref/0/type||The value was expected to be of type 'integer'
            fail||||The value was expected to match the subschema.
            fail|/$ref|/$ref||The value did not match all subschema.
            """,
            Dump(collector));
    }

    [TestMethod]
    public void AnnotationSchemaLocationText_IsTheSameJsonPointerAsTheResultsCollectorText()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("42");
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);

        Assert.IsTrue(s_fooIdEvaluator.Evaluate(doc.RootElement, collector));

        StringBuilder text = new();
        text.Append(Dump(collector)).Append('\n');

        foreach (JsonSchemaAnnotationProducer.Annotation annotation in JsonSchemaAnnotationProducer.EnumerateAnnotations(collector))
        {
            text.Append("annotation|").Append(annotation.GetKeywordText()).Append('|').Append(annotation.GetSchemaLocationText()).Append('|').Append(annotation.GetSchemaLocationFragmentText()).Append('\n');
        }

        JsonSchemaAnnotationProducer.EnumerateAnnotations(collector, (instanceLocation, keyword, schemaLocation, value) =>
        {
            text.Append("callback|").Append(keyword).Append('|').Append(schemaLocation).Append('\n');
            return true;
        });

        foreach (KeyValuePair<(string InstanceLocation, string Keyword), Dictionary<string, string>> entry in JsonSchemaAnnotationProducer.CollectAnnotations(collector))
        {
            foreach (KeyValuePair<string, string> schemaEntry in entry.Value)
            {
                text.Append("collected|").Append(entry.Key.Keyword).Append('|').Append(schemaEntry.Key).Append('|').Append(schemaEntry.Value).Append('\n');
            }
        }

        AssertEqualLines(
            """
            match|/$defs/fooId|/$ref/0||The value was expected to match the subschema.
            match|/$defs/fooId|/$ref/0/title||"Foo identifier"
            match|/$defs/fooId/minimum|/$ref/0/minimum||The value was expected to be greater than or equal to '0'
            match|/$defs/fooId/type|/$ref/0/type||The value was expected to be of type 'integer'
            match||||The value was expected to match the subschema.
            match|/$ref|/$ref||The value matched all subschema.
            annotation|title|/$defs/fooId|#/$defs/fooId
            callback|title|/$defs/fooId
            collected|title|#/$defs/fooId|"Foo identifier"
            """,
            text.ToString().TrimEnd('\n'));
    }

    /// <summary>
    /// Compares line-oriented text. The expected value is a multi-line raw string literal, whose line breaks
    /// follow the source file's line endings, so they are normalized before comparison.
    /// </summary>
    private static void AssertEqualLines(string expected, string actual)
    {
        Assert.AreEqual(expected.Replace("\r\n", "\n"), actual);
    }

    /// <summary>
    /// Renders every result as <c>match|schemaLocation|evaluationLocation|documentLocation|message</c>, one per line.
    /// </summary>
    private static string Dump(JsonSchemaResultsCollector collector)
    {
        StringBuilder builder = new();
        foreach (JsonSchemaResultsCollector.Result result in collector.EnumerateResults())
        {
            builder
                .Append(result.IsMatch ? "match" : "fail").Append('|')
                .Append(result.GetSchemaEvaluationLocationText()).Append('|')
                .Append(result.GetEvaluationLocationText()).Append('|')
                .Append(result.GetDocumentEvaluationLocationText()).Append('|')
                .Append(result.GetMessageText()).Append('\n');
        }

        return builder.ToString().TrimEnd('\n');
    }
}