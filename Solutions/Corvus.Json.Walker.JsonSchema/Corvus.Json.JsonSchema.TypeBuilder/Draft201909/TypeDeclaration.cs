// <copyright file="TypeDeclaration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.JsonSchema.TypeBuilder.Draft201909
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Corvus.Json;
    using Corvus.Json.JsonSchema.Draft201909;

    /// <summary>
    /// A type declaration based on a schema.
    /// </summary>
    public class TypeDeclaration
    {
        private const string BuiltInsLocation = "https://github.com/Corvus-dotnet/Corvus/tree/master/schema/builtins";
        private ImmutableArray<TypeDeclaration> children = ImmutableArray<TypeDeclaration>.Empty;
        private ImmutableArray<PropertyDeclaration> properties = ImmutableArray<PropertyDeclaration>.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeDeclaration"/> class.
        /// </summary>
        /// <param name="absoluteLocation">The canonical location of the type declaration.</param>
        /// <param name="lexicalLocation">The lexical location of the type declaration.</param>
        /// <param name="schema">The schema with which this type declaration is associated.</param>
        public TypeDeclaration(string absoluteLocation, string lexicalLocation, Schema schema)
        {
            this.Location = absoluteLocation;
            this.LexicalLocation = lexicalLocation;
            this.Schema = schema;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeDeclaration"/> class.
        /// </summary>
        /// <param name="location">The canonical location of the type declaration.</param>
        /// <param name="schema">The schema with which this type declaration is associated.</param>
        public TypeDeclaration(string location, Schema schema)
        {
            this.Location = location;
            this.LexicalLocation = location;
            this.Schema = schema;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeDeclaration"/> class for a built-in type.
        /// </summary>
        /// <param name="schema">The schema for the built-in type.</param>
        public TypeDeclaration(Schema schema)
            : this(BuiltInsLocation, schema)
        {
            if (!schema.IsBuiltInType())
            {
                throw new ArgumentException("The schema must be a built-in type.");
            }

            this.SetBuiltInTypeNameAndNamespace();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeDeclaration"/> class for a built-in type.
        /// </summary>
        /// <param name="ns">The namespace for the built-in type.</param>
        /// <param name="typename">The typename for the built-in type.</param>
        public TypeDeclaration(string? ns, string typename)
            : this(BuiltInsLocation, default(Schema))
        {
            this.SetNamespaceAndTypeName(ns, typename);
        }

        /// <summary>
        /// Gets a value indicating whether this is a built-in type.
        /// </summary>
        public bool IsBuiltInType => this.Location == BuiltInsLocation || this.Schema.IsBuiltInType();

        /// <summary>
        /// Gets the canonical location of the type declaration.
        /// </summary>
        public string Location { get; }

        /// <summary>
        /// Gets the lexical location of the type declaration.
        /// </summary>
        public string LexicalLocation { get; }

        /// <summary>
        /// Gets the schema associated with this type declaration.
        /// </summary>
        public Schema Schema { get; }

        /// <summary>
        /// Gets the parent declaration of this type declaration.
        /// </summary>
        public TypeDeclaration? Parent { get; private set; }

        /// <summary>
        /// Gets the set of <see cref="TypeDeclaration"/> instances referenced by this <see cref="TypeDeclaration"/>.
        /// </summary>
        public ImmutableHashSet<TypeDeclaration>? ReferencedTypes { get; private set; }

        /// <summary>
        /// Gets the embedded children of this type.
        /// </summary>
        public ImmutableArray<TypeDeclaration> Children => this.children;

        /// <summary>
        /// Gets the list of property declarations in this type.
        /// </summary>
        public ImmutableArray<PropertyDeclaration> Properties => this.properties;

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
        /// Gets the fully qualified dotnet type name for this type.
        /// </summary>
        public string? FullyQualifiedDotnetTypeName { get; private set; }

        /// <summary>
        /// Sets the parent type declaration.
        /// </summary>
        /// <param name="parent">The parent type declaration.</param>
        public void SetParent(TypeDeclaration parent)
        {
            if (this.Parent is not null)
            {
                if (this.Parent == parent)
                {
                    return;
                }

                this.Parent.RemoveChild(this);
            }

            this.Parent = parent;
            parent.AddChild(this);
        }

        /// <summary>
        /// Calculates a name for the type based on the inforamtion we have.
        /// </summary>
        /// <remarks>
        /// A builder should call <see cref="SetDotnetTypeNameAndNamespace(string, string)"/> across the whole hierarchy before calling set fully qualified name.
        /// </remarks>
        public void SetFullyQualifiedDotnetTypeName()
        {
            if (this.DotnetTypeName is null)
            {
                throw new InvalidOperationException("The dotnet type name must be set before you can set the fully qualified dotnet type name.");
            }

            this.FullyQualifiedDotnetTypeName = this.BuildFullyQualifiedDotnetTypeName(this.DotnetTypeName);
        }

        /// <summary>
        /// Sets a built-in type name and namespace.
        /// </summary>
        public void SetBuiltInTypeNameAndNamespace()
        {
            if (this.Schema.IsBuiltInType())
            {
                string ns;
                string type;

                if (this.Schema.ValueKind == System.Text.Json.JsonValueKind.True)
                {
                    (ns, type) = BuiltInTypes.AnyTypeDeclaration;
                }
                else if (this.Schema.ValueKind == System.Text.Json.JsonValueKind.True || this.Schema.ValueKind == System.Text.Json.JsonValueKind.False)
                {
                    (ns, type) = BuiltInTypes.NotAnyTypeDeclaration;
                }
                else if (this.Schema.IsEmpty())
                {
                    (ns, type) = BuiltInTypes.AnyTypeDeclaration;
                }
                else
                {
                    (ns, type) = BuiltInTypes.GetTypeNameFor(this.Schema.Type.AsSimpleTypesEntity.AsString(), this.Schema.Format, this.Schema.ContentEncoding, this.Schema.ContentMediaType);
                }

                this.SetNamespaceAndTypeName(ns, type);
            }
        }

        /// <summary>
        /// Explicitly sets the dotnet type name to a new value.
        /// </summary>
        /// <param name="dotnetTypeName">The new dotnet type name.</param>
        public void OverrideDotnetTypeName(string dotnetTypeName)
        {
            this.DotnetTypeName = dotnetTypeName;
        }

        /// <summary>
        /// Calculates a name for the type based on the inforamtion we have.
        /// </summary>
        /// <param name="rootNamespace">The namespace to use for this type if it has no parent.</param>
        /// <param name="fallbackBaseName">The base type name to fall back on if we can't derive one from our location and type infomration.</param>
        /// <remarks>
        /// A builder should call <see cref="SetParent(TypeDeclaration)"/> before calling set name.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1009:Closing parenthesis should be spaced correctly", Justification = "Sylecop does not yet support this syntax.")]
        public void SetDotnetTypeNameAndNamespace(string rootNamespace, string fallbackBaseName)
        {
            var reference = JsonReferenceBuilder.From(this.Location);

            if (this.Parent is null)
            {
                if (reference.HasFragment)
                {
                    int lastSlash = reference.Fragment.LastIndexOf('/');
                    ReadOnlySpan<char> dnt = Formatting.ToPascalCaseWithReservedWords(reference.Fragment[(lastSlash + 1)..].ToString());
                    this.DotnetTypeName = dnt.ToString();
                }
                else if (reference.HasPath)
                {
                    int lastSlash = reference.Path.LastIndexOf('/');
                    if (lastSlash == reference.Path.Length - 1 && lastSlash > 0)
                    {
                        lastSlash = reference.Path[.. (lastSlash - 1)].LastIndexOf('/');
                        ReadOnlySpan<char> dnt = Formatting.ToPascalCaseWithReservedWords(reference.Path[(lastSlash + 1)..].ToString());
                        this.DotnetTypeName = dnt.ToString();
                    }
                    else if (lastSlash == reference.Path.Length - 1)
                    {
                        ReadOnlySpan<char> dnt = fallbackBaseName;
                        this.DotnetTypeName = dnt.ToString();
                    }
                    else
                    {
                        ReadOnlySpan<char> dnt = Formatting.ToPascalCaseWithReservedWords(reference.Path[(lastSlash + 1)..].ToString());
                        this.DotnetTypeName = dnt.ToString();
                    }
                }
                else
                {
                    ReadOnlySpan<char> dnt = fallbackBaseName;
                    this.DotnetTypeName = dnt.ToString();
                }

                this.Namespace = rootNamespace;
            }
            else
            {
                ReadOnlySpan<char> typename;

                if (reference.HasFragment)
                {
                    int lastSlash = reference.Fragment.LastIndexOf('/');
                    if (char.IsDigit(reference.Fragment[lastSlash + 1]) && lastSlash > 0)
                    {
                        int previousSlash = reference.Fragment[.. (lastSlash - 1)].LastIndexOf('/');
                        if (previousSlash >= 0)
                        {
                            lastSlash = previousSlash;
                        }

                        typename = Formatting.ToPascalCaseWithReservedWords(reference.Fragment[(lastSlash + 1)..].ToString());
                    }
                    else if (reference.Fragment[(lastSlash + 1)..].SequenceEqual("items") && lastSlash > 0)
                    {
                        int previousSlash = reference.Fragment[.. (lastSlash - 1)].LastIndexOf('/');
                        typename = Formatting.ToPascalCaseWithReservedWords(reference.Fragment[(previousSlash + 1) .. lastSlash].ToString());
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
                        lastSlash = reference.Path[.. (lastSlash - 1)].LastIndexOf('/');
                    }

                    if (char.IsDigit(reference.Path[lastSlash + 1]))
                    {
                        int previousSlash = reference.Path[.. (lastSlash - 1)].LastIndexOf('/');
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

                if (this.Schema.IsExplicitArrayType())
                {
                    Span<char> dnt = stackalloc char[typename.Length + 5];
                    typename.CopyTo(dnt);
                    "Array".AsSpan().CopyTo(dnt[typename.Length..]);
                    this.DotnetTypeName = dnt.ToString();
                }
                else if (this.Schema.IsSimpleType())
                {
                    Span<char> dnt = stackalloc char[typename.Length + 5];
                    typename.CopyTo(dnt);
                    "Value".AsSpan().CopyTo(dnt[typename.Length..]);
                    this.DotnetTypeName = dnt.ToString();
                }
                else
                {
                    Span<char> dnt = stackalloc char[typename.Length + 6];
                    typename.CopyTo(dnt);
                    "Entity".AsSpan().CopyTo(dnt[typename.Length..]);
                    this.DotnetTypeName = dnt.ToString();
                }
            }
        }

        /// <summary>
        /// Sets the referenced types.
        /// </summary>
        /// <param name="referencedTypes">The referenced types.</param>
        public void SetReferencedTypes(HashSet<TypeDeclaration> referencedTypes)
        {
            this.ReferencedTypes = referencedTypes.ToImmutableHashSet();
        }

        /// <summary>
        /// Adds a property to the collection, replacing any that match the json property name.
        /// </summary>
        /// <param name="propertyDeclaration">The property declaration to add.</param>
        /// <remarks>Note that this will happily add duplicate properties.</remarks>
        public void AddOrReplaceProperty(PropertyDeclaration propertyDeclaration)
        {
            int index = this.properties.IndexOf(propertyDeclaration, PropertyDeclarationEqualityComparer.Instance);
            if (index >= 0)
            {
                this.properties = this.properties.SetItem(index, propertyDeclaration);
            }
            else
            {
                this.properties = this.properties.Add(propertyDeclaration);
            }
        }

        /// <summary>
        /// Normalizes the property set, ensuring we don't have any duplicate types or names.
        /// </summary>
        public void SetDotnetPropertyNames()
        {
            var existingNames = new HashSet<string>
            {
                this.DotnetTypeName!,
            };

            if (this.Schema.Enum.IsNotUndefined())
            {
                existingNames.Add("EnumValues");
            }

            foreach (PropertyDeclaration property in this.properties)
            {
                MapDotnetPropertyNameForProperty(existingNames, property);
            }

            static void MapDotnetPropertyNameForProperty(HashSet<string> existingNames, PropertyDeclaration property)
            {
                ReadOnlySpan<char> baseName = Formatting.ToPascalCaseWithReservedWords(property.JsonPropertyName);
                Span<char> name = stackalloc char[baseName.Length + 3];
                baseName.CopyTo(name);
                int suffixLength = 0;
                int index = 1;
                string nameString = name[.. (baseName.Length + suffixLength)].ToString();
                while (existingNames.Contains(nameString))
                {
                    index.TryFormat(name[baseName.Length..], out suffixLength);
                    index++;
                    nameString = name[.. (baseName.Length + suffixLength)].ToString();
                }

                existingNames.Add(nameString);
                property.DotnetPropertyName = nameString;
            }
        }

        private string BuildFullyQualifiedDotnetTypeName(string dotnetTypeName)
        {
            var nameSegments = new List<string>
                {
                    dotnetTypeName,
                };

            TypeDeclaration rootParent = this;
            TypeDeclaration? parent = this.Parent;
            while (parent is TypeDeclaration p)
            {
                nameSegments.Insert(0, p.DotnetTypeName!);
                rootParent = parent;
                parent = parent.Parent;
            }

            if (rootParent.Namespace is not null)
            {
                nameSegments.Insert(0, rootParent.Namespace);
            }

            return string.Join('.', nameSegments);
        }

        private void AddChild(TypeDeclaration typeDeclaration)
        {
            this.children = this.children.Add(typeDeclaration);
        }

        private void RemoveChild(TypeDeclaration typeDeclaration)
        {
            this.children = this.children.Remove(typeDeclaration);
        }

        private void SetNamespaceAndTypeName(string? ns, string type)
        {
            this.DotnetTypeName = type;

            if (ns is not null)
            {
                this.FullyQualifiedDotnetTypeName = $"{ns}.{type}";
            }
            else
            {
                this.FullyQualifiedDotnetTypeName = type;
            }

            this.Namespace = ns;
        }

        private class PropertyDeclarationEqualityComparer : IEqualityComparer<PropertyDeclaration>
        {
            public static readonly PropertyDeclarationEqualityComparer Instance = new ();

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
}
