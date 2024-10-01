// <copyright file="CodeGeneratorExtensions.Object.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using System.Web;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extension methods for the <see cref="CodeGenerator"/>.
/// </summary>
internal static partial class CodeGeneratorExtensions
{
    /// <summary>
    /// Append the AsPropertyBacking() method.
    /// </summary>
    /// <param name="generator">The generator to which to append the method.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsPropertyBackingMethod(this CodeGenerator generator)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .ReserveNameIfNotReserved("AsPropertyBacking")
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <inheritdoc/>
            public ImmutableList<JsonObjectProperty> AsPropertyBacking()
            {
                return __CorvusObjectHelpers.GetPropertyBacking(this);
            }
            """)
            .AppendBlockIndent(
            """
            /// <inheritdoc/>
            public ImmutableList<JsonObjectProperty>.Builder AsPropertyBackingBuilder()
            {
                return __CorvusObjectHelpers.GetPropertyBacking(this).ToBuilder();
            }
            """);
    }

    /// <summary>
    /// Append the IReadOnlyDictionary properties.
    /// </summary>
    /// <param name="generator">The generator to which to append the properties.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the indexers.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendReadOnlyDictionaryProperties(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.FallbackObjectPropertyType() is FallbackObjectPropertyType propertyType
             && !propertyType.ReducedType.IsBuiltInJsonAnyType())
        {
            string propertyTypeName = propertyType.ReducedType.FullyQualifiedDotnetTypeName();

            return generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendReadOnlyDictionaryIndexer(propertyTypeName)
                .AppendSeparatorLine()
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendReadOnlyDictionaryKeys(propertyTypeName)
                .AppendSeparatorLine()
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendReadOnlyDictionaryValues(propertyTypeName)
                .AppendSeparatorLine()
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendReadOnlyDictionaryCount(propertyTypeName);
        }

        return generator;
    }

    /// <summary>
    /// Append the object property count property.
    /// </summary>
    /// <param name="generator">The generator to which to append the properties.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendPropertyCount(this CodeGenerator generator)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .ReserveNameIfNotReserved("Count")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Gets the number of properties in the object.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("public int Count")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalWrappedBackingValueLineIndent("Backing.JsonElement", "return ", "jsonElementBacking", ".GetPropertyCount();")
                    .AppendConditionalWrappedBackingValueLineIndent("Backing.Object", "return ", "objectBacking", ".Count;")
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the dictionary enumerable methods.
    /// </summary>
    /// <param name="generator">The generator to which to append the methods.</param>
    /// <param name="typeDeclaration">The type declaration for which to append dictionary enumerable methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendReadOnlyDictionaryMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.FallbackObjectPropertyType() is FallbackObjectPropertyType propertyType
             && !propertyType.ReducedType.IsBuiltInJsonAnyType())
        {
            string propertyTypeName = propertyType.ReducedType.FullyQualifiedDotnetTypeName();

            return generator
                .ReserveNameIfNotReserved("GetEnumerator")
                .AppendSeparatorLine()
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("IEnumerator<KeyValuePair<JsonPropertyName, ")
                .Append(propertyTypeName)
                .Append(">> IEnumerable<KeyValuePair<JsonPropertyName, ")
                .Append(propertyTypeName)
                .AppendLine(">>.GetEnumerator()")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalBackingValueCallbackIndent("Backing.JsonElement", "jsonElementBacking", (g, f) => AppendCreateEnumerator(g, f, propertyTypeName))
                    .AppendConditionalBackingValueCallbackIndent("Backing.Object", "objectBacking", (g, f) => AppendCreateEnumerator(g, f, propertyTypeName))
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendLineIndent("IEnumerator IEnumerable.GetEnumerator() => this.EnumerateObject();")
                .AppendSeparatorLine()
                .AppendLineIndent(
                    "bool IReadOnlyDictionary<JsonPropertyName, ",
                    propertyTypeName,
                    ">.ContainsKey(JsonPropertyName key) => this.HasProperty(key);")
                .AppendSeparatorLine()
                .AppendLineIndent(
                    "bool IReadOnlyDictionary<JsonPropertyName, ",
                    propertyTypeName,
                    ">.TryGetValue(JsonPropertyName key, out ",
                    propertyTypeName,
                    " result) => this.TryGetProperty(key, out result);");
        }

        return generator;

        static void AppendCreateEnumerator(CodeGenerator generator, string fieldName, string propertyTypeName)
        {
            generator
                .AppendLineIndent("return new ReadOnlyDictionaryJsonObjectEnumerator<", propertyTypeName, ">(this.", fieldName, ");");
        }
    }

    /// <summary>
    /// Append the property accessors.
    /// </summary>
    /// <param name="generator">The generator to which to append the properties.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property accessors.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendPropertyAccessors(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        foreach (PropertyDeclaration property in typeDeclaration.PropertyDeclarations)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            bool isNullable = typeDeclaration.OptionalAsNullable() && property.RequiredOrOptional == RequiredOrOptional.Optional;
            generator
                .AppendSeparatorLine()
                .AppendPropertyDocumentation(property)
                .AppendObsoleteAttribute(property)
                .BeginPublicReadOnlyPropertyDeclaration(property.ReducedPropertyType.FullyQualifiedDotnetTypeName(), property.DotnetPropertyName(), isNullable)
                    .AppendConditionalBackingValueCallbackIndent("Backing.JsonElement", "jsonElementBacking", (g, f) => AppendJsonBackedAccessor(g, typeDeclaration, f, property))
                    .AppendConditionalBackingValueCallbackIndent("Backing.Object", "objectBacking", (g, f) => AppendObjectBackedAccessor(g, typeDeclaration, f, property))
                    .AppendSeparatorLine()
                    .AppendLineIndent("return default;")
                .EndReadOnlyPropertyDeclaration();
        }

        return generator;

        static void AppendJsonBackedAccessor(CodeGenerator generator, TypeDeclaration typeDeclaration, string fieldName, PropertyDeclaration property)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .AppendLineIndent("if (this.", fieldName, ".ValueKind != JsonValueKind.Object)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return default;")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent(
                    "if (this.",
                    fieldName,
                    ".TryGetProperty(",
                    generator.JsonPropertyNamesClassName(),
                    ".",
                    property.DotnetPropertyName(),
                    "Utf8, out JsonElement result))")
                .AppendLineIndent("{")
                .PushIndent();

            if (typeDeclaration.OptionalAsNullable() && property.RequiredOrOptional == RequiredOrOptional.Optional)
            {
                generator
                    .AppendLineIndent("if (result.ValueKind == JsonValueKind.Null || result.ValueKind == JsonValueKind.Undefined)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("return default;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine();
            }

            generator
                    .AppendLineIndent("return new(result);")
                .PopIndent()
                .AppendLineIndent("}");
        }

        static void AppendObjectBackedAccessor(CodeGenerator generator, TypeDeclaration typeDeclaration, string fieldName, PropertyDeclaration property)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .AppendSeparatorLine()
                .AppendLineIndent(
                    "if (this.",
                    fieldName,
                    ".TryGetValue(",
                    generator.JsonPropertyNamesClassName(),
                    ".",
                    property.DotnetPropertyName(),
                    ", out JsonAny result))")
                .AppendLineIndent("{")
                .PushIndent();

            if (typeDeclaration.OptionalAsNullable() && property.RequiredOrOptional == RequiredOrOptional.Optional)
            {
                generator
                    .AppendLineIndent("if (result.IsNullOrUndefined())")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("return default;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine();
            }

            if (property.ReducedPropertyType.IsBuiltInJsonAnyType())
            {
                generator
                    .AppendLineIndent("return result;");
            }
            else
            {
                generator
                    .AppendLineIndent("return result.As<", property.ReducedPropertyType.FullyQualifiedDotnetTypeName(), ">();");
            }

            generator
                .PopIndent()
                .AppendLineIndent("}");
        }
    }

    /// <summary>
    /// Append the JsonPropertyNames class.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property names class.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendJsonPropertyNamesClass(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (!typeDeclaration.HasPropertyDeclarations)
        {
            return generator;
        }

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Provides UTF8 and string versions of the JSON property names on the object.")
            .AppendLineIndent("/// </summary>")
            .BeginPublicStaticClassDeclaration(generator.JsonPropertyNamesClassName());

        int i = 0;
        foreach (PropertyDeclaration property in typeDeclaration.PropertyDeclarations)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (i > 0)
            {
                generator
                    .AppendLine();
            }

            generator
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Gets the JSON property name for <see cref=\"", property.DotnetPropertyName(), "\"/>.")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent(
                    "public const string ",
                    property.DotnetPropertyName(),
                    " = ",
                    SymbolDisplay.FormatLiteral(property.JsonPropertyName, true),
                    ";");
            i++;
        }

        foreach (PropertyDeclaration property in typeDeclaration.PropertyDeclarations)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (i > 0)
            {
                generator
                    .AppendLine();
            }

            generator
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Gets the JSON property name for <see cref=\"", property.DotnetPropertyName(), "\"/>.")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent(
                    "public static ReadOnlySpan<byte> ",
                    property.DotnetPropertyName(),
                    "Utf8 => ",
                    SymbolDisplay.FormatLiteral(property.JsonPropertyName, true),
                    "u8;");
            i++;
        }

        return generator
            .EndClassOrStructDeclaration();
    }

    /// <summary>
    /// Append object indexer properties.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the indexers.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendObjectIndexerProperties(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .AppendSeparatorLine();

        if (typeDeclaration.FallbackObjectPropertyType() is FallbackObjectPropertyType propertyType
            && !propertyType.ReducedType.IsBuiltInJsonAnyType())
        {
            generator
                .AppendLineIndent(
                    "/// <inheritdoc/>")
                .AppendObjectIndexer(typeDeclaration, propertyType.ReducedType.FullyQualifiedDotnetTypeName(), isExplicit: true)
                .AppendSeparatorLine()
                .AppendBlockIndent(
                """
                    /// <summary>
                    /// Gets the property with the given name.
                    /// </summary>
                    /// <param name="name">The name of the property to retrieve.</param>
                    /// <returns>The value of the property with the given name.</returns>
                    /// <exception cref="IndexOutOfRangeException">The given property was not present on the object.</exception>
                    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
                    """)
                .AppendObjectIndexer(typeDeclaration, propertyType.ReducedType.FullyQualifiedDotnetTypeName(), isExplicit: false);
        }
        else
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendObjectIndexer(typeDeclaration, WellKnownTypeDeclarations.JsonAny.FullyQualifiedDotnetTypeName(), isExplicit: false);
        }

        return generator;
    }

    /// <summary>
    /// Append the <c>EnumerateObject()</c> methods.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the enumerate object methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendEnumerateObjectMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .ReserveNameIfNotReserved("EnumerateObject");

        if (typeDeclaration.FallbackObjectPropertyType() is FallbackObjectPropertyType propertyType && !propertyType.ReducedType.IsBuiltInJsonAnyType())
        {
            string propertyTypeName = propertyType.ReducedType.FullyQualifiedDotnetTypeName();

            generator
                .AppendEnumerateObjectMethod(typeDeclaration, isExplicit: true)
                .AppendEnumerateObjectMethod(typeDeclaration, propertyTypeName);
        }
        else
        {
            generator
                .AppendEnumerateObjectMethod(typeDeclaration);
        }

        return generator;
    }

    /// <summary>
    /// Append the <c>MatchesPattern()</c> and <c>TryAsPattern()</c> methods.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendPatternPropertiesMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.PatternProperties() is IReadOnlyDictionary<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>> patternProperties)
        {
            foreach (IObjectPatternPropertyValidationKeyword keyword in patternProperties.Keys)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                IReadOnlyCollection<PatternPropertyDeclaration> declarations = patternProperties[keyword];
                int index = 1;
                bool hasIndex = declarations.Count > 1;

                foreach (PatternPropertyDeclaration declaration in declarations)
                {
                    if (generator.IsCancellationRequested)
                    {
                        return generator;
                    }

                    string regexAccessor =
                        generator.GetStaticReadOnlyFieldNameInScope(
                            declaration.Keyword.Keyword,
                            rootScope: generator.ValidationClassScope(),
                            suffix: hasIndex ? index.ToString() : null);

                    string matchesPatternName = generator.GetMethodNameInScope(declaration.ReducedPatternPropertyType.DotnetTypeName(), prefix: "MatchesPattern");
                    string tryAsPatternName = generator.GetMethodNameInScope(declaration.ReducedPatternPropertyType.DotnetTypeName(), prefix: "TryAsPattern");

                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("/// <summary>")
                        .AppendLineIndent("/// Determines if a property name matches '", HttpUtility.HtmlEncode(declaration.Pattern), "'")
                        .AppendLineIndent("/// for the pattern property producing the type")
                        .AppendLineIndent("/// <see cref=\"", declaration.ReducedPatternPropertyType.FullyQualifiedDotnetTypeName(), "\"/>.")
                        .AppendLineIndent("/// </summary>")
                        .AppendLineIndent("public static bool ", matchesPatternName, "(in JsonObjectProperty property) => property.Name.IsMatch(", generator.ValidationClassName(), ".", regexAccessor, ");")
                        .AppendSeparatorLine()
                        .AppendLineIndent("/// <summary>")
                        .AppendLineIndent("/// Gets an instance of the type")
                        .AppendLineIndent("/// <see cref=\"", declaration.ReducedPatternPropertyType.FullyQualifiedDotnetTypeName(), "\"/>")
                        .AppendLineIndent("/// if the property name matches '", HttpUtility.HtmlEncode(declaration.Pattern), "'.")
                        .AppendLineIndent("/// </summary>")
                        .AppendLineIndent("public static bool ", tryAsPatternName, "(in JsonObjectProperty property, out ", declaration.ReducedPatternPropertyType.FullyQualifiedDotnetTypeName(), " result)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("if (property.Name.IsMatch(", generator.ValidationClassName(), ".", regexAccessor, "))")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent("result = property.Value.As<", declaration.ReducedPatternPropertyType.FullyQualifiedDotnetTypeName(), ">();")
                                .AppendLineIndent("return result.IsValid();")
                            .PopIndent()
                            .AppendLineIndent("}")
                            .AppendSeparatorLine()
                            .AppendLineIndent("result = ", declaration.ReducedPatternPropertyType.FullyQualifiedDotnetTypeName(), ".Undefined;")
                            .AppendLineIndent("return false;")
                        .PopIndent()
                        .AppendLineIndent("}");
                    ++index;
                }
            }
        }

        return generator;
    }

    /// <summary>
    /// Append the pattern properties static properties.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the properties.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendPatternPropertiesStaticProperties(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.PatternProperties() is IReadOnlyDictionary<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>> patternProperties)
        {
            foreach (IObjectPatternPropertyValidationKeyword keyword in patternProperties.Keys)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                IReadOnlyCollection<PatternPropertyDeclaration> declarations = patternProperties[keyword];
                int index = 1;
                bool hasIndex = declarations.Count > 1;

                foreach (PatternPropertyDeclaration declaration in declarations)
                {
                    if (generator.IsCancellationRequested)
                    {
                        return generator;
                    }

                    string regexAccessor =
                        generator.GetStaticReadOnlyFieldNameInScope(
                            declaration.Keyword.Keyword,
                            rootScope: generator.ValidationClassScope(),
                            suffix: hasIndex ? index.ToString() : null);

                    string propertyName = generator.GetPropertyNameInScope(declaration.ReducedPatternPropertyType.DotnetTypeName(), prefix: "PatternProperty");

                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("/// <summary>")
                        .AppendLineIndent("/// The pattern matching '", HttpUtility.HtmlEncode(declaration.Pattern), "'")
                        .AppendLineIndent("/// for the pattern property producing the type")
                        .AppendLineIndent("/// <see cref=\"", declaration.ReducedPatternPropertyType.FullyQualifiedDotnetTypeName(), "\"/>.")
                        .AppendLineIndent("/// </summary>")
                        .AppendLineIndent("public static Regex ", propertyName, " => ", generator.ValidationClassName(), ".", regexAccessor, ";");

                    ++index;
                }
            }
        }

        return generator;
    }

    /// <summary>
    /// Append the <c>TryGetDependentSchema()</c> methods.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendTryGetAsDependentSchemaMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.DependentSchemasSubschemaTypes() is IReadOnlyDictionary<IObjectPropertyDependentSchemasValidationKeyword, IReadOnlyCollection<DependentSchemaDeclaration>> dependencies)
        {
            HashSet<string> visitedTypeNames = [];
            foreach (IReadOnlyCollection<DependentSchemaDeclaration> declarations in dependencies.Values)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                foreach (DependentSchemaDeclaration declaration in declarations)
                {
                    if (generator.IsCancellationRequested)
                    {
                        return generator;
                    }

                    if (!visitedTypeNames.Add(declaration.JsonPropertyName))
                    {
                        continue;
                    }

                    string name = generator.GetMethodNameInScope("TryAsDependentSchemaFor", suffix: declaration.JsonPropertyName);
                    string dotnetPropertyName =
                        typeDeclaration
                            .PropertyDeclarations
                            .Single(p => p.JsonPropertyName == declaration.JsonPropertyName)
                            .DotnetPropertyName();
                    string targetName = declaration.ReducedDepdendentSchemaType.FullyQualifiedDotnetTypeName();

                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("/// <summary>")
                        .AppendLineIndent(
                            "/// Tries to get the instance as a <see cref=\"",
                            targetName,
                            "\"/>, if the <see cref=\"",
                            dotnetPropertyName,
                            "\"/> property is present.")
                        .AppendLineIndent("/// </summary>")
                        .AppendLineIndent("/// <param name=\"result\">The value as a <see cref=\"", targetName, "\" />.</param>.")
                        .AppendLineIndent("/// <returns><see langword=\"true\" /> if the property was present, and the instance matches the dependent schema.</returns>")
                        .AppendLineIndent("public bool ", name, "(out ", targetName, " result)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("if (this.", dotnetPropertyName, ".IsNotUndefined())")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent("result = this.As<", targetName, ">();")
                                .AppendLineIndent("return result.IsValid();")
                            .PopIndent()
                            .AppendLineIndent("}")
                            .AppendSeparatorLine()
                            .AppendLineIndent("result = ", targetName, ".Undefined;")
                            .AppendLineIndent("return false;")
                        .PopIndent()
                        .AppendLineIndent("}");
                }
            }
        }

        return generator;
    }

    /// <summary>
    /// Append the <c>Create()</c> factory method.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the create factory method.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendCreateFactoryMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (!typeDeclaration.HasPropertyDeclarations)
        {
            return generator;
        }

        MethodParameter[] methodParameters = BuildMethodParameters(generator, typeDeclaration);
        PropertyDeclaration[] orderedProperties = BuildOrderedProperties(typeDeclaration);

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Creates an instance of a <see cref=\"", typeDeclaration.DotnetTypeName(), "\"/>.")
            .AppendLineIndent("/// </summary>");

        generator
            .BeginReservedMethodDeclaration(
                "public static",
                typeDeclaration.DotnetTypeName(),
                "Create",
                methodParameters);

        AppendBuildObjectFromParameters(generator, orderedProperties);

        return generator
            .EndMethodDeclaration();

        static MethodParameter[] BuildMethodParameters(CodeGenerator generator, TypeDeclaration typeDeclaration)
        {
            if (generator.IsCancellationRequested)
            {
                return [];
            }

            // Filter out the constant values from the property list;
            // we don't need to supply them.
            return
            [
                .. typeDeclaration.PropertyDeclarations
                            .Where(p => p.RequiredOrOptional == RequiredOrOptional.Required &&
                                   p.ReducedPropertyType.SingleConstantValue().ValueKind == JsonValueKind.Undefined)
                            .OrderBy(p => p.JsonPropertyName)
                            .Select(p => new MethodParameter("in", p.ReducedPropertyType.FullyQualifiedDotnetTypeName(), generator.GetParameterNameInScope(p.DotnetPropertyName(), childScope: "Create"))),
                .. typeDeclaration.PropertyDeclarations
                                        .Where(p => p.RequiredOrOptional == RequiredOrOptional.Optional)
                                        .OrderBy(p => p.JsonPropertyName)
                                        .Select(p =>
                                            new MethodParameter(
                                                "in",
                                                p.ReducedPropertyType.FullyQualifiedDotnetTypeName(),
                                                generator.GetParameterNameInScope(p.DotnetPropertyName(), childScope: "Create"),
                                                typeIsNullable: true,
                                                defaultValue: "null")),
            ];
        }

        static PropertyDeclaration[] BuildOrderedProperties(TypeDeclaration typeDeclaration)
        {
            // Filter out the constant values from the property list;
            // we don't need to supply them.
            return
            [
                .. typeDeclaration.PropertyDeclarations
                                            .Where(p => p.RequiredOrOptional == RequiredOrOptional.Required)
                                            .OrderBy(p => p.JsonPropertyName),
                .. typeDeclaration.PropertyDeclarations
                                            .Where(p => p.RequiredOrOptional == RequiredOrOptional.Optional)
                                            .OrderBy(p => p.JsonPropertyName),
            ];
        }

        static CodeGenerator AppendBuildObjectFromParameters(CodeGenerator generator, PropertyDeclaration[] properties)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            generator
                .AppendLineIndent("var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();");

            foreach (PropertyDeclaration property in properties)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                if (property.RequiredOrOptional == RequiredOrOptional.Required)
                {
                    AddRequiredProperty(generator, property);
                }
                else
                {
                    AddOptionalProperty(generator, property);
                }
            }

            return generator
                .AppendSeparatorLine()
                .AppendLineIndent("return new(builder.ToImmutable());");
        }

        static void AddRequiredProperty(CodeGenerator generator, PropertyDeclaration property)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            string propertyNamesClass = generator.JsonPropertyNamesClassName();
            if (property.ReducedPropertyType.SingleConstantValue().ValueKind != JsonValueKind.Undefined)
            {
                generator
                    .AppendLineIndent(
                        "builder.Add(",
                        propertyNamesClass,
                        ".",
                        property.DotnetPropertyName(),
                        ", ",
                        property.ReducedPropertyType.FullyQualifiedDotnetTypeName(),
                        ".ConstInstance.AsAny);");
            }
            else
            {
                string parameterName = generator.GetParameterNameInScope(property.DotnetPropertyName());

                generator
                    .AppendLineIndent(
                        "builder.Add(",
                        propertyNamesClass,
                        ".",
                        property.DotnetPropertyName(),
                        ", ",
                        parameterName,
                        ".AsAny);");
            }
        }

        static void AddOptionalProperty(CodeGenerator generator, PropertyDeclaration property)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            string propertyNamesClass = generator.JsonPropertyNamesClassName();
            string parameterName = generator.GetParameterNameInScope(property.DotnetPropertyName());

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("if (", parameterName, " is not null)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent(
                        "builder.Add(",
                        propertyNamesClass,
                        ".",
                        property.DotnetPropertyName(),
                        ", ",
                        parameterName,
                        ".Value.AsAny);")
                .PopIndent()
                .AppendLineIndent("}");
        }
    }

    /// <summary>
    /// Append the <c>FromProperties()</c> factory methods.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the factory methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendFromPropertiesFactoryMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .ReserveNameIfNotReserved("FromProperties");

        if (typeDeclaration.FallbackObjectPropertyType() is FallbackObjectPropertyType propertyType && !propertyType.ReducedType.IsBuiltInJsonAnyType())
        {
            string propertyTypeName = propertyType.ReducedType.FullyQualifiedDotnetTypeName();

            generator
                .AppendSeparatorLine()
                .AppendLine("#if NET8_0_OR_GREATER")
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("static ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" IJsonObject<")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(">.FromProperties(IDictionary<JsonPropertyName, JsonAny> source)")
                .AppendBlockIndent(
                """
                {
                    return new(source.Select(kvp => new JsonObjectProperty(kvp.Key, kvp.Value)).ToImmutableList());
                }
                """)

                .AppendSeparatorLine()
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("static ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" IJsonObject<")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(">.FromProperties(params (JsonPropertyName Name, JsonAny Value)[] source)")
                .AppendBlockIndent(
                """
                {
                    return new(source.Select(s => new JsonObjectProperty(s.Name, s.Value)).ToImmutableList());
                }
                """)
                .AppendLine("#endif")

                .AppendSeparatorLine()
                .AppendBlockIndent(
                """
                /// <summary>
                /// Creates an instance of the type from the given dictionary of properties.
                /// </summary>
                /// <param name="source">The dictionary of properties.</param>
                /// <returns>An instance of the type initialized from the dictionary of properties.</returns>
                """)
                .AppendIndent("public static ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" FromProperties(IDictionary<JsonPropertyName, ")
                .Append(propertyTypeName)
                .AppendLine("> source)")
                .AppendBlockIndent(
                """
                {
                    return new(source.Select(kvp => new JsonObjectProperty(kvp.Key, kvp.Value.AsAny)).ToImmutableList());
                }
                """)

                .AppendSeparatorLine()
                .AppendBlockIndent(
                """
                /// <summary>
                /// Creates an instance of the type from the given name/value tuples.
                /// </summary>
                /// <param name="source">The name value tuples.</param>
                /// <returns>An instance of the type initialized from the properties.</returns>
                """)
                .AppendIndent("public static ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" FromProperties(params (JsonPropertyName Name, ")
                .Append(propertyTypeName)
                .AppendLine(" Value)[] source)")
                .AppendBlockIndent(
                """
                {
                    return new(source.Select(s => new JsonObjectProperty(s.Name, s.Value.AsAny)).ToImmutableList());
                }
                """);
        }
        else
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public static ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" FromProperties(IDictionary<JsonPropertyName, JsonAny> source)")
                .AppendBlockIndent(
                """
                {
                    return new(source.Select(kvp => new JsonObjectProperty(kvp.Key, kvp.Value)).ToImmutableList());
                }
                """)

                .AppendSeparatorLine()
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("public static ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" FromProperties(params (JsonPropertyName Name, JsonAny Value)[] source)")
                .AppendBlockIndent(
                """
                {
                    return new(source.Select(s => new JsonObjectProperty(s.Name, s.Value.AsAny)).ToImmutableList());
                }
                """);
        }

        return generator
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <summary>
            /// Creates an instance of the type from the given immutable list of properties.
            /// </summary>
            /// <param name="source">The list of properties.</param>
            /// <returns>An instance of the type initialized from the list of properties.</returns>
            """)
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" FromProperties(ImmutableList<JsonObjectProperty> source)")
            .AppendBlockIndent(
            """
            {
                return new(source);
            }
            """);
    }

    /// <summary>
    /// Append the <c>HasProperties()</c> method.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendHasPropertiesMethod(this CodeGenerator generator)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .ReserveNameIfNotReserved("HasProperties")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent("public bool HasProperties()")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalWrappedBackingValueLineIndent("Backing.Object", "return ", "objectBacking", ".Count > 0;")
                .AppendConditionalBackingValueCallbackIndent("Backing.JsonElement", "jsonElementBacking", TestEnumerable)
                .AppendSeparatorLine()
                .AppendLineIndent("throw new InvalidOperationException();")
            .PopIndent()
            .AppendLineIndent("}");

        static void TestEnumerable(CodeGenerator generator, string fieldName)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .AppendIndent("using JsonElement.ObjectEnumerator enumerator = this.")
                .Append(fieldName)
                .AppendLine(".EnumerateObject();")
                .AppendLineIndent("return enumerator.MoveNext();");
        }
    }

    /// <summary>
    /// Append the <c>HasProperty()</c> methods.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendHasPropertyMethods(this CodeGenerator generator)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .ReserveNameIfNotReserved("HasProperty")
            .AppendHasPropertyForJsonPropertyNameMethod()
            .AppendHasPropertyMethod("string")
            .AppendHasPropertyMethod("ReadOnlySpan<char>")
            .AppendHasPropertyMethod("ReadOnlySpan<byte>");
    }

    /// <summary>
    /// Append the <c>TryGetProperty()</c> methods.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendTryGetPropertyMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .ReserveNameIfNotReserved("TryGetProperty")
            .AppendTryGetPropertyForJsonPropertyNameMethod(typeDeclaration)
            .AppendTryGetPropertyMethod(typeDeclaration, "string")
            .AppendTryGetPropertyMethod(typeDeclaration, "ReadOnlySpan<char>")
            .AppendTryGetPropertyMethod(typeDeclaration, "ReadOnlySpan<byte>")
            .AppendGenericTryGetPropertyForJsonPropertyNameMethod(typeDeclaration)
            .AppendGenericTryGetPropertyMethod(typeDeclaration, "string")
            .AppendGenericTryGetPropertyMethod(typeDeclaration, "ReadOnlySpan<char>")
            .AppendGenericTryGetPropertyMethod(typeDeclaration, "ReadOnlySpan<byte>");
    }

    /// <summary>
    /// Append the <c>RemoveProperty()</c> methods.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendRemovePropertyMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .ReserveNameIfNotReserved("RemoveProperty")
            .AppendRemovePropertyMethod(typeDeclaration, "in JsonPropertyName")
            .AppendRemovePropertyMethod(typeDeclaration, "string")
            .AppendRemovePropertyMethod(typeDeclaration, "ReadOnlySpan<char>")
            .AppendRemovePropertyMethod(typeDeclaration, "ReadOnlySpan<byte>");
    }

    /// <summary>
    /// Append the <c>SetProperty()</c> methods.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendSetPropertyMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .ReserveNameIfNotReserved("SetProperty");

        if (typeDeclaration.FallbackObjectPropertyType() is FallbackObjectPropertyType propertyType && !propertyType.ReducedType.IsBuiltInJsonAnyType())
        {
            generator
                .AppendSetPropertyMethod(typeDeclaration, "TValue", isGeneric: true, isExplicit: true)
                .AppendSetPropertyMethod(typeDeclaration, propertyType.ReducedType.FullyQualifiedDotnetTypeName());
        }
        else
        {
            generator
                .AppendSetPropertyMethod(typeDeclaration, "TValue", isGeneric: true, isExplicit: false);
        }

        return generator;
    }

    /// <summary>
    /// Append the <c>__CorvusObjectHelpers</c> class.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the helper class.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendCorvusObjectHelpers(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .ReserveNameIfNotReserved("__CorvusObjectHelpers")
            .AppendSeparatorLine()
            .AppendLineIndent("private static class __CorvusObjectHelpers")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBlockIndent(
                """
                /// <summary>
                /// Builds an <see cref="ImmutableList{JsonObjectProperty}"/> from the object.
                /// </summary>
                /// <returns>An immutable list of <see cref="JsonAny"/> built from the object.</returns>
                /// <exception cref="InvalidOperationException">The value is not an object.</exception>
                """)
                .AppendIndent("public static ImmutableList<JsonObjectProperty> GetPropertyBacking(in ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" that)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalWrappedBackingValueLineIndent("Backing.Object", "return ", "objectBacking", ";", identifier: "that")
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.JsonElement",
                        "return PropertyBackingBuilders.GetPropertyBackingBuilder(",
                        "jsonElementBacking",
                        ").ToImmutable();",
                        identifier: "that")
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendGetPropertyBackingWithout(typeDeclaration.DotnetTypeName(), "in JsonPropertyName")
                .AppendGetPropertyBackingWithout(typeDeclaration.DotnetTypeName(), "ReadOnlySpan<char>")
                .AppendGetPropertyBackingWithout(typeDeclaration.DotnetTypeName(), "ReadOnlySpan<byte>")
                .AppendGetPropertyBackingWithout(typeDeclaration.DotnetTypeName(), "string")
                .AppendGetPropertyBackingWith(typeDeclaration.DotnetTypeName(), "in JsonPropertyName")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendPropertyDocumentation(this CodeGenerator generator, PropertyDeclaration property)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        bool optional = property.RequiredOrOptional == RequiredOrOptional.Optional;

        generator
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent(
            "/// Gets the ",
            optional ? "(optional) " : string.Empty,
            "<c>",
            SymbolDisplay.FormatLiteral(property.JsonPropertyName, false),
            "</c> ",
            "property.");

        if (property.UnreducedPropertyType.CanReduce())
        {
            // We include documentation attached to a reducible reference type; this is usually the means by
            // which property-specific documentation is attached to a particular instance of a common reference type.
            if (property.UnreducedPropertyType.ShortDocumentation() is string shortDocumentation)
            {
                generator
                    .AppendBlockIndentWithPrefix(shortDocumentation, "/// ");
            }
        }

        generator
            .AppendLineIndent("/// </summary>");

        bool usingRemarks = false;
        if (!optional)
        {
            usingRemarks = true;

            generator
                .AppendLineIndent("/// <remarks>")
                .AppendLineIndent("/// <para>")
                .AppendIndent("/// If the instance is valid, this property will not be <see cref=\"JsonValueKind.Undefined\"/>");

            if ((property.ReducedPropertyType.ImpliedCoreTypesOrAny() & CoreTypes.Null) != 0)
            {
                generator.Append(", but may be <see cref=\"JsonValueKind.Null\"/>");
            }

            generator
                .AppendLine(".")
                .AppendLineIndent("/// </para>");
        }
        else if (property.Owner.OptionalAsNullable())
        {
            usingRemarks = true;

            generator
                .AppendLineIndent("/// <remarks>")
                .AppendLineIndent("/// <para>")
                .AppendIndent("/// If this JSON property is <see cref=\"JsonValueKind.Undefined\"/>");

            if ((property.ReducedPropertyType.ImpliedCoreTypesOrAny() & CoreTypes.Null) != 0)
            {
                generator.Append(", or <see cref=\"JsonValueKind.Null\"/>");
            }

            generator
                .AppendLine(" then the value returned will be <see langword=\"null\" />.")
                .AppendLineIndent("/// </para>");
        }

        if (property.UnreducedPropertyType.LongDocumentation() is string longDocumentation)
        {
            if (!usingRemarks)
            {
                usingRemarks = true;
                generator
                    .AppendLineIndent("/// <remarks>");
            }

            generator
                .AppendParagraphs(longDocumentation);
        }

        if (property.ReducedPropertyType != property.UnreducedPropertyType && property.ReducedPropertyType.LongDocumentation() is string longDocumentationReduced)
        {
            if (!usingRemarks)
            {
                usingRemarks = true;
                generator
                    .AppendLineIndent("/// <remarks>");
            }

            generator
                .AppendParagraphs(longDocumentationReduced);
        }

        if (usingRemarks)
        {
            generator.AppendLineIndent("/// </remarks>");
        }

        return generator;
    }

    private static CodeGenerator AppendGetPropertyBackingWithout(this CodeGenerator generator, string dotnetTypeName, string parameterNameAndModifiers)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <summary>
            /// Builds an <see cref="ImmutableList{JsonObjectProperty}"/> from the object, without a specific property.
            /// </summary>
            /// <returns>An immutable list of <see cref="JsonObjectProperty"/>, built from the existing object, without the given property.</returns>
            /// <exception cref="InvalidOperationException">The value is not an object.</exception>
            """)
            .AppendIndent("public static ImmutableList<JsonObjectProperty> GetPropertyBackingWithout(in ")
            .Append(dotnetTypeName)
            .Append(" that, ")
            .Append(parameterNameAndModifiers)
            .AppendLine(" name)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalWrappedBackingValueLineIndent("Backing.Object", "return ", "objectBacking", ".Remove(name);", identifier: "that")
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.JsonElement",
                    "return PropertyBackingBuilders.GetPropertyBackingBuilderWithout(",
                    "jsonElementBacking",
                    ", name).ToImmutable();",
                    identifier: "that")
                .AppendSeparatorLine()
                .AppendLineIndent("throw new InvalidOperationException();")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendGetPropertyBackingWith(this CodeGenerator generator, string dotnetTypeName, string parameterNameAndModifiers)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <summary>
            /// Builds an <see cref="ImmutableList{JsonObjectProperty}"/> from the object, without a specific property.
            /// </summary>
            /// <returns>An immutable list of <see cref="JsonObjectProperty"/>, built from the existing object, with the given property.</returns>
            /// <exception cref="InvalidOperationException">The value is not an object.</exception>
            """)
            .AppendIndent("public static ImmutableList<JsonObjectProperty> GetPropertyBackingWith(in ")
            .Append(dotnetTypeName)
            .Append(" that, ")
            .Append(parameterNameAndModifiers)
            .AppendLine(" name, in JsonAny value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalWrappedBackingValueLineIndent("Backing.Object", "return ", "objectBacking", ".SetItem(name, value);", identifier: "that")
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.JsonElement",
                    "return PropertyBackingBuilders.GetPropertyBackingBuilderReplacing(",
                    "jsonElementBacking",
                    ", name, value).ToImmutable();",
                    identifier: "that")
                .AppendSeparatorLine()
                .AppendLineIndent("throw new InvalidOperationException();")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendSetPropertyMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration, string propertyTypeName, bool isGeneric = false, bool isExplicit = false)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .AppendSeparatorLine();

        if (isGeneric)
        {
            generator.AppendLineIndent("/// <inheritdoc />");
        }
        else
        {
            generator
                .AppendBlockIndent(
                """
                /// <summary>
                /// Sets the given property value.
                /// </summary>
                /// <param name="name">The name of the property.</param>
                /// <param name="value">The value of the property.</param>
                /// <returns>The instance with the property set.</returns>
                """);
        }

        if (!isExplicit)
        {
            generator
                .AppendIndent("public ");
        }

        generator
            .Append(typeDeclaration.DotnetTypeName());

        if (isExplicit)
        {
            generator
                .Append(" IJsonObject<")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(">.");
        }
        else
        {
            generator
                .Append(' ');
        }

        generator
            .Append("SetProperty");

        if (isGeneric)
        {
            generator
                .Append('<')
                .Append(propertyTypeName)
                .Append('>');
        }

        generator
            .Append("(in JsonPropertyName name, ")
            .Append(propertyTypeName)
            .AppendLine(" value)");

        if (isGeneric && !isExplicit)
        {
            generator
                .PushIndent()
                .AppendIndent("where ")
                .Append(propertyTypeName)
                .AppendLine(" : struct, IJsonValue")
                .PopIndent();
        }

        return generator
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return new(__CorvusObjectHelpers.GetPropertyBackingWith(this, name, value.AsAny));")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendTryGetPropertyMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration, string propertyNameType)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.FallbackObjectPropertyType() is FallbackObjectPropertyType propertyType && !propertyType.ReducedType.IsBuiltInJsonAnyType())
        {
            generator
                .AppendTryGetPropertyMethod(typeDeclaration, propertyNameType, isExplicit: true)
                .AppendTryGetPropertyMethod(typeDeclaration, propertyNameType, isExplicit: false, propertyType.ReducedType.FullyQualifiedDotnetTypeName());
        }
        else
        {
            generator.AppendTryGetPropertyMethod(typeDeclaration, propertyNameType, isExplicit: false);
        }

        return generator;
    }

    private static CodeGenerator AppendTryGetPropertyForJsonPropertyNameMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.FallbackObjectPropertyType() is FallbackObjectPropertyType propertyType && !propertyType.ReducedType.IsBuiltInJsonAnyType())
        {
            generator
                .AppendTryGetPropertyForJsonPropertyNameMethod(typeDeclaration, isExplicit: true)
                .AppendTryGetPropertyForJsonPropertyNameMethod(typeDeclaration, isExplicit: false, propertyType.ReducedType.FullyQualifiedDotnetTypeName());
        }
        else
        {
            generator.AppendTryGetPropertyForJsonPropertyNameMethod(typeDeclaration, isExplicit: false);
        }

        return generator;
    }

    private static CodeGenerator AppendRemovePropertyMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration, string parameterTypeAndModifier)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc />")
            .AppendIndent("public ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" RemoveProperty(")
            .Append(parameterTypeAndModifier)
            .AppendLine(" name)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return new(__CorvusObjectHelpers.GetPropertyBackingWithout(this, name));")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendTryGetPropertyForJsonPropertyNameMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isExplicit, string? propertyTypeName = null, bool isGenericPropertyNameType = false)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .AppendSeparatorLine();

        if (isExplicit)
        {
            generator
                .AppendLineIndent("/// <inheritdoc />")
                .AppendIndent("bool IJsonObject<")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(">.");
        }
        else
        {
            if (!isGenericPropertyNameType)
            {
                generator
                    .AppendBlockIndent(
                    """
                    /// <summary>
                    /// Get a property.
                    /// </summary>
                    /// <param name="name">The name of the property.</param>
                    /// <param name="value">The value of the property.</param>
                    /// <returns><c>True</c> if the property was present.</returns>
                    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
                    """);
            }
            else
            {
                generator
                    .AppendLineIndent("/// <inheritdoc />");
            }

            generator
                .AppendIndent("public bool ");
        }

        generator
            .Append("TryGetProperty");

        if (isGenericPropertyNameType)
        {
            generator
                .Append('<')
                .Append(propertyTypeName)
                .Append('>');
        }

        generator
            .Append("(in JsonPropertyName name, out ");

        if (propertyTypeName is string pn)
        {
            generator
                .Append(pn);
        }
        else
        {
            generator
                .Append("JsonAny");
        }

        generator
            .AppendLine(" value)");

        if (isGenericPropertyNameType && !isExplicit)
        {
            generator
                .PushIndent()
                    .AppendIndent("where ")
                    .Append(propertyTypeName)
                    .Append(" : struct, IJsonValue<")
                    .Append(propertyTypeName)
                    .AppendLine(">")
                .PopIndent();
        }

        generator
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalBackingValueCallbackIndent("Backing.JsonElement", "jsonElementBacking", (g, f) => TryGetPropertyFromJsonElement(g, f, propertyTypeName, isGenericPropertyNameType))
                .AppendConditionalBackingValueCallbackIndent("Backing.Object", "objectBacking", (g, f) => TryGetPropertyFromImmutableDictionary(g, f, propertyTypeName, isGenericPropertyNameType))
                .AppendSeparatorLine()
                .AppendLineIndent("throw new InvalidOperationException();")
            .PopIndent()
            .AppendLineIndent("}");

        return generator;

        static void TryGetPropertyFromJsonElement(CodeGenerator generator, string fieldName, string? propertyTypeName, bool isGenericPropertyNameType)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .AppendLineIndent("if (this.", fieldName, ".ValueKind != JsonValueKind.Object)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("value = default;")
                    .AppendLineIndent("return false;")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendIndent("if (name.TryGetProperty(this.")
                .Append(fieldName)
                .AppendLine(", out JsonElement element))")
                .AppendLineIndent("{")
                .PushIndent();

            if (isGenericPropertyNameType)
            {
                generator
                    .AppendLine("#if NET8_0_OR_GREATER")
                    .AppendIndent("value = ")
                    .Append(propertyTypeName)
                    .AppendLine(".FromJson(element);")
                    .AppendLine("#else")
                    .AppendIndent("value = JsonValueNetStandard20Extensions.FromJsonElement<")
                    .Append(propertyTypeName)
                    .AppendLine(">(element);")
                    .AppendLine("#endif")
                    .AppendSeparatorLine();
            }
            else
            {
                generator
                    .AppendLineIndent("value = new(element);");
            }

            generator
                .AppendLineIndent("return true;")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("value = default;")
                .AppendLineIndent("return false;");
        }
    }

    private static CodeGenerator AppendTryGetPropertyMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration, string propertyNameType, bool isExplicit, string? propertyTypeName = null, bool isGenericPropertyNameType = false)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .AppendSeparatorLine();

        if (isExplicit)
        {
            generator
                .AppendLineIndent("/// <inheritdoc />")
                .AppendIndent("bool IJsonObject<")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(">.");
        }
        else
        {
            if (!isGenericPropertyNameType)
            {
                generator
                    .AppendBlockIndent(
                    """
                    /// <summary>
                    /// Get a property.
                    /// </summary>
                    /// <param name="name">The name of the property.</param>
                    /// <param name="value">The value of the property.</param>
                    /// <returns><c>True</c> if the property was present.</returns>
                    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
                    """);
            }
            else
            {
                generator
                    .AppendLineIndent("/// <inheritdoc />");
            }

            generator
                .AppendIndent("public bool ");
        }

        generator
            .Append("TryGetProperty");

        if (isGenericPropertyNameType)
        {
            generator
                .Append('<')
                .Append(propertyTypeName)
                .Append('>');
        }

        generator
            .Append('(')
            .Append(propertyNameType)
            .Append(" name, out ");

        if (propertyTypeName is string pn)
        {
            generator
                .Append(pn);
        }
        else
        {
            generator
                .Append("JsonAny");
        }

        generator
            .AppendLine(" value)");

        if (isGenericPropertyNameType && !isExplicit)
        {
            generator
                .PushIndent()
                    .AppendIndent("where ")
                    .Append(propertyTypeName)
                    .Append(" : struct, IJsonValue<")
                    .Append(propertyTypeName)
                    .AppendLine(">")
                .PopIndent();
        }

        generator
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalBackingValueCallbackIndent("Backing.JsonElement", "jsonElementBacking", (g, f) => TryGetPropertyFromJsonElement(g, f, propertyTypeName, isGenericPropertyNameType))
                .AppendConditionalBackingValueCallbackIndent("Backing.Object", "objectBacking", (g, f) => TryGetPropertyFromImmutableDictionary(g, f, propertyTypeName, isGenericPropertyNameType))
                .AppendSeparatorLine()
                .AppendLineIndent("throw new InvalidOperationException();")
            .PopIndent()
            .AppendLineIndent("}");

        return generator;

        static void TryGetPropertyFromJsonElement(CodeGenerator generator, string fieldName, string? propertyTypeName, bool isGenericPropertyNameType)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .AppendLineIndent("if (this.", fieldName, ".ValueKind != JsonValueKind.Object)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("value = default;")
                    .AppendLineIndent("return false;")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendIndent("if (this.")
                .Append(fieldName)
                .AppendLine(".TryGetProperty(name, out JsonElement element))")
                .AppendLineIndent("{")
                .PushIndent();

            if (isGenericPropertyNameType)
            {
                generator
                    .AppendLine("#if NET8_0_OR_GREATER")
                    .AppendIndent("value = ")
                    .Append(propertyTypeName)
                    .AppendLine(".FromJson(element);")
                    .AppendLine("#else")
                    .AppendIndent("value = JsonValueNetStandard20Extensions.FromJsonElement<")
                    .Append(propertyTypeName)
                    .AppendLine(">(element);")
                    .AppendLine("#endif")
                    .AppendSeparatorLine();
            }
            else
            {
                generator
                    .AppendLineIndent("value = new(element);");
            }

            generator
                .AppendLineIndent("return true;")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("value = default;")
                .AppendLineIndent("return false;");
        }
    }

    private static void TryGetPropertyFromImmutableDictionary(CodeGenerator generator, string fieldName, string? propertyTypeName, bool isGenericPropertyNameType)
    {
        if (generator.IsCancellationRequested)
        {
            return;
        }

        generator
            .AppendIndent("if (this.")
            .Append(fieldName)
            .AppendLine(".TryGetValue(name, out JsonAny result))")
            .AppendLineIndent("{")
            .PushIndent();

        if (isGenericPropertyNameType)
        {
            generator
                .AppendLine("#if NET8_0_OR_GREATER")
                    .AppendIndent("value = ")
                    .Append(propertyTypeName)
                    .AppendLine(".FromAny(result);")
                .AppendLine("#else")
                    .AppendIndent("value = result.As<")
                    .Append(propertyTypeName)
                    .AppendLine(">();")
                .AppendLine("#endif");
        }
        else
        {
            if (propertyTypeName is string ptn)
            {
                generator
                    .AppendIndent("value = ")
                    .Append(ptn)
                    .AppendLine(".FromAny(result);");
            }
            else
            {
                generator
                    .AppendLineIndent("value = result;");
            }
        }

        generator
            .AppendLineIndent("return true;")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendSeparatorLine()
            .AppendLineIndent("value = default;")
            .AppendLineIndent("return false;");
    }

    private static CodeGenerator AppendGenericTryGetPropertyMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration, string propertyNameType)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.FallbackObjectPropertyType() is not null)
        {
            generator
                .AppendTryGetPropertyMethod(typeDeclaration, propertyNameType, isExplicit: true, "TValue", isGenericPropertyNameType: true);
        }
        else
        {
            generator.AppendTryGetPropertyMethod(typeDeclaration, propertyNameType, isExplicit: false, "TValue", isGenericPropertyNameType: true);
        }

        return generator;
    }

    private static CodeGenerator AppendGenericTryGetPropertyForJsonPropertyNameMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.FallbackObjectPropertyType() is not null)
        {
            generator
                .AppendTryGetPropertyForJsonPropertyNameMethod(typeDeclaration, isExplicit: true, "TValue", isGenericPropertyNameType: true);
        }
        else
        {
            generator.AppendTryGetPropertyForJsonPropertyNameMethod(typeDeclaration, isExplicit: false, "TValue", isGenericPropertyNameType: true);
        }

        return generator;
    }

    private static CodeGenerator AppendHasPropertyMethod(this CodeGenerator generator, string propertyNameType)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc />")
            .AppendIndent("public bool HasProperty(")
            .Append(propertyNameType)
            .AppendLine(" name)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalWrappedBackingValueLineIndent("Backing.Object", "return ", "objectBacking", ".ContainsKey(name);")
                .AppendConditionalWrappedBackingValueLineIndent("Backing.JsonElement", "return ", "jsonElementBacking", ".TryGetProperty(name, out _);")
                .AppendSeparatorLine()
                .AppendLineIndent("throw new InvalidOperationException();")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendHasPropertyForJsonPropertyNameMethod(this CodeGenerator generator)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc />")
            .AppendLineIndent("public bool HasProperty(in JsonPropertyName name)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalWrappedBackingValueLineIndent("Backing.Object", "return ", "objectBacking", ".ContainsKey(name);")
                .AppendConditionalWrappedBackingValueLineIndent("Backing.JsonElement", "return name.TryGetProperty(", "jsonElementBacking", ", out JsonElement _);")
                .AppendSeparatorLine()
                .AppendLineIndent("throw new InvalidOperationException();")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendEnumerateObjectMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration, string? propertyTypeName = null, bool isExplicit = false)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .AppendSeparatorLine();

        if (isExplicit)
        {
            generator
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("JsonObjectEnumerator")
                .AppendGenericParameterIfRequired(propertyTypeName)
                .Append(" IJsonObject<")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(">.");
        }
        else
        {
            if (propertyTypeName is not null)
            {
                generator
                    .AppendBlockIndent(
                    """
                    /// <summary>
                    /// Enumerate the object.
                    /// </summary>
                    /// <returns>An enumerator for the object.</returns>
                    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
                    """);
            }
            else
            {
                generator
                    .AppendLineIndent("/// <inheritdoc/>");
            }

            generator
                .AppendIndent("public JsonObjectEnumerator")
                .AppendGenericParameterIfRequired(propertyTypeName)
                .Append(' ');
        }

        return generator
            .AppendLine("EnumerateObject()")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalConstructFromBacking("Backing.JsonElement", "jsonElementBacking")
                .AppendConditionalConstructFromBacking("Backing.Object", "objectBacking")
                .AppendSeparatorLine()
                .AppendLineIndent("throw new InvalidOperationException();")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendGenericParameterIfRequired(this CodeGenerator generator, string? propertyTypeName)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (propertyTypeName is string ptn)
        {
            generator
                .Append('<')
                .Append(ptn)
                .Append('>');
        }

        return generator;
    }

    private static CodeGenerator AppendObjectIndexer(this CodeGenerator generator, TypeDeclaration typeDeclaration, string propertyTypeName, bool isExplicit)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (!isExplicit)
        {
            generator
                .AppendIndent("public ")
                .Append(propertyTypeName)
                .Append(" ");
        }
        else
        {
            generator
                .AppendIndent("JsonAny")
                .Append(" IJsonObject<")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(">.");
        }

        return generator
            .AppendLine("this[in JsonPropertyName name]")
            .AppendLineIndent("{")
            .PushIndent()
            .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("if (this.TryGetProperty(name, out ")
                    .Append(propertyTypeName)
                    .AppendLine(" result))")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("return result;")
                    .PopIndent()
                    .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendReadOnlyDictionaryIndexer(this CodeGenerator generator, string propertyTypeName)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendIndent(propertyTypeName)
            .Append(" IReadOnlyDictionary<JsonPropertyName, ")
            .Append(propertyTypeName)
            .AppendLine(">.this[JsonPropertyName key] => this[key];");
    }

    private static CodeGenerator AppendReadOnlyDictionaryKeys(this CodeGenerator generator, string propertyTypeName)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendIndent("IEnumerable<JsonPropertyName> IReadOnlyDictionary<JsonPropertyName, ")
            .Append(propertyTypeName)
            .AppendLine(">.Keys")
            .AppendBlockIndent(
            """
            {
                get
                {
                    foreach(var property in this.EnumerateObject())
                    {
                        yield return property.Name;
                    }
                }
            }
            """);
    }

    private static CodeGenerator AppendReadOnlyDictionaryValues(this CodeGenerator generator, string propertyTypeName)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendIndent("IEnumerable<")
            .Append(propertyTypeName)
            .Append("> IReadOnlyDictionary<JsonPropertyName, ")
            .Append(propertyTypeName)
            .AppendLine(">.Values")
            .AppendBlockIndent(
            """
            {
                get
                {
                    foreach(var property in this.EnumerateObject())
                    {
                        yield return property.Value;
                    }
                }
            }
            """);
    }

    private static CodeGenerator AppendObsoleteAttribute(this CodeGenerator generator, PropertyDeclaration property)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (property.UnreducedPropertyType.IsDeprecated(out string? message) ||
            property.ReducedPropertyType.IsDeprecated(out message))
        {
            generator
                .AppendLineIndent(
                    "[Obsolete(",
                    SymbolDisplay.FormatLiteral(message ?? "This property is defined as deprecated in the JSON schema.", true),
                    ")]");
        }

        return generator;
    }

    private static CodeGenerator AppendReadOnlyDictionaryCount(this CodeGenerator generator, string propertyTypeName)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendIndent("int IReadOnlyCollection<KeyValuePair<JsonPropertyName, ")
            .Append(propertyTypeName)
            .AppendLine(">>.Count => this.Count;");
    }
}