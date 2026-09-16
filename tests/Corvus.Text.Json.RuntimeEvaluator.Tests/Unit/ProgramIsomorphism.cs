// <copyright file="ProgramIsomorphism.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// Decides whether two program images are the same program up to numbering: the same node graph from the root and
/// from every entry point, with node ids and resource ids renamed consistently. The compiler numbers nodes and
/// resources in the order it meets them, so registering the same entry points in another order gives an image whose
/// bytes differ although the program is the same.
/// </summary>
/// <remarks>
/// A node's shape is every value the image serialises for it (the list in <c>ProgramImage.WriteNode</c>), with node
/// ids, resource ids and the table indices the image uses for constants, patterns and locations replaced by the values
/// they stand for. Two nodes match when their shapes are equal and their references pair up position by position; the
/// pairing must be a bijection over both node sets and both resource id sets, and it must reach every node.
/// </remarks>
internal static class ProgramIsomorphism
{
    /// <summary>Asserts that two images are the same program up to numbering.</summary>
    /// <param name="left">The first image.</param>
    /// <param name="right">The second image.</param>
    /// <param name="options">The options both images are read with.</param>
    public static void AssertIsomorphic(byte[] left, byte[] right, JsonSchemaEvaluatorOptions options)
    {
        using CompiledSchema first = ProgramImage.Read(left, options);
        using CompiledSchema second = ProgramImage.Read(right, options);
        Assert.AreEqual(first.Nodes.Length, second.Nodes.Length, "node count");
        Assert.AreEqual(first.ResourceCount, second.ResourceCount, "resource count");
        Assert.AreEqual(first.UsesDynamicScope, second.UsesDynamicScope, "uses dynamic scope");
        Assert.AreEqual(first.EntryPoints.Count, second.EntryPoints.Count, "entry point count");

        var matcher = new Matcher(first, second);
        matcher.Pair(first.RootNode, second.RootNode, "the root");
        foreach (KeyValuePair<string, int> entry in first.EntryPoints)
        {
            Assert.IsTrue(second.EntryPoints.TryGetValue(entry.Key, out int other), $"entry point '{entry.Key}' is only in the first program");
            matcher.Pair(entry.Value, other, $"entry point '{entry.Key}'");
        }

        matcher.Run();
        Assert.AreEqual(first.Nodes.Length, matcher.PairedNodes, "every node is reached from the root or an entry point");
    }

    private sealed class Shape
    {
        public string Text = string.Empty;

        public int Resource;

        /// <summary>The node ids the node refers to, in serialisation order.</summary>
        public List<int> Nodes = [];

        /// <summary>Tables indexed by resource id whose values are node ids (or -1).</summary>
        public List<int[]?> ResourceMaps = [];
    }

    private sealed class Matcher(CompiledSchema left, CompiledSchema right)
    {
        private readonly Dictionary<int, int> nodes = [];
        private readonly Dictionary<int, int> nodesBack = [];
        private readonly Dictionary<int, int> resources = [];
        private readonly Dictionary<int, int> resourcesBack = [];
        private readonly Queue<(int Left, int Right)> pending = new();
        private readonly List<(int Left, int Right, List<int[]?> LeftMaps, List<int[]?> RightMaps)> deferred = [];

        public int PairedNodes => this.nodes.Count;

        public void Pair(int x, int y, string what)
        {
            if (this.nodes.TryGetValue(x, out int known))
            {
                Assert.AreEqual(known, y, $"{what}: node {x} of the first program already pairs with node {known} of the second, not {y}");
                return;
            }

            Assert.IsFalse(this.nodesBack.TryGetValue(y, out int other), $"{what}: node {y} of the second program already pairs with node {other} of the first, not {x}");
            this.nodes[x] = y;
            this.nodesBack[y] = x;
            this.pending.Enqueue((x, y));
        }

