// <copyright file="CodeGenProbe.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta;
using Corvus.Text.Json.RuntimeEvaluator.CodeGeneration;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;
using Corvus.Text.Json.RuntimeEvaluator.Evaluation;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks;

/// <summary>
/// Runtime codegen against the interpreter on the Sourcemeta corpora: how many nodes of each schema get specialised
/// methods, whether the generated code gives the interpreter's result on every instance, and the warm time of a pass
/// over the corpus through each (<c>codegen [warm-up ms] [corpus...]</c>; a warm-up of 0 skips the timing).
/// </summary>
/// <remarks>
/// Both engines are measured through the evaluator's public entry, as an application calls it: two evaluators of the
/// one schema, one of them with its generated code compiled. (Calling the generated entry directly leaves out the
/// per-document cost of the public entry, which the interpreter's figure includes: on corpora of small documents
/// that made generated code look far better than an application sees.)
/// </remarks>
public static class CodeGenProbe
{
    public static int Run(string[] args)
    {
        int warmUp = args.Length > 0 && int.TryParse(args[0], out int w) ? w : 0;
        string[] names = args.Length > 1 ? args[1..] : Array.ConvertAll(SourceMetaCases.All, c => c.File);
        Console.WriteLine($"{"corpus",-24} {"instances",9} {"entry plan",-14} {"specialised",11} {"mismatches",10} {"interpreter us",14} {"generated us",12} {"ratio",6}");
        int mismatched = 0;
        foreach (string name in names)
        {
            using var c = new SourceMetaCase(name);
            CompiledSchema program = c.Evaluator.Program;
            SchemaNode[] nodes = program.Nodes;
            SchemaNode entry = nodes[nodes[c.Evaluator.RootNode].FlagEntry];
            JsonSchemaDialect dialect = JsonSchemaDialect.Draft7;
            foreach ((string file, JsonSchemaDialect d) in SourceMetaCases.All)
            {
                if (file == name)
                {
                    dialect = d;
                }
            }

            using JsonSchemaEvaluator generated = JsonSchemaEvaluator.Compile(c.SchemaBytes, new JsonSchemaEvaluatorOptions { DefaultDialect = dialect });
            if (!generated.CompileGeneratedCode())
            {
                Console.WriteLine($"{name,-24} {c.Documents.Length,9} {"(not compiled)",-14}");
                continue;
            }

            SchemaLowering.Compile(nodes, entry, out _, out int specialised);
            if (Environment.GetEnvironmentVariable("CORVUS_RT_CODEGEN_STATS") == "1")
            {
                Stats(nodes);
            }

            if (Environment.GetEnvironmentVariable("CORVUS_RT_CODEGEN_STATS") == "fused")
            {
                FusedCensus(name, nodes);
            }

            int mismatches = 0;
            foreach (ParsedJsonDocument<JsonElement> d in c.Documents)
            {
                if (c.Evaluator.Evaluate(d.RootElement) != generated.Evaluate(d.RootElement))
                {
                    mismatches++;
                }
            }

            mismatched += mismatches;
            string timing = string.Empty;
            if (warmUp > 0)
            {
                double interpreterTime = Warm(warmUp, c.Evaluator, c.Documents);
                double generatedTime = Warm(warmUp, generated, c.Documents);
                timing = $" {interpreterTime,14:F1} {generatedTime,12:F1} {generatedTime / interpreterTime,6:F2}";
            }

            Console.WriteLine($"{name,-24} {c.Documents.Length,9} {entry.Plan,-14} {specialised,11} {mismatches,10}{timing}");
        }

        return mismatched == 0 ? 0 : 1;
    }

    // The median of the last passes of a warm-up by time, in microseconds: the same loop and call site for both engines.
    private static double Warm(int milliseconds, JsonSchemaEvaluator evaluator, ParsedJsonDocument<JsonElement>[] documents)
    {
        const int Last = 51;
        long[] passes = new long[Last];
        long end = Stopwatch.GetTimestamp() + (Stopwatch.Frequency * milliseconds / 1000);
        int n = 0;
        while (n < Last || Stopwatch.GetTimestamp() < end)
        {
            long start = Stopwatch.GetTimestamp();
            Pass(evaluator, documents);
            passes[n++ % Last] = Stopwatch.GetTimestamp() - start;
        }

        Array.Sort(passes);
        return passes[Last / 2] * 1_000_000.0 / Stopwatch.Frequency;
    }

