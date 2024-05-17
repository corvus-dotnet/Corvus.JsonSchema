// <copyright file="TypeDeclaration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A type declaration for a Json Schema.
/// </summary>
[DebuggerDisplay("{DotnetTypeName} Location={LocatedSchema.Location}")]
public class TypeDeclaration
{
    private readonly JsonSchemaTypeBuilder typeBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeDeclaration"/> class.
    /// </summary>
    /// <param name="typeBuilder">The <see cref="JsonSchemaTypeBuilder"/> building the types.</param>
    /// <param name="schema">The actual schema for the type declaration, including its location.</param>
    public TypeDeclaration(JsonSchemaTypeBuilder typeBuilder, LocatedSchema schema)
    {
        this.typeBuilder = typeBuilder;
        this.LocatedSchema = schema;
        this.RefResolvablePropertyDeclarations = ImmutableDictionary<string, TypeDeclaration>.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeDeclaration"/> class.
    /// </summary>
    /// <param name="typeBuilder">The <see cref="JsonSchemaTypeBuilder"/> building the types.</param>
    /// <param name="schema">The actual schema for the type declaration, including its location.</param>
    /// <param name="refResolvablePropertyDeclarations">The type declarations for ref-resolvable properties.</param>
    public TypeDeclaration(JsonSchemaTypeBuilder typeBuilder, LocatedSchema schema, ImmutableDictionary<string, TypeDeclaration> refResolvablePropertyDeclarations)
    {
        this.typeBuilder = typeBuilder;
        this.LocatedSchema = schema;
        this.RefResolvablePropertyDeclarations = refResolvablePropertyDeclarations;
    }

    /// <summary>
    /// Gets the schema for the type declaration.
    /// </summary>
    public LocatedSchema LocatedSchema { get; private set; }

    /// <summary>
    /// Gets the recursive scope for the type declaration.
    /// </summary>
    public JsonReference? RecursiveScope { get; private set; }

    /// <summary>
    /// Gets the location for the schema that defines this type, relative to the
    /// base location.
    /// </summary>
    public JsonReference RelativeSchemaLocation
    {
        get
        {
            return this.typeBuilder.GetRelativeLocationFor(this.LocatedSchema.Location);
        }
    }

    /// <summary>
    /// Gets the dotnet property declarations for the type.
    /// </summary>
    public ImmutableArray<PropertyDeclaration> Properties { get; private set; } = [];

    /// <summary>
    /// Gets the ref-resolvable property declarations for the type declaration.
    /// </summary>
    public ImmutableDictionary<string, TypeDeclaration> RefResolvablePropertyDeclarations { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the type declaration has a dynamic reference.
    /// </summary>
    public bool HasDynamicReference => this.RefResolvablePropertyDeclarations.Any(r => this.typeBuilder.JsonSchemaConfiguration.RefKeywords.Any(k => (k.RefKind == RefKind.DynamicRef || k.RefKind == RefKind.RecursiveRef) && new JsonReference("#").AppendUnencodedPropertyNameToFragment(k.Name) == r.Key));

    /// <summary>
    /// Gets the parent type declaration for this type declaration, or null if this is a root type declaration.
    /// </summary>
    public TypeDeclaration? Parent
    {
        get;
        private set;
    }

    /// <summary>
    /// Gets the set of type declarations nested in this type.
    /// </summary>
    public ImmutableHashSet<TypeDeclaration> Children { get; private set; } = [];

    /// <summary>
    /// Gets the namespace in which to put this type.
    /// </summary>
    /// <remarks>
    /// If <see cref="Parent"/> is not null, then this will be null, and vice-versa.
    /// </remarks>
    public string? Namespace { get; private set; }

    /// <summary>
    /// Gets the dotnet type name for this type.
    /// </summary>
    public string? DotnetTypeName { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this is a built-in type.
    /// </summary>
    public bool IsBuiltInType { get; internal set; }

    /// <summary>
    /// Gets the fully qualified dotnet type name for this type.
    /// </summary>
    public string? FullyQualifiedDotnetTypeName => this.DotnetTypeName is string dnt && this.Namespace is string ns ? (this.IsBuiltInType || this.Parent is null) ? $"{ns}.{dnt}" : $"{this.Parent.FullyQualifiedDotnetTypeName}.{dnt}" : null;

    /// <summary>
    /// Adds a ref resolvable property to the type declaration.
    /// </summary>
    /// <param name="propertyPath">The path to the property from its parent.</param>
    /// <param name="type">The type declaration for the property.</param>
    /// <remarks>
    /// <para>
    /// The path may be a compound path for e.g. array or map properties.
    /// </para>
    /// <para>
    /// This will include all the properties for <see cref="JsonSchemaConfiguration.RefResolvableKeywords"/>, and for irreducible reference
    /// properties.
    /// </para>
    /// </remarks>
    public void AddRefResolvablePropertyDeclaration(string propertyPath, TypeDeclaration type)
    {
        this.RefResolvablePropertyDeclarations = this.RefResolvablePropertyDeclarations.Add(propertyPath, type);
    }

    /// <summary>
    /// Gets the set of types to build, given we start at the given root type declaration.
    /// </summary>
    /// <returns>A set of types that need to be built.</returns>
    public ImmutableArray<TypeDeclaration> GetTypesToGenerate()
    {
        HashSet<TypeDeclaration> typesToGenerate = [];
        GetTypesToGenerateCore(this, typesToGenerate);
        return [.. typesToGenerate];
    }

    /// <summary>
    /// Replace the given ref resolvable property with another type declaration.
    /// </summary>
    /// <param name="propertyPath">The property path at which to replace the type declaration.</param>
    /// <param name="type">The new type declaration.</param>
    public void ReplaceRefResolvablePropertyDeclaration(string propertyPath, TypeDeclaration type)
    {
        this.RefResolvablePropertyDeclarations = this.RefResolvablePropertyDeclarations.SetItem(propertyPath, type);
    }

    /// <summary>
    /// Adds a property to the collection, replacing any that match the json property name.
    /// </summary>
    /// <param name="propertyDeclaration">The property declaration to add.</param>
    /// <remarks>Note that this will happily add duplicate properties.</remarks>
    public void AddOrReplaceProperty(PropertyDeclaration propertyDeclaration)
    {
        int index = this.Properties.IndexOf(propertyDeclaration, PropertyDeclarationEqualityComparer.Instance);
        if (index >= 0)
        {
            this.MergeProperties(index, propertyDeclaration);
        }
        else
        {
            this.Properties = this.Properties.Add(propertyDeclaration);
        }
    }

    /// <summary>
    /// Get the reduced type for this type declaration.
    /// </summary>
    /// <param name="reducedType">The reduced type for the type declaration. If the type was irreducible, this will be the original type.</param>
    /// <returns><see langword="true"/> if the type was reduced, otherwise <see langword="false"/>.</returns>
    public bool TryGetReducedType(out TypeDeclaration reducedType)
    {
        if (!this.CanReduce())
        {
            reducedType = this;
            return false;
        }

        foreach (RefKeyword refKeyword in this.typeBuilder.JsonSchemaConfiguration.RefKeywords)
        {
            if (this.RefResolvablePropertyDeclarations.TryGetValue(new JsonReference("#").AppendUnencodedPropertyNameToFragment(refKeyword.Name), out TypeDeclaration? rr))
            {
                // Attempt to reduce this type down the chain.
                rr.TryGetReducedType(out reducedType);
                return true;
            }
        }

        reducedType = this;
        return false;
    }

    /// <summary>
    /// Gets the type declaration for the specified property.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>The type declaration for the named property.</returns>
    /// <exception cref="InvalidOperationException">There was no property at the given location.</exception>
    public TypeDeclaration GetTypeDeclarationForProperty(string propertyName)
    {
        if (this.RefResolvablePropertyDeclarations.TryGetValue(JsonReference.RootFragment.AppendUnencodedPropertyNameToFragment(propertyName), out TypeDeclaration? propertyType))
        {
            return propertyType;
        }

        throw new InvalidOperationException($"Unable to get the type declaration for property '{propertyName}' from the type at {this.LocatedSchema.Location}");
    }

    /// <summary>
    /// Gets the type declaration for the specified array-like property and the array index.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="arrayIndex">The index of the array.</param>
    /// <returns>The type declaration for the named property.</returns>
    /// <exception cref="InvalidOperationException">There was no property at the given location.</exception>
    public TypeDeclaration GetTypeDeclarationForPropertyArrayIndex(string propertyName, int arrayIndex)
    {
        if (this.RefResolvablePropertyDeclarations.TryGetValue(JsonReference.RootFragment.AppendUnencodedPropertyNameToFragment(propertyName).AppendArrayIndexToFragment(arrayIndex), out TypeDeclaration? propertyType))
        {
            return propertyType;
        }

        throw new InvalidOperationException($"Unable to get the type declaration for array property '{propertyName}' at the array index '{arrayIndex}' from the type at {this.LocatedSchema.Location}");
    }

    /// <summary>
    /// Gets the type declaration for the specified mapped property.
    /// </summary>
    /// <param name="propertyName">The name of the map property.</param>
    /// <param name="mapName">The name of the property in the map.</param>
    /// <returns>The type declaration for the named mapped property.</returns>
    /// <exception cref="InvalidOperationException">There was no property at the given location.</exception>
    public TypeDeclaration GetTypeDeclarationForMappedProperty(string propertyName, string mapName)
    {
        if (this.RefResolvablePropertyDeclarations.TryGetValue(JsonReference.RootFragment.AppendUnencodedPropertyNameToFragment(propertyName).AppendUnencodedPropertyNameToFragment(mapName), out TypeDeclaration? propertyType))
        {
            return propertyType;
        }

        throw new InvalidOperationException($"Unable to get the type declaration map value '{mapName}' in the property '{propertyName}' from the type at {this.LocatedSchema.Location}");
    }

    /// <summary>
    /// Gets the type declaration for the specified mapped property.
    /// </summary>
    /// <param name="propertyName">The name of the map property.</param>
    /// <param name="mapName">The name of the property in the map.</param>
    /// <param name="result">The type declaration for the named mapped property.</param>
    /// <returns><see langword="true"/> if the type declaration was found.</returns>
    /// <exception cref="InvalidOperationException">There was no property at the given location.</exception>
    public bool TryGetTypeDeclarationForMappedProperty(string propertyName, string mapName, [NotNullWhen(true)] out TypeDeclaration? result)
    {
        return this.RefResolvablePropertyDeclarations.TryGetValue(JsonReference.RootFragment.AppendUnencodedPropertyNameToFragment(propertyName).AppendUnencodedPropertyNameToFragment(mapName), out result);
    }

    /// <summary>
    /// Explicitly sets the dotnet type name to a new value.
    /// </summary>
    /// <param name="dotnetTypeName">The new dotnet type name.</param>
    internal void SetDotnetTypeName(string dotnetTypeName)
    {
        this.DotnetTypeName = dotnetTypeName;
    }

    /// <summary>
    /// Explicitly sets the dotnet namespace to a new value.
    /// </summary>
    /// <param name="ns">The new dotnet namespace .</param>
    internal void SetNamespace(string ns)
    {
        this.Namespace = ns;
    }

    /// <summary>
    /// Sets the parent for this type.
    /// </summary>
    internal void SetParent()
    {
        if (this.IsBuiltInType)
        {
            this.Parent = null;
            return;
        }

        JsonReference currentLocation = this.LocatedSchema.Location;
        JsonReferenceBuilder builder = currentLocation.AsBuilder();
        if (builder.HasQuery)
        {
            // We were created in a dynamic scope, so our parent will be that dynamic scope.
            currentLocation = new JsonReference(Uri.UnescapeDataString(builder.Query[(builder.Query.IndexOf('=') + 1)..].ToString()));

            if (this.typeBuilder.TryGetReducedTypeDeclarationFor(currentLocation, out TypeDeclaration? reducedTypeDeclaration))
            {
                if (reducedTypeDeclaration.Equals(this) || reducedTypeDeclaration.IsBuiltInType)
                {
                    this.Parent = null;
                    return;
                }

                this.Parent = reducedTypeDeclaration;
                reducedTypeDeclaration.AddChild(this);
                return;
            }
        }

        while (true)
        {
            if (!currentLocation.HasFragment)
            {
                // We have reached the root of a dynamic scope, and not found anything, so that's that.
                this.Parent = null;
                return;
            }

            currentLocation = StepBackOneFragment(currentLocation);

            if (this.typeBuilder.TryGetReducedTypeDeclarationFor(currentLocation, out TypeDeclaration? reducedTypeDeclaration))
            {
                if (reducedTypeDeclaration.Equals(this) || reducedTypeDeclaration.IsBuiltInType)
                {
                    this.Parent = null;
                    return;
                }

                this.Parent = reducedTypeDeclaration;
                reducedTypeDeclaration.AddChild(this);
                return;
            }
        }

        static JsonReference StepBackOneFragment(JsonReference reference)
        {
            if (!reference.HasFragment)
            {
                return reference;
            }

            ReadOnlySpan<char> fragment = reference.Fragment;
            int lastSlash = fragment.LastIndexOf('/');
            if (lastSlash <= 0)
            {
                return reference.WithFragment(string.Empty);
            }

            if (fragment[lastSlash - 1] == '#')
            {
                lastSlash--;
            }

            if (lastSlash <= 0)
            {
                return reference.WithFragment(string.Empty);
            }

            fragment = fragment[..lastSlash];
            return new JsonReference(reference.Uri, fragment);
        }
    }

    /// <summary>
    /// Updates the located schema associated with this type to present a new dynamic location.
    /// </summary>
    /// <param name="dynamicScopeLocation">The new dynamic scope location.</param>
    internal void UpdateDynamicLocation(JsonReference dynamicScopeLocation)
    {
        JsonReferenceBuilder builder = this.LocatedSchema.Location.AsBuilder();
        builder = new JsonReferenceBuilder(builder.Scheme, builder.Authority, builder.Path, ("dynamicScope=" + Uri.EscapeDataString(dynamicScopeLocation.ToString())).AsSpan(), builder.Fragment);
        this.LocatedSchema = this.LocatedSchema.WithLocation(builder.AsReference());
    }

    /// <summary>
    /// Updates the located schema associated with this type to present a new dynamic location.
    /// </summary>
    /// <param name="recursiveScopeLocation">The new dynamic scope location.</param>
    internal void UpdateRecursiveLocation(JsonReference recursiveScopeLocation)
    {
        JsonReferenceBuilder builder = this.LocatedSchema.Location.AsBuilder();
        builder = new JsonReferenceBuilder(builder.Scheme, builder.Authority, builder.Path, ("dynamicScope=" + Uri.EscapeDataString(recursiveScopeLocation.ToString())).AsSpan(), builder.Fragment);
        this.LocatedSchema = this.LocatedSchema.WithLocation(builder.AsReference());
        this.RecursiveScope = recursiveScopeLocation;
    }

    /// <summary>
    /// Try to get the <c>$corvusTypeName</c> for the type.
    /// </summary>
    /// <param name="typeName">The type name.</param>
    /// <returns><see langword="true"/> if the type had an explicit type name specified by the <c>$corvusTypeName</c> keyword.</returns>
    internal bool TryGetCorvusTypeName([NotNullWhen(true)] out string? typeName)
    {
        if (this.LocatedSchema.Schema.ValueKind == JsonValueKind.Object &&
            this.LocatedSchema.Schema.AsObject.TryGetProperty("$corvusTypeName", out JsonAny value) &&
            value.ValueKind == JsonValueKind.String &&
            value.AsString.TryGetString(out typeName))
        {
            if (typeName.Length > 1)
            {
                return true;
            }
        }

        typeName = null;
        return false;
    }

    /// <summary>
    /// Sets the recursive scope.
    /// </summary>
    /// <param name="recursiveScope">The recursive scope.</param>
    internal void SetRecursiveScope(JsonReference recursiveScope)
    {
        this.RecursiveScope = recursiveScope;
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

    private void AddChild(TypeDeclaration typeDeclaration)
    {
        this.Children = this.Children.Add(typeDeclaration);
    }

    private void MergeProperties(int index, PropertyDeclaration propertyDeclaration)
    {
        PropertyDeclaration? original = this.Properties[index];

        // Merge whether this is a required property with the parent
        PropertyDeclaration propertyToAdd =
            propertyDeclaration.WithRequired(
                propertyDeclaration.IsRequired || original.IsRequired)
            .WithXmlDocumentationRemarks(propertyDeclaration.XmlDocumentationRemarks ?? original.XmlDocumentationRemarks);

        this.Properties = this.Properties.SetItem(index, propertyToAdd);
    }

    private bool CanReduce()
    {
        if (this.LocatedSchema.Schema.ValueKind == JsonValueKind.True || this.LocatedSchema.Schema.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        JsonObject schemaObject = this.LocatedSchema.Schema.AsObject;

        // You can't reduce a type with an ID and a dynamic anchor.
        if (this.LocatedSchema.HasDynamicAnchor() && schemaObject.HasProperty(this.typeBuilder.JsonSchemaConfiguration.IdKeyword))
        {
            return false;
        }

        foreach (JsonObjectProperty property in schemaObject.EnumerateObject())
        {
            if (this.typeBuilder.JsonSchemaConfiguration.IrreducibleKeywords.Contains(property.Name.GetString()))
            {
                return false;
            }
        }

        return true;
    }

    private class PropertyDeclarationEqualityComparer : IEqualityComparer<PropertyDeclaration>
    {
        public static readonly PropertyDeclarationEqualityComparer Instance = new();

        public bool Equals(PropertyDeclaration? x, PropertyDeclaration? y)
        {
            return x is PropertyDeclaration x1 && y is PropertyDeclaration y1 && x1.JsonPropertyName == y1.JsonPropertyName;
        }

        public int GetHashCode(PropertyDeclaration obj)
        {
            return obj.JsonPropertyName.GetHashCode();
        }
    }
}