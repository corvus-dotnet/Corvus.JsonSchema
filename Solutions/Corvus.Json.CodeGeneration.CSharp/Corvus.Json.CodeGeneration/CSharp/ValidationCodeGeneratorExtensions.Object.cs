// <copyright file="ValidationCodeGeneratorExtensions.Object.cs" company="Endjin Limited">
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
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendObjectValidation(
        this CodeGenerator generator,
        string methodName,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Object validation.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value to validate.</param>")
            .AppendLineIndent("/// <param name=\"valueKind\">The <see cref=\"JsonValueKind\" /> of the value to validate.</param>")
            .AppendLineIndent("/// <param name=\"validationContext\">The current validation context.</param>")
            .AppendLineIndent("/// <param name=\"level\">The current validation level.</param>")
            .AppendLineIndent("/// <returns>The resulting validation context after validation.</returns>")
            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .BeginReservedMethodDeclaration(
                "internal static",
                "ValidationContext",
                methodName,
                new("in", typeDeclaration.DotnetTypeName(), "value"),
                ("JsonValueKind", "valueKind"),
                ("in ValidationContext", "validationContext"),
                ("ValidationLevel", "level", "ValidationLevel.Flag"))
                .ReserveName("result")
                .ReserveName("isValid")
                .AppendLineIndent("ValidationContext result = validationContext;")
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
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.FallbackObjectPropertyType() is FallbackObjectPropertyType propertyType && !propertyType.ReducedType.IsBuiltInJsonAnyType())
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
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.FallbackObjectPropertyType() is FallbackObjectPropertyType propertyType && !propertyType.ReducedType.IsBuiltInJsonAnyType())
        {
            generator
                .GenericTypeOf("JsonObjectProperty", propertyType.ReducedType);
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
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendIndent("using ")
            .AppendEnumeratorType(typeDeclaration)
            .Append(' ')
            .Append(variableName)
            .AppendLine(" = value.EnumerateObject();");
    }

    private static CodeGenerator AppendObjectValidation(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

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
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            generator
                .AppendLineIndent(
                    "ignoredResult = ignoredResult.PushValidationLocationProperty(",
                    SymbolDisplay.FormatLiteral(keyword.Keyword, true),
                    ");")
                .AppendKeywordValidationResult(isValid: true, keyword, "ignoredResult", "ignored because the value is not an object")
                .AppendLineIndent("ignoredResult = ignoredResult.PopLocation();");
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
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

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

            generator
                .AppendIndent("foreach (")
                .AppendJsonObjectPropertyType(typeDeclaration)
                .AppendLine(" property in value.EnumerateObject())")
                .AppendLineIndent("{")
                .PushIndent();

            if (children
                    .OfType<IChildObjectPropertyValidationHandler>()
                    .Any(child => child.RequiresPropertyNameAsString(typeDeclaration)))
            {
                generator
                    .ReserveName("propertyNameAsString")
                    .AppendLineIndent("string? propertyNameAsString = null;");
            }

            foreach (IChildObjectPropertyValidationHandler child in children.OfType<IChildObjectPropertyValidationHandler>())
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                child.AppendObjectPropertyValidationCode(generator, typeDeclaration);
            }

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("propertyCount++;")
                .PopIndent()
                .AppendLineIndent("}");
        }

        foreach (IChildValidationHandler child in children)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            child.AppendValidationCode(generator, typeDeclaration);
        }

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("return result;");
    }
}