// <copyright file="PreferMemoryParseAnalyzer.cs" company="Endjin Limited">
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
/// CTJ010: Detects calls to <c>ParsedJsonDocument&lt;T&gt;.Parse(string)</c> or
/// <c>JsonElement.ParseValue(string)</c> where a more efficient overload could be used.
/// Detects two patterns:
/// <list type="bullet">
/// <item>Pattern A: Encoding roundtrip — <c>Parse(Encoding.UTF8.GetString(bytes))</c></item>
/// <item>Pattern B: String literal — <c>ParseValue("...")</c> where <c>"..."u8</c> would work</item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferMemoryParseAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [DiagnosticDescriptors.PreferMemoryParse];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Get the method being called.
        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        // Only check Parse and ParseValue methods.
        if (method.Name != "Parse" && method.Name != "ParseValue")
        {
            return;
        }

        // Must be on a Corvus.Text.Json type.
        string? containingNs = method.ContainingType?.ContainingNamespace?.ToDisplayString();
        if (containingNs is null || (containingNs != "Corvus.Text.Json" && !containingNs.StartsWith("Corvus.Text.Json.")))
        {
            return;
        }

        // Must take a string parameter in the first position.
        if (method.Parameters.Length == 0 || method.Parameters[0].Type.SpecialType != SpecialType.System_String)
        {
            return;
        }

        // Get the first argument.
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return;
        }

        ExpressionSyntax arg = invocation.ArgumentList.Arguments[0].Expression;

        // Pattern A: Encoding.UTF8.GetString(bytes)
        if (IsEncodingGetString(arg, context.SemanticModel, context.CancellationToken))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.PreferMemoryParse,
                    invocation.GetLocation(),
                    "Pass the byte array directly using .AsMemory() instead of converting to string first"));
            return;
        }

        // Pattern B: String literal — suggest u8 suffix.
        if (arg is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            // Check if there's a ReadOnlySpan<byte> or ReadOnlyMemory<byte> overload.
            if (HasBytesOverload(method))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.PreferMemoryParse,
                        arg.GetLocation(),
                        "Use a UTF-8 string literal (\"...\"u8) or ReadOnlyMemory<byte> overload for better performance"));
            }
        }
    }

    private static bool IsEncodingGetString(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        // Check for: Encoding.UTF8.GetString(...)
        if (expression is not InvocationExpressionSyntax innerInvocation)
        {
            return false;
        }

        SymbolInfo innerSymbol = semanticModel.GetSymbolInfo(innerInvocation, cancellationToken);
        if (innerSymbol.Symbol is not IMethodSymbol innerMethod)
        {
            return false;
        }

        return innerMethod.Name == "GetString" &&
               innerMethod.ContainingType.Name == "Encoding" &&
               innerMethod.ContainingType.ContainingNamespace?.ToDisplayString() == "System.Text";
    }

    private static bool HasBytesOverload(IMethodSymbol method)
    {
        ITypeSymbol containingType = method.ContainingType;

        foreach (IMethodSymbol candidate in containingType.GetMembers(method.Name).OfType<IMethodSymbol>())
        {
            if (ReferenceEquals(candidate, method))
            {
                continue;
            }

            if (candidate.Parameters.Length < 1)
            {
                continue;
            }

            ITypeSymbol firstParamType = candidate.Parameters[0].Type;

            // Check for ReadOnlySpan<byte> or ReadOnlyMemory<byte>.
            if (firstParamType is INamedTypeSymbol namedType &&
                namedType.TypeArguments.Length == 1 &&
                namedType.TypeArguments[0].SpecialType == SpecialType.System_Byte)
            {
                string name = namedType.Name;
                if (name == "ReadOnlySpan" || name == "ReadOnlyMemory")
                {
                    return true;
                }
            }
        }

        return false;
    }
}