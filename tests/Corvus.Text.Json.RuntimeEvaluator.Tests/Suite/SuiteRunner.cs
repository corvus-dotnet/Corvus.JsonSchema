using System.Reflection;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using TestUtilities.JsonSchemaTestSuite;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Suite;

/// <summary>
/// Drives the official JSON-Schema-Test-Suite against the runtime evaluator.
/// </summary>
/// <remarks>
/// The suite keeps every dialect in one tree (<c>validation/tests</c>). Each test case says which releases it
/// applies to in its <c>compatibility</c> property, whether it expects <c>format</c> to be asserted in its
/// <c>tags</c>, and carries any other schemas it references in <c>externalSchemas</c>, which are scoped to that
/// test case.
/// </remarks>
public static class SuiteRunner
{
    public static string SuiteRoot { get; } = ResolveSuiteRoot();

    public static string TestsRoot => Path.Combine(SuiteRoot, "validation", "tests");

    public static readonly string[] Drafts = ["draft4", "draft6", "draft7", "draft2019-09", "draft2020-12"];

    public static JsonSchemaDialect DialectFor(string draft) => draft switch
    {
        "draft4" => JsonSchemaDialect.Draft4,
        "draft6" => JsonSchemaDialect.Draft6,
        "draft7" => JsonSchemaDialect.Draft7,
        "draft2019-09" => JsonSchemaDialect.Draft201909,
        _ => JsonSchemaDialect.Draft202012,
    };

    /// <summary>
    /// Gets the release number the suite's <c>compatibility</c> property uses for a draft.
    /// </summary>
    public static int ReleaseFor(string draft) => draft switch
    {
        "draft4" => 4,
        "draft6" => 6,
        "draft7" => 7,
        "draft2019-09" => 2019,
        _ => 2020,
    };

    /// <summary>
    /// Gets a value indicating whether the engine asserts <c>format</c> by default in the draft (draft4 to draft7,
    /// which the specification allows). A <c>format-annotation</c> test case does not apply there.
    /// </summary>
    public static bool AssertsFormatByDefault(string draft) => ReleaseFor(draft) <= 7;

    public static JsonSchemaEvaluatorOptions OptionsFor(string draft, bool assertFormat, JsonSchemaDocumentResolver? resolver = null)
    {
        return new JsonSchemaEvaluatorOptions
        {
            DefaultDialect = DialectFor(draft),
            AssertFormat = assertFormat ? true : null,
            DocumentResolver = resolver,
        };
    }

    /// <summary>
    /// Builds a resolver serving a test case's <c>externalSchemas</c> at their retrieval URIs.
    /// </summary>
    /// <param name="testCase">The test case.</param>
    /// <returns>The resolver, or <see langword="null"/> if the test case has no external schemas.</returns>
    public static JsonSchemaDocumentResolver? ResolverFor(JsonElement testCase)
    {
        if (!testCase.TryGetProperty("externalSchemas", out JsonElement externalSchemas))
        {
            return null;
        }

        var documents = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (JsonProperty<JsonElement> property in externalSchemas.EnumerateObject())
        {
            documents[property.Name] = Encoding.UTF8.GetBytes(property.Value.GetRawText());
        }

        return (string uri, out ReadOnlyMemory<byte> utf8) =>
        {
            if (documents.TryGetValue(uri, out byte[]? document))
            {
                utf8 = document;
                return true;
            }

            utf8 = default;
            return false;
        };
    }

