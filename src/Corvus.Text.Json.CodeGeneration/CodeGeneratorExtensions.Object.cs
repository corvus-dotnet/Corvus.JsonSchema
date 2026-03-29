// <copyright file="CodeGeneratorExtensions.Object.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Corvus.Json.CodeGeneration;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Code generator extensions for object-related functionality.
/// </summary>
internal static partial class CodeGeneratorExtensions
{
    /// <summary>
    /// Appends methods to apply object composition types.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to apply composition types.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendApplyObjectCompositionTypes(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        HashSet<TypeDeclaration> seenTypes = [];

        string applyMethodName = generator.GetUniqueMethodNameInScope("Apply");

        foreach (TypeDeclaration? type in typeDeclaration.AllOfCompositionTypes().Values.SelectMany(t => t).Select(t => t.ReducedTypeDeclaration().ReducedType))
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (type is TypeDeclaration t && seenTypes.Add(t))
            {
                AppendApply(generator, t, applyMethodName);
            }
        }

        foreach (TypeDeclaration? type in typeDeclaration.AnyOfCompositionTypes().Values.SelectMany(t => t).Select(t => t.ReducedTypeDeclaration().ReducedType))
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (type is TypeDeclaration t && seenTypes.Add(t))
            {
                AppendApply(generator, t, applyMethodName);
            }
        }

        foreach (TypeDeclaration? type in typeDeclaration.OneOfCompositionTypes().Values.SelectMany(t => t).Select(t => t.ReducedTypeDeclaration().ReducedType))
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (type is TypeDeclaration t && seenTypes.Add(t))
            {
                AppendApply(generator, t, applyMethodName);
            }
        }

        return generator;

        static CodeGenerator AppendApply(CodeGenerator generator, TypeDeclaration typeDeclaration, string applyMethodName)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if ((typeDeclaration.ImpliedCoreTypesOrAny() & CoreTypes.Object) == 0 || typeDeclaration.DoNotGenerate())
            {
                return generator;
            }

            if (typeDeclaration.FallbackObjectPropertyType() is FallbackObjectPropertyType objectPropertyType)
            {
                if (objectPropertyType.ReducedType.IsBuiltInJsonNotAnyType())
                {
                    return generator;
                }
            }
            else if (typeDeclaration.LocalEvaluatedPropertyType() is FallbackObjectPropertyType localObjectPropertyType)
            {
                if (localObjectPropertyType.ReducedType.IsBuiltInJsonNotAnyType())
                {
                    return generator;
                }
            }
            else if (typeDeclaration.LocalAndAppliedEvaluatedPropertyType() is FallbackObjectPropertyType localAndAppliedObjectPropertyType)
            {
                if (localAndAppliedObjectPropertyType.ReducedType.IsBuiltInJsonNotAnyType())
                {
                    return generator;
                }
            }

            string fqdtn = typeDeclaration.FullyQualifiedDotnetTypeName();

            return generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Apply a composed value.")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent("/// <remarks>")
                .AppendLineIndent("/// This will add or update any property values provided by the <paramref name=\"value\"/>.")
                .AppendLineIndent("/// </remarks>")
                .AppendLineIndent("public void ", applyMethodName, "(in ", fqdtn, " value)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("CheckValidInstance();")
                    .AppendLineIndent("")
                    .AppendLineIndent("foreach (var property in value.EnumerateObject())")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("JsonElementHelpers.SetPropertyUnsafe(this, property);")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendLineIndent("")
                    .AppendLineIndent("_documentVersion = _parent.Version;")
                .PopIndent()
                .AppendLineIndent("}");
        }
    }

    /// <summary>
    /// Append EnumerateObject() method.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the indexers.</param>
    /// <param name="forMutable">Indicates that the value is intended for a mutable instance.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendEnumerateObject(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool forMutable = false)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if ((typeDeclaration.ImpliedCoreTypesOrAny() & CoreTypes.Object) == 0)
        {
            return generator;
        }

        string fqdtn;

        if (typeDeclaration.FallbackObjectPropertyType() is FallbackObjectPropertyType objectPropertyType)
        {
            if (objectPropertyType.ReducedType.IsBuiltInJsonNotAnyType())
            {
                return generator;
            }

            fqdtn = objectPropertyType.ReducedType.FullyQualifiedDotnetTypeName();
        }
        else if (typeDeclaration.LocalEvaluatedPropertyType() is FallbackObjectPropertyType localObjectPropertyType)
        {
            if (localObjectPropertyType.ReducedType.IsBuiltInJsonNotAnyType())
            {
                return generator;
            }

            fqdtn = localObjectPropertyType.ReducedType.FullyQualifiedDotnetTypeName();
        }
        else if (typeDeclaration.LocalAndAppliedEvaluatedPropertyType() is FallbackObjectPropertyType localAndAppliedObjectPropertyType)
        {
            if (localAndAppliedObjectPropertyType.ReducedType.IsBuiltInJsonNotAnyType())
            {
                return generator;
            }

            fqdtn = localAndAppliedObjectPropertyType.ReducedType.FullyQualifiedDotnetTypeName();
        }
        else
        {
            fqdtn = WellKnownTypeDeclarations.JsonAny.DotnetTypeName();
        }

        if (forMutable)
        {
            fqdtn += ".Mutable";
        }

        return generator
            .AppendSeparatorLine()
            .ReserveName("EnumerateObject")
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Enumerates the object.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">The value is not an object.</exception>")
            .AppendLineIndent("public ObjectEnumerator<", fqdtn, "> EnumerateObject()")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("CheckValidInstance();")
                .AppendLineIndent("return EnumeratorCreator.CreateObjectEnumerator<", fqdtn, ">(_parent, _idx);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append GetPropertyCount() method.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the indexers.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendGetPropertyCount(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if ((typeDeclaration.ImpliedCoreTypesOrAny() & CoreTypes.Object) == 0)
        {
            return generator;
        }

        return generator
            .AppendSeparatorLine()
            .ReserveName("GetPropertyCount")
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Gets the number of properties in the object.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">The value is not an object.</exception>")
            .AppendLineIndent("public int GetPropertyCount()")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("CheckValidInstance();")
                .AppendLineIndent("return _parent.GetPropertyCount(_idx);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends a JsonPropertyNames nested class containing property name constants.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property names class.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendJsonPropertyNames(this CodeGenerator generator, TypeDeclaration typeDeclaration)
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
            .EndClassStructOrEnumDeclaration();
    }

    /// <summary>
    /// Appends a JsonPropertyNamesEscaped nested class containing escaped property name constants.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the escaped property names class.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendJsonPropertyNamesEscaped(this CodeGenerator generator, TypeDeclaration typeDeclaration)
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
            .AppendLineIndent("/// Provides escaped UTF-8 versions of the JSON property names on the object.")
            .AppendLineIndent("/// </summary>")
            .BeginPrivateStaticClassDeclaration(generator.JsonPropertyNamesEscapedClassName());

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

            string encodedName = JavaScriptEncoder.Default.Encode(property.JsonPropertyName);

            generator
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Gets the escaped UTF-8 JSON property name for <see cref=\"", property.DotnetPropertyName(), "\"/>.")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent(
                    "public static ReadOnlySpan<byte> ",
                    property.DotnetPropertyName(),
                    " => ",
                    SymbolDisplay.FormatLiteral(encodedName, true),
                    "u8;");
            i++;
        }

        return generator
            .EndClassStructOrEnumDeclaration();
    }

    /// <summary>
    /// Append object indexer properties.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the indexers.</param>
    /// <param name="forMutable">Whether to emit the indexers for the mutable element.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendObjectIndexerProperties(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool forMutable = false)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if ((typeDeclaration.ImpliedCoreTypesOrAny() & CoreTypes.Object) == 0)
        {
            return generator;
        }

        string fqdtn;

        if (typeDeclaration.FallbackObjectPropertyType() is FallbackObjectPropertyType objectPropertyType)
        {
            if (objectPropertyType.ReducedType == WellKnownTypeDeclarations.JsonNotAny)
            {
                return generator;
            }

            fqdtn = objectPropertyType.ReducedType.FullyQualifiedDotnetTypeName();
        }
        else if (typeDeclaration.LocalEvaluatedPropertyType() is FallbackObjectPropertyType localObjectPropertyType)
        {
            if (localObjectPropertyType.ReducedType == WellKnownTypeDeclarations.JsonNotAny)
            {
                return generator;
            }

            fqdtn = localObjectPropertyType.ReducedType.FullyQualifiedDotnetTypeName();
        }
        else if (typeDeclaration.LocalAndAppliedEvaluatedPropertyType() is FallbackObjectPropertyType localAndAppliedObjectPropertyType)
        {
            if (localAndAppliedObjectPropertyType.ReducedType == WellKnownTypeDeclarations.JsonNotAny)
            {
                return generator;
            }

            fqdtn = localAndAppliedObjectPropertyType.ReducedType.FullyQualifiedDotnetTypeName();
        }
        else
        {
            fqdtn = WellKnownTypeDeclarations.JsonAny.DotnetTypeName();
        }

        AppendPropertyIndexer(generator, fqdtn, "ReadOnlySpan<byte>", forMutable);
        AppendPropertyIndexer(generator, fqdtn, "ReadOnlySpan<char>", forMutable);
        AppendPropertyIndexer(generator, fqdtn, "string", forMutable);

        return generator;

        static void AppendPropertyIndexer(CodeGenerator generator, string fqdtn, string propertyNameType, bool forMutable) => generator
                            .AppendSeparatorLine()
                            .AppendBlockIndent(
                            """
                            /// <summary>
                            /// Gets the value of the property with the given name.
                            /// </summary>
                            /// <param name="propertyName">The name of the property.</param>
                            /// <returns>The value of the property with the given name.</returns>
                            /// <exception cref="InvalidOperationException">The value is not an object.</exception>
                            """)
                            .AppendLineIndent("public ", fqdtn, forMutable ? ".Mutable" : "", " this[", propertyNameType, " propertyName]")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent("get")
                                .AppendLineIndent("{")
                                .PushIndent()
                                    .AppendLineIndent("CheckValidInstance();")
                                    .AppendLineIndent("if (!_parent.TryGetNamedPropertyValue(_idx, propertyName, out ", fqdtn, forMutable ? ".Mutable" : "", " value))")
                                    .AppendLineIndent("{")
                                    .PushIndent()
                                        .AppendLineIndent("return default;")
                                    .PopIndent()
                                    .AppendLineIndent("}")
                                    .AppendSeparatorLine()
                                    .AppendLineIndent("return value;")
                                .PopIndent()
                                .AppendLineIndent("}")
                            .PopIndent()
                            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append TryGetProperty methods for open object types.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the methods.</param>
    /// <param name="forMutable">Whether to emit the methods for the mutable element.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendObjectTryGetPropertyMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool forMutable = false)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if ((typeDeclaration.ImpliedCoreTypesOrAny() & CoreTypes.Object) == 0)
        {
            return generator;
        }

        string fqdtn;

        if (typeDeclaration.FallbackObjectPropertyType() is FallbackObjectPropertyType objectPropertyType)
        {
            if (objectPropertyType.ReducedType == WellKnownTypeDeclarations.JsonNotAny)
            {
                // additionalProperties: only local pattern properties are visible
                if (!typeDeclaration.HasLocalPatternProperties())
                {
                    return generator;
                }

                fqdtn = WellKnownTypeDeclarations.JsonAny.DotnetTypeName();
            }
            else
            {
                fqdtn = objectPropertyType.ReducedType.FullyQualifiedDotnetTypeName();
            }
        }
        else if (typeDeclaration.LocalEvaluatedPropertyType() is FallbackObjectPropertyType localObjectPropertyType)
        {
            if (localObjectPropertyType.ReducedType == WellKnownTypeDeclarations.JsonNotAny)
            {
                // additionalProperties with local evaluation: only local pattern properties are visible
                if (!typeDeclaration.HasLocalPatternProperties())
                {
                    return generator;
                }

                fqdtn = WellKnownTypeDeclarations.JsonAny.DotnetTypeName();
            }
            else
            {
                fqdtn = localObjectPropertyType.ReducedType.FullyQualifiedDotnetTypeName();
            }
        }
        else if (typeDeclaration.LocalAndAppliedEvaluatedPropertyType() is FallbackObjectPropertyType localAndAppliedObjectPropertyType)
        {
            if (localAndAppliedObjectPropertyType.ReducedType == WellKnownTypeDeclarations.JsonNotAny)
            {
                // unevaluatedProperties: composed pattern properties are also visible
                if (!typeDeclaration.ImpliedPatternProperties())
                {
                    return generator;
                }

                fqdtn = WellKnownTypeDeclarations.JsonAny.DotnetTypeName();
            }
            else
            {
                fqdtn = localAndAppliedObjectPropertyType.ReducedType.FullyQualifiedDotnetTypeName();
            }
        }
        else
        {
            fqdtn = WellKnownTypeDeclarations.JsonAny.DotnetTypeName();
        }

        AppendTryGetPropertyMethod(generator, fqdtn, "ReadOnlySpan<byte>", forMutable);
        AppendTryGetPropertyMethod(generator, fqdtn, "ReadOnlySpan<char>", forMutable);
        AppendTryGetPropertyMethod(generator, fqdtn, "string", forMutable);

        return generator;

        static void AppendTryGetPropertyMethod(CodeGenerator generator, string fqdtn, string propertyNameType, bool forMutable) => generator
                            .AppendSeparatorLine()
                            .AppendBlockIndent(
                            """
                            /// <summary>
                            /// Tries to get the value of the property with the given name.
                            /// </summary>
                            /// <param name="propertyName">The name of the property.</param>
                            /// <param name="value">The value of the property, if present.</param>
                            /// <returns><see langword="true"/> if the property was found, otherwise <see langword="false"/>.</returns>
                            /// <exception cref="InvalidOperationException">The value is not an object.</exception>
                            """)
                            .AppendLineIndent("public bool TryGetProperty(", propertyNameType, " propertyName, out ", fqdtn, forMutable ? ".Mutable" : "", " value)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent("CheckValidInstance();")
                                .AppendLineIndent("return _parent.TryGetNamedPropertyValue(_idx, propertyName, out value);")
                            .PopIndent()
                            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append object properties.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the indexers.</param>
    /// <param name="forMutable">Whether to emit the indexers for the mutable element.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendObjectProperties(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool forMutable = false)
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
            string propertyType = $"{property.ReducedPropertyType.FullyQualifiedDotnetTypeName()}{(forMutable ? ".Mutable" : "")}";

            string immutableTypeName = property.ReducedPropertyType.FullyQualifiedDotnetTypeName();
            bool hasDefaultValue = !forMutable && property.ReducedPropertyType.DefaultValue().ValueKind != JsonValueKind.Undefined;

            generator
                .AppendSeparatorLine()
                .AppendPropertyDocumentation(property)
                .AppendObsoleteAttribute(property)
                .BeginPublicReadOnlyPropertyDeclaration(propertyType, property.DotnetPropertyName(), isNullable)
                    .AppendLineIndent("if (_parent.TryGetNamedPropertyValue(_idx, ", generator.JsonPropertyNamesClassName(), ".", property.DotnetPropertyName(), "Utf8, out ", propertyType, " value))")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("return value;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent(hasDefaultValue ? $"return {immutableTypeName}.DefaultInstance;" : "return default;")
                .EndReadOnlyPropertyDeclaration();
        }

        return generator;
    }

    public static CodeGenerator AppendObjectPropertySetters(this CodeGenerator generator, TypeDeclaration typeDeclaration)
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

            // Don't emit a setter for boolean false properties, as they cannot be set!
            if (property.ReducedPropertyType.IsBuiltInJsonNotAnyType())
            {
                continue;
            }

            string propertyTypeName = property.ReducedPropertyType.FullyQualifiedDotnetTypeName();

            string name = SymbolDisplay.FormatLiteral(property.JsonPropertyName, true);
            name = name.Substring(1, name.Length - 2);
            bool requiresEncoding = JavaScriptEncoder.Default.Encode(property.JsonPropertyName) != property.JsonPropertyName;
            bool isOptional = property.RequiredOrOptional == RequiredOrOptional.Optional;

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Set the <c>", name, "</c> property.")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent("/// <param name=\"value\">The value of the property to add.</param>")
                .AppendLineIndent("public void Set", property.DotnetPropertyName(), "(in ", propertyTypeName, ".", generator.SourceClassName(propertyTypeName), " value)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("CheckValidInstance();")
                    .AppendSeparatorLine();

            if (isOptional)
            {
                generator
                    .AppendLineIndent("if (value.IsUndefined)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("JsonElementHelpers.RemovePropertyUnsafe(_parent, _idx, ", generator.JsonPropertyNamesClassName(), ".", property.DotnetPropertyName(), "Utf8);")
                        .AppendLineIndent("_documentVersion = _parent.Version;")
                        .AppendLineIndent("return;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine();
            }
            else
            {
                generator
                    .AppendLineIndent("if (value.IsUndefined)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("CodeGenThrowHelper.ThrowInvalidOperationException_SetRequiredPropertyToUndefined(\"", name, "\");")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine();
            }

            generator
                    .AppendLineIndent("ComplexValueBuilder cvb = ComplexValueBuilder.Create(_parent, 2);")
                    .AppendLineIndent("if (_parent.TryGetNamedPropertyValue(_idx, ", generator.JsonPropertyNamesClassName(), ".", property.DotnetPropertyName(), "Utf8, out IJsonDocument? elementParent, out int elementIdx))")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("// We are going to replace just the value")
                        .AppendLineIndent("value.AddAsItem(ref cvb);")
                        .AppendLineIndent("_parent.OverwriteAndDispose(_idx, elementIdx, elementIdx + elementParent.GetDbSize(elementIdx, true), 1, ref cvb);")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendLineIndent("else")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("// We are going to insert the new value")
                        .AppendLineIndent("value.AddAsProperty(", generator.JsonPropertyNamesEscapedClassName(), ".", property.DotnetPropertyName(), ", ref cvb, escapeName: false, nameRequiresUnescaping: ", requiresEncoding ? "true" : "false", ");")
                        .AppendLineIndent("int endIndex = _idx + _parent.GetDbSize(_idx, false);")
                        .AppendLineIndent("_parent.InsertAndDispose(_idx, endIndex, ref cvb);")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("_documentVersion = _parent.Version;")
                .PopIndent()
                .AppendLineIndent("}");

            if ((property.ReducedPropertyType.ImpliedCoreTypesOrAny() & (CoreTypes.Object | CoreTypes.Array)) != 0)
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("/// <summary>")
                    .AppendLineIndent("/// Set the <c>", name, "</c> property.")
                    .AppendLineIndent("/// </summary>")
                    .AppendLineIndent("/// <param name=\"value\">The value of the property to add.</param>")
                    .AppendLineIndent("public void Set", property.DotnetPropertyName(), "<TContext>(in ", propertyTypeName, ".", generator.SourceClassName(propertyTypeName), "<TContext> value)")
                    .AppendLine("#if NET9_0_OR_GREATER")
                    .PushIndent()
                        .AppendLineIndent("where TContext : allows ref struct")
                    .PopIndent()
                    .AppendLine("#endif")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("CheckValidInstance();")
                        .AppendSeparatorLine();

                if (isOptional)
                {
                    generator
                        .AppendLineIndent("if (value.IsUndefined)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("JsonElementHelpers.RemovePropertyUnsafe(_parent, _idx, ", generator.JsonPropertyNamesClassName(), ".", property.DotnetPropertyName(), "Utf8);")
                            .AppendLineIndent("_documentVersion = _parent.Version;")
                            .AppendLineIndent("return;")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendSeparatorLine();
                }
                else
                {
                    generator
                        .AppendLineIndent("if (value.IsUndefined)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("CodeGenThrowHelper.ThrowInvalidOperationException_SetRequiredPropertyToUndefined(\"", name, "\");")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendSeparatorLine();
                }

                generator
                        .AppendLineIndent("ComplexValueBuilder cvb = ComplexValueBuilder.Create(_parent, 2);")
                        .AppendLineIndent("if (_parent.TryGetNamedPropertyValue(_idx, ", generator.JsonPropertyNamesClassName(), ".", property.DotnetPropertyName(), "Utf8, out IJsonDocument? elementParent, out int elementIdx))")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("// We are going to replace just the value")
                            .AppendLineIndent("value.AddAsItem(ref cvb);")
                            .AppendLineIndent("_parent.OverwriteAndDispose(_idx, elementIdx, elementIdx + elementParent.GetDbSize(elementIdx, true), 1, ref cvb);")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendLineIndent("else")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("// We are going to insert the new value")
                            .AppendLineIndent("value.AddAsProperty(", generator.JsonPropertyNamesEscapedClassName(), ".", property.DotnetPropertyName(), ", ref cvb, escapeName: false, nameRequiresUnescaping: ", requiresEncoding ? "true" : "false", ");")
                            .AppendLineIndent("int endIndex = _idx + _parent.GetDbSize(_idx, false);")
                            .AppendLineIndent("_parent.InsertAndDispose(_idx, endIndex, ref cvb);")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendSeparatorLine()
                        .AppendLineIndent("_documentVersion = _parent.Version;")
                    .PopIndent()
                    .AppendLineIndent("}");
            }

            // For optional properties, also generate a Remove[PropertyName] method
            if (isOptional)
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("/// <summary>")
                    .AppendLineIndent("/// Remove the <c>", name, "</c> property, if present.")
                    .AppendLineIndent("/// </summary>")
                    .AppendLineIndent("/// <returns><see langword=\"true\"/> if the property was found and removed; otherwise, <see langword=\"false\"/>.</returns>")
                    .AppendLineIndent("public bool Remove", property.DotnetPropertyName(), "()")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("CheckValidInstance();")
                        .AppendLineIndent("bool result = JsonElementHelpers.RemovePropertyUnsafe(_parent, _idx, ", generator.JsonPropertyNamesClassName(), ".", property.DotnetPropertyName(), "Utf8);")
                        .AppendLineIndent("_documentVersion = _parent.Version;")
                        .AppendLineIndent("return result;")
                    .PopIndent()
                    .AppendLineIndent("}");
            }
        }

        return generator;
    }

    /// <summary>
    /// Append mutation methods for objects.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the mutators.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendObjectMutators(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if ((typeDeclaration.ImpliedCoreTypes() & CoreTypes.Object) == 0)
        {
            return generator;
        }

        // Get the type for the object properties.
        string fqdtn;

        if (typeDeclaration.FallbackObjectPropertyType() is FallbackObjectPropertyType objectPropertyType)
        {
            if (objectPropertyType.ReducedType == WellKnownTypeDeclarations.JsonNotAny)
            {
                // additionalProperties: only local pattern properties are visible
                if (!typeDeclaration.HasLocalPatternProperties())
                {
                    return generator;
                }

                fqdtn = WellKnownTypeDeclarations.JsonAny.DotnetTypeName();
            }
            else
            {
                fqdtn = objectPropertyType.ReducedType.FullyQualifiedDotnetTypeName();
            }
        }
        else if (typeDeclaration.LocalEvaluatedPropertyType() is FallbackObjectPropertyType localObjectPropertyType)
        {
            if (localObjectPropertyType.ReducedType == WellKnownTypeDeclarations.JsonNotAny)
            {
                // additionalProperties with local evaluation: only local pattern properties are visible
                if (!typeDeclaration.HasLocalPatternProperties())
                {
                    return generator;
                }

                fqdtn = WellKnownTypeDeclarations.JsonAny.DotnetTypeName();
            }
            else
            {
                fqdtn = localObjectPropertyType.ReducedType.FullyQualifiedDotnetTypeName();
            }
        }
        else if (typeDeclaration.LocalAndAppliedEvaluatedPropertyType() is FallbackObjectPropertyType localAndAppliedObjectPropertyType)
        {
            if (localAndAppliedObjectPropertyType.ReducedType == WellKnownTypeDeclarations.JsonNotAny)
            {
                // unevaluatedProperties: composed pattern properties are also visible
                if (!typeDeclaration.ImpliedPatternProperties())
                {
                    return generator;
                }

                fqdtn = WellKnownTypeDeclarations.JsonAny.DotnetTypeName();
            }
            else
            {
                fqdtn = localAndAppliedObjectPropertyType.ReducedType.FullyQualifiedDotnetTypeName();
            }
        }
        else
        {
            fqdtn = WellKnownTypeDeclarations.JsonAny.DotnetTypeName();
        }

        // SetProperty(string) - delegates to ReadOnlySpan<char>
        generator
            .ReserveNameIfNotReserved("SetProperty")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("///   Sets a property on this JSON object element.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"propertyName\">The name of the property to set.</param>")
            .AppendLineIndent("/// <param name=\"value\">The value of the property to set.</param>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">")
            .AppendLineIndent("///   This element's <see cref=\"ValueKind\"/> is not <see cref=\"JsonValueKind.Object\"/>,")
            .AppendLineIndent("///   or the element reference is stale due to document mutations.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ObjectDisposedException\">")
            .AppendLineIndent("///   The parent <see cref=\"JsonDocument\"/> has been disposed.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <remarks>")
            .AppendLineIndent("///   <para>")
            .AppendLineIndent("///     If the property already exists, its value will be replaced.")
            .AppendLineIndent("///     If the property doesn't exist, it will be added to the object.")
            .AppendLineIndent("///   </para>")
            .AppendLineIndent("/// </remarks>")
            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLineIndent("public void SetProperty(string propertyName, in ", fqdtn, ".", generator.SourceClassName(fqdtn), " value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("SetProperty(propertyName.AsSpan(), value);")
            .PopIndent()
            .AppendLineIndent("}");

        // SetProperty(ReadOnlySpan<char>) - full implementation
        generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("///   Sets a property on this JSON object element.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"propertyName\">The name of the property to set.</param>")
            .AppendLineIndent("/// <param name=\"value\">The value of the property to set.</param>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">")
            .AppendLineIndent("///   This element's <see cref=\"ValueKind\"/> is not <see cref=\"JsonValueKind.Object\"/>,")
            .AppendLineIndent("///   or the element reference is stale due to document mutations.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ObjectDisposedException\">")
            .AppendLineIndent("///   The parent <see cref=\"JsonDocument\"/> has been disposed.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <remarks>")
            .AppendLineIndent("///   <para>")
            .AppendLineIndent("///     If the property already exists, its value will be replaced.")
            .AppendLineIndent("///     If the property doesn't exist, it will be added to the object.")
            .AppendLineIndent("///   </para>")
            .AppendLineIndent("/// </remarks>")
            .AppendLineIndent("public void SetProperty(ReadOnlySpan<char> propertyName, in ", fqdtn, ".", generator.SourceClassName(fqdtn), " value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("CheckValidInstance();")
                .AppendSeparatorLine()
                .AppendLineIndent("if (value.IsUndefined)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("JsonElementHelpers.RemovePropertyUnsafe(_parent, _idx, propertyName);")
                    .AppendLineIndent("_documentVersion = _parent.Version;")
                    .AppendLineIndent("return;")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("ComplexValueBuilder cvb = ComplexValueBuilder.Create(_parent, 2);")
                .AppendLineIndent("if (_parent.TryGetNamedPropertyValue(_idx, propertyName, out IJsonDocument? elementParent, out int elementIdx))")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("// We are going to replace just the value")
                    .AppendLineIndent("value.AddAsItem(ref cvb);")
                    .AppendLineIndent("_parent.OverwriteAndDispose(_idx, elementIdx, elementIdx + elementParent.GetDbSize(elementIdx, true), 1, ref cvb);")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLineIndent("else")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("// We are going to insert the new value")
                    .AppendLineIndent("value.AddAsProperty(propertyName, ref cvb);")
                    .AppendLineIndent("int endIndex = _idx + _parent.GetDbSize(_idx, false);")
                    .AppendLineIndent("_parent.InsertAndDispose(_idx, endIndex, ref cvb);")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("_documentVersion = _parent.Version;")
            .PopIndent()
            .AppendLineIndent("}");

        // SetProperty(ReadOnlySpan<byte>) - UTF-8 implementation
        generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("///   Sets a property on this JSON object element.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"propertyName\">The UTF-8 encoded name of the property to set.</param>")
            .AppendLineIndent("/// <param name=\"value\">The value of the property to set.</param>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">")
            .AppendLineIndent("///   This element's <see cref=\"ValueKind\"/> is not <see cref=\"JsonValueKind.Object\"/>,")
            .AppendLineIndent("///   or the element reference is stale due to document mutations.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ObjectDisposedException\">")
            .AppendLineIndent("///   The parent <see cref=\"JsonDocument\"/> has been disposed.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <remarks>")
            .AppendLineIndent("///   <para>")
            .AppendLineIndent("///     If the property already exists, its value will be replaced.")
            .AppendLineIndent("///     If the property doesn't exist, it will be added to the object.")
            .AppendLineIndent("///   </para>")
            .AppendLineIndent("/// </remarks>")
            .AppendLineIndent("public void SetProperty(ReadOnlySpan<byte> propertyName, in ", fqdtn, ".", generator.SourceClassName(fqdtn), " value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("CheckValidInstance();")
                .AppendSeparatorLine()
                .AppendLineIndent("if (value.IsUndefined)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("JsonElementHelpers.RemovePropertyUnsafe(_parent, _idx, propertyName);")
                    .AppendLineIndent("_documentVersion = _parent.Version;")
                    .AppendLineIndent("return;")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("ComplexValueBuilder cvb = ComplexValueBuilder.Create(_parent, 2);")
                .AppendLineIndent("if (_parent.TryGetNamedPropertyValue(_idx, propertyName, out IJsonDocument? elementParent, out int elementIdx))")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("// We are going to replace just the value")
                    .AppendLineIndent("value.AddAsItem(ref cvb);")
                    .AppendLineIndent("_parent.OverwriteAndDispose(_idx, elementIdx, elementIdx + elementParent.GetDbSize(elementIdx, true), 1, ref cvb);")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLineIndent("else")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("// We are going to insert the new value")
                    .AppendLineIndent("value.AddAsProperty(propertyName, ref cvb);")
                    .AppendLineIndent("int endIndex = _idx + _parent.GetDbSize(_idx, false);")
                    .AppendLineIndent("_parent.InsertAndDispose(_idx, endIndex, ref cvb);")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("_documentVersion = _parent.Version;")
            .PopIndent()
            .AppendLineIndent("}");

        // RemoveProperty(string) - delegates to ReadOnlySpan<char>
        generator
            .ReserveNameIfNotReserved("RemoveProperty")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("///   Removes the property with the given name, if present.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"propertyName\">The property name to remove.</param>")
            .AppendLineIndent("/// <returns><see langword=\"true\"/> if the property was found and removed; otherwise, <see langword=\"false\"/>.</returns>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">")
            .AppendLineIndent("///   This element's <see cref=\"ValueKind\"/> is not <see cref=\"JsonValueKind.Object\"/>,")
            .AppendLineIndent("///   or the element reference is stale due to document mutations.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ObjectDisposedException\">")
            .AppendLineIndent("///   The parent <see cref=\"JsonDocument\"/> has been disposed.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .AppendLineIndent("public bool RemoveProperty(string propertyName)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return RemoveProperty(propertyName.AsSpan());")
            .PopIndent()
            .AppendLineIndent("}");

        // RemoveProperty(ReadOnlySpan<char>)
        generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("///   Removes the property with the given name, if present.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"propertyName\">The property name to remove.</param>")
            .AppendLineIndent("/// <returns><see langword=\"true\"/> if the property was found and removed; otherwise, <see langword=\"false\"/>.</returns>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">")
            .AppendLineIndent("///   This element's <see cref=\"ValueKind\"/> is not <see cref=\"JsonValueKind.Object\"/>,")
            .AppendLineIndent("///   or the element reference is stale due to document mutations.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ObjectDisposedException\">")
            .AppendLineIndent("///   The parent <see cref=\"JsonDocument\"/> has been disposed.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("public bool RemoveProperty(ReadOnlySpan<char> propertyName)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("CheckValidInstance();")
                .AppendLineIndent("bool result = JsonElementHelpers.RemovePropertyUnsafe(_parent, _idx, propertyName);")
                .AppendLineIndent("_documentVersion = _parent.Version;")
                .AppendLineIndent("return result;")
            .PopIndent()
            .AppendLineIndent("}");

        // RemoveProperty(ReadOnlySpan<byte>)
        generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("///   Removes the property with the given name, if present.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"propertyName\">The UTF-8 encoded property name to remove.</param>")
            .AppendLineIndent("/// <returns><see langword=\"true\"/> if the property was found and removed; otherwise, <see langword=\"false\"/>.</returns>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">")
            .AppendLineIndent("///   This element's <see cref=\"ValueKind\"/> is not <see cref=\"JsonValueKind.Object\"/>,")
            .AppendLineIndent("///   or the element reference is stale due to document mutations.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("/// <exception cref=\"ObjectDisposedException\">")
            .AppendLineIndent("///   The parent <see cref=\"JsonDocument\"/> has been disposed.")
            .AppendLineIndent("/// </exception>")
            .AppendLineIndent("public bool RemoveProperty(ReadOnlySpan<byte> propertyName)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("CheckValidInstance();")
                .AppendLineIndent("bool result = JsonElementHelpers.RemovePropertyUnsafe(_parent, _idx, propertyName);")
                .AppendLineIndent("_documentVersion = _parent.Version;")
                .AppendLineIndent("return result;")
            .PopIndent()
            .AppendLineIndent("}");

        return generator;
    }

    /// <summary>
    /// Append the start of a public readonly property declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append the property.</param>
    /// <param name="propertyType">The type of the property.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="nullable">If true, make the property type nullable.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator BeginPublicReadOnlyPropertyDeclaration(this CodeGenerator generator, string propertyType, string propertyName, bool nullable = false)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendLineIndent("public ", propertyType, nullable ? "? " : " ", propertyName)
            .AppendLineIndent("{")
            .PushIndent()
            .AppendLineIndent("get")
            .AppendLineIndent("{")
            .PushIndent();
    }

    /// <summary>
    /// Append the start of a public readonly property declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator EndReadOnlyPropertyDeclaration(this CodeGenerator generator)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .PopIndent()
            .AppendLineIndent("}")
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

        string name = SymbolDisplay.FormatLiteral(property.JsonPropertyName, true);
        name = name.Substring(1, name.Length - 2);

        generator
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent(
            "/// Gets the ",
            optional ? "(optional) " : string.Empty,
            "<c>",
            name,
            "</c> ",
            "property.");

        // We include documentation attached to the local (unreduced) type; this is usually the means by
        // which property-specific documentation is attached to a particular instance of a common reference type.
        if (property.UnreducedPropertyType.ShortDocumentation() is string shortDocumentation)
        {
            generator
                .AppendBlockIndentWithPrefix(shortDocumentation, "/// ");
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
}