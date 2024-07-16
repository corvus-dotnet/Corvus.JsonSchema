// <copyright file="DependentRequiredKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The dependentRequired keyword.
/// </summary>
public sealed class DependentRequiredKeyword : IPropertyProviderKeyword, IObjectValidationKeyword
{
    private DependentRequiredKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="DependentRequiredKeyword"/> keyword.
    /// </summary>
    public static DependentRequiredKeyword Instance { get; } = new DependentRequiredKeyword();

    /// <inheritdoc />
    public string Keyword => "dependentRequired";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "dependentRequired"u8;

    /// <inheritdoc />
    public uint PropertyProviderPriority => PropertyProviderPriorities.First;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Default;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc />
    public void CollectProperties(TypeDeclaration source, TypeDeclaration target, HashSet<TypeDeclaration> visitedTypeDeclarations, bool treatDependentRequiredAsOptional)
    {
        if (source.LocatedSchema.Schema.ValueKind == JsonValueKind.Object &&
            source.LocatedSchema.Schema.TryGetProperty(this.KeywordUtf8, out JsonElement value) &&
            value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                // Add the dependentRequired property itself
                target.AddOrUpdatePropertyDeclaration(
                    new PropertyDeclaration(
                        Uri.UnescapeDataString(property.Name),
                        WellKnownTypeDeclarations.JsonAny,
                        RequiredOrOptional.Optional,
                        source == target ? LocalOrComposed.Local : LocalOrComposed.Composed,
                        null));

                foreach (JsonElement requiredValue in property.Value.EnumerateArray())
                {
                    string propertyName = property.Value.GetString() ?? throw new InvalidOperationException("The dependentRequired properties must be strings.");
                    target.AddOrUpdatePropertyDeclaration(
                        new PropertyDeclaration(
                            Uri.UnescapeDataString(propertyName),
                            WellKnownTypeDeclarations.JsonAny,
                            RequiredOrOptional.Optional,
                            source == target ? LocalOrComposed.Local : LocalOrComposed.Composed,
                            null));
                }
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