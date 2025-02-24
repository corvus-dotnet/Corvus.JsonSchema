// <copyright file="TypeDeclarationExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extension methods for <see cref="TypeDeclaration"/>.
/// </summary>
public static class TypeDeclarationExtensions
{
    private const string DotnetNamespaceKey = "CSharp_DotnetNamespace";
    private const string DotnetTypeNameKey = "CSharp_DotnetTypeName";
    private const string ParentKey = "CSharp_LanguageProvider_Parent";
    private const string ChildrenKey = "CSharp_LanguageProvider_Children";
    private const string DoNotGenerateKey = "CSharp_LanguageProvider_DoNotGenerate";
    private const string DotnetTypeNameWithoutNamespaceKey = "CSharp_LanguageProvider_DotnetTypeNameWithoutNamespace";
    private const string FullyQualifiedDotnetTypeNameKey = "CSharp_LanguageProvider_FullyQualifiedDotnetTypeName";
    private const string PreferredDotnetNumericTypeNameKey = "CSharp_LanguageProvider_PreferredDotnetNumericTypeName";
    private const string AlwaysAssertFormatKey = "CSharp_LanguageProvider_AlwaysAssertFormat";
    private const string OptionalAsNullableKey = "CSharp_LanguageProvider_OptionalAsNullable";
    private const string PreferredBinaryJsonNumberKindKey = "CSharp_LanguageProvider_PreferredBinaryJsonNumberKind";
    private const string UseImplicitOperatorStringKey = "CSharp_LanguageProvider_UseImplicitOperatorString";
    private const string AddExplicitUsingsKey = "CSharp_LanguageProvider_AddExplicitUsings";
    private const string DefaultAccessibilityKey = "CSharp_LanguageProvider_DefaultAccessibilityKey";
    private const string AccessibilityKey = "CSharp_LanguageProvider_AccessibilityKey";

    /// <summary>
    /// Sets the relevant metadata from the <see cref="CSharpLanguageProvider.Options"/>.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration on which to set the options.</param>
    /// <param name="options">The <see cref="CSharpLanguageProvider.Options"/> to set.</param>
    public static void SetCSharpOptions(this TypeDeclaration typeDeclaration, CSharpLanguageProvider.Options options)
    {
        typeDeclaration.SetMetadata(AlwaysAssertFormatKey, options.AlwaysAssertFormat);
        typeDeclaration.SetMetadata(OptionalAsNullableKey, options.OptionalAsNullable);
        typeDeclaration.SetMetadata(UseImplicitOperatorStringKey, options.UseImplicitOperatorString);
        typeDeclaration.SetMetadata(AddExplicitUsingsKey, options.AddExplicitUsings);
        typeDeclaration.SetMetadata(DefaultAccessibilityKey, options.DefaultAccessibility);
    }

    /// <summary>
    /// Gets a value indicating whether this is a Corvus extended JSON type.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type is a Corvus extended JSON type.</returns>
    public static bool IsCorvusJsonExtendedType(this TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.DotnetNamespace() == "Corvus.Json" && typeDeclaration.DotnetTypeName().StartsWith("Json");
    }

    /// <summary>
    /// Try to get the Corvus extended type name for the given type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to get the corvus extended type name.</param>
    /// <param name="extendedTypeName">The corvus extended type name, or <see langword="null"/> if this was not a corvus extended type.</param>
    /// <returns><see langword="true"/> if this type declaration represents a coruvs extended type.</returns>
    public static bool TryGetCorvusJsonExtendedTypeName(this TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string? extendedTypeName)
    {
        if (typeDeclaration.DotnetNamespace() == "Corvus.Json")
        {
            if (typeDeclaration.DotnetTypeName().StartsWith("Json"))
            {
                extendedTypeName = typeDeclaration.DotnetTypeName();
                return true;
            }
        }

        extendedTypeName = null;
        return false;
    }

    /// <summary>
    /// Gets a value indicating whether this type prefers 128bit integers.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type prefers 128 bit integers.</returns>
    public static string PreferredBinaryJsonNumberKind(this TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(PreferredBinaryJsonNumberKindKey, out string? numericType))
        {
            numericType = GetPreferredBinaryJsonNumberKind(typeDeclaration);
            typeDeclaration.SetMetadata(PreferredBinaryJsonNumberKindKey, numericType);
        }

        return numericType ?? "Double";

