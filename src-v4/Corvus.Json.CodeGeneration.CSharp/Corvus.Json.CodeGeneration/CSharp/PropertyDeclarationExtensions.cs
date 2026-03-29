// <copyright file="PropertyDeclarationExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration.CSharp;

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

    private static bool HasMatchingProperty(PropertyDeclaration property, ReadOnlySpan<char> buffer, [NotNullWhen(true)] out string? match)
    {
        foreach (PropertyDeclaration sibling in property.Owner.PropertyDeclarations)
        {
            if (property == sibling)
            {
                continue;
            }

            if (sibling.TryGetMetadata(nameof(DotnetPropertyName), out string? siblingName))
            {
                if (siblingName is string sn && sn.AsSpan().Equals(buffer, StringComparison.Ordinal))
                {
                    match = siblingName;
                    return true;
                }
            }
        }

        match = null;
        return false;
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

        if (writtenBuffer.Equals(that.Owner.DotnetTypeName().AsSpan(), StringComparison.Ordinal) || OwnerHasMatchingChild(that.Owner.Children(), writtenBuffer))
        {
            ValueSpan.CopyTo(buffer[written..]);
            written += ValueSpan.Length;
            currentName = buffer[..written];
        }

        int index = 1;

        while (HasMatchingProperty(that, currentName, out string? match))
        {
            int writtenSuffix = Formatting.ApplySuffix(index++, appendBuffer);
            currentName = buffer[..(written + writtenSuffix)];
        }

        name = currentName.ToString();

        that.SetMetadata(nameof(DotnetPropertyName), name);
        return name;
    }

    private static bool OwnerHasMatchingChild(IReadOnlyCollection<TypeDeclaration> typeDeclarations, ReadOnlySpan<char> writtenBuffer)
    {
        foreach (TypeDeclaration typeDeclaration in typeDeclarations)
        {
            TypeDeclaration reducedType = typeDeclaration.ReducedTypeDeclaration().ReducedType;
            if (reducedType.Parent() is null)
            {
                continue;
            }

            if (reducedType.TryGetDotnetTypeName(out string? dotnetTypeName) &&
                writtenBuffer.Equals(dotnetTypeName.AsSpan(), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}