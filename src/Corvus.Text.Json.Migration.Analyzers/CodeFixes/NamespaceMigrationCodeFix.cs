// <copyright file="NamespaceMigrationCodeFix.cs" company="Endjin Limited">
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
/// Code fix that replaces <c>using Corvus.Json[.*];</c> with
/// <c>using Corvus.Text.Json[.*];</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NamespaceMigrationCodeFix))]
[Shared]
public sealed class NamespaceMigrationCodeFix : CodeFixProvider
{
    private const string OldPrefix = "Corvus.Json";
    private const string NewPrefix = "Corvus.Text.Json";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.NamespaceMigration.Id);

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
            if (node is not UsingDirectiveSyntax usingDirective || usingDirective.Name is null)
            {
                continue;
            }

            string oldName = usingDirective.Name.ToString();
            string newName = NewPrefix + oldName.Substring(OldPrefix.Length);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Replace with 'using {newName}'",
                    createChangedDocument: ct => ReplaceNamespaceAsync(context.Document, usingDirective, newName, ct),
                    equivalenceKey: DiagnosticDescriptors.NamespaceMigration.Id),
                diagnostic);
        }
    }

    private static async Task<Document> ReplaceNamespaceAsync(
        Document document,
        UsingDirectiveSyntax usingDirective,
        string newNamespace,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        NameSyntax newName = SyntaxFactory.ParseName(newNamespace)
            .WithTriviaFrom(usingDirective.Name!);

        UsingDirectiveSyntax newUsing = usingDirective.WithName(newName);
        SyntaxNode newRoot = root.ReplaceNode(usingDirective, newUsing);

        return document.WithSyntaxRoot(newRoot);
    }
}