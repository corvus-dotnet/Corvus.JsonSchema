// <copyright file="V4TypeHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Microsoft.CodeAnalysis;

namespace Corvus.Text.Json.Migration.Analyzers;

/// <summary>
/// Helper methods for resolving V4 Corvus.Json types in a compilation.
/// </summary>
internal static class V4TypeHelper
{
    /// <summary>
    /// Gets the <c>Corvus.Json.IJsonValue</c> interface from the compilation, or <see langword="null"/>
    /// if V4 types are not referenced.
    /// </summary>
    /// <param name="compilation">The compilation to search.</param>
    /// <returns>The <see cref="INamedTypeSymbol"/> for <c>IJsonValue</c>, or <see langword="null"/>.</returns>
    public static INamedTypeSymbol? GetIJsonValueInterface(Compilation compilation)
    {
        return compilation.GetTypeByMetadataName("Corvus.Json.IJsonValue");
    }

    /// <summary>
    /// Gets the <c>Corvus.Json.IJsonValue`1</c> generic interface from the compilation, or <see langword="null"/>
    /// if V4 types are not referenced.
    /// </summary>
    /// <param name="compilation">The compilation to search.</param>
    /// <returns>The <see cref="INamedTypeSymbol"/> for <c>IJsonValue&lt;T&gt;</c>, or <see langword="null"/>.</returns>
    public static INamedTypeSymbol? GetGenericIJsonValueInterface(Compilation compilation)
    {
        return compilation.GetTypeByMetadataName("Corvus.Json.IJsonValue`1");
    }

    /// <summary>
    /// Determines whether the given type symbol implements the V4 <c>Corvus.Json.IJsonValue</c> interface.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <param name="compilation">The compilation containing type information.</param>
    /// <returns><see langword="true"/> if the type implements <c>IJsonValue</c>; otherwise <see langword="false"/>.</returns>
    public static bool ImplementsIJsonValue(ITypeSymbol? type, Compilation compilation)
    {
        if (type is null)
        {
            return false;
        }

        INamedTypeSymbol? ijsonValue = GetIJsonValueInterface(compilation);
        if (ijsonValue is null)
        {
            return false;
        }

        // Check direct implementation
        if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, ijsonValue))
        {
            return true;
        }

        // Check all interfaces
        foreach (INamedTypeSymbol iface in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, ijsonValue) ||
                SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, GetGenericIJsonValueInterface(compilation)))
            {
                return true;
            }
        }

        // Check if the type is constrained to IJsonValue (for generic type parameters)
        if (type is ITypeParameterSymbol typeParam)
        {
            foreach (ITypeSymbol constraint in typeParam.ConstraintTypes)
            {
                if (ImplementsIJsonValue(constraint, compilation))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether the given type symbol implements <c>Corvus.Json.IJsonValue</c>,
    /// or is an unresolved (error) type that could not be bound — which happens when the
    /// user has already changed the <c>using Corvus.Json;</c> directive to <c>Corvus.Text.Json</c>
    /// but hasn't yet renamed the types.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <param name="compilation">The compilation containing type information.</param>
    /// <returns><see langword="true"/> if the type implements <c>IJsonValue</c> or is unresolved.</returns>
    public static bool ImplementsIJsonValueOrUnresolved(ITypeSymbol? type, Compilation compilation)
    {
        if (type is null)
        {
            return false;
        }

        if (type is IErrorTypeSymbol)
        {
            return true;
        }

        return ImplementsIJsonValue(type, compilation);
    }

    /// <summary>
    /// Determines whether the given type symbol is <c>System.Text.Json.Utf8JsonWriter</c>.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <param name="compilation">The compilation containing type information.</param>
    /// <returns><see langword="true"/> if the type is <c>System.Text.Json.Utf8JsonWriter</c>.</returns>
    public static bool IsSystemTextJsonUtf8JsonWriter(ITypeSymbol? type, Compilation compilation)
    {
        if (type is null)
        {
            return false;
        }

        INamedTypeSymbol? utf8JsonWriter = compilation.GetTypeByMetadataName("System.Text.Json.Utf8JsonWriter");
        if (utf8JsonWriter is null)
        {
            return false;
        }

        return SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, utf8JsonWriter.OriginalDefinition);
    }
}