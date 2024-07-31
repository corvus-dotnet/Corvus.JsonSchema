// <copyright file="ValidationCodeGeneratorExtensions.Type.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if !NET8_0_OR_GREATER
using System.Buffers;
#endif

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extensions to <see cref="CodeGenerator"/> for validation.
/// </summary>
public static partial class ValidationCodeGeneratorExtensions
{
    /// <summary>
    /// Append a validation method for required core types.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="methodName">The name of the validation method.</param>
    /// <param name="typeDeclaration">The type declaration which requires type validation.</param>
    /// <param name="children">The child handlers for the <see cref="IKeywordValidationHandler"/>.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <param name="parentHandlerPriority">The parent validation handler priority.</param>
    public static CodeGenerator AppendCoreTypeValidation(
        this CodeGenerator generator,
        string methodName,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children,
        uint parentHandlerPriority)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
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
            .PrependChildValidationCode(typeDeclaration, children, parentHandlerPriority)
            .AppendCoreTypeValidation(typeDeclaration.AllowedCoreTypes())
            .AppendChildValidationCode(typeDeclaration, children, parentHandlerPriority)
            .EndMethodDeclaration();
    }

    private static CodeGenerator AppendCoreTypeValidation(
        this CodeGenerator generator,
        CoreTypes allowedCoreTypes)
    {
#if NET8_0_OR_GREATER
        CoreTypesValidationResults coreTypeResultsToMerge = default;
#else
        string[] coreTypeResultsToMerge = ArrayPool<string>.Shared.Rent(7);
#endif

        try
        {
            if (allowedCoreTypes.CountTypes() == 1)
            {
                if ((allowedCoreTypes & CoreTypes.Integer) != 0)
                {
                    generator
                        .AppendLineIndent("return Corvus.Json.Validate.TypeInteger(value, result, level);");
                }
                else
                {
                    generator
                        .AppendLineIndent("return Corvus.Json.Validate.Type", allowedCoreTypes.SingleCoreTypeName(), "(valueKind, result, level);");
                }
            }
            else
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("bool isValid = false;");

                int coreTypesCount = 0;

                if ((allowedCoreTypes & CoreTypes.String) != 0)
                {
                    generator
                        .AppendSeparatorLine()
                        .ReserveName("localResultString")
                        .AppendBlockIndent(
                        """
                        ValidationContext localResultString = Corvus.Json.Validate.TypeString(valueKind, result.CreateChildContext(), level);
                        if (level == ValidationLevel.Flag && localResultString.IsValid)
                        {
                            return validationContext;
                        }

                        if (localResultString.IsValid)
                        {
                            isValid = true;
                        }
                        """);

                    coreTypeResultsToMerge[coreTypesCount++] = "localResultString";
                }

                if ((allowedCoreTypes & CoreTypes.Object) != 0)
                {
                    generator
                        .AppendSeparatorLine()
                        .ReserveName("localResultObject")
                        .AppendBlockIndent(
                        """
                        ValidationContext localResultObject = Corvus.Json.Validate.TypeObject(valueKind, result.CreateChildContext(), level);
                        if (level == ValidationLevel.Flag && localResultObject.IsValid)
                        {
                            return validationContext;
                        }

                        if (localResultObject.IsValid)
                        {
                            isValid = true;
                        }
                        """);

                    coreTypeResultsToMerge[coreTypesCount++] = "localResultObject";
                }

                if ((allowedCoreTypes & CoreTypes.Array) != 0)
                {
                    generator
                        .AppendSeparatorLine()
                        .ReserveName("localResultArray")
                        .AppendBlockIndent(
                        """
                        ValidationContext localResultArray = Corvus.Json.Validate.TypeArray(valueKind, result.CreateChildContext(), level);
                        if (level == ValidationLevel.Flag && localResultArray.IsValid)
                        {
                            return validationContext;
                        }

                        if (localResultArray.IsValid)
                        {
                            isValid = true;
                        }
                        """);

                    coreTypeResultsToMerge[coreTypesCount++] = "localResultArray";
                }

                if ((allowedCoreTypes & CoreTypes.Number) != 0)
                {
                    generator
                        .AppendSeparatorLine()
                        .ReserveName("localResultNumber")
                        .AppendBlockIndent(
                        """
                        ValidationContext localResultNumber = Corvus.Json.Validate.TypeNumber(valueKind, result.CreateChildContext(), level);
                        if (level == ValidationLevel.Flag && localResultNumber.IsValid)
                        {
                            return validationContext;
                        }

                        if (localResultNumber.IsValid)
                        {
                            isValid = true;
                        }
                        """);

                    coreTypeResultsToMerge[coreTypesCount++] = "localResultNumber";
                }

                if ((allowedCoreTypes & CoreTypes.Integer) != 0)
                {
                    generator
                        .AppendSeparatorLine()
                        .ReserveName("localResultInteger")
                        .AppendBlockIndent(
                        """
                        ValidationContext localResultInteger = Corvus.Json.Validate.TypeInteger(value, result.CreateChildContext(), level);
                        if (level == ValidationLevel.Flag && localResultInteger.IsValid)
                        {
                            return validationContext;
                        }

                        if (localResultInteger.IsValid)
                        {
                            isValid = true;
                        }
                        """);

                    coreTypeResultsToMerge[coreTypesCount++] = "localResultInteger";
                }

                if ((allowedCoreTypes & CoreTypes.Boolean) != 0)
                {
                    generator
                        .AppendSeparatorLine()
                        .ReserveName("localResultBoolean")
                        .AppendBlockIndent(
                        """
                        ValidationContext localResultBoolean = Corvus.Json.Validate.TypeBoolean(valueKind, result.CreateChildContext(), level);
                        if (level == ValidationLevel.Flag && localResultBoolean.IsValid)
                        {
                            return validationContext;
                        }

                        if (localResultBoolean.IsValid)
                        {
                            isValid = true;
                        }
                        """);

                    coreTypeResultsToMerge[coreTypesCount++] = "localResultBoolean";
                }

                if ((allowedCoreTypes & CoreTypes.Null) != 0)
                {
                    generator
                        .AppendSeparatorLine()
                        .ReserveName("localResultNull")
                        .AppendBlockIndent(
                        """
                        ValidationContext localResultNull = Corvus.Json.Validate.TypeNull(valueKind, result.CreateChildContext(), level);
                        if (level == ValidationLevel.Flag && localResultNull.IsValid)
                        {
                            return validationContext;
                        }

                        if (localResultNull.IsValid)
                        {
                            isValid = true;
                        }
                        """);

                    coreTypeResultsToMerge[coreTypesCount++] = "localResultNull";
                }

                if (coreTypesCount > 0)
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendBlockIndent(
                        """
                    return result.MergeResults(
                        isValid,
                    """)
                        .PushIndent()
                        .AppendIndent("level");

                    for (int i = 0; i < coreTypesCount; ++i)
                    {
                        generator.AppendLine(",");
                        generator.AppendIndent(coreTypeResultsToMerge[i]);
                    }

                    return generator
                        .AppendLine(");")
                        .PopIndent();
                }
            }

            return generator;
        }
        finally
        {
#if !NET8_0_OR_GREATER
            ArrayPool<string>.Shared.Return(coreTypeResultsToMerge);
#endif
        }
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// A fixed-sizse aray to capture the core types valdiation results.
    /// </summary>
    [System.Runtime.CompilerServices.InlineArray(7)]
    private struct CoreTypesValidationResults
    {
#pragma warning disable //// Naming conventions haven't caught up with inline array
        private string element0;
#pragma warning restore
    }
#endif
}