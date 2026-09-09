using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

[TestClass]
public class ResultsTests
{
    private const string PersonSchema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "title": "Person",
          "properties": {
            "name": {"type": "string", "minLength": 1, "description": "The name"},
            "age": {"type": "integer", "minimum": 0}
          },
          "required": ["name"],
          "additionalProperties": false
        }
        """;

    private static List<(bool IsMatch, string Eval, string Schema, string Document, string Message)> Collect(string schema, string instance, JsonSchemaResultsLevel level, out bool valid)
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(instance);
        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(level);
        valid = evaluator.Evaluate(doc.RootElement, collector);
        var results = new List<(bool, string, string, string, string)>();
        foreach (JsonSchemaResultsCollector.Result r in collector.EnumerateResults())
        {
            results.Add((r.IsMatch, r.GetEvaluationLocationText(), r.GetSchemaEvaluationLocationText(), r.GetDocumentEvaluationLocationText(), r.GetMessageText()));
        }

        return results;
    }

    [TestMethod]
    public void FlagAndCollectingAgree()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(PersonSchema);
        foreach ((string instance, bool expected) in new[]
        {
            ("""{"name": "a", "age": 3}""", true),
            ("""{"name": "", "age": 3}""", false),
            ("""{"age": 3}""", false),
            ("""{"name": "a", "extra": 1}""", false),
            ("""{"name": "a", "age": -1}""", false),
            ("[]", false),
        })
        {
            using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(instance);
            Assert.AreEqual(expected, evaluator.Evaluate(doc.RootElement), instance);
            foreach (JsonSchemaResultsLevel level in new[] { JsonSchemaResultsLevel.Basic, JsonSchemaResultsLevel.Detailed, JsonSchemaResultsLevel.Verbose })
            {
                using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(level);
                Assert.AreEqual(expected, evaluator.Evaluate(doc.RootElement, collector), $"{instance} at {level}");
            }
        }
    }

    [TestMethod]
    public void BasicResultsReportFailingKeywordWithLocations()
    {
        var results = Collect(PersonSchema, """{"name": "", "age": -1}""", JsonSchemaResultsLevel.Basic, out bool valid);
        Assert.IsFalse(valid);
        var failures = results.Where(r => !r.IsMatch).ToList();
        string dump = string.Join("\n", failures);
        Assert.IsTrue(failures.Any(f => f.Eval.EndsWith("/minLength") && f.Document == "/name"), dump);
        Assert.IsTrue(failures.Any(f => f.Eval.EndsWith("/minimum") && f.Document == "/age"), dump);
        Assert.IsTrue(failures.Any(f => f.Schema == "/properties/name"), dump);
    }

    [TestMethod]
    public void VerboseAnnotationsAreProduced()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(PersonSchema);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"name": "a"}""");
        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        Assert.IsTrue(evaluator.Evaluate(doc.RootElement, collector));
        var annotations = JsonSchemaAnnotationProducer.CollectAnnotations(collector);
        Assert.IsTrue(annotations.TryGetValue(("", "title"), out var title), string.Join(",", annotations.Keys));
        Assert.AreEqual("\"Person\"", title["#"]);
        Assert.IsTrue(annotations.TryGetValue(("/name", "description"), out var description));
        Assert.AreEqual("\"The name\"", description["#/properties/name"]);
    }

    [TestMethod]
    public void CollectorIsReusableAcrossEvaluations()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(PersonSchema);
        for (int i = 0; i < 3; i++)
        {
            using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"age": "x"}""");
            using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
            Assert.IsFalse(evaluator.Evaluate(doc.RootElement, collector));
            Assert.IsTrue(collector.GetResultCount() > 0);
        }
    }
}