        public void Run()
        {
            bool progress = true;
            while (progress)
            {
                progress = false;
                while (this.pending.Count > 0)
                {
                    (int x, int y) = this.pending.Dequeue();
                    this.Compare(x, y);
                    progress = true;
                }

                // A resource-indexed table can be compared once the resources it is indexed by are paired, which
                // happens when a node of each resource has been compared.
                for (int i = this.deferred.Count - 1; i >= 0; i--)
                {
                    if (this.TryCompareResourceMaps(this.deferred[i]))
                    {
                        this.deferred.RemoveAt(i);
                        progress = true;
                    }
                }
            }

            Assert.AreEqual(0, this.deferred.Count, "resource-indexed tables whose resources were never paired");
        }

        private static string Location(SchemaNode node)
        {
            return Encoding.UTF8.GetString(node.SchemaLocation);
        }

        private static Shape ShapeOf(SchemaNode n)
        {
            var shape = new Shape { Resource = n.ResourceId };
            var t = new StringBuilder();

            void Int(int v) => t.Append(v).Append(';');
            void Bool(bool v) => t.Append(v ? '1' : '0').Append(';');
            void Enum(object v) => t.Append(v).Append(';');
            void Str(string? v) => t.Append(v is null ? "~" : Convert.ToBase64String(Encoding.UTF8.GetBytes(v))).Append(';');
            void Bytes(byte[]? v) => t.Append(v is null ? "~" : Convert.ToBase64String(v)).Append(';');
            void Node(int id)
            {
                if (id < 0)
                {
                    t.Append("-;");
                }
                else
                {
                    t.Append("n;");
                    shape.Nodes.Add(id);
                }
            }

            void Ref(in ChildRef c)
            {
                if (c.Node < 0)
                {
                    t.Append("none;");
                    return;
                }

                t.Append("ref;");
                Bytes(c.Path);
                Bytes(c.CollectingPath);
                shape.Nodes.Add(c.Node);
                shape.Nodes.Add(c.FastNode);
            }

            void Refs(ChildRef[]? refs)
            {
                if (refs is null)
                {
                    Int(-1);
                    return;
                }

                Int(refs.Length);
                foreach (ChildRef c in refs)
                {
                    Ref(c);
                }
            }

            void Ints(int[]? v)
            {
                if (v is null)
                {
                    Int(-1);
                    return;
                }

                Int(v.Length);
                foreach (int i in v)
                {
                    Int(i);
                }
            }

            void ByteArrays(byte[][]? v)
            {
                if (v is null)
                {
                    Int(-1);
                    return;
                }

                Int(v.Length);
                foreach (byte[] b in v)
                {
                    Bytes(b);
                }
            }

            void Constant(in ConstantValue v) => Str(Elements.Create<JsonElement>(v.Document, v.Index).GetRawText());

            void Pattern(PatternMatcher? m)
            {
                Str(m?.Source);
                Bool(m?.UsesRegex ?? false);
            }

            void ResourceMap(int[]? map)
            {
                t.Append(map is null ? "~;" : "map;");
                shape.ResourceMaps.Add(map);
            }

            void Disc(Discriminator? d)
            {
                if (d is null)
                {
                    Bool(false);
                    return;
                }

                Bool(true);
                Bytes(d.PropertyName);
                Int(d.KnownValues.Count);
                byte[][] keys = d.KnownValues.Keys;
                ReadOnlySpan<int[]> values = d.KnownValues.Values;
                for (int i = 0; i < keys.Length; i++)
                {
                    Bytes(keys[i]);
                    Ints(values[i]);
                }

                Ints(d.UnknownString);
                Ints(d.NonString);
                Ints(d.AllBranches);
                Bool(d.AllRequire);
            }

            Enum(n.Dialect);
            Bool(n.AlwaysTrue);
            Bool(n.AlwaysFalse);
            Bool(n.IsTypeOnly);
            Bool(n.IsLeaf);
            Bool(n.IsSimpleArray);
            Bool(n.TracksProperties);
            Bool(n.TracksItems);
            Bool(n.MarksProperties);
            Bool(n.MarksItems);
            Bool(n.HasNumberKeywords);
            Bool(n.HasStringKeywords);
            Bool(n.HasObjectKeywords);
            Bool(n.HasArrayKeywords);
            Bool(n.HasInPlaceApplicators);
            Bool(n.InPlaceCycle);
            Bool(n.HasSeenBits);
            Bool(n.HasType);
            Bool(n.HasConst);
            Bool(n.EnumAllStrings);
            Bool(n.AssertFormat);
            Bool(n.WarnFormat);
            Bool(n.AssertContent);
            Bool(n.UniqueItems);
            Bool(n.ContainsMarksEvaluated);
            Enum(n.Flags);
            Enum(n.Plan);
            Bytes(n.SchemaLocation);
            Enum(n.Type);

            // const / enum
            if (n.HasConst)
            {
                Constant(n.Const);
            }

            Bytes(n.ConstString);
            Str(n.ConstNumber?.Text);
            if (n.Enum is ConstantValue[] enumValues)
            {
                Int(enumValues.Length);
                foreach (ConstantValue v in enumValues)
                {
                    Constant(v);
                }
            }
            else
            {
                Int(-1);
            }

            if (n.EnumStrings is Utf8NameMap<object> enumStrings)
            {
                Int(enumStrings.Count);
                foreach (byte[] key in enumStrings.Keys)
                {
                    Bytes(key);
                }
            }
            else
            {
                Int(-1);
            }

            // number
            Str(n.Minimum?.Text);
            Str(n.Maximum?.Text);
            Str(n.ExclusiveMinimum?.Text);
            Str(n.ExclusiveMaximum?.Text);
            Str(n.MultipleOf?.Text);

            // string
            Int(n.MinLength);
            Int(n.MaxLength);
            Pattern(n.Pattern);
            Enum(n.Format);
            Node(n.ElidedTarget);
            Enum(n.Content);

            // object
            PropertyEntry[]? propertyValues = null;
            if (n.Properties is Utf8NameMap<PropertyEntry> properties)
            {
                propertyValues = properties.Values.ToArray();
                Int(propertyValues.Length);
                foreach (PropertyEntry p in propertyValues)
                {
                    Bytes(p.Name);
                    Ref(p.Schema);
                    Int(p.SeenBit);
                    Bool(p.IsRequired);
                }
            }
            else
            {
                Int(-1);
            }

            if (n.UnrolledProperties is PropertyEntry[] unrolled)
            {
                Int(unrolled.Length);
                foreach (PropertyEntry p in unrolled)
                {
                    Int(Array.IndexOf(propertyValues!, p));
                }
            }
            else
            {
                Int(-1);
            }

            Int(n.SeenBitCount);
            Ints(n.RequiredSeenBits);
            ByteArrays(n.RequiredNames);
            if (n.PatternProperties is PatternPropertyEntry[] patternProperties)
            {
                Int(patternProperties.Length);
                foreach (PatternPropertyEntry p in patternProperties)
                {
                    Pattern(p.Matcher);
                    Ref(p.Schema);
                    Bytes(p.Name);
                }
            }
            else
            {
                Int(-1);
            }

            Ref(n.AdditionalProperties);
            Ref(n.PropertyNames);
            Ref(n.UnevaluatedProperties);
            Int(n.MinProperties);
            Int(n.MaxProperties);
            if (n.Dependencies is DependencyEntry[] dependencies)
            {
                Int(dependencies.Length);
                foreach (DependencyEntry d in dependencies)
                {
                    Bytes(d.Name);
                    Int(d.SeenBit);
                    Ints(d.RequiredSeenBits);
                    ByteArrays(d.RequiredNames);
                    Ref(d.Schema);
                }
            }
            else
            {
                Int(-1);
            }

            // array
            Refs(n.PrefixItems);
            Ref(n.Items);
            Ref(n.Contains);
            Int(n.MinContains);
            Int(n.MaxContains);
            Int(n.MinItems);
            Int(n.MaxItems);
            Ref(n.UnevaluatedItems);

            // in-place applicators
            Ref(n.Ref);
            if (n.DynamicRef is DynamicRefTarget dynamicRef)
            {
                Bool(true);
                Str(dynamicRef.Anchor);
                Node(dynamicRef.FallbackNode);
                ResourceMap(dynamicRef.NodeByResource);
                ResourceMap(dynamicRef.NodeByEntryResource);
                Bytes(dynamicRef.PathSegment);
                Bool(dynamicRef.IsRecursive);
            }
            else
            {
                Bool(false);
            }

            Refs(n.AllOf);
            Refs(n.AnyOf);
            Refs(n.OneOf);
            Disc(n.AnyOfDiscriminator);
            Disc(n.OneOfDiscriminator);
            Enum(n.AnyOfTypeUnion);
            Enum(n.OneOfTypeUnion);
            Ref(n.Not);
            Ref(n.If);
            Ref(n.Then);
            Ref(n.Else);

            // annotations
            if (n.Annotations is AnnotationEntry[] annotations)
            {
                Int(annotations.Length);
                foreach (AnnotationEntry a in annotations)
                {
                    Bytes(a.Keyword);
                    Bytes(a.RawJson);
                    Bool(a.StringsOnly);
                }
            }
            else
            {
                Int(-1);
            }

            shape.Text = t.ToString();
            return shape;
        }

