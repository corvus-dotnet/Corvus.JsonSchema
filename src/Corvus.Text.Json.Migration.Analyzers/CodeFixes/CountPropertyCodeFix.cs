// <copyright file="CountPropertyCodeFix.cs" company="Endjin Limited">
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

namespace Corvus.Text.Json.Migration.Analyzers;

/// <summary>
/// Code fix for CVJ005: replaces <c>.Count</c> property access
/// with <c>.GetPropertyCount()</c> method call.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class CountPropertyCodeFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.CountMigration.Id);

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
            MemberAccessExpressionSyntax? memberAccess = node.FirstAncestorOrSelf<MemberAccessExpressionSyntax>();

            if (memberAccess is not null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Replace with GetPropertyCount()",
                        createChangedDocument: ct => ReplaceWithGetPropertyCountAsync(
                            context.Document, memberAccess, ct),
                        equivalenceKey: "CVJ005_GetPropertyCount"),
                    diagnostic);
            }
        }
    }

    private static async Task<Document> ReplaceWithGetPropertyCountAsync(
        Document document,
        MemberAccessExpressionSyntax memberAccess,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Replace .Count with .GetPropertyCount()
        MemberAccessExpressionSyntax newMemberAccess = memberAccess
            .WithName(SyntaxFactory.IdentifierName("GetPropertyCount"));

        InvocationExpressionSyntax invocation = SyntaxFactory.InvocationExpression(newMemberAccess)
            .WithTriviaFrom(memberAccess);

        SyntaxNode newRoot = root.ReplaceNode(memberAccess, invocation);
        return document.WithSyntaxRoot(newRoot);
    }
}