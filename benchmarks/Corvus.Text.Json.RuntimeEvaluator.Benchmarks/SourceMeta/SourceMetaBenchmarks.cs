using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta;

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "ansible-meta": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class AnsibleMetaBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.AnsibleMetaBenchmark.Current.AnsibleMetaSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("ansible-meta");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "ansible-meta-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.AnsibleMetaBenchmark.Current.AnsibleMetaSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.AnsibleMetaBenchmark.Current.AnsibleMetaSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.AnsibleMetaBenchmark.Current.AnsibleMetaSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.AnsibleMetaBenchmark.Current.AnsibleMetaSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.AnsibleMetaBenchmark.Current.AnsibleMetaSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "aws-cdk": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class AwsCdkBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.AwsCdkBenchmark.Current.AwsCdkSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("aws-cdk");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "aws-cdk-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.AwsCdkBenchmark.Current.AwsCdkSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.AwsCdkBenchmark.Current.AwsCdkSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.AwsCdkBenchmark.Current.AwsCdkSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.AwsCdkBenchmark.Current.AwsCdkSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.AwsCdkBenchmark.Current.AwsCdkSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "babelrc": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class BabelrcBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.BabelrcBenchmark.Current.BabelrcSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("babelrc");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "babelrc-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.BabelrcBenchmark.Current.BabelrcSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.BabelrcBenchmark.Current.BabelrcSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.BabelrcBenchmark.Current.BabelrcSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.BabelrcBenchmark.Current.BabelrcSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.BabelrcBenchmark.Current.BabelrcSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "clang-format": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class ClangFormatBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.ClangFormatBenchmark.Current.ClangFormatSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("clang-format");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "clang-format-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.ClangFormatBenchmark.Current.ClangFormatSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.ClangFormatBenchmark.Current.ClangFormatSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.ClangFormatBenchmark.Current.ClangFormatSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.ClangFormatBenchmark.Current.ClangFormatSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.ClangFormatBenchmark.Current.ClangFormatSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "cmake-presets": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class CmakePresetsBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.CmakePresetsBenchmark.Current.CmakePresetsSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("cmake-presets");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "cmake-presets-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.CmakePresetsBenchmark.Current.CmakePresetsSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.CmakePresetsBenchmark.Current.CmakePresetsSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.CmakePresetsBenchmark.Current.CmakePresetsSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.CmakePresetsBenchmark.Current.CmakePresetsSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.CmakePresetsBenchmark.Current.CmakePresetsSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "code-climate": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class CodeClimateBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.CodeClimateBenchmark.Current.CodeClimateSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("code-climate");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "code-climate-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.CodeClimateBenchmark.Current.CodeClimateSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.CodeClimateBenchmark.Current.CodeClimateSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.CodeClimateBenchmark.Current.CodeClimateSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.CodeClimateBenchmark.Current.CodeClimateSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.CodeClimateBenchmark.Current.CodeClimateSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "cql2": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class Cql2Benchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.Cql2Benchmark.Current.Cql2Schema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("cql2");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "cql2-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.Cql2Benchmark.Current.Cql2Schema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.Cql2Benchmark.Current.Cql2Schema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.Cql2Benchmark.Current.Cql2Schema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.Cql2Benchmark.Current.Cql2Schema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.Cql2Benchmark.Current.Cql2Schema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "cspell": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class CspellBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.CspellBenchmark.Current.CspellSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("cspell");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "cspell-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.CspellBenchmark.Current.CspellSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.CspellBenchmark.Current.CspellSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.CspellBenchmark.Current.CspellSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.CspellBenchmark.Current.CspellSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.CspellBenchmark.Current.CspellSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "cypress": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class CypressBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.CypressBenchmark.Current.CypressSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("cypress");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "cypress-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.CypressBenchmark.Current.CypressSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.CypressBenchmark.Current.CypressSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.CypressBenchmark.Current.CypressSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.CypressBenchmark.Current.CypressSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.CypressBenchmark.Current.CypressSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "deno": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class DenoBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.DenoBenchmark.Current.DenoSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("deno");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "deno-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.DenoBenchmark.Current.DenoSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.DenoBenchmark.Current.DenoSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.DenoBenchmark.Current.DenoSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.DenoBenchmark.Current.DenoSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.DenoBenchmark.Current.DenoSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "dependabot": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class DependabotBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.DependabotBenchmark.Current.DependabotSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("dependabot");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "dependabot-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.DependabotBenchmark.Current.DependabotSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.DependabotBenchmark.Current.DependabotSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.DependabotBenchmark.Current.DependabotSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.DependabotBenchmark.Current.DependabotSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.DependabotBenchmark.Current.DependabotSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "draft-04": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class Draft04Benchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.Draft04Benchmark.Current.Draft04Schema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("draft-04");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "draft-04-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.Draft04Benchmark.Current.Draft04Schema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.Draft04Benchmark.Current.Draft04Schema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.Draft04Benchmark.Current.Draft04Schema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.Draft04Benchmark.Current.Draft04Schema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.Draft04Benchmark.Current.Draft04Schema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "fabric-mod": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class FabricModBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.FabricModBenchmark.Current.FabricModSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("fabric-mod");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "fabric-mod-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.FabricModBenchmark.Current.FabricModSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.FabricModBenchmark.Current.FabricModSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.FabricModBenchmark.Current.FabricModSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.FabricModBenchmark.Current.FabricModSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.FabricModBenchmark.Current.FabricModSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "geojson": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class GeoJsonBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.GeoJsonBenchmark.Current.GeoJsonSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("geojson");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "geojson-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.GeoJsonBenchmark.Current.GeoJsonSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.GeoJsonBenchmark.Current.GeoJsonSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.GeoJsonBenchmark.Current.GeoJsonSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.GeoJsonBenchmark.Current.GeoJsonSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.GeoJsonBenchmark.Current.GeoJsonSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "gitpod-configuration": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class GitpodConfigurationBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.GitpodConfigurationBenchmark.Current.GitpodConfigurationSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("gitpod-configuration");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "gitpod-configuration-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.GitpodConfigurationBenchmark.Current.GitpodConfigurationSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.GitpodConfigurationBenchmark.Current.GitpodConfigurationSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.GitpodConfigurationBenchmark.Current.GitpodConfigurationSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.GitpodConfigurationBenchmark.Current.GitpodConfigurationSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.GitpodConfigurationBenchmark.Current.GitpodConfigurationSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "helm-chart-lock": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class HelmChartLockBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.HelmChartLockBenchmark.Current.HelmChartLockSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("helm-chart-lock");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "helm-chart-lock-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.HelmChartLockBenchmark.Current.HelmChartLockSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.HelmChartLockBenchmark.Current.HelmChartLockSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.HelmChartLockBenchmark.Current.HelmChartLockSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.HelmChartLockBenchmark.Current.HelmChartLockSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.HelmChartLockBenchmark.Current.HelmChartLockSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "importmap": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class ImportmapBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.ImportmapBenchmark.Current.ImportmapSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("importmap");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "importmap-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.ImportmapBenchmark.Current.ImportmapSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.ImportmapBenchmark.Current.ImportmapSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.ImportmapBenchmark.Current.ImportmapSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.ImportmapBenchmark.Current.ImportmapSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.ImportmapBenchmark.Current.ImportmapSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "jasmine": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class JasmineBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.JasmineBenchmark.Current.JasmineSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("jasmine");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "jasmine-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.JasmineBenchmark.Current.JasmineSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.JasmineBenchmark.Current.JasmineSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.JasmineBenchmark.Current.JasmineSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.JasmineBenchmark.Current.JasmineSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.JasmineBenchmark.Current.JasmineSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "jsconfig": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class JsconfigBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.JsconfigBenchmark.Current.JsconfigSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("jsconfig");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "jsconfig-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.JsconfigBenchmark.Current.JsconfigSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.JsconfigBenchmark.Current.JsconfigSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.JsconfigBenchmark.Current.JsconfigSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.JsconfigBenchmark.Current.JsconfigSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.JsconfigBenchmark.Current.JsconfigSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "jshintrc": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class JshintrcBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.JshintrcBenchmark.Current.JshintrcSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("jshintrc");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "jshintrc-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.JshintrcBenchmark.Current.JshintrcSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.JshintrcBenchmark.Current.JshintrcSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.JshintrcBenchmark.Current.JshintrcSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.JshintrcBenchmark.Current.JshintrcSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.JshintrcBenchmark.Current.JshintrcSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "krakend": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class KrakendBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.KrakendBenchmark.Current.KrakendSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("krakend");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "krakend-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.KrakendBenchmark.Current.KrakendSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.KrakendBenchmark.Current.KrakendSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.KrakendBenchmark.Current.KrakendSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.KrakendBenchmark.Current.KrakendSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.KrakendBenchmark.Current.KrakendSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "lazygit": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class LazygitBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.LazygitBenchmark.Current.LazygitSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("lazygit");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "lazygit-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.LazygitBenchmark.Current.LazygitSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.LazygitBenchmark.Current.LazygitSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.LazygitBenchmark.Current.LazygitSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.LazygitBenchmark.Current.LazygitSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.LazygitBenchmark.Current.LazygitSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "lerna": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class LernaBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.LernaBenchmark.Current.LernaSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("lerna");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "lerna-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.LernaBenchmark.Current.LernaSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.LernaBenchmark.Current.LernaSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.LernaBenchmark.Current.LernaSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.LernaBenchmark.Current.LernaSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.LernaBenchmark.Current.LernaSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "nest-cli": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class NestCliBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.NestCliBenchmark.Current.NestCliSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("nest-cli");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "nest-cli-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.NestCliBenchmark.Current.NestCliSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.NestCliBenchmark.Current.NestCliSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.NestCliBenchmark.Current.NestCliSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.NestCliBenchmark.Current.NestCliSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.NestCliBenchmark.Current.NestCliSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "omnisharp": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class OmnisharpBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.OmnisharpBenchmark.Current.OmnisharpSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("omnisharp");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "omnisharp-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.OmnisharpBenchmark.Current.OmnisharpSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.OmnisharpBenchmark.Current.OmnisharpSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.OmnisharpBenchmark.Current.OmnisharpSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.OmnisharpBenchmark.Current.OmnisharpSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.OmnisharpBenchmark.Current.OmnisharpSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "openapi": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class OpenapiBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.OpenapiBenchmark.Current.OpenapiSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("openapi");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "openapi-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.OpenapiBenchmark.Current.OpenapiSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.OpenapiBenchmark.Current.OpenapiSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.OpenapiBenchmark.Current.OpenapiSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.OpenapiBenchmark.Current.OpenapiSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.OpenapiBenchmark.Current.OpenapiSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "pre-commit-hooks": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class PreCommitHooksBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.PreCommitHooksBenchmark.Current.PreCommitHooksSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("pre-commit-hooks");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "pre-commit-hooks-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.PreCommitHooksBenchmark.Current.PreCommitHooksSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.PreCommitHooksBenchmark.Current.PreCommitHooksSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.PreCommitHooksBenchmark.Current.PreCommitHooksSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.PreCommitHooksBenchmark.Current.PreCommitHooksSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.PreCommitHooksBenchmark.Current.PreCommitHooksSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "pulumi": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class PulumiBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.PulumiBenchmark.Current.PulumiSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("pulumi");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "pulumi-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.PulumiBenchmark.Current.PulumiSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.PulumiBenchmark.Current.PulumiSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.PulumiBenchmark.Current.PulumiSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.PulumiBenchmark.Current.PulumiSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.PulumiBenchmark.Current.PulumiSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "semantic-release": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class SemanticReleaseBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.SemanticReleaseBenchmark.Current.SemanticReleaseSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("semantic-release");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "semantic-release-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.SemanticReleaseBenchmark.Current.SemanticReleaseSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.SemanticReleaseBenchmark.Current.SemanticReleaseSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.SemanticReleaseBenchmark.Current.SemanticReleaseSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.SemanticReleaseBenchmark.Current.SemanticReleaseSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.SemanticReleaseBenchmark.Current.SemanticReleaseSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "stale": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class StaleBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.StaleBenchmark.Current.StaleSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("stale");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "stale-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.StaleBenchmark.Current.StaleSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.StaleBenchmark.Current.StaleSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.StaleBenchmark.Current.StaleSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.StaleBenchmark.Current.StaleSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.StaleBenchmark.Current.StaleSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "stylecop": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class StylecopBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.StylecopBenchmark.Current.StylecopSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("stylecop");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "stylecop-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.StylecopBenchmark.Current.StylecopSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.StylecopBenchmark.Current.StylecopSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.StylecopBenchmark.Current.StylecopSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.StylecopBenchmark.Current.StylecopSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.StylecopBenchmark.Current.StylecopSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "tmuxinator": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class TmuxinatorBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.TmuxinatorBenchmark.Current.TmuxinatorSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("tmuxinator");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "tmuxinator-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.TmuxinatorBenchmark.Current.TmuxinatorSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.TmuxinatorBenchmark.Current.TmuxinatorSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.TmuxinatorBenchmark.Current.TmuxinatorSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.TmuxinatorBenchmark.Current.TmuxinatorSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.TmuxinatorBenchmark.Current.TmuxinatorSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "ui5": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class Ui5Benchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.Ui5Benchmark.Current.Ui5Schema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("ui5");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "ui5-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.Ui5Benchmark.Current.Ui5Schema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.Ui5Benchmark.Current.Ui5Schema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.Ui5Benchmark.Current.Ui5Schema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.Ui5Benchmark.Current.Ui5Schema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.Ui5Benchmark.Current.Ui5Schema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "ui5-manifest": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class Ui5ManifestBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.Ui5ManifestBenchmark.Current.Ui5ManifestSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("ui5-manifest");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "ui5-manifest-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.Ui5ManifestBenchmark.Current.Ui5ManifestSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.Ui5ManifestBenchmark.Current.Ui5ManifestSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.Ui5ManifestBenchmark.Current.Ui5ManifestSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.Ui5ManifestBenchmark.Current.Ui5ManifestSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.Ui5ManifestBenchmark.Current.Ui5ManifestSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "unreal-engine-uproject": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class UnrealEngineUprojectBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.UnrealEngineUprojectBenchmark.Current.UnrealEngineUprojectSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("unreal-engine-uproject");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "unreal-engine-uproject-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.UnrealEngineUprojectBenchmark.Current.UnrealEngineUprojectSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.UnrealEngineUprojectBenchmark.Current.UnrealEngineUprojectSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.UnrealEngineUprojectBenchmark.Current.UnrealEngineUprojectSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.UnrealEngineUprojectBenchmark.Current.UnrealEngineUprojectSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.UnrealEngineUprojectBenchmark.Current.UnrealEngineUprojectSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "vercel": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class VercelBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.VercelBenchmark.Current.VercelSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("vercel");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "vercel-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.VercelBenchmark.Current.VercelSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.VercelBenchmark.Current.VercelSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.VercelBenchmark.Current.VercelSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.VercelBenchmark.Current.VercelSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.VercelBenchmark.Current.VercelSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

