// <copyright file="SchemaTypeMap.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// An <see cref="IModelTypeMap"/> over the pointer-to-type-name map and builder-shape set the CLI builds after
/// generating the model types.
/// </summary>
public sealed class SchemaTypeMap : IModelTypeMap
{
    private readonly IReadOnlyDictionary<string, string> typeNames;
    private readonly IReadOnlySet<string> builderPointers;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaTypeMap"/> class.
    /// </summary>
    /// <param name="typeNames">The generated type name for each schema pointer that got one.</param>
    /// <param name="builderPointers">The subset of pointers whose generated type has a builder shape.</param>
    /// <param name="fallbackTypeReference">The type reference for positions without a generated type.</param>
    public SchemaTypeMap(IReadOnlyDictionary<string, string> typeNames, IReadOnlySet<string>? builderPointers, string fallbackTypeReference)
    {
        this.typeNames = typeNames;
        this.builderPointers = builderPointers ?? new HashSet<string>(StringComparer.Ordinal);
        this.FallbackTypeReference = fallbackTypeReference;
    }

    /// <inheritdoc/>
    public string FallbackTypeReference { get; }

    /// <inheritdoc/>
    public bool TryGetTypeReference(string schemaPointer, [NotNullWhen(true)] out string typeReference)
        => this.typeNames.TryGetValue(schemaPointer, out typeReference!);

    /// <inheritdoc/>
    public bool HasBuilder(string schemaPointer) => this.builderPointers.Contains(schemaPointer);
}