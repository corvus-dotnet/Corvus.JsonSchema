// <copyright file="CodeGeneratorExtensions.Numeric.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extension methods for the <see cref="CodeGenerator"/>.
/// </summary>
internal static partial class CodeGeneratorExtensions
{
    /// <summary>
    /// Append the Equals() method overload for a <see cref="BinaryJsonNumber"/>.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendEqualsBinaryJsonNumber(this CodeGenerator generator)
    {
        return generator
            .ReserveNameIfNotReserved("Equals")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Equality comparison.
                /// </summary>
                /// <param name="other">The <see cref="BinaryJsonNumber"/> with which to compare.</param>
                /// <returns><see langword="true"/> if the values were equal.</returns>
                """)
            .AppendIndent("public bool Equals(in BinaryJsonNumber other)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalBackingValueCallbackIndent("Backing.JsonElement", "jsonElementBacking", AppendJsonElementComparison)
                .AppendSeparatorLine()
                .AppendConditionalWrappedBackingValueLineIndent("Backing.Number", "return BinaryJsonNumber.Equals(other, ", "numberBacking", ");")
                .AppendSeparatorLine()
                .AppendLineIndent("return false;")
            .PopIndent()
            .AppendLineIndent("}");

        static void AppendJsonElementComparison(CodeGenerator generator, string fieldName)
        {
            generator
                .Append("return this.")
                .Append(fieldName)
                .Append(".ValueKind == JsonValueKind.Number && other.Equals(this.")
                .Append(fieldName)
                .Append(");");
        }
    }

    /// <summary>
    /// Appends the <c>As[NumericType]()</c> method.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the <c>As[NumericType]()</c> method.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsDotnetNumericValue(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.PreferredDotnetNumericTypeName() is string numericTypeName)
        {
            bool isNet80OrGreaterType = IsNet8OrGreaterNumericType(numericTypeName);

            if (isNet80OrGreaterType)
            {
                generator
                    .AppendLine("#if NET8_0_OR_GREATER");
            }

            string dotnetTypeSuffix = FormatProviderRegistry.Instance.NumberTypeFormatProviders.GetTypeNameForNumericLangwordOrTypeName(numericTypeName) ?? numericTypeName;
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendIndent("/// Gets the value as a ")
                .Append(numericTypeName)
                .AppendLine(".")
                .AppendLineIndent("/// </summary>")
                .AppendIndent("public ")
                .Append(numericTypeName)
                .Append(" As")
                .Append(dotnetTypeSuffix)
                .Append("() => (")
                .Append(numericTypeName)
                .AppendLine(")this;");

            if (isNet80OrGreaterType)
            {
                generator
                    .AppendLine("#endif");
            }
        }

        return generator;
    }

    /// <summary>
    /// Appends the <c>AsBinaryJsonNumber</c> property.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsBinaryJsonNumber(this CodeGenerator generator)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Gets the value as a <see cref=\"BinaryJsonNumber\"/>.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("public BinaryJsonNumber AsBinaryJsonNumber")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalWrappedBackingValueLineIndent("Backing.Number", "return ", "numberBacking", ";")
                    .AppendConditionalWrappedBackingValueLineIndent("Backing.JsonElement", "return BinaryJsonNumber.FromJson(", "jsonElementBacking", ");")
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends numeric operators.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append numeric operators.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendNumericOperators(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .AppendNumericComparison(typeDeclaration, "<", "Less than operator.", "<see langword=\"true\"/> if the left is less than the right, otherwise <see langword=\"false\"/>.")
            .AppendNumericComparison(typeDeclaration, "<=", "Less than or equals operator.", "<see langword=\"true\"/> if the left is less than or equal to the right, otherwise <see langword=\"false\"/>.")
            .AppendNumericComparison(typeDeclaration, ">", "Greater than operator.", "<see langword=\"true\"/> if the left is greater than the right, otherwise <see langword=\"false\"/>.")
            .AppendNumericComparison(typeDeclaration, ">=", "Greater than or equals operator.", "<see langword=\"true\"/> if the left is greater than or equal to the right, otherwise <see langword=\"false\"/>.")
            .AppendNumericBinaryOperator(typeDeclaration, "+", "Adds two numbers to produce their sum.")
            .AppendNumericBinaryOperator(typeDeclaration, "-", "Subtracts two numbers to produce their difference.")
            .AppendNumericBinaryOperator(typeDeclaration, "*", "Multiplies two numbers.")
            .AppendNumericBinaryOperator(typeDeclaration, "/", "Divides two numbers.")
            .AppendNumericUnaryOperator(typeDeclaration, "++", "Increments the number.")
            .AppendNumericUnaryOperator(typeDeclaration, "--", "Decrements the number.")
            .AppendNumericCompare(typeDeclaration);
    }

    /// <summary>
    /// Appends conversions to and from the .NET numeric types.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration from which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendNumericConversions(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .AppendNumericConversionsForDotnetType(typeDeclaration, "byte", "SafeGetByte")
            .AppendNumericConversionsForDotnetType(typeDeclaration, "decimal", "SafeGetDecimal")
            .AppendNumericConversionsForDotnetType(typeDeclaration, "double", "SafeGetDouble")
            .AppendNumericConversionsForDotnetType(typeDeclaration, "short", "SafeGetInt16")
            .AppendNumericConversionsForDotnetType(typeDeclaration, "int", "SafeGetInt32")
            .AppendNumericConversionsForDotnetType(typeDeclaration, "long", "SafeGetInt64")
            .AppendNumericConversionsForDotnetType(typeDeclaration, "Int128", "SafeGetInt128", FrameworkType.Net80OrGreater)
            .AppendNumericConversionsForDotnetType(typeDeclaration, "sbyte", "SafeGetSByte")
            .AppendNumericConversionsForDotnetType(typeDeclaration, "Half", "SafeGetHalf", FrameworkType.Net80OrGreater)
            .AppendNumericConversionsForDotnetType(typeDeclaration, "float", "SafeGetSingle")
            .AppendNumericConversionsForDotnetType(typeDeclaration, "ushort", "SafeGetUInt16")
            .AppendNumericConversionsForDotnetType(typeDeclaration, "uint", "SafeGetUInt32")
            .AppendNumericConversionsForDotnetType(typeDeclaration, "ulong", "SafeGetUInt64")
            .AppendNumericConversionsForDotnetType(typeDeclaration, "UInt128", "SafeGetUInt128", FrameworkType.Net80OrGreater);
    }

    /// <summary>
    /// Appends conversions to and from the <paramref name="numericType"/>.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration from which to convert.</param>
    /// <param name="numericType">The name of the numeric type for conversion.</param>
    /// <param name="numericValueAccessorMethodName">The name of the method that converts from a <see cref="JsonElement"/>
    /// value to the <paramref name="numericType"/>.</param>
    /// <param name="frameworkType">The framework type for which to emit the code.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendNumericConversionsForDotnetType(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string numericType,
        string numericValueAccessorMethodName,
        FrameworkType frameworkType = FrameworkType.All)
    {
        generator.AppendSeparatorLine();

        return ConditionalCodeSpecification.AppendConditional(generator, AppendConversions, frameworkType);

        void AppendConversions(CodeGenerator generator)
        {
            string operatorKind = typeDeclaration.PreferredDotnetNumericTypeName() == numericType ? "implicit" : "explicit";
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Conversion to ", numericType, ".")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent("/// <param name=\"value\">The value from which to convert.</param>")
                .AppendLineIndent("/// <returns>An instance of the ", numericType, ".</returns>")
                .AppendIndent("public static ")
                .Append(operatorKind)
                .Append(" operator ")
                .Append(numericType)
                .Append('(')
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(" value)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalBackingValueCallbackIndent(
                        "Backing.JsonElement",
                        "jsonElementBacking",
                        (g, f) =>
                        {
                            g.AppendIndent("return value.")
                             .Append(f)
                             .Append('.')
                             .Append(numericValueAccessorMethodName)
                             .AppendLine("();");
                        },
                        identifier: "value")
                    .AppendConditionalBackingValueCallbackIndent(
                        "Backing.Number",
                        "numberBacking",
                        (g, f) =>
                        {
                            g.AppendIndent("return value.")
                             .Append(f)
                             .Append(".CreateChecked<")
                             .Append(numericType)
                             .AppendLine(">();");
                        },
                        identifier: "value")
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Conversion from ", numericType, ".")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent("/// <param name=\"value\">The value from which to convert.</param>")
                .AppendLineIndent("/// <returns>An instance of the <see cref=\"", typeDeclaration.DotnetTypeName(), "\"/>.</returns>")
                .AppendIndent("public static ")
                .Append(operatorKind)
                .Append(" operator ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append('(')
                .Append(numericType)
                .AppendLine(" value)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return new(new BinaryJsonNumber(value));")
                .PopIndent()
                .AppendLineIndent("}");
        }
    }

    /// <summary>
    /// Append the public numeric constructor appropriate for the type declaration.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the numeric constructor.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <exception cref="InvalidOperationException">This method was called for a non-numeric type declaration.</exception>
    public static CodeGenerator AppendPublicNumericConstructor(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration)
    {
        string preferredNumericTypeName =
            typeDeclaration.PreferredDotnetNumericTypeName()
            ?? throw new InvalidOperationException("There must be a preferred numeric type name for a numeric type");

        bool isNet80OrGreaterType = IsNet8OrGreaterNumericType(preferredNumericTypeName);
        if (isNet80OrGreaterType)
        {
            generator
                .AppendLine("#if NET8_0_OR_GREATER");
        }

        generator
            .AppendPublicNumericConstructor(typeDeclaration, preferredNumericTypeName);

        if (isNet80OrGreaterType)
        {
            generator
                .AppendLine("#else")
                .AppendPublicNumericConstructor(
                    typeDeclaration,
                    (typeDeclaration.ImpliedCoreTypesOrAny() & CoreTypes.Integer) != 0 ? "long" : "double")
                .AppendLine("#endif");
        }

        return generator;
    }

    /// <summary>
    /// Determines if a .NET type is a NET8_0_OR_GREATER type.
    /// </summary>
    /// <param name="preferredNumericTypeName">The type name.</param>
    /// <returns><see langword="true"/> if the type is for .NET 8.0 or greater.</returns>
    public static bool IsNet8OrGreaterNumericType(string preferredNumericTypeName)
    {
        return preferredNumericTypeName switch
        {
            "Half" => true,
            "UInt128" => true,
            "Int128" => true,
            _ => false,
        };
    }

    /// <summary>
    /// Append the specific public numeric constructor.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the numeric constructor.</param>
    /// <param name="numericTypeName">The name of the .NET numeric type.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendPublicNumericConstructor(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string numericTypeName)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Initializes a new instance of the <see cref = \"", typeDeclaration.DotnetTypeName(), "\"/> struct.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to construct the instance.</param>")
            .AppendIndent("public ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append('(')
            .Append(numericTypeName)
            .AppendLine(" value)")
            .PushIndent()
                .AppendLineIndent(": this(new BinaryJsonNumber(value))")
            .PopIndent()
            .AppendLineIndent("{")
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendNumericCompare(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        string backingName = generator.GetFieldNameInScope("backing");
        string numberBacking = generator.GetFieldNameInScope("numberBacking");
        string jsonElementBacking = generator.GetFieldNameInScope("jsonElementBacking");

        return generator
            .ReserveNameIfNotReserved("Compare")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Compare two numbers.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"lhs\">The left hand side of the comparison.</param>")
            .AppendLineIndent("/// <param name=\"rhs\">The right hand side of the comparison.</param>")
            .AppendLineIndent("/// <returns>")
            .AppendLineIndent("/// 0 if the numbers are equal, -1 if <paramref name=\"lhs\"/> is less than <paramref name=\"rhs\"/>,")
            .AppendLineIndent("/// and 1 if <paramref name=\"lhs\"/> is greater than <paramref name=\"rhs\"/>.")
            .AppendLineIndent("/// </returns>")
            .AppendIndent("public static int Compare(in ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" lhs, in ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" rhs)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBlockIndent(
                """
                if (lhs.ValueKind != rhs.ValueKind)
                {
                    // We can't be equal if we are not the same underlying type
                    return lhs.IsNullOrUndefined() ? 1 : -1;
                }

                if (lhs.IsNull())
                {
                    // Nulls are always equal
                    return 0;
                }
                """)

                .AppendSeparatorLine()
                .AppendIndent("if (lhs.")
                .Append(backingName)
                .Append(" == Backing.Number && rhs.")
                .Append(backingName)
                .AppendLine(" == Backing.Number)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return BinaryJsonNumber.Compare(lhs.")
                    .Append(numberBacking)
                    .Append(", rhs.")
                    .Append(numberBacking)
                    .AppendLine(");")
                .PopIndent()
                .AppendLineIndent("}")

                .AppendSeparatorLine()
                .AppendIndent("if (lhs.")
                .Append(backingName)
                .Append(" == Backing.Number && rhs.")
                .Append(backingName)
                .AppendLine(" == Backing.JsonElement)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return BinaryJsonNumber.Compare(lhs.")
                    .Append(numberBacking)
                    .Append(", rhs.")
                    .Append(jsonElementBacking)
                    .AppendLine(");")
                .PopIndent()
                .AppendLineIndent("}")

                .AppendSeparatorLine()
                .AppendIndent("if (lhs.")
                .Append(backingName)
                .Append(" == Backing.JsonElement && rhs.")
                .Append(backingName)
                .AppendLine(" == Backing.Number)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return BinaryJsonNumber.Compare(lhs.")
                    .Append(jsonElementBacking)
                    .Append(", rhs.")
                    .Append(numberBacking)
                    .AppendLine(");")
                .PopIndent()
                .AppendLineIndent("}")

                .AppendSeparatorLine()
                .AppendIndent("if (lhs.")
                .Append(backingName)
                .Append(" == Backing.JsonElement && rhs.")
                .Append(backingName)
                .Append(" == Backing.JsonElement && rhs.")
                .Append(jsonElementBacking)
                .AppendLine(".ValueKind == JsonValueKind.Number)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return JsonValueHelpers.NumericCompare(lhs.")
                    .Append(jsonElementBacking)
                    .Append(", rhs.")
                    .Append(jsonElementBacking)
                    .AppendLine(");")
                .PopIndent()
                .AppendLineIndent("}")

                .AppendSeparatorLine()
                .AppendLineIndent("throw new InvalidOperationException();")

            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendNumericUnaryOperator(this CodeGenerator generator, TypeDeclaration typeDeclaration, string op, string summary)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendBlockIndentWithPrefix(summary, "/// ")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value on which to operate.</param>")
            .AppendLineIndent("/// <returns>The result of the operation.</returns>")
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" operator ")
            .Append(op)
            .Append("(")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendIndent("BinaryJsonNumber num = value.AsBinaryJsonNumber;")
                .AppendIndent("return new(num")
                .Append(op)
                .AppendLine(");")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendNumericBinaryOperator(this CodeGenerator generator, TypeDeclaration typeDeclaration, string op, string summary)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendBlockIndentWithPrefix(summary, "/// ")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"left\">The left hand side of the binary operator.</param>")
            .AppendLineIndent("/// <param name=\"right\">The right hand side of the binary operator.</param>")
            .AppendLineIndent("/// <returns>The result of the operation.</returns>")
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" operator ")
            .Append(op)
            .Append("(")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" left, ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" right)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendIndent("return new(left.AsBinaryJsonNumber ")
                .Append(op)
                .AppendLine(" right.AsBinaryJsonNumber);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendNumericComparison(this CodeGenerator generator, TypeDeclaration typeDeclaration, string op, string summary, string returns)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendBlockIndentWithPrefix(summary, "/// ")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"left\">The LHS of the comparison.</param>")
            .AppendLineIndent("/// <param name=\"right\">The RHS of the comparison.</param>")
            .AppendLineIndent("/// <returns>")
            .AppendBlockIndentWithPrefix(returns, "/// ")
            .AppendLineIndent("/// </returns>")
            .AppendIndent("public static bool operator ")
            .Append(op)
            .Append("(")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" left, ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" right)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendIndent("return left.IsNotNullOrUndefined() && right.IsNotNullOrUndefined() && Compare(left, right) ")
                .Append(op)
                .AppendLine(" 0;")
            .PopIndent()
            .AppendLineIndent("}");
    }
}