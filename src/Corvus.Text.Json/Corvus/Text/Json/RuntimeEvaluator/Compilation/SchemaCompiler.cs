// <copyright file="SchemaCompiler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
#if !STJ
using Corvus.Text.Json.Internal;
#endif

namespace Corvus.Text.Json.RuntimeEvaluator.Compilation;

/// <summary>
/// The compiled program: the node graph plus the documents that back it.
/// </summary>
internal sealed class CompiledSchema : IDisposable
{
    private readonly object gate = new();
    private SchemaCompiler? compiler;
    private int references;
    private readonly Dictionary<string, int> entryPoints = new(StringComparer.Ordinal);
    private readonly ParsedJsonDocument<JsonElement>? constantsDocument;

    public CompiledSchema(SchemaNode[] nodes, int rootNode, SchemaLoader loader, bool usesDynamicScope, JsonSchemaEvaluatorOptions options, SchemaCompiler compiler)
    {
        this.Nodes = nodes;
        this.RootNode = rootNode;
        this.Loader = loader;
        this.UsesDynamicScope = usesDynamicScope;
        this.Options = options;
        this.ResourceCount = loader.Resources.Count;
        this.compiler = compiler;
        this.entryPoints[options.EntryPoint ?? string.Empty] = rootNode;
    }

    /// <summary>
    /// Initializes a program loaded from an image: no loader and no compiler, so only the entry points recorded in
    /// the image are available, and the constants document is the program's only parsed JSON.
    /// </summary>
    public CompiledSchema(SchemaNode[] nodes, int rootNode, bool usesDynamicScope, int resourceCount, JsonSchemaEvaluatorOptions options, Dictionary<string, int> entryPoints, ParsedJsonDocument<JsonElement> constantsDocument)
    {
        this.Nodes = nodes;
        this.RootNode = rootNode;
        this.Loader = null;
        this.UsesDynamicScope = usesDynamicScope;
        this.Options = options;
        this.ResourceCount = resourceCount;
        this.entryPoints = entryPoints;
        this.constantsDocument = constantsDocument;
    }

    /// <summary>Gets the entry points compiled so far, keyed by the reference they were requested with.</summary>
    public IReadOnlyDictionary<string, int> EntryPoints => this.entryPoints;

    /// <summary>Gets a value indicating whether the program was loaded from an image rather than compiled.</summary>
    public bool IsImage => this.Loader is null;

    /// <summary>The node graph. Replaced (never mutated in place) when entry points add nodes.</summary>
    public SchemaNode[] Nodes { get; private set; }

    public int RootNode { get; }

    public SchemaLoader? Loader { get; }

    public bool UsesDynamicScope { get; private set; }

    public int ResourceCount { get; private set; }

    public JsonSchemaEvaluatorOptions Options { get; }

    /// <summary>Adds a reference for an evaluator sharing this program.</summary>
    public void AddReference()
    {
        Interlocked.Increment(ref this.references);
    }

    /// <summary>
    /// Resolves (compiling as needed) the node for another entry point, sharing documents and nodes.
    /// </summary>
    public int AddEntryPoint(string reference)
    {
        lock (this.gate)
        {
            if (this.entryPoints.TryGetValue(reference, out int known))
            {
                return known;
            }

            if (this.Loader is null)
            {
                throw new JsonSchemaCompilationException($"The program image has no entry point '{reference}'; entry points must be compiled when the image is created.");
            }

            SchemaCompiler c = this.compiler ?? throw new ObjectDisposedException(nameof(JsonSchemaEvaluator));
            int node = c.AddEntryPoint(reference);
            this.Nodes = c.NodesSnapshot();
            this.UsesDynamicScope = c.UsesDynamicScope;
            this.ResourceCount = this.Loader.Resources.Count;
            this.entryPoints[reference] = node;
            return node;
        }
    }

    /// <summary>Compiles several entry points at once (see <see cref="SchemaCompiler.AddEntryPoints"/>).</summary>
    public void AddEntryPoints(IReadOnlyList<string> references)
    {
        lock (this.gate)
        {
            var pending = new List<string>();
            foreach (string reference in references)
            {
                if (!this.entryPoints.ContainsKey(reference) && !pending.Contains(reference))
                {
                    pending.Add(reference);
                }
            }

            if (pending.Count == 0)
            {
                return;
            }

            if (this.Loader is null)
            {
                throw new JsonSchemaCompilationException("The program image has no entry point for one of the references; entry points must be compiled when the image is created.");
            }

            SchemaCompiler c = this.compiler ?? throw new ObjectDisposedException(nameof(JsonSchemaEvaluator));
            int[] nodes = c.AddEntryPoints(pending);
            this.Nodes = c.NodesSnapshot();
            this.UsesDynamicScope = c.UsesDynamicScope;
            this.ResourceCount = this.Loader.Resources.Count;
            for (int i = 0; i < pending.Count; i++)
            {
                this.entryPoints[pending[i]] = nodes[i];
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Decrement(ref this.references) > 0)
        {
            return;
        }

        lock (this.gate)
        {
            this.compiler = null;
        }

        if (this.Loader is SchemaLoader loader)
        {
            foreach (SchemaDocument doc in loader.Documents)
            {
                doc.Document.Dispose();
            }
        }

        this.constantsDocument?.Dispose();
    }
}

/// <summary>
/// Compiles schema documents into a <see cref="CompiledSchema"/>.
/// </summary>
internal sealed class SchemaCompiler
{
    // Experiment switches (environment variables) for A/B measurement of flag-mode optimisations.
#if STJ
    private static readonly bool DisableUnroll = false;
    private static readonly bool DisableElision = false;
    private static readonly bool DisableDiscriminator = false;
    private static readonly bool DisableLeaf = false;
    private static readonly bool DisableOrdering = false;
    private static readonly bool DisablePlans = false;
#else
    private static readonly bool DisableUnroll = Environment.GetEnvironmentVariable("CORVUS_RT_NO_UNROLL") == "1";
    private static readonly bool DisableElision = Environment.GetEnvironmentVariable("CORVUS_RT_NO_ELIDE") == "1";
    private static readonly bool DisableDiscriminator = Environment.GetEnvironmentVariable("CORVUS_RT_NO_DISCRIMINATOR") == "1";
    private static readonly bool DisableLeaf = Environment.GetEnvironmentVariable("CORVUS_RT_NO_LEAF") == "1";
    private static readonly bool DisableOrdering = Environment.GetEnvironmentVariable("CORVUS_RT_NO_ORDER") == "1";
    private static readonly bool DisablePlans = Environment.GetEnvironmentVariable("CORVUS_RT_NO_PLANS") == "1";
#endif

    private readonly SchemaLoader loader;
    private readonly JsonSchemaEvaluatorOptions options;
    private readonly Dictionary<(int Doc, int Index), int> nodeIds = [];
    private readonly List<SchemaNode> nodes = [];
    private readonly List<SchemaTarget> targets = [];
    private readonly Queue<int> worklist = new();
    private readonly List<PendingDynamicRef> pendingDynamicRefs = [];

    // The resources evaluation can start in: the dynamic scope's outermost entry is always one of these.
    private readonly HashSet<int> entryNodeIds = [];

    private SchemaCompiler(SchemaLoader loader, JsonSchemaEvaluatorOptions options)
    {
        this.loader = loader;
        this.options = options;
    }

    private sealed class PendingDynamicRef
    {
        public int NodeId;
        public string Anchor = string.Empty;
        public bool IsRecursive;
        public SchemaTarget InitialTarget;
        public byte[] PathSegment = [];
        public HashSet<int> SeenResources = [];
        public List<(int ResourceId, int NodeId)> Candidates = [];
    }

    public static CompiledSchema Compile(ReadOnlyMemory<byte> utf8Schema, JsonSchemaEvaluatorOptions options)
    {
        var loader = new SchemaLoader(options);
        SchemaResource rootResource = loader.LoadRoot(utf8Schema, options.BaseUri);
        var compiler = new SchemaCompiler(loader, options) { rootResource = rootResource };
        int root = compiler.AddEntryPoint(options.EntryPoint);
        return new CompiledSchema(compiler.NodesSnapshot(), root, loader, compiler.UsesDynamicScope, options, compiler);
    }

    /// <summary>
    /// Compiles the schema document at a URI, loaded through the options' resolvers.
    /// </summary>
    public static CompiledSchema CompileFromUri(string uri, JsonSchemaEvaluatorOptions options)
    {
        var loader = new SchemaLoader(options);
        SchemaResource rootResource = loader.LoadRoot(uri);
        var compiler = new SchemaCompiler(loader, options) { rootResource = rootResource };
        int root = compiler.AddEntryPoint(options.EntryPoint);
        return new CompiledSchema(compiler.NodesSnapshot(), root, loader, compiler.UsesDynamicScope, options, compiler);
    }

    private SchemaResource rootResource = null!;

    /// <summary>Gets a value indicating whether any dynamic reference remains dynamic after finalisation.</summary>
    public bool UsesDynamicScope { get; private set; }

    public SchemaNode[] NodesSnapshot() => [.. this.nodes];

    /// <summary>
    /// Resolves an entry point (null or "#" for the document root), compiles everything newly reachable from it
    /// and re-runs the whole-graph passes, which are idempotent over already-compiled nodes.
    /// </summary>
    public int AddEntryPoint(string? entryPoint)
    {
        int node = this.RegisterEntryPoint(entryPoint);
        this.CompileAll();
        this.ComputeUsesDynamicScope();
        return node;
    }

    /// <summary>
    /// Adds several entry points with one pass of the post-compilation analyses, which otherwise run once per entry
    /// point: a program generated for hundreds of types would repeat them hundreds of times.
    /// </summary>
    public int[] AddEntryPoints(IReadOnlyList<string> entryPoints)
    {
        var nodes = new int[entryPoints.Count];
        for (int i = 0; i < entryPoints.Count; i++)
        {
            nodes[i] = this.RegisterEntryPoint(entryPoints[i]);
        }

        this.CompileAll();
        this.ComputeUsesDynamicScope();
        return nodes;
    }

    private int RegisterEntryPoint(string? entryPoint)
    {
        SchemaTarget entry = new(this.rootResource.Document, this.rootResource.RootIndex, this.rootResource);
        if (entryPoint is { Length: > 0 } && entryPoint != "#")
        {
            if (!this.loader.TryResolveReference(this.rootResource, entryPoint, out entry))
            {
                throw new JsonSchemaCompilationException($"Unable to resolve entry point '{entryPoint}' from '{this.rootResource.Uri}'.");
            }
        }

        int node = this.GetNode(entry);
        this.entryNodeIds.Add(node);
        return node;
    }

    private void ComputeUsesDynamicScope()
    {
        bool usesDynamicScope = false;
        foreach (SchemaNode n in this.nodes)
        {
            usesDynamicScope |= n.DynamicRef is { NeedsScope: true };
        }

        this.UsesDynamicScope = usesDynamicScope;
    }

    private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

    private static byte[] Segment(string keyword) => Utf8(keyword);

