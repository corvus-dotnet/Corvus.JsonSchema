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
/// place and their strict-object children called as generated methods. Every other node is evaluated by the
/// interpreter, which does not yet call back into generated code for the values beneath it.
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

    /// <summary>Compiles the flag-mode code for an entry node.</summary>
    /// <param name="nodes">The program's nodes.</param>
    /// <param name="entry">The entry node (after reference elision).</param>
    /// <returns>The entry's generated method.</returns>
    public static NodeValidator Compile(SchemaNode[] nodes, SchemaNode entry) => Compile(nodes, entry, out _);

    /// <summary>Compiles the flag-mode code for an entry node.</summary>
    /// <param name="nodes">The program's nodes.</param>
    /// <param name="entry">The entry node (after reference elision).</param>
    /// <param name="specialised">The number of nodes given specialised methods (the rest run in the interpreter).</param>
    /// <returns>The entry's generated method.</returns>
    public static NodeValidator Compile(SchemaNode[] nodes, SchemaNode entry, out int specialised)
    {
        Interlocked.Increment(ref compiledCount);
        var emitter = new IlSchemaEmitter();
        specialised = Lower(emitter, nodes, entry);
        return emitter.Build(entry.Id);
    }

    private static int Lower(ISchemaEmitter emitter, SchemaNode[] nodes, SchemaNode entry)
    {
        // The entry's method always exists; a child gets one when a generated method calls it.
        var requested = new HashSet<int> { entry.Id };
        var pending = new Queue<int>();
        pending.Enqueue(entry.Id);
        int specialised = 0;
        while (pending.TryDequeue(out int id))
        {
            SchemaNode node = nodes[id];
            emitter.BeginMethod(id);
            if (IsSpecialised(node))
            {
                LowerStrictObject(emitter, nodes, node, requested, pending);
                specialised++;
            }
            else
            {
                emitter.ReturnInterpreted(id);
            }

            emitter.EndMethod();
        }

        return specialised;
    }

    private static bool IsSpecialised(SchemaNode node) => node.Plan == NodePlan.StrictObject && (node.Properties?.Count ?? 0) <= MaxDispatchNames;

    // The interpreter's strict object plan (Evaluator.EvalStrictObjectPlan and its loops), written out for this node.
    private static void LowerStrictObject(ISchemaEmitter emitter, SchemaNode[] nodes, SchemaNode node, HashSet<int> requested, Queue<int> pending)
    {
        ushort otherTokens = node.HasType ? (ushort)(StrictEntry.TokenBitsOf(node.Type) & ~(1 << (int)JsonTokenType.StartObject)) : ushort.MaxValue;
        bool integerOnly = node.HasType && (node.Type & TypeMask.Integer) != 0 && (node.Type & TypeMask.Number) == 0;
        bool acceptsObject = !node.HasType || (node.Type & TypeMask.Object) != 0;
        emitter.BeginObject(otherTokens, integerOnly, (node.Flags & NodeFlags.Draft4) != 0, acceptsObject, node.MinProperties, node.MaxProperties);
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
                LowerChild(emitter, nodes, in node.AdditionalEntry, requested, pending);
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
            LowerChild(emitter, nodes, in entry, requested, pending);
        }
        else if (entry.LengthBounded)
        {
            emitter.FailUnlessLength(in entry);
        }
    }

    private static void LowerChild(ISchemaEmitter emitter, SchemaNode[] nodes, in StrictEntry entry, HashSet<int> requested, Queue<int> pending)
    {
        // The child's target, its forwards taken as the interpreter takes them.
        int target = entry.Child;
        for (int hops = 0; hops < MaxForwards && nodes[target].Plan == NodePlan.Forward; hops++)
        {
            target = nodes[target].ForwardNode;
        }

        if (!IsSpecialised(nodes[target]))
        {
            emitter.FailUnlessChild(entry.ChildDecided, entry.ChildAccepts, entry.Child, generated: false);
            return;
        }

        if (requested.Add(target))
        {
            pending.Enqueue(target);
        }

        emitter.FailUnlessChild(entry.ChildDecided, entry.ChildAccepts, target, generated: true);
    }
}
#endif