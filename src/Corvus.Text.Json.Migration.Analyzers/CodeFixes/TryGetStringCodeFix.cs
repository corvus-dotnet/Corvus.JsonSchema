// <copyright file="TryGetStringCodeFix.cs" company="Endjin Limited">
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
/// Code fix for CVJ018: renames <c>TryGetString</c> to <c>TryGetValue</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TryGetStringCodeFix))]
[Shared]
public sealed class TryGetStringCodeFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.TryGetStringMigration.Id);

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

            if (invocation?.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.ValueText == "TryGetString")
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Replace TryGetString with TryGetValue",
                        createChangedDocument: ct => RenameMethodAsync(
                            context.Document, invocation, memberAccess, ct),
                        equivalenceKey: DiagnosticDescriptors.TryGetStringMigration.Id),
                    diagnostic);
            }
        }
    }

    private static async Task<Document> RenameMethodAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        MemberAccessExpressionSyntax newMemberAccess = memberAccess
            .WithName(SyntaxFactory.IdentifierName("TryGetValue").WithTriviaFrom(memberAccess.Name));

        InvocationExpressionSyntax newInvocation = invocation
            .WithExpression(newMemberAccess);

        SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }
}