// <copyright file="PropertyDeclarationExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extension methods for property declaration.
/// </summary>
internal static class PropertyDeclarationExtensions
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
        if (!that.TryGetMetadata(nameof(DotnetPropertyName), out string? name))
        {
            Span<char> buffer = stackalloc char[Formatting.MaxIdentifierLength];
            that.JsonPropertyName.AsSpan().CopyTo(buffer);
            int written = Formatting.ToPascalCase(buffer[..that.JsonPropertyName.Length]);
            written = Formatting.FixReservedWords(buffer, written, "V".AsSpan(), "Property".AsSpan());

            if (that.Owner.DotnetTypeName() == buffer[..written].ToString())
            {
                ValueSpan.CopyTo(buffer[written..]);
                written += ValueSpan.Length;
            }

            Span<char> appendBuffer = buffer[written..];
            ReadOnlySpan<char> currentName = buffer[..written];

            int index = 1;

            while (HasMatchingProperty(that, currentName, out string? match))
            {
                int writtenSuffix = Formatting.ApplySuffix(index++, appendBuffer);
                currentName = buffer[..(written + writtenSuffix)];
            }

            name = currentName.ToString();

            that.SetMetadata(nameof(DotnetPropertyName), name);
        }

        return name ?? throw new InvalidOperationException("Null names are not permitted.");

        static bool HasMatchingProperty(PropertyDeclaration property, ReadOnlySpan<char> buffer, [NotNullWhen(true)] out string? match)
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
    }
}