    private static byte[] Segment(string keyword, int index) => Utf8(keyword + "/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static byte[] Segment(string keyword, ReadOnlySpan<byte> name)
    {
        // JSON pointer escaping of the name.
        int extra = 0;
        foreach (byte b in name)
        {
            if (b == (byte)'~' || b == (byte)'/')
            {
                extra++;
            }
        }

        byte[] result = new byte[keyword.Length + 1 + name.Length + extra];
        int w = Encoding.UTF8.GetBytes(keyword, result);
        result[w++] = (byte)'/';
        foreach (byte b in name)
        {
            if (b == (byte)'~')
            {
                result[w++] = (byte)'~';
                result[w++] = (byte)'0';
            }
            else if (b == (byte)'/')
            {
                result[w++] = (byte)'~';
                result[w++] = (byte)'1';
            }
            else
            {
                result[w++] = b;
            }
        }

        return result;
    }

    private static byte[] RawJson(in JsonElement element) => Utf8(element.GetRawText());

    private static int GetInt(in JsonElement element, int fallback)
    {
        if (element.ValueKind != JsonValueKind.Number)
        {
            return fallback;
        }

        if (element.TryGetInt32(out int v))
        {
            return v;
        }

        if (element.TryGetDouble(out double d))
        {
            if (d >= int.MaxValue)
            {
                return int.MaxValue;
            }

            if (d <= 0)
            {
                return 0;
            }

            return (int)d;
        }

        return fallback;
    }

    private int GetNode(SchemaTarget target)
    {
        var key = (target.Document.Id, target.Index);
        if (this.nodeIds.TryGetValue(key, out int id))
        {
            return id;
        }

        id = this.nodes.Count;
        var node = new SchemaNode
        {
            Id = id,
            ResourceId = target.Resource.Id,
            Dialect = target.Resource.Dialect,
        };

        // Schema location: JSON pointer of the element within its document.
        JsonElement element = target.Element;
        Span<byte> pointerBuffer = stackalloc byte[512];
        if (element.TryGetJsonPointer(pointerBuffer, out int written))
        {
            node.SchemaLocation = pointerBuffer[..written].ToArray();
        }
        else
        {
            byte[] big = new byte[8192];
            if (element.TryGetJsonPointer(big, out written))
            {
                node.SchemaLocation = big.AsSpan(0, written).ToArray();
            }
        }

        this.nodes.Add(node);
        this.targets.Add(target);
        this.nodeIds[key] = id;
        this.worklist.Enqueue(id);
        return id;
    }

    private void CompileAll()
    {
        do
        {
            while (this.worklist.Count > 0)
            {
                int id = this.worklist.Dequeue();
                this.CompileNode(this.nodes[id], this.targets[id]);
            }
        }
        while (this.ExpandDynamicRefs());

        this.FinalizeDynamicRefs();
        this.ComputeLeafFlags();
        this.ComputeTracking();
        this.ComputeMarking();
        this.ElidePureRefs();
        this.ComputeDiscriminators();
        this.OrderUnrolledProperties();
        this.ComputeSimpleArrays();
        this.ComputeInPlaceCycles();
        SchemaNode[] all = [.. this.nodes];
        FusedObjects.Compute(all);
        ComputeForwards(all);
        this.ComputePlans();
        this.ComputeFlags();
    }

    /// <summary>
    /// Marks every node whose only assertion is a single in-place child (a lone <c>allOf</c> branch, or a <c>$ref</c>
    /// the pure-reference elision left in place) so that flag mode dispatches straight to that child's plan. A node
    /// on an in-place cycle keeps the general path and its runaway guard; when the program keeps a dynamic scope a
    /// child in another resource is not forwarded to, because entering it must push that resource. Runs at compile
    /// time and again when an image is loaded, since the result is not stored.
    /// </summary>
    internal static void ComputeForwards(SchemaNode[] nodes)
    {
        bool usesDynamicScope = false;
        foreach (SchemaNode n in nodes)
        {
            n.ForwardNode = -1;
            usesDynamicScope |= n.DynamicRef is { NeedsScope: true };
        }

        if (DisablePlans)
        {
            return;
        }

        foreach (SchemaNode node in nodes)
        {
            if (node.AlwaysTrue || node.AlwaysFalse || node.InPlaceCycle || node.Fused is not null
                || node.HasType || node.HasConst || node.Enum is not null || node.HasNumberKeywords || node.HasStringKeywords
                || node.HasObjectKeywords || node.HasArrayKeywords || node.DynamicRef is not null || node.AnyOf is not null || node.OneOf is not null
                || node.Not.IsPresent || node.If.IsPresent || node.Dependencies is not null
                || node.UnevaluatedProperties.IsPresent || node.UnevaluatedItems.IsPresent)
            {
                continue;
            }

            int target;
            if (node.Ref.IsPresent && node.AllOf is null)
            {
                target = node.Ref.FastNode;
            }
            else if (!node.Ref.IsPresent && node.AllOf is { Length: 1 } allOf)
            {
                target = allOf[0].FastNode;
            }
            else
            {
                continue;
            }

            // A target on an in-place cycle is entered through the guarded general edge, as dynamic references do.
            if (target == node.Id || nodes[target].InPlaceCycle || (usesDynamicScope && nodes[target].ResourceId != node.ResourceId))
            {
                continue;
            }

            node.ForwardNode = target;
        }
    }

    /// <summary>
    /// Marks array schemas whose only content is a leaf <c>items</c> schema plus size bounds.
    /// </summary>
    private void ComputeSimpleArrays()
    {
        foreach (SchemaNode node in this.nodes)
        {
            if (DisableLeaf || !node.Items.IsPresent || node.PrefixItems is not null || node.Contains.IsPresent
                || node.UnevaluatedItems.IsPresent || node.TracksItems || node.HasInPlaceApplicators || node.HasObjectKeywords
                || node.HasConst || node.Enum is not null || node.HasNumberKeywords || node.HasStringKeywords
                || node.AlwaysTrue || node.AlwaysFalse || node.UnevaluatedProperties.IsPresent)
            {
                continue;
            }

            if (node.HasType && node.Type != TypeMask.Array)
            {
                continue;
            }

            SchemaNode items = this.nodes[node.Items.FastNode];
            if (items.IsLeaf || items.IsTypeOnly || items.AlwaysTrue)
            {
                node.IsSimpleArray = true;
            }
        }
    }

    /// <summary>
    /// Orders unrolled property checks cheapest first: required before optional, then leaf schemas before
    /// applicator-bearing ones (Blaze's properties_reorder).
    /// </summary>
    private void OrderUnrolledProperties()
    {
        if (DisableOrdering)
        {
            return;
        }

        foreach (SchemaNode node in this.nodes)
        {
            if (node.UnrolledProperties is not PropertyEntry[] entries)
            {
                continue;
            }

            int Cost(PropertyEntry e)
            {
                int cost = e.IsRequired ? 0 : 2;
                if (e.Schema.IsPresent)
                {
                    SchemaNode target = this.nodes[e.Schema.FastNode];
                    cost += target.AlwaysTrue ? 0 : target.IsTypeOnly ? 0 : target.IsLeaf ? 1 : 3;
                }

                return cost;
            }

            Array.Sort(entries, (a, b) => Cost(a).CompareTo(Cost(b)));
        }
    }

    /// <summary>
    /// Points every child reference at the end of any chain of pure <c>$ref</c> nodes for flag-mode evaluation.
    /// </summary>
    private void ElidePureRefs()
    {
        if (DisableElision)
        {
            return;
        }

        bool usesDynamicScope = false;
        foreach (SchemaNode n in this.nodes)
        {
            usesDynamicScope |= n.DynamicRef is { NeedsScope: true };
        }

        int Resolve(int id, out int hops)
        {
            int current = id;
            hops = 0;
            while (hops < 16)
            {
                SchemaNode n = this.nodes[current];
                if (!n.Ref.IsPresent || !IsPureRef(n))
                {
                    break;
                }

                // Crossing a resource boundary must still push the dynamic scope, so keep such hops.
                if (usesDynamicScope && this.nodes[n.Ref.Node].ResourceId != n.ResourceId)
                {
                    break;
                }

                current = n.Ref.Node;
                hops++;
            }

            return current;
        }

        void Fix(ref ChildRef c)
        {
            if (c.IsPresent)
            {
                c.FastNode = Resolve(c.Node, out int hops);
                c.CollectingPath = hops == 0 ? null : CollectingPathFor(c.Path, hops);
            }
        }

        foreach (SchemaNode node in this.nodes)
        {
            int target = Resolve(node.Id, out int hops);
            node.ElidedTarget = hops == 0 ? -1 : target;
        }

        void FixAll(ChildRef[]? cs)
        {
            if (cs is null)
            {
                return;
            }

            for (int i = 0; i < cs.Length; i++)
            {
                Fix(ref cs[i]);
            }
        }

        foreach (SchemaNode node in this.nodes)
        {
            Fix(ref node.Ref);
            FixAll(node.AllOf);
            FixAll(node.AnyOf);
            FixAll(node.OneOf);
            Fix(ref node.Not);
            Fix(ref node.If);
            Fix(ref node.Then);
            Fix(ref node.Else);
            Fix(ref node.AdditionalProperties);
            Fix(ref node.PropertyNames);
            Fix(ref node.UnevaluatedProperties);
            Fix(ref node.Items);
            Fix(ref node.Contains);
            Fix(ref node.UnevaluatedItems);
            FixAll(node.PrefixItems);
            if (node.Properties is not null)
            {
                foreach (PropertyEntry e in node.Properties.Values)
                {
                    Fix(ref e.Schema);
                }
            }

            if (node.PatternProperties is not null)
            {
                foreach (PatternPropertyEntry e in node.PatternProperties)
                {
                    Fix(ref e.Schema);
                }
            }

            if (node.Dependencies is not null)
            {
                foreach (DependencyEntry e in node.Dependencies)
                {
                    Fix(ref e.Schema);
                }
            }

            if (node.DynamicRef is not null)
            {
                node.DynamicRef.FallbackNode = Resolve(node.DynamicRef.FallbackNode, out _);
                for (int i = 0; i < node.DynamicRef.NodeByResource.Length; i++)
                {
                    if (node.DynamicRef.NodeByResource[i] >= 0)
                    {
                        node.DynamicRef.NodeByResource[i] = Resolve(node.DynamicRef.NodeByResource[i], out _);
                    }
                }
            }
        }
    }

    /// <summary>
    /// The evaluation path segment for a child reached through elided pure <c>$ref</c> hops: the child's own
    /// segment followed by one <c>$ref</c> per hop, matching the paths generated models report for reduced types.
    /// </summary>
    private static byte[] CollectingPathFor(byte[]? path, int hops)
    {
        ReadOnlySpan<byte> refSegment = "$ref"u8;
        int length = (path?.Length ?? 0) + (hops * (refSegment.Length + 1)) - (path is null ? 1 : 0);
        byte[] result = new byte[length];
        int written = 0;
        if (path is not null)
        {
            path.CopyTo(result, 0);
            written = path.Length;
        }

        for (int i = 0; i < hops; i++)
        {
            if (written > 0)
            {
                result[written++] = (byte)'/';
            }

            refSegment.CopyTo(result.AsSpan(written));
            written += refSegment.Length;
        }

        return result;
    }

    private static bool IsPureRef(SchemaNode n)
    {
        return !n.HasType && !n.HasConst && n.Enum is null && !n.HasNumberKeywords && !n.HasStringKeywords && !n.HasObjectKeywords && !n.HasArrayKeywords
            && n.DynamicRef is null && n.AllOf is null && n.AnyOf is null && n.OneOf is null && !n.Not.IsPresent && !n.If.IsPresent
            && !n.UnevaluatedProperties.IsPresent && !n.UnevaluatedItems.IsPresent && n.Annotations is null && !n.AlwaysTrue && !n.AlwaysFalse;
    }

    // ---------------------------------------------------------------------------------------------
    // Discriminators
    // ---------------------------------------------------------------------------------------------
    private enum BranchKind
    {
        Wildcard,
        Positive,
        Negative,
    }

    private void ComputeDiscriminators()
    {
        if (DisableDiscriminator)
        {
            return;
        }

        foreach (SchemaNode node in this.nodes)
        {
            if (node.OneOf?.Length > 1)
            {
                node.OneOfDiscriminator = this.BuildDiscriminator(node.OneOf);
                node.OneOfTypeUnion = this.TypeUnion(node.OneOf, requireDisjoint: true);
            }

            if (node.AnyOf?.Length > 1)
            {
                node.AnyOfDiscriminator = this.BuildDiscriminator(node.AnyOf);
                node.AnyOfTypeUnion = this.TypeUnion(node.AnyOf, requireDisjoint: false);
            }
        }

        ComputeTypeDispatch([.. this.nodes]);
    }

    /// <summary>
    /// For every <c>anyOf</c>/<c>oneOf</c> whose branches all assert a <c>type</c> and no two branches accept the same
    /// token type, builds the token-type-to-branch table (see <see cref="SchemaNode.AnyOfTypeDispatch"/>); a branch
    /// keeps its own type test, so <c>integer</c> and draft 4 lexical integers need nothing special here. Runs at
    /// compile time and on image load, since the tables are derived from the branches' types rather than stored.
    /// </summary>
    internal static void ComputeTypeDispatch(SchemaNode[] nodes)
    {
        foreach (SchemaNode node in nodes)
        {
            node.AnyOfTypeDispatch = null;
            node.OneOfTypeDispatch = null;
        }

        if (DisableDiscriminator)
        {
            return;
        }

        foreach (SchemaNode node in nodes)
        {
            if (node.OneOf is { Length: > 1 } oneOf && node.OneOfTypeUnion == TypeMask.None)
            {
                node.OneOfTypeDispatch = BuildTypeDispatch(nodes, oneOf);
            }

            if (node.AnyOf is { Length: > 1 } anyOf && node.AnyOfTypeUnion == TypeMask.None)
            {
                node.AnyOfTypeDispatch = BuildTypeDispatch(nodes, anyOf);
            }
        }
    }

    private static int[]? BuildTypeDispatch(SchemaNode[] nodes, ChildRef[] branches)
    {
        // Token types are 0..11; a number token is accepted by number and integer alike.
        int[] table = new int[12];
        table.AsSpan().Fill(-1);
        for (int b = 0; b < branches.Length; b++)
        {
            SchemaNode n = nodes[branches[b].FastNode];
            if (n.AlwaysFalse)
            {
                continue;
            }

            if (n.AlwaysTrue || !n.HasType)
            {
                return null;
            }

            TypeMask mask = n.Type;
            if (!Claim(table, JsonTokenType.String, (mask & TypeMask.String) != 0, b)
                || !Claim(table, JsonTokenType.StartObject, (mask & TypeMask.Object) != 0, b)
                || !Claim(table, JsonTokenType.StartArray, (mask & TypeMask.Array) != 0, b)
                || !Claim(table, JsonTokenType.Number, (mask & (TypeMask.Number | TypeMask.Integer)) != 0, b)
                || !Claim(table, JsonTokenType.True, (mask & TypeMask.Boolean) != 0, b)
                || !Claim(table, JsonTokenType.False, (mask & TypeMask.Boolean) != 0, b)
                || !Claim(table, JsonTokenType.Null, (mask & TypeMask.Null) != 0, b))
            {
                return null;
            }
        }

        return table;

        static bool Claim(int[] table, JsonTokenType token, bool accepts, int branch)
        {
            if (!accepts)
            {
                return true;
            }

            if (table[(int)token] >= 0)
            {
                return false;
            }

            table[(int)token] = branch;
            return true;
        }
    }

    /// <summary>
    /// If every branch is a type-only schema, returns the union of the types (Blaze's AssertionTypeStrictAny).
    /// </summary>
    private TypeMask TypeUnion(ChildRef[] branches, bool requireDisjoint)
    {
        TypeMask union = TypeMask.None;
        foreach (ChildRef branch in branches)
        {
            SchemaNode n = this.nodes[branch.FastNode];
            if (!n.IsTypeOnly || n.Annotations is not null)
            {
                return TypeMask.None;
            }

            TypeMask mask = n.Type;
            if (requireDisjoint)
            {
                // integer and number overlap, so a oneOf of both is not a plain union.
                const TypeMask numeric = TypeMask.Number | TypeMask.Integer;
                if ((union & mask) != 0 || ((union & numeric) != 0 && (mask & numeric) != 0))
                {
                    return TypeMask.None;
                }
            }

            union |= mask;
        }

        return union;
    }

    private Discriminator? BuildDiscriminator(ChildRef[] branches)
    {
        // Candidate property names: those with a const or enum constraint (string, integer or boolean) in the first
        // constrained branch.
        var candidates = new List<byte[]>();
        foreach (ChildRef branch in branches)
        {
            SchemaNode effective = this.EffectiveNode(branch.Node);
            if (effective.Properties is null)
            {
                continue;
            }

            foreach (PropertyEntry entry in effective.Properties.Values)
            {
                if (entry.Schema.IsPresent && this.ClassifyBranch(effective, entry.Name, out _) != BranchKind.Wildcard)
                {
                    candidates.Add(entry.Name);
                }
            }

            break;
        }

        foreach (byte[] name in candidates)
        {
            var kinds = new BranchKind[branches.Length];
            var sets = new List<byte[]>?[branches.Length];
            int constrained = 0;
            for (int i = 0; i < branches.Length; i++)
            {
                kinds[i] = this.ClassifyBranch(this.EffectiveNode(branches[i].Node), name, out sets[i]);
                if (kinds[i] != BranchKind.Wildcard)
                {
                    constrained++;
                }
            }

            if (constrained < 2)
            {
                continue;
            }

            // Collect every value mentioned by any set.
            var values = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < branches.Length; i++)
            {
                if (sets[i] is List<byte[]> set)
                {
                    foreach (byte[] v in set)
                    {
                        values.Add(Encoding.UTF8.GetString(v));
                    }
                }
            }

            var known = new List<KeyValuePair<byte[], int[]>>();
            foreach (string value in values)
            {
                byte[] utf8 = Encoding.UTF8.GetBytes(value);
                var list = new List<int>();
                for (int i = 0; i < branches.Length; i++)
                {
                    bool contains = sets[i] is List<byte[]> set && set.Exists(b => b.AsSpan().SequenceEqual(utf8));
                    bool candidate = kinds[i] switch
                    {
                        BranchKind.Positive => contains,
                        BranchKind.Negative => !contains,
                        _ => true,
                    };
                    if (candidate)
                    {
                        list.Add(i);
                    }
                }

                known.Add(new KeyValuePair<byte[], int[]>(utf8, [.. list]));
            }

            var unknownOrNonString = new List<int>();
            bool allRequire = true;
            for (int i = 0; i < branches.Length; i++)
            {
                if (kinds[i] != BranchKind.Positive)
                {
                    unknownOrNonString.Add(i);
                }

                SchemaNode effective = this.EffectiveNode(branches[i].Node);
                allRequire &= effective.Properties is not null
                    && effective.Properties.TryGetValue(name, out PropertyEntry? pe)
                    && pe.IsRequired;
            }

            int[] all = new int[branches.Length];
            for (int i = 0; i < all.Length; i++)
            {
                all[i] = i;
            }

            return new Discriminator
            {
                PropertyName = name,
                KnownValues = new Utf8NameMap<int[]>(known),
                UnknownString = [.. unknownOrNonString],
                NonString = [.. unknownOrNonString],
                AllBranches = all,
                AllRequire = allRequire,
            };
        }

        return null;
    }

