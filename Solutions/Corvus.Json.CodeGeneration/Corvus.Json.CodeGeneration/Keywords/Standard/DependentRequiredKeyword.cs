// <copyright file="DependentRequiredKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The dependentRequired keyword.
/// </summary>
public sealed class DependentRequiredKeyword : IPropertyProviderKeyword, IObjectDependentRequiredValidationKeyword
{
    private const string KeywordPath = "#/dependentRequired";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

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
                        target,
                        Uri.UnescapeDataString(property.Name),
                        WellKnownTypeDeclarations.JsonAny,
                        RequiredOrOptional.Optional,
                        source == target ? LocalOrComposed.Local : LocalOrComposed.Composed,
                        this,
                        null));

                foreach (JsonElement requiredValue in property.Value.EnumerateArray())
                {
                    string propertyName = property.Value.GetString() ?? throw new InvalidOperationException("The dependentRequired properties must be strings.");
                    target.AddOrUpdatePropertyDeclaration(
                        new PropertyDeclaration(
                            target,
                            Uri.UnescapeDataString(propertyName),
                            WellKnownTypeDeclarations.JsonAny,
                            RequiredOrOptional.Optional,
                            source == target ? LocalOrComposed.Local : LocalOrComposed.Composed,
                            this,
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

    /// <inheritdoc/>
    public bool RequiresObjectEnumeration(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public string GetPathModifier(DependentRequiredDeclaration dependentRequired, int index)
    {
        return KeywordPathReference.AppendArrayIndexToFragment(index);
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<DependentRequiredDeclaration> GetDependentRequiredDeclarations(TypeDeclaration typeDeclaration)
    {
        List<DependentRequiredDeclaration> declarations = [];

        if (typeDeclaration.TryGetKeyword(this, out JsonElement value) &&
            value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    declarations.Add(
                        new(
                            this,
                            Uri.UnescapeDataString(property.Name),
                            property.Value.EnumerateArray()
                                .Select(a => Uri.UnescapeDataString(a.GetString()!))
                                .ToList()));
                }
            }
        }

        return declarations;
    }
}