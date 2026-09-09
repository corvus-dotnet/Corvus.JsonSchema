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
    private static readonly string[] CompatOrder = ["3", "4", "6", "7", "2019", "2020"];

    private static string DraftCompat(string draft) => draft switch
    {
        "draft4" => "4",
        "draft6" => "6",
        "draft7" => "7",
        "draft2019-09" => "2019",
        _ => "2020",
    };

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
        string draftCompat = DraftCompat(draft);
        JsonSchemaEvaluatorOptions options = SuiteRunner.OptionsFor(draft, assertFormat: false);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(File.ReadAllBytes(file));
        foreach (JsonElement group in doc.RootElement.GetProperty("suite").EnumerateArray())
        {
            if (group.TryGetProperty("compatibility", out JsonElement compat) && !IsCompatible(draftCompat, compat.GetString()!))
            {
                continue;
            }

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

    private static bool IsCompatible(string draftCompat, string suiteCompat)
    {
        int draftLevel = Array.IndexOf(CompatOrder, draftCompat);
        if (suiteCompat.StartsWith("<=", StringComparison.Ordinal))
        {
            int max = Array.IndexOf(CompatOrder, suiteCompat[2..]);
            return max >= 0 && draftLevel <= max;
        }

        int min = Array.IndexOf(CompatOrder, suiteCompat);
        return min >= 0 && draftLevel >= min;
    }
}
