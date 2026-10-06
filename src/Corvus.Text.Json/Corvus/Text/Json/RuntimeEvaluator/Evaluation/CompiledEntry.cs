// <copyright file="CompiledEntry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET
using Corvus.Text.Json.RuntimeEvaluator.CodeGeneration;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;

namespace Corvus.Text.Json.RuntimeEvaluator.Evaluation;

/// <summary>
/// What flag-mode evaluation through a schema's generated code needs of the schema, gathered once when the code is
/// compiled: the evaluation's entry reads it from one object.
/// </summary>
internal sealed class CompiledEntry(NodeValidator compiled, nint entryAddress, CompiledSchema program, SchemaNode[] sourceNodes, SchemaNode[] nodes, int entryResource, int maxDepth, int rootNode)
{
    /// <summary>The program's node array the code was compiled from: the code is the schema's while the program still has this array.</summary>
    public readonly SchemaNode[] SourceNodes = sourceNodes;

    /// <summary>
    /// The address of the schema's generated entry method, which takes this object, the document (as its class and as
    /// its interface) and the index: it sets up the state in its own frame and runs the entry node's code there.
    /// <see cref="Compiled"/> keeps its code alive (they are methods of one type).
    /// </summary>
    public readonly nint EntryAddress = entryAddress;

    /// <summary>The entry's generated method. The delegate keeps its code alive.</summary>
    public readonly NodeValidator Compiled = compiled;

    /// <summary>The address of <see cref="Compiled"/>'s method, which the evaluation calls.</summary>
    public readonly nint Address = compiled.Method.MethodHandle.GetFunctionPointer();

    /// <summary>The program.</summary>
    public readonly CompiledSchema Program = program;

    /// <summary>The node array carrying the generated methods.</summary>
    public readonly SchemaNode[] Nodes = nodes;

    /// <summary>The resource of the evaluation's entry.</summary>
    public readonly int EntryResource = entryResource;

    /// <summary>The depth limit.</summary>
    public readonly int MaxDepth = maxDepth;

    /// <summary>The program's root node, for a document the generated code cannot read.</summary>
    public readonly int RootNode = rootNode;
}
#endif