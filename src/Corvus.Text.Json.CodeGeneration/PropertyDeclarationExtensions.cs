// <copyright file="PropertyDeclarationExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Extension methods for property declaration.
/// </summary>
public static class PropertyDeclarationExtensions
{
    private static ReadOnlySpan<char> ValueSpan => "Value".AsSpan();

    /// <summary>
    /// Gets the .NET property name for the property.
    /// </summary>
    /// <param name="that">The <see cref="PropertyDeclaration"/> for which to get the
    /// .NET property name.</param>
    /// <returns>the formatted .NET property name.</returns>
    public static string DotnetPropertyName(this PropertyDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(DotnetPropertyName), out string? name) || name is null)
        {
            name = BuildDotnetPropertyName(that);
        }

        return name ?? throw new InvalidOperationException("Null names are not permitted.");
    }

    private static string BuildDotnetPropertyName(PropertyDeclaration that)
    {
        string? name;
        Span<char> buffer = stackalloc char[Formatting.MaxIdentifierLength];
        that.JsonPropertyName.AsSpan().CopyTo(buffer);
        int written = Formatting.FormatPropertyNameComponent(buffer, that.JsonPropertyName.Length);

        Span<char> appendBuffer = buffer[written..];
        ReadOnlySpan<char> currentName = buffer[..written];

        ReadOnlySpan<char> writtenBuffer = currentName;

        if (writtenBuffer.Equals(that.Owner.DotnetTypeName().AsSpan(), StringComparison.Ordinal)
            || OwnerHasMatchingChild(that.Owner.Children(), writtenBuffer, that.Owner.DotnetNamespace()))
        {
            ValueSpan.CopyTo(buffer[written..]);
            written += ValueSpan.Length;
            currentName = buffer[..written];
        }

        // Build a HashSet of already-assigned sibling names once for O(1) collision checks
        HashSet<string> assignedSiblingNames = BuildAssignedSiblingNames(that);
        int index = 1;

        while (assignedSiblingNames.Contains(currentName.ToString()))
        {
            int writtenSuffix = Formatting.ApplySuffix(index++, appendBuffer);
            currentName = buffer[..(written + writtenSuffix)];
        }

        name = currentName.ToString();

        that.SetMetadata(nameof(DotnetPropertyName), name);
        return name;
    }

    private static HashSet<string> BuildAssignedSiblingNames(PropertyDeclaration property)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (PropertyDeclaration sibling in property.Owner.PropertyDeclarations)
        {
            if (property == sibling)
            {
                continue;
            }

            if (sibling.TryGetMetadata(nameof(DotnetPropertyName), out string? siblingName) && siblingName is not null)
            {
                names.Add(siblingName);
            }
        }

        return names;
    }

    private static bool OwnerHasMatchingChild(IReadOnlyCollection<TypeDeclaration> typeDeclarations, ReadOnlySpan<char> writtenBuffer, string currentNamespace)
    {
        foreach (TypeDeclaration typeDeclaration in typeDeclarations)
        {
            TypeDeclaration reducedType = typeDeclaration.ReducedTypeDeclaration().ReducedType;
            if (reducedType.Parent() is null)
            {
                continue;
            }

            if (reducedType.TryGetDotnetTypeName(out string? dotnetTypeName) &&
                writtenBuffer.Equals(dotnetTypeName.AsSpan(), StringComparison.Ordinal) &&
                reducedType.DotnetNamespace() == currentNamespace)
            {
                return true;
            }
        }

        return false;
    }
}