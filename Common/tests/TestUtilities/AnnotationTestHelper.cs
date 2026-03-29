// <copyright file="AnnotationTestHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Xunit;

namespace TestUtilities;

/// <summary>
/// Helpers for annotation test assertions.
/// </summary>
public static class AnnotationTestHelper
{
    /// <summary>
    /// Evaluates an instance against a compiled standalone evaluator and asserts
    /// that the expected annotations are produced at the given location and keyword.
    /// </summary>
    /// <param name="evaluator">The compiled evaluator.</param>
    /// <param name="instanceJson">The JSON instance to evaluate.</param>
    /// <param name="location">The instance location (JSON pointer, e.g. "", "/foo", "/0").</param>
    /// <param name="keyword">The annotation keyword name (e.g. "title").</param>
    /// <param name="expectedJson">
    /// The expected annotations as a JSON object mapping schema location to annotation value,
    /// e.g. <c>{"#": "Foo"}</c> or <c>{"#/$defs/foo": "Foo"}</c>.
    /// An empty object <c>{}</c> means no annotations are expected.
    /// </param>
    public static void AssertAnnotations(
        CompiledEvaluator evaluator,
        string instanceJson,
        string location,
        string keyword,
        string expectedJson)
    {
        using ParsedJsonDocument<Corvus.Text.Json.JsonElement> doc =
            ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(instanceJson);
        Corvus.Text.Json.JsonElement instance = doc.RootElement;

        using JsonSchemaResultsCollector collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        evaluator.Evaluate(instance, collector);

        // Write the annotations to a buffer using the Utf8JsonWriter API.
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, default))
        {
            JsonSchemaAnnotationProducer.WriteAnnotationsTo(collector, writer);
        }

        // Parse the produced annotations as a CTJ JsonElement.
        using ParsedJsonDocument<Corvus.Text.Json.JsonElement> producedDoc =
            ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(buffer.ToArray());
        Corvus.Text.Json.JsonElement producedRoot = producedDoc.RootElement;

        // Parse the expected JSON as a CTJ JsonElement.
        using ParsedJsonDocument<Corvus.Text.Json.JsonElement> expectedDoc =
            ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(expectedJson);
        Corvus.Text.Json.JsonElement expectedElement = expectedDoc.RootElement;

        // Navigate to [instanceLocation][keyword] in the produced element.
        bool hasExpectedAnnotations = expectedElement.EnumerateObject().MoveNext();

        if (!hasExpectedAnnotations)
        {
            // Empty expected = no annotations at this location/keyword.
            if (producedRoot.TryGetProperty(location, out Corvus.Text.Json.JsonElement locationObj) &&
                locationObj.TryGetProperty(keyword, out _))
            {
                Assert.Fail(
                    $"Expected no annotations at location={location}, keyword={keyword}, " +
                    $"but annotations were present in the produced output.");
            }

            return;
        }

        // Non-empty expected — annotations should exist at the given location/keyword.
        if (!producedRoot.TryGetProperty(location, out Corvus.Text.Json.JsonElement producedLocationObj))
        {
            Assert.Fail(BuildDiagnostics(
                $"Expected annotations at location={location}, keyword={keyword}, but the instance location was not found in produced output.",
                collector,
                evaluator,
                producedRoot));
        }

        if (!producedLocationObj.TryGetProperty(keyword, out Corvus.Text.Json.JsonElement producedKeywordObj))
        {
            Assert.Fail(BuildDiagnostics(
                $"Expected annotations at location={location}, keyword={keyword}, but the keyword was not found under the instance location.",
                collector,
                evaluator,
                producedRoot));
        }

        // DeepEquals comparison.
        if (!producedKeywordObj.Equals(expectedElement))
        {
            Assert.Fail(BuildDiagnostics(
                $"Annotation mismatch at location={location}, keyword={keyword}.\n" +
                $"Expected: {expectedElement.GetRawText()}\n" +
                $"Actual:   {producedKeywordObj.GetRawText()}",
                collector,
                evaluator,
                producedRoot));
        }
    }

    private static string BuildDiagnostics(
        string message,
        JsonSchemaResultsCollector collector,
        CompiledEvaluator evaluator,
        Corvus.Text.Json.JsonElement producedRoot)
    {
        var diag = new System.Text.StringBuilder();
        diag.AppendLine(message);
        diag.AppendLine($"Produced annotations: {producedRoot.GetRawText()}");

        diag.AppendLine("All collector results:");
        int resultIdx = 0;
        foreach (JsonSchemaResultsCollector.Result r in collector.EnumerateResults())
        {
            diag.AppendLine($"  [{resultIdx}] IsMatch={r.IsMatch}, EvalLoc={r.GetEvaluationLocationText()}, SchemaLoc={r.GetSchemaEvaluationLocationText()}, DocLoc={r.GetDocumentEvaluationLocationText()}, Msg={r.GetMessageText()}");
            resultIdx++;
        }

        if (evaluator.GeneratedCode is not null)
        {
            diag.AppendLine("Generated evaluator code:");
            diag.AppendLine(evaluator.GeneratedCode);
        }

        return diag.ToString();
    }
}