using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Suite;

/// <summary>
/// Drives the official JSON-Schema-Test-Suite against the runtime evaluator.
/// </summary>
public static class SuiteRunner
{
    private static readonly ConcurrentDictionary<string, byte[]> RemoteCache = new();

    public static string SuiteRoot { get; } = ResolveSuiteRoot();

    public static string RemotesRoot => Path.Combine(SuiteRoot, "remotes");

    public static readonly string[] Drafts = ["draft4", "draft6", "draft7", "draft2019-09", "draft2020-12"];

    public static JsonSchemaDialect DialectFor(string draft) => draft switch
    {
        "draft4" => JsonSchemaDialect.Draft4,
        "draft6" => JsonSchemaDialect.Draft6,
        "draft7" => JsonSchemaDialect.Draft7,
        "draft2019-09" => JsonSchemaDialect.Draft201909,
        _ => JsonSchemaDialect.Draft202012,
    };

    public static bool ResolveRemote(string uri, out ReadOnlyMemory<byte> utf8)
    {
        const string prefix = "http://localhost:1234/";
        if (!uri.StartsWith(prefix, StringComparison.Ordinal))
        {
            utf8 = default;
            return false;
        }

        string path = Path.Combine(RemotesRoot, uri[prefix.Length..].Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            utf8 = default;
            return false;
        }

        utf8 = RemoteCache.GetOrAdd(path, static p => File.ReadAllBytes(p));
        return true;
    }

    public static JsonSchemaEvaluatorOptions OptionsFor(string draft, bool assertFormat)
    {
        return new JsonSchemaEvaluatorOptions
        {
            DefaultDialect = DialectFor(draft),
            AssertFormat = assertFormat ? true : null,
            DocumentResolver = ResolveRemote,
        };
    }

    public static IEnumerable<string> Files(string draft, string subdirectory = "")
    {
        string dir = Path.Combine(SuiteRoot, "tests", draft, subdirectory);
        if (!Directory.Exists(dir))
        {
            yield break;
        }

        foreach (string file in Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            yield return file;
        }
    }

    public sealed record CaseResult(string Group, string Test, bool Expected, bool? Actual, string? Error)
    {
        public bool Passed => Error is null && Actual == Expected;
    }

    /// <summary>
    /// Runs every group and test in a suite file.
    /// </summary>
    /// <summary>
    /// Runs every case in a suite file.
    /// </summary>
    /// <param name="file">The suite file.</param>
    /// <param name="draft">The draft directory name.</param>
    /// <param name="assertFormat">Whether <c>format</c> is asserted.</param>
    /// <param name="throughImage">When set, each compiled schema is round-tripped through a program image before the
    /// cases are evaluated, so the run exercises <see cref="JsonSchemaEvaluator.FromProgramImage"/>.</param>
    /// <param name="collecting">When set, every case is evaluated with a results collector, which takes the engine's
    /// general path rather than its fused flag-mode plans; the two must agree.</param>
    /// <returns>The case results.</returns>
    public static List<CaseResult> RunFile(string file, string draft, bool assertFormat, bool throughImage = false, bool collecting = false)
    {
        var results = new List<CaseResult>();
        JsonSchemaEvaluatorOptions options = OptionsFor(draft, assertFormat);
        byte[] json = File.ReadAllBytes(file);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        foreach (JsonElement group in doc.RootElement.EnumerateArray())
        {
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
                    results.Add(new CaseResult(groupDescription, testDescription, expected, null, compileError));
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
                    results.Add(new CaseResult(groupDescription, testDescription, expected, actual, null));
                }
                catch (Exception ex)
                {
                    results.Add(new CaseResult(groupDescription, testDescription, expected, null, ex.GetType().Name + ": " + ex.Message));
                }
            }

            evaluator?.Dispose();
        }

        return results;
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
