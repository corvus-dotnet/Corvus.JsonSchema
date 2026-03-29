// <copyright file="TypeDeclarationExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.Keywords;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Represents a numeric type name with fallback for .NET Standard.
/// </summary>
public readonly struct NumericTypeName
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NumericTypeName"/> struct.
    /// </summary>
    /// <param name="name">The name of the numeric type.</param>
    /// <param name="isNetOnly">A value indicating whether this is a .NET only type.</param>
    public NumericTypeName(string name, bool isNetOnly, string? netStandardFallbackName)
    {
        Name = name;
        IsNetOnly = isNetOnly;
        NetStandardFallbackName = netStandardFallbackName;
    }

    /// <summary>
    /// Gets a value indicating whether this is a .NET only type.
    /// </summary>
    [MemberNotNullWhen(true, nameof(NetStandardFallbackName))]
    public bool IsNetOnly { get; }

    /// <summary>
    /// Gets the name of the numeric type.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the name of the numeric type for the netstandard fallback.
    /// </summary>
    public string? NetStandardFallbackName { get; }
}

/// <summary>
/// Extension methods for <see cref="TypeDeclaration"/>.
/// </summary>
public static class TypeDeclarationExtensions
{
    private const string AccessibilityKey = "CSharp_LanguageProvider_AccessibilityKey";
    private const string AddExplicitUsingsKey = "CSharp_LanguageProvider_AddExplicitUsings";
    private const string AlwaysAssertFormatKey = "CSharp_LanguageProvider_AlwaysAssertFormat";
    private const string BuilderSourcesKey = "CSharp_LanguageProvider_BuilderSources";
    private const string ChildrenKey = "CSharp_LanguageProvider_Children";
    private const string DefaultAccessibilityKey = "CSharp_LanguageProvider_DefaultAccessibilityKey";
    private const string DoNotGenerateKey = "CSharp_LanguageProvider_DoNotGenerate";
    private const string DotnetNamespaceKey = "CSharp_DotnetNamespace";
    private const string DotnetTypeNameKey = "CSharp_DotnetTypeName";
    private const string DotnetTypeNameWithoutNamespaceKey = "CSharp_LanguageProvider_DotnetTypeNameWithoutNamespace";
    private const string FullyQualifiedDotnetTypeNameKey = "CSharp_LanguageProvider_FullyQualifiedDotnetTypeName";
    private const string OptionalAsNullableKey = "CSharp_LanguageProvider_OptionalAsNullable";
    private const string ParentKey = "CSharp_LanguageProvider_Parent";
    private const string PreferredDotnetNumericTypeNameKey = "CSharp_LanguageProvider_PreferredDotnetNumericTypeName";
    private const string UseImplicitOperatorStringKey = "CSharp_LanguageProvider_UseImplicitOperatorString";
    private const string ExecutedValidationHandlersKey = "CSharp_LanguageProvider_ExecutedValidationHandlers";
    private const string GlobalSimpleTypeKey = "CSharp_LanguageProvider_GlobalSimpleType";
    private const string IgnoredKeywordsKey = "CSharp_LanguageProvider_IgnoredKeywords";

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
    /// Gets a value indicating whether this type declaration is a global simple type
    /// (a shared type generated once for a simple core-type-only schema).
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type is a global simple type.</returns>
    public static bool IsGlobalSimpleType(this TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetMetadata(GlobalSimpleTypeKey, out bool? isGlobal) &&
            isGlobal is bool value)
        {
            return value;
        }

