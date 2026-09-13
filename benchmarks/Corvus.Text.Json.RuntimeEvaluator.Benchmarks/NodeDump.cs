// <copyright file="NodeDump.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using System.Text;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks;

/// <summary>
/// Prints the compiled node graph of a schema (micro case name or schema file) to inspect which fast paths apply.
/// </summary>
public static class NodeDump
{
    public static int Run(string[] args)
    {
        string what = args.Length > 0 ? args[0] : "dynamic";
        string path = File.Exists(what) ? what : Path.Combine(AppContext.BaseDirectory, "micro-schemas", what + ".json");
        if (!File.Exists(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, "sourcemeta", what + "-schema.json");
        }

        byte[] schema = File.ReadAllBytes(path);
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema, new JsonSchemaEvaluatorOptions { AssertFormat = null });
        SchemaNode[] nodes = evaluator.Program.Nodes;
        Console.WriteLine($"{path}: {nodes.Length} nodes, root {evaluator.RootNode}, dynamic scope {evaluator.UsesDynamicScope}");
        foreach (SchemaNode n in nodes)
        {
            var sb = new StringBuilder();
            sb.Append($"#{n.Id,-4} r{n.ResourceId} {Encoding.UTF8.GetString(n.SchemaLocation),-50} [{n.Plan}] {n.Flags}");
            if (n.IsLeaf) sb.Append(" LEAF");
            if (n.IsTypeOnly) sb.Append(" TYPEONLY");
            if (n.IsSimpleArray) sb.Append(" SIMPLEARRAY");
            if (n.UnrolledProperties is not null) sb.Append($" UNROLLED({n.UnrolledProperties.Length})");
            if (n.MarksProperties) sb.Append(" marksP");
            if (n.MarksItems) sb.Append(" marksI");
            if (n.Properties is not null) sb.Append($" props={n.Properties.Count}");
            if (n.Fused is { } fused) sb.Append($" fused[entries={fused.EntryList.Length} contributors={fused.Contributors.Length} conditions={fused.Conditions.Length} alternatives={fused.Alternatives.Length} altGroups={fused.AltGroups.Length} unknownNames={fused.ResolvesUnknownNames} unevaluated={fused.Unevaluated.IsPresent} applications={fused.EntryList.Sum(e => e.Applications.Length)}]");
            if (n.Fused is { } fused2 && Environment.GetEnvironmentVariable("DUMP_FUSED") == "1")
            {
                foreach (var e in fused2.EntryList)
                {
                    sb.Append($"\n        {System.Text.Encoding.UTF8.GetString(e.Name)}: ");
                    foreach (var a in e.Applications)
                    {
                        sb.Append($"[c{a.Contributor}{(a.OtherContributors is null ? string.Empty : "+" + a.OtherContributors.Length)} n{a.Node}{(a.InlineEnum is null ? string.Empty : " enum" + a.InlineEnum.Count)}{(a.InlineType == TypeMask.None ? string.Empty : " type")}] ");
                    }
                }
            }
            if (n.RequiredSeenBits is not null) sb.Append($" required={n.RequiredSeenBits.Length}");
            if (n.PatternProperties is not null) sb.Append($" patternProps={n.PatternProperties.Length}");
            if (n.AdditionalProperties.IsPresent) sb.Append($" additional->{n.AdditionalProperties.Node}/{n.AdditionalProperties.FastNode}");
            if (n.Items.IsPresent) sb.Append($" items->{n.Items.Node}/{n.Items.FastNode}");
            if (n.Ref.IsPresent) sb.Append($" ref->{n.Ref.Node}/{n.Ref.FastNode}");
            if (n.DynamicRef is not null) sb.Append($" dynref[{string.Join(",", n.DynamicRef.NodeByResource)}] fb={n.DynamicRef.FallbackNode}");
            if (n.AllOf is not null) sb.Append($" allOf={n.AllOf.Length}");
            if (n.AnyOf is not null) sb.Append($" anyOf={n.AnyOf.Length}{(n.AnyOfDiscriminator is not null ? "(disc)" : string.Empty)}");
            if (n.OneOf is not null) sb.Append($" oneOf={n.OneOf.Length}{(n.OneOfDiscriminator is not null ? "(disc)" : string.Empty)}");
            if (n.UnevaluatedProperties.IsPresent) sb.Append(" unevalP");
            if (n.UnevaluatedItems.IsPresent) sb.Append(" unevalI");
            Console.WriteLine(sb.ToString());
        }

        return 0;
    }
}
