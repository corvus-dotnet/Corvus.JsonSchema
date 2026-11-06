// <copyright file="Verify.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta;

/// <summary>
/// Checks that every instance of each corpus is valid against its schema (<c>verify &lt;corpus...&gt;</c>), as the
/// jsonschema-benchmark requires: every instance in its corpora is valid, and <c>format</c> is not asserted. The build
/// publishes the runner with native AOT and the package's freshly recorded profile and runs this, so that a profile
/// that makes the AOT compiler compile something wrongly fails the build instead of shipping.
/// </summary>
internal static class Verify
{
    /// <summary>Evaluates every instance of each corpus named, and reports the ones that are not valid.</summary>
    /// <param name="corpora">The corpora, by name (<c>&lt;name&gt;-schema.json</c> and <c>&lt;name&gt;-instances.jsonl</c> under
    /// <c>COLD_ROOT</c>).</param>
    /// <returns>0 when every instance is valid, otherwise 1.</returns>
    public static int Run(string[] corpora)
    {
        string root = Environment.GetEnvironmentVariable("COLD_ROOT") ?? Path.Combine(AppContext.BaseDirectory, "sourcemeta");
        int instances = 0;
        int wrong = 0;
        foreach (string corpus in corpora)
        {
            JsonSchemaDialect dialect = JsonSchemaDialect.Draft7;
            foreach ((string file, JsonSchemaDialect d) in SourceMetaCases.All)
            {
                if (file == corpus)
                {
                    dialect = d;
                }
            }

            byte[] schema = File.ReadAllBytes(Path.Combine(root, corpus + "-schema.json"));
            string[] lines = [.. File.ReadAllLines(Path.Combine(root, corpus + "-instances.jsonl")).Where(l => l.Length > 0)];
            using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema, new JsonSchemaEvaluatorOptions { DefaultDialect = dialect, AssertFormat = false });
            var invalid = new List<int>();
            for (int i = 0; i < lines.Length; i++)
            {
                using var document = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(lines[i]));
                if (!evaluator.Evaluate(document.RootElement))
                {
                    invalid.Add(i);
                }
            }

            instances += lines.Length;
            wrong += invalid.Count;
            if (invalid.Count > 0)
            {
                Console.Error.WriteLine($"{corpus}: {invalid.Count} of {lines.Length} valid instances evaluated as not valid (the first: {string.Join(", ", invalid.Take(10))})");
            }
        }

        Console.WriteLine($"{instances - wrong} of {instances} instances of {corpora.Length} corpora are valid");
        return wrong == 0 ? 0 : 1;
    }
}
