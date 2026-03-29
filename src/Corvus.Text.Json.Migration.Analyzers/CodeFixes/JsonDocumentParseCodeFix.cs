// <copyright file="JsonDocumentParseCodeFix.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Corvus.Text.Json.Migration.Analyzers;

/// <summary>
/// Code fix for CVJ008: replaces <c>JsonDocument.Parse(...)</c> with
/// <c>ParsedJsonDocument&lt;T&gt;.Parse(...)</c>. When a subsequent
/// <c>T.FromJson(doc.RootElement)</c> statement is found, collapses both
/// into the typed parse and a direct <c>.RootElement</c> access.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(JsonDocumentParseCodeFix))]
[Shared]
public sealed class JsonDocumentParseCodeFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.JsonDocumentParseMigration.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan);
            InvocationExpressionSyntax? invocation = node as InvocationExpressionSyntax
                ?? node.FirstAncestorOrSelf<InvocationExpressionSyntax>();

            if (invocation is null)
            {
                continue;
            }

            // Find the containing statement and check for the FromJson pattern.
            StatementSyntax? containingStmt = invocation.FirstAncestorOrSelf<StatementSyntax>();
            if (containingStmt is null)
            {
                continue;
            }

            string? docVarName = GetDeclaredVariableName(containingStmt, invocation);

            if (docVarName is not null &&
                containingStmt.Parent is BlockSyntax block &&
                TryFindFromJsonStatement(block, docVarName, containingStmt, out string? targetType, out StatementSyntax? fromJsonStmt))
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Replace with ParsedJsonDocument<{targetType}>.Parse and simplify",
                        createChangedDocument: ct => CollapseParseAndFromJsonAsync(
                            context.Document, containingStmt, fromJsonStmt!, docVarName, targetType!, invocation.ArgumentList, ct),
                        equivalenceKey: DiagnosticDescriptors.JsonDocumentParseMigration.Id),
                    diagnostic);
            }
            else
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Replace with ParsedJsonDocument<JsonElement>.Parse",
                        createChangedDocument: ct => SimpleReplaceAsync(
                            context.Document, invocation, ct),
                        equivalenceKey: DiagnosticDescriptors.JsonDocumentParseMigration.Id),
                    diagnostic);
            }
        }
    }

    private static async Task<Document> SimpleReplaceAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Replace JsonDocument.Parse(...) with ParsedJsonDocument<JsonElement>.Parse(...)
        MemberAccessExpressionSyntax newAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.ParseTypeName("ParsedJsonDocument<JsonElement>"),
            SyntaxFactory.IdentifierName("Parse"));

        InvocationExpressionSyntax newInvocation = invocation
            .WithExpression(newAccess)
            .WithTriviaFrom(invocation);

        return document.WithSyntaxRoot(root.ReplaceNode(invocation, newInvocation));
    }

    private static async Task<Document> CollapseParseAndFromJsonAsync(
        Document document,
        StatementSyntax parseStmt,
        StatementSyntax fromJsonStmt,
        string docVarName,
        string targetType,
        ArgumentListSyntax parseArgs,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || parseStmt.Parent is not BlockSyntax block)
        {
            return document;
        }

        SyntaxTriviaList leadingTrivia = parseStmt.GetLeadingTrivia();

        // Build: using var <docVar> = ParsedJsonDocument<T>.Parse(...);
        string parseArgsText = parseArgs.WithoutTrivia().ToFullString();
        StatementSyntax newParseStmt = SyntaxFactory.ParseStatement(
            $"using var {docVarName} = ParsedJsonDocument<{targetType}>.Parse{parseArgsText};\r\n")
            .WithLeadingTrivia(leadingTrivia);

        // Build: T element = <docVar>.RootElement;
        // Extract the variable name from the FromJson statement.
        string? elementVarName = GetDeclaredVariableName(fromJsonStmt, null);
        SyntaxTriviaList fromJsonTrivia = fromJsonStmt.GetLeadingTrivia();

        StatementSyntax newFromJsonStmt = SyntaxFactory.ParseStatement(
            $"{targetType} {elementVarName ?? "element"} = {docVarName}.RootElement;\r\n")
            .WithLeadingTrivia(fromJsonTrivia);

        // Rebuild the block replacing both statements.
        var newStatements = new List<StatementSyntax>();
        foreach (StatementSyntax stmt in block.Statements)
        {
            if (stmt == parseStmt)
            {
                newStatements.Add(newParseStmt);
            }
            else if (stmt == fromJsonStmt)
            {
                newStatements.Add(newFromJsonStmt);
            }
            else
            {
                newStatements.Add(stmt);
            }
        }

        BlockSyntax newBlock = block.WithStatements(SyntaxFactory.List(newStatements));
        return document.WithSyntaxRoot(root.ReplaceNode(block, newBlock));
    }

    /// <summary>
    /// Gets the variable name declared in a local declaration statement.
    /// If <paramref name="initializerExpr"/> is provided, only matches if the
    /// declarator's initializer contains that expression.
    /// </summary>
    private static string? GetDeclaredVariableName(
        StatementSyntax statement,
        InvocationExpressionSyntax? initializerExpr)
    {
        if (statement is not LocalDeclarationStatementSyntax localDecl)
        {
            return null;
        }

        foreach (VariableDeclaratorSyntax declarator in localDecl.Declaration.Variables)
        {
            if (initializerExpr is null || declarator.Initializer?.Value == initializerExpr)
            {
                return declarator.Identifier.Text;
            }
        }

        return null;
    }

    /// <summary>
    /// Looks for a statement like <c>T element = T.FromJson(docVar.RootElement);</c>
    /// following the parse statement in the same block.
    /// </summary>
    private static bool TryFindFromJsonStatement(
        BlockSyntax block,
        string docVarName,
        StatementSyntax afterStmt,
        out string? targetType,
        out StatementSyntax? fromJsonStmt)
    {
        targetType = null;
        fromJsonStmt = null;

        bool foundParseStmt = false;
        foreach (StatementSyntax stmt in block.Statements)
        {
            if (stmt == afterStmt)
            {
                foundParseStmt = true;
                continue;
            }

            if (!foundParseStmt)
            {
                continue;
            }

            // Look for: T var = T.FromJson(docVar.RootElement);
            if (stmt is LocalDeclarationStatementSyntax localDecl)
            {
                foreach (VariableDeclaratorSyntax declarator in localDecl.Declaration.Variables)
                {
                    if (declarator.Initializer?.Value is InvocationExpressionSyntax inv &&
                        inv.Expression is MemberAccessExpressionSyntax ma &&
                        (ma.Name.Identifier.Text == "FromJson" || ma.Name.Identifier.Text == "From") &&
                        inv.ArgumentList.Arguments.Count == 1 &&
                        IsDocRootElementAccess(inv.ArgumentList.Arguments[0].Expression, docVarName))
                    {
                        targetType = ma.Expression.WithoutTrivia().ToFullString();
                        fromJsonStmt = stmt;
                        return true;
                    }
                }
            }

            // Only check the immediately following statement.
            break;
        }

        return false;
    }

    private static bool IsDocRootElementAccess(ExpressionSyntax expression, string docVarName)
    {
        return expression is MemberAccessExpressionSyntax ma &&
               ma.Name.Identifier.Text == "RootElement" &&
               ma.Expression is IdentifierNameSyntax id &&
               id.Identifier.Text == docVarName;
    }
}