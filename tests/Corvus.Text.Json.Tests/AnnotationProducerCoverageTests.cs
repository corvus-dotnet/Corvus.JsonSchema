// <copyright file="AnnotationProducerCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for <see cref="JsonSchemaAnnotationProducer"/> targeting
/// uncovered code paths identified from Cobertura coverage reports.
/// </summary>
/// <remarks>
/// <para>Covered paths and their line references:</para>
/// <list type="bullet">
/// <item>Callback-based <c>EnumerateAnnotations(collector, callback)</c> overload (lines 83-96)</item>
/// <item><c>Annotation</c> span properties: InstanceLocation, Keyword, SchemaLocation, Value (lines 288-303)</item>
/// <item><c>Annotation.WriteValueTo</c> (lines 310-312)</item>
/// <item><c>Annotation.WriteSchemaLocationPropertyTo</c> with stackalloc/ArrayPool pattern (lines 320-344)</item>
/// <item><c>AnnotationEnumerator.MoveNext</c> filter: <c>!result.IsMatch</c> (lines 421-422)</item>
/// </list>
/// <para>
/// Dead code: <c>IsAnnotation</c> (lines 209-249) is an internal static method with zero callers.
/// Its filtering logic is duplicated inline in <c>AnnotationEnumerator.MoveNext()</c>.
/// </para>
/// <para>
/// Defensive guards not covered: empty message (lines 427-428) and empty keyword (lines 446-447)
/// require the generated evaluator to emit results with zero-length spans, which does not occur
/// in normal evaluation. These are structural impossibilities in generated code, not testable paths.
/// </para>
/// </remarks>
[Trait("AnnotationTestSuite", "CoverageTests")]
public class AnnotationProducerCoverageTests : IClassFixture<AnnotationProducerCoverageTests.Fixture>
{
    private readonly Fixture _fixture;

    public AnnotationProducerCoverageTests(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void CallbackOverload_InvokesForEachAnnotation()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("42");
        using JsonSchemaResultsCollector collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        _fixture.MultiAnnotationEvaluator.Evaluate(doc.RootElement, collector);

        var annotations = new List<(string Location, string Keyword, string SchemaLocation, string Value)>();
        JsonSchemaAnnotationProducer.EnumerateAnnotations(
            collector,
            (location, keyword, schemaLocation, value) =>
            {
                annotations.Add((location, keyword, schemaLocation, value));
                return true;
            });

        Assert.True(annotations.Count >= 2, $"Expected at least 2 annotations, got {annotations.Count}");
        Assert.Contains(annotations, a => a.Keyword == "title" && a.Value == "\"Foo\"");
        Assert.Contains(annotations, a => a.Keyword == "default");
    }

    [Fact]
    public void CallbackOverload_StopsWhenCallbackReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("42");
        using JsonSchemaResultsCollector collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        _fixture.MultiAnnotationEvaluator.Evaluate(doc.RootElement, collector);

        // Verify there are at least 2 annotations so stopping is meaningful.
        int totalCount = 0;
        foreach (JsonSchemaAnnotationProducer.Annotation _ in
            JsonSchemaAnnotationProducer.EnumerateAnnotations(collector))
        {
            totalCount++;
        }

        Assert.True(totalCount >= 2, $"Expected at least 2 annotations, got {totalCount}");

        // Callback returns false on first invocation — should stop after 1.
        int callbackCount = 0;
        JsonSchemaAnnotationProducer.EnumerateAnnotations(
            collector,
            (_, _, _, _) =>
            {
                callbackCount++;
                return false;
            });