        private void Compare(int x, int y)
        {
            SchemaNode leftNode = left.Nodes[x];
            SchemaNode rightNode = right.Nodes[y];
            string what = $"node {x} of the first program ({Location(leftNode)}) against node {y} of the second ({Location(rightNode)})";
            Shape sx = ShapeOf(leftNode);
            Shape sy = ShapeOf(rightNode);
            Assert.AreEqual(sx.Text, sy.Text, what);
            this.PairResource(sx.Resource, sy.Resource, what);
            Assert.AreEqual(sx.Nodes.Count, sy.Nodes.Count, what);
            for (int i = 0; i < sx.Nodes.Count; i++)
            {
                this.Pair(sx.Nodes[i], sy.Nodes[i], what);
            }

            if (sx.ResourceMaps.Count > 0)
            {
                this.deferred.Add((x, y, sx.ResourceMaps, sy.ResourceMaps));
            }
        }

        private void PairResource(int r, int s, string what)
        {
            if (this.resources.TryGetValue(r, out int known))
            {
                Assert.AreEqual(known, s, $"{what}: resource {r} of the first program already pairs with resource {known} of the second, not {s}");
                return;
            }

            Assert.IsFalse(this.resourcesBack.TryGetValue(s, out int other), $"{what}: resource {s} of the second program already pairs with resource {other} of the first, not {r}");
            this.resources[r] = s;
            this.resourcesBack[s] = r;
        }

