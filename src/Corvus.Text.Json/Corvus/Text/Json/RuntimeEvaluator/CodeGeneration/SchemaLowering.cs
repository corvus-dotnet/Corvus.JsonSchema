// <copyright file="SchemaLowering.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET && !STJ
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;

namespace Corvus.Text.Json.RuntimeEvaluator.CodeGeneration;

/// <summary>
/// Lowers a compiled schema to generated code through an <see cref="ISchemaEmitter"/>: decides which nodes get a
/// method and what each does in place, leaving the rest to the interpreter.
/// </summary>
/// <remarks>
/// Specialised so far: strict objects (<see cref="NodePlan.StrictObject"/>), with their properties' leaves tested in
/// place, and arrays of items (<see cref="NodePlan.SimpleArray"/> and <see cref="NodePlan.ArrayItems"/>, with
/// prefix items and <c>uniqueItems</c>), flat fused objects (an <c>allOf</c>/<c>$ref</c> chain of object schemas in one
/// pass), objects with pattern properties and dependencies, type unions and type dispatch, and nodes with in-place
/// applicators (allOf, anyOf, oneOf, not, if/then/else) over own keywords of those kinds; a child that is specialised
/// is called as a generated method. Every other node
/// is evaluated by the interpreter, which calls the generated methods of the specialised nodes it reaches.
/// </remarks>
internal static class SchemaLowering
{
    // The size guard: an object with more names than this is left to the interpreter, whose lookup is a hash; its
    // method would be too large for the JIT to optimise.
    private const int MaxDispatchNames = 256;

    // The longest chain of forwards followed to a child's target.
    private const int MaxForwards = 16;

    private static int compiledCount;

    /// <summary>Gets the number of schemas compiled in this process (for tests).</summary>
    public static int CompiledCount => Volatile.Read(ref compiledCount);

    /// <summary>Whether generated code can run here (it needs Reflection.Emit and the JIT).</summary>
    public static bool IsSupported => RuntimeFeature.IsDynamicCodeSupported;

