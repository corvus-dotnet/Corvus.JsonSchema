// <copyright file="ConstPropertyNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Linq;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

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
    public bool TryGetName(ILanguageProvider languageProvider, TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        if (typeDeclaration.Parent() is null || typeDeclaration.IsInDefinitionsContainer())
        {
            written = 0;
            return false;
        }

        int count = 0;
        written = 0;
        foreach (PropertyDeclaration? property in
                    typeDeclaration.PropertyDeclarations
                        .Where(p =>
                            p.LocalOrComposed == LocalOrComposed.Local &&
                            p.ReducedPropertyType.SingleConstantValue().ValueKind != System.Text.Json.JsonValueKind.Undefined))
        {
            if (count > 3)
            {
                // We don't do it for more than 3 properties.
                written = 0;
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

            JsonElement constValue = property.ReducedPropertyType.SingleConstantValue();

            ReadOnlySpan<char> constSpan =
                constValue.ValueKind == JsonValueKind.String
                    ? constValue.GetString().AsSpan()
                    : constValue.GetRawText().AsSpan();

            written += Formatting.FormatTypeNameComponent(typeDeclaration, constSpan, typeNameBuffer[written..]);
        }

        if (written > 0 && typeDeclaration.CollidesWithParent(typeNameBuffer[..written]))
        {
            written = 0;
        }

        return written > 0;
    }
}