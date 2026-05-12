// <copyright file="MatchLambdaCaptureCodeFix.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Corvus.Text.Json.Analyzers;

/// <summary>
/// Code fix for CTJ003: Adds the <c>static</c> modifier to non-capturing lambdas
/// passed to <c>Match</c> methods.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MatchLambdaCaptureCodeFix))]
[Shared]
public sealed class MatchLambdaCaptureCodeFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        [DiagnosticDescriptors.MatchLambdaShouldBeStatic.Id];

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
            // Only offer the "make static" fix for non-capturing lambdas.
            if (diagnostic.Properties.TryGetValue(MatchLambdaCaptureAnalyzer.CapturesKey, out string? captures) &&
                captures == "True")
            {
                // Capturing lambda — the TContext conversion code fix is not yet implemented.
                continue;
            }

            SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            if (node is not ParenthesizedLambdaExpressionSyntax lambda)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Make lambda static",
                    createChangedDocument: ct => MakeStaticAsync(context.Document, lambda, ct),
                    equivalenceKey: DiagnosticDescriptors.MatchLambdaShouldBeStatic.Id + ".Static"),
                diagnostic);
        }
    }

    private static async Task<Document> MakeStaticAsync(
        Document document,
        ParenthesizedLambdaExpressionSyntax lambda,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Transfer leading trivia from the lambda's first token to the new static keyword.
        SyntaxToken firstToken = lambda.GetFirstToken();
        SyntaxTriviaList leadingTrivia = firstToken.LeadingTrivia;

        SyntaxToken staticKeyword = SyntaxFactory.Token(SyntaxKind.StaticKeyword)
            .WithLeadingTrivia(leadingTrivia)
            .WithTrailingTrivia(SyntaxFactory.Space);

        // Remove leading trivia from the original first token.
        ParenthesizedLambdaExpressionSyntax newLambda = lambda
            .ReplaceToken(firstToken, firstToken.WithLeadingTrivia(SyntaxTriviaList.Empty))
            .WithModifiers(new SyntaxTokenList(staticKeyword));

        return document.WithSyntaxRoot(root.ReplaceNode(lambda, newLambda));
    }
}