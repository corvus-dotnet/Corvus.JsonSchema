// <copyright file="PropertyNameAllocationAnalyzer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Corvus.Text.Json.Analyzers;

/// <summary>
/// CTJ008: Detects <c>property.Name == "literal"</c> and similar patterns where
/// <c>property.NameEquals("literal"u8)</c> avoids the string allocation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PropertyNameAllocationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [DiagnosticDescriptors.PreferNonAllocatingPropertyName];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Detect property.Name == "literal" and property.Name != "literal"
        context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);

        // Detect property.Name.Equals("literal") and string.Equals(property.Name, "literal")
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeBinaryExpression(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;

        // Check if one side is property.Name and the other is a string literal.
        (ExpressionSyntax? nameAccess, LiteralExpressionSyntax? literal) = GetNameAndLiteral(binary.Left, binary.Right);
        if (nameAccess is null || literal is null)
        {
            return;
        }

        if (!IsJsonPropertyNameAccess(nameAccess, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.PreferNonAllocatingPropertyName,
                binary.GetLocation(),
                literal.Token.ValueText));
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Pattern: property.Name.Equals("literal")
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.ValueText == "Equals" &&
            invocation.ArgumentList.Arguments.Count == 1 &&
            invocation.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression) &&
            IsJsonPropertyNameAccess(memberAccess.Expression, context.SemanticModel, context.CancellationToken))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.PreferNonAllocatingPropertyName,
                    invocation.GetLocation(),
                    literal.Token.ValueText));
            return;
        }

        // Pattern: string.Equals(property.Name, "literal") or string.Equals("literal", property.Name)
        if (invocation.Expression is MemberAccessExpressionSyntax staticMemberAccess &&
            staticMemberAccess.Name.Identifier.ValueText == "Equals" &&
            invocation.ArgumentList.Arguments.Count >= 2)
        {
            SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
            if (symbolInfo.Symbol is IMethodSymbol method &&
                method.IsStatic &&
                method.ContainingType.SpecialType == SpecialType.System_String)
            {
                ExpressionSyntax arg0 = invocation.ArgumentList.Arguments[0].Expression;
                ExpressionSyntax arg1 = invocation.ArgumentList.Arguments[1].Expression;

                (ExpressionSyntax? nameExpr, LiteralExpressionSyntax? literalExpr) = GetNameAndLiteral(arg0, arg1);
                if (nameExpr is not null && literalExpr is not null &&
                    IsJsonPropertyNameAccess(nameExpr, context.SemanticModel, context.CancellationToken))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.PreferNonAllocatingPropertyName,
                            invocation.GetLocation(),
                            literalExpr.Token.ValueText));
                }
            }
        }
    }

    private static (ExpressionSyntax? NameAccess, LiteralExpressionSyntax? Literal) GetNameAndLiteral(
        ExpressionSyntax left,
        ExpressionSyntax right)
    {
        if (right is LiteralExpressionSyntax rightLiteral && rightLiteral.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return (left, rightLiteral);
        }

        if (left is LiteralExpressionSyntax leftLiteral && leftLiteral.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return (right, leftLiteral);
        }

        return (null, null);
    }

    private static bool IsJsonPropertyNameAccess(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        // Check for: something.Name where something is a JsonProperty<T>
        if (expression is not MemberAccessExpressionSyntax memberAccess ||
            memberAccess.Name.Identifier.ValueText != "Name")
        {
            return false;
        }

        ITypeSymbol? receiverType = semanticModel.GetTypeInfo(memberAccess.Expression, cancellationToken).Type;
        if (receiverType is null)
        {
            return false;
        }

        // Check if the receiver type is JsonProperty<T> in the Corvus.Text.Json namespace
        // and has a NameEquals method (our indicator it's the right type).
        return receiverType.Name == "JsonProperty" &&
            receiverType.ContainingNamespace?.ToDisplayString() == "Corvus.Text.Json";
    }
}