    /// <summary>
    /// Follows pure <c>$ref</c> nodes (no other keywords) to the node that carries constraints.
    /// </summary>
    private SchemaNode EffectiveNode(int id)
    {
        SchemaNode node = this.nodes[id];
        for (int hops = 0; hops < 16 && node.Ref.IsPresent && IsPureRef(node); hops++)
        {
            node = this.nodes[node.Ref.Node];
        }

        return node;
    }

    /// <summary>
    /// Classifies a branch by the constraint its <c>properties[name]</c> schema places on string values.
    /// </summary>
    private BranchKind ClassifyBranch(SchemaNode branch, byte[] name, out List<byte[]>? set)
    {
        set = null;
        if (branch.Properties is null || !branch.Properties.TryGetValue(name, out PropertyEntry? entry) || !entry.Schema.IsPresent)
        {
            return BranchKind.Wildcard;
        }

        SchemaNode property = this.EffectiveNode(entry.Schema.Node);
        if (property.HasConst && TryGetDiscriminatorKey(property.Const, out byte[]? constKey))
        {
            set = [constKey!];
            return BranchKind.Positive;
        }

        if (property.Enum is ConstantValue[] values && values.Length > 0)
        {
            var keys = new List<byte[]>(values.Length);
            foreach (ConstantValue v in values)
            {
                if (!TryGetDiscriminatorKey(v, out byte[]? key))
                {
                    keys = null;
                    break;
                }

                keys.Add(key!);
            }

            if (keys is not null)
            {
                set = keys;
                return BranchKind.Positive;
            }
        }

        // { "type": "string", "not": { "enum": [...] } }: any non-listed string may pass.
        if (property.Not.IsPresent && property.HasType && property.Type == TypeMask.String)
        {
            SchemaNode not = this.EffectiveNode(property.Not.Node);
            if (not.EnumAllStrings && !not.HasType && !not.HasConst && !not.HasStringKeywords && !not.HasInPlaceApplicators)
            {
                set = [];
                foreach (byte[] s in not.EnumStrings!.Keys)
                {
                    set.Add(Tagged(Discriminator.StringTag, s));
                }

                return BranchKind.Negative;
            }
        }

        return BranchKind.Wildcard;
    }

    /// <summary>
    /// The discriminator key of a constant: a tag byte for its kind followed by its text. Strings, booleans and
    /// canonical integers key; other numbers (a fraction or exponent form may equal an integer) and structured values
    /// do not.
    /// </summary>
    internal static bool TryGetDiscriminatorKey(in ConstantValue value, out byte[]? key)
    {
        switch (value.TokenType)
        {
            case JsonTokenType.String:
            {
                using UnescapedUtf8JsonString s = Elements.Create<JsonElement>(value.Document, value.Index).GetUtf8String();
                key = Tagged(Discriminator.StringTag, s.Span);
                return true;
            }

            case JsonTokenType.True:
                key = Tagged(Discriminator.BooleanTag, "true"u8);
                return true;
            case JsonTokenType.False:
                key = Tagged(Discriminator.BooleanTag, "false"u8);
                return true;
            case JsonTokenType.Number:
            {
                ReadOnlySpan<byte> raw = value.Document.GetRawSimpleValue(value.Index).Span;
                if (IsCanonicalInteger(raw))
                {
                    key = Tagged(Discriminator.NumberTag, raw);
                    return true;
                }

                key = null;
                return false;
            }

            default:
                key = null;
                return false;
        }
    }

    internal static bool IsCanonicalInteger(ReadOnlySpan<byte> raw)
    {
        // Optional '-', no leading zero (except "0" itself), digits only.
        int start = raw.Length > 0 && raw[0] == (byte)'-' ? 1 : 0;
        if (raw.Length == start)
        {
            return false;
        }

        for (int i = start; i < raw.Length; i++)
        {
            if (raw[i] < (byte)'0' || raw[i] > (byte)'9')
            {
                return false;
            }
        }

        return !(raw[start] == (byte)'0' && raw.Length > start + 1) && !raw.SequenceEqual("-0"u8);
    }

