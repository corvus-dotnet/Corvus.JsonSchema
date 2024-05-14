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

    private void SetDotnetPropertyNamesCore(TypeDeclaration type)
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

        void SetDotnetPropertyName(TypeDeclaration type, HashSet<string> existingNames, PropertyDeclaration property)
        {
            string baseName = Formatting.ToPascalCaseWithReservedWords(property.JsonPropertyName).ToString();

            if (baseName == type.DotnetTypeName || type.Children.Any(t => t.DotnetTypeName == baseName) || this.JsonSchemaConfiguration.GeneratorReservedWords.Contains(baseName))
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

        this.SetDotnetPropertyNamesCore(type);

        foreach (TypeDeclaration child in type.RefResolvablePropertyDeclarations.Values)
        {
            this.SetDotnetPropertyNames(child, visitedTypes);
        }
    }

    private void SetBuiltInTypeNamesAndNamespaces(TypeDeclaration rootTypeDeclaration)
    {
        HashSet<TypeDeclaration> visitedTypeDeclarations = [];
        this.SetBuiltInTypeNamesAndNamespaces(rootTypeDeclaration, visitedTypeDeclarations);
    }

    private void SetTypeNamesAndNamespaces(TypeDeclaration rootTypeDeclaration, string rootNamespace, ImmutableDictionary<string, string>? baseUriToNamespaceMap, string? rootTypeName)
    {
        HashSet<TypeDeclaration> visitedTypeDeclarations = [];
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
            if (type.LocatedSchema.Schema.AsObject.TryGetProperty(this.JsonSchemaConfiguration.ItemsKeyword, out JsonAny value) && value.ValueKind != JsonValueKind.Array)
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
        foreach (TypeDeclaration child in typeDeclaration.RefResolvablePropertyDeclarations.Values.OrderBy(t => t.LocatedSchema.Location.ToString()))
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
            reference = new JsonReferenceBuilder(reference.Scheme, reference.Authority, reference.Path, [], reference.Fragment);
        }

        ReadOnlySpan<char> typename;

        if (typeDeclaration.TryGetCorvusTypeName(out string? title))
        {
            typename = Formatting.ToPascalCaseWithReservedWords(title);

            if (!this.CollidesWithGeneratedName(typeDeclaration, typename))
            {
                typeDeclaration.SetDotnetTypeName(typename.ToString());
                typeDeclaration.SetNamespace(rootNamespace);
                return;
            }
        }
        else if (typeDeclaration.Parent is null || this.IsDirectlyInDefinitions(reference))
        {
            if (reference.HasFragment)
            {
                int lastSlash = reference.Fragment.LastIndexOf('/');
                typename = Formatting.ToPascalCaseWithReservedWords(reference.Fragment[(lastSlash + 1)..].ToString());
            }
            else if (reference.HasPath)
            {
                typename = GetNameFromPath(fallbackBaseName, reference);
            }
            else
            {
                typename = fallbackBaseName.AsSpan();
            }

            if (!this.CollidesWithGeneratedName(typeDeclaration, typename) && this.MatchesExistingType(typename, rootNamespace))
            {
                if (reference.HasPath && reference.HasFragment)
                {
#if NET8_0_OR_GREATER
                    typename = $"{GetNameFromPath(fallbackBaseName, reference)}{typename}";
#else
                    typename = $"{GetNameFromPath(fallbackBaseName, reference).ToString()}{typename.ToString()}".AsSpan();
#endif
                }

                while (this.MatchesExistingType(typename, rootNamespace))
                {
                    int startOfSuffix = typename.Length;
                    bool found = false;
                    while (startOfSuffix > 0)
                    {
                        if (!char.IsDigit(typename[^startOfSuffix]))
                        {
                            break;
                        }

                        found = true;
                        startOfSuffix--;
                    }

                    int suffix = 1;

                    if (found)
                    {
#if NET8_0_OR_GREATER
                        suffix = int.Parse(typename[startOfSuffix..]);
#else
                        suffix = int.Parse(typename[startOfSuffix..].ToString());
#endif
                    }

#if NET8_0_OR_GREATER
                    typename = $"{typename[..startOfSuffix]}{suffix}";
#else
                    typename = $"{typename[..startOfSuffix].ToString()}{suffix}".AsSpan();
#endif
                }
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
                else if (reference.Fragment[(lastSlash + 1)..].SequenceEqual("items".AsSpan()) && lastSlash > 0)
                {
                    int previousSlash = reference.Fragment[..(lastSlash - 1)].LastIndexOf('/');
                    if (reference.Fragment[(previousSlash + 1)..lastSlash].SequenceEqual("properties".AsSpan()))
                    {
                        // If it is a property called "Items" then use the property name.
                        typename = Formatting.ToPascalCaseWithReservedWords(reference.Fragment[(lastSlash + 1)..].ToString());
                    }
                    else
                    {
                        // Otherwise, wind back to the parent to create the name.
                        typename = Formatting.ToPascalCaseWithReservedWords(reference.Fragment[(previousSlash + 1)..lastSlash].ToString());
                    }
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
                typename = fallbackBaseName.AsSpan();
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

        static ReadOnlySpan<char> GetNameFromPath(string fallbackBaseName, JsonReferenceBuilder reference)
        {
            ReadOnlySpan<char> typename;
            int lastSlash = reference.Path.LastIndexOf('/');
            if (lastSlash == reference.Path.Length - 1 && lastSlash > 0)
            {
                lastSlash = reference.Path[..(lastSlash - 1)].LastIndexOf('/');
                typename = Formatting.ToPascalCaseWithReservedWords(reference.Path[(lastSlash + 1)..].ToString());
            }
            else if (lastSlash == reference.Path.Length - 1)
            {
                typename = fallbackBaseName.AsSpan();
            }
            else
            {
                int lastDot = reference.Path.LastIndexOf('.');
                if (lastDot > 0)
                {
                    typename = Formatting.ToPascalCaseWithReservedWords(reference.Path[(lastSlash + 1)..lastDot].ToString());
                }
                else
                {
                    typename = Formatting.ToPascalCaseWithReservedWords(reference.Path[(lastSlash + 1)..].ToString());
                }
            }

            return typename;
        }
    }

    private bool MatchesExistingType(ReadOnlySpan<char> typename, string rootNamespace)
    {
        string fqtn = $"{rootNamespace}.{typename.ToString()}";
        return this.locatedTypeDeclarations.Any(t => t.Value.FullyQualifiedDotnetTypeName == fqtn);
    }

    private bool IsDirectlyInDefinitions(JsonReferenceBuilder reference)
    {
        foreach (string keyword in this.JsonSchemaConfiguration.DefinitionKeywords)
        {
            if (reference.HasFragment && reference.Fragment.Length > 1 && reference.Fragment.LastIndexOf('/') == keyword.Length + 1 && reference.Fragment[1..].StartsWith(keyword.AsSpan()))
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