        Assert.Equal(1, callbackCount);
    }

    [Fact]
    public void SpanProperties_ReturnCorrectUtf8Values()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("42");
        using JsonSchemaResultsCollector collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        _fixture.MultiAnnotationEvaluator.Evaluate(doc.RootElement, collector);

        bool found = false;
        foreach (JsonSchemaAnnotationProducer.Annotation annotation in
            JsonSchemaAnnotationProducer.EnumerateAnnotations(collector))
        {
            if (annotation.Keyword.SequenceEqual("title"u8))
            {
                found = true;
                Assert.True(annotation.InstanceLocation.IsEmpty, "Root instance location should be empty");
                Assert.True(annotation.Keyword.SequenceEqual("title"u8));
                Assert.True(annotation.SchemaLocation.IsEmpty, "Root schema location should be empty");
                Assert.True(annotation.Value.SequenceEqual("\"Foo\""u8));
                break;
            }
        }

        Assert.True(found, "Expected to find 'title' annotation");
    }

    [Fact]
    public void WriteValueTo_WritesRawJsonValue()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("42");
        using JsonSchemaResultsCollector collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        _fixture.MultiAnnotationEvaluator.Evaluate(doc.RootElement, collector);

        bool found = false;
        foreach (JsonSchemaAnnotationProducer.Annotation annotation in
            JsonSchemaAnnotationProducer.EnumerateAnnotations(collector))
        {
            if (annotation.Keyword.SequenceEqual("default"u8))
            {
                found = true;
                using var buffer = new MemoryStream();
                using (var writer = new Utf8JsonWriter(buffer))
                {
                    annotation.WriteValueTo(writer);
                }

                using ParsedJsonDocument<JsonElement> resultDoc =
                    ParsedJsonDocument<JsonElement>.Parse(buffer.ToArray());
                JsonElement resultRoot = resultDoc.RootElement;
                Assert.True(resultRoot.TryGetProperty("x", out JsonElement xProp));
                Assert.Equal(1, xProp.GetInt32());
                break;
            }
        }

        Assert.True(found, "Expected to find 'default' annotation");
    }

    [Fact]
    public void WriteSchemaLocationPropertyTo_WritesPropertyWithHashPrefix()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("42");
        using JsonSchemaResultsCollector collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        _fixture.MultiAnnotationEvaluator.Evaluate(doc.RootElement, collector);

        bool found = false;
        foreach (JsonSchemaAnnotationProducer.Annotation annotation in
            JsonSchemaAnnotationProducer.EnumerateAnnotations(collector))
        {
            if (annotation.Keyword.SequenceEqual("title"u8))
            {
                found = true;
                using var buffer = new MemoryStream();
                using (var writer = new Utf8JsonWriter(buffer))
                {
                    writer.WriteStartObject();
                    annotation.WriteSchemaLocationPropertyTo(writer);
                    writer.WriteEndObject();
                }

                using ParsedJsonDocument<JsonElement> resultDoc =
                    ParsedJsonDocument<JsonElement>.Parse(buffer.ToArray());
                JsonElement resultRoot = resultDoc.RootElement;
                Assert.True(resultRoot.TryGetProperty("#", out JsonElement value));
                Assert.Equal("Foo", value.GetString());
                break;
            }
        }

        Assert.True(found, "Expected to find 'title' annotation");
    }

    [Fact]
    public void WriteSchemaLocationPropertyTo_WithNestedSchema_WritesFullPath()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("42");
        using JsonSchemaResultsCollector collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        _fixture.NestedSchemaEvaluator.Evaluate(doc.RootElement, collector);

        bool found = false;
        foreach (JsonSchemaAnnotationProducer.Annotation annotation in
            JsonSchemaAnnotationProducer.EnumerateAnnotations(collector))
        {
            if (annotation.Keyword.SequenceEqual("title"u8))
            {
                found = true;

                // The schema location for allOf[0].title should contain a path component.
                Assert.False(
                    annotation.SchemaLocation.IsEmpty,
                    "Nested schema annotation should have a non-empty schema location");

                using var buffer = new MemoryStream();
                using (var writer = new Utf8JsonWriter(buffer))
                {
                    writer.WriteStartObject();
                    annotation.WriteSchemaLocationPropertyTo(writer);
                    writer.WriteEndObject();
                }

                // Verify the property name contains the '#' prefix with a path.
                string json = Encoding.UTF8.GetString(buffer.ToArray());
                Assert.Contains("#/", json);
                break;
            }
        }

        Assert.True(found, "Expected to find 'title' annotation in allOf schema");
    }

    [Fact]
    public void EnumerateAnnotations_WithInvalidInstance_SkipsNonMatchResults()
    {
        // Schema has type:integer + title. Evaluating a string triggers !IsMatch for
        // the type check (line 421), but the title annotation should still be present.
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse("\"not a number\"");
        using JsonSchemaResultsCollector collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        _fixture.TypeValidatingEvaluator.Evaluate(doc.RootElement, collector);

        var keywords = new List<string>();
        foreach (JsonSchemaAnnotationProducer.Annotation annotation in
            JsonSchemaAnnotationProducer.EnumerateAnnotations(collector))
        {
            keywords.Add(annotation.GetKeywordText());
        }

        Assert.Contains("title", keywords);
    }

    /// <summary>
    /// Compiles evaluators for different schema patterns used by the coverage tests.
    /// </summary>
    public class Fixture : IAsyncLifetime
    {
        /// <summary>
        /// Gets a compiled evaluator for a schema producing two annotations (title + default).
        /// </summary>
        public CompiledEvaluator MultiAnnotationEvaluator { get; private set; } = null!;

        /// <summary>
        /// Gets a compiled evaluator for a schema with type validation and an annotation.
        /// </summary>
        public CompiledEvaluator TypeValidatingEvaluator { get; private set; } = null!;

        /// <summary>
        /// Gets a compiled evaluator for a schema with a nested annotation via allOf.
        /// </summary>
        public CompiledEvaluator NestedSchemaEvaluator { get; private set; } = null!;

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            string repoRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", ".."));
            string remotes = Path.Combine(repoRoot, "JSON-Schema-Test-Suite", "remotes");

            this.MultiAnnotationEvaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "coverage/multi-annotation.json",
                """{"title":"Foo","default":{"x":1}}""",
                "Corvus.Text.Json.Tests.Coverage.MultiAnnotation",
                remotes,
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());

            this.TypeValidatingEvaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "coverage/type-validating.json",
                """{"type":"integer","title":"Foo"}""",
                "Corvus.Text.Json.Tests.Coverage.TypeValidating",
                remotes,
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());

            this.NestedSchemaEvaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "coverage/nested-annotation.json",
                """{"allOf":[{"title":"Foo"}]}""",
                "Corvus.Text.Json.Tests.Coverage.NestedAnnotation",
                remotes,
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
