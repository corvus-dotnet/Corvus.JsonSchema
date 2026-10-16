// <copyright file="IModelTypeMap.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// What a client emitter knows about the generated model types: the type reference for a schema position, whether
/// that type has a builder shape the client can offer as a closure-free overload, and the type to use where no
/// model type was generated.
/// </summary>
public interface IModelTypeMap
{
    /// <summary>
    /// Gets the type reference used where a schema position has no generated type (in C#, <c>JsonElement</c>).
    /// </summary>
    string FallbackTypeReference { get; }

    /// <summary>
    /// Gets the type reference generated for a schema position.
    /// </summary>
    /// <param name="schemaPointer">The JSON Pointer of the schema within the document.</param>
    /// <param name="typeReference">The type reference, when one was generated.</param>
    /// <returns><see langword="true"/> when a type was generated for the position.</returns>
    bool TryGetTypeReference(string schemaPointer, out string typeReference);

    /// <summary>
    /// Gets whether the type generated for a schema position has a builder shape (in C#, a context-threaded
    /// <c>Source&lt;TContext&gt;</c>), so the client can offer the closure-free overload.
    /// </summary>
    /// <param name="schemaPointer">The JSON Pointer of the schema within the document.</param>
    /// <returns><see langword="true"/> when the generated type has a builder shape.</returns>
    bool HasBuilder(string schemaPointer);
}