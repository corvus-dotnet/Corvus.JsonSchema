// <copyright file="ParsedValueCodeFix.cs" company="Endjin Limited">
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
/// Code fix that replaces <c>ParsedValue&lt;T&gt;</c> with <c>ParsedJsonDocument&lt;T&gt;</c>
/// and <c>.Instance</c> with <c>.RootElement</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ParsedValueCodeFix))]
[Shared]
public sealed class ParsedValueCodeFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.ParsedValueMigration.Id);

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

            if (node is GenericNameSyntax genericName && genericName.Identifier.Text == "ParsedValue")
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Replace 'ParsedValue<T>' with 'ParsedJsonDocument<T>'",
                        createChangedDocument: ct => ReplaceAllParsedValueInStatementAsync(context.Document, genericName, ct),
                        equivalenceKey: DiagnosticDescriptors.ParsedValueMigration.Id),
                    diagnostic);
            }
            else if (node is IdentifierNameSyntax identifierName && identifierName.Identifier.Text == "Instance")
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Replace '.Instance' with '.RootElement'",
                        createChangedDocument: ct => ReplacePropertyNameAsync(context.Document, identifierName, ct),
                        equivalenceKey: DiagnosticDescriptors.ParsedValueMigration.Id),
                    diagnostic);
            }
        }
    }

    private static async Task<Document> ReplaceAllParsedValueInStatementAsync(
        Document document,
        GenericNameSyntax triggeringNode,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Walk up to the containing statement — covers using declarations,
        // using blocks, and plain local declarations.
        SyntaxNode? statement = triggeringNode.FirstAncestorOrSelf<StatementSyntax>();
        if (statement is null)
        {
            // Fallback: just replace the single node.
            return document.WithSyntaxRoot(
                root.ReplaceNode(triggeringNode, RenameParsedValue(triggeringNode)));
        }

        // Find every ParsedValue<T> generic name within the statement.
        GenericNameSyntax[] parsedValueNodes = statement
            .DescendantNodesAndSelf()
            .OfType<GenericNameSyntax>()
            .Where(g => g.Identifier.Text == "ParsedValue")
            .ToArray();

        SyntaxNode newRoot = root.ReplaceNodes(
            parsedValueNodes,
            (original, _) => RenameParsedValue(original));

        return document.WithSyntaxRoot(newRoot);
    }

    private static GenericNameSyntax RenameParsedValue(GenericNameSyntax genericName)
    {
        return genericName.WithIdentifier(
            SyntaxFactory.Identifier("ParsedJsonDocument")
                .WithTriviaFrom(genericName.Identifier));
    }

    private static async Task<Document> ReplacePropertyNameAsync(
        Document document,
        IdentifierNameSyntax identifierName,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        IdentifierNameSyntax newName = identifierName.WithIdentifier(
            SyntaxFactory.Identifier("RootElement")
                .WithTriviaFrom(identifierName.Identifier));

        SyntaxNode newRoot = root.ReplaceNode(identifierName, newName);
        return document.WithSyntaxRoot(newRoot);
    }
}