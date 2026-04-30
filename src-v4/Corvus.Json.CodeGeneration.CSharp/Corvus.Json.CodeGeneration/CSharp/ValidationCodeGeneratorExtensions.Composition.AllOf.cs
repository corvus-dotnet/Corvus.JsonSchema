// <copyright file="ValidationCodeGeneratorExtensions.Composition.AllOf.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extensions to <see cref="CodeGenerator"/> for validation.
/// </summary>
public static partial class ValidationCodeGeneratorExtensions
{
    /// <summary>
    /// Append a validation method for all-of composite types.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="methodName">The name of the validation method.</param>
    /// <param name="typeDeclaration">The type declaration which requires allOf validation.</param>
    /// <param name="children">The child handlers for the <see cref="IKeywordValidationHandler"/>.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendCompositionAllOfValidation(
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
            .AppendLineIndent("/// Composition validation (all-of).")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value to validate.</param>")
            .AppendLineIndent("/// <param name=\"validationContext\">The current validation context.</param>")
            .AppendLineIndent("/// <param name=\"level\">The current validation level.</param>")
            .AppendLineIndent("/// <returns>The resulting validation context after validation.</returns>")
            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .BeginReservedMethodDeclaration(
                "internal static",
                "ValidationContext",
                methodName,
                new("in", typeDeclaration.DotnetTypeName(), "value"),
                ("in ValidationContext", "validationContext"),
                ("ValidationLevel", "level", "ValidationLevel.Flag"))
                .ReserveName("result")
                .ReserveName("isValid")
                .AppendBlockIndent(
                """
                ValidationContext result = validationContext;
                ValidationContext childContextBase = result;
                """)
            .AppendCompositionAllOfValidation(typeDeclaration, children)
            .AppendSeparatorLine()
            .AppendLineIndent("return result;")
            .EndMethodDeclaration();
    }

    private static CodeGenerator AppendCompositionAllOfValidation(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children)
    {
        foreach (IChildValidationHandler child in children)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            child.AppendValidationCode(generator, typeDeclaration);
        }

        return generator;
    }
}