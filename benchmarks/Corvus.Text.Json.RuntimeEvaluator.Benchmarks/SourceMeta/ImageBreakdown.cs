// <copyright file="ImageBreakdown.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta;

/// <summary>Where the bytes of a compiled program go, per corpus, to guide image compaction.</summary>
public static class ImageBreakdown
{
    public static int Run(string[] filters)
    {
        Console.WriteLine($"{"schema",-18} {"schema",8} {"image",8} {"nodes",6} {"edges",6} {"annot",8} {"loc",8} {"locU",6} {"paths",7} {"pathU",6} {"names",7} {"nameU",6} {"consts",7} {"enumS",7}");
        foreach ((string file, JsonSchemaDialect dialect) in SourceMetaCases.All)
        {
            if (filters.Length > 0 && !Array.Exists(filters, f => file.Contains(f, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            byte[] schema = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "sourcemeta", file + "-schema.json"));
            using JsonSchemaEvaluator e = JsonSchemaEvaluator.Compile(schema, new JsonSchemaEvaluatorOptions { DefaultDialect = dialect, CompileRegularExpressions = false });
            byte[] image = e.ToProgramImage();
            SchemaNode[] nodes = e.Program.Nodes;
            long annot = 0, loc = 0, paths = 0, names = 0, consts = 0, enums = 0;
            int edges = 0;
            var distinctLoc = new HashSet<string>(StringComparer.Ordinal);
            var distinctPath = new HashSet<string>(StringComparer.Ordinal);
            var distinctName = new HashSet<string>(StringComparer.Ordinal);
            void Edge(in ChildRef c)
            {
                if (!c.IsPresent)
                {
                    return;
                }

                edges++;
                if (c.Path is byte[] p)
                {
                    paths += p.Length;
                    distinctPath.Add(System.Text.Encoding.UTF8.GetString(p));
                }

                if (c.CollectingPath is byte[] cp)
                {
                    paths += cp.Length;
                    distinctPath.Add(System.Text.Encoding.UTF8.GetString(cp));
                }
            }

            foreach (SchemaNode n in nodes)
            {
                loc += n.SchemaLocation.Length;
                distinctLoc.Add(System.Text.Encoding.UTF8.GetString(n.SchemaLocation));
                if (n.Annotations is not null)
                {
                    foreach (AnnotationEntry a in n.Annotations)
                    {
                        annot += a.Keyword.Length + a.RawJson.Length;
                    }
                }

                if (n.Properties is not null)
                {
                    foreach (PropertyEntry p in n.Properties.Values)
                    {
                        names += p.Name.Length;
                        distinctName.Add(System.Text.Encoding.UTF8.GetString(p.Name));
                        Edge(p.Schema);
                    }
                }

                if (n.RequiredNames is not null)
                {
                    foreach (byte[] r in n.RequiredNames)
                    {
                        names += r.Length;
                    }
                }

                if (n.ConstString is not null)
                {
                    consts += n.ConstString.Length;
                }

                if (n.EnumStrings is not null)
                {
                    foreach (byte[] k in n.EnumStrings.Keys)
                    {
                        enums += k.Length;
                    }
                }

                if (n.PatternProperties is not null)
                {
                    foreach (PatternPropertyEntry pp in n.PatternProperties)
                    {
                        Edge(pp.Schema);
                    }
                }

                Edge(n.AdditionalProperties); Edge(n.PropertyNames); Edge(n.UnevaluatedProperties); Edge(n.Items); Edge(n.Contains); Edge(n.UnevaluatedItems);
                Edge(n.Ref); Edge(n.Not); Edge(n.If); Edge(n.Then); Edge(n.Else);
                if (n.PrefixItems is not null) { foreach (ChildRef c in n.PrefixItems) { Edge(c); } }
                if (n.AllOf is not null) { foreach (ChildRef c in n.AllOf) { Edge(c); } }
                if (n.AnyOf is not null) { foreach (ChildRef c in n.AnyOf) { Edge(c); } }
                if (n.OneOf is not null) { foreach (ChildRef c in n.OneOf) { Edge(c); } }
                if (n.Dependencies is not null) { foreach (DependencyEntry d in n.Dependencies) { Edge(d.Schema); } }
            }

            Console.WriteLine($"{file,-18} {schema.Length,8} {image.Length,8} {nodes.Length,6} {edges,6} {annot,8} {loc,8} {distinctLoc.Count,6} {paths,7} {distinctPath.Count,6} {names,7} {distinctName.Count,6} {consts,7} {enums,7}");
        }

        return 0;
    }
}
