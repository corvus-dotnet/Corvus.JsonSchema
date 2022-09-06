// <copyright file="JsonSchemaTypeBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Walks a JSON schema and builds a type map of it.
/// </summary>
public class JsonSchemaTypeBuilder
{
    private static readonly JsonReference BuiltInsLocation = new JsonReference("https://github.com/Corvus-dotnet/Corvus/tree/master/schema/builtins");
    private static readonly Regex AnchorPattern = new("^[A-Za-z][-A-Za-z0-9.:_]*$", RegexOptions.Compiled, TimeSpan.FromSeconds(3));

    private static readonly JsonReference DefaultAbsoluteLocation = new("https://endjin.com/");
    private readonly Dictionary<string, LocatedSchema> locatedSchema = new();
    private readonly Dictionary<string, TypeDeclaration> locatedTypeDeclarations = new();
    private readonly IDocumentResolver documentResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSchemaTypeBuilder"/> class.
    /// </summary>
    /// <param name="documentResolver">The document resolver to use.</param>
    public JsonSchemaTypeBuilder(IDocumentResolver documentResolver)
    {
        this.documentResolver = documentResolver;
        this.AnyTypeDeclarationInstance = new TypeDeclaration(this, new LocatedSchema(BuiltInsLocation, JsonAny.From(true)));
        this.AnyTypeDeclarationInstance.IsBuiltInType = true;
        this.AnyTypeDeclarationInstance.SetDotnetTypeName(BuiltInTypes.AnyTypeDeclaration.Type);
        this.AnyTypeDeclarationInstance.SetNamespace(BuiltInTypes.AnyTypeDeclaration.Ns);
        this.NotAnyTypeDeclarationInstance = new TypeDeclaration(this, new LocatedSchema(BuiltInsLocation, JsonAny.From(false)));
        this.NotAnyTypeDeclarationInstance.IsBuiltInType = true;
        this.NotAnyTypeDeclarationInstance.SetDotnetTypeName(BuiltInTypes.NotAnyTypeDeclaration.Type);
        this.NotAnyTypeDeclarationInstance.SetNamespace(BuiltInTypes.NotAnyTypeDeclaration.Ns);
    }

