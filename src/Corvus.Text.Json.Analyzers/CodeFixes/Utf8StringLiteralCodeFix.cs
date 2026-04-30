// <copyright file="Utf8StringLiteralCodeFix.cs" company="Endjin Limited">
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
/// Code fix for CTJ001: Replaces a <c>string</c> literal with a UTF-8 string literal
/// (<c>"..."u8</c>) when a <c>ReadOnlySpan&lt;byte&gt;</c> overload is available.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Utf8StringLiteralCodeFix))]
[Shared]
public sealed class Utf8StringLiteralCodeFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        [DiagnosticDescriptors.PreferUtf8StringLiteral.Id];

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
            SyntaxToken token = root.FindToken(diagnostic.Location.SourceSpan.Start);
            if (token.Parent is not LiteralExpressionSyntax literal ||
                !literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Use UTF-8 string literal {literal.Token.Text}u8",
                    createChangedDocument: ct => ConvertToUtf8Async(context.Document, literal, ct),
                    equivalenceKey: DiagnosticDescriptors.PreferUtf8StringLiteral.Id),
                diagnostic);
        }
    }

    private static async Task<Document> ConvertToUtf8Async(
        Document document,
        LiteralExpressionSyntax literal,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Build the UTF-8 string literal: "text"u8
        // Use ParseExpression for reliable UTF-8 literal construction.
        string utf8Text = literal.Token.Text + "u8";
        ExpressionSyntax utf8Literal = SyntaxFactory.ParseExpression(utf8Text)
            .WithLeadingTrivia(literal.GetLeadingTrivia())
            .WithTrailingTrivia(literal.GetTrailingTrivia());

        return document.WithSyntaxRoot(root.ReplaceNode(literal, utf8Literal));
    }
}