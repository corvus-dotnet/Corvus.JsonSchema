﻿// <copyright file="DefaultValueNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A name heuristic based on a default value.
/// </summary>
public sealed class DefaultValueNameHeuristic : INameHeuristicBeforeSubschema
{
    private DefaultValueNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="DefaultValueNameHeuristic"/>.
    /// </summary>
    public static DefaultValueNameHeuristic Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => true;

    /// <inheritdoc/>
    public uint Priority => 1600;

    private static ReadOnlySpan<char> DefaultValuePrefix => "DefaultValue".AsSpan();

    /// <inheritdoc/>
    public bool TryGetName(TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        JsonElement defaultValue = typeDeclaration.DefaultValue();
        if (defaultValue.ValueKind != JsonValueKind.Undefined &&
            typeDeclaration.LocatedSchema.Schema.EnumerateObject().Count() == 1)
        {
            DefaultValuePrefix.CopyTo(typeNameBuffer);

            ReadOnlySpan<char> dvSpan =
                defaultValue.ValueKind == JsonValueKind.String
                    ? defaultValue.GetString().AsSpan()
                    : defaultValue.GetRawText().AsSpan();

            written = DefaultValuePrefix.Length;
            written += Formatting.FormatTypeNameComponent(typeDeclaration, dvSpan, typeNameBuffer[written..]);
            return true;
        }

        written = 0;
        return false;
    }
}