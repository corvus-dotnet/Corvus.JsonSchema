// <copyright file="ConstPropertyNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A name heuristic based on const properties.
/// </summary>
public sealed class ConstPropertyNameHeuristic : INameHeuristicBeforeSubschema
{
    private ConstPropertyNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="ConstPropertyNameHeuristic"/>.
    /// </summary>
    public static ConstPropertyNameHeuristic Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => true;

    /// <inheritdoc/>
    public uint Priority => 1600;

    private static ReadOnlySpan<char> ConstPropertyPrefix => "With".AsSpan();

    private static ReadOnlySpan<char> ConstPropertySeparator => "And".AsSpan();

    /// <inheritdoc/>
    public bool TryGetName(TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        int count = 0;
        written = 0;
        foreach (PropertyDeclaration? property in
                    typeDeclaration.PropertyDeclarations
                        .Where(p =>
                            p.LocalOrComposed == LocalOrComposed.Local &&
                            p.PropertyType.ReducedType.SingleConstantValue().ValueKind != System.Text.Json.JsonValueKind.Undefined))
        {
            if (count > 3)
            {
                // We don't do it for more than 3 properties.
                return false;
            }

            count++;

            if (written == 0)
            {
                ConstPropertyPrefix.CopyTo(typeNameBuffer);
                written = ConstPropertyPrefix.Length;
            }
            else
            {
                ConstPropertySeparator.CopyTo(typeNameBuffer[written..]);
                written += ConstPropertySeparator.Length;
            }

            written += Formatting.FormatTypeNameComponent(typeDeclaration, property.JsonPropertyName.AsSpan(), typeNameBuffer[written..]);

            JsonElement constValue = property.PropertyType.ReducedType.SingleConstantValue();

            ReadOnlySpan<char> constSpan =
                constValue.ValueKind == JsonValueKind.String
                    ? constValue.GetString().AsSpan()
                    : constValue.GetRawText().AsSpan();

            written += Formatting.FormatTypeNameComponent(typeDeclaration, constSpan, typeNameBuffer[written..]);
        }

        return written > 0;
    }
}