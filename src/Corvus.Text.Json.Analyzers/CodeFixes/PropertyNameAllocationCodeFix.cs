// <copyright file="PropertyNameAllocationCodeFix.cs" company="Endjin Limited">
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
/// Code fix for CTJ008: Replaces <c>property.Name == "literal"</c> with
/// <c>property.NameEquals("literal"u8)</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PropertyNameAllocationCodeFix))]
[Shared]
public sealed class PropertyNameAllocationCodeFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ["CTJ008"];

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

            // Handle binary expression: property.Name == "literal"
            if (node is BinaryExpressionSyntax binary)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Use NameEquals with UTF-8 literal",
                        createChangedDocument: ct => ReplaceBinaryWithNameEqualsAsync(context.Document, binary, ct),
                        equivalenceKey: "UseNameEquals"),
                    diagnostic);
            }

            // Handle invocation: property.Name.Equals("literal")
            if (node is InvocationExpressionSyntax invocation)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Use NameEquals with UTF-8 literal",
                        createChangedDocument: ct => ReplaceInvocationWithNameEqualsAsync(context.Document, invocation, ct),
                        equivalenceKey: "UseNameEquals"),
                    diagnostic);
            }
        }
    }

    private static async Task<Document> ReplaceBinaryWithNameEqualsAsync(
        Document document,
        BinaryExpressionSyntax binary,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Find which side is the property.Name and which is the literal.
        (MemberAccessExpressionSyntax? nameAccess, LiteralExpressionSyntax? literal) = FindNameAccessAndLiteral(binary);
        if (nameAccess is null || literal is null)
        {
            return document;
        }

        // Build: property.NameEquals("literal"u8)
        ExpressionSyntax nameEqualsCall = BuildNameEqualsCall(nameAccess.Expression, literal);

        // If the original was !=, negate: !property.NameEquals("literal"u8)
        ExpressionSyntax replacement = binary.IsKind(SyntaxKind.NotEqualsExpression)
            ? SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, nameEqualsCall)
            : nameEqualsCall;

        SyntaxNode newRoot = root.ReplaceNode(binary, replacement.WithTriviaFrom(binary));
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> ReplaceInvocationWithNameEqualsAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Pattern: property.Name.Equals("literal")
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Expression is MemberAccessExpressionSyntax nameAccess &&
            nameAccess.Name.Identifier.ValueText == "Name" &&
            invocation.ArgumentList.Arguments.Count == 1 &&
            invocation.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax literal)
        {
            ExpressionSyntax nameEqualsCall = BuildNameEqualsCall(nameAccess.Expression, literal);
            SyntaxNode newRoot = root.ReplaceNode(invocation, nameEqualsCall.WithTriviaFrom(invocation));
            return document.WithSyntaxRoot(newRoot);
        }

        return document;
    }

    private static ExpressionSyntax BuildNameEqualsCall(
        ExpressionSyntax receiver,
        LiteralExpressionSyntax stringLiteral)
    {
        // Create: "literal"u8
        SyntaxToken utf8Token = SyntaxFactory.Token(
            SyntaxTriviaList.Empty,
            SyntaxKind.Utf8StringLiteralToken,
            "\"" + stringLiteral.Token.ValueText + "\"u8",
            stringLiteral.Token.ValueText,
            SyntaxTriviaList.Empty);

        ExpressionSyntax utf8Literal = SyntaxFactory.LiteralExpression(
            SyntaxKind.Utf8StringLiteralExpression,
            utf8Token);

        // Build: receiver.NameEquals("literal"u8)
        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                receiver,
                SyntaxFactory.IdentifierName("NameEquals")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(utf8Literal))));
    }

    private static (MemberAccessExpressionSyntax? NameAccess, LiteralExpressionSyntax? Literal) FindNameAccessAndLiteral(
        BinaryExpressionSyntax binary)
    {
        if (binary.Left is MemberAccessExpressionSyntax leftAccess &&
            leftAccess.Name.Identifier.ValueText == "Name" &&
            binary.Right is LiteralExpressionSyntax rightLiteral)
        {
            return (leftAccess, rightLiteral);
        }

        if (binary.Right is MemberAccessExpressionSyntax rightAccess &&
            rightAccess.Name.Identifier.ValueText == "Name" &&
            binary.Left is LiteralExpressionSyntax leftLiteral)
        {
            return (rightAccess, leftLiteral);
        }

        return (null, null);
    }
}