// <copyright file="ISchemaEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET && !STJ
namespace Corvus.Text.Json.RuntimeEvaluator.CodeGeneration;

/// <summary>
/// What the lowering asks a backend to write. The lowering decides the code's shape (which nodes get a method, what
/// each does inline); a backend only writes it: IL into a collectible assembly now, C# source later.
/// </summary>
internal interface ISchemaEmitter
{
    /// <summary>Starts the method for a node.</summary>
    /// <param name="nodeId">The node.</param>
    void BeginMethod(int nodeId);

    /// <summary>Returns the interpreter's result for a node at the current value: for nodes the lowering does not specialise.</summary>
    /// <param name="nodeId">The node to evaluate.</param>
    void ReturnInterpreted(int nodeId);

    /// <summary>Finishes the current method.</summary>
    void EndMethod();
}
#endif