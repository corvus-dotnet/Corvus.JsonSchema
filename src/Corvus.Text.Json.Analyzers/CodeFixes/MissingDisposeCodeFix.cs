// <copyright file="MissingDisposeCodeFix.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Corvus.Text.Json.Analyzers;

/// <summary>
/// Code fix for CTJ004/CTJ005/CTJ006: Adds <c>using</c> keyword to the local declaration.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingDisposeCodeFix))]
[Shared]
public sealed class MissingDisposeCodeFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ["CTJ004", "CTJ005", "CTJ006"];

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

            // Walk up to find the LocalDeclarationStatementSyntax.
            LocalDeclarationStatementSyntax? localDecl = node.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();
            if (localDecl is null || localDecl.UsingKeyword != default)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Add 'using' to ensure disposal",
                    createChangedDocument: ct => AddUsingKeywordAsync(context.Document, localDecl, ct),
                    equivalenceKey: "AddUsingKeyword"),
                diagnostic);
        }
    }

    private static async Task<Document> AddUsingKeywordAsync(
        Document document,
        LocalDeclarationStatementSyntax localDecl,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // The leading trivia (indentation/newline) lives on the first token of the declaration
        // (the type keyword or 'var'). We need to move it to the 'using' keyword so the output
        // reads "    using var ..." instead of "using\n    var ...".
        SyntaxToken firstToken = localDecl.Declaration.GetFirstToken();
        SyntaxTriviaList leadingTrivia = firstToken.LeadingTrivia;

        SyntaxToken usingKeyword = SyntaxFactory.Token(SyntaxKind.UsingKeyword)
            .WithLeadingTrivia(leadingTrivia)
            .WithTrailingTrivia(SyntaxFactory.Space);

        // Strip the leading trivia from the declaration since it's now on the using keyword.
        VariableDeclarationSyntax newDeclaration = localDecl.Declaration
            .ReplaceToken(firstToken, firstToken.WithLeadingTrivia(SyntaxTriviaList.Empty));

        LocalDeclarationStatementSyntax newLocalDecl = localDecl
            .WithUsingKeyword(usingKeyword)
            .WithDeclaration(newDeclaration);

        SyntaxNode newRoot = root.ReplaceNode(localDecl, newLocalDecl);
        return document.WithSyntaxRoot(newRoot);
    }
}