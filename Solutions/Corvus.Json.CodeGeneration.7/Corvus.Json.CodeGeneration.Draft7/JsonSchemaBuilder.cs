// <copyright file="JsonSchemaBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Corvus.Json.CodeGeneration.Generators.Draft7;
using Corvus.Json.JsonSchema.Draft7;

namespace Corvus.Json.CodeGeneration.Draft7;

/// <summary>
/// A JSON schema type builder.
/// </summary>
public class JsonSchemaBuilder : IJsonSchemaBuilder
{
    private static readonly TypeDeclaration AnyTypeDeclarationInstance = new(new Schema(true));

    private readonly HashSet<TypeDeclaration> typeDeclarations = new();
    private readonly Dictionary<string, TypeDeclaration> locatedTypeDeclarations = new();
    private readonly JsonWalker walker;
    private readonly Dictionary<string, TypeDeclaration> dynamicAnchors = new();

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
    public async Task<(string RootType, ImmutableDictionary<string, TypeAndCode> GeneratedTypes)> BuildTypesFor(string reference, string rootNamespace, bool rebase = false, Dictionary<string, string>? baseUriToNamespaceMap = null, string? rootTypeName = null)
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
        SetParents(typesForGenerationByLocation);

        // Once the parents are set, we can build names for our types.
        this.SetNames(typesForGenerationByLocation, rootNamespace, baseUriToNamespaceMap, rootTypeName, rootLocation);

        // Now, find and add all our properties.
        this.FindProperties(typesForGenerationByLocation);

        rootTypeName = this.locatedTypeDeclarations[rootLocation].FullyQualifiedDotnetTypeName!;
        return (
            rootTypeName,
            typesForGenerationByLocation.Select(kvp => (kvp.Key, kvp.Value)).Select(this.GenerateFilesForType).ToImmutableDictionary(i => i.Location, i => i.TypeAndCode));
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
        return this.GetTypeDeclarationForProperty(new JsonReference(typeDeclaration.Location).AppendUnencodedPropertyNameToFragment("dependencies"), new JsonReference(typeDeclaration.LexicalLocation).AppendUnencodedPropertyNameToFragment("dependentSchemas"), dependentSchema);
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

    private static string GetDottedFileNameFor(TypeDeclaration typeDeclaration)
    {
        StringBuilder builder = new();
        TypeDeclaration? current = typeDeclaration;
        while (current is not null)
        {
            if (builder.Length > 0)
            {
                builder.Insert(0, ".");
            }

            builder.Insert(0, current.DotnetTypeName);
            current = current.Parent;
        }

        return builder.ToString();
    }

    private static void AddTypeDeclarationsToReferencedTypes(HashSet<TypeDeclaration> referencedTypes, TypeDeclaration typeDeclaration)
    {
        if (!referencedTypes.Contains(typeDeclaration))
        {
            referencedTypes.Add(typeDeclaration);
        }
    }

    private static void SetParents(Dictionary<string, TypeDeclaration> referencedTypesByLocation)
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