    private static byte[] Tagged(byte tag, ReadOnlySpan<byte> value)
    {
        byte[] key = new byte[value.Length + 1];
        key[0] = tag;
        value.CopyTo(key.AsSpan(1));
        return key;
    }

    /// <summary>
    /// Adds candidate nodes for every pending dynamic reference from every loaded resource.
    /// </summary>
    /// <returns><see langword="true"/> if any new node was created.</returns>
    private bool ExpandDynamicRefs()
    {
        bool added = false;
        foreach (PendingDynamicRef pending in this.pendingDynamicRefs)
        {
            foreach (SchemaResource resource in this.loader.Resources)
            {
                if (!pending.SeenResources.Add(resource.Id))
                {
                    continue;
                }

                int index;
                if (pending.IsRecursive)
                {
                    if (!resource.RecursiveAnchor)
                    {
                        continue;
                    }

                    index = resource.RootIndex;
                }
                else
                {
                    if (resource.DynamicAnchors is null || !resource.DynamicAnchors.TryGetValue(pending.Anchor, out index))
                    {
                        continue;
                    }
                }

                int before = this.nodes.Count;
                int nodeId = this.GetNode(new SchemaTarget(resource.Document, index, resource));
                added |= this.nodes.Count != before;
                pending.Candidates.Add((resource.Id, nodeId));
            }
        }

        return added;
    }

    private void FinalizeDynamicRefs()
    {
        int resourceCount = this.loader.Resources.Count;
        Dictionary<int, bool[]>? reachableFromEntry = null;
        foreach (PendingDynamicRef pending in this.pendingDynamicRefs)
        {
            SchemaNode node = this.nodes[pending.NodeId];
            int fallback = this.GetNode(pending.InitialTarget);
            if (pending.Candidates.Count <= 1)
            {
                // Only the initial target's resource defines the anchor: resolution is static.
                node.Ref = new ChildRef(fallback, pending.PathSegment);
                node.DynamicRef = null;
                node.HasInPlaceApplicators = true;
                continue;
            }

            // The dynamic scope is searched outermost-first and its outermost entry is always the resource evaluation
            // started in. When every entry resource that can reach this reference defines the anchor, and with the
            // same target, that target is the answer on every path, so the reference is static after all (the
            // strict-tree shape). Entries that cannot reach it never evaluate it, so a program with many entry points
            // (one per generated type) is judged per reference, not as a whole.
            reachableFromEntry ??= this.ComputeReachabilityFromEntries();
            if (this.TryGetUniformEntryTarget(pending, reachableFromEntry, out int uniform))
            {
                node.Ref = new ChildRef(uniform, pending.PathSegment);
                node.DynamicRef = null;
                node.HasInPlaceApplicators = true;
                continue;
            }

            // Promoted (or re-finalised) to a dynamic reference: undo any earlier static demotion.
            if (node.Ref.IsPresent && node.Ref.Path == pending.PathSegment)
            {
                node.Ref = ChildRef.None;
            }

            int[] table = new int[resourceCount];
            table.AsSpan().Fill(-1);
            foreach ((int resourceId, int nodeId) in pending.Candidates)
            {
                table[resourceId] = nodeId;
            }

            node.DynamicRef = new DynamicRefTarget
            {
                Anchor = pending.Anchor,
                FallbackNode = fallback,
                NodeByResource = table,
                NodeByEntryResource = this.TryGetEntryResolvedTable(pending, reachableFromEntry, resourceCount),
                PathSegment = pending.PathSegment,
                IsRecursive = pending.IsRecursive,
            };
            node.HasInPlaceApplicators = true;
        }

        // Nodes may have been created by GetNode above (fallbacks); they are already compiled
        // because every target of a pending ref was compiled as a candidate or reachable.
        while (this.worklist.Count > 0)
        {
            int id = this.worklist.Dequeue();
            this.CompileNode(this.nodes[id], this.targets[id]);
        }
    }

    /// <summary>
    /// Computes, for each entry node, the set of nodes reachable from it through any child, counting every candidate
    /// of a dynamic reference (finalised or still pending) as a child.
    /// </summary>
    private Dictionary<int, bool[]> ComputeReachabilityFromEntries()
    {
        var pendingChildren = new Dictionary<int, List<int>>();
        foreach (PendingDynamicRef pending in this.pendingDynamicRefs)
        {
            if (!pendingChildren.TryGetValue(pending.NodeId, out List<int>? list))
            {
                list = [];
                pendingChildren.Add(pending.NodeId, list);
            }

            list.Add(this.GetNode(pending.InitialTarget));
            foreach ((int _, int nodeId) in pending.Candidates)
            {
                list.Add(nodeId);
            }
        }

        var result = new Dictionary<int, bool[]>();
        var stack = new Stack<int>();
        var children = new List<int>();
        foreach (int entry in this.entryNodeIds)
        {
            bool[] reached = new bool[this.nodes.Count];
            reached[entry] = true;
            stack.Push(entry);
            while (stack.Count > 0)
            {
                int id = stack.Pop();
                children.Clear();
                this.nodes[id].CollectChildren(children);
                if (pendingChildren.TryGetValue(id, out List<int>? extra))
                {
                    children.AddRange(extra);
                }

                foreach (int child in children)
                {
                    if (!reached[child])
                    {
                        reached[child] = true;
                        stack.Push(child);
                    }
                }
            }

            result[entry] = reached;
        }

        return result;
    }

    /// <summary>
    /// When every entry that can reach the reference starts in a resource that defines the anchor, the outermost
    /// scope decides on every path, so the target depends on the entry resource alone (the strict-tree shape with an
    /// entry point per generated type, including one in the inner resource). Returns the table indexed by entry
    /// resource id, or null when some reaching entry's resource lacks the anchor and the scope must be kept.
    /// </summary>
    private int[]? TryGetEntryResolvedTable(PendingDynamicRef pending, Dictionary<int, bool[]> reachableFromEntry, int resourceCount)
    {
        int[]? table = null;
        foreach (KeyValuePair<int, bool[]> entry in reachableFromEntry)
        {
            if (!entry.Value[pending.NodeId])
            {
                continue;
            }

            int entryResource = this.nodes[entry.Key].ResourceId;
            int candidate = -1;
            foreach ((int resourceId, int nodeId) in pending.Candidates)
            {
                if (resourceId == entryResource)
                {
                    candidate = nodeId;
                    break;
                }
            }

            if (candidate < 0)
            {
                return null;
            }

            if (table is null)
            {
                table = new int[resourceCount];
                table.AsSpan().Fill(-1);
            }

            table[entryResource] = candidate;
        }

        return table;
    }

    private bool TryGetUniformEntryTarget(PendingDynamicRef pending, Dictionary<int, bool[]> reachableFromEntry, out int target)
    {
        target = -1;
        bool anyEntry = false;
        foreach (KeyValuePair<int, bool[]> entry in reachableFromEntry)
        {
            if (!entry.Value[pending.NodeId])
            {
                continue;
            }

            anyEntry = true;
            int entryResource = this.nodes[entry.Key].ResourceId;
            int candidate = -1;
            foreach ((int resourceId, int nodeId) in pending.Candidates)
            {
                if (resourceId == entryResource)
                {
                    candidate = nodeId;
                    break;
                }
            }

            if (candidate < 0 || (target >= 0 && target != candidate))
            {
                target = -1;
                return false;
            }

            target = candidate;
        }

        return anyEntry;
    }