    /// <summary>
    /// Gets the suite files, as paths relative to <see cref="TestsRoot"/> with forward slashes.
    /// </summary>
    /// <param name="optional">Whether to list the files under <c>optional/</c> rather than the required ones.</param>
    public static IEnumerable<string> Files(bool optional)
    {
        foreach (string file in Directory.GetFiles(TestsRoot, "*.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            string relative = file.Substring(TestsRoot.Length + 1).Replace('\\', '/');
            if (relative.StartsWith("proposals/", StringComparison.Ordinal))
            {
                continue;
            }

            if (relative.StartsWith("optional/", StringComparison.Ordinal) == optional)
            {
                yield return relative;
            }
        }
    }

    public sealed record CaseResult(string Group, string Test, bool Expected, bool? Actual, string? Error, bool AssertedFormat)
    {
        public bool Passed => Error is null && Actual == Expected;
    }

    /// <summary>
    /// Runs every test case in a suite file that applies to the draft.
    /// </summary>
    /// <param name="relativePath">The suite file, relative to <see cref="TestsRoot"/>.</param>
    /// <param name="draft">The draft name.</param>
    /// <param name="throughImage">When set, each compiled schema is round-tripped through a program image before the
    /// cases are evaluated, so the run exercises <see cref="JsonSchemaEvaluator.FromProgramImage"/>.</param>
    /// <param name="collecting">When set, every case is evaluated with a results collector, which takes the engine's
    /// general path rather than its fused flag-mode plans; the two must agree.</param>
    /// <returns>The case results.</returns>
    public static List<CaseResult> RunFile(string relativePath, string draft, bool throughImage = false, bool collecting = false)
    {
        var results = new List<CaseResult>();
        int release = ReleaseFor(draft);
        byte[] json = File.ReadAllBytes(Path.Combine(TestsRoot, relativePath));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        foreach (JsonElement group in doc.RootElement.GetProperty("tests").EnumerateArray())
        {
            string? compatibility = group.TryGetProperty("compatibility", out JsonElement compatibilityElement)
                ? compatibilityElement.GetString()
                : null;
            if (!SuiteCompatibility.IsCompatible(compatibility, release))
            {
                continue;
            }

            bool assertFormat = HasTag(group, "format-assertion");
            if (HasTag(group, "format-annotation") && AssertsFormatByDefault(draft))
            {
                continue;
            }

            JsonSchemaEvaluatorOptions options = OptionsFor(draft, assertFormat, ResolverFor(group));
            string groupDescription = group.GetProperty("description").GetString()!;
            JsonElement schema = group.GetProperty("schema");
            byte[] schemaBytes = Encoding.UTF8.GetBytes(schema.GetRawText());
            JsonSchemaEvaluator? evaluator = null;
            string? compileError = null;
            try
            {
                evaluator = JsonSchemaEvaluator.Compile(schemaBytes, options);
                if (throughImage)
                {
                    byte[] image = evaluator.ToProgramImage();
                    evaluator.Dispose();
                    evaluator = JsonSchemaEvaluator.FromProgramImage(image, options);
                }
            }
            catch (Exception ex)
            {
                compileError = ex.GetType().Name + ": " + ex.Message;
            }

            foreach (JsonElement test in group.GetProperty("tests").EnumerateArray())
            {
                string testDescription = test.GetProperty("description").GetString()!;
                bool expected = test.GetProperty("valid").GetBoolean();
                if (evaluator is null)
                {
                    results.Add(new CaseResult(groupDescription, testDescription, expected, null, compileError, assertFormat));
                    continue;
                }

                try
                {
                    JsonElement instance = test.GetProperty("data");
                    bool actual;
                    if (collecting)
                    {
                        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);
                        actual = evaluator.Evaluate(instance, collector);
                    }
                    else
                    {
                        actual = evaluator.Evaluate(instance);
                    }

                    results.Add(new CaseResult(groupDescription, testDescription, expected, actual, null, assertFormat));
                }
                catch (Exception ex)
                {
                    results.Add(new CaseResult(groupDescription, testDescription, expected, null, ex.GetType().Name + ": " + ex.Message, assertFormat));
                }
            }

            evaluator?.Dispose();
        }

        return results;
    }

    /// <summary>
    /// Gets the failures in a file's results, leaving out the leap-second cases that run with <c>format</c>
    /// asserted: the shared date/time parsers do not support leap seconds (as in Corvus.JsonSchema).
    /// </summary>
    /// <param name="results">The case results.</param>
    /// <returns>The failures.</returns>
    public static List<CaseResult> Failures(IEnumerable<CaseResult> results)
    {
        return results
            .Where(r => !r.Passed)
            .Where(r => !(r.AssertedFormat && r.Test.Contains("leap second", StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public static string Describe(IEnumerable<CaseResult> failures)
    {
        var sb = new StringBuilder();
        foreach (CaseResult f in failures)
        {
            sb.Append("  [").Append(f.Group).Append("] ").Append(f.Test)
              .Append(" => expected ").Append(f.Expected)
              .Append(", actual ").Append(f.Actual?.ToString() ?? "n/a");
            if (f.Error is not null)
            {
                sb.Append(" (").Append(f.Error).Append(')');
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static bool HasTag(JsonElement group, string tag)
    {
        if (!group.TryGetProperty("tags", out JsonElement tags))
        {
            return false;
        }

        foreach (JsonElement t in tags.EnumerateArray())
        {
            if (t.GetString() == tag)
            {
                return true;
            }
        }

        return false;
    }

    private static string ResolveSuiteRoot()
    {
        string? configured = typeof(SuiteRunner).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "JsonSchemaTestSuiteRoot")?.Value;
        if (!string.IsNullOrEmpty(configured) && Directory.Exists(configured))
        {
            return Path.GetFullPath(configured);
        }

        // Fall back to walking up from the test directory.
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "..", "Corvus.JsonSchema", "JSON-Schema-Test-Suite");
            if (Directory.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("JSON-Schema-Test-Suite not found.");
    }
}