    /// <summary>
    /// Gets or sets the ID keyword for the schema model.
    /// </summary>
    public string IdKeyword { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the items keyword for the schema model.
    /// </summary>
    public string ItemsKeyword { get; set; } = string.Empty;

    /// <summary>
    /// Gets the (required, non-dynamic) reference keyword for the schema model.
    /// </summary>
    public string RefKeyword => this.RefKeywords.Single(k => k.RefKind == RefKind.Ref).Name;

    /// <summary>
    /// Gets or sets the schema keyword for the schema model.
    /// </summary>
    public string SchemaKeyword { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the reference keywords for the schema model.
    /// </summary>
    public ImmutableArray<RefKeyword> RefKeywords { get; set; } = ImmutableArray<RefKeyword>.Empty;

    /// <summary>
    /// Gets or sets the anchor keywords for the schema model.
    /// </summary>
    public ImmutableArray<AnchorKeyword> AnchorKeywords { get; set; } = ImmutableArray<AnchorKeyword>.Empty;

    /// <summary>
    /// Gets or sets the ref-resolvable keywords for the schema model.
    /// </summary>
    public ImmutableArray<RefResolvableKeyword> RefResolvableKeywords { get; set; } = ImmutableArray<RefResolvableKeyword>.Empty;

    /// <summary>
    /// Gets or sets the list of non-reducing keywords for the schema model.
    /// </summary>
    /// <remarks>
    /// These are the keywords that, if placed alongside a reference keyword, prevent the local type from being reduced to the referenced type.
    /// </remarks>
    public ImmutableHashSet<string> IrreducibleKeywords { get; set; } = ImmutableHashSet<string>.Empty;

    /// <summary>
    /// Gets or sets the list of definition keywords for the schema model.
    /// </summary>
    /// <remarks>
    /// These are the keywords that, while they are ref resolvable, do not contribute directly to a type declaration.
    /// </remarks>
    public ImmutableHashSet<string> DefinitionKeywords { get; set; } = ImmutableHashSet<string>.Empty;

    /// <summary>
    /// Gets or sets the validation semantic model to use.
    /// </summary>
    public ValidationSemantics ValidatingAs { get; set; } = ValidationSemantics.Unknown;

    /// <summary>
    /// Gets or sets a predicate that validates the schema.
    /// </summary>
    public Predicate<JsonAny> ValidateSchema { get; set; } = static _ => false;

    /// <summary>
    /// Gets or sets a predicate that indicates whether the given schema is an explicit array type.
    /// </summary>
    public Predicate<JsonAny> IsExplicitArrayType { get; set; } = static _ => false;

    /// <summary>
    /// Gets or sets a predicate that indicates whether the given schema is a simple type.
    /// </summary>
    public Predicate<JsonAny> IsSimpleType { get; set; } = static _ => false;

    /// <summary>
    /// Gets or sets a function to get the built-in type name for a schema with particular validation semantics.
    /// </summary>
    public Func<JsonAny, ValidationSemantics, (string Ns, string TypeName)?> GetBuiltInTypeName { get; set; } = static (_, _) => null;

    /// <summary>
    /// Gets or sets a function to build dotnet properties for given type declaration.
    /// </summary>
    public Action<JsonSchemaTypeBuilder, TypeDeclaration, TypeDeclaration, HashSet<TypeDeclaration>, bool> FindAndBuildPropertiesAdapter { get; set; } = static (_, _, _, _, _) => { };

    /// <summary>
    /// Gets a type declaration to use as the JSON "any" type instance.
    /// </summary>
    public TypeDeclaration AnyTypeDeclarationInstance { get; }

    /// <summary>
    /// Gets a type declaration to use as the JSON "not any" type instance.
    /// </summary>
    public TypeDeclaration NotAnyTypeDeclarationInstance { get; }

    /// <summary>
    /// Walk a JSON document to build a JSON schema type declaration.
    /// </summary>
    /// <param name="documentPath">The path to the root of the json-schema document.</param>
    /// <param name="rootNamespace">The root namespace in which to generate types.</param>
    /// <param name="rebaseAsRoot">Whether to rebase the <paramref name="documentPath"/> as a root document. This should only be done for a JSON schema island in a larger non-schema document.
    /// If <see langword="true"/>, then references in this document should be taken as if the fragment was the root of a document. This will effectively generate a custom $id for the root scope.</param>
    /// <param name="baseUriToNamespaceMap">An optional map of base URIs in the document to namespaces in which to generate the types.</param>
    /// <param name="rootTypeName">An optional explicit type name for the root element.</param>
    /// <param name="subschemaPointer">The subschema within the document at which to being actually generating types, or null if the root schema is required. This allows you to generate a subset of the complete document for large schema.</param>
    /// <returns>A <see cref="Task"/> which, when complete, provides the requested type declaration.</returns>
    /// <remarks>
    /// <para>This method may be called multiple times to build up a set of related types, perhaps from multiple fragments of a single document, or a family of related documents.</para>
    /// <para>Any re-used schema will (if possible) be reduced to the same type, to build a single coherent type system.</para>
    /// <para>Once you have finished adding types, call <see cref="GetTypesToGenerate(TypeDeclaration)"/> to retrieve the set of types that need to be built for each root type you wish to build.</para>
    /// </remarks>
    public async Task<TypeDeclaration?> AddTypeDeclarationsFor(JsonReference documentPath, string rootNamespace, bool rebaseAsRoot = false, ImmutableDictionary<string, string>? baseUriToNamespaceMap = null, string? rootTypeName = null, JsonReference? subschemaPointer = null)
    {
        // First we do a document "load" - this enables us to build the map of the schema, anchors etc.
        JsonReference scope = await this.RegisterDocumentSchema(documentPath, rebaseAsRoot).ConfigureAwait(false);

        // Then we do a second "contextual" pass over the loaded schema from the root location. This enables
        // us to build correct dynamic references.
        TypeDeclaration rootTypeDeclaration = await this.BuildTypeDeclarationFor(this.locatedSchema[scope]).ConfigureAwait(false);

        // Then we reduce the reducible chains of references to the final target type.
        rootTypeDeclaration = this.ReduceTypeDeclarations(rootTypeDeclaration);

        // Set up the built in types before we set the parent type names.
        this.SetBuiltInTypeNamesAndNamespaces(rootTypeDeclaration);

        // Then, set the parent type names.
        this.SetParents(rootTypeDeclaration);

        // Next, we figure out which are built in types, and which need new types and namespaces for the custom types.
        this.SetTypeNamesAndNamespaces(rootTypeDeclaration, rootNamespace, baseUriToNamespaceMap, rootTypeName);

        // Finally, we walk the tree finding and building properties from the properties themselves, and any conditionally applied subschema.
        this.FindAndBuildPropertiesCore(rootTypeDeclaration);

        return rootTypeDeclaration;
    }

    /// <summary>
    /// Find and build properties for the given types.
    /// </summary>
    /// <param name="source">The source from which to find the properties.</param>
    /// <param name="target">The target to which to add the properties.</param>
    /// <param name="typesVisited">The types we have already visited to find properties.</param>
    /// <param name="treatRequiredAsOptional">Whether to treat required properties as optional when adding.</param>
    public void FindAndBuildProperties(TypeDeclaration source, TypeDeclaration target, HashSet<TypeDeclaration> typesVisited, bool treatRequiredAsOptional)
    {
        this.FindAndBuildPropertiesAdapter(this, source, target, typesVisited, treatRequiredAsOptional);
    }

    /// <summary>
    /// Gets the set of types to build, given we start at the given root type declaration.
    /// </summary>
    /// <param name="rootTypeDeclaration">The root type declaration from which to create the types to generate.</param>
    /// <returns>A set of types that need to be built.</returns>
    public ImmutableArray<TypeDeclaration> GetTypesToGenerate(TypeDeclaration rootTypeDeclaration)
    {
        HashSet<TypeDeclaration> typesToGenerate = new();
        GetTypesToGenerateCore(rootTypeDeclaration, typesToGenerate);
        return typesToGenerate.ToImmutableArray();
    }

    /// <summary>
    /// Gets the validation semantics for the document at the given location.
    /// </summary>
    /// <param name="reference">The reference to the document.</param>
    /// <param name="rebaseToRootPath">Whether we are rebasing the element to a root path.</param>
    /// <returns>A <see cref="Task{ValidationSemantics}"/> that, when complete, provides the validation semantics for the reference.</returns>
    public async Task<ValidationSemantics> GetValidationSemantics(JsonReference reference, bool rebaseToRootPath)
    {
        if (!rebaseToRootPath)
        {
            reference = reference.WithFragment(string.Empty);
        }

        JsonElement? element = await this.documentResolver.TryResolve(reference).ConfigureAwait(false);

        if (element is JsonElement e)
        {
            if (e.TryGetProperty(this.SchemaKeyword, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                string? schemaValue = value.GetString();
                if (schemaValue is string sv)
                {
                    if (sv == "http://json-schema.org/draft-06/schema" || sv == "http://json-schema.org/draft-06/schema#")
                    {
                        return ValidationSemantics.Draft6;
                    }

                    if (sv == "http://json-schema.org/draft-07/schema" || sv == "http://json-schema.org/draft-07/schema#")
                    {
                        return ValidationSemantics.Draft7;
                    }

                    if (sv == "https://json-schema.org/draft/2019-09/schema")
                    {
                        return ValidationSemantics.Draft201909;
                    }

                    if (sv == "https://json-schema.org/draft/2020-12/schema")
                    {
                        return ValidationSemantics.Draft202012;
                    }
                }
            }
        }

        return ValidationSemantics.Unknown;
    }

    /// <summary>
    /// Gets the type declaration for the specified property.
    /// </summary>
    /// <param name="parent">The parent type containing the property.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>The type declaration for the named property.</returns>
    /// <exception cref="InvalidOperationException">There was no property at the given location.</exception>
    public TypeDeclaration GetTypeDeclarationForProperty(TypeDeclaration parent, string propertyName)
    {
        if (parent.RefResolvablePropertyDeclarations.TryGetValue(JsonReference.RootFragment.AppendUnencodedPropertyNameToFragment(propertyName), out TypeDeclaration? propertyType))
        {
            return propertyType;
        }

        throw new InvalidOperationException($"Unable to get the type declaration for property '{propertyName}' from the type at {parent.LocatedSchema.Location}");
    }

    /// <summary>
    /// Gets the type declaration for the specified array-like property and the array index.
    /// </summary>
    /// <param name="parent">The parent type containing the property.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="arrayIndex">The index of the array.</param>
    /// <returns>The type declaration for the named property.</returns>
    /// <exception cref="InvalidOperationException">There was no property at the given location.</exception>
    public TypeDeclaration GetTypeDeclarationForPropertyArrayIndex(TypeDeclaration parent, string propertyName, int arrayIndex)
    {
        if (parent.RefResolvablePropertyDeclarations.TryGetValue(JsonReference.RootFragment.AppendUnencodedPropertyNameToFragment(propertyName).AppendArrayIndexToFragment(arrayIndex), out TypeDeclaration? propertyType))
        {
            return propertyType;
        }

        throw new InvalidOperationException($"Unable to get the type declaration for array property '{propertyName}' at the array index '{arrayIndex}' from the type at {parent.LocatedSchema.Location}");
    }

    /// <summary>
    /// Gets the type declaration for the specified mapped property property.
    /// </summary>
    /// <param name="parent">The parent type containing the property.</param>
    /// <param name="propertyName">The name of the map property.</param>
    /// <param name="mapName">The name of the property in the map.</param>
    /// <returns>The type declaration for the named mapped property.</returns>
    /// <exception cref="InvalidOperationException">There was no property at the given location.</exception>
    public TypeDeclaration GetTypeDeclarationForMappedProperty(TypeDeclaration parent, string propertyName, string mapName)
    {
        if (parent.RefResolvablePropertyDeclarations.TryGetValue(JsonReference.RootFragment.AppendUnencodedPropertyNameToFragment(propertyName).AppendUnencodedPropertyNameToFragment(mapName), out TypeDeclaration? propertyType))
        {
            return propertyType;
        }

        throw new InvalidOperationException($"Unable to get the type declaration map value '{mapName}' in the property '{propertyName}' from the type at {parent.LocatedSchema.Location}");
    }

    /// <summary>
    /// Gets the reduced type declaration for the specified location.
    /// </summary>
    /// <param name="location">The location for which to get the type declaration.</param>
    /// <returns>The reduced type declaraitn for the specified location.</returns>
    /// <exception cref="InvalidOperationException">No type could be found for the given location.</exception>
    internal TypeDeclaration GetReducedTypeDeclarationFor(JsonReference location)
    {
        if (this.locatedTypeDeclarations.TryGetValue(location, out TypeDeclaration? baseTypeDeclaration))
        {
            if (baseTypeDeclaration.TryGetReducedType(out TypeDeclaration reducedType))
            {
                return reducedType;
            }

            return baseTypeDeclaration;
        }

        throw new InvalidOperationException($"Unable to find type for '{location}'");
    }

    /// <summary>
    /// Gets the reduced type declaration for the specified location.
    /// </summary>
    /// <param name="location">The location for which to get the type declaration.</param>
    /// <param name="typeDeclaration">The reduced type declaraiton for the specified location.</param>
    /// <returns><see langword="true"/> if a type declaration was found for the location.</returns>
    internal bool TryGetReducedTypeDeclarationFor(JsonReference location, [NotNullWhen(true)] out TypeDeclaration? typeDeclaration)
    {
        if (this.locatedTypeDeclarations.TryGetValue(location, out TypeDeclaration? baseTypeDeclaration))
        {
            if (baseTypeDeclaration.TryGetReducedType(out TypeDeclaration reducedType))
            {
                typeDeclaration = reducedType;
                return true;
            }

            typeDeclaration = baseTypeDeclaration;
            return true;
        }

        typeDeclaration = null;
        return false;
    }

    private static void GetTypesToGenerateCore(TypeDeclaration type, HashSet<TypeDeclaration> typesToGenerate)
    {
        if (typesToGenerate.Contains(type) || type.IsBuiltInType)
        {
            return;
        }

        typesToGenerate.Add(type);
        foreach (TypeDeclaration child in type.RefResolvablePropertyDeclarations.Values)
        {
            GetTypesToGenerateCore(child, typesToGenerate);
        }
    }

    private static JsonDocument GetDocumentFrom(JsonElement documentRoot)
    {
        ArrayBufferWriter<byte> abw = new();
        using Utf8JsonWriter writer = new(abw);
        documentRoot.WriteTo(writer);
        writer.Flush();
        return JsonDocument.Parse(abw.WrittenMemory);
    }

    private static JsonReference MakeAbsolute(JsonReference location)
    {
        if (location.HasAbsoluteUri)
        {
            return location;
        }

        return DefaultAbsoluteLocation.Apply(location);
    }

    private static (JsonReference Location, JsonReference Pointer) GetLocationAndPointerForAnchor(LocatedSchema baseSchemaForReference, string anchor, JsonReference baseSchemaForReferenceLocation)
    {
        JsonReference schemaForRefPointer;
        if (baseSchemaForReference.TryGetAnchor(anchor, out Anchor? registeredAnchor))
        {
            LocatedSchema schemaForRef = registeredAnchor.Schema;

            // Figure out a base schema location from the location of the anchored schema.
            baseSchemaForReferenceLocation = schemaForRef.Location.WithFragment(string.Empty);
            schemaForRefPointer = new JsonReference(ReadOnlySpan<char>.Empty, schemaForRef.Location.Fragment);
        }
        else
        {
            throw new InvalidOperationException($"Unable to find the anchor '{anchor}' in the schema at location '{baseSchemaForReferenceLocation}'");
        }

        return (baseSchemaForReferenceLocation, schemaForRefPointer);
    }

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

    private void SetParents(TypeDeclaration rootTypeDeclaration)
    {
        HashSet<TypeDeclaration> visitedTypes = new();
        this.SetParentsCore(rootTypeDeclaration, visitedTypes);
    }

    private void SetParentsCore(TypeDeclaration type, HashSet<TypeDeclaration> visitedTypes)
    {
        if (visitedTypes.Contains(type))
        {
            return;
        }

        visitedTypes.Add(type);

        type.SetParent();
        foreach (TypeDeclaration child in type.RefResolvablePropertyDeclarations.Values)
        {
            this.SetParentsCore(child, visitedTypes);
        }
    }

    private void FindAndBuildPropertiesCore(TypeDeclaration rootTypeDeclaration)
    {
        HashSet<TypeDeclaration> typesVisitedForBuild = new();

        this.FindAndBuildPropertiesCore(rootTypeDeclaration, typesVisitedForBuild);

        typesVisitedForBuild.Clear();
        this.SetDotnetPropertyNames(rootTypeDeclaration, typesVisitedForBuild);
    }

    private void FindAndBuildPropertiesCore(TypeDeclaration type, HashSet<TypeDeclaration> typesVisitedForBuild)
    {
        if (typesVisitedForBuild.Contains(type))
        {
            return;
        }

        typesVisitedForBuild.Add(type);

        HashSet<TypeDeclaration> typesVisited = new();

        this.FindAndBuildPropertiesAdapter(this, type, type, typesVisited, false);

        foreach (TypeDeclaration subtype in type.RefResolvablePropertyDeclarations.Values)
        {
            this.FindAndBuildPropertiesCore(subtype, typesVisitedForBuild);
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

    private void SetDotnetPropertyNamesCore(TypeDeclaration type)
    {
        if (type.DotnetTypeName is null)
        {
            throw new InvalidOperationException("Unable to set dotnet property names until the dotnet type name is set.");
        }

        var existingNames = new HashSet<string>
        {
            type.DotnetTypeName,
        };

        foreach (PropertyDeclaration property in type.Properties)
        {
            SetDotnetPropertyName(existingNames, property);
        }

        static void SetDotnetPropertyName(HashSet<string> existingNames, PropertyDeclaration property)
        {
            ReadOnlySpan<char> baseName = Formatting.ToPascalCaseWithReservedWords(property.JsonPropertyName);
            Span<char> name = stackalloc char[baseName.Length + 3];
            baseName.CopyTo(name);
            int suffixLength = 0;
            int index = 1;
            string nameString = name[..(baseName.Length + suffixLength)].ToString();
            while (existingNames.Contains(nameString))
            {
                index.TryFormat(name[baseName.Length..], out suffixLength);
                index++;
                nameString = name[..(baseName.Length + suffixLength)].ToString();
            }

            existingNames.Add(nameString);
            property.DotnetPropertyName = nameString;
        }
    }

    //// Set type names and namespaces

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

        if (!skipRoot && this.IsExplicitArrayType(type.LocatedSchema.Schema))
        {
            if (type.LocatedSchema.Schema.TryGetProperty(this.ItemsKeyword, out JsonAny value) && value.ValueKind != JsonValueKind.Array)
            {
                TypeDeclaration itemsDeclaration = this.GetTypeDeclarationForProperty(type, this.ItemsKeyword);

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

        (string Ns, string TypeName)? builtInTypeName = this.GetBuiltInTypeName(typeDeclaration.LocatedSchema.Schema, this.ValidatingAs);

        if (builtInTypeName is (string, string) bitn)
        {
            this.SetBuiltInTypeNameAndNamespace(typeDeclaration, bitn.Ns, bitn.TypeName);
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
    /// Sets a built-in type name and namespace.
    /// </summary>
    private void SetBuiltInTypeNameAndNamespace(TypeDeclaration typeDeclaration, string ns, string type)
    {
        typeDeclaration.SetNamespace(ns);
        typeDeclaration.SetDotnetTypeName(type);
        typeDeclaration.IsBuiltInType = true;
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

        // Remove the query.
        reference = new JsonReferenceBuilder(reference.Scheme, reference.Authority, reference.Path, ReadOnlySpan<char>.Empty, reference.Fragment);

        if (typeDeclaration.Parent is null)
        {
            if (reference.HasFragment)
            {
                int lastSlash = reference.Fragment.LastIndexOf('/');
                ReadOnlySpan<char> dnt = Formatting.ToPascalCaseWithReservedWords(reference.Fragment[(lastSlash + 1)..].ToString());
                typeDeclaration.SetDotnetTypeName(dnt.ToString());
            }
            else if (reference.HasPath)
            {
                int lastSlash = reference.Path.LastIndexOf('/');
                if (lastSlash == reference.Path.Length - 1 && lastSlash > 0)
                {
                    lastSlash = reference.Path[..(lastSlash - 1)].LastIndexOf('/');
                    ReadOnlySpan<char> dnt = Formatting.ToPascalCaseWithReservedWords(reference.Path[(lastSlash + 1)..].ToString());
                    typeDeclaration.SetDotnetTypeName(dnt.ToString());
                }
                else if (lastSlash == reference.Path.Length - 1)
                {
                    ReadOnlySpan<char> dnt = fallbackBaseName;
                    typeDeclaration.SetDotnetTypeName(dnt.ToString());
                }
                else
                {
                    ReadOnlySpan<char> dnt = Formatting.ToPascalCaseWithReservedWords(reference.Path[(lastSlash + 1)..].ToString());
                    typeDeclaration.SetDotnetTypeName(dnt.ToString());
                }
            }
            else
            {
                ReadOnlySpan<char> dnt = fallbackBaseName;
                typeDeclaration.SetDotnetTypeName(dnt.ToString());
            }

            typeDeclaration.SetNamespace(rootNamespace);
        }
        else
        {
            ReadOnlySpan<char> typename;

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

            if (this.IsExplicitArrayType(typeDeclaration.LocatedSchema.Schema) && !typename.EndsWith("Array".AsSpan()))
            {
                Span<char> dnt = stackalloc char[typename.Length + 5];
                typename.CopyTo(dnt);
                "Array".AsSpan().CopyTo(dnt[typename.Length..]);
                typeDeclaration.SetDotnetTypeName(dnt.ToString());
            }
            else if (this.IsSimpleType(typeDeclaration.LocatedSchema.Schema) && !typename.EndsWith("Value".AsSpan()))
            {
                Span<char> dnt = stackalloc char[typename.Length + 5];
                typename.CopyTo(dnt);
                "Value".AsSpan().CopyTo(dnt[typename.Length..]);
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
    }

    //// Reduce type declarations

    /// <summary>
    /// This reduces the type declarations required by the root type declaration,
    /// including the root type declaration itself.
    /// </summary>
    private TypeDeclaration ReduceTypeDeclarations(TypeDeclaration root)
    {
        Dictionary<TypeDeclaration, TypeDeclaration?> reductionCache = new();
        if (this.TryReduceTypeDeclarationsCore(root, reductionCache, out TypeDeclaration? reducedType))
        {
            if (root.LocatedSchema.Location != reducedType.LocatedSchema.Location)
            {
                this.locatedTypeDeclarations.Remove(root.LocatedSchema.Location);
                this.locatedTypeDeclarations.Add(root.LocatedSchema.Location, reducedType);
            }

            return reducedType;
        }

        return root;
    }

    private bool TryReduceTypeDeclarationsCore(TypeDeclaration typeDeclaration, Dictionary<TypeDeclaration, TypeDeclaration?> reductionCache, [NotNullWhen(true)] out TypeDeclaration? reducedType)
    {
        if (reductionCache.TryGetValue(typeDeclaration, out TypeDeclaration? cachedReduction))
        {
            reducedType = cachedReduction;

            // Null means we didn't reduce it.
            return cachedReduction is not null;
        }

        typeDeclaration.TryGetReducedType(out reducedType);

        // Add the mapping (which will be to a null instance if the item wasn't reduced;
        reductionCache.Add(typeDeclaration, reducedType);
        this.ReducedRefResolvableProperties(reducedType ?? typeDeclaration, reductionCache);
        return reducedType is not null;
    }

    private void ReducedRefResolvableProperties(TypeDeclaration typeDeclaration, Dictionary<TypeDeclaration, TypeDeclaration?> reductionCache)
    {
        foreach (KeyValuePair<string, TypeDeclaration> refResolvablePropertyDeclaration in typeDeclaration.RefResolvablePropertyDeclarations.ToList())
        {
            if (this.TryReduceTypeDeclarationsCore(refResolvablePropertyDeclaration.Value, reductionCache, out TypeDeclaration? reducedType))
            {
                typeDeclaration.ReplaceRefResolvablePropertyDeclaration(refResolvablePropertyDeclaration.Key, reducedType);
            }
        }
    }

    //// Build type declarations

    private async Task<TypeDeclaration> BuildTypeDeclarationFor(LocatedSchema baseSchema)
    {
        WalkContext context = new(baseSchema);
        return await this.BuildTypeDeclarationFor(context).ConfigureAwait(false);
    }

    private async Task<TypeDeclaration> BuildTypeDeclarationFor(WalkContext context)
    {
        if (!this.locatedSchema.TryGetValue(context.SubschemaLocation, out LocatedSchema? schema))
        {
            throw new InvalidOperationException($"Unable to find the schema at ${context.SubschemaLocation}");
        }

        if (TryGetBooleanSchemaTypeDeclaration(schema, out TypeDeclaration? booleanTypeDeclaration))
        {
            if (this.locatedTypeDeclarations.TryGetValue(context.SubschemaLocation, out TypeDeclaration? existingBooleanTypeDeclaration))
            {
                return existingBooleanTypeDeclaration;
            }

            this.locatedTypeDeclarations.Add(context.SubschemaLocation, booleanTypeDeclaration);
            return booleanTypeDeclaration;
        }

        // Create a type declaration for this location
        TypeDeclaration typeDeclaration = new(this, schema);

        // Are we already building the type here?
        if (this.locatedTypeDeclarations.TryGetValue(context.SubschemaLocation, out TypeDeclaration? existingTypeDeclaration))
        {
            // We need to determine if it has a dynamic reference to a dynamic anchor
            // owned by this type
            if (this.TryGetNewDynamicScope(existingTypeDeclaration, context, out JsonReference? dynamicScope))
            {
                // We remove the existing one and replace it, for this context.
                this.locatedTypeDeclarations.Remove(context.SubschemaLocation);

                // Update the dynamic location
                typeDeclaration.UpdateDynamicLocation(dynamicScope.Value);

                if (this.locatedTypeDeclarations.TryGetValue(typeDeclaration.LocatedSchema.Location, out TypeDeclaration? existingDynamicDeclaration))
                {
                    // If we already exist in the dynamic location
                    // Add it to the current locatedTypeDeclarations for this subschema, and return it.
                    this.locatedTypeDeclarations.Add(context.SubschemaLocation, existingDynamicDeclaration);
                    return existingDynamicDeclaration;
                }

                this.locatedSchema.Add(typeDeclaration.LocatedSchema.Location, typeDeclaration.LocatedSchema);
                this.locatedTypeDeclarations.Add(context.SubschemaLocation, typeDeclaration);
                this.locatedTypeDeclarations.Add(typeDeclaration.LocatedSchema.Location, typeDeclaration);
            }
            else
            {
                // We can just use the existing type.
                return existingTypeDeclaration;
            }
        }
        else
        {
            this.locatedTypeDeclarations.Add(context.SubschemaLocation, typeDeclaration);
        }

        // Capture the original location before potentially entering a dynamic scope.
        JsonReference currentLocation = context.SubschemaLocation;

        // Try to enter the dynamic scope of the ID - note that this *replaces*
        // the current scope, so we will be automatically popping it when we get done.
        bool enteredDynamicScope = false;

        try
        {
            if (this.TryEnterDynamicScope(context, schema))
            {
                enteredDynamicScope = true;
                if (currentLocation != context.SubschemaLocation)
                {
                    // We have already built this in a preceding reference, at the dynamic location, so we need to replace our "local" version of the type
                    // and then return the version we were already building/had already built.
                    if (this.locatedTypeDeclarations.TryGetValue(context.SubschemaLocation, out TypeDeclaration? alreadyBuildingDynamicTypeDeclaration))
                    {
                        this.locatedTypeDeclarations.Remove(currentLocation);
                        this.locatedTypeDeclarations.Add(currentLocation, alreadyBuildingDynamicTypeDeclaration);
                        return alreadyBuildingDynamicTypeDeclaration;
                    }

                    this.locatedTypeDeclarations.Add(context.SubschemaLocation, typeDeclaration);
                }
            }

            await this.AddSubschemaForRefKeywords(context, typeDeclaration).ConfigureAwait(false);
            await this.AddSubschemaForRefResolvableKeywords(context, typeDeclaration).ConfigureAwait(false);

            return typeDeclaration;
        }
        finally
        {
            if (enteredDynamicScope)
            {
                context.LeaveScope();
            }
        }

        bool TryGetBooleanSchemaTypeDeclaration(LocatedSchema schema, [NotNullWhen(true)] out TypeDeclaration? booleanTypeDeclaration)
        {
            if (schema.Schema.ValueKind == JsonValueKind.True)
            {
                // We neeed to return a type declaration for JsonAny.
                booleanTypeDeclaration = this.AnyTypeDeclarationInstance;
                return true;
            }

            if (schema.Schema.ValueKind == JsonValueKind.False)
            {
                // We neeed to return a type declaration for JsonNotAny.
                booleanTypeDeclaration = this.NotAnyTypeDeclarationInstance;
                return true;
            }

            booleanTypeDeclaration = null;
            return false;
        }
    }

    private bool TryGetNewDynamicScope(TypeDeclaration existingTypeDeclaration, WalkContext context, [NotNullWhen(true)] out JsonReference? dynamicScope)
    {
        HashSet<string> dynamicReferences = this.GetDynamicReferences(existingTypeDeclaration);

        if (dynamicReferences.Count == 0)
        {
            dynamicScope = null;
            return false;
        }

        foreach (string dynamicAnchor in dynamicReferences)
        {
            if (context.TryGetScopeForFirstDynamicAnchor(dynamicAnchor, out JsonReference? baseScopeLocation))
            {
                // We have found a new dynamic anchor in the containing scope, so we cannot share a type
                // declaration with the previous instance.
                if (context.TryGetPreviousScope(out JsonReference? location) && location == baseScopeLocation)
                {
                    dynamicScope = location;
                    return true;
                }
            }
        }

        dynamicScope = null;
        return false;
    }

    private HashSet<string> GetDynamicReferences(TypeDeclaration typeDeclaration)
    {
        HashSet<string> result = new();
        HashSet<TypeDeclaration> visitedTypes = new();
        this.GetDynamicReferences(typeDeclaration, result, visitedTypes);
        return result;
    }

    private void GetDynamicReferences(TypeDeclaration type, HashSet<string> result, HashSet<TypeDeclaration> visitedTypes)
    {
        if (visitedTypes.Contains(type))
        {
            return;
        }

        visitedTypes.Add(type);

        var dynamicRefKeywords = this.RefKeywords.Where(k => k.RefKind == RefKind.DynamicRef).ToDictionary(k => (string)new JsonReference("#").AppendUnencodedPropertyNameToFragment(k.Name), v => v);

        foreach (KeyValuePair<string, TypeDeclaration> prop in type.RefResolvablePropertyDeclarations)
        {
            if (dynamicRefKeywords.TryGetValue(prop.Key, out RefKeyword? refKeyword))
            {
                if (type.LocatedSchema.Schema.TryGetProperty(refKeyword.Name, out JsonAny value))
                {
                    var reference = new JsonReference(value);
                    if (reference.HasFragment)
                    {
                        ReadOnlySpan<char> fragmentWithoutLeadingHash = reference.Fragment[1..];
                        if (AnchorPattern.IsMatch(fragmentWithoutLeadingHash))
                        {
                            result.Add(fragmentWithoutLeadingHash.ToString());
                        }
                    }
                }
            }

            this.GetDynamicReferences(prop.Value, result, visitedTypes);
        }
    }

    private bool TryEnterDynamicScope(WalkContext context, LocatedSchema schema)
    {
        if (!schema.Schema.TryGetProperty(this.IdKeyword, out JsonAny idValue))
        {
            return false;
        }

        if ((this.ValidatingAs & ValidationSemantics.Pre201909) != 0 && HasRefKeyword(schema))
        {
            // Ignore ID if we are pre-draft2019-09 semantics and a reference keyword is present.
            return false;
        }

        context.EnterDynamicScope(context.Scope.Location.Apply(new JsonReference(idValue)), schema);
        return true;

        bool HasRefKeyword(LocatedSchema subschema)
        {
            foreach (RefKeyword keyword in this.RefKeywords)
            {
                if (subschema.Schema.HasProperty(keyword.Name))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private async Task AddSubschemaForRefKeywords(WalkContext context, TypeDeclaration typeDeclaration)
    {
        LocatedSchema schema = typeDeclaration.LocatedSchema;

        foreach (RefKeyword keyword in this.RefKeywords)
        {
            if (schema.Schema.TryGetProperty(keyword.Name, out JsonAny value))
            {
                context.EnterSubschemaScopeForUnencodedPropertyName(keyword.Name);
                JsonReference subschemaPath = new JsonReference("#").AppendUnencodedPropertyNameToFragment(keyword.Name);

                switch (keyword.RefKind)
                {
                    case RefKind.Ref:
                        await this.AddSubschemaForRef(subschemaPath, value, context, typeDeclaration).ConfigureAwait(false);
                        break;
                    case RefKind.DynamicRef:
                        await this.AddSubschemaForDynamicRef(subschemaPath, value, context, typeDeclaration).ConfigureAwait(false);
                        break;
                    case RefKind.RecursiveRef:
                        await this.AddSubschemaForRecursiveRef(subschemaPath, value, context, typeDeclaration).ConfigureAwait(false);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown reference kind '{Enum.GetName(keyword.RefKind)}' at '{context.SubschemaLocation}'");
                }

                context.LeaveScope();

                // We can return immediately because there can only be one ref-type keyword in a valid schema, and we've already validated the schema on the way in.
                return;
            }
        }
    }

    private async Task AddSubschemaForRef(JsonReference subschemaPath, JsonAny referenceValue, WalkContext context, TypeDeclaration typeDeclaration)
    {
        JsonReference reference = new(HttpUtility.UrlDecode(referenceValue));

        LocatedSchema baseSchemaForReference;
        JsonReference baseSchemaForReferenceLocation;

        // First, we need to find the base schema against which we are resolving pointers/anchors
        if (reference.HasUri)
        {
            if (reference.HasAbsoluteUri)
            {
                // Find the base schema, ignoring the fragment
                baseSchemaForReferenceLocation = reference.WithFragment(string.Empty);
                baseSchemaForReference = await this.ResolveBaseReference(baseSchemaForReferenceLocation).ConfigureAwait(false) ?? throw new InvalidOperationException($"Unable to load the schema at location '{baseSchemaForReferenceLocation}'");
            }
            else
            {
                // Apply to the parent scope, ignoring the fragment
                baseSchemaForReferenceLocation = context.Scope.Location.Apply(reference.WithFragment(string.Empty));
                baseSchemaForReference = await this.ResolveBaseReference(baseSchemaForReferenceLocation).ConfigureAwait(false) ?? throw new InvalidOperationException($"Unable to load the schema at location '{baseSchemaForReferenceLocation}'");
            }
        }
        else
        {
            baseSchemaForReferenceLocation = context.Scope.Location;
            baseSchemaForReference = context.Scope.Schema;
        }

        // Now, figure out the pointer or anchor if we have one
        JsonReference schemaForRefPointer;

        string fragmentWithoutLeadingHash = reference.HasFragment ? reference.Fragment[1..].ToString() : string.Empty;
        bool referenceHasAnchor = !string.IsNullOrEmpty(fragmentWithoutLeadingHash) && AnchorPattern.IsMatch(fragmentWithoutLeadingHash);
        if (referenceHasAnchor)
        {
            // The fragment is an anchor
            (baseSchemaForReferenceLocation, schemaForRefPointer) = GetLocationAndPointerForAnchor(baseSchemaForReference, fragmentWithoutLeadingHash, baseSchemaForReferenceLocation);
        }
        else
        {
            schemaForRefPointer = this.GetPointerForReference(baseSchemaForReference, baseSchemaForReferenceLocation, reference);
        }

        context.EnterReferenceScope(baseSchemaForReferenceLocation, baseSchemaForReference, schemaForRefPointer);
        TypeDeclaration subschemaTypeDeclaration = await this.BuildTypeDeclarationFor(context);
        typeDeclaration.AddRefResolvablePropertyDeclaration(subschemaPath, subschemaTypeDeclaration);
        context.LeaveScope();
    }

    private async Task AddSubschemaForDynamicRef(JsonReference subschemaPath, JsonAny referenceValue, WalkContext context, TypeDeclaration typeDeclaration)
    {
        JsonReference reference = new(HttpUtility.UrlDecode(referenceValue));

        LocatedSchema baseSchemaForReference;
        JsonReference baseSchemaForReferenceLocation;

        // First, we need to find the base schema against which we are resolving pointers/anchors
        if (reference.HasUri)
        {
            if (reference.HasAbsoluteUri)
            {
                // Find the base schema, ignoring the fragment
                baseSchemaForReferenceLocation = reference.WithFragment(string.Empty);
                baseSchemaForReference = await this.ResolveBaseReference(baseSchemaForReferenceLocation).ConfigureAwait(false) ?? throw new InvalidOperationException($"Unable to load the schema at location '{baseSchemaForReferenceLocation}'");
            }
            else
            {
                // Apply to the parent scope, ignoring the fragment
                baseSchemaForReferenceLocation = context.Scope.Location.Apply(reference.WithFragment(string.Empty));
                baseSchemaForReference = await this.ResolveBaseReference(baseSchemaForReferenceLocation).ConfigureAwait(false) ?? throw new InvalidOperationException($"Unable to load the schema at location '{baseSchemaForReferenceLocation}'");
            }
        }
        else
        {
            baseSchemaForReferenceLocation = context.Scope.Location;
            baseSchemaForReference = context.Scope.Schema;
        }

        string fragmentWithoutLeadingHash = reference.HasFragment ? reference.Fragment[1..].ToString() : string.Empty;

        // We've reached the first landing spot. Does it define a dynamic anchor?
        bool referenceHasAnchor = !string.IsNullOrEmpty(fragmentWithoutLeadingHash) && AnchorPattern.IsMatch(fragmentWithoutLeadingHash);

        bool hasDynamicAnchor =
            referenceHasAnchor &&
            baseSchemaForReference.TryGetAnchor(fragmentWithoutLeadingHash, out Anchor? registeredAnchor) &&
            registeredAnchor.IsDynamic;

        // It does, so look in the scopes for the first dynamic anchor
        if (hasDynamicAnchor && context.TryGetScopeForFirstDynamicAnchor(fragmentWithoutLeadingHash, out JsonReference? baseScopeLocation))
        {
            baseSchemaForReferenceLocation = baseScopeLocation.Value;
            if (!this.locatedSchema.TryGetValue(baseSchemaForReferenceLocation, out LocatedSchema? dynamicBaseSchema))
            {
                throw new InvalidOperationException($"Unable to resolve the dynamic base schema at '{baseSchemaForReferenceLocation}'");
            }

            baseSchemaForReference = dynamicBaseSchema;
        }

        JsonReference schemaForRefPointer;

        if (referenceHasAnchor)
        {
            // The fragment is an anchor
            (baseSchemaForReferenceLocation, schemaForRefPointer) = GetLocationAndPointerForAnchor(baseSchemaForReference, fragmentWithoutLeadingHash, baseSchemaForReferenceLocation);
        }
        else
        {
            schemaForRefPointer = this.GetPointerForReference(baseSchemaForReference, baseSchemaForReferenceLocation, reference);
        }

        context.EnterReferenceScope(baseSchemaForReferenceLocation, baseSchemaForReference, schemaForRefPointer);
        TypeDeclaration subschemaTypeDeclaration = await this.BuildTypeDeclarationFor(context);
        typeDeclaration.AddRefResolvablePropertyDeclaration(subschemaPath, subschemaTypeDeclaration);
        context.LeaveScope();
    }

    private async Task AddSubschemaForRecursiveRef(JsonReference subschemaPath, JsonAny referenceValue, WalkContext context, TypeDeclaration typeDeclaration)
    {
        JsonReference reference = new(HttpUtility.UrlDecode(referenceValue));

        LocatedSchema baseSchemaForReference;
        JsonReference baseSchemaForReferenceLocation;

        // First, we need to find the base type against which we are resolving pointers/anchors
        if (reference.HasUri)
        {
            if (reference.HasAbsoluteUri)
            {
                // Find the base schema, ignoring the fragment
                baseSchemaForReferenceLocation = reference.WithFragment(string.Empty);
                baseSchemaForReference = await this.ResolveBaseReference(baseSchemaForReferenceLocation).ConfigureAwait(false) ?? throw new InvalidOperationException($"Unable to load the schema at location '{baseSchemaForReferenceLocation}'");
            }
            else
            {
                // Apply to the parent scope, ignoring the fragment
                baseSchemaForReferenceLocation = context.Scope.Location.Apply(reference.WithFragment(string.Empty));
                baseSchemaForReference = await this.ResolveBaseReference(baseSchemaForReferenceLocation).ConfigureAwait(false) ?? throw new InvalidOperationException($"Unable to load the schema at location '{baseSchemaForReferenceLocation}'");
            }
        }
        else
        {
            baseSchemaForReferenceLocation = context.Scope.Location;
            baseSchemaForReference = context.Scope.Schema;
        }

        string fragmentWithoutLeadingHash = reference.HasFragment ? reference.Fragment[1..].ToString() : string.Empty;

        // We've reached the first landing spot. Does it define a dynamic anchor?
        bool referenceHasAnchor = !string.IsNullOrEmpty(fragmentWithoutLeadingHash) && AnchorPattern.IsMatch(fragmentWithoutLeadingHash);

        bool hasRecursiveAnchor =
            reference.HasFragment &&
            baseSchemaForReference.IsRecursiveAnchor;

        // It does, so look in the scopes for the first dynamic anchor
        if (hasRecursiveAnchor && context.TryGetScopeForFirstRecursiveAnchor(out JsonReference? baseScopeLocation))
        {
            baseSchemaForReferenceLocation = baseScopeLocation.Value;
            if (!this.locatedSchema.TryGetValue(baseSchemaForReferenceLocation, out LocatedSchema? dynamicBaseSchema))
            {
                throw new InvalidOperationException($"Unable to resolve the dynamic base schema at '{baseSchemaForReferenceLocation}'");
            }

            baseSchemaForReference = dynamicBaseSchema;
        }

        JsonReference schemaForRefPointer;

        if (referenceHasAnchor)
        {
            // The fragment is an anchor
            (baseSchemaForReferenceLocation, schemaForRefPointer) = GetLocationAndPointerForAnchor(baseSchemaForReference, fragmentWithoutLeadingHash, baseSchemaForReferenceLocation);
        }
        else
        {
            schemaForRefPointer = this.GetPointerForReference(baseSchemaForReference, baseSchemaForReferenceLocation, reference);
        }

        context.EnterReferenceScope(baseSchemaForReferenceLocation, baseSchemaForReference, schemaForRefPointer);
        TypeDeclaration subschemaTypeDeclaration = await this.BuildTypeDeclarationFor(context);
        typeDeclaration.AddRefResolvablePropertyDeclaration(subschemaPath, subschemaTypeDeclaration);
        context.LeaveScope();
    }

    private JsonReference GetPointerForReference(LocatedSchema baseSchemaForReference, JsonReference baseSchemaForReferenceLocation, JsonReference reference)
    {
        JsonReference schemaForRefPointer;
        if (reference.HasFragment)
        {
            // If we've already located it, this must be the thing.
            if (this.locatedSchema.TryGetValue(new JsonReference(baseSchemaForReferenceLocation.Uri, reference.Fragment), out _))
            {
                schemaForRefPointer = new JsonReference(ReadOnlySpan<char>.Empty, reference.Fragment);
            }
            else
            {
                // Resolve the pointer, and add the type
                if (!JsonPointerUtilities.TryResolvePointer(baseSchemaForReference.Schema.AsJsonElement, reference.Fragment, out JsonElement? resolvedElement))
                {
                    throw new InvalidOperationException($"Unable to resolve the schema from the base element '{baseSchemaForReferenceLocation}' with the pointer '{reference.Fragment.ToString()}'");
                }

                schemaForRefPointer = new JsonReference(ReadOnlySpan<char>.Empty, reference.Fragment);
                this.AddLocatedSchemaAndSubschema(baseSchemaForReferenceLocation.Apply(schemaForRefPointer), JsonAny.FromJson(resolvedElement.Value));
            }
        }
        else
        {
            schemaForRefPointer = new JsonReference("#");
        }

        return schemaForRefPointer;
    }

    private async Task<LocatedSchema?> ResolveBaseReference(JsonReference baseSchemaForReferenceLocation)
    {
        LocatedSchema? baseReferenceSchema;

        if (!this.locatedSchema.TryGetValue(baseSchemaForReferenceLocation, out baseReferenceSchema))
        {
            JsonReference registeredSchemaReference = await this.RegisterDocumentSchema(baseSchemaForReferenceLocation).ConfigureAwait(false);
            if (!this.locatedSchema.TryGetValue(registeredSchemaReference, out baseReferenceSchema))
            {
                return null;
            }
        }

        return baseReferenceSchema;
    }

    private async Task AddSubschemaForRefResolvableKeywords(WalkContext context, TypeDeclaration typeDeclaration)
    {
        LocatedSchema schema = typeDeclaration.LocatedSchema;

        foreach (RefResolvableKeyword keyword in this.RefResolvableKeywords)
        {
            if (this.DefinitionKeywords.Contains(keyword.Name))
            {
                // Don't build the type for a "definitions" types unless/until we actually use them, typically by reference.
                continue;
            }

            if (schema.Schema.TryGetProperty(keyword.Name, out JsonAny value))
            {
                context.EnterSubschemaScopeForUnencodedPropertyName(keyword.Name);

                // This is the path to the subschema for the property accessed at the keyword, from the parent subschema.
                JsonReference subschemaPath = new JsonReference("#").AppendUnencodedPropertyNameToFragment(keyword.Name);

                switch (keyword.RefResolvablePropertyKind)
                {
                    case RefResolvablePropertyKind.ArrayOfSchema:
                        await this.AddSubschemaFromArrayOfSchemaForRefResolvableKeyword(subschemaPath, value, context, typeDeclaration).ConfigureAwait(false);
                        break;
                    case RefResolvablePropertyKind.MapOfSchema:
                        await this.AddSubschemaFromMapOfSchemaForRefResolvableKeyword(subschemaPath, value, context, typeDeclaration).ConfigureAwait(false);
                        break;
                    case RefResolvablePropertyKind.Schema:
                        await this.AddSubschemaFromSchemaForRefResolvableKeyword(subschemaPath, value, context, typeDeclaration).ConfigureAwait(false);
                        break;
                    case RefResolvablePropertyKind.SchemaIfValueIsSchemaLike:
                        await this.AddSubschemaFromSchemaIfValueIsSchemaLikeForRefResolvableKeyword(subschemaPath, value, context, typeDeclaration).ConfigureAwait(false);
                        break;
                    case RefResolvablePropertyKind.SchemaOrArrayOfSchema:
                        await this.AddSubschemaFromSchemaOrArrayOfSchemaForRefResolvableKeyword(subschemaPath, value, context, typeDeclaration).ConfigureAwait(false);
                        break;
                }

                context.LeaveScope();
            }
        }
    }

    private async Task AddSubschemaFromSchemaForRefResolvableKeyword(JsonReference subschemaPath, JsonAny schema, WalkContext context, TypeDeclaration typeDeclaration)
    {
        if (!this.ValidateSchema(schema))
        {
            throw new InvalidOperationException($"The schema at {context.SubschemaLocation} was not valid.");
        }

        // Build the subschema type declaration.
        TypeDeclaration subschemaTypeDeclaration = await this.BuildTypeDeclarationFor(context).ConfigureAwait(false);

        // And add it to our ref resolvables.
        typeDeclaration.AddRefResolvablePropertyDeclaration(subschemaPath, subschemaTypeDeclaration);
    }

    private async Task AddSubschemaFromArrayOfSchemaForRefResolvableKeyword(JsonReference subschemaPath, JsonAny array, WalkContext context, TypeDeclaration typeDeclaration)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"The value at {context.SubschemaLocation} was not an array.");
        }

        int index = 0;
        foreach (JsonAny item in array.EnumerateArray())
        {
            context.EnterSubschemaScopeForArrayIndex(index);

            // Build the subschema type declaration.
            TypeDeclaration subschemaTypeDeclaration = await this.BuildTypeDeclarationFor(context).ConfigureAwait(false);

            // And add it to our ref resolvables.
            typeDeclaration.AddRefResolvablePropertyDeclaration(subschemaPath.AppendArrayIndexToFragment(index), subschemaTypeDeclaration);

            ++index;
            context.LeaveScope();
        }
    }

    private async Task AddSubschemaFromMapOfSchemaForRefResolvableKeyword(JsonReference subschemaPath, JsonAny map, WalkContext context, TypeDeclaration typeDeclaration)
    {
        if (map.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"The value at {context.SubschemaLocation} was not an object.");
        }

        foreach (JsonObjectProperty item in map.EnumerateObject())
        {
            context.EnterSubschemaScopeForUnencodedPropertyName(item.Name);
            await this.AddSubschemaFromSchemaForRefResolvableKeyword(subschemaPath.AppendUnencodedPropertyNameToFragment(item.Name), item.Value, context, typeDeclaration).ConfigureAwait(false);
            context.LeaveScope();
        }
    }

    private async Task AddSubschemaFromSchemaIfValueIsSchemaLikeForRefResolvableKeyword(JsonReference subschemaPath, JsonAny schema, WalkContext context, TypeDeclaration typeDeclaration)
    {
        if (!this.ValidateSchema(schema))
        {
            // If our schema isn't a valid schema, we just ignore it.
            return;
        }

        // Build the subschema type declaration.
        TypeDeclaration subschemaTypeDeclaration = await this.BuildTypeDeclarationFor(context).ConfigureAwait(false);

        // And add it to our ref resolvables.
        typeDeclaration.AddRefResolvablePropertyDeclaration(subschemaPath, subschemaTypeDeclaration);
    }

    private async Task AddSubschemaFromSchemaOrArrayOfSchemaForRefResolvableKeyword(JsonReference subschemaPath, JsonAny schema, WalkContext context, TypeDeclaration typeDeclaration)
    {
        if (schema.ValueKind == JsonValueKind.Array)
        {
            await this.AddSubschemaFromArrayOfSchemaForRefResolvableKeyword(subschemaPath, schema, context, typeDeclaration).ConfigureAwait(false);
        }
        else
        {
            await this.AddSubschemaFromSchemaForRefResolvableKeyword(subschemaPath, schema, context, typeDeclaration).ConfigureAwait(false);
        }
    }

    // Schema walk and registration

    /// <summary>
    /// Walk a JSON document and build a schema map.
    /// </summary>
    /// <param name="jsonSchemaPath">The path to the JSON schema root document.</param>
    /// <param name="rebaseAsRoot">Whether to rebase this path as a root document. This should only be done for a JSON schema island in a larger non-schema document.
    /// If <see langoword="true"/>, then references in this document should be taken as if the fragment was the root of a document.</param>
    /// <returns>A <see cref="Task"/> which, when complete, provides the base URI for the document.</returns>
    /// <remarks><paramref name="jsonSchemaPath"/> must point to a root scope. If it has a pointer into the document, then <paramref name="rebaseAsRoot"/> must be true.</remarks>
    private async Task<JsonReference> RegisterDocumentSchema(JsonReference jsonSchemaPath, bool rebaseAsRoot = false)
    {
        var uri = new JsonUri(jsonSchemaPath);
        if (!uri.IsValid() || uri.GetUri().IsFile)
        {
            string schemaFile = jsonSchemaPath;

            // If this is, in fact, a local file path, not a uri, then convert to a fullpath and URI-style separators.
            if (!Path.IsPathFullyQualified(schemaFile))
            {
                schemaFile = Path.GetFullPath(schemaFile);
            }

            schemaFile = schemaFile.Replace('\\', '/');
            jsonSchemaPath = new JsonReference(schemaFile);
        }

        JsonReference basePath = jsonSchemaPath.WithFragment(string.Empty);

        if (basePath.Uri.StartsWith(DefaultAbsoluteLocation.Uri))
        {
            basePath = new JsonReference(jsonSchemaPath.Uri[DefaultAbsoluteLocation.Uri.Length..], ReadOnlySpan<char>.Empty);
        }

        JsonElement? optionalDocumentRoot = await this.documentResolver.TryResolve(basePath).ConfigureAwait(false);

        if (optionalDocumentRoot is null)
        {
            throw new InvalidOperationException($"Unable to locate the root document at '{basePath}'");
        }

        JsonElement documentRoot = optionalDocumentRoot.Value;

        if (jsonSchemaPath.HasFragment)
        {
            if (rebaseAsRoot)
            {
                // Switch the root to be an absolute URI
                JsonElement newBase = JsonPointerUtilities.ResolvePointer(documentRoot, jsonSchemaPath.Fragment);
                jsonSchemaPath = DefaultAbsoluteLocation.Apply(new JsonReference($"{Guid.NewGuid()}/Schema"));

                // And add the document back to the document resolver against that root URI
                this.documentResolver.AddDocument(jsonSchemaPath, GetDocumentFrom(newBase));
                JsonElement? resolvedBase = await this.documentResolver.TryResolve(jsonSchemaPath).ConfigureAwait(false);

                if (resolvedBase is null)
                {
                    throw new InvalidOperationException($"Expected to find a rebased schema at {jsonSchemaPath}");
                }

                var rebasedSchema = JsonAny.FromJson(resolvedBase.Value);
                if (!this.ValidateSchema(rebasedSchema))
                {
                    throw new InvalidOperationException($"The document at '{jsonSchemaPath}' is not a valid schema.");
                }

                return this.AddLocatedSchemaAndSubschema(jsonSchemaPath, rebasedSchema);
            }
            else
            {
                throw new InvalidOperationException($"The path '{jsonSchemaPath}' is not a root scope, but 'rebaseAsRoot' was false.");
            }
        }

        var baseSchema = JsonAny.FromJson(documentRoot);

        if (!this.ValidateSchema(baseSchema))
        {
            throw new InvalidOperationException($"The root document at '{basePath}' is not a valid schema.");
        }

        return this.AddLocatedSchemaAndSubschema(basePath, baseSchema);
    }

    private JsonReference AddLocatedSchemaAndSubschema(JsonReference currentLocation, JsonAny schema)
    {
        // First, add the element at the current location.
        bool leavingEarly = false;

        currentLocation = MakeAbsolute(currentLocation);

        if (!this.AddLocatedSchema(currentLocation, schema))
        {
            // We've already registered this schema, so we are going to leave early.
            // But we have to resolve the currentLocation for the ID (if present and applicable)
            leavingEarly = true;
        }

        if (schema.ValueKind != JsonValueKind.Object)
        {
            // We are a boolean schema, so we can just leave.
            return currentLocation;
        }

        if (!RefMatters(schema) && schema.TryGetProperty(this.IdKeyword, out JsonAny id))
        {
            JsonReference previousLocation = currentLocation;
            currentLocation = currentLocation.Apply(new JsonReference(id));

            // We skip adding if we are leaving early
            if (!leavingEarly)
            {
                if (currentLocation.HasFragment)
                {
                    this.AddAnchor(new JsonReference(currentLocation.Uri.ToString()), currentLocation.Fragment.ToString());
                }
                else
                {
                    if (!this.locatedSchema.TryGetValue(previousLocation, out LocatedSchema? previousSchema))
                    {
                        throw new InvalidOperationException($"The previously registered schema for '{previousLocation}' was not found.");
                    }

                    this.locatedSchema.TryAdd(currentLocation, previousSchema);
                }
            }
        }
        else if (schema.TryGetProperty(this.IdKeyword, out JsonAny id2))
        {
            JsonReference previousLocation = currentLocation;
            JsonReference location = currentLocation.Apply(new JsonReference(id2));

            if (!this.locatedSchema.TryGetValue(previousLocation, out LocatedSchema? previousSchema))
            {
                throw new InvalidOperationException($"The previously registered schema for '{previousLocation}' was not found.");
            }

            // Also add the dynamic located schema for the root.
            this.locatedSchema.TryAdd(location, previousSchema);
        }

        // Having (possibly) updated the current location for the ID we can now leave.
        if (leavingEarly)
        {
            return currentLocation;
        }

        AddAnchors(currentLocation, schema);
        AddSubschemas(currentLocation, schema);

        return currentLocation;

        bool RefMatters(JsonAny schema)
        {
            return (this.ValidatingAs & ValidationSemantics.Pre201909) != 0 && schema.HasProperty(this.RefKeyword);
        }

        void AddAnchors(JsonReference currentLocation, JsonAny schema)
        {
            foreach (AnchorKeyword anchorKeyword in this.AnchorKeywords)
            {
                if (schema.TryGetProperty(anchorKeyword.Name, out JsonAny value))
                {
                    this.AddAnchor(currentLocation, value);

                    if (anchorKeyword.IsDynamic)
                    {
                        this.AddDynamicAnchor(currentLocation, value);
                    }

                    if (anchorKeyword.IsRecursive && value.ValueKind == JsonValueKind.True)
                    {
                        this.SetRecursiveAnchor(currentLocation);
                    }
                }
            }
        }

        void AddSubschemas(JsonReference currentLocation, JsonAny schema)
        {
            foreach (RefResolvableKeyword keyword in this.RefResolvableKeywords)
            {
                if (schema.TryGetProperty(keyword.Name, out JsonAny value))
                {
                    AddSubschemasForKeyword(keyword, value, currentLocation, schema);
                }
            }
        }

        void AddSubschemasForKeyword(RefResolvableKeyword keyword, JsonAny value, JsonReference currentLocation, JsonAny schema)
        {
            switch (keyword.RefResolvablePropertyKind)
            {
                case RefResolvablePropertyKind.Schema:
                    AddSubschemasForSchemaProperty(keyword.Name, value, currentLocation);
                    break;
                case RefResolvablePropertyKind.ArrayOfSchema:
                    AddSubschemasForArrayOfSchemaProperty(keyword.Name, value, currentLocation);
                    break;
                case RefResolvablePropertyKind.SchemaOrArrayOfSchema:
                    AddSubschemasForSchemaOrArrayOfSchemaProperty(keyword.Name, value, currentLocation);
                    break;
                case RefResolvablePropertyKind.MapOfSchema:
                    AddSubschemasForMapOfSchemaProperty(keyword.Name, value, currentLocation);
                    break;
                case RefResolvablePropertyKind.SchemaIfValueIsSchemaLike:
                    AddSubschemasForSchemaIfValueIsASchemaLikeKindProperty(keyword.Name, value, currentLocation);
                    break;
                default:
                    throw new InvalidOperationException($"Unrecognized property ref resolvable property kind: {Enum.GetName(keyword.RefResolvablePropertyKind)}");
            }
        }

        void AddSubschemasForSchemaProperty(string propertyName, JsonAny value, JsonReference currentLocation)
        {
            JsonReference propertyLocation = currentLocation.AppendUnencodedPropertyNameToFragment(propertyName);

            if (!this.ValidateSchema(value))
            {
                throw new InvalidOperationException($"The property at {propertyLocation} was expected to be a schema object.");
            }

            this.AddLocatedSchemaAndSubschema(propertyLocation, value);
        }

        void AddSubschemasForArrayOfSchemaProperty(string propertyName, JsonAny value, JsonReference currentLocation)
        {
            JsonReference propertyLocation = currentLocation.AppendUnencodedPropertyNameToFragment(propertyName);
            if (value.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException($"The property at {propertyLocation} was expected to be an array of schema objects.");
            }

            int index = 0;
            foreach (JsonAny subschema in value.EnumerateArray())
            {
                JsonReference subschemaLocation = propertyLocation.AppendArrayIndexToFragment(index);
                this.AddLocatedSchemaAndSubschema(subschemaLocation, subschema);
                ++index;
            }
        }

        void AddSubschemasForSchemaOrArrayOfSchemaProperty(string propertyName, JsonAny value, JsonReference currentLocation)
        {
            if (value.ValueKind == JsonValueKind.Object || value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            {
                AddSubschemasForSchemaProperty(propertyName, value, currentLocation);
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                AddSubschemasForArrayOfSchemaProperty(propertyName, value, currentLocation);
            }
            else
            {
                JsonReference propertyLocation = currentLocation.AppendUnencodedPropertyNameToFragment(propertyName);
                throw new InvalidOperationException($"The property at {propertyLocation} was expected to be either a schema object, or an array of schema objects.");
            }
        }

        void AddSubschemasForMapOfSchemaProperty(string propertyName, JsonAny value, JsonReference currentLocation)
        {
            JsonReference propertyLocation = currentLocation.AppendUnencodedPropertyNameToFragment(propertyName);
            if (value.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"The property at {propertyLocation} was expected to be a map of schema objects.");
            }

            int index = 0;
            foreach (JsonObjectProperty property in value.EnumerateObject())
            {
                JsonReference subschemaLocation = propertyLocation.AppendUnencodedPropertyNameToFragment(property.Name);
                this.AddLocatedSchemaAndSubschema(subschemaLocation, property.Value);
                ++index;
            }
        }

        void AddSubschemasForSchemaIfValueIsASchemaLikeKindProperty(string propertyName, JsonAny value, JsonReference currentLocation)
        {
            JsonReference propertyLocation = currentLocation.AppendUnencodedPropertyNameToFragment(propertyName);

            if (value.ValueKind != JsonValueKind.Object && value.ValueKind != JsonValueKind.False && value.ValueKind != JsonValueKind.True)
            {
                // If we are not an object, that's OK - we just ignore it in this case.
                return;
            }

            this.AddLocatedSchemaAndSubschema(propertyLocation, value);
        }
    }

    private bool AddLocatedSchema(JsonReference location, JsonAny schema)
    {
        location = MakeAbsolute(location);
        return this.locatedSchema.TryAdd(location, new(location, schema));
    }

    private void AddAnchor(JsonReference location, string anchorName)
    {
        // Go back up to root
        JsonReference rootLocation = MakeAbsolute(location.WithFragment(string.Empty));
        location = MakeAbsolute(location);
        if (!this.locatedSchema.TryGetValue(rootLocation, out LocatedSchema? rootSchema))
        {
            throw new InvalidOperationException($"No schema has been registered for '{rootLocation}' while trying to add anchors for '{location}'");
        }

        if (!this.locatedSchema.TryGetValue(location, out LocatedSchema? anchoredSchema))
        {
            throw new InvalidOperationException($"No schema has been registered for anchor at '{location}");
        }

        rootSchema.TryAddAnchor(anchorName, anchoredSchema);
    }

    private void AddDynamicAnchor(JsonReference location, string anchorName)
    {
        // Go back up to root
        JsonReference rootLocation = MakeAbsolute(location.WithFragment(string.Empty));
        location = MakeAbsolute(location);
        if (!this.locatedSchema.TryGetValue(rootLocation, out LocatedSchema? rootSchema))
        {
            throw new InvalidOperationException($"No schema has been registered for '{rootLocation}' while trying to add anchors for '{location}'");
        }

        if (!this.locatedSchema.TryGetValue(location, out LocatedSchema? anchoredSchema))
        {
            throw new InvalidOperationException($"No schema has been registered for anchor at '{location}");
        }

        rootSchema.AddOrUpdateDynamicAnchor(anchorName, anchoredSchema);
    }

    private void SetRecursiveAnchor(JsonReference location)
    {
        location = MakeAbsolute(location);
        if (!this.locatedSchema.TryGetValue(location, out LocatedSchema? anchoredSchema))
        {
            throw new InvalidOperationException($"No schema has been registered for anchor at '{location}");
        }

        anchoredSchema.IsRecursiveAnchor = true;
    }
}