    private (string Location, TypeAndCode TypeAndCode) GenerateFilesForType((string Location, TypeDeclaration TypeDeclaration) typeForGeneration)
    {
        var codeGenerator = new CodeGenerator(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorArrayAdd = new CodeGeneratorArrayAdd(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorArrayRemove = new CodeGeneratorArrayRemove(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorArray = new CodeGeneratorArray(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorBoolean = new CodeGeneratorBoolean(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorConst = new CodeGeneratorConst(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorConversionsAccessors = new CodeGeneratorConversionsAccessors(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorConversionsOperators = new CodeGeneratorConversionsOperators(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorDefaults = new CodeGeneratorDefaults(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorDependentRequired = new CodeGeneratorDependentRequired(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorDependentSchema = new CodeGeneratorDependentSchema(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorEnum = new CodeGeneratorEnum(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorIfThenElse = new CodeGeneratorIfThenElse(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorNumber = new CodeGeneratorNumber(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorObject = new CodeGeneratorObject(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorPattern = new CodeGeneratorPattern(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorPatternProperties = new CodeGeneratorPatternProperties(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorProperties = new CodeGeneratorProperties(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorString = new CodeGeneratorString(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateAllOf = new CodeGeneratorValidateAllOf(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateAnyOf = new CodeGeneratorValidateAnyOf(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateArray = new CodeGeneratorValidateArray(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateFormat = new CodeGeneratorValidateFormat(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateIfThenElse = new CodeGeneratorValidateIfThenElse(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateMediaTypeAndEncoding = new CodeGeneratorValidateMediaTypeAndEncoding(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateNot = new CodeGeneratorValidateNot(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateObject = new CodeGeneratorValidateObject(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateOneOf = new CodeGeneratorValidateOneOf(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateRef = new CodeGeneratorValidateRef(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidate = new CodeGeneratorValidate(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateType = new CodeGeneratorValidateType(this, typeForGeneration.TypeDeclaration);

        string dotnetTypeName = typeForGeneration.TypeDeclaration.DotnetTypeName!;
        string fileName = GetDottedFileNameFor(typeForGeneration.TypeDeclaration);
        ImmutableArray<CodeAndFilename>.Builder files = ImmutableArray.CreateBuilder<CodeAndFilename>();

        files.Add(new(codeGenerator.TransformText(), $"{fileName}.cs"));
        files.Add(new(codeGeneratorValidate.TransformText(), $"{fileName}.Validate.cs"));

        if (codeGeneratorArrayAdd.ShouldGenerate)
        {
            files.Add(new(codeGeneratorArrayAdd.TransformText(), $"{fileName}.Array.Add.cs"));
        }

        if (codeGeneratorArrayRemove.ShouldGenerate)
        {
            files.Add(new(codeGeneratorArrayRemove.TransformText(), $"{fileName}.Array.Remove.cs"));
        }

        if (codeGeneratorArray.ShouldGenerate)
        {
            files.Add(new(codeGeneratorArray.TransformText(), $"{fileName}.Array.cs"));
        }

        if (codeGeneratorBoolean.ShouldGenerate)
        {
            files.Add(new(codeGeneratorBoolean.TransformText(), $"{fileName}.Boolean.cs"));
        }

        if (codeGeneratorConst.ShouldGenerate)
        {
            files.Add(new(codeGeneratorConst.TransformText(), $"{fileName}.Const.cs"));
        }

        if (codeGeneratorConversionsAccessors.ShouldGenerate)
        {
            files.Add(new(codeGeneratorConversionsAccessors.TransformText(), $"{fileName}.Conversions.Accessors.cs"));
        }

        if (codeGeneratorConversionsOperators.ShouldGenerate)
        {
            files.Add(new(codeGeneratorConversionsOperators.TransformText(), $"{fileName}.Conversions.Operators.cs"));
        }

        if (codeGeneratorDefaults.ShouldGenerate)
        {
            files.Add(new(codeGeneratorDefaults.TransformText(), $"{fileName}.Defaults.cs"));
        }

        if (codeGeneratorDependentRequired.ShouldGenerate)
        {
            files.Add(new(codeGeneratorDependentRequired.TransformText(), $"{fileName}.DependentRequired.cs"));
        }

        if (codeGeneratorDependentSchema.ShouldGenerate)
        {
            files.Add(new(codeGeneratorDependentSchema.TransformText(), $"{fileName}.DependentSchema.cs"));
        }

        if (codeGeneratorEnum.ShouldGenerate)
        {
            files.Add(new(codeGeneratorEnum.TransformText(), $"{fileName}.Enum.cs"));
        }

        if (codeGeneratorIfThenElse.ShouldGenerate)
        {
            files.Add(new(codeGeneratorIfThenElse.TransformText(), $"{fileName}.IfThenElse.cs"));
        }

        if (codeGeneratorNumber.ShouldGenerate)
        {
            files.Add(new(codeGeneratorNumber.TransformText(), $"{fileName}.Number.cs"));
        }

        if (codeGeneratorObject.ShouldGenerate)
        {
            files.Add(new(codeGeneratorObject.TransformText(), $"{fileName}.Object.cs"));
        }

        if (codeGeneratorPattern.ShouldGenerate)
        {
            files.Add(new(codeGeneratorPattern.TransformText(), $"{fileName}.Pattern.cs"));
        }

        if (codeGeneratorPatternProperties.ShouldGenerate)
        {
            files.Add(new(codeGeneratorPatternProperties.TransformText(), $"{fileName}.PatternProperties.cs"));
        }

        if (codeGeneratorProperties.ShouldGenerate)
        {
            files.Add(new(codeGeneratorProperties.TransformText(), $"{fileName}.Properties.cs"));
        }

        if (codeGeneratorString.ShouldGenerate)
        {
            files.Add(new(codeGeneratorString.TransformText(), $"{fileName}.String.cs"));
        }

        if (codeGeneratorValidateAllOf.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateAllOf.TransformText(), $"{fileName}.Validate.AllOf.cs"));
        }

        if (codeGeneratorValidateAnyOf.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateAnyOf.TransformText(), $"{fileName}.Validate.AnyOf.cs"));
        }

        if (codeGeneratorValidateArray.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateArray.TransformText(), $"{fileName}.Validate.Array.cs"));
        }

        if (codeGeneratorValidateFormat.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateFormat.TransformText(), $"{fileName}.Validate.Format.cs"));
        }

        if (codeGeneratorValidateIfThenElse.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateIfThenElse.TransformText(), $"{fileName}.Validate.IfThenElse.cs"));
        }

        if (codeGeneratorValidateMediaTypeAndEncoding.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateMediaTypeAndEncoding.TransformText(), $"{fileName}.Validate.MediaTypeAndEncoding.cs"));
        }

        if (codeGeneratorValidateNot.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateNot.TransformText(), $"{fileName}.Validate.Not.cs"));
        }

        if (codeGeneratorValidateObject.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateObject.TransformText(), $"{fileName}.Validate.Object.cs"));
        }

        if (codeGeneratorValidateOneOf.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateOneOf.TransformText(), $"{fileName}.Validate.OneOf.cs"));
        }

        if (codeGeneratorValidateRef.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateRef.TransformText(), $"{fileName}.Validate.Ref.cs"));
        }

        if (codeGeneratorValidateType.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateType.TransformText(), $"{fileName}.Validate.Type.cs"));
        }

        return new(
            typeForGeneration.Location,
            new(dotnetTypeName, files.ToImmutable()));
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
        if (currentDeclaration.Schema.AdditionalItems.IsNotUndefined())
        {
            TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration, "additionalItems");
            AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
            localTypes.Add(typeDeclaration);
        }

        if (currentDeclaration.Schema.AdditionalItems.IsNotUndefined())
        {
            TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration, "additionalItems");
            AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
            localTypes.Add(typeDeclaration);
        }

        if (currentDeclaration.Schema.AdditionalProperties.IsNotUndefined())
        {
            TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration, "additionalProperties");
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
            TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration, "contains");
            AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
            localTypes.Add(typeDeclaration);
        }

        if (currentDeclaration.Schema.Dependencies.IsNotUndefined())
        {
            foreach (JsonObjectProperty schemaProperty in currentDeclaration.Schema.Dependencies.EnumerateObject())
            {
                if (!schemaProperty.Value.As<Schema>().IsValid())
                {
                    // This is not a schema-style dependency.
                    continue;
                }

                TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(new JsonReference(currentDeclaration.Location).AppendUnencodedPropertyNameToFragment("dependencies"), new JsonReference(currentDeclaration.LexicalLocation).AppendUnencodedPropertyNameToFragment("dependencies"), schemaProperty.Name);
                AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                localTypes.Add(typeDeclaration);
            }
        }

        if (currentDeclaration.Schema.Else.IsNotUndefined())
        {
            TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration, "else");
            AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
            localTypes.Add(typeDeclaration);
        }

        if (currentDeclaration.Schema.If.IsNotUndefined())
        {
            TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration, "if");
            AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
            localTypes.Add(typeDeclaration);
        }

        if (currentDeclaration.Schema.Items.IsNotUndefined())
        {
            if (currentDeclaration.Schema.Items.IsSchemaArray)
            {
                int index = 0;
                foreach (Schema schema in currentDeclaration.Schema.Items.EnumerateArray())
                {
                    TypeDeclaration typeDeclaration = this.GetTypeDeclarationForPropertyArrayIndex(currentDeclaration.Location, "items", index);
                    AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                    localTypes.Add(typeDeclaration);
                    index++;
                }
            }
            else
            {
                TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration, "items");
                AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                localTypes.Add(typeDeclaration);
            }
        }

        if (currentDeclaration.Schema.Not.IsNotUndefined())
        {
            TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration, "not");
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
            foreach (JsonObjectProperty schemaProperty in currentDeclaration.Schema.PatternProperties.EnumerateObject())
            {
                TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(new JsonReference(currentDeclaration.Location).AppendUnencodedPropertyNameToFragment("patternProperties"), new JsonReference(currentDeclaration.LexicalLocation).AppendUnencodedPropertyNameToFragment("patternProperties"), schemaProperty.Name);
                AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
                localTypes.Add(typeDeclaration);
            }
        }

        if (currentDeclaration.Schema.Properties.IsNotUndefined())
        {
            foreach (JsonObjectProperty schemaProperty in currentDeclaration.Schema.Properties.EnumerateObject())
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
            TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration, "propertyNames");
            AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
            localTypes.Add(typeDeclaration);
        }

