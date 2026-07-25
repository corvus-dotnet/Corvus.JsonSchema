// <copyright file="RequiredArrayOnlyKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The required keyword, active only when its value is an array of property names.
/// </summary>
/// <remarks>
/// <para>
/// OpenAPI 2.0 (Swagger) Parameter Objects reuse the <c>required</c> name for a
/// <em>boolean</em> field, and the code generator points the type builder directly at
/// Parameter Objects. Under <see cref="RequiredKeyword"/> the mere presence of the
/// keyword implies <see cref="CoreTypes.Object"/> and object enumeration, giving
/// parameter-as-schema types a spurious object face. This variant is inert unless the
/// value is the draft-04 array form.
/// </para>
/// </remarks>
public sealed class RequiredArrayOnlyKeyword : IPropertyProviderKeyword, IObjectRequiredPropertyValidationKeyword
{
    private const string KeywordPath = "#/required";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private RequiredArrayOnlyKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="RequiredArrayOnlyKeyword"/> keyword.
    /// </summary>
    public static RequiredArrayOnlyKeyword Instance { get; } = new RequiredArrayOnlyKeyword();

    /// <inheritdoc />
    public string Keyword => "required";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "required"u8;

    /// <inheritdoc />
    public uint PropertyProviderPriority => PropertyProviderPriorities.First;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.AfterComposition;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) =>
        schemaValue.ValueKind != JsonValueKind.Object ||
        !schemaValue.TryGetProperty(this.KeywordUtf8, out JsonElement value) ||
        value.ValueKind != JsonValueKind.Array;

    /// <inheritdoc />
    public void CollectProperties(TypeDeclaration source, TypeDeclaration target, HashSet<TypeDeclaration> visitedTypeDeclarations, bool treatRequiredAsOptional, CancellationToken cancellationToken)
    {
        if (source.LocatedSchema.Schema.ValueKind == JsonValueKind.Object &&
            source.LocatedSchema.Schema.TryGetProperty(this.KeywordUtf8, out JsonElement value) &&
            value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement property in value.EnumerateArray())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                string propertyName = property.GetString() ?? throw new InvalidOperationException("The required properties must be strings.");
                target.AddOrUpdatePropertyDeclaration(
                    new PropertyDeclaration(
                        target,
                        Uri.UnescapeDataString(propertyName),
                        WellKnownTypeDeclarations.JsonAny,
                        treatRequiredAsOptional ? RequiredOrOptional.Optional : source == target ? RequiredOrOptional.Required : RequiredOrOptional.ComposedRequired,
                        source == target ? LocalOrComposed.Local : LocalOrComposed.Composed,
                        this,
                        this));
            }
        }
    }

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.TryGetKeyword(this, out JsonElement value) && value.ValueKind == JsonValueKind.Array
            ? CoreTypes.Object
            : CoreTypes.None;

    /// <inheritdoc/>
    public bool RequiresPropertyCount(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresPropertyEvaluationTracking(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresObjectEnumeration(TypeDeclaration typeDeclaration) =>
        typeDeclaration.TryGetKeyword(this, out JsonElement value) && value.ValueKind == JsonValueKind.Array;

    /// <inheritdoc/>
    public string GetPathModifier(PropertyDeclaration property)
    {
        if (property.Owner.TryGetKeyword(this, out JsonElement element)
            && element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (item.ValueEquals(property.JsonPropertyName))
                {
                    break;
                }

                index++;
            }

            return KeywordPathReference.AppendArrayIndexToFragment(index);
        }

        return KeywordPath;
    }
}