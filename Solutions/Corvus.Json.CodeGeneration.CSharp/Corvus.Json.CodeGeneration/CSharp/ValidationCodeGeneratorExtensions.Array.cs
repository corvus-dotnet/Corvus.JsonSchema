﻿// <copyright file="ValidationCodeGeneratorExtensions.Array.cs" company="Endjin Limited">
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
    /// Append a validation method for array types.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="methodName">The name of the validation method.</param>
    /// <param name="typeDeclaration">The type declaration which requires array validation.</param>
    /// <param name="children">The child handlers for the <see cref="IKeywordValidationHandler"/>.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendArrayValidation(
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
            .AppendLineIndent("/// Array validation.")
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
                ("ValidationLevel", "level"))
                .ReserveName("result")
                .ReserveName("isValid")
                .AppendBlockIndent(
                """
                ValidationContext result = validationContext;
                """)
            .AppendArrayValidation(typeDeclaration, children)
            .EndMethodDeclaration();
    }

    /// <summary>
    /// Appends an array enumeration call.
    /// </summary>
    /// <param name="generator">The code generator to which to append the enumeration.</param>
    /// <param name="typeDeclaration">The typeDeclaration for which to enumerate the array.</param>
    /// <param name="variableName">The variable name into which to write the enumerator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendArrayEnumerator(this CodeGenerator generator, TypeDeclaration typeDeclaration, string variableName)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .AppendIndent("using ");

        if (typeDeclaration.ArrayItemsType() is ArrayItemsTypeDeclaration itemsType)
        {
            generator
                .GenericTypeOf("JsonArrayEnumerator", itemsType.ReducedType);
        }
        else
        {
            generator
                .Append("JsonArrayEnumerator");
        }

        return generator
            .Append(' ')
            .Append(variableName)
            .AppendLine(" = value.EnumerateArray();");
    }

    /// <summary>
    /// Appends items validation code for an <see cref="ArrayItemsTypeDeclaration"/>.
    /// </summary>
    /// <param name="generator">The code generator to which to append the enumeration.</param>
    /// <param name="arrayItems">The <see cref="ArrayItemsTypeDeclaration"/> for which to emit validation code.</param>
    /// <param name="enumeratorIsCorrectType">Indicates whether the enumerator automatically returns the correct type for validation.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendValidateNonTupleItemsType(this CodeGenerator generator, ArrayItemsTypeDeclaration arrayItems, bool enumeratorIsCorrectType)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .AppendLineIndent("if (level > ValidationLevel.Basic)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent(
                    "result = result.PushValidationLocationReducedPathModifier(new(",
                    SymbolDisplay.FormatLiteral(arrayItems.Keyword.GetPathModifier(arrayItems), true),
                    "));")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendSeparatorLine();

        if (!arrayItems.ReducedType.IsBuiltInJsonAnyType())
        {
            if (enumeratorIsCorrectType)
            {
                generator
                    .AppendLineIndent(
                        "var nonTupleItemsResult = arrayEnumerator.Current.Validate(result.CreateChildContext(), level);")
                    .AppendBlockIndent(
                        """
                        if (level == ValidationLevel.Flag && !nonTupleItemsResult.IsValid)
                        {
                            return nonTupleItemsResult;
                        }

                        result = result.MergeResults(nonTupleItemsResult.IsValid, level, nonTupleItemsResult);
                        """);
            }
            else
            {
                generator
                    .AppendLineIndent(
                        "var nonTupleItemsResult = arrayEnumerator.Current.As<",
                        arrayItems.ReducedType.FullyQualifiedDotnetTypeName(),
                        ">().Validate(result.CreateChildContext(), level);")
                    .AppendBlockIndent(
                        """
                        if (level == ValidationLevel.Flag && !nonTupleItemsResult.IsValid)
                        {
                            return nonTupleItemsResult;
                        }

                        result = result.MergeResults(nonTupleItemsResult.IsValid, level, nonTupleItemsResult);
                        """);
            }
        }

        return generator
            .AppendBlockIndent(
                """
                if (level > ValidationLevel.Basic)
                {
                    result = result.PopLocation();
                }

                result = result.WithLocalItemIndex(length);
                """);
    }

    /// <summary>
    /// Appends items validation code for an unevaluated <see cref="ArrayItemsTypeDeclaration"/>.
    /// </summary>
    /// <param name="generator">The code generator to which to append the enumeration.</param>
    /// <param name="arrayItems">The <see cref="ArrayItemsTypeDeclaration"/> for which to emit validation code.</param>
    /// <param name="enumeratorIsCorrectType">Indicates whether the enumerator automatically returns the correct type for validation.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendValidateUnevaluatedItemsType(this CodeGenerator generator, ArrayItemsTypeDeclaration arrayItems, bool enumeratorIsCorrectType)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .AppendLineIndent("if (!result.HasEvaluatedLocalOrAppliedItemIndex(length))")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("if (level > ValidationLevel.Basic)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent(
                        "result = result.PushValidationLocationReducedPathModifier(new(",
                        SymbolDisplay.FormatLiteral(arrayItems.Keyword.GetPathModifier(arrayItems), true),
                        "));")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine();

        if (!arrayItems.ReducedType.IsBuiltInJsonAnyType())
        {
            if (enumeratorIsCorrectType)
            {
                generator
                    .AppendLineIndent(
                        "var unevaluatedItemsResult = arrayEnumerator.Current.Validate(result.CreateChildContext(), level);")
                    .AppendBlockIndent(
                        """
                        if (level == ValidationLevel.Flag && !unevaluatedItemsResult.IsValid)
                        {
                            return unevaluatedItemsResult;
                        }

                        result = result.MergeResults(unevaluatedItemsResult.IsValid, level, unevaluatedItemsResult);
                        """);
            }
            else
            {
                generator
                    .AppendLineIndent(
                        "var unevaluatedItemsResult = arrayEnumerator.Current.As<",
                        arrayItems.ReducedType.FullyQualifiedDotnetTypeName(),
                        ">().Validate(result.CreateChildContext(), level);")
                    .AppendBlockIndent(
                        """
                        if (level == ValidationLevel.Flag && !unevaluatedItemsResult.IsValid)
                        {
                            return unevaluatedItemsResult;
                        }

                        result = result.MergeResults(unevaluatedItemsResult.IsValid, level, unevaluatedItemsResult);
                        """);
            }
        }

        return generator
            .AppendBlockIndent(
            """
                if (level > ValidationLevel.Basic)
                {
                    result = result.PopLocation();
                }

                result = result.WithLocalItemIndex(length);
                """)
        .PopIndent()
        .AppendLineIndent("}");
    }

    private static CodeGenerator AppendArrayValidation(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .AppendLineIndent("if (valueKind != JsonValueKind.Array)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("if (level == ValidationLevel.Verbose)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("ValidationContext ignoredResult = validationContext;");

        foreach (IArrayValidationKeyword keyword in typeDeclaration.Keywords().OfType<IArrayValidationKeyword>())
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            generator
                .AppendKeywordValidationResult(isValid: true, keyword, "ignoredResult", "ignored because the value is not an array", withKeyword: true);
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

        if (typeDeclaration.RequiresArrayEnumeration() ||
            typeDeclaration.RequiresArrayLength())
        {
            generator.ReserveName("length");

            if (typeDeclaration.RequiresArrayEnumeration())
            {
                generator.AppendLineIndent("int length = 0;");
            }
            else
            {
                generator.AppendLineIndent("int length = value.GetArrayLength();");
            }
        }

        if (typeDeclaration.RequiresArrayEnumeration())
        {
            generator
                .ReserveName("arrayEnumerator");

            AppendArrayEnumerator(generator, typeDeclaration, "arrayEnumerator");

            generator
                .AppendLineIndent("while (arrayEnumerator.MoveNext())")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendBlockIndent(
                    """
                    if (level > ValidationLevel.Basic)
                    {
                        result = result.PushDocumentArrayIndex(length);
                    }
                    """);

            foreach (IChildArrayItemValidationHandler child in children.OfType<IChildArrayItemValidationHandler>())
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                child.AppendArrayItemValidationCode(generator, typeDeclaration);
            }

            generator
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                    """
                    if (level > ValidationLevel.Basic)
                    {
                        result = result.PopLocation();
                    }

                    length++;
                    """)
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