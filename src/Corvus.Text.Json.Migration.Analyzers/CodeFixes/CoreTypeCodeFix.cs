// <copyright file="CoreTypeCodeFix.cs" company="Endjin Limited">
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

namespace Corvus.Text.Json.Migration.Analyzers;

/// <summary>
/// Code fix for CVJ007: replaces <c>JsonAny</c> with <c>JsonElement</c> throughout
/// the containing statement so both declaration type and usage are updated together.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CoreTypeCodeFix))]
[Shared]
public sealed class CoreTypeCodeFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.CoreTypeMigration.Id);

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

            if (node is IdentifierNameSyntax identifierName && identifierName.Identifier.Text == "JsonAny")
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Replace 'JsonAny' with 'JsonElement'",
                        createChangedDocument: ct => ReplaceAllInStatementAsync(
                            context.Document, identifierName, ct),
                        equivalenceKey: DiagnosticDescriptors.CoreTypeMigration.Id),
                    diagnostic);
            }
        }
    }

    private static async Task<Document> ReplaceAllInStatementAsync(
        Document document,
        IdentifierNameSyntax triggeringNode,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Replace all JsonAny identifiers in the containing statement so
        // the preview shows the complete fix (e.g. both LHS type and RHS usage).
        SyntaxNode? statement = triggeringNode.FirstAncestorOrSelf<StatementSyntax>();
        if (statement is null)
        {
            return document.WithSyntaxRoot(
                root.ReplaceNode(triggeringNode, RenameIdentifier(triggeringNode)));
        }

        IdentifierNameSyntax[] jsonAnyNodes = statement
            .DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Where(id => id.Identifier.Text == "JsonAny")
            .ToArray();

        SyntaxNode newRoot = root.ReplaceNodes(
            jsonAnyNodes,
            (original, _) => RenameIdentifier(original));

        return document.WithSyntaxRoot(newRoot);
    }

    private static IdentifierNameSyntax RenameIdentifier(IdentifierNameSyntax node)
    {
        return node.WithIdentifier(
            SyntaxFactory.Identifier("JsonElement")
                .WithTriviaFrom(node.Identifier));
    }
}