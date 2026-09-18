using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta;

/// <summary>Correctness cross-check: runs every Sourcemeta instance through both evaluators.</summary>
public static class SourceMetaDiff
{
    private static readonly (string File, Func<string, List<(int Index, bool Generated, bool Runtime)>> Diff)[] Cases =
    [
        ("ansible-meta", AnsibleMetaBenchmark.Diff),
        ("aws-cdk", AwsCdkBenchmark.Diff),
        ("babelrc", BabelrcBenchmark.Diff),
        ("clang-format", ClangFormatBenchmark.Diff),
        ("cmake-presets", CmakePresetsBenchmark.Diff),
        ("code-climate", CodeClimateBenchmark.Diff),
        ("cql2", Cql2Benchmark.Diff),
        ("cspell", CspellBenchmark.Diff),
        ("cypress", CypressBenchmark.Diff),
        ("deno", DenoBenchmark.Diff),
        ("dependabot", DependabotBenchmark.Diff),
        ("draft-04", Draft04Benchmark.Diff),
        ("fabric-mod", FabricModBenchmark.Diff),
        ("geojson", GeoJsonBenchmark.Diff),
        ("gitpod-configuration", GitpodConfigurationBenchmark.Diff),
        ("helm-chart-lock", HelmChartLockBenchmark.Diff),
        ("importmap", ImportmapBenchmark.Diff),
        ("jasmine", JasmineBenchmark.Diff),
        ("jsconfig", JsconfigBenchmark.Diff),
        ("jshintrc", JshintrcBenchmark.Diff),
        ("krakend", KrakendBenchmark.Diff),
        ("lazygit", LazygitBenchmark.Diff),
        ("lerna", LernaBenchmark.Diff),
        ("nest-cli", NestCliBenchmark.Diff),
        ("omnisharp", OmnisharpBenchmark.Diff),
        ("openapi", OpenapiBenchmark.Diff),
        ("pre-commit-hooks", PreCommitHooksBenchmark.Diff),
        ("pulumi", PulumiBenchmark.Diff),
        ("semantic-release", SemanticReleaseBenchmark.Diff),
        ("stale", StaleBenchmark.Diff),
        ("stylecop", StylecopBenchmark.Diff),
        ("tmuxinator", TmuxinatorBenchmark.Diff),
        ("ui5", Ui5Benchmark.Diff),
        ("ui5-manifest", Ui5ManifestBenchmark.Diff),
        ("unreal-engine-uproject", UnrealEngineUprojectBenchmark.Diff),
        ("vercel", VercelBenchmark.Diff),
        ("yamllint", YamllintBenchmark.Diff),
    ];

    private static readonly (string File, Func<string, int, (double GeneratedNs, double RuntimeNs)> Quick)[] QuickCases =
    [
        ("ansible-meta", AnsibleMetaBenchmark.Quick),
        ("aws-cdk", AwsCdkBenchmark.Quick),
        ("babelrc", BabelrcBenchmark.Quick),
        ("clang-format", ClangFormatBenchmark.Quick),
        ("cmake-presets", CmakePresetsBenchmark.Quick),
        ("code-climate", CodeClimateBenchmark.Quick),
        ("cql2", Cql2Benchmark.Quick),
        ("cspell", CspellBenchmark.Quick),
        ("cypress", CypressBenchmark.Quick),
        ("deno", DenoBenchmark.Quick),
        ("dependabot", DependabotBenchmark.Quick),
        ("draft-04", Draft04Benchmark.Quick),
        ("fabric-mod", FabricModBenchmark.Quick),
        ("geojson", GeoJsonBenchmark.Quick),
        ("gitpod-configuration", GitpodConfigurationBenchmark.Quick),
        ("helm-chart-lock", HelmChartLockBenchmark.Quick),
        ("importmap", ImportmapBenchmark.Quick),
        ("jasmine", JasmineBenchmark.Quick),
        ("jsconfig", JsconfigBenchmark.Quick),
        ("jshintrc", JshintrcBenchmark.Quick),
        ("krakend", KrakendBenchmark.Quick),
        ("lazygit", LazygitBenchmark.Quick),
        ("lerna", LernaBenchmark.Quick),
        ("nest-cli", NestCliBenchmark.Quick),
        ("omnisharp", OmnisharpBenchmark.Quick),
        ("openapi", OpenapiBenchmark.Quick),
        ("pre-commit-hooks", PreCommitHooksBenchmark.Quick),
        ("pulumi", PulumiBenchmark.Quick),
        ("semantic-release", SemanticReleaseBenchmark.Quick),
        ("stale", StaleBenchmark.Quick),
        ("stylecop", StylecopBenchmark.Quick),
        ("tmuxinator", TmuxinatorBenchmark.Quick),
        ("ui5", Ui5Benchmark.Quick),
        ("ui5-manifest", Ui5ManifestBenchmark.Quick),
        ("unreal-engine-uproject", UnrealEngineUprojectBenchmark.Quick),
        ("vercel", VercelBenchmark.Quick),
        ("yamllint", YamllintBenchmark.Quick),
    ];

    public static int RunQuick(string[] filters, int rounds)
    {
        Console.WriteLine($"{"case",-26} {"generated",14} {"runtime",14} {"ratio",8}");
        double product = 1;
        int count = 0;
        foreach ((string file, var quick) in QuickCases)
        {
            if (filters.Length > 0 && !Array.Exists(filters, f => file.Contains(f, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            (double g, double r) = quick(file, rounds);
            product *= r / g;
            count++;
            Console.WriteLine($"{file,-26} {QuickTimer.Format(g),14} {QuickTimer.Format(r),14} {r / g,8:F2}");
        }

        if (count > 0)
        {
            Console.WriteLine($"geometric mean ratio (runtime / generated): {Math.Pow(product, 1.0 / count):F2} over {count} cases");
        }

        return 0;
    }

    public static int Run(string[] filters)
    {
        int total = 0;
        foreach ((string file, var diff) in Cases)
        {
            if (filters.Length > 0 && !Array.Exists(filters, f => file.Contains(f, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
                var mismatches = diff(file);
                total += mismatches.Count;
                Console.WriteLine($"{file}: {(mismatches.Count == 0 ? "agree" : mismatches.Count + " mismatches")}");
                foreach ((int index, bool g, bool r) in mismatches.Take(5))
                {
                    Console.WriteLine($"   instance {index}: generated={g} runtime={r}");
                    ExplainRuntime(file, index);
                }
            }
            catch (Exception ex)
            {
                total++;
                Console.WriteLine($"{file}: ERROR {ex.GetType().Name}: {ex.Message}");
            }
        }

        return total;
    }

    private static void ExplainRuntime(string file, int index)
    {
        using var runtime = new SourceMetaCase(file);
        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);
        runtime.Evaluator.Evaluate(runtime.Documents[index].RootElement, collector);
        int shown = 0;
        foreach (JsonSchemaResultsCollector.Result r in collector.EnumerateResults())
        {
            if (!r.IsMatch && shown++ < 8)
            {
                Console.WriteLine($"      FAIL {r.GetEvaluationLocationText()} @ {r.GetDocumentEvaluationLocationText()} ({r.GetSchemaEvaluationLocationText()}) {r.GetMessageText()}");
            }
        }
    }
}