    /// <summary>
    /// Compiles a program's flag-mode code: a method for every node the lowering specialises, and a copy of the node
    /// array in which those nodes carry their methods (<see cref="NodePlan.Generated"/>), so that the interpreter
    /// calls generated code for them wherever it evaluates the nodes around them.
    /// </summary>
    /// <param name="nodes">The program's nodes.</param>
    /// <param name="entry">The entry node (after reference elision).</param>
    /// <param name="generatedNodes">The node array to evaluate with: the program's, with the specialised nodes replaced.</param>
    /// <param name="specialised">The number of nodes given methods.</param>
    /// <returns>The entry's method, or null when the entry is not specialised (the interpreter then enters it, over <paramref name="generatedNodes"/>).</returns>
    public static NodeValidator? Compile(SchemaNode[] nodes, SchemaNode entry, out SchemaNode[] generatedNodes, out int specialised)
    {
        Interlocked.Increment(ref compiledCount);
        generatedNodes = (SchemaNode[])nodes.Clone();
        var emitter = new IlSchemaEmitter();
        specialised = Lower(emitter, nodes);
        if (specialised == 0)
        {
            return null;
        }

        Dictionary<int, NodeValidator> methods = emitter.Build();
        foreach (KeyValuePair<int, NodeValidator> method in methods)
        {
            if (nodes[method.Key].Plan == NodePlan.Leaf)
            {
                // The interpreter's own leaf evaluation is a direct call; the method is for generated callers.
                continue;
            }

            SchemaNode generated = nodes[method.Key].ShallowClone();
            generated.Plan = NodePlan.Generated;
            generated.Generated = method.Value;
            generatedNodes[method.Key] = generated;
        }

        // An interpreted node that enters a strict-object child by its loop (skipping the child's plan) enters a
        // specialised child by its plan instead: the generated method.
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] is SchemaNode node && !methods.ContainsKey(i) && EntersSpecialisedChildDirectly(node, methods))
            {
                generatedNodes[i] = ThroughPlans(node, methods);
            }
        }

        return methods.GetValueOrDefault(entry.Id);
    }

    private static int Lower(ISchemaEmitter emitter, SchemaNode[] nodes)
    {
        // Every node the lowering specialises gets a method, whatever reaches it: on the Sourcemeta corpora every
        // such node is reachable from the entry, most of them through nodes the interpreter evaluates.
        var requested = new HashSet<int>();
        var pending = new Queue<int>();
        for (int i = 0; i < nodes.Length; i++)
        {
            // A leaf gets a method only when generated code calls it: the interpreter evaluates a leaf directly.
            if (nodes[i] is SchemaNode node && node.Plan != NodePlan.Leaf && IsSpecialised(nodes, node) && requested.Add(i))
            {
                pending.Enqueue(i);
            }
        }

        int specialised = 0;
        while (pending.TryDequeue(out int id))
        {
            SchemaNode node = nodes[id];
            emitter.BeginMethod(id);
            switch (node.Plan)
            {
                case NodePlan.StrictObject:
                    LowerStrictObject(emitter, nodes, node, requested, pending);
                    break;
                case NodePlan.SimpleArray:
                    LowerSimpleArray(emitter, nodes, node, requested, pending);
                    break;
                case NodePlan.ArrayItems:
                    LowerArrayItems(emitter, nodes, node, requested, pending);
                    break;
                case NodePlan.FusedObject when node.Fused!.FlatEntries is not null:
                    LowerFlatFusedObject(emitter, nodes, node, requested, pending);
                    break;
                case NodePlan.FusedObject:
                    LowerFusedObject(emitter, nodes, node, requested, pending);
                    break;
                case NodePlan.Object:
                    LowerObject(emitter, nodes, node, requested, pending);
                    break;
                case NodePlan.Composite:
                case NodePlan.Conditional:
                    LowerComposition(emitter, nodes, node, requested, pending);
                    break;
                case NodePlan.Leaf:
                    emitter.UseSelfAsValue();
                    LowerLeaf(emitter, node);
                    emitter.Succeed();
                    break;
                case NodePlan.TypeUnion:
                    emitter.ReturnTokenTest(StrictEntry.TokenBitsOf(node.InPlaceUnionMask), (node.InPlaceUnionMask & TypeMask.Integer) != 0 && (node.InPlaceUnionMask & TypeMask.Number) == 0, node.Dialect == JsonSchemaDialect.Draft4);
                    break;
                default:
                    LowerTypeDispatch(emitter, nodes, node, requested, pending);
                    break;
            }

            emitter.EndMethod();
            specialised++;
        }

        return specialised;
    }

    private static bool IsNestedSpecialised(in StrictEntry entry, Dictionary<int, NodeValidator> methods) => entry.NestedObject && entry.Child >= 0 && methods.ContainsKey(entry.Child);

    private static bool EntersSpecialisedChildDirectly(SchemaNode node, Dictionary<int, NodeValidator> methods)
    {
        if (IsNestedSpecialised(in node.AdditionalEntry, methods) || (node.ItemsNestedObject && node.Items.IsPresent && methods.ContainsKey(node.Items.FastNode)))
        {
            return true;
        }

        foreach (StrictEntry[]? entries in (ReadOnlySpan<StrictEntry[]?>)[node.StrictEntries, node.PatternMap, node.PrefixEntries])
        {
            foreach (StrictEntry entry in entries ?? [])
            {
                if (IsNestedSpecialised(in entry, methods))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static SchemaNode ThroughPlans(SchemaNode node, Dictionary<int, NodeValidator> methods)
    {
        SchemaNode copy = node.ShallowClone();
        copy.StrictEntries = Entries(node.StrictEntries);
        copy.PatternMap = Entries(node.PatternMap);
        copy.PrefixEntries = Entries(node.PrefixEntries);
        if (IsNestedSpecialised(in node.AdditionalEntry, methods))
        {
            copy.AdditionalEntry = node.AdditionalEntry.WithoutNestedObject();
        }

        if (node.ItemsNestedObject && node.Items.IsPresent && methods.ContainsKey(node.Items.FastNode))
        {
            copy.ItemsNestedObject = false;
        }

        return copy;

        StrictEntry[]? Entries(StrictEntry[]? entries)
        {
            if (entries is null)
            {
                return null;
            }

            var through = (StrictEntry[])entries.Clone();
            for (int i = 0; i < through.Length; i++)
            {
                if (IsNestedSpecialised(in through[i], methods))
                {
                    through[i] = through[i].WithoutNestedObject();
                }
            }

            return through;
        }
    }

    private static bool IsSpecialised(SchemaNode[] nodes, SchemaNode node)
    {
        switch (node.Plan)
        {
            case NodePlan.Composite:
                return IsOwnPlanSpecialised(nodes, node, node.ConditionalOwnPlan) && IsInPlaceSpecialised(nodes, node);
            case NodePlan.Conditional:
                // Its own keywords are a strict object, an object, a type, or nothing; then if/then/else.
                return node.ConditionalOwnPlan != NodePlan.ArrayItems && IsOwnPlanSpecialised(nodes, node, node.ConditionalOwnPlan);
            default:
                return IsOwnPlanSpecialised(nodes, node, node.Plan);
        }
    }

    // Whether the node's keywords of one plan are specialised: the node's plan, or the plan of the own keywords of a
    // node that also has in-place applicators.
    private static bool IsOwnPlanSpecialised(SchemaNode[] nodes, SchemaNode node, NodePlan plan)
    {
        switch (plan)
        {
            case NodePlan.Leaf:
                return true;
            case NodePlan.AlwaysTrue:
                // As a node's plan this is the interpreter's (nothing is evaluated); as own keywords it is nothing.
                return node.Plan != plan;
            case NodePlan.Object:
                // One word of seen bits; a small object probed by name (unrolled) has at most 32 names.
                return node.SeenBitCount <= 64 && (node.Properties?.Count ?? 0) <= MaxDispatchNames;
            case NodePlan.StrictObject:
                return (node.Properties?.Count ?? 0) <= MaxDispatchNames;
            case NodePlan.SimpleArray:
                // Its items are a leaf: nothing to test, a type, or the interpreter's leaf evaluation.
                SchemaNode items = nodes[node.Items.FastNode];
                return items.AlwaysTrue || items.IsTypeOnly || items.Plan == NodePlan.Leaf;
            case NodePlan.ArrayItems:
                return true;
            case NodePlan.FusedObject:
                // A flat fused plan (the strict loop over the merged names of an allOf/$ref chain of object schemas),
                // or the full pass when its state fits one word each (names, conditions, an alternative group's
                // branches); unevaluatedProperties with alternative groups is the interpreter's (a property is
                // covered there by whether a branch's resolution of it succeeded).
                return node.Fused is FusedObject fused
                    && (fused.FlatEntries is not null
                        || ((!fused.Unevaluated.IsPresent || fused.AltGroups.Length == 0)
                            && fused.EntryList.Length <= 64
                            && fused.Conditions.Length <= 64
                            && Array.TrueForAll(fused.AltGroups, g => g.BranchCount <= 64)));
            case NodePlan.TypeUnion:
                return true;
            case NodePlan.TypeDispatch:
                // A branch on an in-place cycle is entered through the interpreter's guarded general edge.
                foreach (int branch in node.InPlaceDispatch!)
                {
                    if (branch >= 0 && (nodes[node.InPlaceBranches![branch].FastNode].Flags & NodeFlags.InPlaceCycle) != 0)
                    {
                        return false;
                    }
                }

                return true;
            default:
                return false;
        }
    }

    // The token types a node's type accepts for a value that is not the container it specialises (every type when
    // the node has no type), as Evaluator.MatchesType decides them.
    private static ushort OtherTokens(SchemaNode node, JsonTokenType container) => node.HasType ? (ushort)(StrictEntry.TokenBitsOf(node.Type) & ~(1 << (int)container)) : ushort.MaxValue;

    private static bool IntegerOnly(SchemaNode node) => node.HasType && (node.Type & TypeMask.Integer) != 0 && (node.Type & TypeMask.Number) == 0;

    // Whether the node's in-place applicators are specialised: not a dynamic reference, a discriminator over more
    // than 64 branches, or a child on an in-place cycle (entered under the interpreter's depth guard).
    private static bool IsInPlaceSpecialised(SchemaNode[] nodes, SchemaNode node)
    {
        if (node.DynamicRef is not null
            || (node.AnyOfDiscriminator is not null && node.AnyOf!.Length > 64)
            || (node.OneOfDiscriminator is not null && node.OneOf!.Length > 64))
        {
            return false;
        }

        var children = new List<int>();
        Add(node.Ref);
        Add(node.If);
        Add(node.Then);
        Add(node.Else);
        foreach (ChildRef[]? branches in (ReadOnlySpan<ChildRef[]?>)[node.AllOf, node.AnyOf, node.OneOf])
        {
            foreach (ChildRef branch in branches ?? [])
            {
                Add(branch);
            }
        }

        foreach (int child in children)
        {
            if ((nodes[child].Flags & NodeFlags.InPlaceCycle) != 0)
            {
                return false;
            }
        }

        return !node.Not.IsPresent || (nodes[node.Not.Node].Flags & NodeFlags.InPlaceCycle) == 0;

        void Add(in ChildRef child)
        {
            if (child.IsPresent)
            {
                children.Add(child.FastNode);
            }
        }
    }

    // A node with in-place applicators (Evaluator.EvalCompositePlan and EvalConditionalPlan): its own keywords
    // through their plan, then the applicators on the same value.
    private static void LowerComposition(ISchemaEmitter emitter, SchemaNode[] nodes, SchemaNode node, HashSet<int> requested, Queue<int> pending)
    {
        emitter.BeginOwnKeywords();
        switch (node.ConditionalOwnPlan)
        {
            case NodePlan.StrictObject:
                LowerStrictObject(emitter, nodes, node, requested, pending);
                break;
            case NodePlan.Object:
                LowerObject(emitter, nodes, node, requested, pending);
                break;
            case NodePlan.ArrayItems:
                LowerArrayItems(emitter, nodes, node, requested, pending);
                break;
            case NodePlan.Leaf when node.Plan == NodePlan.Composite:
                emitter.UseSelfAsValue();
                LowerLeaf(emitter, node);
                emitter.Succeed();
                break;
            case NodePlan.Leaf:
                emitter.FailUnlessSelfToken(StrictEntry.TokenBitsOf(node.Type), (node.Type & TypeMask.Integer) != 0 && (node.Type & TypeMask.Number) == 0, (node.Flags & NodeFlags.Draft4) != 0);
                emitter.Succeed();
                break;
            default:
                emitter.Succeed();
                break;
        }

        emitter.EndOwnKeywords();
        if (node.Plan == NodePlan.Conditional)
        {
            LowerIf(emitter, nodes, node, requested, pending);
        }
        else
        {
            LowerInPlace(emitter, nodes, node, requested, pending);
        }

        emitter.Succeed();
    }

    // The in-place applicators in flag mode with nothing to mark (Evaluator.EvalInPlace): each keyword in turn.
    private static void LowerInPlace(ISchemaEmitter emitter, SchemaNode[] nodes, SchemaNode node, HashSet<int> requested, Queue<int> pending)
    {
        if (node.Ref.IsPresent)
        {
            (int child, bool generated) = Target(nodes, node.Ref.FastNode, requested, pending);
            emitter.FailUnlessSelf(child, generated);
        }

        foreach (ChildRef branch in node.AllOf ?? [])
        {
            (int child, bool generated) = Target(nodes, branch.FastNode, requested, pending);
            emitter.FailUnlessSelf(child, generated);
        }

        if (node.AnyOf is ChildRef[] anyOf)
        {
            LowerBranches(emitter, nodes, node, anyOf, node.AnyOfTypeUnion, node.AnyOfDiscriminator, node.AnyOfByKind, node.AnyOfTypeDispatch, exactlyOne: false, requested, pending);
        }

        if (node.OneOf is ChildRef[] oneOf)
        {
            LowerBranches(emitter, nodes, node, oneOf, node.OneOfTypeUnion, node.OneOfDiscriminator, node.OneOfByKind, node.OneOfTypeDispatch, exactlyOne: true, requested, pending);
        }

        if (node.Not.IsPresent)
        {
            (int child, bool generated) = Target(nodes, node.Not.Node, requested, pending);
            emitter.FailIfSelf(child, generated);
        }

        if (node.If.IsPresent)
        {
            LowerIf(emitter, nodes, node, requested, pending);
        }
    }

    private static void LowerIf(ISchemaEmitter emitter, SchemaNode[] nodes, SchemaNode node, HashSet<int> requested, Queue<int> pending)
    {
        (int condition, bool conditionGenerated) = Target(nodes, node.If.FastNode, requested, pending);
        emitter.BeginIfSelf(condition, conditionGenerated);
        if (node.Then.IsPresent)
        {
            (int child, bool generated) = Target(nodes, node.Then.FastNode, requested, pending);
            emitter.FailUnlessSelf(child, generated);
        }

        emitter.Else();
        if (node.Else.IsPresent)
        {
            (int child, bool generated) = Target(nodes, node.Else.FastNode, requested, pending);
            emitter.FailUnlessSelf(child, generated);
        }

        emitter.EndIf();
    }

    // An anyOf or oneOf: one mask test when its branches are types; otherwise the branches a discriminator selects
    // for the value, or else those that can accept the value's token type (by the kinds each admits, else the one its
    // type selects, else all of them), any of which, or exactly one of which, must hold.
    private static void LowerBranches(ISchemaEmitter emitter, SchemaNode[] nodes, SchemaNode node, ChildRef[] branches, TypeMask union, Discriminator? discriminator, int[]?[]? byKind, int[]? dispatch, bool exactlyOne, HashSet<int> requested, Queue<int> pending)
    {
        if (union != TypeMask.None)
        {
            emitter.FailUnlessSelfToken(StrictEntry.TokenBitsOf(union), (union & TypeMask.Integer) != 0 && (union & TypeMask.Number) == 0, node.Dialect == JsonSchemaDialect.Draft4);
            return;
        }

        if (discriminator is not null)
        {
            emitter.BeginIfDiscriminated(discriminator);
            if (exactlyOne)
            {
                emitter.BeginCount();
            }
            else
            {
                emitter.BeginAlternatives();
            }

            for (int i = 0; i < branches.Length; i++)
            {
                (int child, bool generated) = Target(nodes, branches[i].FastNode, requested, pending);
                emitter.BeginIfBranchSelected(i);
                if (exactlyOne)
                {
                    emitter.CountSelf(child, generated);
                }
                else
                {
                    emitter.OrSelf(child, generated);
                }

                emitter.EndIf();
            }

            if (exactlyOne)
            {
                emitter.FailUnlessCountedOne();
            }
            else
            {
                emitter.EndAlternatives();
            }

            emitter.Else();
            Narrowed();
            emitter.EndIf();
            return;
        }

        Narrowed();
        return;

        void Narrowed()
        {
            // The candidates for each token type a value can have, and the token types that share each list.
            int[] all = new int[branches.Length];
            for (int i = 0; i < all.Length; i++)
            {
                all[i] = i;
            }

            var lists = new List<(int[] Candidates, ushort Tokens)>();
            foreach (JsonTokenType token in (ReadOnlySpan<JsonTokenType>)[JsonTokenType.StartObject, JsonTokenType.StartArray, JsonTokenType.String, JsonTokenType.Number, JsonTokenType.True, JsonTokenType.False, JsonTokenType.Null])
            {
                int[] candidates = byKind?[(int)token] is int[] narrowed
                    ? narrowed
                    : dispatch is not null ? (dispatch[(int)token] >= 0 ? [dispatch[(int)token]] : []) : all;
                int at = lists.FindIndex(l => l.Candidates.AsSpan().SequenceEqual(candidates));
                if (at < 0)
                {
                    lists.Add((candidates, (ushort)(1 << (int)token)));
                }
                else
                {
                    lists[at] = (lists[at].Candidates, (ushort)(lists[at].Tokens | (1 << (int)token)));
                }
            }

            if (lists.Count == 1)
            {
                Candidates(lists[0].Candidates);
                return;
            }

            emitter.BeginTokenSwitch();
            foreach ((int[] candidates, ushort tokens) in lists)
            {
                emitter.BeginTokenCase(tokens);
                Candidates(candidates);
                emitter.EndTokenCase();
            }

            emitter.EndTokenSwitch();
        }

        void Candidates(int[] candidates)
        {
            if (candidates.Length == 0)
            {
                emitter.Fail();
                return;
            }

            if (candidates.Length == 1)
            {
                (int child, bool generated) = Target(nodes, branches[candidates[0]].FastNode, requested, pending);
                emitter.FailUnlessSelf(child, generated);
                return;
            }

            if (exactlyOne)
            {
                emitter.BeginCount();
            }
            else
            {
                emitter.BeginAlternatives();
            }

            foreach (int candidate in candidates)
            {
                (int child, bool generated) = Target(nodes, branches[candidate].FastNode, requested, pending);
                if (exactlyOne)
                {
                    emitter.CountSelf(child, generated);
                }
                else
                {
                    emitter.OrSelf(child, generated);
                }
            }

            if (exactlyOne)
            {
                emitter.FailUnlessCountedOne();
            }
            else
            {
                emitter.EndAlternatives();
            }
        }
    }

    // The interpreter's object plan (Evaluator.EvalObjectPlan and EvalObjectPlanCore): an object with pattern
    // properties or dependencies, in one of three forms.
    private static void LowerObject(ISchemaEmitter emitter, SchemaNode[] nodes, SchemaNode node, HashSet<int> requested, Queue<int> pending)
    {
        bool acceptsObject = !node.HasType || (node.Type & TypeMask.Object) != 0;
        ushort otherTokens = OtherTokens(node, JsonTokenType.StartObject);
        bool lexical = (node.Flags & NodeFlags.Draft4) != 0;
        if (node.UnrolledProperties is PropertyEntry[] unrolled)
        {
            // A few names probed in one pass: each known name's value against its schema, the rest ignored.
            var named = new List<KeyValuePair<byte[], PropertyEntry>>(unrolled.Length);
            ulong required = 0;
            foreach (PropertyEntry entry in unrolled)
            {
                named.Add(new(entry.Name, entry));
            }

            var names = new Utf8NameMap<PropertyEntry>(named);
            emitter.BeginObject(otherTokens, IntegerOnly(node), lexical, acceptsObject, node.MinProperties, node.MaxProperties);
            emitter.BeginNameDispatch(names);
            for (int i = 0; i < unrolled.Length; i++)
            {
                names.TryGetIndex(unrolled[i].Name, out int at);
                emitter.BeginCase(at);
                if (unrolled[i].IsRequired)
                {
                    required |= 1UL << i;
                    emitter.MarkSeen(i);
                }

                if (unrolled[i].Schema.IsPresent)
                {
                    LowerChild(emitter, nodes, 0, 0, unrolled[i].Schema.FastNode, requested, pending);
                }

                emitter.EndCase();
            }

            emitter.BeginCase(-1);
            emitter.EndCase();
            emitter.EndNameDispatch();
            emitter.EndObject(required);
            return;
        }

        if (node.PatternMap is StrictEntry[] patternMap)
        {
            // One pattern property: a matching name's value takes the pattern's resolution, another the additional one.
            PatternMatcher matcher = node.PatternProperties![0].Matcher;
            emitter.BeginObject(otherTokens, IntegerOnly(node), lexical, acceptsObject, node.MinProperties, node.MaxProperties);
            if (matcher.MatchesEverything)
            {
                LowerEntry(emitter, nodes, in patternMap[0], -1, 0, requested, pending);
            }
            else
            {
                emitter.BeginIfNameMatches(matcher);
                LowerEntry(emitter, nodes, in patternMap[0], -1, 0, requested, pending);
                emitter.Else();
                if (node.AdditionalProperties.IsPresent)
                {
                    if (node.AdditionalRejects)
                    {
                        emitter.Fail();
                    }
                    else
                    {
                        LowerEntry(emitter, nodes, in node.AdditionalEntry, -1, 0, requested, pending);
                    }
                }

                emitter.EndIf();
            }

            emitter.EndObject(0);
            return;
        }

        // The general form: each name against the properties, then every pattern, then (when nothing matched) the
        // additional schema; after the properties, the required names and the dependencies.
        Utf8NameMap<PropertyEntry>? properties = node.Properties;
        PatternPropertyEntry[] patterns = node.PatternProperties ?? [];
        bool hasAdditional = node.AdditionalProperties.IsPresent;
        ulong requiredMask = node.RequiredSeenBits is not null ? node.RequiredMask : 0;
        ulong read = requiredMask;
        foreach (DependencyEntry dependency in node.Dependencies ?? [])
        {
            read |= 1UL << dependency.SeenBit;
            read |= Mask(dependency.RequiredSeenBits);
        }

        bool loop = properties is not null || patterns.Length > 0 || hasAdditional;
        emitter.BeginObject(otherTokens, IntegerOnly(node), lexical, acceptsObject, node.MinProperties, node.MaxProperties, properties: loop);
        if (loop)
        {
            bool common = patterns.Length > 0;
            if (properties is not null)
            {
                StrictEntry[] entries = node.StrictEntries ?? [];
                emitter.BeginNameDispatch(properties, thenCommon: common);
                for (int i = 0; i < properties.Count; i++)
                {
                    emitter.BeginCase(i);
                    LowerEntry(emitter, nodes, in entries[i], entries[i].SeenBit, read, requested, pending);
                    if (common)
                    {
                        emitter.SetMatched(true);
                    }

                    emitter.EndCase();
                }

                emitter.BeginCase(-1);
                if (common)
                {
                    emitter.SetMatched(false);
                }
                else if (hasAdditional)
                {
                    Additional();
                }

                emitter.EndCase();
                emitter.EndNameDispatch();
            }
            else if (common)
            {
                emitter.SetMatched(false);
            }
            else
            {
                Additional();
            }

            if (common)
            {
                foreach (PatternPropertyEntry pattern in patterns)
                {
                    emitter.BeginIfNameMatches(pattern.Matcher);
                    emitter.SetMatched(true);
                    LowerChild(emitter, nodes, 0, 0, pattern.Schema.FastNode, requested, pending);
                    emitter.EndIf();
                }

                if (hasAdditional)
                {
                    emitter.BeginIfNotMatched();
                    Additional();
                    emitter.EndIf();
                }
            }
        }

        emitter.EndProperties();
        emitter.FailUnlessSeen(requiredMask);
        foreach (DependencyEntry dependency in node.Dependencies ?? [])
        {
            emitter.BeginIfSeen(dependency.SeenBit);
            emitter.FailUnlessSeen(Mask(dependency.RequiredSeenBits));
            if (dependency.Schema.IsPresent)
            {
                (int child, bool generated) = Target(nodes, dependency.Schema.FastNode, requested, pending);
                emitter.FailUnlessSelf(child, generated);
            }

            emitter.EndIf();
        }

        emitter.Succeed();

        void Additional()
        {
            if (node.AdditionalEntry.TokenBits != 0)
            {
                emitter.FailUnlessToken(node.AdditionalEntry.TokenBits, node.AdditionalEntry.IntegerOnly, node.AdditionalEntry.Lexical);
            }
            else
            {
                LowerChild(emitter, nodes, 0, 0, node.AdditionalProperties.FastNode, requested, pending);
            }
        }

        static ulong Mask(int[] bits)
        {
            ulong mask = 0;
            foreach (int bit in bits)
            {
                mask |= 1UL << bit;
            }

            return mask;
        }
    }

    // The interpreter's leaf evaluation (Evaluator.EvalLeafFast) of the current value: the keywords the node has, in
    // its order (type, const, enum, then the number or string keywords by the value's kind). A string const and an
    // enum of strings are compared in place; integer bounds are compared as longs for a plain integer literal, as the
    // interpreter's fast path compares them; everything else is the interpreter's evaluation of that keyword.
    private static void LowerLeaf(ISchemaEmitter emitter, SchemaNode node)
    {
        if (node.HasType)
        {
            emitter.FailUnlessToken(StrictEntry.TokenBitsOf(node.Type), (node.Type & TypeMask.Integer) != 0 && (node.Type & TypeMask.Number) == 0, node.Dialect == JsonSchemaDialect.Draft4);
        }

        if (node.HasConst)
        {
            if (node.ConstString is byte[] expected)
            {
                emitter.FailUnlessStringConst(expected);
            }
            else
            {
                emitter.FailUnlessConst(node.Id);
            }
        }

        if (node.Enum is not null)
        {
            if (node.EnumAllStrings)
            {
                emitter.FailUnlessStringSet(node.EnumStrings!);
            }
            else
            {
                emitter.FailUnlessEnum(node.Id);
            }
        }

        if (node.HasNumberKeywords)
        {
            emitter.BeginIfValueToken(1 << (int)JsonTokenType.Number);
            NumberValue?[] bounds = [node.Minimum, node.Maximum, node.ExclusiveMinimum, node.ExclusiveMaximum];
            bool longs = Evaluation.Evaluator.GenIntegerFastPath
                && !(node.AssertFormat && FormatKinds.IsNumeric(node.Format))
                && Array.TrueForAll(bounds, b => b is null || b.AsLong is not null)
                && (node.MultipleOf is null || node.MultipleOf.AsLong is not null);
            if (longs)
            {
                emitter.BeginIfValueLong();
                if (node.Minimum?.AsLong is long minimum)
                {
                    emitter.FailIfLongBelow(minimum, exclusive: false);
                }

                if (node.Maximum?.AsLong is long maximum)
                {
                    emitter.FailIfLongAbove(maximum, exclusive: false);
                }

                if (node.ExclusiveMinimum?.AsLong is long exclusiveMinimum)
                {
                    emitter.FailIfLongBelow(exclusiveMinimum, exclusive: true);
                }

                if (node.ExclusiveMaximum?.AsLong is long exclusiveMaximum)
                {
                    emitter.FailIfLongAbove(exclusiveMaximum, exclusive: true);
                }

                if (node.MultipleOf?.AsLong is long divisor)
                {
                    emitter.FailUnlessLongMultipleOf(divisor);
                }

                emitter.Else();
                emitter.FailUnlessNumberKeywords(node.Id);
                emitter.EndIf();
            }
            else
            {
                emitter.FailUnlessNumberKeywords(node.Id);
            }

            emitter.EndIf();
        }

        if (node.HasStringKeywords)
        {
            emitter.BeginIfValueToken(1 << (int)JsonTokenType.String);
            bool lengthsOnly = node.Pattern is null && node.Content == default && !(node.AssertFormat && !FormatKinds.IsNumeric(node.Format));
            if (lengthsOnly)
            {
                emitter.FailUnlessStringLength(node.MinLength, node.MaxLength);
            }
            else
            {
                emitter.FailUnlessStringKeywords(node.Id);
            }

            emitter.EndIf();
        }
    }

    // The interpreter's flat fused loop (Evaluator.EvalFlatFusedLoop): the strict loop over the merged names, with the
    // entry's index as its seen bit, and names no entry knows ignored. A value that is not an object takes the
    // interpreter's general evaluation, as the child dispatch gives it.
    private static void LowerFlatFusedObject(ISchemaEmitter emitter, SchemaNode[] nodes, SchemaNode node, HashSet<int> requested, Queue<int> pending)
    {
        FusedObject fused = node.Fused!;
        StrictEntry[] entries = fused.FlatEntries!;
        emitter.BeginObject(0, integerOnly: false, lexical: false, acceptsObject: true, fused.FlatMinProperties, fused.FlatMaxProperties, otherwiseInterpreted: node.Id);
        emitter.BeginNameDispatch(fused.Entries);
        for (int i = 0; i < entries.Length; i++)
        {
            emitter.BeginCase(i);
            LowerEntry(emitter, nodes, in entries[i], i, fused.FlatRequiredMask, requested, pending);
            emitter.EndCase();
        }

        emitter.BeginCase(-1);
        emitter.EndCase();
        emitter.EndNameDispatch();
        emitter.EndObject(fused.FlatRequiredMask);
    }

    // The interpreter's full fused pass (Evaluator.EvalFusedObjectCore), without coverage tracking: one pass applying
    // every unconditional resolution of each name, the conditions decided from what was seen, then what the
    // conditions select, the branches' required names and counts, and the alternatives.
    private static void LowerFusedObject(ISchemaEmitter emitter, SchemaNode[] nodes, SchemaNode node, HashSet<int> requested, Queue<int> pending)
    {
        FusedObject f = node.Fused!;
        FusedContributor[] contributors = f.Contributors;

        // The count bounds of the branches that always apply, as one test before the pass.
        int min = -1;
        int max = -1;
        foreach (FusedContributor contributor in contributors)
        {
            if (f.HasCountBounds && contributor.Condition < 0 && contributor.AltGroup < 0)
            {
                min = Math.Max(min, contributor.MinProperties);
                max = contributor.MaxProperties < 0 ? max : max < 0 ? contributor.MaxProperties : Math.Min(max, contributor.MaxProperties);
            }
        }

        emitter.BeginObject(0, integerOnly: false, lexical: false, acceptsObject: true, min, max, otherwiseInterpreted: node.Id);

        // The first pass: each known name's value tests and unconditional applications; an unknown name against
        // the unconditional branches' patterns and additional schemas.
        int[] slots = new int[f.EntryList.Length];
        emitter.BeginNameDispatch(f.Entries);
        for (int i = 0; i < f.EntryList.Length; i++)
        {
            FusedEntry entry = f.EntryList[i];
            emitter.BeginCase(i);

            // A repeated name would be applied twice by the interpreter; the object is its to evaluate.
            emitter.ReturnInterpretedIfSeen(i, node.Id);
            emitter.MarkSeen(i);
            if (entry.HasValueTests)
            {
                emitter.FusedValueTests(entry, f.Conditions.Length);
            }

            slots[i] = -1;
            foreach (FusedApplication application in entry.Applications)
            {
                FusedContributor applied = contributors[application.Contributor];
                if (applied.Condition >= 0)
                {
                    if (slots[i] < 0)
                    {
                        slots[i] = emitter.DeclareValueSlot();
                        emitter.StoreValue(slots[i]);
                    }
                }
                else if (applied.AltGroup < 0)
                {
                    Apply(in application);
                }
                else
                {
                    // A branch of an alternative group fails on its own.
                    emitter.BeginTry();
                    Apply(in application);
                    emitter.OnFail();
                    emitter.MarkAlternativeFailed(applied.AltGroup, applied.AltBranch);
                    emitter.EndTry();
                }
            }

            emitter.EndCase();
        }

        emitter.BeginCase(-1);
        if (f.ResolvesUnknownNames)
        {
            foreach (FusedAbsentPattern absent in f.AbsentPatterns)
            {
                emitter.BeginIfNameMatches(absent.Matcher);
                emitter.MarkConditionFailed(absent.Condition);
                emitter.EndIf();
            }

            foreach (FusedContributor contributor in contributors)
            {
                if (contributor.Condition >= 0 || !ResolvesUnknown(contributor))
                {
                    continue;
                }

                if (contributor.AltGroup < 0)
                {
                    Unknown(contributor);
                }
                else
                {
                    emitter.BeginTry();
                    Unknown(contributor);
                    emitter.OnFail();
                    emitter.MarkAlternativeFailed(contributor.AltGroup, contributor.AltBranch);
                    emitter.EndTry();
                }
            }
        }

        emitter.EndCase();
        emitter.EndNameDispatch();
        emitter.EndProperties();

        // The conditions, then the gates along each chain.
        for (int i = 0; i < f.Conditions.Length; i++)
        {
            emitter.DecideCondition(i, Mask(f.Conditions[i].RequiredBits));
        }

        for (int i = 0; i < f.Conditions.Length; i++)
        {
            emitter.DecideGate(i, f.Conditions[i].Gate, f.Conditions[i].GatePolarity);
        }

        // The applications the conditions select, at the values kept in the pass.
        var active = new List<(int Condition, bool Polarity)>();
        for (int i = 0; i < f.EntryList.Length; i++)
        {
            if (slots[i] < 0)
            {
                continue;
            }

            emitter.BeginIfSeen(i);
            emitter.UseStoredValue(slots[i]);
            foreach (FusedApplication application in f.EntryList[i].Applications)
            {
                FusedContributor applied = contributors[application.Contributor];
                if (applied.Condition < 0)
                {
                    continue;
                }

                active.Clear();
                active.Add((applied.Condition, applied.Polarity));
                foreach (int other in application.OtherContributors ?? [])
                {
                    if (contributors[other].Condition >= 0)
                    {
                        active.Add((contributors[other].Condition, contributors[other].Polarity));
                    }
                }

                emitter.BeginIfActive(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(active));
                Apply(in application);
                emitter.EndIf();
            }

            emitter.EndIf();
        }

        // The unknown names against the conditional branches that resolve them: a second pass.
        if (f.ResolvesUnknownNames && Array.Exists(contributors, c => c.Condition >= 0 && ResolvesUnknown(c)))
        {
            emitter.BeginPropertiesAgain();
            emitter.BeginNameDispatch(f.Entries);
            for (int i = 0; i < f.EntryList.Length; i++)
            {
                emitter.BeginCase(i);
                emitter.EndCase();
            }

            emitter.BeginCase(-1);
            foreach (FusedContributor contributor in contributors)
            {
                if (contributor.Condition >= 0 && ResolvesUnknown(contributor))
                {
                    emitter.BeginIfActive([(contributor.Condition, contributor.Polarity)]);
                    Unknown(contributor);
                    emitter.EndIf();
                }
            }

            emitter.EndCase();
            emitter.EndNameDispatch();
            emitter.EndProperties();
        }

        // Each branch's count bounds and required names: a conditional branch when its condition selects it, and a
        // branch of an alternative group failing on its own.
        foreach (FusedContributor contributor in contributors)
        {
            ulong required = Mask(contributor.RequiredBits);
            bool counts = contributor.MinProperties >= 0 || contributor.MaxProperties >= 0;
            if (contributor.Condition < 0 && contributor.AltGroup < 0)
            {
                emitter.FailUnlessSeen(required);
                continue;
            }

            if (required == 0 && !counts)
            {
                continue;
            }

            if (contributor.Condition >= 0)
            {
                emitter.BeginIfActive([(contributor.Condition, contributor.Polarity)]);
                emitter.FailUnlessPropertyCount(contributor.MinProperties, contributor.MaxProperties);
            }

            if (contributor.AltGroup < 0)
            {
                emitter.FailUnlessSeen(required);
            }
            else
            {
                emitter.BeginTry();
                if (contributor.Condition < 0)
                {
                    emitter.FailUnlessPropertyCount(contributor.MinProperties, contributor.MaxProperties);
                }

                emitter.FailUnlessSeen(required);
                emitter.OnFail();
                emitter.MarkAlternativeFailed(contributor.AltGroup, contributor.AltBranch);
                emitter.EndTry();
            }

            if (contributor.Condition >= 0)
            {
                emitter.EndIf();
            }
        }

        // unevaluatedProperties: a last pass over the properties no branch covered. Without alternative groups a
        // known name is covered by any resolution that applies to it (always, or when a condition selects it), and
        // an unknown name by a branch, applying, whose pattern matches it or whose additional schema takes it.
        if (f.Unevaluated.IsPresent)
        {
            SchemaNode unevaluated = nodes[f.Unevaluated.FastNode];
            emitter.BeginPropertiesAgain();
            emitter.BeginNameDispatch(f.Entries);
            for (int i = 0; i < f.EntryList.Length; i++)
            {
                emitter.BeginCase(i);
                FusedApplication[] applications = f.EntryList[i].Applications;
                if (!Array.Exists(applications, a => contributors[a.Contributor].Condition < 0))
                {
                    active.Clear();
                    foreach (FusedApplication application in applications)
                    {
                        active.Add((contributors[application.Contributor].Condition, contributors[application.Contributor].Polarity));
                        foreach (int other in application.OtherContributors ?? [])
                        {
                            if (contributors[other].Condition >= 0)
                            {
                                active.Add((contributors[other].Condition, contributors[other].Polarity));
                            }
                        }
                    }

                    if (active.Count == 0)
                    {
                        Unevaluated();
                    }
                    else
                    {
                        emitter.BeginIfActive(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(active));
                        emitter.Else();
                        Unevaluated();
                        emitter.EndIf();
                    }
                }

                emitter.EndCase();
            }

            emitter.BeginCase(-1);
            emitter.SetMatched(false);
            if (f.ResolvesUnknownNames)
            {
                foreach (FusedContributor contributor in contributors)
                {
                    bool takesAll = contributor.AdditionalNode >= 0 || contributor.AdditionalCoversOnly;
                    if (!takesAll && contributor.Patterns is null)
                    {
                        continue;
                    }

                    if (contributor.Condition >= 0)
                    {
                        emitter.BeginIfActive([(contributor.Condition, contributor.Polarity)]);
                    }

                    if (takesAll)
                    {
                        emitter.SetMatched(true);
                    }
                    else
                    {
                        foreach (PatternPropertyEntry pattern in contributor.Patterns!)
                        {
                            emitter.BeginIfNameMatches(pattern.Matcher);
                            emitter.SetMatched(true);
                            emitter.EndIf();
                        }
                    }

                    if (contributor.Condition >= 0)
                    {
                        emitter.EndIf();
                    }
                }
            }

            emitter.BeginIfNotMatched();
            Unevaluated();
            emitter.EndIf();
            emitter.EndCase();
            emitter.EndNameDispatch();
            emitter.EndProperties();

            void Unevaluated()
            {
                if (unevaluated.AlwaysFalse)
                {
                    emitter.Fail();
                }
                else if (!unevaluated.AlwaysTrue)
                {
                    LowerChild(emitter, nodes, 0, 0, f.Unevaluated.FastNode, requested, pending);
                }
            }
        }

        for (int g = 0; g < f.AltGroups.Length; g++)
        {
            emitter.FailUnlessAlternativeSurvives(g, f.AltGroups[g].BranchCount, f.AltGroups[g].ExactlyOne);
        }

        // Required-only anyOf/oneOf, and not: {required}, from the seen bits.
        foreach (FusedAlternative alternative in f.Alternatives)
        {
            if (alternative.Condition >= 0)
            {
                emitter.BeginIfActive([(alternative.Condition, alternative.Polarity)]);
            }

            if (alternative.ExactlyOne)
            {
                emitter.BeginCount();
                foreach (int[] branch in alternative.Branches)
                {
                    emitter.CountSeen(Mask(branch));
                }

                emitter.FailUnlessCountedOne();
            }
            else
            {
                emitter.BeginAlternatives();
                foreach (int[] branch in alternative.Branches)
                {
                    emitter.OrSeen(Mask(branch));
                }

                emitter.EndAlternatives();
            }

            if (alternative.Condition >= 0)
            {
                emitter.EndIf();
            }
        }

        foreach (FusedAlternative forbidden in f.Forbidden)
        {
            if (forbidden.Condition >= 0)
            {
                emitter.BeginIfActive([(forbidden.Condition, forbidden.Polarity)]);
            }

            emitter.FailIfSeen(Mask(forbidden.Branches[0]));
            if (forbidden.Condition >= 0)
            {
                emitter.EndIf();
            }
        }

        emitter.Succeed();

        static bool ResolvesUnknown(FusedContributor contributor) => contributor.Patterns is not null || contributor.AdditionalNode >= 0;

        // One resolution of the current value (Evaluator.ApplyApplication).
        void Apply(in FusedApplication application)
        {
            if (application.Node < 0)
            {
                return;
            }

            if (application.TokenBits != 0)
            {
                emitter.FailUnlessToken(application.TokenBits, application.IntegerOnly, application.InlineLexical);
            }
            else if (application.InlineConst is byte[] expected)
            {
                emitter.FailUnlessStringConst(expected);
            }
            else if (application.InlineEnum is Utf8NameMap<object> allowed)
            {
                emitter.FailUnlessStringSet(allowed);
            }
            else
            {
                LowerChild(emitter, nodes, 0, 0, application.Node, requested, pending);
            }
        }

        // A branch's resolution of a name no entry knows (Evaluator.ResolveUnknownName): every pattern it matches,
        // and the additional schema when it matches none.
        void Unknown(FusedContributor contributor)
        {
            if (contributor.Patterns is PatternPropertyEntry[] patterns)
            {
                emitter.SetMatched(false);
                foreach (PatternPropertyEntry pattern in patterns)
                {
                    emitter.BeginIfNameMatches(pattern.Matcher);
                    emitter.SetMatched(true);
                    if (!nodes[pattern.Schema.FastNode].AlwaysTrue)
                    {
                        LowerChild(emitter, nodes, 0, 0, pattern.Schema.FastNode, requested, pending);
                    }

                    emitter.EndIf();
                }

                if (contributor.AdditionalNode >= 0)
                {
                    emitter.BeginIfNotMatched();
                    LowerChild(emitter, nodes, 0, 0, contributor.AdditionalNode, requested, pending);
                    emitter.EndIf();
                }
            }
            else if (contributor.AdditionalNode >= 0)
            {
                LowerChild(emitter, nodes, 0, 0, contributor.AdditionalNode, requested, pending);
            }
        }

        static ulong Mask(int[] bits)
        {
            ulong mask = 0;
            foreach (int bit in bits)
            {
                mask |= 1UL << bit;
            }

            return mask;
        }
    }

    // The interpreter's type dispatch (Evaluator.EvalTypeDispatchPlan): the token type selects the one branch that can match.
    private static void LowerTypeDispatch(ISchemaEmitter emitter, SchemaNode[] nodes, SchemaNode node, HashSet<int> requested, Queue<int> pending)
    {
        int[] dispatch = node.InPlaceDispatch!;
        int[] childByToken = new int[dispatch.Length];
        bool[] generatedByToken = new bool[dispatch.Length];
        for (int token = 0; token < dispatch.Length; token++)
        {
            childByToken[token] = -1;
            if (dispatch[token] >= 0)
            {
                (childByToken[token], generatedByToken[token]) = Target(nodes, node.InPlaceBranches![dispatch[token]].FastNode, requested, pending);
            }
        }

        emitter.ReturnChildByToken(childByToken, generatedByToken);
    }

    // The interpreter's simple array (Evaluator.EvalSimpleArrayFast): a value that is not an array fails a type and
    // passes otherwise; the items are a leaf.
    private static void LowerSimpleArray(ISchemaEmitter emitter, SchemaNode[] nodes, SchemaNode node, HashSet<int> requested, Queue<int> pending)
    {
        ushort otherTokens = node.HasType ? (ushort)0 : ushort.MaxValue;
        SchemaNode items = nodes[node.Items.FastNode];
        if (items.AlwaysTrue)
        {
            emitter.ReturnArrayWithoutItems(otherTokens, integerOnly: false, lexical: false, acceptsArray: true, node.MinItems, node.MaxItems, node.UniqueItems);
            return;
        }

        emitter.BeginArray(otherTokens, integerOnly: false, lexical: false, acceptsArray: true, node.MinItems, node.MaxItems, node.UniqueItems);
        if (items.IsTypeOnly)
        {
            emitter.FailUnlessToken(StrictEntry.TokenBitsOf(items.Type), (items.Type & TypeMask.Integer) != 0 && (items.Type & TypeMask.Number) == 0, items.Dialect == JsonSchemaDialect.Draft4);
        }
        else
        {
            LowerChild(emitter, nodes, 0, 0, items.Id, requested, pending);
        }

        emitter.EndArray();
    }

    // The interpreter's array-of-items plan (Evaluator.EvalArrayItemsPlan): prefix items by position, or one items
    // schema, with uniqueItems tested before the pass.
    private static void LowerArrayItems(ISchemaEmitter emitter, SchemaNode[] nodes, SchemaNode node, HashSet<int> requested, Queue<int> pending)
    {
        ushort otherTokens = OtherTokens(node, JsonTokenType.StartArray);
        bool lexical = (node.Flags & NodeFlags.Draft4) != 0;
        bool acceptsArray = !node.HasType || (node.Type & TypeMask.Array) != 0;
        if (node.PrefixEntries is StrictEntry[] prefix)
        {
            // Prefix items: each leading position's resolution, then the rest's (the last entry) for every item after.
            int rest = prefix.Length - 1;
            emitter.BeginArray(otherTokens, IntegerOnly(node), lexical, acceptsArray, node.MinItems, node.MaxItems);
            emitter.BeginPositionDispatch(rest);
            for (int i = 0; i < rest; i++)
            {
                emitter.BeginCase(i);
                LowerEntry(emitter, nodes, in prefix[i], -1, 0, requested, pending);
                emitter.EndCase();
            }

            emitter.BeginCase(-1);
            LowerEntry(emitter, nodes, in prefix[rest], -1, 0, requested, pending);
            emitter.EndCase();
            emitter.EndNameDispatch();
            emitter.EndArray();
            return;
        }

        if (!node.Items.IsPresent || nodes[node.Items.FastNode].Plan == NodePlan.AlwaysTrue)
        {
            emitter.ReturnArrayWithoutItems(otherTokens, IntegerOnly(node), lexical, acceptsArray, node.MinItems, node.MaxItems, node.UniqueItems);
            return;
        }

        emitter.BeginArray(otherTokens, IntegerOnly(node), lexical, acceptsArray, node.MinItems, node.MaxItems, node.UniqueItems);
        if (node.UniqueItems)
        {
            // With uniqueItems the interpreter enters the items schema for every item, whatever its type decides.
            LowerChild(emitter, nodes, 0, 0, node.Items.FastNode, requested, pending);
        }
        else
        {
            LowerChild(emitter, nodes, node.ItemsDecided, node.ItemsAccepts, node.Items.FastNode, requested, pending);
        }

        emitter.EndArray();
    }

    // The interpreter's strict object plan (Evaluator.EvalStrictObjectPlan and its loops), written out for this node.
    private static void LowerStrictObject(ISchemaEmitter emitter, SchemaNode[] nodes, SchemaNode node, HashSet<int> requested, Queue<int> pending)
    {
        bool acceptsObject = !node.HasType || (node.Type & TypeMask.Object) != 0;
        emitter.BeginObject(OtherTokens(node, JsonTokenType.StartObject), IntegerOnly(node), (node.Flags & NodeFlags.Draft4) != 0, acceptsObject, node.MinProperties, node.MaxProperties);
        if (node.Properties is not Utf8NameMap<PropertyEntry> properties)
        {
            // A map: no names are read, and every value takes the additional resolution's type test or child.
            if (node.AdditionalRejects)
            {
                emitter.Fail();
            }
            else if (node.AdditionalEntry.TokenBits != 0)
            {
                emitter.FailUnlessToken(node.AdditionalEntry.TokenBits, node.AdditionalEntry.IntegerOnly, node.AdditionalEntry.Lexical);
            }
            else if (node.AdditionalEntry.Child >= 0)
            {
                LowerChild(emitter, nodes, node.AdditionalEntry.ChildDecided, node.AdditionalEntry.ChildAccepts, node.AdditionalEntry.Child, requested, pending);
            }

            emitter.EndObject(0);
            return;
        }

        StrictEntry[] entries = node.StrictEntries ?? [];
        emitter.BeginNameDispatch(properties);
        for (int i = 0; i < properties.Count; i++)
        {
            emitter.BeginCase(i);
            LowerEntry(emitter, nodes, in entries[i], entries[i].SeenBit, node.RequiredMask, requested, pending);
            emitter.EndCase();
        }

        emitter.BeginCase(-1);
        if (node.AdditionalRejects)
        {
            emitter.Fail();
        }
        else
        {
            LowerEntry(emitter, nodes, in node.AdditionalEntry, node.AdditionalEntry.SeenBit, node.RequiredMask, requested, pending);
        }

        emitter.EndCase();
        emitter.EndNameDispatch();
        emitter.EndObject(node.RequiredMask);
    }

    // One property's resolution, in the order the interpreter's loop tests it.
    private static void LowerEntry(ISchemaEmitter emitter, SchemaNode[] nodes, in StrictEntry entry, int seenBit, ulong requiredMask, HashSet<int> requested, Queue<int> pending)
    {
        // Only the required bits are read back.
        if (seenBit >= 0 && (requiredMask & (1UL << seenBit)) != 0)
        {
            emitter.MarkSeen(seenBit);
        }

        if (entry.TokenBits != 0)
        {
            emitter.FailUnlessToken(entry.TokenBits, entry.IntegerOnly, entry.Lexical);
        }
        else if (entry.Set is Utf8NameMap<object> allowed)
        {
            emitter.FailUnlessStringSet(allowed);
        }
        else if (entry.ConstBytes is byte[] expected)
        {
            emitter.FailUnlessStringConst(expected);
        }
        else if (entry.Child >= 0)
        {
            LowerChild(emitter, nodes, entry.ChildDecided, entry.ChildAccepts, entry.Child, requested, pending);
        }
        else if (entry.LengthBounded)
        {
            emitter.FailUnlessLength(in entry);
        }
    }

    private static void LowerChild(ISchemaEmitter emitter, SchemaNode[] nodes, ushort decided, ushort accepts, int child, HashSet<int> requested, Queue<int> pending)
    {
        (int target, bool generated) = Target(nodes, child, requested, pending);
        emitter.FailUnlessChild(decided, accepts, target, generated);
    }

    // A child's target, its forwards taken as the interpreter takes them: the node whose generated method to call
    // (requested here), or the child itself for the interpreter.
    private static (int Node, bool Generated) Target(SchemaNode[] nodes, int child, HashSet<int> requested, Queue<int> pending)
    {
        int target = child;
        for (int hops = 0; hops < MaxForwards && nodes[target].Plan == NodePlan.Forward; hops++)
        {
            target = nodes[target].ForwardNode;
        }

        if (!IsSpecialised(nodes, nodes[target]))
        {
            return (child, false);
        }

        if (requested.Add(target))
        {
            pending.Enqueue(target);
        }

        return (target, true);
    }
}
#endif