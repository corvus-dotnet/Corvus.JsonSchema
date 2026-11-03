using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using Corvus.Text.Json.RuntimeEvaluator.Tests.Suite;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests;

/// <summary>
/// Runs the JSON-Schema-Test-Suite annotation tests through the verbose results collector.
/// </summary>
[TestClass]
public class AnnotationSuiteTests
{
    public static IEnumerable<object[]> Files()
    {
        string dir = Path.Combine(SuiteRunner.SuiteRoot, "annotations", "tests");
        foreach (string draft in SuiteRunner.Drafts)
        {
            foreach (string file in Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                yield return [draft, Path.GetFileName(file), file];
            }
        }
    }

    public static string DisplayName(System.Reflection.MethodInfo method, object[] data) => $"{data[0]}/{data[1]}";

    [TestMethod]
    [DynamicData(nameof(Files), DynamicDataDisplayName = nameof(DisplayName))]
    public void Annotations(string draft, string name, string file)
    {
        var failures = new StringBuilder();
        int total = 0;
        int failed = 0;
        int release = SuiteRunner.ReleaseFor(draft);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(File.ReadAllBytes(file));
        foreach (JsonElement group in doc.RootElement.GetProperty("suite").EnumerateArray())
        {
            string? compatibility = group.TryGetProperty("compatibility", out JsonElement compat) ? compat.GetString() : null;
            if (!TestUtilities.JsonSchemaTestSuite.SuiteCompatibility.IsCompatible(compatibility, release))
            {
                continue;
            }

            JsonSchemaEvaluatorOptions options = SuiteRunner.OptionsFor(draft, assertFormat: false, SuiteRunner.ResolverFor(group));

            string groupDescription = group.GetProperty("description").GetString()!;
            using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(Encoding.UTF8.GetBytes(group.GetProperty("schema").GetRawText()), options);

            foreach (JsonElement test in group.GetProperty("tests").EnumerateArray())
            {
                string instanceJson = test.GetProperty("instance").GetRawText();
                using ParsedJsonDocument<JsonElement> instanceDoc = ParsedJsonDocument<JsonElement>.Parse(instanceJson);
                using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
                evaluator.Evaluate(instanceDoc.RootElement, collector);

                using var buffer = new MemoryStream();
                using (var writer = new Utf8JsonWriter(buffer, default))
                {
                    JsonSchemaAnnotationProducer.WriteAnnotationsTo(collector, writer);
                }

                using ParsedJsonDocument<JsonElement> produced = ParsedJsonDocument<JsonElement>.Parse(buffer.ToArray());

                foreach (JsonElement assertion in test.GetProperty("assertions").EnumerateArray())
                {
                    total++;
                    string location = assertion.GetProperty("location").GetString()!;
                    string keyword = assertion.GetProperty("keyword").GetString()!;
                    JsonElement expected = assertion.GetProperty("expected");

                    JsonElement? actual = null;
                    if (produced.RootElement.TryGetProperty(location, out JsonElement locationObj) && locationObj.TryGetProperty(keyword, out JsonElement keywordObj))
                    {
                        actual = keywordObj;
                    }

                    bool expectedEmpty = !expected.EnumerateObject().MoveNext();
                    bool ok = expectedEmpty ? actual is null : actual is JsonElement a && a.Equals(expected);
                    if (!ok)
                    {
                        failed++;
                        failures.Append("  [").Append(groupDescription).Append("] instance ").Append(instanceJson)
                            .Append(" location '").Append(location).Append("' keyword '").Append(keyword)
                            .Append("': expected ").Append(expected.GetRawText())
                            .Append(", actual ").Append(actual?.GetRawText() ?? "<none>").AppendLine();
                    }
                }
            }
        }

        if (failed > 0)
        {
            Assert.Fail($"{failed}/{total} annotation assertions failed in {name}:\n{failures}");
        }
    }
}
