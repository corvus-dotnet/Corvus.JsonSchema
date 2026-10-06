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

            SchemaLowering.Compile(nodes, entry, out SchemaNode[] generatedNodes, out int specialised);
            if (Environment.GetEnvironmentVariable("CORVUS_RT_CODEGEN_STATS") == "census")
            {
                InterpretedCensus(name, generatedNodes);
            }

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

    /// <summary>
    /// The warm time of a pass through each engine, measured finely enough to compare two builds
    /// (<c>precise [corpus...]</c>): after a 2-second warm-up, 41 batches of at least 20 ms each, timed as a whole,
    /// and the median batch's time per pass. Prints <c>corpus,interpreter ns,generated ns</c>.
    /// </summary>
    public static int RunPrecise(string[] args)
    {
        string[] names = args.Length > 0 ? args : Array.ConvertAll(SourceMetaCases.All, c => c.File);
        foreach (string name in names)
        {
            using var c = new SourceMetaCase(name);
            JsonSchemaDialect dialect = JsonSchemaDialect.Draft7;
            foreach ((string file, JsonSchemaDialect d) in SourceMetaCases.All)
            {
                if (file == name)
                {
                    dialect = d;
                }
            }

            using JsonSchemaEvaluator generated = JsonSchemaEvaluator.Compile(c.SchemaBytes, new JsonSchemaEvaluatorOptions { DefaultDialect = dialect });
            bool compiled = generated.CompileGeneratedCode();
            double interpreterTime = Precise(c.Evaluator, c.Documents);
            double generatedTime = compiled ? Precise(generated, c.Documents) : double.NaN;
            Console.WriteLine($"{name},{interpreterTime:F0},{generatedTime:F0}");
        }

        return 0;
    }

    /// <summary>
    /// How much of a pass is reaching the documents' memory (<c>locality [corpus...]</c>): generated code over the
    /// corpus's documents, and over an array of the same length that refers to one of them at every position (the
    /// mean over eight documents), so the same work is done on memory already in the nearest cache. Prints
    /// <c>corpus,documents,ns a document over the corpus,ns a document over one document</c>.
    /// </summary>
    public static int RunLocality(string[] args)
    {
        foreach (string name in args)
        {
            using var c = new SourceMetaCase(name);
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
                continue;
            }

            int count = c.Documents.Length;
            double corpus = Precise(generated, c.Documents) / count;

            // Eight documents spread over the corpus, each alone: their mean against the same eight in turn.
            const int Samples = 8;
            double alone = 0;
            var sampled = new ParsedJsonDocument<JsonElement>[count];
            for (int s = 0; s < Samples; s++)
            {
                ParsedJsonDocument<JsonElement> one = c.Documents[(int)((long)s * count / Samples)];
                alone += Precise(generated, [.. Enumerable.Repeat(one, count)], 250) / count / Samples;
                for (int i = s; i < count; i += Samples)
                {
                    sampled[i] = one;
                }
            }

            double together = Precise(generated, sampled, 250) / count;

            // The same eight documents' content, each position a document of its own (parsed in a shuffled order, so
            // that neighbours in the array are not neighbours in memory): the same work as the line above.
            var copies = new ParsedJsonDocument<JsonElement>[count];
            int[] order = [.. Enumerable.Range(0, count)];
            new Random(1).Shuffle(order);
            foreach (int i in order)
            {
                copies[i] = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(sampled[i].RootElement.GetRawText()));
            }

            double apart = Precise(generated, copies, 250) / count;
            Console.WriteLine($"{name},{count},{corpus:F1},{apart:F1},{together:F1},{alone:F1}");
        }

        return 0;
    }

    /// <summary>
    /// The least an evaluation costs (<c>floor</c>): 1,000 separately parsed documents against a schema that only tests
    /// the type, by the interpreter and by generated code, as nanoseconds a document. One document repeated shows the
    /// same without reaching a new document's memory each time.
    /// </summary>
    public static int RunFloor()
    {
        // The least time of many short batches, which a busy moment cannot lower: differences of a few percent between
        // two builds show in it, where the median of a pass does not resolve them.
        foreach ((string name, string schema, string instance) in ((string, string, string)[])[
            ("empty", "{\"type\":\"object\"}", "{}"),
            ("tiny", "{\"type\":\"object\",\"properties\":{\"a\":{\"type\":\"string\"}}}", "{\"a\":\"alpha12\"}"),
            ("map", "{\"type\":\"object\",\"additionalProperties\":{\"type\":\"string\"}}", "{\"a\":\"b\",\"c\":\"d\",\"e\":\"f\",\"g\":\"h\"}"),
            ("strings", "{\"type\":\"array\",\"items\":{\"type\":\"string\"}}", "[\"a\",\"b\",\"c\",\"d\",\"e\",\"f\",\"g\",\"h\"]"),
("closed", "{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{\"name\":{\"type\":\"string\"},\"before\":{\"type\":\"string\"},\"init\":{\"type\":\"string\"},\"prebuild\":{\"type\":\"string\"},\"command\":{\"type\":\"string\"},\"env\":{\"type\":\"string\"},\"openIn\":{\"type\":\"string\"},\"openMode\":{\"type\":\"string\"}}}", "{\"command\":\"mu25\",\"env\":\"delta65\",\"before\":\"alpha36\",\"name\":\"delta47\",\"init\":\"zeta34\"}"),
            ("open", "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"},\"before\":{\"type\":\"string\"},\"init\":{\"type\":\"string\"},\"prebuild\":{\"type\":\"string\"},\"command\":{\"type\":\"string\"},\"env\":{\"type\":\"string\"},\"openIn\":{\"type\":\"string\"},\"openMode\":{\"type\":\"string\"}}}", "{\"command\":\"mu25\",\"env\":\"delta65\",\"before\":\"alpha36\",\"name\":\"delta47\",\"init\":\"zeta34\"}"),
            ("required", "{\"type\":\"object\",\"required\":[\"name\",\"init\",\"env\"],\"properties\":{\"name\":{\"type\":\"string\"},\"before\":{\"type\":\"string\"},\"init\":{\"type\":\"string\"},\"prebuild\":{\"type\":\"string\"},\"command\":{\"type\":\"string\"},\"env\":{\"type\":\"string\"},\"openIn\":{\"type\":\"string\"},\"openMode\":{\"type\":\"string\"}}}", "{\"command\":\"mu25\",\"env\":\"delta65\",\"before\":\"alpha36\",\"name\":\"delta47\",\"init\":\"zeta34\"}"),
                        ("integers", "{\"type\":\"array\",\"items\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":65535}}", "[44877,52433,40174,53803,13497,47953,10600,22455]")])
        {
            using JsonSchemaEvaluator generated = JsonSchemaEvaluator.Compile(schema);
            bool compiled = generated.CompileGeneratedCode();
            ParsedJsonDocument<JsonElement>[] documents = [.. Enumerable.Range(0, 1000).Select(_ => ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(instance)))];
            long end = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
            int valid = 0;
            while (Stopwatch.GetTimestamp() < end)
            {
                valid += Pass(generated, documents);
            }

            double least = double.MaxValue;
            for (int batch = 0; batch < 400; batch++)
            {
                long start = Stopwatch.GetTimestamp();
                for (int i = 0; i < 20; i++)
                {
                    valid += Pass(generated, documents);
                }

                least = Math.Min(least, (Stopwatch.GetTimestamp() - start) * 1_000_000_000.0 / Stopwatch.Frequency / 20 / documents.Length);
            }

            Console.WriteLine($"{name},{least:F3},{(compiled && valid > 0 ? "ok" : "CHECK")}");
        }

        return 0;
    }

    private static double Precise(JsonSchemaEvaluator evaluator, ParsedJsonDocument<JsonElement>[] documents, int warmUpMilliseconds)
    {
        const int Batches = 41;
        long end = Stopwatch.GetTimestamp() + (warmUpMilliseconds * Stopwatch.Frequency / 1000);
        long passes = 0;
        while (Stopwatch.GetTimestamp() < end)
        {
            Pass(evaluator, documents);
            passes++;
        }

        int perBatch = (int)Math.Max(1, passes * 20 / warmUpMilliseconds);
        double[] batches = new double[Batches];
        for (int b = 0; b < Batches; b++)
        {
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < perBatch; i++)
            {
                Pass(evaluator, documents);
            }

            batches[b] = (Stopwatch.GetTimestamp() - start) * 1_000_000_000.0 / Stopwatch.Frequency / perBatch;
        }

        Array.Sort(batches);
        return batches[Batches / 2];
    }

    private static double Precise(JsonSchemaEvaluator evaluator, ParsedJsonDocument<JsonElement>[] documents)
    {
        const int Batches = 41;
        long end = Stopwatch.GetTimestamp() + (2 * Stopwatch.Frequency);
        long passes = 0;
        while (Stopwatch.GetTimestamp() < end)
        {
            Pass(evaluator, documents);
            passes++;
        }

        // Passes per batch for about 20 ms, from the warm-up's rate.
        int perBatch = (int)Math.Max(1, passes / 100);
        double[] batches = new double[Batches];
        for (int b = 0; b < Batches; b++)
        {
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < perBatch; i++)
            {
                Pass(evaluator, documents);
            }

            batches[b] = (Stopwatch.GetTimestamp() - start) * 1_000_000_000.0 / Stopwatch.Frequency / perBatch;
        }

        Array.Sort(batches);
        return batches[Batches / 2];
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

    // The nodes left to the interpreter, by plan and by the keywords that keep them there.
    private static void InterpretedCensus(string name, SchemaNode[] nodes)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (SchemaNode node in nodes)
        {
            if (node is null || node.Plan is NodePlan.Generated or NodePlan.Leaf or NodePlan.AlwaysTrue or NodePlan.AlwaysFalse or NodePlan.Forward)
            {
                continue;
            }

            var reasons = new List<string>();
            if (node.Contains.IsPresent)
            {
                reasons.Add("contains");
            }

            if ((node.Flags & NodeFlags.HasUnevaluatedItems) != 0)
            {
                reasons.Add("unevaluatedItems");
            }

            if ((node.Flags & NodeFlags.HasUnevaluatedProperties) != 0)
            {
                reasons.Add("unevaluatedProperties");
            }

            if ((node.Flags & NodeFlags.InPlaceCycle) != 0)
            {
                reasons.Add("cycle");
            }

            if ((node.Flags & (NodeFlags.TracksProperties | NodeFlags.TracksItems)) != 0)
            {
                reasons.Add("tracks");
            }

            if ((node.Flags & NodeFlags.HasInPlaceApplicators) != 0)
            {
                reasons.Add("applicators");
            }

            if ((node.Flags & NodeFlags.HasObjectKeywords) != 0)
            {
                reasons.Add("object");
            }

            if ((node.Flags & NodeFlags.HasArrayKeywords) != 0)
            {
                reasons.Add("array");
            }

            void Has(bool present, string keyword)
            {
                if (present)
                {
                    reasons.Add(keyword);
                }
            }

            Has(node.Properties is not null, $"properties:{node.Properties?.Count}");
            Has(node.SeenBitCount > 64, $"seenBits:{node.SeenBitCount}");
            Has(node.PatternProperties is not null, "patternProperties");
            Has(node.AdditionalProperties.IsPresent, "additionalProperties");
            Has(node.PropertyNames.IsPresent, "propertyNames");
            Has(node.Dependencies is not null, "dependencies");
            Has(node.MinProperties >= 0 || node.MaxProperties >= 0, "propertyCount");
            Has(node.PrefixItems is not null, "prefixItems");
            Has(node.Items.IsPresent, "items");
            Has(node.UniqueItems, "uniqueItems");
            Has(node.DynamicRef is not null, "dynamicRef");
            Has(node.HasType, $"type:{node.Type}");
            Has(node.HasConst, "const");
            Has(node.Enum is not null, "enum");
            Has((node.Flags & NodeFlags.HasStringKeywords) != 0, "string");
            Has((node.Flags & NodeFlags.HasNumberKeywords) != 0, "number");
            Has(node.MinItems >= 0 || node.MaxItems >= 0, "itemCount");
            Has(node.Items.IsPresent, $"itemsPlan:{(node.Items.IsPresent ? nodes[node.Items.FastNode].Plan : default)}");

            string key = $"{node.Plan}[{string.Join("+", reasons)}]";
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        Console.WriteLine($"    interpreted {name,-24} {string.Join(", ", counts.Select(c => $"{c.Key} {c.Value}"))}");
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