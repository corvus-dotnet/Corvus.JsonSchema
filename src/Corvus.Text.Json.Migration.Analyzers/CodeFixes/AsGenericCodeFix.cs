// <copyright file="AsGenericCodeFix.cs" company="Endjin Limited">
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
/// Code fix for CVJ004: transforms <c>value.As&lt;TargetType&gt;()</c>
/// to <c>TargetType.From(value)</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class AsGenericCodeFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.AsGenericMigration.Id);

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
            if (node is not InvocationExpressionSyntax invocation)
            {
                invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
            }

            if (invocation?.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name is GenericNameSyntax genericName &&
                genericName.TypeArgumentList.Arguments.Count == 1)
            {
                string typeArgName = genericName.TypeArgumentList.Arguments[0].ToString();

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Replace with {typeArgName}.From()",
                        createChangedDocument: ct => ReplaceWithFromAsync(
                            context.Document, invocation, memberAccess, genericName, ct),
                        equivalenceKey: "CVJ004_From"),
                    diagnostic);
            }
        }
    }

    private static async Task<Document> ReplaceWithFromAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        GenericNameSyntax genericName,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // value.As<TargetType>() → TargetType.From(value)
        TypeSyntax typeArgument = genericName.TypeArgumentList.Arguments[0];
        ExpressionSyntax receiver = memberAccess.Expression;

        // Build: TargetType.From(value)
        MemberAccessExpressionSyntax fromAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            typeArgument.WithoutTrivia(),
            SyntaxFactory.IdentifierName("From"));

        InvocationExpressionSyntax newInvocation = SyntaxFactory.InvocationExpression(
            fromAccess,
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(receiver.WithoutTrivia()))))
            .WithTriviaFrom(invocation);

        SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }
}