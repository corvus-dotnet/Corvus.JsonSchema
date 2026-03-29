// <copyright file="CodeGeneratorExtensions.Operators.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Code generator extensions for operator overloading functionality.
/// </summary>
internal static partial class CodeGeneratorExtensions
{
    /// <summary>
    /// Appends a binary operator for the <paramref name="typeDeclaration"/>
    /// to JsonAny.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to add the operator.</param>
    /// <param name="returnType">The return type of the operator.</param>
    /// <param name="operatorSymbol">The symbol to inject for the operator.</param>
    /// <param name="operatorBody">The body to inject for the operator.</param>
    /// <param name="returnValueDocumentation">The return value documentation.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendBinaryOperator(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string returnType,
        string operatorSymbol,
        string operatorBody,
        string returnValueDocumentation)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Operator ")
            .Append(operatorSymbol)
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"left\">The lhs of the operator.</param>")
            .AppendLineIndent("/// <param name=\"right\">The rhs of the operator.</param>")
            .AppendLineIndent("/// <returns>")
            .AppendBlockIndentWithPrefix(returnValueDocumentation, "/// ")
            .AppendLineIndent("/// </returns>")
            .AppendIndent("public static ")
            .Append(returnType)
            .Append(" operator ")
            .Append(operatorSymbol)
            .Append("(in ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" left, in ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" right)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBlockIndent(operatorBody)
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends a binary operator for the <paramref name="typeDeclaration"/>
    /// to JsonAny.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="leftType">The type to the left of the opertor.</param>
    /// <param name="rightType">The type to the right of the operator</param>
    /// <param name="returnType">The return type of the operator.</param>
    /// <param name="operatorSymbol">The symbol to inject for the operator.</param>
    /// <param name="operatorBody">The body to inject for the operator.</param>
    /// <param name="returnValueDocumentation">The return value documentation.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendBinaryOperator(
        this CodeGenerator generator,
        string leftType,
        string rightType,
        string returnType,
        string operatorSymbol,
        string operatorBody,
        string returnValueDocumentation)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Operator ")
            .Append(operatorSymbol)
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"left\">The lhs of the operator.</param>")
            .AppendLineIndent("/// <param name=\"right\">The rhs of the operator.</param>")
            .AppendLineIndent("/// <returns>")
            .AppendBlockIndentWithPrefix(returnValueDocumentation, "/// ")
            .AppendLineIndent("/// </returns>")
            .AppendIndent("public static ")
            .Append(returnType)
            .Append(" operator ")
            .Append(operatorSymbol)
            .Append("(in ")
            .Append(leftType)
            .Append(" left, in ")
            .Append(rightType)
            .AppendLine(" right)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBlockIndent(operatorBody)
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends a binary operator for the <paramref name="typeDeclaration"/>
    /// to JsonAny.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to add the operator.</param>
    /// <param name="rightType">The type to the right of the operator</param>
    /// <param name="returnType">The return type of the operator.</param>
    /// <param name="operatorSymbol">The symbol to inject for the operator.</param>
    /// <param name="operatorBody">The body to inject for the operator.</param>
    /// <param name="returnValueDocumentation">The return value documentation.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendBinaryOperator(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string rightType,
        string returnType,
        string operatorSymbol,
        string operatorBody,
        string returnValueDocumentation)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Operator ")
            .Append(operatorSymbol)
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"left\">The lhs of the operator.</param>")
            .AppendLineIndent("/// <param name=\"right\">The rhs of the operator.</param>")
            .AppendLineIndent("/// <returns>")
            .AppendBlockIndentWithPrefix(returnValueDocumentation, "/// ")
            .AppendLineIndent("/// </returns>")
            .AppendIndent("public static ")
            .Append(returnType)
            .Append(" operator ")
            .Append(operatorSymbol)
            .Append("(in ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" left, in ")
            .Append(rightType)
            .AppendLine(" right)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBlockIndent(operatorBody)
            .PopIndent()
            .AppendLineIndent("}");
    }
}