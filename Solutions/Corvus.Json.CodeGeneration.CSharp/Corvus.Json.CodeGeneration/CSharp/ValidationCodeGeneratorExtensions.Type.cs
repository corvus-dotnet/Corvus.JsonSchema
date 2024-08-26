// <copyright file="ValidationCodeGeneratorExtensions.Type.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

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
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Core type validation.")
            .AppendLineIndent("/// </summary>");

        bool requiresValue = (typeDeclaration.AllowedCoreTypes() & CoreTypes.Integer) != 0;

        if (requiresValue)
        {
            generator
                .AppendLineIndent("/// <param name=\"value\">The value to validate.</param>");
        }

        generator
            .AppendLineIndent("/// <param name=\"valueKind\">The <see cref=\"JsonValueKind\" /> of the value to validate.</param>")
            .AppendLineIndent("/// <param name=\"validationContext\">The current validation context.</param>")
            .AppendLineIndent("/// <param name=\"level\">The current validation level.</param>")
            .AppendLineIndent("/// <returns>The resulting validation context after validation.</returns>")
            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]");

        if (requiresValue)
        {
            generator
                .BeginReservedMethodDeclaration(
                    "internal static",
                    "ValidationContext",
                    methodName,
                    new("in", typeDeclaration.DotnetTypeName(), "value"),
                    ("JsonValueKind", "valueKind"),
                    ("in ValidationContext", "validationContext"),
                    ("ValidationLevel", "level", "ValidationLevel.Flag"));
        }
        else
        {
            generator
                .BeginReservedMethodDeclaration(
                    "internal static",
                    "ValidationContext",
                    methodName,
                    ("JsonValueKind", "valueKind"),
                    ("in ValidationContext", "validationContext"),
                    ("ValidationLevel", "level", "ValidationLevel.Flag"));
        }

        generator
            .ReserveName("result")
            .ReserveName("isValid");
        bool hasChildren = children.Count > 0;
        if (hasChildren)
        {
            generator
                .AppendBlockIndent(
                    """
                    ValidationContext result = validationContext;
                    """)
                .PrependChildValidationCode(typeDeclaration, children, parentHandlerPriority)
                .AppendCoreTypeValidation(typeDeclaration.AllowedCoreTypes(), hasChildren)
                .AppendChildValidationCode(typeDeclaration, children, parentHandlerPriority);
        }
        else
        {
            generator
                .AppendCoreTypeValidation(typeDeclaration.AllowedCoreTypes(), hasChildren);
        }

        return generator
            .EndMethodDeclaration();
    }

    private static CodeGenerator AppendCoreTypeValidation(
        this CodeGenerator generator,
        CoreTypes allowedCoreTypes,
        bool hasChildren)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (allowedCoreTypes.CountTypes() == 1)
        {
            if ((allowedCoreTypes & CoreTypes.Integer) != 0)
            {
                generator
                    .AppendLineIndent("return Corvus.Json.ValidateWithoutCoreType.TypeInteger(value, ", hasChildren ? "result, " : "validationContext, ", "level);");
            }
            else
            {
                generator
                    .AppendLineIndent("return Corvus.Json.ValidateWithoutCoreType.Type", allowedCoreTypes.SingleCoreTypeName(), "(valueKind, ", hasChildren ? "result, " : "validationContext, ", "level);");
            }
        }
        else
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("bool isValid = false;");

            if ((allowedCoreTypes & CoreTypes.String) != 0)
            {
                generator
                    .AppendSeparatorLine()
                    .ReserveName("localResultString")
                    .AppendBlockIndent(
                    """
                    ValidationContext localResultString = Corvus.Json.ValidateWithoutCoreType.TypeString(valueKind, ValidationContext.ValidContext, level);
                    if (level == ValidationLevel.Flag && localResultString.IsValid)
                    {
                        return validationContext;
                    }

                    if (localResultString.IsValid)
                    {
                        isValid = true;
                    }
                    """);
            }

            if ((allowedCoreTypes & CoreTypes.Object) != 0)
            {
                generator
                    .AppendSeparatorLine()
                    .ReserveName("localResultObject")
                    .AppendBlockIndent(
                    """
                    ValidationContext localResultObject = Corvus.Json.ValidateWithoutCoreType.TypeObject(valueKind, ValidationContext.ValidContext, level);
                    if (level == ValidationLevel.Flag && localResultObject.IsValid)
                    {
                        return validationContext;
                    }

                    if (localResultObject.IsValid)
                    {
                        isValid = true;
                    }
                    """);
            }

            if ((allowedCoreTypes & CoreTypes.Array) != 0)
            {
                generator
                    .AppendSeparatorLine()
                    .ReserveName("localResultArray")
                    .AppendBlockIndent(
                    """
                    ValidationContext localResultArray = Corvus.Json.ValidateWithoutCoreType.TypeArray(valueKind, ValidationContext.ValidContext, level);
                    if (level == ValidationLevel.Flag && localResultArray.IsValid)
                    {
                        return validationContext;
                    }

                    if (localResultArray.IsValid)
                    {
                        isValid = true;
                    }
                    """);
            }

            if ((allowedCoreTypes & CoreTypes.Number) != 0)
            {
                generator
                    .AppendSeparatorLine()
                    .ReserveName("localResultNumber")
                    .AppendBlockIndent(
                    """
                    ValidationContext localResultNumber = Corvus.Json.ValidateWithoutCoreType.TypeNumber(valueKind, ValidationContext.ValidContext, level);
                    if (level == ValidationLevel.Flag && localResultNumber.IsValid)
                    {
                        return validationContext;
                    }

                    if (localResultNumber.IsValid)
                    {
                        isValid = true;
                    }
                    """);
            }

            if ((allowedCoreTypes & CoreTypes.Integer) != 0)
            {
                generator
                    .AppendSeparatorLine()
                    .ReserveName("localResultInteger")
                    .AppendBlockIndent(
                    """
                    ValidationContext localResultInteger = Corvus.Json.ValidateWithoutCoreType.TypeInteger(value, ValidationContext.ValidContext, level);
                    if (level == ValidationLevel.Flag && localResultInteger.IsValid)
                    {
                        return validationContext;
                    }

                    if (localResultInteger.IsValid)
                    {
                        isValid = true;
                    }
                    """);
            }

            if ((allowedCoreTypes & CoreTypes.Boolean) != 0)
            {
                generator
                    .AppendSeparatorLine()
                    .ReserveName("localResultBoolean")
                    .AppendBlockIndent(
                    """
                    ValidationContext localResultBoolean = Corvus.Json.ValidateWithoutCoreType.TypeBoolean(valueKind, ValidationContext.ValidContext, level);
                    if (level == ValidationLevel.Flag && localResultBoolean.IsValid)
                    {
                        return validationContext;
                    }

                    if (localResultBoolean.IsValid)
                    {
                        isValid = true;
                    }
                    """);
            }

            if ((allowedCoreTypes & CoreTypes.Null) != 0)
            {
                generator
                    .AppendSeparatorLine()
                    .ReserveName("localResultNull")
                    .AppendBlockIndent(
                    """
                    ValidationContext localResultNull = Corvus.Json.ValidateWithoutCoreType.TypeNull(valueKind, ValidationContext.ValidContext, level);
                    if (level == ValidationLevel.Flag && localResultNull.IsValid)
                    {
                        return validationContext;
                    }

                    if (localResultNull.IsValid)
                    {
                        isValid = true;
                    }
                    """);
            }

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("if (!isValid)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("if (level >= ValidationLevel.Verbose)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendIndent("return validationContext.WithResult(isValid: false, $\"Validation type - should have been ");

            AppendCommaSeparateCoreTypes(generator, allowedCoreTypes);

            generator
                        .AppendLine(" but was {valueKind}\");")
                    .PopIndent()
                    .AppendLineIndent("}")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLineIndent("else")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("if (level >= ValidationLevel.Detailed)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendIndent("return validationContext.WithResult(isValid: false, $\"Validation type - should have been ");

            AppendCommaSeparateCoreTypes(generator, allowedCoreTypes);

            generator
                        .AppendLine(".\");")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendLineIndent("else")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("return validationContext.WithResult(isValid: false);")
                    .PopIndent()
                    .AppendLineIndent("}")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("if (level >= ValidationLevel.Verbose)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return validationContext.WithResult(isValid: true, $\"Validation type - should be ");

            AppendCommaSeparateCoreTypes(generator, allowedCoreTypes);

            generator
                    .AppendLine(" but was {valueKind}\");")
                .PopIndent()
                .AppendLineIndent("}")

                .AppendLineIndent("return validationContext;");
        }

        return generator;

        static void AppendCommaSeparateCoreTypes(CodeGenerator generator, CoreTypes allowedCoreTypes)
        {
            bool first = true;

            if ((allowedCoreTypes & CoreTypes.Array) != 0)
            {
                generator.Append("'array'");
            }

            if ((allowedCoreTypes & CoreTypes.Object) != 0)
            {
                if (!first)
                {
                    generator.Append(", ");
                }
                else
                {
                    first = false;
                }

                generator.Append("'object'");
            }

            if ((allowedCoreTypes & CoreTypes.Boolean) != 0)
            {
                if (!first)
                {
                    generator.Append(", ");
                }
                else
                {
                    first = false;
                }

                generator.Append("'boolean'");
            }

            if ((allowedCoreTypes & CoreTypes.String) != 0)
            {
                if (!first)
                {
                    generator.Append(", ");
                }
                else
                {
                    first = false;
                }

                generator.Append("'string'");
            }

            if ((allowedCoreTypes & CoreTypes.Number) != 0)
            {
                if (!first)
                {
                    generator.Append(", ");
                }
                else
                {
                    first = false;
                }

                generator.Append("'number'");
            }

            if ((allowedCoreTypes & CoreTypes.Integer) != 0)
            {
                if (!first)
                {
                    generator.Append(", ");
                }
                else
                {
                    first = false;
                }

                generator.Append("'integer'");
            }

            if ((allowedCoreTypes & CoreTypes.Null) != 0)
            {
                if (!first)
                {
                    generator.Append(", ");
                }

                generator.Append("'null'");
            }
        }
    }
}