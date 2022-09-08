// <copyright file="JsonSchemaTypeBuilder.Naming.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Walks a JSON schema and builds a type map of it.
/// </summary>
public partial class JsonSchemaTypeBuilder
{
    private static void FixNameForCollisionsWithParent(TypeDeclaration type)
    {
        if (type.Parent is TypeDeclaration parent)
        {
            for (int index = 1; parent.DotnetTypeName == type.DotnetTypeName || parent.Children.Any(c => c != type && c.DotnetTypeName == type.DotnetTypeName); index++)
            {
                string trimmedString = type.DotnetTypeName!.Trim('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                string newName = $"{trimmedString}{index}";
                type.SetDotnetTypeName(newName);
            }
        }
    }

    /// <summary>
    /// Sets a built-in type name and namespace.
    /// </summary>
    private static void SetBuiltInTypeNameAndNamespace(TypeDeclaration typeDeclaration, string ns, string type)
    {
        typeDeclaration.SetNamespace(ns);
        typeDeclaration.SetDotnetTypeName(type);
        typeDeclaration.IsBuiltInType = true;
    }

    private static void SetDotnetPropertyNamesCore(TypeDeclaration type)
    {
        var existingNames = new HashSet<string>
        {
            type.DotnetTypeName!,
        };

        foreach (TypeDeclaration child in type.Children)
        {
            existingNames.Add(child.DotnetTypeName!);
        }

        foreach (PropertyDeclaration property in type.Properties)
        {
            SetDotnetPropertyName(type, existingNames, property);
        }

        static void SetDotnetPropertyName(TypeDeclaration type, HashSet<string> existingNames, PropertyDeclaration property)
        {
            string baseName = Formatting.ToPascalCaseWithReservedWords(property.JsonPropertyName).ToString();

            if (baseName == type.DotnetTypeName || type.Children.Any(t => t.DotnetTypeName == baseName))
            {
                baseName += "Value";
            }

            int index = 1;
            string nameString = baseName;
            while (existingNames.Contains(nameString))
            {
                nameString = baseName + index.ToString();
                index++;
            }

            existingNames.Add(nameString);
            property.DotnetPropertyName = nameString;
        }
    }

    private void SetDotnetPropertyNames(TypeDeclaration type, HashSet<TypeDeclaration> visitedTypes)
    {
        if (visitedTypes.Contains(type))
        {
            return;
        }

        visitedTypes.Add(type);

        SetDotnetPropertyNamesCore(type);

        foreach (TypeDeclaration child in type.RefResolvablePropertyDeclarations.Values)
        {
            this.SetDotnetPropertyNames(child, visitedTypes);
        }
    }

    private void SetBuiltInTypeNamesAndNamespaces(TypeDeclaration rootTypeDeclaration)
    {
        HashSet<TypeDeclaration> visitedTypeDeclarations = new();
        this.SetBuiltInTypeNamesAndNamespaces(rootTypeDeclaration, visitedTypeDeclarations);
    }

    private void SetTypeNamesAndNamespaces(TypeDeclaration rootTypeDeclaration, string rootNamespace, ImmutableDictionary<string, string>? baseUriToNamespaceMap, string? rootTypeName)
    {
        HashSet<TypeDeclaration> visitedTypeDeclarations = new();
        this.SetTypeNamesAndNamespaces(rootTypeDeclaration, rootNamespace, baseUriToNamespaceMap, rootTypeName, visitedTypeDeclarations, index: null, isRootTypeDeclaration: true);
        visitedTypeDeclarations.Clear();
        this.RecursivelyFixArrayNames(rootTypeDeclaration, visitedTypeDeclarations, true);
    }

    private void RecursivelyFixArrayNames(TypeDeclaration type, HashSet<TypeDeclaration> visitedTypes, bool skipRoot = false)
    {
        if (visitedTypes.Contains(type))
        {
            return;
        }

        visitedTypes.Add(type);
        var reference = JsonReferenceBuilder.From(type.LocatedSchema.Location);
        if (!skipRoot && this.JsonSchemaConfiguration.IsExplicitArrayType(type.LocatedSchema.Schema) && !this.IsDirectlyInDefinitions(reference))
        {
            if (type.LocatedSchema.Schema.TryGetProperty(this.JsonSchemaConfiguration.ItemsKeyword, out JsonAny value) && value.ValueKind != JsonValueKind.Array)
            {
                TypeDeclaration itemsDeclaration = type.GetTypeDeclarationForProperty(this.JsonSchemaConfiguration.ItemsKeyword);

                string targetName = $"{itemsDeclaration.DotnetTypeName}Array";
                if (type.Parent is TypeDeclaration p)
                {
                    if (type.Parent.Children.Any(c => c.DotnetTypeName == targetName))
                    {
                        targetName = $"{type.DotnetTypeName.AsSpan()[..^5].ToString()}{targetName}";
                    }

                    if (type.Parent.DotnetTypeName == targetName)
                    {
                        targetName = $"{type.DotnetTypeName.AsSpan()[..^5].ToString()}{targetName}";
                    }
                }

                type.SetDotnetTypeName(targetName);
            }
        }

        foreach (TypeDeclaration child in type.RefResolvablePropertyDeclarations.Values)
        {
            this.RecursivelyFixArrayNames(child, visitedTypes);
        }
    }

    private void SetBuiltInTypeNamesAndNamespaces(TypeDeclaration typeDeclaration, HashSet<TypeDeclaration> visitedTypeDeclarations)
    {
        // Quit early if we are already visiting the type declaration.
        if (visitedTypeDeclarations.Contains(typeDeclaration))
        {
            return;
        }

        // Tell ourselves early that we are visiting this type declaration already.
        visitedTypeDeclarations.Add(typeDeclaration);

        if (typeDeclaration.IsBuiltInType)
        {
            // This has already been established as a built in type.
            return;
        }

        (string Ns, string TypeName)? builtInTypeName = this.JsonSchemaConfiguration.GetBuiltInTypeName(typeDeclaration.LocatedSchema.Schema, this.JsonSchemaConfiguration.ValidatingAs);

        if (builtInTypeName is (string, string) bitn)
        {
            SetBuiltInTypeNameAndNamespace(typeDeclaration, bitn.Ns, bitn.TypeName);
            return;
        }

        foreach (TypeDeclaration child in typeDeclaration.RefResolvablePropertyDeclarations.Values)
        {
            this.SetBuiltInTypeNamesAndNamespaces(child, visitedTypeDeclarations);
        }
    }

    private void SetTypeNamesAndNamespaces(TypeDeclaration typeDeclaration, string rootNamespace, ImmutableDictionary<string, string>? baseUriToNamespaceMap, string? rootTypeName, HashSet<TypeDeclaration> visitedTypeDeclarations, int? index = null, bool isRootTypeDeclaration = false)
    {
        // Quit early if we are already visiting the type declaration.
        if (visitedTypeDeclarations.Contains(typeDeclaration))
        {
            return;
        }

        // Tell ourselves early that we are visiting this type declaration already.
        visitedTypeDeclarations.Add(typeDeclaration);

        if (typeDeclaration.IsBuiltInType)
        {
            // We've already set this as a built-in type.
            return;
        }

        string? ns;
        if (baseUriToNamespaceMap is ImmutableDictionary<string, string> butnmp)
        {
            var location = new JsonReference(typeDeclaration.LocatedSchema.Location);

            if (!location.HasAbsoluteUri || !butnmp.TryGetValue(location.Uri.ToString(), out ns))
            {
                ns = rootNamespace;
            }
        }
        else
        {
            ns = rootNamespace;
        }

        this.SetDotnetTypeNameAndNamespace(typeDeclaration, ns, index is null ? "Entity" : $"Entity{index + 1}");

        if (isRootTypeDeclaration && rootTypeName is string rtn)
        {
            typeDeclaration.SetDotnetTypeName(rtn);
        }
        else
        {
            FixNameForCollisionsWithParent(typeDeclaration);
        }

        int childIndex = 0;
        foreach (TypeDeclaration child in typeDeclaration.RefResolvablePropertyDeclarations.Values)
        {
            this.SetTypeNamesAndNamespaces(child, rootNamespace, baseUriToNamespaceMap, rootTypeName, visitedTypeDeclarations, childIndex, false);
            ++childIndex;
        }
    }

    /// <summary>
    /// Calculates a name for the type based on the information we have.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to set the type and namespace.</param>
    /// <param name="rootNamespace">The namespace to use for this type if it has no parent.</param>
    /// <param name="fallbackBaseName">The base type name to fall back on if we can't derive one from our location and type infomration.</param>
    private void SetDotnetTypeNameAndNamespace(TypeDeclaration typeDeclaration, string rootNamespace, string fallbackBaseName)
    {
        var reference = JsonReferenceBuilder.From(typeDeclaration.LocatedSchema.Location);

        if (reference.HasQuery)
        {
            // Remove the query.
            reference = new JsonReferenceBuilder(reference.Scheme, reference.Authority, reference.Path, ReadOnlySpan<char>.Empty, reference.Fragment);
        }

        ReadOnlySpan<char> typename;

        if (typeDeclaration.Parent is null || this.IsDirectlyInDefinitions(reference))
        {
            if (reference.HasFragment)
            {
                int lastSlash = reference.Fragment.LastIndexOf('/');
                typename = Formatting.ToPascalCaseWithReservedWords(reference.Fragment[(lastSlash + 1)..].ToString());
            }
            else if (reference.HasPath)
            {
                int lastSlash = reference.Path.LastIndexOf('/');
                if (lastSlash == reference.Path.Length - 1 && lastSlash > 0)
                {
                    lastSlash = reference.Path[..(lastSlash - 1)].LastIndexOf('/');
                    typename = Formatting.ToPascalCaseWithReservedWords(reference.Path[(lastSlash + 1)..].ToString());
                }
                else if (lastSlash == reference.Path.Length - 1)
                {
                    typename = fallbackBaseName;
                }
                else
                {
                    typename = Formatting.ToPascalCaseWithReservedWords(reference.Path[(lastSlash + 1)..].ToString());
                }
            }
            else
            {
                typename = fallbackBaseName;
            }

            // If we have a collision with a parent name or a named reserved by the code generator, we do not
            // want to set the type and namespace, and return; we will fall through to the code that appends
            // Array/Entity/Value (which are reserved suffixes for the code generator - you may not use them in your
            // generated code.
            if (!this.CollidesWithGeneratedName(typeDeclaration, typename))
            {
                typeDeclaration.SetDotnetTypeName(typename.ToString());
                typeDeclaration.SetNamespace(rootNamespace);
                return;
            }
        }
        else
        {
            if (reference.HasFragment)
            {
                int lastSlash = reference.Fragment.LastIndexOf('/');
                if (char.IsDigit(reference.Fragment[lastSlash + 1]) && lastSlash > 0)
                {
                    int previousSlash = reference.Fragment[..(lastSlash - 1)].LastIndexOf('/');
                    if (previousSlash >= 0)
                    {
                        lastSlash = previousSlash;
                    }

                    typename = Formatting.ToPascalCaseWithReservedWords(reference.Fragment[(lastSlash + 1)..].ToString());
                }
                else if (reference.Fragment[(lastSlash + 1)..].SequenceEqual("items") && lastSlash > 0)
                {
                    int previousSlash = reference.Fragment[..(lastSlash - 1)].LastIndexOf('/');
                    typename = Formatting.ToPascalCaseWithReservedWords(reference.Fragment[(previousSlash + 1)..lastSlash].ToString());
                }
                else
                {
                    typename = Formatting.ToPascalCaseWithReservedWords(reference.Fragment[(lastSlash + 1)..].ToString());
                }
            }
            else if (reference.HasPath)
            {
                int lastSlash = reference.Path.LastIndexOf('/');
                if (lastSlash == reference.Path.Length - 1)
                {
                    lastSlash = reference.Path[..(lastSlash - 1)].LastIndexOf('/');
                }

                if (char.IsDigit(reference.Path[lastSlash + 1]))
                {
                    int previousSlash = reference.Path[..(lastSlash - 1)].LastIndexOf('/');
                    if (previousSlash >= 0)
                    {
                        lastSlash = previousSlash;
                    }
                }

                typename = Formatting.ToPascalCaseWithReservedWords(reference.Path[(lastSlash + 1)..].ToString());
            }
            else
            {
                typename = fallbackBaseName;
            }
        }

        if (this.JsonSchemaConfiguration.IsExplicitArrayType(typeDeclaration.LocatedSchema.Schema) && !typename.EndsWith("Array".AsSpan()))
        {
            Span<char> dnt = stackalloc char[typename.Length + 5];
            typename.CopyTo(dnt);
            "Array".AsSpan().CopyTo(dnt[typename.Length..]);
            typeDeclaration.SetDotnetTypeName(dnt.ToString());
        }
        else if (!typename.EndsWith("Entity".AsSpan()))
        {
            Span<char> dnt = stackalloc char[typename.Length + 6];
            typename.CopyTo(dnt);
            "Entity".AsSpan().CopyTo(dnt[typename.Length..]);
            typeDeclaration.SetDotnetTypeName(dnt.ToString());
        }
        else
        {
            typeDeclaration.SetDotnetTypeName(typename.ToString());
        }

        typeDeclaration.SetNamespace(rootNamespace);
    }

    private bool IsDirectlyInDefinitions(JsonReferenceBuilder reference)
    {
        foreach (string keyword in this.JsonSchemaConfiguration.DefinitionKeywords)
        {
            if (reference.HasFragment && reference.Fragment.Length > 1 && reference.Fragment.LastIndexOf('/') == keyword.Length + 1 && reference.Fragment[1..].StartsWith(keyword))
            {
                return true;
            }
        }

        return false;
    }

    private bool CollidesWithGeneratedName(TypeDeclaration typeDeclaration, ReadOnlySpan<char> dotnetTypeName)
    {
        string name = dotnetTypeName.ToString();
        return
            (typeDeclaration.Parent is TypeDeclaration t && t.DotnetTypeName == name) ||
            this.JsonSchemaConfiguration.GeneratorReservedWords.Contains(name);
    }
}