// <copyright file="MissingDisposeAnalyzer.cs" company="Endjin Limited">
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
/// CTJ004/CTJ005/CTJ006: Detects factory method calls that return disposable Corvus.Text.Json types
/// (ParsedJsonDocument, JsonWorkspace, JsonDocumentBuilder) without being wrapped in a <c>using</c>
/// statement or having <c>Dispose()</c> called.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingDisposeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [
            DiagnosticDescriptors.MissingDisposeOnParsedJsonDocument,
            DiagnosticDescriptors.MissingDisposeOnJsonWorkspace,
            DiagnosticDescriptors.MissingDisposeOnJsonDocumentBuilder,
        ];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeLocalDeclaration, SyntaxKind.LocalDeclarationStatement);
        context.RegisterSyntaxNodeAction(AnalyzeExpressionStatement, SyntaxKind.ExpressionStatement);
    }

    private static void AnalyzeLocalDeclaration(SyntaxNodeAnalysisContext context)
    {
        var localDecl = (LocalDeclarationStatementSyntax)context.Node;

        // If already a using declaration, it's fine.
        if (localDecl.UsingKeyword != default)
        {
            return;
        }

        // Check if this local declaration is inside a using block.
        if (localDecl.Parent is BlockSyntax block && block.Parent is UsingStatementSyntax)
        {
            return;
        }

        foreach (VariableDeclaratorSyntax declarator in localDecl.Declaration.Variables)
        {
            if (declarator.Initializer?.Value is null)
            {
                continue;
            }

            ITypeSymbol? type = context.SemanticModel.GetTypeInfo(declarator.Initializer.Value, context.CancellationToken).Type;
            if (type is null)
            {
                continue;
            }

            DiagnosticDescriptor? descriptor = GetDescriptorForType(type);
            if (descriptor is null)
            {
                continue;
            }

            // Check if Dispose() is called on this variable in the containing block.
            string variableName = declarator.Identifier.ValueText;
            if (IsDisposedInScope(variableName, localDecl, context.SemanticModel))
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    descriptor,
                    declarator.Initializer.Value.GetLocation(),
                    type.Name));
        }
    }

    private static void AnalyzeExpressionStatement(SyntaxNodeAnalysisContext context)
    {
        var exprStmt = (ExpressionStatementSyntax)context.Node;

        // Detect: ParsedJsonDocument<T>.Parse(...); — result discarded entirely.
        if (exprStmt.Expression is InvocationExpressionSyntax invocation)
        {
            ITypeSymbol? returnType = context.SemanticModel.GetTypeInfo(invocation, context.CancellationToken).Type;
            if (returnType is null)
            {
                return;
            }

            DiagnosticDescriptor? descriptor = GetDescriptorForType(returnType);
            if (descriptor is not null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        descriptor,
                        invocation.GetLocation(),
                        returnType.Name));
            }
        }
    }

    private static DiagnosticDescriptor? GetDescriptorForType(ITypeSymbol type)
    {
        string typeName = type.Name;
        string? containingNamespace = type.ContainingNamespace?.ToDisplayString();

        if (containingNamespace != "Corvus.Text.Json")
        {
            return null;
        }

        return typeName switch
        {
            "ParsedJsonDocument" => DiagnosticDescriptors.MissingDisposeOnParsedJsonDocument,
            "JsonWorkspace" => DiagnosticDescriptors.MissingDisposeOnJsonWorkspace,
            "JsonDocumentBuilder" => DiagnosticDescriptors.MissingDisposeOnJsonDocumentBuilder,
            _ => null,
        };
    }

    private static bool IsDisposedInScope(
        string variableName,
        LocalDeclarationStatementSyntax declaration,
        SemanticModel semanticModel)
    {
        if (declaration.Parent is not BlockSyntax block)
        {
            return false;
        }

        // Look for variableName.Dispose() or using statements referencing this variable
        // after the declaration.
        bool passedDeclaration = false;
        foreach (StatementSyntax statement in block.Statements)
        {
            if (statement == declaration)
            {
                passedDeclaration = true;
                continue;
            }

            if (!passedDeclaration)
            {
                continue;
            }

            // Check for: variableName.Dispose();
            if (statement is ExpressionStatementSyntax exprStmt &&
                exprStmt.Expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } &&
                memberAccess.Name.Identifier.ValueText == "Dispose" &&
                memberAccess.Expression is IdentifierNameSyntax identifier &&
                identifier.Identifier.ValueText == variableName)
            {
                return true;
            }

            // Check for: using (variableName) or using var x = variableName
            if (statement is UsingStatementSyntax usingStmt &&
                usingStmt.Expression is IdentifierNameSyntax usingId &&
                usingId.Identifier.ValueText == variableName)
            {
                return true;
            }
        }

        // Also check if the variable is passed to a using block via a return or
        // assigned to a field (which indicates ownership transfer).
        return false;
    }
}