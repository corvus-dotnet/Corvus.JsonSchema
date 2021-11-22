// <copyright file="JsonSchemaBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.SchemaModel.Draft202012
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Corvus.Json;
    using Corvus.Json.Draft202012;

    /// <summary>
    /// A JSON schema type builder.
    /// </summary>
    public class JsonSchemaBuilder : IJsonSchemaBuilder
    {
        private readonly HashSet<TypeDeclaration> typeDeclarations = new ();
        private readonly Dictionary<string, TypeDeclaration> locatedTypeDeclarations = new ();
        private readonly JsonWalker walker;
        private readonly Dictionary<string, TypeDeclaration> dynamicAnchors = new ();

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSchemaBuilder"/> class.
        /// </summary>
        /// <param name="walker">The JsonWalker to use.</param>
        public JsonSchemaBuilder(JsonWalker walker)
        {
            this.walker = walker;
            var jsonSchemaWalker = new JsonSchemaWalker();
            jsonSchemaWalker.RegisterWith(this.walker);
        }

        /// <inheritdoc/>
        public async Task<string> RebaseReferenceAsRootDocument(string reference)
        {
            return await this.walker.RebaseReferenceAsRootDocument(reference);
        }

        /// <inheritdoc/>
        public async Task<(string rootType, ImmutableDictionary<string, (string, string)> generatedTypes)> BuildTypesFor(string reference, string rootNamespace, bool rebase = false, Dictionary<string, string>? baseUriToNamespaceMap = null, string? rootTypeName = null)
        {
            // First, we resolve the reference and locate our root element.
            LocatedElement? rootElement = await this.walker.ResolveReference(new JsonReference(reference), false, false).ConfigureAwait(false);

            await this.walker.ResolveUnresolvedReferences();

            if (rootElement is null)
            {
                throw new ArgumentException($"Unable to find a schema at location: {reference}", nameof(reference));
            }

            // Then, we build type instances for all the elements we have found
            string rootLocation = this.BuildTypeDeclarations(rootElement);

            // Then we prune the types we aren't actually using
            Dictionary<string, TypeDeclaration> referencedTypesByLocation = this.ResolveDynamicAndUnreferencedTypes(rootLocation);

            // Then prune the built-in types (setting their dotnet type names appropriately)
            Dictionary<string, TypeDeclaration> typesForGenerationByLocation = PruneBuiltInTypes(referencedTypesByLocation);

            // Now we are going to populate the types we have built with information that cannot be derived locally from the schema.
            // And set the parents for the remaining types that we are going to generate.
            this.SetParents(typesForGenerationByLocation);

            // Once the parents are set, we can build names for our types.
            this.SetNames(typesForGenerationByLocation, rootNamespace, baseUriToNamespaceMap, rootTypeName, rootLocation);

            // Now, find and add all our properties.
            this.FindProperties(typesForGenerationByLocation);

            rootTypeName = this.locatedTypeDeclarations[rootLocation].FullyQualifiedDotnetTypeName!;
            return (
                rootTypeName,
                typesForGenerationByLocation.Where(t => t.Value.Parent is null).Select(
                    typeForGeneration =>
                    {
                        var template = new SchemaEntity202012(this, typeForGeneration.Value);
                        return (typeForGeneration.Key, (typeForGeneration.Value.DotnetTypeName!, template.TransformText()));
                    }).ToImmutableDictionary(i => i.Key, i => i.Item2));
        }

        /// <summary>
        /// Gets the type declaration for a property of a type.
        /// </summary>
        /// <param name="typeDeclaration">The type declaration.</param>
        /// <param name="property">The property that provides a schema.</param>
        /// <returns>The given type declaration.</returns>
        internal TypeDeclaration GetTypeDeclarationForProperty(TypeDeclaration typeDeclaration, string property)
        {
            return this.GetTypeDeclarationForProperty(typeDeclaration.Location, typeDeclaration.LexicalLocation, property);
        }

        /// <summary>
        /// Gets the type declaration for a property of a type.
        /// </summary>
        /// <param name="typeDeclaration">The type declaration.</param>
        /// <param name="patternProperty">The pattern property that provides a schema.</param>
        /// <returns>The given type declaration.</returns>
        internal TypeDeclaration GetTypeDeclarationForPatternProperty(TypeDeclaration typeDeclaration, string patternProperty)
        {
            return this.GetTypeDeclarationForProperty(new JsonReference(typeDeclaration.Location).AppendUnencodedPropertyNameToFragment("patternProperties"), new JsonReference(typeDeclaration.LexicalLocation).AppendUnencodedPropertyNameToFragment("patternProperties"), patternProperty);
        }

        /// <summary>
        /// Gets the type declaration for a dependent of a type.
        /// </summary>
        /// <param name="typeDeclaration">The type declaration.</param>
        /// <param name="dependentSchema">The dependent schema that provides a schema.</param>
        /// <returns>The given type declaration.</returns>
        internal TypeDeclaration GetTypeDeclarationForDependentSchema(TypeDeclaration typeDeclaration, string dependentSchema)
        {
            return this.GetTypeDeclarationForProperty(new JsonReference(typeDeclaration.Location).AppendUnencodedPropertyNameToFragment("dependentSchemas"), new JsonReference(typeDeclaration.LexicalLocation).AppendUnencodedPropertyNameToFragment("dependentSchemas"), dependentSchema);
        }

        /// <summary>
        /// Gets the type declaration for a schema array property at a given index.
        /// </summary>
        /// <param name="typeDeclaration">The type declaration.</param>
        /// <param name="property">The property that provides a schema.</param>
        /// <param name="index">The index of the schema in the array.</param>
        /// <returns>The given type declaration.</returns>
        internal TypeDeclaration GetTypeDeclarationForPropertyArrayIndex(TypeDeclaration typeDeclaration, string property, int index)
        {
            return this.GetTypeDeclarationForPropertyArrayIndex(typeDeclaration.Location, property, index);
        }

        private static void AddTypeDeclarationsToReferencedTypes(HashSet<TypeDeclaration> referencedTypes, TypeDeclaration typeDeclaration)
        {
            if (!referencedTypes.Contains(typeDeclaration))
            {
                referencedTypes.Add(typeDeclaration);
            }
        }

        private static Dictionary<string, TypeDeclaration> PruneBuiltInTypes(Dictionary<string, TypeDeclaration> referencedTypesByLocation)
        {
            var pruned = new Dictionary<string, TypeDeclaration>();

            foreach (KeyValuePair<string, TypeDeclaration> item in referencedTypesByLocation)
            {
                if (!item.Value.Schema.IsBuiltInType())
                {
                    pruned.Add(item.Key, item.Value);
                }
                else
                {
                    item.Value.SetBuiltInTypeNameAndNamespace();
                }
            }

            return pruned;
        }

        private static void FixNameForChildren(TypeDeclaration type)
        {
            if (type.Parent is TypeDeclaration parent)
            {
                int index = 1;
                while (parent.DotnetTypeName == type.DotnetTypeName || parent.Children.Any(c => c != type && c.DotnetTypeName == type.DotnetTypeName))
                {
                    string trimmedString = type.DotnetTypeName!.Trim('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                    string newName = $"{trimmedString}{index}";
                    type.OverrideDotnetTypeName(newName);
                    index++;
                }
            }
        }

        private Dictionary<string, TypeDeclaration> ResolveDynamicAndUnreferencedTypes(string rootLocation)
        {
            TypeDeclaration root = this.locatedTypeDeclarations[rootLocation];
            var referencedTypes = new HashSet<TypeDeclaration>();
            this.FindAndBuildReferencedTypes(root, referencedTypes);
            referencedTypes.Add(root);
            return referencedTypes.ToDictionary(k => k.LexicalLocation);
        }

        private string BuildTypeDeclarations(LocatedElement rootElement)
        {
            string result = string.Empty;

            foreach (LocatedElement element in this.walker.EnumerateLocatedElements())
            {
                if (element.ContentType == JsonSchemaWalker.SchemaContent && new JsonReference(element.AbsoluteLocation).HasAbsoluteUri)
                {
                    TypeDeclaration declaration = this.BuildTypeDeclarationFor(element);
                    if (element.AbsoluteLocation == rootElement.AbsoluteLocation)
                    {
                        result = declaration.Location;
                    }
                }
            }

            return result;
        }

        private void FindAndBuildReferencedTypes(TypeDeclaration currentDeclaration, HashSet<TypeDeclaration> referencedTypes)
        {
            var localTypes = new HashSet<TypeDeclaration>();

            this.FindAndBuildReferencedTypesCore(currentDeclaration, referencedTypes, localTypes);
            currentDeclaration.SetReferencedTypes(localTypes);

            var inspectedTypes = localTypes.ToHashSet();

            var currentTypes = localTypes.ToList();

            while (currentTypes.Any())
            {
                localTypes.Clear();
                foreach (TypeDeclaration type in currentTypes)
                {
                    inspectedTypes.Add(type);
                    this.FindAndBuildReferencedTypesCore(type, referencedTypes, localTypes);
                }

                currentTypes = localTypes.Except(inspectedTypes).ToList();
            }
        }

        private void FindAndBuildReferencedTypesCore(TypeDeclaration currentDeclaration, HashSet<TypeDeclaration> referencedTypes, HashSet<TypeDeclaration> localTypes)
        {
            if (currentDeclaration.Schema.AdditionalProperties.IsNotUndefined())
            {
                TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration.Location, currentDeclaration.LexicalLocation, "additionalProperties");
                AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                localTypes.Add(typeDeclaration);
            }

            if (currentDeclaration.Schema.AllOf.IsNotUndefined())
            {
                int index = 0;
                foreach (Schema schema in currentDeclaration.Schema.AllOf.EnumerateArray())
                {
                    TypeDeclaration typeDeclaration = this.GetTypeDeclarationForPropertyArrayIndex(currentDeclaration.Location, "allOf", index);
                    AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                    localTypes.Add(typeDeclaration);
                    index++;
                }
            }

            if (currentDeclaration.Schema.AnyOf.IsNotUndefined())
            {
                int index = 0;
                foreach (Schema schema in currentDeclaration.Schema.AnyOf.EnumerateArray())
                {
                    TypeDeclaration typeDeclaration = this.GetTypeDeclarationForPropertyArrayIndex(currentDeclaration.Location, "anyOf", index);
                    AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                    localTypes.Add(typeDeclaration);
                    index++;
                }
            }

            if (currentDeclaration.Schema.Contains.IsNotUndefined())
            {
                TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration.Location, currentDeclaration.LexicalLocation, "contains");
                AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                localTypes.Add(typeDeclaration);
            }

            if (currentDeclaration.Schema.ContentSchema.IsNotUndefined())
            {
                TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration.Location, currentDeclaration.LexicalLocation, "contentSchema");
                AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                localTypes.Add(typeDeclaration);
            }

            if (currentDeclaration.Schema.DependentSchemas.IsNotUndefined())
            {
                foreach (Property<Schema> schemaProperty in currentDeclaration.Schema.DependentSchemas.EnumerateProperties())
                {
                    TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(new JsonReference(currentDeclaration.Location).AppendUnencodedPropertyNameToFragment("dependentSchemas"), new JsonReference(currentDeclaration.LexicalLocation).AppendUnencodedPropertyNameToFragment("dependentSchemas"), schemaProperty.Name);
                    AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                    localTypes.Add(typeDeclaration);
                }
            }

            if (currentDeclaration.Schema.Else.IsNotUndefined())
            {
                TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration.Location, currentDeclaration.LexicalLocation, "else");
                AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                localTypes.Add(typeDeclaration);
            }

            if (currentDeclaration.Schema.If.IsNotUndefined())
            {
                TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration.Location, currentDeclaration.LexicalLocation, "if");
                AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                localTypes.Add(typeDeclaration);
            }

            if (currentDeclaration.Schema.Items.IsNotUndefined())
            {
                TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration.Location, currentDeclaration.LexicalLocation, "items");
                AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                localTypes.Add(typeDeclaration);
            }

            if (currentDeclaration.Schema.PrefixItems.IsNotUndefined())
            {
                int index = 0;
                foreach (Schema schema in currentDeclaration.Schema.PrefixItems.EnumerateItems())
                {
                    TypeDeclaration typeDeclaration = this.GetTypeDeclarationForPropertyArrayIndex(currentDeclaration.Location, "prefixItems", index);
                    AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                    localTypes.Add(typeDeclaration);
                    index++;
                }
            }

            if (currentDeclaration.Schema.Not.IsNotUndefined())
            {
                TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration.Location, currentDeclaration.LexicalLocation, "not");
                AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                localTypes.Add(typeDeclaration);
            }

            if (currentDeclaration.Schema.OneOf.IsNotUndefined())
            {
                int index = 0;
                foreach (Schema schema in currentDeclaration.Schema.OneOf.EnumerateArray())
                {
                    TypeDeclaration typeDeclaration = this.GetTypeDeclarationForPropertyArrayIndex(currentDeclaration.Location, "oneOf", index);
                    AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                    localTypes.Add(typeDeclaration);
                    index++;
                }
            }

            if (currentDeclaration.Schema.PatternProperties.IsNotUndefined())
            {
                foreach (Property<Schema> schemaProperty in currentDeclaration.Schema.PatternProperties.EnumerateProperties())
                {
                    TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(new JsonReference(currentDeclaration.Location).AppendUnencodedPropertyNameToFragment("patternProperties"), new JsonReference(currentDeclaration.LexicalLocation).AppendUnencodedPropertyNameToFragment("patternProperties"), schemaProperty.Name);
                    AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                    localTypes.Add(typeDeclaration);
                }
            }

            if (currentDeclaration.Schema.Properties.IsNotUndefined())
            {
                foreach (Property<Schema> schemaProperty in currentDeclaration.Schema.Properties.EnumerateProperties())
                {
                    if (this.TryGetTypeDeclarationForProperty(new JsonReference(currentDeclaration.Location).AppendUnencodedPropertyNameToFragment("properties"), new JsonReference(currentDeclaration.LexicalLocation).AppendUnencodedPropertyNameToFragment("properties"), schemaProperty.Name, out TypeDeclaration? typeDeclaration))
                    {
                        AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                        localTypes.Add(typeDeclaration);
                    }
                }
            }

            if (currentDeclaration.Schema.PropertyNames.IsNotUndefined())
            {
                TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration.Location, currentDeclaration.LexicalLocation, "propertyNames");
                AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                localTypes.Add(typeDeclaration);
            }

            if (currentDeclaration.Schema.RecursiveRef.IsNotUndefined())
            {
                // If we have a recursive ref, this is being applied in place; naked refs have already been reduced.
                TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration.Location, currentDeclaration.LexicalLocation, "$recursiveRef");
                AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                localTypes.Add(typeDeclaration);
            }

            if (currentDeclaration.Schema.Ref.IsNotUndefined())
            {
                TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration.Location, currentDeclaration.LexicalLocation, "$ref");
                AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                localTypes.Add(typeDeclaration);
            }

            if (currentDeclaration.Schema.Then.IsNotUndefined())
            {
                TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration.Location, currentDeclaration.LexicalLocation, "then");
                AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                localTypes.Add(typeDeclaration);
            }

            if (currentDeclaration.Schema.UnevaluatedItems.IsNotUndefined())
            {
                TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration.Location, currentDeclaration.LexicalLocation, "unevaluatedItems");
                AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                localTypes.Add(typeDeclaration);
            }

            if (currentDeclaration.Schema.UnevaluatedProperties.IsNotUndefined())
            {
                TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration.Location, currentDeclaration.LexicalLocation, "unevaluatedProperties");
                AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                localTypes.Add(typeDeclaration);
            }
        }

        private TypeDeclaration BuildTypeDeclarationFor(LocatedElement rootElement)
        {
            return this.BuildTypeDeclarationFor(rootElement.AbsoluteLocation, new Schema(rootElement.Element));
        }

        /// <summary>
        /// Build the type declaration for the given schema.
        /// </summary>
        private TypeDeclaration BuildTypeDeclarationFor(string location, Schema draft201909Schema)
        {
            if (this.locatedTypeDeclarations.TryGetValue(location, out TypeDeclaration? existingDeclaration))
            {
                return existingDeclaration;
            }

            if (!draft201909Schema.Validate(ValidationContext.ValidContext).IsValid)
            {
                throw new InvalidOperationException("Unable to build types for an invalid schema.");
            }

            if (this.TryReduceSchema(location, draft201909Schema, out TypeDeclaration? reducedTypeDeclaration))
            {
                this.locatedTypeDeclarations.Add(location, reducedTypeDeclaration);
                return reducedTypeDeclaration;
            }

            // Check to see that we haven't located a type declaration during reduction.
            if (this.locatedTypeDeclarations.TryGetValue(location, out TypeDeclaration? builtDuringReduction))
            {
                return builtDuringReduction;
            }

            // If not, then add ourselves into the collection
            var result = new TypeDeclaration(location, draft201909Schema);
            this.typeDeclarations.Add(result);
            this.locatedTypeDeclarations.Add(location, result);

            return result;
        }

        private bool TryReduceSchema(string absoluteLocation, Schema draft201909Schema, [NotNullWhen(true)] out TypeDeclaration? reducedTypeDeclaration)
        {
            if (draft201909Schema.IsNakedReference())
            {
                return this.ReduceSchema(absoluteLocation, out reducedTypeDeclaration, "$ref");
            }

            if (draft201909Schema.IsNakedRecursiveReference())
            {
                return this.ReduceSchema(absoluteLocation, out reducedTypeDeclaration, "$recursiveRef");
            }

            if (draft201909Schema.IsNakedDynamicReference())
            {
                return this.ReduceSchema(absoluteLocation, out reducedTypeDeclaration, "$dynamicRef");
            }

            reducedTypeDeclaration = default;
            return false;
        }

        private bool ReduceSchema(string absoluteLocation, out TypeDeclaration? reducedTypeDeclaration, string referenceProperty)
        {
            JsonReference currentLocation = new JsonReference(absoluteLocation).AppendUnencodedPropertyNameToFragment(referenceProperty);
            if (this.locatedTypeDeclarations.TryGetValue(currentLocation, out TypeDeclaration? locatedTypeDeclaration))
            {
                reducedTypeDeclaration = locatedTypeDeclaration;
                return true;
            }

            LocatedElement? locatedElement = this.walker.GetLocatedElement(currentLocation.ToString());
            reducedTypeDeclaration = this.BuildTypeDeclarationFor(locatedElement);
            return true;
        }

        private TypeDeclaration GetTypeDeclarationForPropertyArrayIndex(string location, string propertyName, int arrayIndex)
        {
            JsonReference schemaLocation = new JsonReference(location).AppendUnencodedPropertyNameToFragment(propertyName).AppendArrayIndexToFragment(arrayIndex);
            string resolvedSchemaLocation = this.walker.GetLocatedElement(schemaLocation.ToString()).AbsoluteLocation;
            if (this.locatedTypeDeclarations.TryGetValue(resolvedSchemaLocation, out TypeDeclaration? typeDeclaration))
            {
                return typeDeclaration;
            }

            return BuiltInTypes.AnyTypeDeclarationInstance;
        }

        private TypeDeclaration GetTypeDeclarationForProperty(string location, string lexicalLocation, string propertyName)
        {
            if (this.TryGetTypeDeclarationForProperty(location, lexicalLocation, propertyName, out TypeDeclaration? typeDeclaration))
            {
                return typeDeclaration;
            }

            return BuiltInTypes.AnyTypeDeclarationInstance;
        }

        private bool TryGetTypeDeclarationForProperty(string location, string lexicalLocation, string propertyName, [NotNullWhen(true)] out TypeDeclaration? typeDeclaration)
        {
            JsonReference schemaLocation = new JsonReference(location).AppendUnencodedPropertyNameToFragment(propertyName);
            string resolvedSchemaLocation = this.walker.GetLocatedElement(schemaLocation.ToString()).AbsoluteLocation;

            if (this.locatedTypeDeclarations.TryGetValue(resolvedSchemaLocation, out typeDeclaration))
            {
                // Figure out if we are in an unresolved dynamicReference situation due to a dynamic anchor
                if (location != lexicalLocation &&
                    typeDeclaration.Schema.DynamicAnchor.IsNotNullOrUndefined() &&
                    this.dynamicAnchors.TryGetValue(new JsonReference(lexicalLocation).WithFragment(typeDeclaration.Schema.DynamicAnchor).ToString(), out TypeDeclaration? dynamicAnchoredTypeDeclaration))
                {
                    typeDeclaration = dynamicAnchoredTypeDeclaration;
                }
                else if (resolvedSchemaLocation != schemaLocation &&
                    this.locatedTypeDeclarations.TryGetValue(location, out TypeDeclaration? sourceDeclaration) &&
                    sourceDeclaration.Schema.DynamicAnchor.IsNotNullOrUndefined() &&
                    sourceDeclaration.Schema.DynamicAnchor.Equals(typeDeclaration.Schema.DynamicAnchor))
                {
                    // This is a dynamic anchor, so don't go for the original one, figure out if we can get one for
                    // this location instead; we build an artificial key
                    if (this.locatedTypeDeclarations.TryGetValue(schemaLocation, out TypeDeclaration? dynamicTypeDeclaration))
                    {
                        typeDeclaration = dynamicTypeDeclaration;
                    }
                    else
                    {
                        typeDeclaration = new TypeDeclaration(resolvedSchemaLocation, schemaLocation, typeDeclaration.Schema);
                        this.locatedTypeDeclarations.Add(schemaLocation, typeDeclaration);
                        this.dynamicAnchors.Add(new JsonReference(location).WithFragment(sourceDeclaration.Schema.DynamicAnchor), sourceDeclaration);
                    }
                }

                return true;
            }

            typeDeclaration = default;
            return false;
        }

        private void SetParents(Dictionary<string, TypeDeclaration> referencedTypesByLocation)
        {
            foreach (TypeDeclaration typeDeclaration in referencedTypesByLocation.Values)
            {
                FindAndSetParent(referencedTypesByLocation, typeDeclaration);
            }

            static void FindAndSetParent(Dictionary<string, TypeDeclaration> referencedTypesByLocation, TypeDeclaration typeDeclaration)
            {
                ReadOnlySpan<char> location = typeDeclaration.LexicalLocation.AsSpan();
                bool isItemsArray = location.EndsWith("items");

                while (true)
                {
                    int lastSlash = location.LastIndexOf('/');
                    if (lastSlash <= 0)
                    {
                        break;
                    }

                    if (location[lastSlash - 1] == '#')
                    {
                        lastSlash -= 1;
                    }

                    if (lastSlash <= 0)
                    {
                        break;
                    }

                    location = location[..lastSlash];
                    if (referencedTypesByLocation.TryGetValue(location.ToString(), out TypeDeclaration? parent) && parent != typeDeclaration)
                    {
                        // Walk back up to the parent type for the items type, if available.
                        if (isItemsArray && parent.Parent is TypeDeclaration p)
                        {
                            parent = p;
                        }

                        typeDeclaration.SetParent(parent);
                        break;
                    }
                }
            }
        }

        private void FindProperties(Dictionary<string, TypeDeclaration> typesForGenerationByLocation)
        {
            // Find all the properties in everything
            foreach (TypeDeclaration type in typesForGenerationByLocation.Values)
            {
                var typesVisited = new HashSet<string>();
                this.AddPropertiesFromType(type, type, typesVisited);
            }

            // Once we've got the whole set, set the dotnet property names.
            foreach (TypeDeclaration type in typesForGenerationByLocation.Values)
            {
                type.SetDotnetPropertyNames();
            }
        }

        private void AddPropertiesFromType(TypeDeclaration source, TypeDeclaration target, HashSet<string> typesVisited)
        {
            if (typesVisited.Contains(source.Location))
            {
                return;
            }

            typesVisited.Add(source.Location);

            // First we add the 'required' properties as JsonAny; they will be overridden if we have explicit implementations
            // elsewhere
            if (source.Schema.Required.IsNotUndefined())
            {
                foreach (JsonString requiredName in source.Schema.Required.EnumerateItems())
                {
                    target.AddOrReplaceProperty(new PropertyDeclaration(BuiltInTypes.AnyTypeDeclarationInstance, requiredName!, true, source.Location == target.Location));
                }
            }

            if (source.Schema.AllOf.IsNotUndefined())
            {
                int index = 0;
                foreach (Schema schema in source.Schema.AllOf.EnumerateArray())
                {
                    TypeDeclaration allOfTypeDeclaration = this.GetTypeDeclarationForPropertyArrayIndex(source.Location, "allOf", index);
                    this.AddPropertiesFromType(allOfTypeDeclaration, target, typesVisited);
                    index++;
                }
            }

            if (source.Schema.Ref.IsNotUndefined() && !source.Schema.IsNakedReference())
            {
                TypeDeclaration refTypeDeclaration = this.GetTypeDeclarationForProperty(source.Location, source.LexicalLocation, "$ref");
                this.AddPropertiesFromType(refTypeDeclaration, target, typesVisited);
            }

            if (source.Schema.RecursiveRef.IsNotUndefined() && !source.Schema.IsNakedRecursiveReference())
            {
                TypeDeclaration refTypeDeclaration = this.GetTypeDeclarationForProperty(source.Location, source.LexicalLocation, "$recursiveRef");
                this.AddPropertiesFromType(refTypeDeclaration, target, typesVisited);
            }

            if (source.Schema.DynamicRef.IsNotUndefined() && !source.Schema.IsNakedDynamicReference())
            {
                TypeDeclaration refTypeDeclaration = this.GetTypeDeclarationForProperty(source.Location, source.LexicalLocation, "$dynamicRef");
                this.AddPropertiesFromType(refTypeDeclaration, target, typesVisited);
            }

            // Then we add our own properties.
            if (source.Schema.Properties.IsNotUndefined())
            {
                foreach (Property<Schema> property in source.Schema.Properties.EnumerateProperties())
                {
                    string propertyName = property.Name;
                    bool isRequired = false;

                    if (source.Schema.Required.IsNotUndefined())
                    {
                        if (source.Schema.Required.EnumerateItems().Any(r => propertyName == r.GetString()))
                        {
                            isRequired = true;
                        }
                    }

                    TypeDeclaration propertyTypeDeclaration = this.GetTypeDeclarationForProperty(new JsonReference(source.Location).AppendUnencodedPropertyNameToFragment("properties"), new JsonReference(source.LexicalLocation).AppendUnencodedPropertyNameToFragment("properties"), propertyName);
                    target.AddOrReplaceProperty(new PropertyDeclaration(propertyTypeDeclaration, propertyName, isRequired, source.Location == target.Location));
                }
            }
        }

        private void SetNames(Dictionary<string, TypeDeclaration> typesForGenerationByLocation, string rootNamespace, Dictionary<string, string>? baseUriToNamespaceMap, string? rootTypeName, string rootLocation)
        {
            foreach (TypeDeclaration type in typesForGenerationByLocation.Values.Where(t => t.Parent is null))
            {
                this.RecursivelySetName(type, rootNamespace, baseUriToNamespaceMap, rootTypeName, rootLocation);
            }

            // Now we've named everything once, we can fix up our array names to better reflect the types of the items
            foreach (TypeDeclaration type in typesForGenerationByLocation.Values.Where(t => t.Parent is null))
            {
                this.RecursivelyFixArrayName(type);
            }

            // Once we've set all the base names, and namespaces, we can set the fully qualified names.
            foreach (TypeDeclaration type in typesForGenerationByLocation.Values)
            {
                type.SetFullyQualifiedDotnetTypeName();
            }
        }

        private void RecursivelyFixArrayName(TypeDeclaration type)
        {
            if (type.Schema.IsExplicitArrayType())
            {
                if (type.Schema.Items.IsNotUndefined() && type.Schema.Items.ValueKind != JsonValueKind.Array)
                {
                    TypeDeclaration itemsDeclaration = this.GetTypeDeclarationForProperty(type.Location, type.LexicalLocation, "items");

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

                    type.OverrideDotnetTypeName(targetName);
                }
            }

            foreach (TypeDeclaration child in type.Children)
            {
                this.RecursivelyFixArrayName(child);
            }
        }

        private void RecursivelySetName(TypeDeclaration type, string rootNamespace, Dictionary<string, string>? baseUriToNamespaceMap, string? rootTypeName, string rootLocation, int? index = null)
        {
            string? ns;
            if (baseUriToNamespaceMap is Dictionary<string, string> butnmp)
            {
                var location = new JsonReference(type.Location);

                if (!location.HasAbsoluteUri || !butnmp.TryGetValue(location.Uri.ToString(), out ns))
                {
                    ns = rootNamespace;
                }
            }
            else
            {
                ns = rootNamespace;
            }

            type.SetDotnetTypeNameAndNamespace(ns, index is null ? "Entity" : $"Entity{index + 1}");

            if (type.Location == rootLocation && rootTypeName is string rtn)
            {
                type.OverrideDotnetTypeName(rtn);
            }

            FixNameForChildren(type);

            int childIndex = 0;
            foreach (TypeDeclaration child in type.Children)
            {
                this.RecursivelySetName(child, rootNamespace, baseUriToNamespaceMap, rootTypeName, rootLocation, childIndex);
                ++childIndex;
            }
        }
    }
}
