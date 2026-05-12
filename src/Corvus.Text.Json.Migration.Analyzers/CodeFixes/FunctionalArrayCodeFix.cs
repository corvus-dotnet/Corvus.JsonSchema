// <copyright file="FunctionalArrayCodeFix.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
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
/// Code fix for CVJ012: renames V4 functional array methods
/// (<c>Add</c>, <c>AddRange</c>, <c>Insert</c>, <c>InsertRange</c>,
/// <c>SetItem</c>, <c>RemoveAt</c>, <c>Remove</c>, <c>RemoveRange</c>,
/// <c>Replace</c>) to their V5 mutable builder equivalents and drops the
/// assignment since V5 mutates in-place.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FunctionalArrayCodeFix))]
[Shared]
public sealed class FunctionalArrayCodeFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.FunctionalArrayMigration.Id);

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
            if (node is not IdentifierNameSyntax identifierName)
            {
                continue;
            }

            // The V5 name is in the second message argument.
            string v5Name = diagnostic.Properties.TryGetValue("v5Name", out string? name)
                ? name
                : GetV5Name(identifierName.Identifier.Text);

            InvocationExpressionSyntax? invocation = identifierName
                .FirstAncestorOrSelf<InvocationExpressionSyntax>();
            if (invocation is null)
            {
                continue;
            }

            // If the method name isn't changing and the call is already a
            // standalone expression statement, there's nothing to fix.
            bool nameChanges = v5Name != identifierName.Identifier.Text;
            bool isAlreadyExpressionStatement = invocation.Parent is ExpressionStatementSyntax;
            if (!nameChanges && isAlreadyExpressionStatement)
            {
                continue;
            }

            if (IsExpressionLambdaBody(invocation))
            {
                // Inside an expression lambda body — only rename, don't restructure.
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Rename '{identifierName.Identifier.Text}' to '{v5Name}'",
                        createChangedDocument: ct => RenameOnlyAsync(
                            context.Document, identifierName, v5Name, ct),
                        equivalenceKey: DiagnosticDescriptors.FunctionalArrayMigration.Id),
                    diagnostic);
            }
            else
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Rename '{identifierName.Identifier.Text}' to '{v5Name}' and drop assignment",
                        createChangedDocument: ct => RenameAndDropAssignmentAsync(
                            context.Document, invocation, identifierName, v5Name, ct),
                        equivalenceKey: DiagnosticDescriptors.FunctionalArrayMigration.Id),
                    diagnostic);
            }
        }
    }

    private static string GetV5Name(string v4Name) => v4Name switch
    {
        "Add" => "AddItem",
        "Insert" => "InsertItem",
        _ => v4Name,
    };

    /// <summary>
    /// Checks whether <paramref name="invocation"/> sits inside an expression
    /// lambda body (not a block lambda). Restructuring the enclosing statement
    /// would destroy the lambda, so only a rename fix is safe.
    /// </summary>
    private static bool IsExpressionLambdaBody(InvocationExpressionSyntax invocation)
    {
        SyntaxNode? current = invocation.Parent;

        while (current is not null)
        {
            if (current is BlockSyntax or StatementSyntax)
            {
                return false;
            }

            if (current is LambdaExpressionSyntax)
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    /// <summary>
    /// Simple rename-only fix: just rename the method identifier without
    /// restructuring the containing statement. Used for expression lambdas
    /// where restructuring would destroy the lambda.
    /// </summary>
    private static async Task<Document> RenameOnlyAsync(
        Document document,
        IdentifierNameSyntax identifier,
        string newName,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        IdentifierNameSyntax newIdentifier = identifier.WithIdentifier(
            SyntaxFactory.Identifier(newName)
                .WithTriviaFrom(identifier.Identifier));

        return document.WithSyntaxRoot(root.ReplaceNode(identifier, newIdentifier));
    }

    private static bool IsFunctionalArrayMethod(string name) =>
        name is "Add" or "AddRange" or "Insert" or "InsertRange"
            or "SetItem" or "RemoveAt" or "Remove" or "RemoveRange" or "Replace";

    /// <summary>
    /// Recursively collects a chain of functional array calls (innermost first).
    /// Returns the root receiver expression (e.g., <c>arr</c> in <c>arr.Add(a).Add(b)</c>).
    /// </summary>
    private static ExpressionSyntax? CollectFunctionalChain(
        InvocationExpressionSyntax invocation,
        List<(string V5Name, ArgumentListSyntax Args)> chain)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return null;
        }

        string methodName = memberAccess.Name.Identifier.Text;
        if (!IsFunctionalArrayMethod(methodName))
        {
            return null;
        }

        string v5Name = GetV5Name(methodName);
        ExpressionSyntax? receiver;

        if (memberAccess.Expression is InvocationExpressionSyntax innerInvocation &&
            innerInvocation.Expression is MemberAccessExpressionSyntax innerAccess &&
            IsFunctionalArrayMethod(innerAccess.Name.Identifier.Text))
        {
            receiver = CollectFunctionalChain(innerInvocation, chain);
        }
        else
        {
            receiver = memberAccess.Expression;
        }

        chain.Add((v5Name, invocation.ArgumentList));
        return receiver;
    }

    private static async Task<Document> RenameAndDropAssignmentAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        IdentifierNameSyntax identifier,
        string newName,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Walk up to find the outermost chained functional array call.
        InvocationExpressionSyntax outermost = invocation;
        while (outermost.Parent is MemberAccessExpressionSyntax ma &&
               ma.Parent is InvocationExpressionSyntax outerInvocation &&
               outerInvocation.Expression is MemberAccessExpressionSyntax outerAccess &&
               IsFunctionalArrayMethod(outerAccess.Name.Identifier.Text))
        {
            outermost = outerInvocation;
        }

        // Collect the full chain of functional array calls.
        var chain = new List<(string V5Name, ArgumentListSyntax Args)>();
        ExpressionSyntax? rootReceiver = CollectFunctionalChain(outermost, chain);

        if (rootReceiver is null || chain.Count == 0)
        {
            // Fallback: simple rename without restructuring.
            IdentifierNameSyntax newIdentifier = identifier.WithIdentifier(
                SyntaxFactory.Identifier(newName).WithTriviaFrom(identifier.Identifier));
            return document.WithSyntaxRoot(root.ReplaceNode(identifier, newIdentifier));
        }

        StatementSyntax? containingStatement = outermost.FirstAncestorOrSelf<StatementSyntax>();
        if (containingStatement is null)
        {
            return document;
        }

        string receiverText = rootReceiver.WithoutTrivia().ToFullString();
        SyntaxTriviaList leadingTrivia = containingStatement.GetLeadingTrivia();
        bool isReturn = containingStatement is ReturnStatementSyntax;

        string? receiverVariableName = rootReceiver is IdentifierNameSyntax receiverId
            ? receiverId.Identifier.Text
            : null;

        // Build replacement statements: one per chain element.
        var replacementStatements = new List<StatementSyntax>();
        foreach ((string v5Name, ArgumentListSyntax args) in chain)
        {
            replacementStatements.Add(
                SyntaxFactory.ParseStatement(
                    $"{receiverText}.{v5Name}{args.WithoutTrivia().ToFullString()};\r\n")
                    .WithLeadingTrivia(leadingTrivia));
        }

        // For return statements, add 'return receiver;' after the mutations.
        if (isReturn && receiverVariableName is not null)
        {
            replacementStatements.Add(
                SyntaxFactory.ParseStatement($"return {receiverVariableName};\r\n")
                    .WithLeadingTrivia(leadingTrivia));
        }

        // Rebuild the block to replace the statement and rewrite the
        // receiver variable's type to .Mutable.
        var block = containingStatement.Parent as BlockSyntax;
        if (block is not null)
        {
            var newBlockStatements = new List<StatementSyntax>();
            foreach (StatementSyntax stmt in block.Statements)
            {
                if (ReferenceEquals(stmt, containingStatement))
                {
                    newBlockStatements.AddRange(replacementStatements);
                }
                else if (receiverVariableName is not null &&
                         stmt is LocalDeclarationStatementSyntax localDecl &&
                         TryRewriteToMutable(localDecl, receiverVariableName, out LocalDeclarationStatementSyntax rewritten))
                {
                    newBlockStatements.Add(rewritten);
                }
                else
                {
                    newBlockStatements.Add(stmt);
                }
            }

            BlockSyntax newBlock = block.WithStatements(SyntaxFactory.List(newBlockStatements));
            return document.WithSyntaxRoot(root.ReplaceNode(block, newBlock));
        }

        return document.WithSyntaxRoot(
            root.ReplaceNode(containingStatement, replacementStatements));
    }

    /// <summary>
    /// If <paramref name="localDecl"/> declares <paramref name="variableName"/>
    /// with an explicit (non-<c>var</c>) type, rewrites that type to
    /// <c>Type.Mutable</c> so that mutation methods can be called on the variable.
    /// </summary>
    private static bool TryRewriteToMutable(
        LocalDeclarationStatementSyntax localDecl,
        string variableName,
        out LocalDeclarationStatementSyntax rewritten)
    {
        rewritten = localDecl;

        foreach (VariableDeclaratorSyntax declarator in localDecl.Declaration.Variables)
        {
            if (declarator.Identifier.Text != variableName)
            {
                continue;
            }

            string typeText = localDecl.Declaration.Type.ToString();
            if (typeText == "var" || typeText == "var?" || typeText.EndsWith(".Mutable") || typeText.EndsWith(".Mutable?"))
            {
                return false;
            }

            // Handle nullable types: "Person?" → "Person.Mutable?" not "Person?.Mutable".
            bool isNullable = typeText.EndsWith("?");
            string baseType = isNullable ? typeText.Substring(0, typeText.Length - 1) : typeText;
            string mutableTypeText = baseType + ".Mutable" + (isNullable ? "?" : "");

            TypeSyntax mutableType = SyntaxFactory.ParseTypeName(mutableTypeText)
                .WithTriviaFrom(localDecl.Declaration.Type);
            rewritten = localDecl.WithDeclaration(
                localDecl.Declaration.WithType(mutableType));
            return true;
        }

        return false;
    }
}