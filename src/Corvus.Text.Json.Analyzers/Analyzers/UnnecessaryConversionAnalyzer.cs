// <copyright file="UnnecessaryConversionAnalyzer.cs" company="Endjin Limited">
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
/// CTJ002: Detects explicit casts or conversion method calls in argument positions where
/// the target parameter type has an implicit conversion from the original (pre-cast) type.
/// For example, <c>(string)element</c> passed to a <c>Source</c> parameter is unnecessary
/// when <c>Source</c> has an implicit conversion from <c>JsonElement</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnnecessaryConversionAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [DiagnosticDescriptors.UnnecessaryConversion];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeArgument, SyntaxKind.Argument);
    }

    private static void AnalyzeArgument(SyntaxNodeAnalysisContext context)
    {
        var argument = (ArgumentSyntax)context.Node;
        ExpressionSyntax expression = argument.Expression;

        // We're looking for cast expressions: (Type)expr
        if (expression is not CastExpressionSyntax castExpression)
        {
            return;
        }

        ExpressionSyntax innerExpression = castExpression.Expression;

        // Get the type of the original (pre-cast) expression.
        TypeInfo innerTypeInfo = context.SemanticModel.GetTypeInfo(innerExpression, context.CancellationToken);
        ITypeSymbol? originalType = innerTypeInfo.Type;
        if (originalType is null)
        {
            return;
        }

        // Get the cast-target type (the explicit conversion type).
        TypeInfo castTypeInfo = context.SemanticModel.GetTypeInfo(castExpression.Type, context.CancellationToken);
        ITypeSymbol? castType = castTypeInfo.Type;
        if (castType is null)
        {
            return;
        }

        // Find the target parameter type.
        ITypeSymbol? parameterType = GetParameterType(argument, context.SemanticModel, context.CancellationToken);
        if (parameterType is null)
        {
            return;
        }

        // If the parameter type is the same as the cast type, the cast might be necessary.
        // We only flag it if the parameter type has an implicit conversion from the original type,
        // making the intermediate cast unnecessary.
        if (SymbolEqualityComparer.Default.Equals(parameterType, castType))
        {
            return;
        }

        // Check if the parameter type has an implicit conversion from the original (pre-cast) type.
        if (HasImplicitConversion(originalType, parameterType, context.SemanticModel.Compilation))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnnecessaryConversion,
                    castExpression.GetLocation(),
                    castType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }
    }

    private static ITypeSymbol? GetParameterType(
        ArgumentSyntax argument,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        if (argument.Parent is not ArgumentListSyntax argList)
        {
            return null;
        }

        int argIndex = argList.Arguments.IndexOf(argument);
        if (argIndex < 0)
        {
            return null;
        }

        if (argList.Parent is InvocationExpressionSyntax invocation)
        {
            SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellationToken);
            if (symbolInfo.Symbol is IMethodSymbol method &&
                argIndex < method.Parameters.Length)
            {
                return method.Parameters[argIndex].Type;
            }
        }
        else if (argList.Parent is ObjectCreationExpressionSyntax creation)
        {
            SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(creation, cancellationToken);
            if (symbolInfo.Symbol is IMethodSymbol ctor &&
                argIndex < ctor.Parameters.Length)
            {
                return ctor.Parameters[argIndex].Type;
            }
        }

        return null;
    }

    private static bool HasImplicitConversion(
        ITypeSymbol sourceType,
        ITypeSymbol targetType,
        Compilation compilation)
    {
        // Check user-defined implicit conversion operators on the target type.
        foreach (IMethodSymbol method in targetType.GetMembers("op_Implicit").OfType<IMethodSymbol>())
        {
            if (method.Parameters.Length == 1 &&
                IsAssignableFrom(method.Parameters[0].Type, sourceType, compilation))
            {
                return true;
            }
        }

        // Also check implicit operators defined on the source type.
        foreach (IMethodSymbol method in sourceType.GetMembers("op_Implicit").OfType<IMethodSymbol>())
        {
            if (method.ReturnType is not null &&
                IsAssignableFrom(targetType, method.ReturnType, compilation))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAssignableFrom(ITypeSymbol target, ITypeSymbol source, Compilation compilation)
    {
        Conversion conversion = compilation.ClassifyConversion(source, target);
        return conversion.IsIdentity || conversion.IsImplicit;
    }
}