    /// <summary>
    /// Computes which nodes can contribute evaluated-property/item annotations, so that in-place applicators
    /// need no scratch bitset for children that cannot mark anything.
    /// </summary>
    private void ComputeMarking()
    {
        bool changed = true;
        foreach (SchemaNode n in this.nodes)
        {
            n.MarksProperties = n.Properties is not null || n.PatternProperties is not null || n.AdditionalProperties.IsPresent || n.UnevaluatedProperties.IsPresent;
            n.MarksItems = n.PrefixItems is not null || n.Items.IsPresent || n.Contains.IsPresent || n.UnevaluatedItems.IsPresent;
        }

        while (changed)
        {
            changed = false;
            foreach (SchemaNode n in this.nodes)
            {
                foreach (int child in n.InPlaceChildren(includeNot: false))
                {
                    SchemaNode c = this.nodes[child];
                    if (c.MarksProperties && !n.MarksProperties)
                    {
                        n.MarksProperties = true;
                        changed = true;
                    }

                    if (c.MarksItems && !n.MarksItems)
                    {
                        n.MarksItems = true;
                        changed = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// A node allocates an evaluated-property/item bitset on entry only when it consumes one itself
    /// (<c>unevaluatedProperties</c>/<c>unevaluatedItems</c>). In-place children receive the parent's bitset (or a
    /// scratch copy) through the call, so they need no flag of their own; a node reached with an empty bitset has
    /// no consumer above it on this instance and skips the marking entirely.
    /// </summary>
    private void ComputeTracking()
    {
        foreach (SchemaNode n in this.nodes)
        {
            n.TracksProperties = n.UnevaluatedProperties.IsPresent;
            n.TracksItems = n.UnevaluatedItems.IsPresent;
        }
    }

    private SchemaTarget ChildTarget(SchemaTarget parent, in JsonElement child)
    {
        int index = Elements.Index(child);
        SchemaResource resource = this.loader.TryGetResourceOf(parent.Document, index, out SchemaResource? r) ? r : parent.Resource;
        return new SchemaTarget(parent.Document, index, resource);
    }

    private ChildRef Child(SchemaTarget parent, in JsonElement child, byte[] segment)
    {
        return new ChildRef(this.GetNode(this.ChildTarget(parent, child)), segment);
    }

    private void CompileNode(SchemaNode node, SchemaTarget target)
    {
        JsonElement element = target.Element;
        switch (element.ValueKind)
        {
            case JsonValueKind.True:
                node.AlwaysTrue = true;
                return;
            case JsonValueKind.False:
                node.AlwaysFalse = true;
                return;
            case JsonValueKind.Object:
                break;
            default:
                node.AlwaysTrue = true;
                return;
        }

        JsonSchemaDialect dialect = target.Resource.Dialect;
        JsonSchemaVocabularies vocab = target.Resource.Vocabularies;
        bool legacy = dialect <= JsonSchemaDialect.Draft7;

        if (legacy && element.TryGetProperty("$ref"u8, out JsonElement legacyRef) && legacyRef.ValueKind == JsonValueKind.String)
        {
            // In Draft 7 and earlier, $ref replaces every sibling keyword.
            this.CompileRef(node, target, legacyRef.GetString()!);
            return;
        }

        bool applicator = legacy || (vocab & JsonSchemaVocabularies.Applicator) != 0;
        bool validation = legacy || (vocab & JsonSchemaVocabularies.Validation) != 0;
        bool metaData = legacy || (vocab & JsonSchemaVocabularies.MetaData) != 0;
        bool unevaluated = dialect == JsonSchemaDialect.Draft201909 ? applicator : (vocab & JsonSchemaVocabularies.Unevaluated) != 0;
        bool content = legacy || (vocab & JsonSchemaVocabularies.Content) != 0;
        bool formatAssert = this.options.AssertFormat ?? ((legacy && this.options.AssertFormatInLegacyDrafts) || (vocab & JsonSchemaVocabularies.FormatAssertion) != 0);
        bool formatAnnotate = legacy || (vocab & (JsonSchemaVocabularies.FormatAnnotation | JsonSchemaVocabularies.FormatAssertion)) != 0 || this.options.AssertFormat is not null;

        Dictionary<string, PropertyEntry>? propertyEntries = null;
        List<AnnotationEntry>? annotations = null;
        List<DependencyEntry>? dependencies = null;
        JsonElement? exclusiveMaximumElement = null;
        JsonElement? exclusiveMinimumElement = null;
        JsonElement? maximumElement = null;
        JsonElement? minimumElement = null;
        JsonElement? itemsElement = null;
        JsonElement? additionalItemsElement = null;
        JsonElement? prefixItemsElement = null;
        JsonElement? minContainsElement = null;
        JsonElement? maxContainsElement = null;
        JsonElement? contentEncodingElement = null;
        JsonElement? contentMediaTypeElement = null;
        JsonElement? contentSchemaElement = null;
        bool anyContent = false;

        PropertyEntry Entry(ReadOnlySpan<byte> name)
        {
            propertyEntries ??= new Dictionary<string, PropertyEntry>(StringComparer.Ordinal);
            string key = Encoding.UTF8.GetString(name);
            if (!propertyEntries.TryGetValue(key, out PropertyEntry? entry))
            {
                entry = new PropertyEntry { Name = name.ToArray() };
                propertyEntries[key] = entry;
            }

            return entry;
        }

        int SeenBit(ReadOnlySpan<byte> name)
        {
            PropertyEntry e = Entry(name);
            if (e.SeenBit < 0)
            {
                e.SeenBit = node.SeenBitCount++;
            }

            return e.SeenBit;
        }

        int SeenBitArray(byte[] name) => SeenBit(name);

        void Annotate(ReadOnlySpan<byte> keyword, in JsonElement value, bool stringsOnly = false)
        {
            annotations ??= [];
            annotations.Add(new AnnotationEntry { Keyword = keyword.ToArray(), RawJson = RawJson(value), StringsOnly = stringsOnly });
        }

        foreach (JsonProperty<JsonElement> property in element.EnumerateObject())
        {
            ReadOnlySpan<byte> name = property.Utf8NameSpan.Span;
            JsonElement value = property.Value;

            switch (name.Length)
            {
                case 2 when name.SequenceEqual("if"u8):
                    if (applicator && dialect >= JsonSchemaDialect.Draft7)
                    {
                        node.If = this.Child(target, value, Segment("if"));
                    }

                    continue;
                case 3 when name.SequenceEqual("not"u8):
                    if (applicator)
                    {
                        node.Not = this.Child(target, value, Segment("not"));
                    }

                    continue;
                case 4:
                    if (name.SequenceEqual("type"u8))
                    {
                        if (validation)
                        {
                            this.CompileType(node, value);
                        }
                    }
                    else if (name.SequenceEqual("enum"u8))
                    {
                        if (validation && value.ValueKind == JsonValueKind.Array)
                        {
                            this.CompileEnum(node, target, value);
                        }
                    }
                    else if (name.SequenceEqual("$ref"u8))
                    {
                        if (value.ValueKind == JsonValueKind.String)
                        {
                            this.CompileRef(node, target, value.GetString()!);
                        }
                    }
                    else if (name.SequenceEqual("then"u8))
                    {
                        if (applicator && dialect >= JsonSchemaDialect.Draft7)
                        {
                            node.Then = this.Child(target, value, Segment("then"));
                        }
                    }
                    else if (name.SequenceEqual("else"u8))
                    {
                        if (applicator && dialect >= JsonSchemaDialect.Draft7)
                        {
                            node.Else = this.Child(target, value, Segment("else"));
                        }
                    }

                    continue;
                case 5:
                    if (name.SequenceEqual("const"u8))
                    {
                        if (validation && dialect >= JsonSchemaDialect.Draft6)
                        {
                            this.CompileConst(node, target, value);
                        }
                    }
                    else if (name.SequenceEqual("items"u8))
                    {
                        itemsElement = value;
                    }
                    else if (name.SequenceEqual("allOf"u8))
                    {
                        if (applicator)
                        {
                            node.AllOf = this.ChildArray(target, value, "allOf");
                        }
                    }
                    else if (name.SequenceEqual("anyOf"u8))
                    {
                        if (applicator)
                        {
                            node.AnyOf = this.ChildArray(target, value, "anyOf");
                        }
                    }
                    else if (name.SequenceEqual("oneOf"u8))
                    {
                        if (applicator)
                        {
                            node.OneOf = this.ChildArray(target, value, "oneOf");
                        }
                    }
                    else if (name.SequenceEqual("title"u8))
                    {
                        if (metaData)
                        {
                            Annotate(name, value);
                        }
                    }
                    else if (!name.SequenceEqual("$defs"u8))
                    {
                        goto default;
                    }

                    continue;
                case 6:
                    if (name.SequenceEqual("format"u8))
                    {
                        if (value.ValueKind == JsonValueKind.String)
                        {
                            string formatName = value.GetString()!;
                            node.Format = GetFormatKind(formatName, dialect);
                            JsonSchemaFormatMode? formatMode = this.ResolveFormatMode(formatName);
                            node.AssertFormat = formatMode switch
                            {
                                JsonSchemaFormatMode.Assert => true,
                                JsonSchemaFormatMode.Warning => true,
                                JsonSchemaFormatMode.Disable => false,
                                _ => formatAssert,
                            };
                            node.WarnFormat = formatMode == JsonSchemaFormatMode.Warning && !FormatKinds.IsNumeric(node.Format);
                            if (node.AssertFormat && node.Format != FormatKind.None && node.Format != FormatKind.Unknown)
                            {
                                if (FormatKinds.IsNumeric(node.Format))
                                {
                                    node.HasNumberKeywords = true;
                                }
                                else
                                {
                                    node.HasStringKeywords = true;
                                }
                            }

                            if (formatAnnotate)
                            {
                                Annotate(name, value);
                            }
                        }
                    }
                    else
                    {
                        goto default;
                    }

                    continue;
                case 7:
                    if (name.SequenceEqual("pattern"u8))
                    {
                        if (validation && value.ValueKind == JsonValueKind.String)
                        {
                            node.Pattern = PatternMatcher.Create(value.GetString()!, this.options);
                            node.HasStringKeywords = true;
                        }
                    }
                    else if (name.SequenceEqual("maximum"u8))
                    {
                        maximumElement = value;
                    }
                    else if (name.SequenceEqual("minimum"u8))
                    {
                        minimumElement = value;
                    }
                    else if (name.SequenceEqual("default"u8))
                    {
                        if (metaData)
                        {
                            Annotate(name, value);
                        }
                    }
                    else if (!name.SequenceEqual("$schema"u8) && !name.SequenceEqual("$anchor"u8))
                    {
                        goto default;
                    }

                    continue;
                case 8:
                    if (name.SequenceEqual("required"u8))
                    {
                        if (validation && value.ValueKind == JsonValueKind.Array)
                        {
                            var bits = new List<int>();
                            var names = new List<byte[]>();
                            foreach (JsonElement r in value.EnumerateArray())
                            {
                                if (r.ValueKind == JsonValueKind.String)
                                {
                                    using UnescapedUtf8JsonString s = r.GetUtf8String();
                                    bits.Add(SeenBit(s.Span));
                                    names.Add(s.Span.ToArray());
                                    Entry(s.Span).IsRequired = true;
                                }
                            }

                            node.RequiredSeenBits = [.. bits];
                            node.RequiredNames = [.. names];
                            node.HasObjectKeywords = true;
                        }
                    }
                    else if (name.SequenceEqual("contains"u8))
                    {
                        if (applicator && dialect >= JsonSchemaDialect.Draft6)
                        {
                            node.Contains = this.Child(target, value, Segment("contains"));
                            node.HasArrayKeywords = true;
                        }
                    }
                    else if (name.SequenceEqual("maxItems"u8))
                    {
                        if (validation)
                        {
                            node.MaxItems = GetInt(value, -1);
                            node.HasArrayKeywords = true;
                        }
                    }
                    else if (name.SequenceEqual("minItems"u8))
                    {
                        if (validation)
                        {
                            node.MinItems = GetInt(value, -1);
                            node.HasArrayKeywords = true;
                        }
                    }
                    else if (name.SequenceEqual("examples"u8))
                    {
                        if (metaData && dialect >= JsonSchemaDialect.Draft6)
                        {
                            Annotate(name, value);
                        }
                    }
                    else if (name.SequenceEqual("readOnly"u8))
                    {
                        if (metaData && dialect >= JsonSchemaDialect.Draft7)
                        {
                            Annotate(name, value);
                        }
                    }
                    else if (!name.SequenceEqual("$comment"u8))
                    {
                        goto default;
                    }

                    continue;
                case 9:
                    if (name.SequenceEqual("maxLength"u8))
                    {
                        if (validation)
                        {
                            node.MaxLength = GetInt(value, -1);
                            node.HasStringKeywords = true;
                        }
                    }
                    else if (name.SequenceEqual("minLength"u8))
                    {
                        if (validation)
                        {
                            node.MinLength = GetInt(value, -1);
                            node.HasStringKeywords = true;
                        }
                    }
                    else if (name.SequenceEqual("writeOnly"u8))
                    {
                        if (metaData && dialect >= JsonSchemaDialect.Draft7)
                        {
                            Annotate(name, value);
                        }
                    }
                    else
                    {
                        goto default;
                    }

                    continue;
                case 10:
                    if (name.SequenceEqual("properties"u8))
                    {
                        if (applicator && value.ValueKind == JsonValueKind.Object)
                        {
                            foreach (JsonProperty<JsonElement> p in value.EnumerateObject())
                            {
                                ReadOnlySpan<byte> pn = p.Utf8NameSpan.Span;
                                PropertyEntry entry = Entry(pn);
                                entry.Schema = this.Child(target, p.Value, Segment("properties", pn));
                            }

                            node.HasObjectKeywords = true;
                        }
                    }
                    else if (name.SequenceEqual("multipleOf"u8))
                    {
                        if (validation && value.ValueKind == JsonValueKind.Number)
                        {
                            node.MultipleOf = new DivisorValue(Elements.Document(value).GetRawSimpleValue(Elements.Index(value)).Span);
                            node.HasNumberKeywords = true;
                        }
                    }
                    else if (name.SequenceEqual("deprecated"u8))
                    {
                        if (metaData && dialect >= JsonSchemaDialect.Draft201909)
                        {
                            Annotate(name, value);
                        }
                    }
                    else
                    {
                        goto default;
                    }

                    continue;
                case 11:
                    if (name.SequenceEqual("uniqueItems"u8))
                    {
                        if (validation && value.ValueKind == JsonValueKind.True)
                        {
                            node.UniqueItems = true;
                            node.HasArrayKeywords = true;
                        }
                    }
                    else if (name.SequenceEqual("prefixItems"u8))
                    {
                        prefixItemsElement = value;
                    }
                    else if (name.SequenceEqual("minContains"u8))
                    {
                        minContainsElement = value;
                    }
                    else if (name.SequenceEqual("maxContains"u8))
                    {
                        maxContainsElement = value;
                    }
                    else if (name.SequenceEqual("description"u8))
                    {
                        if (metaData)
                        {
                            Annotate(name, value);
                        }
                    }
                    else if (name.SequenceEqual("$vocabulary"u8) || name.SequenceEqual("definitions"u8))
                    {
                    }
                    else if (name.SequenceEqual("$dynamicRef"u8))
                    {
                        if (dialect >= JsonSchemaDialect.Draft202012 && value.ValueKind == JsonValueKind.String)
                        {
                            this.CompileDynamicRef(node, target, value.GetString()!, isRecursive: false);
                        }
                    }
                    else
                    {
                        goto default;
                    }

                    continue;
                case 12:
                    if (name.SequenceEqual("dependencies"u8))
                    {
                        // Honoured in every dialect: in 2019-09+ it is an optional compatibility keyword.
                        if (applicator && value.ValueKind == JsonValueKind.Object)
                        {
                            dependencies ??= [];
                            foreach (JsonProperty<JsonElement> p in value.EnumerateObject())
                            {
                                ReadOnlySpan<byte> pn = p.Utf8NameSpan.Span;
                                var dep = new DependencyEntry { Name = pn.ToArray(), NameText = System.Text.Encoding.UTF8.GetString(pn), SeenBit = SeenBit(pn) };
                                if (p.Value.ValueKind == JsonValueKind.Array)
                                {
                                    this.FillRequiredDependency(dep, p.Value, SeenBitArray);
                                }
                                else
                                {
                                    dep.Schema = this.Child(target, p.Value, Segment("dependencies", pn));
                                }

                                dependencies.Add(dep);
                            }

                            node.HasObjectKeywords = true;
                        }
                    }
                    else
                    {
                        goto default;
                    }

                    continue;
                case 13:
                    if (name.SequenceEqual("propertyNames"u8))
                    {
                        if (applicator && dialect >= JsonSchemaDialect.Draft6)
                        {
                            node.PropertyNames = this.Child(target, value, Segment("propertyNames"));
                            node.HasObjectKeywords = true;
                        }
                    }
                    else if (name.SequenceEqual("maxProperties"u8))
                    {
                        if (validation)
                        {
                            node.MaxProperties = GetInt(value, -1);
                            node.HasObjectKeywords = true;
                        }
                    }
                    else if (name.SequenceEqual("minProperties"u8))
                    {
                        if (validation)
                        {
                            node.MinProperties = GetInt(value, -1);
                            node.HasObjectKeywords = true;
                        }
                    }
                    else if (name.SequenceEqual("contentSchema"u8))
                    {
                        contentSchemaElement = value;
                        anyContent = true;
                    }
                    else if (name.SequenceEqual("$recursiveRef"u8))
                    {
                        if (dialect == JsonSchemaDialect.Draft201909 && value.ValueKind == JsonValueKind.String)
                        {
                            this.CompileDynamicRef(node, target, value.GetString()!, isRecursive: true);
                        }
                    }
                    else
                    {
                        goto default;
                    }

                    continue;
                case 14:
                    if (!name.SequenceEqual("$dynamicAnchor"u8))
                    {
                        goto default;
                    }

                    continue;
                case 15:
                    if (name.SequenceEqual("contentEncoding"u8))
                    {
                        contentEncodingElement = value;
                        anyContent = true;
                    }
                    else if (name.SequenceEqual("additionalItems"u8))
                    {
                        additionalItemsElement = value;
                    }
                    else
                    {
                        goto default;
                    }

                    continue;
                case 16:
                    if (name.SequenceEqual("exclusiveMaximum"u8))
                    {
                        exclusiveMaximumElement = value;
                    }
                    else if (name.SequenceEqual("exclusiveMinimum"u8))
                    {
                        exclusiveMinimumElement = value;
                    }
                    else if (name.SequenceEqual("unevaluatedItems"u8))
                    {
                        if (unevaluated && dialect >= JsonSchemaDialect.Draft201909)
                        {
                            node.UnevaluatedItems = this.Child(target, value, Segment("unevaluatedItems"));
                            node.HasArrayKeywords = true;
                        }
                    }
                    else if (name.SequenceEqual("contentMediaType"u8))
                    {
                        contentMediaTypeElement = value;
                        anyContent = true;
                    }
                    else if (name.SequenceEqual("$recursiveAnchor"u8))
                    {
                    }
                    else if (name.SequenceEqual("dependentSchemas"u8))
                    {
                        if (applicator && dialect >= JsonSchemaDialect.Draft201909 && value.ValueKind == JsonValueKind.Object)
                        {
                            dependencies ??= [];
                            foreach (JsonProperty<JsonElement> p in value.EnumerateObject())
                            {
                                ReadOnlySpan<byte> pn = p.Utf8NameSpan.Span;
                                dependencies.Add(new DependencyEntry
                                {
                                    Name = pn.ToArray(),
                                    NameText = System.Text.Encoding.UTF8.GetString(pn),
                                    SeenBit = SeenBit(pn),
                                    Schema = this.Child(target, p.Value, Segment("dependentSchemas", pn)),
                                });
                            }

                            node.HasObjectKeywords = true;
                        }
                    }
                    else
                    {
                        goto default;
                    }

                    continue;
                case 17:
                    if (name.SequenceEqual("patternProperties"u8))
                    {
                        if (applicator && value.ValueKind == JsonValueKind.Object)
                        {
                            var list = new List<PatternPropertyEntry>();
                            foreach (JsonProperty<JsonElement> p in value.EnumerateObject())
                            {
                                ReadOnlySpan<byte> pn = p.Utf8NameSpan.Span;
                                list.Add(new PatternPropertyEntry
                                {
                                    Name = pn.ToArray(),
                                    Matcher = PatternMatcher.Create(p.Name, this.options),
                                    Schema = this.Child(target, p.Value, Segment("patternProperties", pn)),
                                });
                            }

                            node.PatternProperties = [.. list];
                            node.HasObjectKeywords = true;
                        }
                    }
                    else if (name.SequenceEqual("dependentRequired"u8))
                    {
                        if (validation && dialect >= JsonSchemaDialect.Draft201909 && value.ValueKind == JsonValueKind.Object)
                        {
                            dependencies ??= [];
                            foreach (JsonProperty<JsonElement> p in value.EnumerateObject())
                            {
                                ReadOnlySpan<byte> pn = p.Utf8NameSpan.Span;
                                var dep = new DependencyEntry { Name = pn.ToArray(), NameText = System.Text.Encoding.UTF8.GetString(pn), SeenBit = SeenBit(pn) };
                                if (p.Value.ValueKind == JsonValueKind.Array)
                                {
                                    this.FillRequiredDependency(dep, p.Value, SeenBitArray);
                                }

                                dependencies.Add(dep);
                            }

                            node.HasObjectKeywords = true;
                        }
                    }
                    else
                    {
                        goto default;
                    }

                    continue;
                case 20:
                    if (name.SequenceEqual("additionalProperties"u8))
                    {
                        if (applicator)
                        {
                            node.AdditionalProperties = this.Child(target, value, Segment("additionalProperties"));
                            node.HasObjectKeywords = true;
                        }
                    }
                    else
                    {
                        goto default;
                    }

                    continue;
                case 21:
                    if (name.SequenceEqual("unevaluatedProperties"u8))
                    {
                        if (unevaluated && dialect >= JsonSchemaDialect.Draft201909)
                        {
                            node.UnevaluatedProperties = this.Child(target, value, Segment("unevaluatedProperties"));
                            node.HasObjectKeywords = true;
                        }
                    }
                    else
                    {
                        goto default;
                    }

                    continue;
                default:
                    if (name.SequenceEqual("id"u8) || name.SequenceEqual("$id"u8))
                    {
                        continue;
                    }

                    // Unknown keywords are collected as annotations from 2019-09 onwards.
                    if (dialect >= JsonSchemaDialect.Draft201909)
                    {
                        Annotate(name, value);
                    }

                    continue;
            }
        }

        // Numeric bounds (dialect-aware exclusive handling).
        if (validation)
        {
            if (dialect == JsonSchemaDialect.Draft4)
            {
                bool exclusiveMax = exclusiveMaximumElement is JsonElement em && em.ValueKind == JsonValueKind.True;
                bool exclusiveMin = exclusiveMinimumElement is JsonElement en && en.ValueKind == JsonValueKind.True;
                if (maximumElement is JsonElement max && max.ValueKind == JsonValueKind.Number)
                {
                    var v = new NumberValue(Elements.Document(max).GetRawSimpleValue(Elements.Index(max)).Span);
                    if (exclusiveMax)
                    {
                        node.ExclusiveMaximum = v;
                    }
                    else
                    {
                        node.Maximum = v;
                    }

                    node.HasNumberKeywords = true;
                }

                if (minimumElement is JsonElement min && min.ValueKind == JsonValueKind.Number)
                {
                    var v = new NumberValue(Elements.Document(min).GetRawSimpleValue(Elements.Index(min)).Span);
                    if (exclusiveMin)
                    {
                        node.ExclusiveMinimum = v;
                    }
                    else
                    {
                        node.Minimum = v;
                    }

                    node.HasNumberKeywords = true;
                }
            }
            else
            {
                if (maximumElement is JsonElement max && max.ValueKind == JsonValueKind.Number)
                {
                    node.Maximum = new NumberValue(Elements.Document(max).GetRawSimpleValue(Elements.Index(max)).Span);
                    node.HasNumberKeywords = true;
                }

                if (minimumElement is JsonElement min && min.ValueKind == JsonValueKind.Number)
                {
                    node.Minimum = new NumberValue(Elements.Document(min).GetRawSimpleValue(Elements.Index(min)).Span);
                    node.HasNumberKeywords = true;
                }

                if (exclusiveMaximumElement is JsonElement emax && emax.ValueKind == JsonValueKind.Number)
                {
                    node.ExclusiveMaximum = new NumberValue(Elements.Document(emax).GetRawSimpleValue(Elements.Index(emax)).Span);
                    node.HasNumberKeywords = true;
                }

                if (exclusiveMinimumElement is JsonElement emin && emin.ValueKind == JsonValueKind.Number)
                {
                    node.ExclusiveMinimum = new NumberValue(Elements.Document(emin).GetRawSimpleValue(Elements.Index(emin)).Span);
                    node.HasNumberKeywords = true;
                }
            }
        }

        // Array applicators.
        if (applicator)
        {
            if (dialect >= JsonSchemaDialect.Draft202012)
            {
                if (prefixItemsElement is JsonElement prefix && prefix.ValueKind == JsonValueKind.Array)
                {
                    node.PrefixItems = this.ChildArray(target, prefix, "prefixItems");
                    node.HasArrayKeywords = true;
                }

                if (itemsElement is JsonElement items && items.ValueKind != JsonValueKind.Array)
                {
                    node.Items = this.Child(target, items, Segment("items"));
                    node.HasArrayKeywords = true;
                }
            }
            else
            {
                if (itemsElement is JsonElement items)
                {
                    if (items.ValueKind == JsonValueKind.Array)
                    {
                        node.PrefixItems = this.ChildArray(target, items, "items");
                        node.HasArrayKeywords = true;
                        if (additionalItemsElement is JsonElement additional)
                        {
                            node.Items = this.Child(target, additional, Segment("additionalItems"));
                        }
                    }
                    else
                    {
                        node.Items = this.Child(target, items, Segment("items"));
                        node.HasArrayKeywords = true;
                    }
                }
            }

            node.ContainsMarksEvaluated = dialect >= JsonSchemaDialect.Draft202012;
        }

        if (validation && dialect >= JsonSchemaDialect.Draft201909)
        {
            if (minContainsElement is JsonElement minc)
            {
                node.MinContains = GetInt(minc, 1);
            }

            if (maxContainsElement is JsonElement maxc)
            {
                node.MaxContains = GetInt(maxc, -1);
            }
        }

        // Content keywords.
        if (anyContent && content && dialect >= JsonSchemaDialect.Draft7)
        {
            bool assert = dialect == JsonSchemaDialect.Draft7 && this.options.AssertContent;
            bool base64 = contentEncodingElement is JsonElement ce && ce.ValueKind == JsonValueKind.String && ce.ValueEquals("base64"u8);
            bool json = contentMediaTypeElement is JsonElement cm && cm.ValueKind == JsonValueKind.String && cm.ValueEquals("application/json"u8);
            node.Content = (base64, json) switch
            {
                (true, true) => ContentKind.Base64Json,
                (true, false) => ContentKind.Base64,
                (false, true) => ContentKind.Json,
                _ => ContentKind.None,
            };
            node.AssertContent = assert && node.Content != ContentKind.None;
            if (node.AssertContent)
            {
                node.HasStringKeywords = true;
            }

            if (contentEncodingElement is JsonElement cee)
            {
                Annotate("contentEncoding"u8, cee, stringsOnly: true);
            }

            if (contentMediaTypeElement is JsonElement cme)
            {
                Annotate("contentMediaType"u8, cme, stringsOnly: true);

                // contentSchema is only meaningful alongside contentMediaType.
                if (contentSchemaElement is JsonElement cse && dialect >= JsonSchemaDialect.Draft201909)
                {
                    Annotate("contentSchema"u8, cse, stringsOnly: true);
                }
            }
        }

        if (node.Format != FormatKind.None && node.AssertFormat)
        {
            node.HasStringKeywords = true;
        }

        if (node.MultipleOf is not null || node.Minimum is not null || node.Maximum is not null || node.ExclusiveMinimum is not null || node.ExclusiveMaximum is not null)
        {
            node.HasNumberKeywords = true;
        }

        if (propertyEntries is not null)
        {
            var list = new List<KeyValuePair<byte[], PropertyEntry>>(propertyEntries.Count);
            foreach (PropertyEntry e in propertyEntries.Values)
            {
                list.Add(new KeyValuePair<byte[], PropertyEntry>(e.Name, e));
            }

            node.Properties = new Utf8NameMap<PropertyEntry>(list);
            node.HasObjectKeywords = true;

            // Small objects with mostly required properties and no other property-driven keywords are
            // cheaper to check by direct lookup than by enumerating the instance (as Blaze unrolls them).
            int requiredCount = 0;
            foreach (PropertyEntry e in propertyEntries.Values)
            {
                if (e.IsRequired)
                {
                    requiredCount++;
                }
            }

            if (!DisableUnroll
                && propertyEntries.Count <= 6
                && (requiredCount == propertyEntries.Count || propertyEntries.Count <= 2)
                && node.PatternProperties is null
                && !node.AdditionalProperties.IsPresent
                && !node.PropertyNames.IsPresent
                && dependencies is null)
            {
                var unrolled = new PropertyEntry[propertyEntries.Count];
                int u = 0;
                foreach (PropertyEntry e in propertyEntries.Values)
                {
                    if (e.IsRequired)
                    {
                        unrolled[u++] = e;
                    }
                }

                foreach (PropertyEntry e in propertyEntries.Values)
                {
                    if (!e.IsRequired)
                    {
                        unrolled[u++] = e;
                    }
                }

                node.UnrolledProperties = unrolled;
            }
        }

        node.HasSeenBits = node.SeenBitCount > 0;
        node.Dependencies = dependencies is null ? null : [.. dependencies];
        node.Annotations = annotations is null ? null : [.. annotations];
        node.HasInPlaceApplicators = node.Ref.IsPresent || node.DynamicRef is not null || node.AllOf is not null || node.AnyOf is not null || node.OneOf is not null || node.Not.IsPresent || node.If.IsPresent || node.Then.IsPresent || node.Else.IsPresent;
        if (node.Dependencies is not null)
        {
            foreach (DependencyEntry d in node.Dependencies)
            {
                if (d.Schema.IsPresent)
                {
                    node.HasInPlaceApplicators = true;
                }
            }
        }

        node.HasNumberKeywords |= node.HasType && (node.Type & TypeMask.Integer) != 0 && (node.Type & TypeMask.Number) == 0;
    }

    /// <summary>
    /// Computes the leaf flags. This must run after dynamic references are finalised, because a node whose
    /// only keyword is <c>$dynamicRef</c>/<c>$recursiveRef</c> gains its applicator late.
    /// </summary>
    /// <summary>
    /// Marks every node that lies on a cycle of in-place applicators ($ref, $dynamicRef, allOf/anyOf/oneOf, not,
    /// if/then/else, dependent schemas). Only such nodes can recurse without consuming the instance, so only entering
    /// them needs the runaway depth guard; every other in-place edge is bounded by the size of the graph.
    /// Strongly connected components by iterative Tarjan, so deep graphs do not recurse on the CLR stack.
    /// </summary>
    private void ComputeInPlaceCycles()
    {
        int count = this.nodes.Count;
        int[][] edges = new int[count][];
        for (int i = 0; i < count; i++)
        {
            this.nodes[i].InPlaceCycle = false;
            edges[i] = [.. this.nodes[i].InPlaceChildren(includeNot: true)];
        }

        int[] index = new int[count];
        index.AsSpan().Fill(-1);
        int[] low = new int[count];
        bool[] onStack = new bool[count];
        var stack = new Stack<int>();
        var work = new Stack<(int Node, int Edge)>();
        var members = new List<int>();
        int next = 0;

        for (int root = 0; root < count; root++)
        {
            if (index[root] >= 0)
            {
                continue;
            }

            index[root] = low[root] = next++;
            stack.Push(root);
            onStack[root] = true;
            work.Push((root, 0));

            while (work.Count > 0)
            {
                (int v, int e) = work.Pop();
                if (e < edges[v].Length)
                {
                    work.Push((v, e + 1));
                    int w = edges[v][e];
                    if (index[w] < 0)
                    {
                        index[w] = low[w] = next++;
                        stack.Push(w);
                        onStack[w] = true;
                        work.Push((w, 0));
                    }
                    else if (onStack[w])
                    {
                        low[v] = Math.Min(low[v], index[w]);
                    }

                    continue;
                }

                if (low[v] == index[v])
                {
                    members.Clear();
                    int w;
                    do
                    {
                        w = stack.Pop();
                        onStack[w] = false;
                        members.Add(w);
                    }
                    while (w != v);

                    if (members.Count > 1 || Array.IndexOf(edges[v], v) >= 0)
                    {
                        foreach (int m in members)
                        {
                            this.nodes[m].InPlaceCycle = true;
                        }
                    }
                }

                if (work.Count > 0)
                {
                    int parent = work.Peek().Node;
                    low[parent] = Math.Min(low[parent], low[v]);
                }
            }
        }
    }

    /// <summary>
    /// Selects the fused flag-mode routine for every node; see <see cref="NodePlan"/>. With
    /// <c>CORVUS_RT_NO_PLANS=1</c> only the routines that predate plans (leaf, simple array) are selected, for A/B runs.
    /// </summary>
    private void ComputePlans()
    {
        foreach (SchemaNode node in this.nodes)
        {
            node.Plan = SelectPlan(node);
        }
    }

    private static NodePlan SelectPlan(SchemaNode node)
    {
        if (node.AlwaysTrue)
        {
            return NodePlan.AlwaysTrue;
        }

        if (node.AlwaysFalse)
        {
            return NodePlan.AlwaysFalse;
        }

        if (node.IsLeaf)
        {
            return NodePlan.Leaf;
        }

        if (node.IsSimpleArray)
        {
            return NodePlan.SimpleArray;
        }

        if (DisablePlans)
        {
            return NodePlan.General;
        }

        if (node.Fused is not null)
        {
            return NodePlan.FusedObject;
        }

        if (node.ForwardNode >= 0)
        {
            return NodePlan.Forward;
        }

        bool noValueKeywords = !node.HasConst && node.Enum is null && !node.HasNumberKeywords && !node.HasStringKeywords;
        bool noUnevaluated = !node.UnevaluatedProperties.IsPresent && !node.UnevaluatedItems.IsPresent;

        if (node.DynamicRef is not null && !node.Ref.IsPresent && node.AllOf is null && node.AnyOf is null && node.OneOf is null
            && !node.Not.IsPresent && !node.If.IsPresent && !node.Then.IsPresent && !node.Else.IsPresent && node.Dependencies is null
            && !node.HasType && noValueKeywords && !node.HasObjectKeywords && !node.HasArrayKeywords && noUnevaluated)
        {
            return NodePlan.DynamicRef;
        }

        if (node.HasInPlaceApplicators || !noUnevaluated || !noValueKeywords)
        {
            return NodePlan.General;
        }

        if (node.HasObjectKeywords && !node.HasArrayKeywords && !node.PropertyNames.IsPresent && node.SeenBitCount <= SchemaNode.InlineBitWords * 64)
        {
            return NodePlan.Object;
        }

        if (node.HasArrayKeywords && !node.HasObjectKeywords && node.PrefixItems is null && !node.Contains.IsPresent)
        {
            return NodePlan.ArrayItems;
        }

        return NodePlan.General;
    }

    /// <summary>Packs the per-node presence flags into <see cref="SchemaNode.Flags"/>. Runs last.</summary>
    private void ComputeFlags()
    {
        foreach (SchemaNode node in this.nodes)
        {
            NodeFlags f = NodeFlags.None;
            if (node.AlwaysTrue)
            {
                f |= NodeFlags.AlwaysTrue;
            }

            if (node.AlwaysFalse)
            {
                f |= NodeFlags.AlwaysFalse;
            }

            if (node.HasType)
            {
                f |= NodeFlags.HasType;
            }

            if (node.HasConst)
            {
                f |= NodeFlags.HasConst;
            }

            if (node.Enum is not null)
            {
                f |= NodeFlags.HasEnum;
            }

            if (node.HasNumberKeywords)
            {
                f |= NodeFlags.HasNumberKeywords;
            }

            if (node.HasStringKeywords)
            {
                f |= NodeFlags.HasStringKeywords;
            }

            if (node.HasObjectKeywords)
            {
                f |= NodeFlags.HasObjectKeywords;
            }

            if (node.HasArrayKeywords)
            {
                f |= NodeFlags.HasArrayKeywords;
            }

            if (node.HasInPlaceApplicators)
            {
                f |= NodeFlags.HasInPlaceApplicators;
            }

            if (node.UnevaluatedProperties.IsPresent)
            {
                f |= NodeFlags.HasUnevaluatedProperties;
            }

            if (node.UnevaluatedItems.IsPresent)
            {
                f |= NodeFlags.HasUnevaluatedItems;
            }

            if (node.Annotations is not null)
            {
                f |= NodeFlags.HasAnnotations;
            }

            if (node.TracksProperties)
            {
                f |= NodeFlags.TracksProperties;
            }

            if (node.TracksItems)
            {
                f |= NodeFlags.TracksItems;
            }

            if (node.InPlaceCycle)
            {
                f |= NodeFlags.InPlaceCycle;
            }

            if (node.Dialect == JsonSchemaDialect.Draft4)
            {
                f |= NodeFlags.Draft4;
            }

            node.Flags = f;
        }
    }

    private void ComputeLeafFlags()
    {
        foreach (SchemaNode node in this.nodes)
        {
            node.IsTypeOnly = node.HasType && !node.HasConst && node.Enum is null && !node.HasStringKeywords && !node.HasObjectKeywords && !node.HasArrayKeywords
                && !node.HasInPlaceApplicators && node.MultipleOf is null && node.Minimum is null && node.Maximum is null && node.ExclusiveMinimum is null && node.ExclusiveMaximum is null
                && !node.UnevaluatedProperties.IsPresent && !node.UnevaluatedItems.IsPresent && !node.AlwaysTrue && !node.AlwaysFalse;
            node.IsLeaf = !DisableLeaf && !node.HasObjectKeywords && !node.HasArrayKeywords && !node.HasInPlaceApplicators
                && !node.UnevaluatedProperties.IsPresent && !node.UnevaluatedItems.IsPresent && !node.AlwaysTrue && !node.AlwaysFalse;
        }
    }

    private void FillRequiredDependency(DependencyEntry dep, in JsonElement array, Func<byte[], int> seenBit)
    {
        var bits = new List<int>();
        var names = new List<byte[]>();
        foreach (JsonElement r in array.EnumerateArray())
        {
            if (r.ValueKind == JsonValueKind.String)
            {
                using UnescapedUtf8JsonString s = r.GetUtf8String();
                byte[] n = s.Span.ToArray();
                bits.Add(seenBit(n));
                names.Add(n);
            }
        }

        dep.RequiredSeenBits = [.. bits];
        dep.RequiredNames = [.. names];
    }

    private ChildRef[] ChildArray(SchemaTarget target, in JsonElement array, string keyword)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<ChildRef>();
        int i = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            list.Add(this.Child(target, item, Segment(keyword, i)));
            i++;
        }

        return [.. list];
    }

#if !STJ
    /// <summary>
    /// Builds the <c>type</c> message provider for a mask: the shared single-type provider, or, for a type list, the
    /// generated-model form <c>'["array", "object"]'</c> in the generator's order.
    /// </summary>
    internal static JsonSchemaMessageProvider? TypeMessageFor(TypeMask mask)
    {
        switch (mask)
        {
            case TypeMask.None:
                return null;
            case TypeMask.String:
                return JsonSchemaEvaluation.ExpectedTypeString;
            case TypeMask.Object:
                return JsonSchemaEvaluation.ExpectedTypeObject;
            case TypeMask.Array:
                return JsonSchemaEvaluation.ExpectedTypeArray;
            case TypeMask.Number:
                return JsonSchemaEvaluation.ExpectedTypeNumber;
            case TypeMask.Integer:
                return JsonSchemaEvaluation.ExpectedTypeInteger;
            case TypeMask.Boolean:
                return JsonSchemaEvaluation.ExpectedTypeBoolean;
            case TypeMask.Null:
                return JsonSchemaEvaluation.ExpectedTypeNull;
        }

        StringBuilder names = new("[");
        Append(TypeMask.Array, "\"array\"");
        Append(TypeMask.Object, "\"object\"");
        Append(TypeMask.Null, "\"null\"");
        Append(TypeMask.Boolean, "\"boolean\"");
        Append(TypeMask.Number, "\"number\"");
        Append(TypeMask.Integer, "\"integer\"");
        Append(TypeMask.String, "\"string\"");
        names.Append(']');
        byte[] utf8 = Encoding.UTF8.GetBytes(names.ToString());
        return (Span<byte> buffer, out int written) => JsonSchemaEvaluation.ExpectedType(utf8, buffer, out written);

        void Append(TypeMask flag, string name)
        {
            if ((mask & flag) != 0)
            {
                if (names.Length > 1)
                {
                    names.Append(", ");
                }

                names.Append(name);
            }
        }
    }
#endif

    private void CompileType(SchemaNode node, in JsonElement value)
    {
        TypeMask mask = TypeMask.None;
        if (value.ValueKind == JsonValueKind.String)
        {
            mask |= ParseType(value);
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement t in value.EnumerateArray())
            {
                if (t.ValueKind == JsonValueKind.String)
                {
                    mask |= ParseType(t);
                }
            }
        }
        else
        {
            return;
        }

        node.HasType = true;
        node.Type = mask;
#if !STJ
        node.TypeMessage = TypeMessageFor(mask);
#endif

        static TypeMask ParseType(in JsonElement t)
        {
            if (t.ValueEquals("string"u8))
            {
                return TypeMask.String;
            }

            if (t.ValueEquals("object"u8))
            {
                return TypeMask.Object;
            }

            if (t.ValueEquals("array"u8))
            {
                return TypeMask.Array;
            }

            if (t.ValueEquals("number"u8))
            {
                return TypeMask.Number;
            }

            if (t.ValueEquals("integer"u8))
            {
                return TypeMask.Integer;
            }

            if (t.ValueEquals("boolean"u8))
            {
                return TypeMask.Boolean;
            }

            if (t.ValueEquals("null"u8))
            {
                return TypeMask.Null;
            }

            return TypeMask.None;
        }
    }

    private void CompileConst(SchemaNode node, SchemaTarget target, in JsonElement value)
    {
        node.HasConst = true;
        node.Const = new ConstantValue(Elements.Document(value), Elements.Index(value), Elements.Document(value).GetJsonTokenType(Elements.Index(value)));
        if (value.ValueKind == JsonValueKind.String)
        {
            using UnescapedUtf8JsonString s = value.GetUtf8String();
            node.ConstString = s.Span.ToArray();
            node.ConstText = System.Text.Encoding.UTF8.GetString(node.ConstString);
        }
        else if (value.ValueKind == JsonValueKind.Number)
        {
            node.ConstNumber = new NumberValue(Elements.Document(value).GetRawSimpleValue(Elements.Index(value)).Span);
            node.ConstText = node.ConstNumber.Text;
        }
    }

    private void CompileEnum(SchemaNode node, SchemaTarget target, in JsonElement value)
    {
        var values = new List<ConstantValue>();
        bool allStrings = true;
        var strings = new List<KeyValuePair<byte[], object>>();
        foreach (JsonElement v in value.EnumerateArray())
        {
            values.Add(new ConstantValue(Elements.Document(v), Elements.Index(v), Elements.Document(v).GetJsonTokenType(Elements.Index(v))));
            if (v.ValueKind == JsonValueKind.String)
            {
                using UnescapedUtf8JsonString s = v.GetUtf8String();
                strings.Add(new KeyValuePair<byte[], object>(s.Span.ToArray(), EnumSentinel));
            }
            else
            {
                allStrings = false;
            }
        }

        node.Enum = [.. values];
        node.EnumAllStrings = allStrings && values.Count > 0;
        if (node.EnumAllStrings)
        {
            node.EnumStrings = new Utf8NameMap<object>(strings);
        }
    }

    /// <summary>The value stored against every key of an enum string set; only membership matters.</summary>
    internal static readonly object EnumSentinel = new();

    private void CompileRef(SchemaNode node, SchemaTarget target, string reference)
    {
        if (!this.loader.TryResolveReference(target.Resource, reference, out SchemaTarget resolved))
        {
            throw new JsonSchemaCompilationException($"Unable to resolve reference '{reference}' from '{target.Resource.Uri}'.");
        }

        node.Ref = new ChildRef(this.GetNode(resolved), Segment("$ref"));
        node.HasInPlaceApplicators = true;
    }

    private void CompileDynamicRef(SchemaNode node, SchemaTarget target, string reference, bool isRecursive)
    {
        string keyword = isRecursive ? "$recursiveRef" : "$dynamicRef";
        if (!this.loader.TryResolveReference(target.Resource, reference, out SchemaTarget resolved))
        {
            throw new JsonSchemaCompilationException($"Unable to resolve reference '{reference}' from '{target.Resource.Uri}'.");
        }

        UriUtilities.Split(reference, out _, out string fragment);
        fragment = UriUtilities.DecodeFragment(fragment);

        bool dynamic;
        if (isRecursive)
        {
            dynamic = resolved.Resource.RecursiveAnchor && resolved.Index == resolved.Resource.RootIndex;
        }
        else
        {
            dynamic = fragment.Length > 0
                && fragment[0] != '/'
                && resolved.Resource.DynamicAnchors is not null
                && resolved.Resource.DynamicAnchors.TryGetValue(fragment, out int anchorIndex)
                && anchorIndex == resolved.Index;
        }

        if (!dynamic)
        {
            node.Ref = new ChildRef(this.GetNode(resolved), Segment(keyword));
            node.HasInPlaceApplicators = true;
            return;
        }

        this.pendingDynamicRefs.Add(new PendingDynamicRef
        {
            NodeId = node.Id,
            Anchor = fragment,
            IsRecursive = isRecursive,
            InitialTarget = resolved,
            PathSegment = Segment(keyword),
        });
    }

    private JsonSchemaFormatMode? ResolveFormatMode(string format)
    {
        IReadOnlyDictionary<string, JsonSchemaFormatMode>? modes = this.options.FormatModes;
        if (modes is null || modes.Count == 0)
        {
            return null;
        }

        if (modes.TryGetValue(format, out JsonSchemaFormatMode mode) || modes.TryGetValue("*", out mode))
        {
            return mode;
        }

        return null;
    }

    private static FormatKind GetFormatKind(string format, JsonSchemaDialect dialect)
    {
        switch (format)
        {
            case "byte":
                return FormatKind.Byte;
            case "uint16":
                return FormatKind.UInt16;
            case "uint32":
                return FormatKind.UInt32;
            case "uint64":
                return FormatKind.UInt64;
            case "uint128":
                return FormatKind.UInt128;
            case "sbyte":
                return FormatKind.SByte;
            case "int16":
                return FormatKind.Int16;
            case "int32":
                return FormatKind.Int32;
            case "int64":
                return FormatKind.Int64;
            case "int128":
                return FormatKind.Int128;
            case "half":
                return FormatKind.Half;
            case "single":
            case "float":
                return FormatKind.Single;
            case "double":
                return FormatKind.Double;
            case "decimal":
                return FormatKind.Decimal;
            case "date-time":
                return FormatKind.DateTime;
            case "email":
                return FormatKind.Email;
            case "hostname":
                return FormatKind.Hostname;
            case "ipv4":
                return FormatKind.Ipv4;
            case "ipv6":
                return FormatKind.Ipv6;
            case "uri":
                return FormatKind.Uri;
            case "uri-reference":
                return dialect >= JsonSchemaDialect.Draft6 ? FormatKind.UriReference : FormatKind.Unknown;
            case "uri-template":
                return dialect >= JsonSchemaDialect.Draft6 ? FormatKind.UriTemplate : FormatKind.Unknown;
            case "json-pointer":
                return dialect >= JsonSchemaDialect.Draft6 ? FormatKind.JsonPointer : FormatKind.Unknown;
            case "date":
                return dialect >= JsonSchemaDialect.Draft7 ? FormatKind.Date : FormatKind.Unknown;
            case "time":
                return dialect >= JsonSchemaDialect.Draft7 ? FormatKind.Time : FormatKind.Unknown;
            case "regex":
                return dialect >= JsonSchemaDialect.Draft7 ? FormatKind.Regex : FormatKind.Unknown;
            case "relative-json-pointer":
                return dialect >= JsonSchemaDialect.Draft7 ? FormatKind.RelativeJsonPointer : FormatKind.Unknown;
            case "idn-email":
                return dialect >= JsonSchemaDialect.Draft7 ? FormatKind.IdnEmail : FormatKind.Unknown;
            case "idn-hostname":
                return dialect >= JsonSchemaDialect.Draft7 ? FormatKind.IdnHostname : FormatKind.Unknown;
            case "iri":
                return dialect >= JsonSchemaDialect.Draft7 ? FormatKind.Iri : FormatKind.Unknown;
            case "iri-reference":
                return dialect >= JsonSchemaDialect.Draft7 ? FormatKind.IriReference : FormatKind.Unknown;
            case "duration":
                return dialect >= JsonSchemaDialect.Draft201909 ? FormatKind.Duration : FormatKind.Unknown;
            case "uuid":
                return dialect >= JsonSchemaDialect.Draft201909 ? FormatKind.Uuid : FormatKind.Unknown;
            default:
                return FormatKind.Unknown;
        }
    }
}