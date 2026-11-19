// <copyright file="GeneratedProgram.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET && !STJ
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;

namespace Corvus.Text.Json.RuntimeEvaluator.CodeGeneration;

/// <summary>
/// A program's generated code for one of its node arrays: a method for every node the lowering specialises, whatever
/// the entry, so that every evaluator of the program (each of its entry points, as a generated program has one per
/// type) uses the same code, and each has only an entry of its own (see <see cref="GetEntryAddress"/>).
/// </summary>
[RequiresDynamicCode(SchemaLowering.RequiresDynamicCodeMessage)]
internal sealed class GeneratedProgram
{
    private readonly ModuleBuilder? module;
    private readonly Type? created;
    private readonly Dictionary<int, nint> entries = [];

    /// <summary>Initializes a new instance of the <see cref="GeneratedProgram"/> class.</summary>
    /// <param name="sourceNodes">The program's node array the code was generated from.</param>
    /// <param name="nodes">The node array to evaluate with: the source's, with the specialised nodes replaced.</param>
    /// <param name="specialised">The number of nodes the lowering specialised.</param>
    /// <param name="methods">Each specialised node's method.</param>
    /// <param name="module">The module of the code's type, or <see langword="null"/> when no node is specialised.</param>
    /// <param name="created">The code's type, or <see langword="null"/> when no node is specialised.</param>
    /// <param name="prepared">Whether every method has been compiled (otherwise each is compiled at its first call).</param>
    public GeneratedProgram(SchemaNode[] sourceNodes, SchemaNode[] nodes, int specialised, Dictionary<int, NodeValidator> methods, ModuleBuilder? module, Type? created, bool prepared)
    {
        this.Prepared = prepared;
        this.SourceNodes = sourceNodes;
        this.Nodes = nodes;
        this.Specialised = specialised;
        this.Methods = methods;
        this.module = module;
        this.created = created;
    }

    /// <summary>Gets the program's node array the code was generated from.</summary>
    public SchemaNode[] SourceNodes { get; }

    /// <summary>Gets the node array to evaluate with: the source's, with the specialised nodes replaced.</summary>
    public SchemaNode[] Nodes { get; }

    /// <summary>Gets the number of nodes the lowering specialised.</summary>
    public int Specialised { get; }

    /// <summary>Gets each specialised node's method.</summary>
    public Dictionary<int, NodeValidator> Methods { get; }

    /// <summary>Gets a value indicating whether every method has been compiled (see <see cref="PrepareAll"/>).</summary>
    public bool Prepared { get; private set; }

    /// <summary>
    /// Compiles every method that has not been compiled yet: for an evaluator that asks for its code before its first
    /// evaluation (Eager) where the program's code was generated for one that left each method to its first call.
    /// </summary>
    public void PrepareAll()
    {
        lock (this.Methods)
        {
            if (this.Prepared)
            {
                return;
            }

            foreach (NodeValidator method in this.Methods.Values)
            {
                System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(method.Method.MethodHandle);
            }

            this.Prepared = true;
        }
    }

    /// <summary>
    /// Gets the address of the entry for an entry node that has a method, defined and compiled on first request (a
    /// few dozen bytes of IL) and shared by every evaluator with that entry node.
    /// </summary>
    /// <param name="entryNode">The entry node.</param>
    /// <returns>The entry's address (see <see cref="Evaluation.CompiledEntry.EntryAddress"/>).</returns>
    public nint GetEntryAddress(int entryNode)
    {
        lock (this.entries)
        {
            if (!this.entries.TryGetValue(entryNode, out nint address))
            {
                address = IlSchemaEmitter.EmitEntry(this.module!, this.created!, entryNode);
                this.entries[entryNode] = address;
            }

            return address;
        }
    }
}
#endif