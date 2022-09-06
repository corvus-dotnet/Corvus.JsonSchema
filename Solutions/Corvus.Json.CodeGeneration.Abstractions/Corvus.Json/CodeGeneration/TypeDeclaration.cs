// <copyright file="TypeDeclaration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A type declaration for a Json Schema.
/// </summary>
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
    public LocatedSchema LocatedSchema { get; }

    /// <summary>
    /// Gets the dotnet property declarations for the type.
    /// </summary>
    public ImmutableArray<PropertyDeclaration> Properties { get; private set; } = ImmutableArray<PropertyDeclaration>.Empty;

    /// <summary>
    /// Gets the ref-resolvable property declarations for the type declaration.
    /// </summary>
    public ImmutableDictionary<string, TypeDeclaration> RefResolvablePropertyDeclarations { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the type declaration has a dynamic reference.
    /// </summary>
    public bool HasDynamicReference => this.RefResolvablePropertyDeclarations.Any(r => this.typeBuilder.RefKeywords.Any(k => (k.RefKind == RefKind.DynamicRef || k.RefKind == RefKind.RecursiveRef) && new JsonReference("#").AppendUnencodedPropertyNameToFragment(k.Name) == r.Key));

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
    public ImmutableHashSet<TypeDeclaration> Children { get; private set; } = ImmutableHashSet<TypeDeclaration>.Empty;

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
    /// This will include all the properties for <see cref="JsonSchemaTypeBuilder.RefResolvableKeywords"/>, and for irreducible reference
    /// properties.
    /// </para>
    /// </remarks>
    public void AddRefResolvablePropertyDeclaration(string propertyPath, TypeDeclaration type)
    {
        this.RefResolvablePropertyDeclarations = this.RefResolvablePropertyDeclarations.Add(propertyPath, type);
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

        foreach (RefKeyword refKeyword in this.typeBuilder.RefKeywords)
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
                lastSlash -= 1;
            }

            if (lastSlash <= 0)
            {
                return reference.WithFragment(string.Empty);
            }

            fragment = fragment[..lastSlash];
            return new JsonReference(reference.Uri, fragment);
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
                propertyDeclaration.IsRequired || original.IsRequired);

        this.Properties = this.Properties.SetItem(index, propertyToAdd);
    }

    private bool CanReduce()
    {
        if (this.LocatedSchema.Schema.ValueKind == JsonValueKind.True || this.LocatedSchema.Schema.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        foreach (JsonObjectProperty property in this.LocatedSchema.Schema.EnumerateObject())
        {
            if (this.typeBuilder.IrreducibleKeywords.Contains(property.Name))
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