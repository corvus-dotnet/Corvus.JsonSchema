﻿// <copyright file="ValidationCodeGeneratorExtensions.Object.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if !NET8_0_OR_GREATER
using System.Buffers;
#endif

using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extensions to <see cref="CodeGenerator"/> for validation.
/// </summary>
public static partial class ValidationCodeGeneratorExtensions
{
    /// <summary>
    /// Append a validation method for object types.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="methodName">The name of the validation method.</param>
    /// <param name="typeDeclaration">The type declaration which requires object validation.</param>
    /// <param name="children">The child handlers for the <see cref="IKeywordValidationHandler"/>.</param>
    /// <param name="parentHandlerPriority">The parent validation handler priority.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendObjectValidation(
        this CodeGenerator generator,
        string methodName,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children,
        uint parentHandlerPriority)
    {
        return generator
            .BeginReservedMethodDeclaration(
                "public static",
                "ValidationContext",
                methodName,
                new("in", typeDeclaration.DotnetTypeName(), "value"),
                ("JsonValueKind", "valueKind"),
                ("in ValidationContext", "validationContext"),
                ("ValidationLevel", "level", "ValidationLevel.Flag"))
                .ReserveName("result")
                .ReserveName("isValid")
                .AppendBlockIndent(
                """
                ValidationContext result = validationContext;
                """)
            .AppendObjectValidation(typeDeclaration, children)
            .EndMethodDeclaration();
    }

    /// <summary>
    /// Appends the type of an object enumerator.
    /// </summary>
    /// <param name="generator">The code generator to which to append the enumeration.</param>
    /// <param name="typeDeclaration">The typeDeclaration for which to enumerate the object.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendEnumeratorType(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.FallbackObjectPropertyType() is FallbackObjectPropertyType propertyType)
        {
            generator
                .GenericTypeOf("JsonObjectEnumerator", propertyType.ReducedType);
        }
        else
        {
            generator
                .Append("JsonObjectEnumerator");
        }

        return generator;
    }

    /// <summary>
    /// Appends the type of an object enumerator.
    /// </summary>
    /// <param name="generator">The code generator to which to append the enumeration.</param>
    /// <param name="typeDeclaration">The typeDeclaration for which to enumerate the object.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendJsonObjectPropertyType(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.FallbackObjectPropertyType() is FallbackObjectPropertyType propertyType)
        {
            generator
                .GenericTypeOf("JsonObjecProperty", propertyType.ReducedType);
        }
        else
        {
            generator
                .Append("JsonObjectProperty");
        }

        return generator;
    }

    /// <summary>
    /// Appends an object enumeration call.
    /// </summary>
    /// <param name="generator">The code generator to which to append the enumeration.</param>
    /// <param name="typeDeclaration">The typeDeclaration for which to enumerate the object.</param>
    /// <param name="variableName">The variable name into which to write the enumerator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendObjectEnumerator(this CodeGenerator generator, TypeDeclaration typeDeclaration, string variableName)
    {
        return generator
            .AppendIndent("using ")
            .AppendEnumeratorType(typeDeclaration)
            .Append(' ')
            .Append(variableName)
            .AppendLine(" = value.EnumerateObject();");
    }

    /// <summary>
    /// Appends validation code for an <see cref="PropertyDeclaration"/>.
    /// </summary>
    /// <param name="generator">The code generator to which to append the validation code.</param>
    /// <param name="objectProperty">The <see cref="PropertyDeclaration"/> for which to emit validation code.</param>
    /// <param name="enumeratorIsCorrectType">Indicates whether the enumerator automatically reutrns the correct type for validation.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendValidateLocalEvaluatedProperty(this CodeGenerator generator, FallbackObjectPropertyType objectProperty, bool enumeratorIsCorrectType)
    {
        generator
            .AppendLineIndent("if (level > ValidationLevel.Basic)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent(
                    "result = result.PushValidationLocationReducedPathModifier(new(",
                    SymbolDisplay.FormatLiteral(objectProperty.ReducedPathModifier, true),
                    "));")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendSeparatorLine();

        if (enumeratorIsCorrectType)
        {
            generator
                .AppendLineIndent(
                    "result = property.Value.Validate(result, level);");
        }
        else
        {
            generator
                .AppendLineIndent(
                    "result = property.ValueAs<",
                    objectProperty.ReducedType.FullyQualifiedDotnetTypeName(),
                    ">().Validate(result, level);");
        }

        return generator
            .AppendBlockIndent(
            """
            if (level == ValidationLevel.Flag && !result.IsValid)
            {
                return result;
            }

            if (level > ValidationLevel.Basic)
            {
                result = result.PopLocation();
            }

            result = result.WithLocalItemIndex(length);
            """);
    }

    /// <summary>
    /// Appends validation code for an <see cref="PropertyDeclaration"/>.
    /// </summary>
    /// <param name="generator">The code generator to which to append the validation code.</param>
    /// <param name="fallbackPropertyType">The <see cref="PropertyDeclaration"/> for which to emit validation code.</param>
    /// <param name="enumeratorIsCorrectType">Indicates whether the enumerator automatically reutrns the correct type for validation.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendValidateLocalAndAppliedEvaluatedProperty(this CodeGenerator generator, FallbackObjectPropertyType fallbackPropertyType, bool enumeratorIsCorrectType)
    {
        generator
            .AppendLineIndent("if (!result.HasEvaluatedLocalOrAppliedProperty(propertyIndex))")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("if (level > ValidationLevel.Basic)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent(
                        "result = result.PushValidationLocationReducedPathModifier(new(",
                        SymbolDisplay.FormatLiteral(fallbackPropertyType.ReducedPathModifier, true),
                        "));")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine();

        if (enumeratorIsCorrectType)
        {
            generator
                .AppendLineIndent(
                    "result = property.Value.Validate(result, level);");
        }
        else
        {
            generator
                .AppendLineIndent(
                    "result = property.ValueAs<",
                    fallbackPropertyType.ReducedType.FullyQualifiedDotnetTypeName(),
                    ">().Validate(result, level);");
        }

        return generator
            .AppendBlockIndent(
            """
                if (level == ValidationLevel.Flag && !result.IsValid)
                {
                    return result;
                }

                if (level > ValidationLevel.Basic)
                {
                    result = result.PopLocation();
                }

                result = result.WithLocalProperty(propertyCount);
                """)
        .PopIndent()
        .AppendLineIndent("}");
    }

    private static CodeGenerator AppendObjectValidation(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children)
    {
        generator
            .AppendLineIndent("if (valueKind != JsonValueKind.Object)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("if (level == ValidationLevel.Verbose)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("ValidationContext ignoredResult = validationContext;");

        foreach (IObjectValidationKeyword keyword in typeDeclaration.Keywords().OfType<IObjectValidationKeyword>())
        {
            generator
                .AppendKeywordValidationResult(isValid: true, keyword, "ignoredResult", "ignored because the value is not an object");
        }

        generator
                .AppendSeparatorLine()
                .AppendLineIndent("return ignoredResult;")
                .PopIndent()
                .AppendLineIndent("}")
            .AppendSeparatorLine()
            .AppendLineIndent("return validationContext;")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendSeparatorLine();

        foreach (IChildValidationHandler child in children)
        {
            child.AppendValidateMethodSetup(generator, typeDeclaration);
        }

        generator
            .AppendSeparatorLine();

        if (typeDeclaration.RequiresObjectEnumeration() ||
            typeDeclaration.RequiresPropertyCount())
        {
            generator.ReserveName("propertyCount");

            if (typeDeclaration.RequiresObjectEnumeration())
            {
                generator.AppendLineIndent("int propertyCount = 0;");
            }
            else
            {
                generator.AppendLineIndent("int propertyCount = value.Count;");
            }
        }

        if (typeDeclaration.RequiresObjectEnumeration())
        {
            generator
                .ReserveName("objectEnumerator");

            AppendObjectEnumerator(generator, typeDeclaration, "objectEnumerator");

            generator
                .AppendLineIndent("while (objectEnumerator.MoveNext())")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent(
                        (CodeGenerator.Segment)(g => g.AppendJsonObjectPropertyType(typeDeclaration)),
                        " property = objectEnumerator.Current;");

            if (typeDeclaration.RequiresPropertyNameAsString())
            {
                generator
                    .ReserveName("propertyNameAsString")
                    .AppendLineIndent("string? propertyNameAsString = null;");
            }

            foreach (IChildObjectPropertyValidationHandler child in children.OfType<IChildObjectPropertyValidationHandler>())
            {
                child.AppendObjectPropertyValidationCode(generator, typeDeclaration);
            }

            generator
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                    """
                    if (level > ValidationLevel.Basic)
                    {
                        result = result.PopLocation();
                    }

                    propertyCount++;
                    """)
                .PopIndent()
                .AppendLineIndent("}");
        }

        foreach (IChildValidationHandler child in children)
        {
            child.AppendValidationCode(generator, typeDeclaration);
        }

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("return result;");
    }
}