        return false;
    }

    /// <summary>
    /// Sets a value indicating whether this type declaration is a global simple type.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="isGlobal">Whether the type is a global simple type.</param>
    /// <returns>A reference to the type declaration after the operation has completed.</returns>
    public static TypeDeclaration SetIsGlobalSimpleType(this TypeDeclaration typeDeclaration, bool isGlobal)
    {
        typeDeclaration.SetMetadata(GlobalSimpleTypeKey, isGlobal);
        return typeDeclaration;
    }

    /// <summary>
    /// Gets a value indicating whether the type declaration can be reduced to an <c>anyOf</c> match.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns><see langword="true"/> if the type can be reduced.</returns>
    public static bool CanReduceToAnyOf(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot use CanReduceToAnyOf during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(CanReduceToAnyOf), out bool canReduce))
        {
            canReduce = CanReduceTo<IAnyOfValidationKeyword>(that.LocatedSchema);
            that.SetMetadata(nameof(CanReduceToAnyOf), canReduce);
        }

        return canReduce;
    }

    /// <summary>
    /// Gets a value indicating whether the type declaration can be reduced to a <c>oneOf</c> match.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns><see langword="true"/> if the type can be reduced.</returns>
    public static bool CanReduceToOneOf(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot use CanReduceToOneOf during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(CanReduceToOneOf), out bool canReduce))
        {
            canReduce = CanReduceTo<IOneOfValidationKeyword>(that.LocatedSchema);
            that.SetMetadata(nameof(CanReduceToOneOf), canReduce);
        }

        return canReduce;
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
    /// Gets the types for oneOf/anyOf composition sources that can be created for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>The array of types from which this can be converted.</returns>
    public static TypeDeclaration[] CompositionSources(this TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(BuilderSourcesKey, out TypeDeclaration[]? sources))
        {
            sources = GetCompositionSources(typeDeclaration);
            typeDeclaration.SetMetadata(BuilderSourcesKey, sources);
        }

        return sources ?? [];

        static TypeDeclaration[] GetCompositionSources(TypeDeclaration rootDeclaration)
        {
            HashSet<TypeDeclaration> sources = [];

            Queue<TypeDeclaration> typesToProcess = [];

            typesToProcess.Enqueue(rootDeclaration);

            while (typesToProcess.Count > 0)
            {
                TypeDeclaration subschema = typesToProcess.Dequeue();

                if (!sources.Add(subschema) || subschema.DoNotGenerate())
                {
                    // We've already seen it.
                    continue;
                }

                AppendCompositionSources(typesToProcess, rootDeclaration, subschema);
            }

            // We don't want the root declaration in the set
            sources.Remove(rootDeclaration);
            return [.. sources.OrderBy(t => t.FullyQualifiedDotnetTypeName())];
        }
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

    public static string GetIJsonElementInterface(this TypeDeclaration typeDeclaration, bool forMutable)
    {
        return forMutable ? GetIMutableJsonElementInterface(typeDeclaration) : GetIJsonElementInterface(typeDeclaration);
    }

    public static string GetIJsonElementInterface(this TypeDeclaration typeDeclaration)
    {
        return $"IJsonElement<{typeDeclaration.DotnetTypeName()}>";
    }

    public static string GetIMutableJsonElementInterface(this TypeDeclaration typeDeclaration)
    {
        return "IMutableJsonElement<Mutable>";
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
    /// Gets a value which determines if this type is the built-in JsonNotAny type
    /// type.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type declaration is the <see cref="WellKnownTypeDeclarations.JsonAny"/> type.</returns>
    /// <remarks>This uses the <see cref="WellKnownTypeDeclarations.JsonAny"/> schema location IRI to identify the type. Contrast
    /// with <see cref="IsCorvusJsonExtendedJsonAny"/> which uses the type name to distinguish the type.</remarks>
    public static bool IsBuiltInJsonNotAnyType(this TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.LocatedSchema.Location == WellKnownTypeDeclarations.JsonNotAny.LocatedSchema.Location;
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
    /// Gets the preferred .NET numeric type (e.g. int, double, long etc) for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>The preferred .NET numeric type name for the type declaration, or <see langword="null"/> if
    /// this was not a numeric type.</returns>
    public static NumericTypeName? PreferredDotnetNumericTypeName(this TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(PreferredDotnetNumericTypeNameKey, out NumericTypeName? numericTypeName))
        {
            numericTypeName = GetNumericTypeName(typeDeclaration);
            typeDeclaration.SetMetadata(PreferredDotnetNumericTypeNameKey, numericTypeName);
        }

        return numericTypeName;

        static NumericTypeName? GetNumericTypeName(TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration arrayItemsType)
            {
                return arrayItemsType.ReducedType.PreferredDotnetNumericTypeName();
            }

            if ((typeDeclaration.ImpliedCoreTypesOrAny() & (CoreTypes.Number | CoreTypes.Integer)) != 0)
            {
                string? candidateFormat = typeDeclaration.Format();

                if (candidateFormat is string format &&
                    FormatHandlerRegistry.Instance.NumberFormatHandlers.TryGetNumericTypeName(format, out string? typeName, out bool isNetOnly, out string? netStandardFallback))
                {
                    return new NumericTypeName(
                        typeName,
                        isNetOnly,
                        netStandardFallback);
                }
                else
                {
                    return new((typeDeclaration.ImpliedCoreTypes() & CoreTypes.Integer) != 0 ? "long" : "double", false, null);
                }
            }

            return null;
        }
    }

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
    /// Clears the "do not generate" flag, allowing this type to be generated.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the type declaration after the operation has completed.</returns>
    public static TypeDeclaration ClearDoNotGenerate(this TypeDeclaration typeDeclaration)
    {
        typeDeclaration.SetMetadata(DoNotGenerateKey, false);
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
    /// Executes a validation handler, if it has not already been executed.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IKeywordValidationHandler"/>.</typeparam>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="handler">The instance of the <see cref="IKeywordValidationHandler"/></param>
    /// <param name="execute">The function that executes the handler.</param>
    public static void ExecuteValidationHandler<T>(this TypeDeclaration typeDeclaration, T handler, Action<T> execute)
        where T : IKeywordValidationHandler
    {
        if (typeDeclaration.AddExecutedHandler(handler))
        {
            execute(handler);
        }
    }

    /// <summary>
    /// Determines if the given handler has already been executed.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IKeywordValidationHandler"/>.</typeparam>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="handler">The instance of the <see cref="IKeywordValidationHandler"/></param>
    /// <returns><see langword="true"/> if the handler has been executed.</returns>
    public static bool IsHandled<T>(this TypeDeclaration typeDeclaration, T handler)
        where T : IKeywordValidationHandler
    {
        return typeDeclaration.TryGetMetadata(ExecutedValidationHandlersKey, out HashSet<IKeywordValidationHandler>? executedHandlers) && executedHandlers.Contains(handler);
    }

    /// <summary>
    /// Executes a validation handler, if it has not already been executed.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IKeyword"/>.</typeparam>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="keyword">The instance of the <see cref="IKeyword"/></param>
    public static bool AddIgnoredKeyword<T>(this TypeDeclaration typeDeclaration, T keyword)
        where T : IKeyword
    {
        if (!typeDeclaration.TryGetMetadata(IgnoredKeywordsKey, out HashSet<IKeyword>? ignoredKeywords))
        {
            ignoredKeywords = [];
            typeDeclaration.SetMetadata(IgnoredKeywordsKey, ignoredKeywords);
        }

        return ignoredKeywords.Add(keyword);
    }

    /// <summary>
    /// Determines if the given keyword has been ignored.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IKeyword"/>.</typeparam>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="keyword">The instance of the <see cref="IKeyword"/></param>
    /// <returns><see langword="true"/> if the keyword has been ignored.</returns>
    public static bool IsIgnored<T>(this TypeDeclaration typeDeclaration, T keyword)
        where T : IKeyword
    {
        return typeDeclaration.TryGetMetadata(IgnoredKeywordsKey, out HashSet<IKeyword>? ignoredKeywords) && ignoredKeywords.Contains(keyword);
    }

    /// <summary>
    /// Executes a validation handler, if it has not already been executed.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IKeywordValidationHandler"/>.</typeparam>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="handler">The instance of the <see cref="IKeywordValidationHandler"/></param>
    private static bool AddExecutedHandler<T>(this TypeDeclaration typeDeclaration, T handler)
        where T : IKeywordValidationHandler
    {
        if (!typeDeclaration.TryGetMetadata(ExecutedValidationHandlersKey, out HashSet<IKeywordValidationHandler>? executedHandlers))
        {
            executedHandlers = [];
            typeDeclaration.SetMetadata(ExecutedValidationHandlersKey, executedHandlers);
        }

        return executedHandlers.Add(handler);
    }

    private static void AppendCompositionSources(
            Queue<TypeDeclaration> typesToProcess,
            TypeDeclaration rootType,
            TypeDeclaration sourceType)
    {
        if (sourceType.AnyOfCompositionTypes() is IReadOnlyDictionary<IAnyOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> anyOf)
        {
            // Defer any of until all the AllOf have been processed so we prefer an implicit to the allOf types
            foreach (TypeDeclaration subschema in anyOf.SelectMany(k => k.Value))
            {
                typesToProcess.Enqueue(subschema.ReducedTypeDeclaration().ReducedType);
            }
        }

        if (sourceType.OneOfCompositionTypes() is IReadOnlyDictionary<IOneOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> oneOf)
        {
            // Defer any of until all the AllOf have been processed so we prefer an implicit to the allOf types
            foreach (TypeDeclaration subschema in oneOf.SelectMany(k => k.Value))
            {
                typesToProcess.Enqueue(subschema.ReducedTypeDeclaration().ReducedType);
            }
        }
    }

    /// <summary>
    /// Gets the IJsonElement interface implemented by the type.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>The curiously recursive interface name in the form <c>IJsonElement&lt;DotNetTypeName()&gt;</c>.</returns>
    /// <summary>
    /// Gets the IJsonElement interface implemented by the type.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>The curiously recursive interface name in the form <c>IJsonElement&lt;DotNetTypeName()&gt;</c>.</returns>
    /// <summary>
    /// Gets the IMutableJsonElement interface implemented by the type.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>The curiously recursive interface name in the form <c>IMutableJsonElement&lt;DotNetTypeName()&gt;</c>.</returns>
    private static bool CanReduceTo<T>(LocatedSchema locatedSchema)
    {
        if (locatedSchema.IsBooleanSchema)
        {
            return false;
        }

        IKeyword? hidesSiblingsKeyword = locatedSchema.Vocabulary.Keywords
                .FirstOrDefault(k => k is IHidesSiblingsKeyword && locatedSchema.Schema.HasKeyword(k));

        if (hidesSiblingsKeyword is IKeyword k)
        {
            // We have a keyword that hides its siblings
            // is it something that blocks reduction?
            return k.CanReduce(locatedSchema.Schema);
        }

        // If we have more than one keyword of the type we are reducing to,
        // then we cannot reduce.
        if (locatedSchema.Vocabulary.Keywords.Count(k => k is T && locatedSchema.Schema.HasKeyword(k)) > 1)
        {
            return false;
        }

        return locatedSchema.Vocabulary.Keywords.Where(k => k is not T && locatedSchema.Schema.HasKeyword(k)).All(
                k => k.CanReduce(locatedSchema.Schema));
    }

    /// <summary>
    /// Gets a value indicating whether the type requires number value validation.
    /// </summary>
    /// <param name="that">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type declaration requires number value validation.</returns>
    public static bool RequiresNumberValueValidation(this TypeDeclaration that)
    {
        if (!that.TryGetMetadata(nameof(RequiresNumberValueValidation), out bool? result))
        {
            result = that.Keywords().OfType<INumberValidationKeyword>().Any();
            result |= that.Keywords().OfType<ICoreTypeValidationKeyword>().Any(t => t.AllowedCoreTypes(that).HasFlag(CoreTypes.Integer));
            that.SetMetadata(nameof(RequiresNumberValueValidation), result);
        }

        return result ?? false;
    }

    /// <summary>
    /// Gets a value indicating whether the type declaration requires the value kind.
    /// </summary>
    /// <param name="that">The type declaration.</param>
    /// <returns><see langword="true"/> if the type requires the value kind.</returns>
    public static bool RequiresJsonTokenType(this TypeDeclaration that)
    {
        if (!that.BuildComplete)
        {
            throw new InvalidOperationException("You cannot use RequiresValueKind during the type build process.");
        }

        if (!that.TryGetMetadata(nameof(RequiresJsonTokenType), out bool requiresJsonTokenType))
        {
            requiresJsonTokenType = GetRequiresJsonTokenType(that);
            that.SetMetadata(nameof(RequiresJsonTokenType), requiresJsonTokenType);
        }

        return requiresJsonTokenType;

        static bool GetRequiresJsonTokenType(TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.HasSiblingHidingKeyword())
            {
                return false;
            }

            return
                typeDeclaration.RequiresJsonValueKind() ||
                typeDeclaration.Keywords()
                    .OfType<IValidationConstantProviderKeyword>()
                    .Any();
        }
    }

    /// <summary>
    /// Gets a value indicating whether the type declaration has pattern properties,
    /// either locally or via any allOf/anyOf/oneOf subschema reduced type declarations.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns><see langword="true"/> if the type has pattern properties locally or in any subschema.</returns>
    /// <remarks>
    /// Use this for <c>LocalAndAppliedEvaluatedPropertyType</c> (unevaluatedProperties) checks,
    /// where composed pattern properties are visible to the evaluation.
    /// For <c>LocalEvaluatedPropertyType</c> (additionalProperties) checks, use
    /// <see cref="HasLocalPatternProperties"/> instead, since composed pattern properties
    /// use LocalEvaluatedProperties and are not visible to the root type's additionalProperties evaluation.
    /// </remarks>
    public static bool ImpliedPatternProperties(this TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(nameof(ImpliedPatternProperties), out bool result))
        {
            result = GetImpliedPatternProperties(typeDeclaration);
            typeDeclaration.SetMetadata(nameof(ImpliedPatternProperties), result);
        }

        return result;

        static bool GetImpliedPatternProperties(TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.PatternProperties() is not null)
            {
                return true;
            }

            foreach (TypeDeclaration? type in typeDeclaration.AllOfCompositionTypes().Values.SelectMany(t => t).Select(t => t.ReducedTypeDeclaration().ReducedType))
            {
                if (type is TypeDeclaration t && t.PatternProperties() is not null)
                {
                    return true;
                }
            }

            foreach (TypeDeclaration? type in typeDeclaration.AnyOfCompositionTypes().Values.SelectMany(t => t).Select(t => t.ReducedTypeDeclaration().ReducedType))
            {
                if (type is TypeDeclaration t && t.PatternProperties() is not null)
                {
                    return true;
                }
            }

            foreach (TypeDeclaration? type in typeDeclaration.OneOfCompositionTypes().Values.SelectMany(t => t).Select(t => t.ReducedTypeDeclaration().ReducedType))
            {
                if (type is TypeDeclaration t && t.PatternProperties() is not null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the type declaration has pattern properties
    /// declared directly on it (not via composition).
    /// </summary>
    /// <remarks>
    /// Use this for <c>FallbackObjectPropertyType</c> and <c>LocalEvaluatedPropertyType</c>
    /// (additionalProperties) checks, where only locally declared pattern properties
    /// are considered by the evaluation.
    /// </remarks>
    public static bool HasLocalPatternProperties(this TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.PatternProperties() is not null;
    }
}