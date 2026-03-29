// <copyright file="CodeGeneratorExtensions.MemberNames.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Code generator extensions for member name resolution and scoping.
/// </summary>
internal static partial class CodeGeneratorExtensions
{
    /// <summary>
    /// Gets the name for a field.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetFieldNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return string.Empty;
        }

        return generator.GetOrAddMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.CamelCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a method.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetMethodNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return string.Empty;
        }

        return generator.GetOrAddMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a parameter.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetParameterNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return string.Empty;
        }

        return generator.GetOrAddMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.CamelCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a property.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetPropertyNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return string.Empty;
        }

        return generator.GetOrAddMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a static readonly field.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetStaticReadOnlyFieldNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return string.Empty;
        }

        var memberName = new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix);

        return generator.GetOrAddMemberName(memberName);
    }

    /// <summary>
    /// Gets the name for a static readonly property.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetStaticReadOnlyPropertyNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return string.Empty;
        }

        return generator.GetOrAddMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a type.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetTypeNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return string.Empty;
        }

        return generator.GetOrAddMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a class.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetUniqueClassNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return string.Empty;
        }

        return generator.GetUniqueMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets unique name for a field.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetUniqueFieldNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return string.Empty;
        }

        return generator.GetUniqueMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.CamelCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a method.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetUniqueMethodNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return string.Empty;
        }

        return generator.GetUniqueMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the unique name for a parameter.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetUniqueParameterNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return string.Empty;
        }

        return generator.GetUniqueMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.CamelCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a property.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetUniquePropertyNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return string.Empty;
        }

        return generator.GetUniqueMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets a unique name for a static readonly field.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetUniqueStaticReadOnlyFieldNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return string.Empty;
        }

        return generator.GetUniqueMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets a unique name for a static readonly property.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetUniqueStaticReadOnlyPropertyNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return string.Empty;
        }

        return generator.GetUniqueMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets a unique name for a static method.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetUniqueStaticMethodNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return string.Empty;
        }

        return generator.GetUniqueMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets a unique name for a variable.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetUniqueVariableNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return string.Empty;
        }

        return generator.GetUniqueMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.CamelCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a variable.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetVariableNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return string.Empty;
        }

        return generator.GetOrAddMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.CamelCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Reserves a specific name in a scope.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator ReserveName(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator.ReserveName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.Unmodified,
                prefix,
                suffix));
    }

    /// <summary>
    /// Reserves a specific name in a scope.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator ReserveNameIfNotReserved(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator.ReserveNameIfNotReserved(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.Unmodified,
                prefix,
                suffix));
    }

    /// <summary>
    /// Tries to reserves a specific name in a scope.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static bool TryReserveName(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        if (generator.IsCancellationRequested)
        {
            return false;
        }

        return generator.TryReserveName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.Unmodified,
                prefix,
                suffix));
    }
}