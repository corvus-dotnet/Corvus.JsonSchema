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
            if (program.UsesDynamicScope || !SchemaLowering.IsSupported)
            {
                Console.WriteLine($"{name,-24} {c.Documents.Length,9} {"(not compiled)",-14}");
                continue;
            }

            NodeValidator compiled = SchemaLowering.Compile(nodes, entry, out int specialised);
            if (Environment.GetEnvironmentVariable("CORVUS_RT_CODEGEN_STATS") == "1")
            {
                Stats(nodes);
            }

            int mismatches = 0;
            foreach (ParsedJsonDocument<JsonElement> d in c.Documents)
            {
                if (c.Evaluator.Evaluate(d.RootElement) != Generated(c, compiled, entry, d))
                {
                    mismatches++;
                }
            }

            mismatched += mismatches;
            string timing = string.Empty;
            if (warmUp > 0)
            {
                double interpreter = Warm(warmUp, () => c.EvaluateAll());
                double generated = Warm(warmUp, () =>
                {
                    int valid = 0;
                    foreach (ParsedJsonDocument<JsonElement> d in c.Documents)
                    {
                        valid += Generated(c, compiled, entry, d) ? 1 : 0;
                    }

                    return valid;
                });
                timing = $" {interpreter,14:F1} {generated,12:F1} {generated / interpreter,6:F2}";
            }

            Console.WriteLine($"{name,-24} {c.Documents.Length,9} {entry.Plan,-14} {specialised,11} {mismatches,10}{timing}");
        }

        return mismatched == 0 ? 0 : 1;
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

    private static bool Generated(SourceMetaCase c, NodeValidator compiled, SchemaNode entry, ParsedJsonDocument<JsonElement> d)
    {
        IJsonDocument document = ((IJsonElement<JsonElement>)d.RootElement).ParentDocument;
        int index = ((IJsonElement<JsonElement>)d.RootElement).ParentDocumentIndex;
        CompiledSchema program = c.Evaluator.Program;
        return Evaluator.EvaluateFlagCompiled(compiled, program, program.Nodes, entry.ResourceId, program.Options.MaxDepth, (Corvus.Text.Json.Internal.JsonDocument)document, document, c.Evaluator.RootNode, index);
    }

    // The fastest pass after a warm-up, in microseconds.
    private static double Warm(int milliseconds, Func<int> pass)
    {
        long end = Stopwatch.GetTimestamp() + (Stopwatch.Frequency * milliseconds / 1000);
        while (Stopwatch.GetTimestamp() < end)
        {
            pass();
        }

        long best = long.MaxValue;
        for (int i = 0; i < 200; i++)
        {
            long start = Stopwatch.GetTimestamp();
            pass();
            best = Math.Min(best, Stopwatch.GetTimestamp() - start);
        }

        return best * 1_000_000.0 / Stopwatch.Frequency;
    }
}