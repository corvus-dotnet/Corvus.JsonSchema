// <copyright file="UnnecessaryConversionCodeFix.cs" company="Endjin Limited">
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
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Corvus.Text.Json.Analyzers;

/// <summary>
/// Code fix for CTJ002: Removes the unnecessary explicit cast, passing the original
/// expression directly to the parameter that accepts it via implicit conversion.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnnecessaryConversionCodeFix))]
[Shared]
public sealed class UnnecessaryConversionCodeFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        [DiagnosticDescriptors.UnnecessaryConversion.Id];

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
            SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            if (node is not CastExpressionSyntax castExpression)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Remove unnecessary conversion",
                    createChangedDocument: ct => RemoveCastAsync(context.Document, castExpression, ct),
                    equivalenceKey: DiagnosticDescriptors.UnnecessaryConversion.Id),
                diagnostic);
        }
    }

    private static async Task<Document> RemoveCastAsync(
        Document document,
        CastExpressionSyntax castExpression,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Replace the cast expression with just the inner expression,
        // preserving the original trivia.
        ExpressionSyntax innerExpression = castExpression.Expression
            .WithLeadingTrivia(castExpression.GetLeadingTrivia())
            .WithTrailingTrivia(castExpression.GetTrailingTrivia());

        return document.WithSyntaxRoot(root.ReplaceNode(castExpression, innerExpression));
    }
}