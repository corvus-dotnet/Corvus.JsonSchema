// <copyright file="CodeGeneratorExtensions.Object.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
        return generator
            .ReserveNameIfNotReserved("AsPropertyBacking")
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <inheritdoc/>
            public ImmutableList<JsonObjectProperty> AsPropertyBacking()
            {
                return __CorvusObjectHelpers.GetPropertyBacking();
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
        if (typeDeclaration.ObjectPropertyType() is ObjectPropertyTypeDeclaration propertyType)
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
    /// Append object indexer properties.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the indexers.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendObjectIndexerProperties(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendSeparatorLine();

        if (typeDeclaration.ObjectPropertyType() is ObjectPropertyTypeDeclaration propertyType)
        {
            generator
                .AppendLineIndent(
                    "/// <inheritdoc/>")
                .AppendObjectIndexer(typeDeclaration, WellKnownTypeDeclarations.JsonAny.FullyQualifiedDotnetTypeName(), isExplicit: true)
                .AppendSeparatorLine()
                .AppendBlockIndent(
                """
                    /// <summary>
                    /// Gets the property with the given name.
                    /// </summary>
                    /// <param name="name">The name of the property to retrieve.</param>
                    /// <returns>The value of thee property with the given name.</returns>
                    /// <exception cref="IndexOutOfRangeException">The given property was not present on the object.</exception>
                    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
                    """)
                .AppendObjectIndexer(typeDeclaration, propertyType.ReducedType.DotnetTypeName(), isExplicit: false);
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
        generator
            .ReserveNameIfNotReserved("EnumerateObject");

        if (typeDeclaration.ObjectPropertyType() is ObjectPropertyTypeDeclaration propertyType)
        {
            string propertyTypeName = propertyType.ReducedType.FullyQualifiedDotnetTypeName();

            generator
                .AppendObjectEnumerator(typeDeclaration, isExplicit: true)
                .AppendObjectEnumerator(typeDeclaration, propertyTypeName);
        }
        else
        {
            generator
                .AppendObjectEnumerator(typeDeclaration);
        }

        return generator;
    }

    /// <summary>
    /// Append the <c>FromProperties()</c> factory methods.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the factory methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendFromPropertiesFactoryMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .ReserveNameIfNotReserved("FromProperties");

        if (typeDeclaration.ObjectPropertyType() is ObjectPropertyTypeDeclaration propertyType)
        {
            string propertyTypeName = propertyType.ReducedType.FullyQualifiedDotnetTypeName();

            generator
                .AppendSeparatorLine()
                .AppendLine("#if NET8_0_OR_GREATER")
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendIndent("static ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append("IJsonObject<")
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
                .Append("IJsonObject<")
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
                    return new(source.Select(s => new JsonObjectProperty(s.Name, s.Value)).ToImmutableList());
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
    /// <param name="typeDeclaration">The type declaration for which to emit the method.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendHasPropertiesMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveNameIfNotReserved("HasProperties")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent("public bool HasProperties()")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalWrappedBackingValueLineIndent("Backing.Object", "return ", "objectBacking", ".Count > 0")
                .AppendConditionalBackingValueCallbackIndent("Backing.JsonElement", "jsonElementBacking", TestEnumerable)
                .AppendSeparatorLine()
                .AppendLineIndent("throw new InvalidOperationException();")
            .PopIndent()
            .AppendLineIndent("}");

        static void TestEnumerable(CodeGenerator generator, string fieldName)
        {
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
    /// <param name="typeDeclaration">The type declaration for which to emit the methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendHasPropertyMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveNameIfNotReserved("HasProperties")
            .AppendHasPropertyMethod("in JsonPropertyName")
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
        return generator
            .ReserveNameIfNotReserved("TryGetProperty")
            .AppendTryGetPropertyMethod(typeDeclaration, "in JsonPropertyName")
            .AppendTryGetPropertyMethod(typeDeclaration, "string")
            .AppendTryGetPropertyMethod(typeDeclaration, "ReadOnlySpan<char>")
            .AppendTryGetPropertyMethod(typeDeclaration, "ReadOnlySpan<byte>")
            .AppendGenericTryGetPropertyMethod(typeDeclaration, "in JsonPropertyName")
            .AppendGenericTryGetPropertyMethod(typeDeclaration, "string")
            .AppendGenericTryGetPropertyMethod(typeDeclaration, "ReadOnlySpan<char>")
            .AppendGenericTryGetPropertyMethod(typeDeclaration, "ReadOnlySpan<byte>");
    }

    private static CodeGenerator AppendTryGetPropertyMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration, string propertyNameType)
    {
        if (typeDeclaration.ObjectPropertyType() is ObjectPropertyTypeDeclaration propertyType)
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

    private static CodeGenerator AppendTryGetPropertyMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration, string propertyNameType, bool isExplicit, string? propertyTypeName = null, bool isGenericPropertyNameType = false)
    {
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
                .AppendConditionalBackingValueCallbackIndent("Backing.Object", "objectBacking", (g, f) => TryGetPropertyFromJsonAny(g, f, propertyTypeName, isGenericPropertyNameType))
                .AppendSeparatorLine()
                .AppendLineIndent("throw new InvalidOperationException();")
            .PopIndent()
            .AppendLineIndent("}");

        return generator;

        static void TryGetPropertyFromJsonElement(CodeGenerator generator, string fieldName, string? propertyTypeName, bool isGenericPropertyNameType)
        {
            generator
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

        static void TryGetPropertyFromJsonAny(CodeGenerator generator, string fieldName, string? propertyTypeName, bool isGenericPropertyNameType)
        {
            generator
                .AppendIndent("if (this.")
                .Append(fieldName)
                .AppendLine(".TryGetProperty(name, out JsonAny result))")
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
                        .AppendLineIndent("value = new(result);");
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
    }

    private static CodeGenerator AppendGenericTryGetPropertyMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration, string propertyNameType)
    {
        if (typeDeclaration.ObjectPropertyType() is not null)
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

    private static CodeGenerator AppendHasPropertyMethod(this CodeGenerator generator, string propertyNameType)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc />")
            .AppendIndent("public bool HasProperty(")
            .Append(propertyNameType)
            .AppendLine(" name)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalWrappedBackingValueLineIndent("Backing.Object", "return ", "objectBacking", ".ContainsKey(name);")
                .AppendConditionalWrappedBackingValueLineIndent("Backing.JsonElement", "return", "jsonElementBacking", ".TryGetProperty(name, out _);")
                .AppendSeparatorLine()
                .AppendLineIndent("throw new InvalidOperationException();")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendObjectEnumerator(this CodeGenerator generator, TypeDeclaration typeDeclaration, string? propertyTypeName = null, bool isExplicit = false)
    {
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
                .AppendIndent(propertyTypeName)
                .Append(" IJsonObject<")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(">.");
        }

        return generator
            .AppendLine("this[JsonPropertyName name]")
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
        return generator
            .AppendIndent(propertyTypeName)
            .Append(" IReadOnlyDictionary<JsonPropertyName, ")
            .Append(propertyTypeName)
            .AppendLine(">.this[JsonPropertyName key] => this[key];");
    }

    private static CodeGenerator AppendReadOnlyDictionaryKeys(this CodeGenerator generator, string propertyTypeName)
    {
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

    private static CodeGenerator AppendReadOnlyDictionaryCount(this CodeGenerator generator, string propertyTypeName)
    {
        return generator
            .AppendIndent("int IReadOnlyDictionary<JsonPropertyName, ")
            .Append(propertyTypeName)
            .AppendLine(">.Count")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalBackingValueCallbackIndent("Backing.JsonElement", "jsonElementBacking", CountProperties)
                    .AppendConditionalWrappedBackingValueLineIndent("Backing.Object", "return ", "objectBacking", ".Count;")
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException()")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");

        static void CountProperties(CodeGenerator generator, string fieldName)
        {
            generator
                .AppendLineIndent("int count = 0;")
                .AppendIndent("foreach (var _ in this.")
                .Append(fieldName)
                .AppendLine(".EnumerateObject())")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("count++;")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLineIndent("return count;");
        }
    }
}