/// <summary>Sourcemeta (format as annotation, matching the checked-in generated models) "yamllint": generated model (current) vs runtime evaluator.</summary>
[MemoryDiagnoser]
public class YamllintBenchmark
{
    private SourceMetaCase? runtime;
    private ParsedJsonDocument<Corvus.YamllintBenchmark.Current.YamllintSchema>[]? generated;

    [GlobalSetup]
    public void Setup()
    {
        this.runtime = new SourceMetaCase("yamllint");
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", "yamllint-instances.jsonl"));
        this.generated = new ParsedJsonDocument<Corvus.YamllintBenchmark.Current.YamllintSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.generated[i] = ParsedJsonDocument<Corvus.YamllintBenchmark.Current.YamllintSchema>.Parse(lines[i]);
        }

        if (this.Generated() != this.RuntimeEvaluator())
        {
            throw new InvalidOperationException("Generated model and runtime evaluator disagree on the number of valid instances.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.runtime?.Dispose();
        foreach (var d in this.generated!)
        {
            d.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    public int Generated()
    {
        int valid = 0;
        foreach (var doc in this.generated!)
        {
            if (doc.RootElement.EvaluateSchema())
            {
                valid++;
            }
        }

        return valid;
    }


    /// <summary>Per-instance comparison of the generated model and the runtime evaluator.</summary>
    public static List<(int Index, bool Generated, bool Runtime)> Diff(string file)
    {
        var mismatches = new List<(int, bool, bool)>();
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        for (int i = 0; i < lines.Length; i++)
        {
            using var typed = ParsedJsonDocument<Corvus.YamllintBenchmark.Current.YamllintSchema>.Parse(lines[i]);
            bool g = typed.RootElement.EvaluateSchema();
            bool r = runtime.Evaluator.Evaluate(runtime.Documents[i].RootElement);
            if (g != r)
            {
                mismatches.Add((i, g, r));
            }
        }

        return mismatches;
    }


    /// <summary>Interleaved min-of-N timing of both evaluators (robust on a loaded machine).</summary>
    public static (double GeneratedNs, double RuntimeNs) Quick(string file, int rounds)
    {
        using var runtime = new SourceMetaCase(file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-instances.jsonl"));
        var typed = new ParsedJsonDocument<Corvus.YamllintBenchmark.Current.YamllintSchema>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            typed[i] = ParsedJsonDocument<Corvus.YamllintBenchmark.Current.YamllintSchema>.Parse(lines[i]);
        }

        int GeneratedAll()
        {
            int v = 0;
            foreach (var d in typed)
            {
                if (d.RootElement.EvaluateSchema())
                {
                    v++;
                }
            }

            return v;
        }

        (double g, double r) = QuickTimer.Run(rounds, GeneratedAll, runtime.EvaluateAll);
        foreach (var d in typed)
        {
            d.Dispose();
        }

        return (g, r);
    }

    [Benchmark]
    public int RuntimeEvaluator() => this.runtime!.EvaluateAll();
}

