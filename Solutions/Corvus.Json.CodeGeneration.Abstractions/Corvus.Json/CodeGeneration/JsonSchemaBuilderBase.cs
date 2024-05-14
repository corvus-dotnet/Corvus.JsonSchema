// <copyright file="JsonSchemaBuilderBase.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A base for JSON Schema type builders.
/// </summary>
/// <remarks>
/// Implementers implement this base class and provide overrides to adapt to their
/// code generator template set.
/// </remarks>
public abstract class JsonSchemaBuilderBase : IJsonSchemaBuilder
{
    private readonly JsonSchemaTypeBuilder typeBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSchemaBuilderBase"/> class.
    /// </summary>
    /// <param name="typeBuilder">The type builder to use.</param>
    /// <remarks>
    /// Implementers will typically call this having wrapped the builder with the particular meta schema
    /// configuration e.g.  <c>typeBuilder.UseDraft201909();</c>.
    /// </remarks>
    protected JsonSchemaBuilderBase(JsonSchemaTypeBuilder typeBuilder)
    {
        this.typeBuilder = typeBuilder;
    }

    /// <inheritdoc/>
    public void AddDocument(string path, JsonDocument jsonDocument)
    {
        this.typeBuilder.AddDocument(path, jsonDocument);
    }

    /// <inheritdoc/>
    public async ValueTask<(string RootTypeName, ImmutableDictionary<JsonReference, TypeAndCode> GeneratedTypes)> BuildTypesFor(JsonReference reference, string rootNamespace, bool rebase = false, ImmutableDictionary<string, string>? baseUriToNamespaceMap = null, string? rootTypeName = null)
    {
        TypeDeclaration rootTypeDeclaration = await this.typeBuilder.AddTypeDeclarationsFor(reference, rootNamespace, rebase, baseUriToNamespaceMap, rootTypeName) ?? throw new InvalidOperationException($"Unable to find the root type declaration at {reference}");
        rootTypeName = rootTypeDeclaration.FullyQualifiedDotnetTypeName!;
        ImmutableArray<TypeDeclaration> typesToGenerate = rootTypeDeclaration.GetTypesToGenerate();

        return (
            rootTypeName,
            typesToGenerate.Select(t => (t.LocatedSchema.Location, t)).Select(this.GenerateFilesForType).ToImmutableDictionary(i => i.Location, i => i.TypeAndCode));
    }

    /// <inheritdoc/>
    public (string RootTypeName, ImmutableDictionary<JsonReference, TypeAndCode> GeneratedTypes) SafeBuildTypesFor(JsonReference reference, string rootNamespace, bool rebase = false, ImmutableDictionary<string, string>? baseUriToNamespaceMap = null, string? rootTypeName = null)
    {
        ValueTask<(string RootTypeName, ImmutableDictionary<JsonReference, TypeAndCode> GeneratedTypes)> result =
            this.BuildTypesFor(reference, rootNamespace, rebase, baseUriToNamespaceMap, rootTypeName);

        // Ensure that the result is completed synchronously.
        if (!result.IsCompleted)
        {
            throw new InvalidOperationException("The task could not be completed.");
        }

        return result.Result;
    }

    /// <summary>
    /// Gets the type declaration for a property of a type.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="property">The property that provides a Schema().</param>
    /// <returns>The given type declaration.</returns>
    public virtual TypeDeclaration GetTypeDeclarationForProperty(TypeDeclaration typeDeclaration, string property)
    {
        return typeDeclaration.GetTypeDeclarationForProperty(property);
    }

    /// <summary>
    /// Gets the type declaration for a property of a type.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="patternProperty">The pattern property that provides a Schema().</param>
    /// <returns>The given type declaration.</returns>
    public virtual TypeDeclaration GetTypeDeclarationForPatternProperty(TypeDeclaration typeDeclaration, string patternProperty)
    {
        return typeDeclaration.GetTypeDeclarationForMappedProperty("patternProperties", patternProperty);
    }

    /// <summary>
    /// Gets the type declaration for a dependent of a type.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="dependentSchema">The dependent schema that provides a Schema().</param>
    /// <returns>The given type declaration.</returns>
    public virtual TypeDeclaration GetTypeDeclarationForDependentSchema(TypeDeclaration typeDeclaration, string dependentSchema)
    {
        return typeDeclaration.GetTypeDeclarationForMappedProperty("dependencies", dependentSchema);
    }

    /// <summary>
    /// Gets the type declaration for a Schema() array property at a given index.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="property">The property that provides a Schema().</param>
    /// <param name="index">The index of the Schema() in the array.</param>
    /// <returns>The given type declaration.</returns>
    public virtual TypeDeclaration GetTypeDeclarationForPropertyArrayIndex(TypeDeclaration typeDeclaration, string property, int index)
    {
        return typeDeclaration.GetTypeDeclarationForPropertyArrayIndex(property, index);
    }

    /// <summary>
    /// Gets the file name for a given type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to get the appropriate filename.</param>
    /// <returns>The full filename for the file, taking into account its nesting.</returns>
    protected static string GetDottedFileNameFor(TypeDeclaration typeDeclaration)
    {
        StringBuilder builder = new();
        TypeDeclaration? current = typeDeclaration;
        while (current is not null)
        {
            if (builder.Length > 0)
            {
                builder.Insert(0, ".");
            }

            builder.Insert(0, current.DotnetTypeName);
            current = current.Parent;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Generate the files for the given type.
    /// </summary>
    /// <param name="typeForGeneration">The type for which to generate the code.</param>
    /// <returns>The code generated for the type.</returns>
    protected abstract (JsonReference Location, TypeAndCode TypeAndCode) GenerateFilesForType((JsonReference Location, TypeDeclaration TypeDeclaration) typeForGeneration);
}