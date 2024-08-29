// <copyright file="PublicCodeGeneratorExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Publically available code generator extensions.
/// </summary>
public static class PublicCodeGeneratorExtensions
{
    /// <summary>
    /// Gets the name for a static readonly field.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="keyword">The keyword for which to get the field name.</param>
    /// <param name="constantIndex">The constant index, or null if this is a single constant.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetValidationConstantFieldNameForKeyword(
        this CodeGenerator generator,
        IKeyword keyword,
        int? constantIndex = null)
    {
        generator.ValidationClassName();
        return generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, rootScope: generator.ValidationClassScope(), suffix: constantIndex is int index ? index.ToString() : null);
    }

    /// <summary>
    /// Appends the relevant operator.
    /// </summary>
    /// <param name="generator">The generator to which to append the operator.</param>
    /// <param name="op">The operator to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendOperator(this CodeGenerator generator, Operator op)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        switch (op)
        {
            case Operator.Equals:
                return generator.Append("==");
            case Operator.NotEquals:
                return generator.Append("!=");
            case Operator.LessThan:
                return generator.Append("<");
            case Operator.LessThanOrEquals:
                return generator.Append("<=");
            case Operator.GreaterThan:
                return generator.Append(">");
            case Operator.GreaterThanOrEquals:
                return generator.Append(">=");
            default:
                Debug.Fail($"Unexpected operator {op}");
                return generator;
        }
    }

    /// <summary>
    /// Appends the text for the relevant operator.
    /// </summary>
    /// <param name="generator">The generator to which to append the operator.</param>
    /// <param name="op">The operator to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendTextForOperator(this CodeGenerator generator, Operator op)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        switch (op)
        {
            case Operator.Equals:
                return generator.Append("equals");
            case Operator.NotEquals:
                return generator.Append("does not equal");
            case Operator.LessThan:
                return generator.Append("is less than");
            case Operator.LessThanOrEquals:
                return generator.Append("is less than or equal to");
            case Operator.GreaterThan:
                return generator.Append("is greater than");
            case Operator.GreaterThanOrEquals:
                return generator.Append("is greater than or equal to");
            case Operator.MultipleOf:
                return generator.Append("is a multiple of");
            default:
                Debug.Fail($"Unexpected operator {op}");
                return generator;
        }
    }

    /// <summary>
    /// Appends the text for the inverse of the relevant operator.
    /// </summary>
    /// <param name="generator">The generator to which to append the text.</param>
    /// <param name="op">The operator for which to append the text.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <remarks>
    /// This is commonly used for failure cases.
    /// </remarks>
    public static CodeGenerator AppendTextForInverseOperator(this CodeGenerator generator, Operator op)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        switch (op)
        {
            case Operator.Equals:
                return generator.Append("does not equal");
            case Operator.NotEquals:
                return generator.Append("equals");
            case Operator.LessThan:
                return generator.Append("is greater than or equal to");
            case Operator.LessThanOrEquals:
                return generator.Append("is greater than");
            case Operator.GreaterThan:
                return generator.Append("is less than or equal to");
            case Operator.GreaterThanOrEquals:
                return generator.Append("is less than");
            case Operator.MultipleOf:
                return generator.Append("is not a multiple of");
            default:
                Debug.Fail($"Unexpected operator {op}");
                return generator;
        }
    }

    /// <summary>
    /// Append the validation result for a keyword.
    /// </summary>
    /// <param name="generator">The generator to which to append the ignored keyword validation code.</param>
    /// <param name="isValid">Whether the result should be valid.</param>
    /// <param name="keyword">The keyword that has been ignored.</param>
    /// <param name="validationContextIdentifier">The identifier for the validation context to update.</param>
    /// <param name="reasonText">The reason for ignoring the keyword.</param>
    /// <param name="useInterpolatedString">If <see langword="true"/>, then the message string will be an interpolated string.</param>
    /// <param name="withKeyword">If <see langword="true"/>, then the keyword will be passed to the call to <c>WithResult()</c>.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendKeywordValidationResult(
        this CodeGenerator generator,
        bool isValid,
        IKeyword keyword,
        string validationContextIdentifier,
        string reasonText,
        bool useInterpolatedString = false,
        bool withKeyword = false)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .AppendIndent(validationContextIdentifier)
            .Append(" = ")
            .Append(validationContextIdentifier)
            .Append(".WithResult(isValid: ")
            .Append(isValid ? "true" : "false")
            .Append(", ");

        if (useInterpolatedString)
        {
            generator
                .Append('$');
        }

        return generator
            .Append("\"Validation ")
            .Append(keyword.Keyword)
            .Append(" - ")
            .Append(SymbolDisplay.FormatLiteral(reasonText, false))
            .AppendLine("\"", withKeyword ? ", " : string.Empty, withKeyword ? SymbolDisplay.FormatLiteral(keyword.Keyword, true) : string.Empty, ");");
    }

    /// <summary>
    /// Append the validation result for a keyword.
    /// </summary>
    /// <param name="generator">The generator to which to append the keyword validation code.</param>
    /// <param name="isValid">Whether the result should be valid.</param>
    /// <param name="keyword">The keyword that has been ignored.</param>
    /// <param name="validationContextIdentifier">The identifier for the validation context to update.</param>
    /// <param name="appendReasonText">An function which will append the validation reason to the (optionally interpolated) string for the keyword.</param>
    /// <param name="useInterpolatedString">If <see langword="true"/>, then the message string will be an interpolated string.</param>
    /// <param name="withKeyword">If <see langword="true"/>, then the keyword will be passed to the call to <c>WithResult()</c>.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendKeywordValidationResult(
        this CodeGenerator generator,
        bool isValid,
        IKeyword keyword,
        string validationContextIdentifier,
        Action<CodeGenerator> appendReasonText,
        bool useInterpolatedString = false,
        bool withKeyword = false)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .AppendIndent(validationContextIdentifier)
            .Append(" = ")
            .Append(validationContextIdentifier)
            .Append(".WithResult(isValid: ")
            .Append(isValid ? "true" : "false")
            .Append(", ");

        if (useInterpolatedString)
        {
            generator
                .Append('$');
        }

        generator
            .Append("\"Validation ")
            .Append(keyword.Keyword)
            .Append(" - ");

        appendReasonText(generator);

        return generator
            .AppendLine("\"", withKeyword ? ", " : string.Empty, withKeyword ? SymbolDisplay.FormatLiteral(keyword.Keyword, true) : string.Empty, ");");
    }

    /// <summary>
    /// Appends a blank line if the previous line ended with a closing brace.
    /// </summary>
    /// <param name="generator">The generator to which to append the separator line.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendSeparatorLine(this CodeGenerator generator)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if ((generator.ScopeType == ScopeType.Type && generator.EndsWith($";{Environment.NewLine}")) ||
            generator.EndsWith($"}}{Environment.NewLine}") ||
            generator.EndsWith($"#endif{Environment.NewLine}"))
        {
            // Append a blank line
            generator.AppendLine();
        }

        return generator;
    }
}