        static string GetPreferredBinaryJsonNumberKind(TypeDeclaration typeDeclaration)
        {
            string? candidateName = typeDeclaration.PreferredDotnetNumericTypeName();

            return candidateName switch
            {
                "double" => "Double",
                "decimal" => "Decimal",
                "Half" => "Half",
                "float" => "Single",
                "byte" => "Byte",
                "short" => "Int16",
                "int" => "Int32",
                "long" => "Int64",
                "Int128" => "Int128",
                "sbyte" => "SByte",
                "ushort" => "UInt16",
                "uint" => "UInt32",
                "ulong" => "UInt64",
                "UInt128" => "UInt128",
                _ => "Double",
            };
        }
    }

    /// <summary>
    /// Gets a value indicating whether to always assert format, regardless of
    /// the vocabulary.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if format is always to be asserted.</returns>
    public static bool AlwaysAssertFormat(this TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetMetadata(AlwaysAssertFormatKey, out bool? alwaysAssertFormat) &&
            alwaysAssertFormat is bool value)
        {
            return value;
        }

        return false;
    }

    /// <summary>
    /// Determines if there is a child of the parent type declaration whose name matches the proposed name
    /// for a given child type declaration.
    /// </summary>
    /// <param name="parent">The parent type declaration.</param>
    /// <param name="child">The child corresponding to the proposed name.</param>
    /// <param name="span">The proposed name.</param>
    /// <returns>The type declaration whose name collides with the proposed name, or <see langword="null"/> if
    /// there is no collision.</returns>
    public static TypeDeclaration? FindChildNameCollision(this TypeDeclaration parent, TypeDeclaration child, ReadOnlySpan<char> span)
    {
        foreach (TypeDeclaration childToTest in parent.Children())
        {
            TypeDeclaration reducedChild = childToTest.ReducedTypeDeclaration().ReducedType;
            if (reducedChild != child && reducedChild.TryGetDotnetTypeName(out string? typeName) &&
                typeName.AsSpan().Equals(span, StringComparison.Ordinal))
            {
                return reducedChild;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets a value indicating whether to generate optional properties as nullable.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if optional properties should be generated as nullable types.</returns>
    public static bool OptionalAsNullable(this TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetMetadata(OptionalAsNullableKey, out bool? optionalAsNullable) &&
            optionalAsNullable is bool value)
        {
            return value;
        }

        return false;
    }

    /// <summary>
    /// Gets a value indicating whether to generate an implicit operator for conversion to <see langword="string"/>.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the conversion operator to <see langword="string"/> should be implicit.</returns>
    public static bool UseImplicitOperatorString(this TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetMetadata(UseImplicitOperatorStringKey, out bool? useImplicitOperatorString) &&
            useImplicitOperatorString is bool value)
        {
            return value;
        }

        return false;
    }

    /// <summary>
    /// Gets a value indicating whether to generate using statements for the standard implicit usings.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the using statements should be added.</returns>
    public static bool AddExplicitUsings(this TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetMetadata(AddExplicitUsingsKey, out bool? addExplicitUsings) &&
            addExplicitUsings is bool value)
        {
            return value;
        }

        return false;
    }

    /// <summary>
    /// Determines if the given name collides with the parent name.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <param name="name">The name to test.</param>
    /// <returns><see langword="true"/> if the names collide.</returns>
    public static bool CollidesWithParent(this TypeDeclaration typeDeclaration, ReadOnlySpan<char> name)
    {
        return
            typeDeclaration.Parent() is TypeDeclaration parent &&
            parent.TryGetDotnetTypeName(out string? parentName) &&
            name.Equals(parentName.AsSpan(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines if the given name collides with another child in the parent.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <param name="name">The name to test.</param>
    /// <returns><see langword="true"/> if the names collide.</returns>
    public static bool MatchesExistingTypeInParent(this TypeDeclaration typeDeclaration, ReadOnlySpan<char> name)
    {
        TypeDeclaration? parent = typeDeclaration.Parent();

        if (parent is null)
        {
            return false;
        }

        foreach (TypeDeclaration child in parent.Children())
        {
            if (child.TryGetDotnetTypeName(out string? childName) &&
                 name.Equals(childName.AsSpan(), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets a value which determines if this type is the built-in JsonAny type
    /// type.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type declaration is the <see cref="WellKnownTypeDeclarations.JsonAny"/> type.</returns>
    /// <remarks>This uses the <see cref="WellKnownTypeDeclarations.JsonAny"/> schema location IRI to identify the type. Contrast
    /// with <see cref="IsCorvusJsonExtendedJsonAny"/> which uses the type name to distinguish the type.</remarks>
    public static bool IsBuiltInJsonAnyType(this TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.LocatedSchema.Location == WellKnownTypeDeclarations.JsonAny.LocatedSchema.Location;
    }

    /// <summary>
    /// Gets a value which determines if this type is the built-in JsonAny type
    /// type.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type declaration is the <see cref="WellKnownTypeDeclarations.JsonAny"/> type.</returns>
    /// <remarks>This uses the <see cref="WellKnownTypeDeclarations.JsonAny"/> schema location IRI to identify the type. Contrast
    /// with <see cref="IsBuiltInJsonAnyType"/> which uses the type name to distinguish the type.</remarks>
    public static bool IsCorvusJsonExtendedJsonAny(this TypeDeclaration typeDeclaration)
    {
        return
            typeDeclaration.DotnetNamespace() == "Corvus.Json" &&
            typeDeclaration.DotnetTypeName() == "JsonAny" &&
            typeDeclaration.LocatedSchema.Location != WellKnownTypeDeclarations.JsonAny.LocatedSchema.Location;
    }

    /// <summary>
    /// Gets a value which determines if this type is the built-in JsonAny type
    /// type.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type declaration is the <see cref="WellKnownTypeDeclarations.JsonAny"/> type.</returns>
    /// <remarks>This uses the <see cref="WellKnownTypeDeclarations.JsonAny"/> schema location IRI to identify the type. Contrast
    /// with <see cref="IsBuiltInJsonAnyType"/> which uses the type name to distinguish the type.</remarks>
    public static bool IsCorvusJsonExtendedJsonNotAny(this TypeDeclaration typeDeclaration)
    {
        return
            typeDeclaration.DotnetNamespace() == "Corvus.Json" &&
            typeDeclaration.DotnetTypeName() == "JsonNotAny" &&
            typeDeclaration.LocatedSchema.Location != WellKnownTypeDeclarations.JsonNotAny.LocatedSchema.Location;
    }

    /// <summary>
    /// Determines if the given name collides with a property name in the parent.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <param name="name">The name to test.</param>
    /// <returns><see langword="true"/> if the names collide.</returns>
    public static bool MatchesExistingPropertyNameInParent(this TypeDeclaration typeDeclaration, ReadOnlySpan<char> name)
    {
        TypeDeclaration? parent = typeDeclaration.Parent();

        if (parent is null)
        {
            return false;
        }

        foreach (PropertyDeclaration propertyDeclaration in parent.PropertyDeclarations)
        {
            if (propertyDeclaration.DotnetPropertyName().AsSpan().Equals(name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the preferred .NET numeric type (e.g. int, double, long etc) for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>The preferred .NET numeric type name for the type declaration, or <see langword="null"/> if
    /// this was not a numeric type.</returns>
    public static string? PreferredDotnetNumericTypeName(this TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(PreferredDotnetNumericTypeNameKey, out string? numericTypeName))
        {
            numericTypeName = GetNumericTypeName(typeDeclaration);
            typeDeclaration.SetMetadata(PreferredDotnetNumericTypeNameKey, numericTypeName);
        }

        return numericTypeName;

        static string? GetNumericTypeName(TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration arrayItemsType)
            {
                return arrayItemsType.ReducedType.PreferredDotnetNumericTypeName();
            }

            if ((typeDeclaration.ImpliedCoreTypesOrAny() & (CoreTypes.Number | CoreTypes.Integer)) != 0)
            {
                string? candidateFormat = typeDeclaration.Format();

                return candidateFormat switch
                {
                    "double" => "double",
                    "decimal" => "decimal",
                    "half" => "Half",
                    "single" => "float",
                    "byte" => "byte",
                    "int16" => "short",
                    "int32" => "int",
                    "int64" => "long",
                    "int128" => "Int128",
                    "sbyte" => "sbyte",
                    "uint16" => "ushort",
                    "uint32" => "uint",
                    "uint64" => "ulong",
                    "uint128" => "UInt128",
                    _ => (typeDeclaration.ImpliedCoreTypes() & CoreTypes.Integer) != 0 ? "long" : "double",
                };
            }

            return null;
        }
    }

    /// <summary>
    /// Gets the preferred .NET string format type (e.g. JsonUuid, JsonIri etc) for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>The preferred .NET numeric type name for the type declaration, or <see langword="null"/> if
    /// this was not a numeric type.</returns>
    public static string? PreferredDotnetStringFormatTypeName(this TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(nameof(PreferredDotnetStringFormatTypeName), out string? numericTypeName))
        {
            numericTypeName = GetStringTypeName(typeDeclaration);
            typeDeclaration.SetMetadata(nameof(PreferredDotnetStringFormatTypeName), numericTypeName);
        }

        return numericTypeName;

        static string? GetStringTypeName(TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration arrayItemsType)
            {
                return arrayItemsType.ReducedType.PreferredDotnetNumericTypeName();
            }

            if ((typeDeclaration.ImpliedCoreTypes() & CoreTypes.String) != 0 && typeDeclaration.Format() is string candidateFormat)
            {
                return FormatHandlerRegistry.Instance.StringFormatHandlers.GetCorvusJsonTypeNameFor(candidateFormat);
            }

            return null;
        }
    }

    /// <summary>
    /// Gets the fully qualified .NET type name.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>The fully qualified .NET type name.</returns>
    public static string FullyQualifiedDotnetTypeName(this TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(FullyQualifiedDotnetTypeNameKey, out string? fqdntn))
        {
            TypeDeclaration? parent = typeDeclaration.Parent();
            fqdntn = parent is null
                ? $"{typeDeclaration.DotnetNamespace()}.{typeDeclaration.DotnetTypeName()}"
                : $"{parent.FullyQualifiedDotnetTypeName()}.{typeDeclaration.DotnetTypeName()}";

            typeDeclaration.SetMetadata(FullyQualifiedDotnetTypeNameKey, fqdntn);
        }

        return fqdntn ?? throw new InvalidOperationException("The .NET type name metadata is not available.");
    }

    /// <summary>
    /// Gets the .NET type name fully qualified, but without the namespace.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>The fully qualified .NET type name without the namespace.</returns>
    public static string DotnetTypeNameWithoutNamespace(this TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(DotnetTypeNameWithoutNamespaceKey, out string? fqdntn))
        {
            TypeDeclaration? parent = typeDeclaration.Parent();
            fqdntn = parent is null
                ? typeDeclaration.DotnetTypeName()
                : $"{parent.DotnetTypeNameWithoutNamespace()}.{typeDeclaration.DotnetTypeName()}";

            typeDeclaration.SetMetadata(DotnetTypeNameWithoutNamespaceKey, fqdntn);
        }

        return fqdntn ?? throw new InvalidOperationException("The .NET type name metadata is not available.");
    }

    /// <summary>
    /// Gets the .NET namespace.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>The .NET namespace.</returns>
    public static string DotnetNamespace(this TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetMetadata(DotnetNamespaceKey, out string? ns) && ns is not null)
        {
            return ns;
        }

        throw new InvalidOperationException("The dotnet namespace metadata is not available.");
    }

    /// <summary>
    /// Gets the .NET namespace.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>The .NET namespace.</returns>
    public static TypeDeclaration? Parent(this TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetMetadata(ParentKey, out TypeDeclaration? parent) && parent is not null)
        {
            return parent;
        }

        return null;
    }

    /// <summary>
    /// Sets the parent type declaration for a child.
    /// </summary>
    /// <param name="child">The child.</param>
    /// <param name="parent">The parent.</param>
    public static void SetParent(this TypeDeclaration child, TypeDeclaration? parent)
    {
        if (parent is TypeDeclaration p)
        {
            if (!p.TryGetMetadata(ChildrenKey, out HashSet<TypeDeclaration>? children))
            {
                children = [];
                p.SetMetadata(ChildrenKey, children);
            }

            children!.Add(child);
        }
        else
        {
            TypeDeclaration? currentParent = child.Parent();

            // Remove the child from the current parent
            if (currentParent is not null)
            {
                if (currentParent.TryGetMetadata(ChildrenKey, out HashSet<TypeDeclaration>? children) &&
                    children is not null)
                {
                    children.Remove(child);
                }
            }
        }

        child.SetMetadata(ParentKey, parent);
    }

    /// <summary>
    /// Gets the children of a type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to get the children.</param>
    /// <returns>The children of the type declaration.</returns>
    /// <remarks>
    /// Note that the children are the raw type declarations, not the fully reduced type declarations.
    /// </remarks>
    public static IReadOnlyCollection<TypeDeclaration> Children(this TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetMetadata(ChildrenKey, out HashSet<TypeDeclaration>? children) &&
            children is not null)
        {
            return children;
        }

        return [];
    }

    /// <summary>
    /// Gets a value indicating that this type declaration should not
    /// be generated.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type should not be generated.</returns>
    public static bool DoNotGenerate(this TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetMetadata(DoNotGenerateKey, out bool? doNotGenerate) &&
            doNotGenerate is bool value)
        {
            return value;
        }

        // If we have not set do not generate at all, we should be generated.
        return false;
    }

    /// <summary>
    /// Sets a value indicating that this type declaration should not
    /// be generated.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <param name="resetParent">If true, also reset the parent to null.</param>
    /// <returns>A reference to the type declaration after the operation has completed.</returns>
    public static TypeDeclaration SetDoNotGenerate(this TypeDeclaration typeDeclaration, bool resetParent = true)
    {
        typeDeclaration.SetMetadata(DoNotGenerateKey, true);

        if (resetParent)
        {
            typeDeclaration.SetParent(null);
        }

        return typeDeclaration;
    }

    /// <summary>
    /// Gets the .NET type name.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>The .NET type name.</returns>
    public static string DotnetTypeName(this TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetMetadata(DotnetTypeNameKey, out string? name) && name is not null)
        {
            return name;
        }

        throw new InvalidOperationException("The .NET type name metadata is not available.");
    }

    /// <summary>
    /// Gets a value indicating whether the .NET type name has been set for the type declaration..
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns><see langword="true"/> if the type name has been set.</returns>
    public static bool HasDotnetTypeName(this TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.TryGetMetadata(DotnetTypeNameKey, out string? name) && name is not null;
    }

    /// <summary>
    /// Tries to gets the .NET type name for the type declaration..
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="name">The .NET type name.</param>
    /// <returns><see langword="true"/> if the type name has been set.</returns>
    public static bool TryGetDotnetTypeName(this TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string? name)
    {
        return typeDeclaration.TryGetMetadata(DotnetTypeNameKey, out name) && name is not null;
    }

    /// <summary>
    /// Sets the .NET namespace.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="ns">The namespace.</param>
    /// <returns>A reference to the type declaration after the operation has completed.</returns>
    public static TypeDeclaration SetDotnetNamespace(this TypeDeclaration typeDeclaration, string ns)
    {
        typeDeclaration.SetMetadata(DotnetNamespaceKey, ns);
        return typeDeclaration;
    }

    /// <summary>
    /// Sets the .NET accessibility.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="accessibility">The <see cref="GeneratedTypeAccessibility"/>.</param>
    /// <returns>A reference to the type declaration after the operation has completed.</returns>
    public static TypeDeclaration SetDotnetAccessibility(this TypeDeclaration typeDeclaration, GeneratedTypeAccessibility accessibility)
    {
        typeDeclaration.SetMetadata(AccessibilityKey, accessibility);
        return typeDeclaration;
    }

    /// <summary>
    /// Gets the .NET accessibility.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>
    /// The <see cref="GeneratedTypeAccessibility"/> for the type. If this has been set explicitly, it will return the accessibility for this type.
    /// If it has a <c>Parent</c>, it will be <see cref="GeneratedTypeAccessibility.Public"/>. Otherwise, it will fall back to the default accessibility
    /// for the code generation context (which defaults to <see cref="GeneratedTypeAccessibility.Public"/>).
    /// </returns>
    public static GeneratedTypeAccessibility DotnetAccessibility(this TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetMetadata(AccessibilityKey, out GeneratedTypeAccessibility? typeAccessibility) &&
            typeAccessibility is GeneratedTypeAccessibility value)
        {
            return value;
        }

        if (typeDeclaration.Parent() is not null)
        {
            return GeneratedTypeAccessibility.Public;
        }

        if (typeDeclaration.TryGetMetadata(DefaultAccessibilityKey, out GeneratedTypeAccessibility? defaultTypeAccessibility) &&
            defaultTypeAccessibility is GeneratedTypeAccessibility defaultValue)
        {
            return defaultValue;
        }

        return GeneratedTypeAccessibility.Public;
    }

    /// <summary>
    /// Sets the .NET type name.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="typeName">The type name.</param>
    /// <returns>A reference to the type declaration after the operation has completed.</returns>
    public static TypeDeclaration SetDotnetTypeName(this TypeDeclaration typeDeclaration, string typeName)
    {
        typeDeclaration.SetMetadata(DotnetTypeNameKey, typeName);
        return typeDeclaration;
    }
}