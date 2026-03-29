// <copyright file="Utf8StringLiteralAnalyzer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Corvus.Text.Json.Analyzers;

/// <summary>
/// CTJ001: Detects string literal arguments where a <c>ReadOnlySpan&lt;byte&gt;</c> overload
/// exists on the same method or indexer. Suggests using a UTF-8 string literal (<c>"..."u8</c>)
/// for better performance.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Utf8StringLiteralAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [DiagnosticDescriptors.PreferUtf8StringLiteral];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeArgument, SyntaxKind.Argument);
        context.RegisterSyntaxNodeAction(AnalyzeElementAccess, SyntaxKind.ElementAccessExpression);
    }

    private static void AnalyzeArgument(SyntaxNodeAnalysisContext context)
    {
        var argument = (ArgumentSyntax)context.Node;

        // Only fire on string literals (not interpolated, not variables).
        if (argument.Expression is not LiteralExpressionSyntax literal ||
            !literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return;
        }

        // Find the containing invocation.
        if (argument.Parent is not ArgumentListSyntax argList ||
            argList.Parent is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        // Get the method symbol.
        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        // Find which parameter index this argument maps to.
        int argIndex = argList.Arguments.IndexOf(argument);
        if (argIndex < 0 || argIndex >= method.Parameters.Length)
        {
            return;
        }

        IParameterSymbol parameter = method.Parameters[argIndex];

        // Only fire if the parameter is string (not already ReadOnlySpan<byte>).
        if (parameter.Type.SpecialType != SpecialType.System_String)
        {
            return;
        }

        // Check if the containing type has an overload with ReadOnlySpan<byte> in the same position.
        if (HasReadOnlySpanByteOverload(method, argIndex, context.SemanticModel.Compilation))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.PreferUtf8StringLiteral,
                    literal.GetLocation(),
                    literal.Token.ValueText));
        }
    }

    private static void AnalyzeElementAccess(SyntaxNodeAnalysisContext context)
    {
        var elementAccess = (ElementAccessExpressionSyntax)context.Node;

        if (elementAccess.ArgumentList.Arguments.Count != 1)
        {
            return;
        }

        ArgumentSyntax argument = elementAccess.ArgumentList.Arguments[0];
        if (argument.Expression is not LiteralExpressionSyntax literal ||
            !literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return;
        }

        // Get the indexer symbol.
        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(elementAccess, context.CancellationToken);
        if (symbolInfo.Symbol is not IPropertySymbol indexer || !indexer.IsIndexer)
        {
            return;
        }

        if (indexer.Parameters.Length != 1 ||
            indexer.Parameters[0].Type.SpecialType != SpecialType.System_String)
        {
            return;
        }

        // Check if the type has a ReadOnlySpan<byte> indexer.
        ITypeSymbol containingType = indexer.ContainingType;
        bool hasUtf8Indexer = containingType.GetMembers()
            .OfType<IPropertySymbol>()
            .Any(p => p.IsIndexer &&
                      p.Parameters.Length == 1 &&
                      IsReadOnlySpanOfByte(p.Parameters[0].Type));

        if (hasUtf8Indexer)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.PreferUtf8StringLiteral,
                    literal.GetLocation(),
                    literal.Token.ValueText));
        }
    }

    private static bool HasReadOnlySpanByteOverload(
        IMethodSymbol method,
        int parameterIndex,
        Compilation compilation)
    {
        ITypeSymbol containingType = method.ContainingType;

        foreach (IMethodSymbol candidate in containingType.GetMembers(method.Name).OfType<IMethodSymbol>())
        {
            if (ReferenceEquals(candidate, method))
            {
                continue;
            }

            if (candidate.Parameters.Length != method.Parameters.Length)
            {
                continue;
            }

            bool allMatch = true;
            for (int i = 0; i < candidate.Parameters.Length; i++)
            {
                if (i == parameterIndex)
                {
                    if (!IsReadOnlySpanOfByte(candidate.Parameters[i].Type))
                    {
                        allMatch = false;
                        break;
                    }
                }
                else
                {
                    if (!SymbolEqualityComparer.Default.Equals(
                            candidate.Parameters[i].Type,
                            method.Parameters[i].Type))
                    {
                        allMatch = false;
                        break;
                    }
                }
            }

            if (allMatch)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReadOnlySpanOfByte(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
               namedType.Name == "ReadOnlySpan" &&
               namedType.ContainingNamespace?.ToDisplayString() == "System" &&
               namedType.TypeArguments.Length == 1 &&
               namedType.TypeArguments[0].SpecialType == SpecialType.System_Byte;
    }
}