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
/// place, and arrays of items (<see cref="NodePlan.SimpleArray"/> and <see cref="NodePlan.ArrayItems"/>, without
/// prefix items or <c>uniqueItems</c>); a child that is specialised is called as a generated method. Every other node
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
            if (nodes[i] is SchemaNode node && IsSpecialised(nodes, node) && requested.Add(i))
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
                    LowerSimpleArray(emitter, nodes, node);
                    break;
                default:
                    LowerArrayItems(emitter, nodes, node, requested, pending);
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
            case NodePlan.StrictObject:
                return (node.Properties?.Count ?? 0) <= MaxDispatchNames;
            case NodePlan.SimpleArray:
                // Its items are a leaf: nothing to test, a type, or the interpreter's leaf evaluation.
                SchemaNode items = nodes[node.Items.FastNode];
                return !node.UniqueItems && (items.AlwaysTrue || items.IsTypeOnly || items.Plan == NodePlan.Leaf);
            case NodePlan.ArrayItems:
                return !node.UniqueItems && node.PrefixEntries is null;
            default:
                return false;
        }
    }

    // The token types a node's type accepts for a value that is not the container it specialises (every type when
    // the node has no type), as Evaluator.MatchesType decides them.
    private static ushort OtherTokens(SchemaNode node, JsonTokenType container) => node.HasType ? (ushort)(StrictEntry.TokenBitsOf(node.Type) & ~(1 << (int)container)) : ushort.MaxValue;

    private static bool IntegerOnly(SchemaNode node) => node.HasType && (node.Type & TypeMask.Integer) != 0 && (node.Type & TypeMask.Number) == 0;

    // The interpreter's simple array (Evaluator.EvalSimpleArrayFast): a value that is not an array fails a type and
    // passes otherwise; the items are a leaf.
    private static void LowerSimpleArray(ISchemaEmitter emitter, SchemaNode[] nodes, SchemaNode node)
    {
        ushort otherTokens = node.HasType ? (ushort)0 : ushort.MaxValue;
        SchemaNode items = nodes[node.Items.FastNode];
        if (items.AlwaysTrue)
        {
            emitter.ReturnArrayWithoutItems(otherTokens, integerOnly: false, lexical: false, acceptsArray: true, node.MinItems, node.MaxItems);
            return;
        }

        emitter.BeginArray(otherTokens, integerOnly: false, lexical: false, acceptsArray: true, node.MinItems, node.MaxItems);
        if (items.IsTypeOnly)
        {
            emitter.FailUnlessToken(StrictEntry.TokenBitsOf(items.Type), (items.Type & TypeMask.Integer) != 0 && (items.Type & TypeMask.Number) == 0, items.Dialect == JsonSchemaDialect.Draft4);
        }
        else
        {
            emitter.FailUnlessChild(0, 0, items.Id, generated: false);
        }

        emitter.EndArray();
    }

    // The interpreter's array-of-items plan (Evaluator.EvalArrayItemsPlan), without prefix items or uniqueItems.
    private static void LowerArrayItems(ISchemaEmitter emitter, SchemaNode[] nodes, SchemaNode node, HashSet<int> requested, Queue<int> pending)
    {
        ushort otherTokens = OtherTokens(node, JsonTokenType.StartArray);
        bool lexical = (node.Flags & NodeFlags.Draft4) != 0;
        bool acceptsArray = !node.HasType || (node.Type & TypeMask.Array) != 0;
        if (!node.Items.IsPresent || nodes[node.Items.FastNode].Plan == NodePlan.AlwaysTrue)
        {
            emitter.ReturnArrayWithoutItems(otherTokens, IntegerOnly(node), lexical, acceptsArray, node.MinItems, node.MaxItems);
            return;
        }

        emitter.BeginArray(otherTokens, IntegerOnly(node), lexical, acceptsArray, node.MinItems, node.MaxItems);
        LowerChild(emitter, nodes, node.ItemsDecided, node.ItemsAccepts, node.Items.FastNode, requested, pending);
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
            LowerEntry(emitter, nodes, in entries[i], node.RequiredMask, requested, pending);
            emitter.EndCase();
        }

        emitter.BeginCase(-1);
        if (node.AdditionalRejects)
        {
            emitter.Fail();
        }
        else
        {
            LowerEntry(emitter, nodes, in node.AdditionalEntry, node.RequiredMask, requested, pending);
        }

        emitter.EndCase();
        emitter.EndNameDispatch();
        emitter.EndObject(node.RequiredMask);
    }

    // One property's resolution, in the order the interpreter's loop tests it.
    private static void LowerEntry(ISchemaEmitter emitter, SchemaNode[] nodes, in StrictEntry entry, ulong requiredMask, HashSet<int> requested, Queue<int> pending)
    {
        // Only the required bits are read back.
        if (entry.SeenBit >= 0 && (requiredMask & (1UL << entry.SeenBit)) != 0)
        {
            emitter.MarkSeen(entry.SeenBit);
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
        // The child's target, its forwards taken as the interpreter takes them.
        int target = child;
        for (int hops = 0; hops < MaxForwards && nodes[target].Plan == NodePlan.Forward; hops++)
        {
            target = nodes[target].ForwardNode;
        }

        if (!IsSpecialised(nodes, nodes[target]))
        {
            emitter.FailUnlessChild(decided, accepts, child, generated: false);
            return;
        }

        if (requested.Add(target))
        {
            pending.Enqueue(target);
        }

        emitter.FailUnlessChild(decided, accepts, target, generated: true);
    }
}
#endif