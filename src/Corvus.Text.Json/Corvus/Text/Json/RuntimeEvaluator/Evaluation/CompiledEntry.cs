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
internal sealed class CompiledEntry(NodeValidator compiled, CompiledSchema program, SchemaNode[] nodes, int entryResource, int maxDepth, int rootNode)
{
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