        if (currentDeclaration.Schema.Ref.IsNotUndefined())
        {
            // If we have a recursive ref, this is being applied in place; naked refs have already been reduced.
            TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration, "$ref");
            AddTypeDeclarationsToReferencedTypes(referencedTypes, typeDeclaration);
            localTypes.Add(typeDeclaration);
        }

        if (currentDeclaration.Schema.Then.IsNotUndefined())
        {
            TypeDeclaration typeDeclaration = this.GetTypeDeclarationForProperty(currentDeclaration, "then");
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
    private TypeDeclaration BuildTypeDeclarationFor(string location, Schema draft7Schema)
    {
        if (this.locatedTypeDeclarations.TryGetValue(location, out TypeDeclaration? existingDeclaration))
        {
            return existingDeclaration;
        }

        if (!draft7Schema.Validate(ValidationContext.ValidContext).IsValid)
        {
            throw new InvalidOperationException("Unable to build types for an invalid schema.");
        }

        if (this.TryReduceSchema(location, draft7Schema, out TypeDeclaration? reducedTypeDeclaration))
        {
            this.locatedTypeDeclarations.Add(location, reducedTypeDeclaration);
            return reducedTypeDeclaration;
        }

        // Check to see that we haven't located a type declration uring reduction.
        if (this.locatedTypeDeclarations.TryGetValue(location, out TypeDeclaration? builtDuringReduction))
        {
            return builtDuringReduction;
        }

        // If not, then add ourselves into the collection
        var result = new TypeDeclaration(location, draft7Schema);
        this.typeDeclarations.Add(result);
        this.locatedTypeDeclarations.Add(location, result);

        return result;
    }

    private bool TryReduceSchema(string absoluteLocation, Schema draft7Schema, [NotNullWhen(true)] out TypeDeclaration? reducedTypeDeclaration)
    {
        if (draft7Schema.IsNakedReference())
        {
            return this.ReduceSchema(absoluteLocation, out reducedTypeDeclaration, "$ref");
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

        return AnyTypeDeclarationInstance;
    }

    private TypeDeclaration GetTypeDeclarationForProperty(string location, string lexicalLocation, string propertyName)
    {
        if (this.TryGetTypeDeclarationForProperty(location, lexicalLocation, propertyName, out TypeDeclaration? typeDeclaration))
        {
            return typeDeclaration;
        }

        return AnyTypeDeclarationInstance;
    }

    private bool TryGetTypeDeclarationForProperty(string location, string lexicalLocation, string propertyName, [NotNullWhen(true)] out TypeDeclaration? typeDeclaration)
    {
        JsonReference schemaLocation = new JsonReference(location).AppendUnencodedPropertyNameToFragment(propertyName);
        string resolvedSchemaLocation = this.walker.GetLocatedElement(schemaLocation.ToString()).AbsoluteLocation;
        if (this.locatedTypeDeclarations.TryGetValue(resolvedSchemaLocation, out typeDeclaration))
        {
            return true;
        }

        typeDeclaration = default;
        return false;
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
            foreach (JsonString requiredName in source.Schema.Required.EnumerateArray())
            {
                target.AddOrReplaceProperty(new PropertyDeclaration(AnyTypeDeclarationInstance, requiredName!, true, source.Location == target.Location));
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

        // Then we add our own properties.
        if (source.Schema.Properties.IsNotUndefined())
        {
            foreach (JsonObjectProperty property in source.Schema.Properties.EnumerateObject())
            {
                JsonPropertyName propertyName = property.Name;
                bool isRequired = false;

                if (source.Schema.Required.IsNotUndefined())
                {
                    if (source.Schema.Required.EnumerateArray().Any(r => propertyName == r))
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
            if (type.Schema.Items.IsNotUndefined() && type.Schema.Items.IsSchema)
            {
                TypeDeclaration itemsDeclaration = this.GetTypeDeclarationForProperty(type, "items");

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

        if (type.Location == rootLocation && rootTypeName is string rtn && !string.IsNullOrEmpty(rtn))
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