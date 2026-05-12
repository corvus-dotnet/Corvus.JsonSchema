// <copyright file="IgnoredValidationResultAnalyzer.cs" company="Endjin Limited">
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
/// CTJ007: Detects calls to <c>EvaluateSchema()</c> whose return value (<c>bool</c>)
/// is discarded. Validation is pointless if the result is not checked.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IgnoredValidationResultAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [DiagnosticDescriptors.IgnoredSchemaValidationResult];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeExpressionStatement, SyntaxKind.ExpressionStatement);
    }

    private static void AnalyzeExpressionStatement(SyntaxNodeAnalysisContext context)
    {
        var exprStmt = (ExpressionStatementSyntax)context.Node;

        // We're looking for: something.EvaluateSchema(...); — where the result is discarded.
        if (exprStmt.Expression is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        // Get the method name.
        string? methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };

        // Only fire when the result is discarded AND no arguments are passed.
        // EvaluateSchema(collector) has side effects (populates the collector),
        // so discarding the bool return value is legitimate in that case.
        if (methodName != "EvaluateSchema")
        {
            return;
        }

        if (invocation.ArgumentList.Arguments.Count > 0)
        {
            return;
        }

        // Verify it's actually a method that returns bool from the Corvus.Text.Json namespace.
        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (method.ReturnType.SpecialType != SpecialType.System_Boolean)
        {
            return;
        }

        // Check the containing type is in Corvus.Text.Json or implements IJsonElement<T>.
        if (!IsCorvusJsonType(method.ContainingType))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.IgnoredSchemaValidationResult,
                invocation.GetLocation()));
    }

    private static bool IsCorvusJsonType(INamedTypeSymbol? type)
    {
        if (type is null)
        {
            return false;
        }

        string? ns = type.ContainingNamespace?.ToDisplayString();
        if (ns is not null && (ns == "Corvus.Text.Json" || ns.StartsWith("Corvus.Text.Json.")))
        {
            return true;
        }

        // Also check interfaces for IJsonElement<T>.
        foreach (INamedTypeSymbol iface in type.AllInterfaces)
        {
            string? ifaceNs = iface.ContainingNamespace?.ToDisplayString();
            if (ifaceNs is not null && (ifaceNs == "Corvus.Text.Json" || ifaceNs.StartsWith("Corvus.Text.Json.")) && iface.Name == "IJsonElement")
            {
                return true;
            }
        }

        return false;
    }
}