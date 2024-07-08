// <copyright file="RequiredKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The required keyword.
/// </summary>
public sealed class RequiredKeyword : IPropertyProviderKeyword, IObjectValidationKeyword
{
    private RequiredKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="RequiredKeyword"/> keyword.
    /// </summary>
    public static RequiredKeyword Instance { get; } = new RequiredKeyword();

    /// <inheritdoc />
    public string Keyword => "required";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "required"u8;

    /// <inheritdoc />
    public uint PropertyProviderPriority => PropertyProviderPriorities.First;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.AfterComposition;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc />
    public void CollectProperties(TypeDeclaration source, TypeDeclaration target, HashSet<TypeDeclaration> visitedTypeDeclarations, bool treatRequiredAsOptional)
    {
        if (source.LocatedSchema.Schema.ValueKind == JsonValueKind.Object &&
            source.LocatedSchema.Schema.TryGetProperty(this.KeywordUtf8, out JsonElement value) &&
            value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement property in value.EnumerateArray())
            {
                string propertyName = property.GetString() ?? throw new InvalidOperationException("The required properties must be strings.");
                target.AddOrUpdatePropertyDeclaration(
                    new PropertyDeclaration(
                        Uri.UnescapeDataString(propertyName),
                        WellKnownTypeDeclarations.JsonAny,
                        treatRequiredAsOptional ? RequiredOrOptional.Optional : RequiredOrOptional.Required,
                        source == target ? LocalOrComposed.Local : LocalOrComposed.Composed));
            }
        }
    }

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.HasKeyword(this)
            ? CoreTypes.Object
            : CoreTypes.None;

    /// <inheritdoc/>
    public bool RequiresPropertyCount(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresPropertyEvaluationTracking(TypeDeclaration typeDeclaration) => false;
}