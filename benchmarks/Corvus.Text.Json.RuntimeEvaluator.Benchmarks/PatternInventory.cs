// <copyright file="PatternInventory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.RuntimeEvaluator;
using Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks;

/// <summary>
/// <c>patterns [corpus...]</c>: every <c>pattern</c> and <c>patternProperties</c> expression of each corpus that the
/// matcher kinds do not cover and so runs as a <see cref="System.Text.RegularExpressions.Regex"/> (interpreted under
/// native AOT), with the number of nodes carrying it.
/// </summary>
public static class PatternInventory
{
    public static int Run(string[] args)
    {
        string[] names = args.Length > 0 ? args : SourceMetaCases.All.Select(c => c.File).ToArray();
        int totalRegex = 0;
        int totalPatterns = 0;
        foreach (string name in names)
        {
            JsonSchemaDialect dialect = SourceMetaCases.All.First(c => c.File == name).Dialect;
            byte[] schema = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "sourcemeta", name + "-schema.json"));
            using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema, new JsonSchemaEvaluatorOptions { DefaultDialect = dialect });
            var regex = new Dictionary<string, int>(StringComparer.Ordinal);
            int patterns = 0;
            foreach (SchemaNode node in evaluator.Program.Nodes)
            {
                if (node.Pattern is PatternMatcher p)
                {
                    patterns++;
                    if (p.UsesRegex)
                    {
                        regex[p.Source] = regex.TryGetValue(p.Source, out int n) ? n + 1 : 1;
                    }
                }

                if (node.PatternProperties is PatternPropertyEntry[] entries)
                {
                    foreach (PatternPropertyEntry entry in entries)
                    {
                        patterns++;
                        if (entry.Matcher.UsesRegex)
                        {
                            regex[entry.Matcher.Source] = regex.TryGetValue(entry.Matcher.Source, out int n) ? n + 1 : 1;
                        }
                    }
                }
            }

            totalPatterns += patterns;
            int regexNodes = regex.Values.Sum();
            totalRegex += regexNodes;
            Console.WriteLine($"{name}: {patterns} patterns, {regexNodes} on a regex ({regex.Count} distinct)");
            foreach ((string source, int count) in regex.OrderByDescending(kv => kv.Value))
            {
                Console.WriteLine($"    {count,3} x {source}");
            }
        }

        Console.WriteLine($"total: {totalPatterns} patterns, {totalRegex} on a regex");
        return 0;
    }
}