    private static int Pass(JsonSchemaEvaluator evaluator, ParsedJsonDocument<JsonElement>[] documents)
    {
        int valid = 0;
        foreach (ParsedJsonDocument<JsonElement> d in documents)
        {
            valid += evaluator.Evaluate(d.RootElement) ? 1 : 0;
        }

        return valid;
    }

    // The fused objects that are not flat, by what a generated pass would have to handle.
    private static void FusedCensus(string name, SchemaNode[] nodes)
    {
        int full = 0, flat = 0, unevaluated = 0, conditionalUnknown = 0, wide = 0, simple = 0, conditions = 0, altGroups = 0, valueTests = 0, unknown = 0;
        foreach (SchemaNode node in nodes)
        {
            if (node?.Fused is not FusedObject f || node.Plan != NodePlan.FusedObject)
            {
                continue;
            }

            if (f.FlatEntries is not null)
            {
                flat++;
                continue;
            }

            full++;
            bool hasUnevaluated = f.Unevaluated.IsPresent;
            bool hasConditionalUnknown = f.Contributors.Any(c => c.Condition >= 0 && (c.Patterns is not null || c.AdditionalNode >= 0 || c.AdditionalCoversOnly));
            bool isWide = f.EntryList.Length > 64 || f.Conditions.Length > 64;
            unevaluated += hasUnevaluated ? 1 : 0;
            conditionalUnknown += hasConditionalUnknown ? 1 : 0;
            wide += isWide ? 1 : 0;
            simple += !hasUnevaluated && !hasConditionalUnknown && !isWide ? 1 : 0;
            conditions += f.Conditions.Length > 0 ? 1 : 0;
            altGroups += f.AltGroups.Length > 0 ? 1 : 0;
            valueTests += f.EntryList.Any(e => e.HasValueTests) ? 1 : 0;
            unknown += f.ResolvesUnknownNames ? 1 : 0;
        }

        Console.WriteLine($"    fused {name,-24} flat {flat,3} | full {full,3}: within limits {simple,3}; unevaluated {unevaluated,3}, conditional unknown names {conditionalUnknown,3}, over 64 names or conditions {wide,3} | with conditions {conditions,3}, alt groups {altGroups,3}, value tests {valueTests,3}, unknown names {unknown,3}");
    }

    // The shape of each strict object: its names by the dispatch's length classes and its entries by kind.
    private static void Stats(SchemaNode[] nodes)
    {
        foreach (SchemaNode node in nodes)
        {
            if (node is null || node.Plan != NodePlan.StrictObject)
            {
                continue;
            }

            byte[][] keys = node.Properties?.Keys ?? [];
            int oneWord = 0, twoWords = 0, longer = 0, largestGroup = 0;
            var groups = new Dictionary<int, int>();
            foreach (byte[] key in keys)
            {
                _ = key.Length <= 8 ? oneWord++ : key.Length <= 16 ? twoWords++ : longer++;
                groups[key.Length] = groups.GetValueOrDefault(key.Length) + 1;
                largestGroup = Math.Max(largestGroup, groups[key.Length]);
            }

            int tokens = 0, sets = 0, consts = 0, strictChildren = 0, otherChildren = 0, lengths = 0, nothing = 0;
            var plans = new Dictionary<NodePlan, int>();
            StrictEntry[] entries = [.. node.StrictEntries ?? [], node.AdditionalEntry];
            foreach (StrictEntry e in entries)
            {
                if (e.TokenBits != 0) { tokens++; }
                else if (e.Set is not null) { sets++; }
                else if (e.ConstBytes is not null) { consts++; }
                else if (e.Child >= 0)
                {
                    SchemaNode child = nodes[e.Child];
                    while (child.Plan == NodePlan.Forward) { child = nodes[child.ForwardNode]; }
                    if (child.Plan == NodePlan.StrictObject) { strictChildren++; } else { otherChildren++; plans[child.Plan] = plans.GetValueOrDefault(child.Plan) + 1; }
                }
                else if (e.LengthBounded) { lengths++; }
                else { nothing++; }
            }

            Console.WriteLine($"    node {node.Id,5}: names {keys.Length,3} (<=8: {oneWord}, <=16: {twoWords}, longer: {longer}, largest length group {largestGroup}) required {System.Numerics.BitOperations.PopCount(node.RequiredMask)} rejects {node.AdditionalRejects} | token {tokens} set {sets} const {consts} strict {strictChildren} length {lengths} none {nothing} other {otherChildren} [{string.Join(", ", plans.Select(p => $"{p.Key} {p.Value}"))}]");
        }
    }
}