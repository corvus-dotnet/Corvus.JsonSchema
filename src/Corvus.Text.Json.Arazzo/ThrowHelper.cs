// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// Centralized exception-throwing helpers for the Arazzo runtime.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw. All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    /// <summary>Throws when a regex or jsonpath criterion is compiled without the required context expression.</summary>
    /// <param name="paramName">The name of the offending parameter.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowContextExpressionRequired(string paramName)
        => throw new ArgumentException(SR.ContextExpressionRequired, paramName);

    /// <summary>Throws when the condition parser finds unexpected trailing content.</summary>
    /// <param name="position">The position at which the trailing content was found.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUnexpectedTrailingContent(int position)
        => throw new FormatException(SR.Format(SR.UnexpectedTrailingContentInCondition, position));

    /// <summary>Throws when the condition parser expects a closing parenthesis.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowExpectedClosingParen()
        => throw new FormatException(SR.ExpectedClosingParenInCondition);

    /// <summary>Throws when the condition parser expects an operand.</summary>
    /// <param name="position">The position at which an operand was expected.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowExpectedOperand(int position)
        => throw new FormatException(SR.Format(SR.ExpectedOperandInCondition, position));

    /// <summary>Throws when the condition parser encounters an unrecognized literal.</summary>
    /// <param name="literal">The unrecognized literal token.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUnrecognizedLiteral(string literal)
        => throw new FormatException(SR.Format(SR.UnrecognizedLiteralInCondition, literal));
}