        private bool TryCompareResourceMaps((int Left, int Right, List<int[]?> LeftMaps, List<int[]?> RightMaps) item)
        {
            string what = $"node {item.Left} of the first program ({Location(left.Nodes[item.Left])}) against node {item.Right} of the second ({Location(right.Nodes[item.Right])})";
            Assert.AreEqual(item.LeftMaps.Count, item.RightMaps.Count, what);
            for (int k = 0; k < item.LeftMaps.Count; k++)
            {
                int[]? leftMap = item.LeftMaps[k];
                int[]? rightMap = item.RightMaps[k];
                if (leftMap is null || rightMap is null)
                {
                    Assert.IsTrue(leftMap is null && rightMap is null, $"{what}: a resource-indexed table is present in one program only");
                    continue;
                }

                int leftCount = 0;
                int rightCount = 0;
                foreach (int node in leftMap)
                {
                    leftCount += node >= 0 ? 1 : 0;
                }

                foreach (int node in rightMap)
                {
                    rightCount += node >= 0 ? 1 : 0;
                }

                Assert.AreEqual(leftCount, rightCount, $"{what}: a resource-indexed table has a different number of entries");
                for (int r = 0; r < leftMap.Length; r++)
                {
                    if (leftMap[r] < 0)
                    {
                        continue;
                    }

                    if (!this.resources.TryGetValue(r, out int s))
                    {
                        return false;
                    }

                    Assert.IsTrue(s < rightMap.Length && rightMap[s] >= 0, $"{what}: resource {r} of the first program has an entry in a resource-indexed table, its pair {s} in the second has none");
                    this.Pair(leftMap[r], rightMap[s], what);
                }
            }

            return true;
        }
    }
}