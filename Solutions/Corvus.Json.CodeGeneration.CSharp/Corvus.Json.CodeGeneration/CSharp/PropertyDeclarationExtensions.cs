// <copyright file="PropertyDeclarationExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extension methods for property declaration.
/// </summary>
internal static class PropertyDeclarationExtensions
{
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
            written = Formatting.FixReservedWords(buffer, written, "V".AsSpan());
            name = buffer[..written].ToString();
            that.SetMetadata(nameof(DotnetPropertyName), name);
        }

        return name ?? throw new InvalidOperationException("Null names are not permitted.");
    }
}