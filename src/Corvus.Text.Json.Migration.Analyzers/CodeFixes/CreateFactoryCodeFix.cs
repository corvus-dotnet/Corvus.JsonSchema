// <copyright file="CreateFactoryCodeFix.cs" company="Endjin Limited">
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace Corvus.Text.Json.Migration.Analyzers;

/// <summary>
/// Code fix for CVJ013/CVJ014/CVJ015: rewrites V4 static factory methods
/// (<c>Create</c>, <c>FromItems</c>, <c>FromValues</c>).
/// <para>
/// <strong>Top-level</strong> (result assigned to a variable or used as a
/// standalone statement): rewrites to <c>CreateBuilder(workspace, ...)</c>,
/// inserting a <c>using var workspace = JsonWorkspace.Create();</c> if one
/// does not already exist in scope.
/// </para>
/// <para>
/// <strong>Nested</strong> (result used as an argument to another method
/// call, e.g. a setter or another Create): rewrites to <c>Build(...)</c>,
/// which produces a <see cref="T:Corvus.Text.Json.JsonElement.Source"/>
/// without requiring a workspace.
/// </para>
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CreateFactoryCodeFix))]
[Shared]
public sealed class CreateFactoryCodeFix : CodeFixProvider
{
    private const string WorkspaceVarName = "workspace";
    private const string WorkspaceTypeName = "JsonWorkspace";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(
            DiagnosticDescriptors.CreateMigration.Id,
            DiagnosticDescriptors.FromItemsMigration.Id,
            DiagnosticDescriptors.FromValuesMigration.Id);

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

            InvocationExpressionSyntax? invocation = identifierName
                .FirstAncestorOrSelf<InvocationExpressionSyntax>();
            if (invocation is null)
            {
                continue;
            }

            bool isSource = IsUsedAsSource(invocation);

            // Inside an expression lambda body — restructuring would destroy
            // the lambda. Use the nested/rename path which is expression-safe.
            if (IsExpressionLambdaBody(invocation))
            {
                isSource = true;
            }

            string title = isSource
                ? $"Rewrite {identifierName.Identifier.Text}() to Build(...)"
                : $"Rewrite {identifierName.Identifier.Text}() to CreateBuilder(workspace, ...)";

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedDocument: ct => isSource
                        ? RewriteNestedAsync(document: context.Document, invocation, identifierName, diagnostic.Id, ct)
                        : RewriteTopLevelAsync(context.Document, invocation, identifierName, diagnostic.Id, ct),
                    equivalenceKey: "CreateFactoryCodeFix_" + diagnostic.Id),
                diagnostic);
        }
    }

    /// <summary>
    /// Checks whether <paramref name="invocation"/> sits inside an expression
    /// lambda body (not a block lambda). Restructuring the enclosing statement
    /// would destroy the lambda.
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
    /// Determines whether the invocation result is used as input to another
    /// construction — either directly as an argument, or assigned to a
    /// variable that is subsequently passed as an argument but never used
    /// as an instance (no member access). In either case, the value is a
    /// <c>Source</c> produced by <c>Build()</c>, not a top-level builder.
    /// </summary>
    private static bool IsUsedAsSource(InvocationExpressionSyntax invocation)
    {
        // Case 1: direct argument — Create() inside an argument list.
        SyntaxNode? current = invocation.Parent;
        while (current is not null && current is not StatementSyntax)
        {
            if (current is ArgumentSyntax)
            {
                return true;
            }

            current = current.Parent;
        }

        // Case 2: assigned to a local variable — check how that variable
        // is subsequently used in the same block.
        StatementSyntax? stmt = invocation.FirstAncestorOrSelf<StatementSyntax>();
        if (stmt is LocalDeclarationStatementSyntax localDecl &&
            stmt.Parent is BlockSyntax block)
        {
            foreach (VariableDeclaratorSyntax declarator in localDecl.Declaration.Variables)
            {
                if (declarator.Initializer?.Value is not null &&
                    declarator.Initializer.Value.Span.IntersectsWith(invocation.Span))
                {
                    string varName = declarator.Identifier.Text;

                    bool usedAsInstance = IsVariableUsedAsInstance(block, stmt, varName);
                    bool usedAsArgument = IsVariableUsedAsArgument(block, stmt, varName);

                    // If the variable has member access (used as an instance),
                    // it's a real builder — not a source.
                    if (usedAsInstance)
                    {
                        return false;
                    }

                    // If only used as an argument and never as an instance,
                    // it's a source.
                    if (usedAsArgument)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether a local variable has member access in subsequent
    /// statements (i.e., methods or properties are called on it).
    /// </summary>
    private static bool IsVariableUsedAsInstance(
        BlockSyntax block,
        StatementSyntax afterStatement,
        string variableName)
    {
        bool pastTarget = false;
        foreach (StatementSyntax stmt in block.Statements)
        {
            if (ReferenceEquals(stmt, afterStatement))
            {
                pastTarget = true;
                continue;
            }

            if (!pastTarget)
            {
                continue;
            }

            foreach (SyntaxNode node in stmt.DescendantNodes())
            {
                if (node is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Expression is IdentifierNameSyntax id &&
                    id.Identifier.Text == variableName)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether a local variable is used as an argument to any method
    /// call in subsequent statements within the same block.
    /// </summary>
    private static bool IsVariableUsedAsArgument(
        BlockSyntax block,
        StatementSyntax afterStatement,
        string variableName)
    {
        bool pastTarget = false;
        foreach (StatementSyntax stmt in block.Statements)
        {
            if (ReferenceEquals(stmt, afterStatement))
            {
                pastTarget = true;
                continue;
            }

            if (!pastTarget)
            {
                continue;
            }

            foreach (SyntaxNode node in stmt.DescendantNodes())
            {
                if (node is ArgumentSyntax arg &&
                    arg.Expression is IdentifierNameSyntax id &&
                    id.Identifier.Text == variableName)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Nested case: rewrites to <c>Type.Build(...)</c> and, when the result
    /// is assigned to an explicitly typed local variable, changes the type
    /// to <c>Type.Source</c> (since <c>Build()</c> returns a Source, not
    /// the entity type itself).
    /// </summary>
    private static async Task<Document> RewriteNestedAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        IdentifierNameSyntax methodIdentifier,
        string diagnosticId,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // For nested usage, all three become Build(...) with the original args.
        IdentifierNameSyntax newMethodName = methodIdentifier.WithIdentifier(
            SyntaxFactory.Identifier("Build")
                .WithTriviaFrom(methodIdentifier.Identifier));

        SyntaxNode newRoot = root.ReplaceNode(methodIdentifier, newMethodName);

        // If the Build() result is assigned to an explicitly typed local
        // variable (not var), rewrite the type to Type.Source.
        // We need to re-find the containing statement in the new tree.
        StatementSyntax? containingStmt = invocation.FirstAncestorOrSelf<StatementSyntax>();
        if (containingStmt is LocalDeclarationStatementSyntax localDecl)
        {
            // Find the same statement in the new tree by span position.
            LocalDeclarationStatementSyntax? newLocalDecl =
                newRoot.FindNode(localDecl.Span)
                    .FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();

            if (newLocalDecl is not null)
            {
                TypeSyntax declType = newLocalDecl.Declaration.Type;
                string typeText = declType.ToString();

                // Only rewrite explicit types (not var/var?).
                if (typeText != "var" && typeText != "var?")
                {
                    TypeSyntax sourceType = SyntaxFactory.ParseTypeName(typeText + ".Source")
                        .WithTriviaFrom(declType);
                    newRoot = newRoot.ReplaceNode(
                        newLocalDecl.Declaration.Type,
                        sourceType);
                }
            }
        }

        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    /// Top-level case: rewrites to <c>Type.CreateBuilder(workspace, ...)</c>,
    /// inserting a workspace declaration if needed.
    /// <para>
    /// Because <c>CreateBuilder</c> returns
    /// <c>JsonDocumentBuilder&lt;TType&gt;</c> (not <c>TType</c>),
    /// the code fix also:
    /// <list type="bullet">
    /// <item>Changes an explicit variable type to <c>var</c> so the
    /// inferred type is correct.</item>
    /// <item>Rewrites subsequent references to the variable with
    /// <c>.RootElement</c> (which yields the mutable element).</item>
    /// </list>
    /// The builder does not need <c>using</c> — it is disposed with the
    /// workspace.
    /// </para>
    /// </summary>
    private static async Task<Document> RewriteTopLevelAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        IdentifierNameSyntax methodIdentifier,
        string diagnosticId,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        StatementSyntax? containingStatement = invocation.FirstAncestorOrSelf<StatementSyntax>();
        if (containingStatement is null)
        {
            return document;
        }

        var block = containingStatement.Parent as BlockSyntax;

        // Find an existing JsonWorkspace variable in scope.
        string? existingWorkspaceName = block is not null
            ? FindExistingWorkspace(block, containingStatement)
            : null;

        bool needsWorkspaceDeclaration = existingWorkspaceName is null;
        string workspaceName = existingWorkspaceName ?? WorkspaceVarName;

        // Build the new argument list for CreateBuilder.
        ArgumentListSyntax newArgs = BuildCreateBuilderArguments(
            invocation.ArgumentList, diagnosticId, workspaceName);

        // Replace method name with "CreateBuilder".
        IdentifierNameSyntax newMethodName = methodIdentifier.WithIdentifier(
            SyntaxFactory.Identifier("CreateBuilder")
                .WithTriviaFrom(methodIdentifier.Identifier));

        // Build the rewritten invocation.
        InvocationExpressionSyntax newInvocation = invocation
            .ReplaceNode(methodIdentifier, newMethodName)
            .WithArgumentList(newArgs);

        // Replace the invocation in the containing statement.
        StatementSyntax newStatement = containingStatement
            .ReplaceNode(invocation, newInvocation);

        // CreateBuilder returns JsonDocumentBuilder<T>, not T.
        // If the result is assigned to a local variable, change the
        // type to var (so the inferred type is correct) and rewrite
        // subsequent references to variable.RootElement.
        string? variableName = null;
        if (newStatement is LocalDeclarationStatementSyntax newLocalDecl)
        {
            TypeSyntax varType = SyntaxFactory.IdentifierName("var")
                .WithTriviaFrom(newLocalDecl.Declaration.Type);
            newStatement = newLocalDecl.WithDeclaration(
                newLocalDecl.Declaration.WithType(varType));

            if (newLocalDecl.Declaration.Variables.Count > 0)
            {
                variableName = newLocalDecl.Declaration.Variables[0].Identifier.Text;
            }
        }

        if (block is not null)
        {
            var newStatements = new List<StatementSyntax>();
            bool pastTarget = false;

            foreach (StatementSyntax stmt in block.Statements)
            {
                if (ReferenceEquals(stmt, containingStatement))
                {
                    if (needsWorkspaceDeclaration)
                    {
                        newStatements.Add(CreateWorkspaceDeclaration(
                            workspaceName, containingStatement.GetLeadingTrivia()));
                    }

                    newStatements.Add(newStatement);
                    pastTarget = true;
                }
                else if (pastTarget && variableName is not null)
                {
                    newStatements.Add(
                        RewriteVariableReferences(stmt, variableName));
                }
                else
                {
                    newStatements.Add(stmt);
                }
            }

            BlockSyntax newBlock = block.WithStatements(SyntaxFactory.List(newStatements));
            SyntaxNode newRoot = root.ReplaceNode(block, newBlock);

            return document.WithSyntaxRoot(
                newRoot.WithAdditionalAnnotations(
                    RenameAnnotation.Create()));
        }

        return document.WithSyntaxRoot(root.ReplaceNode(containingStatement, newStatement));
    }

    /// <summary>
    /// Replaces every reference to <paramref name="variableName"/> in the
    /// given statement with <c>variableName.RootElement</c>.
    /// </summary>
    private static StatementSyntax RewriteVariableReferences(
        StatementSyntax statement,
        string variableName)
    {
        var identifiers = statement
            .DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => id.Identifier.Text == variableName)
            .ToList();

        if (identifiers.Count == 0)
        {
            return statement;
        }

        return statement.ReplaceNodes(
            identifiers,
            (original, rewritten) =>
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    rewritten,
                    SyntaxFactory.IdentifierName("RootElement")));
    }

    private static ArgumentListSyntax BuildCreateBuilderArguments(
        ArgumentListSyntax originalArgs,
        string diagnosticId,
        string workspaceName)
    {
        IdentifierNameSyntax workspaceArg = SyntaxFactory.IdentifierName(workspaceName);

        switch (diagnosticId)
        {
            case "CVJ014":
                // FromItems(arg1, arg2, ...) → CreateBuilder(workspace, Build(arg1, arg2, ...))
                InvocationExpressionSyntax buildCall = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.IdentifierName("Build"),
                    originalArgs.WithoutTrivia());
                return SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.Argument(workspaceArg),
                        SyntaxFactory.Argument(buildCall),
                    }));

            default:
                // Create(args...) → CreateBuilder(workspace, args...)
                // FromValues(span) → CreateBuilder(workspace, span)
                return SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(
                        new[] { SyntaxFactory.Argument(workspaceArg) }
                            .Concat(originalArgs.Arguments)));
        }
    }

    /// <summary>
    /// Searches for an existing local variable of type <c>JsonWorkspace</c>
    /// declared before the target statement.
    /// </summary>
    private static string? FindExistingWorkspace(
        BlockSyntax block,
        StatementSyntax beforeStatement)
    {
        foreach (StatementSyntax stmt in block.Statements)
        {
            if (ReferenceEquals(stmt, beforeStatement))
            {
                break;
            }

            if (stmt is LocalDeclarationStatementSyntax localDecl)
            {
                string typeName = localDecl.Declaration.Type.ToString();
                bool matchesType = typeName.Contains(WorkspaceTypeName);

                // Also check the initializer for `var ws = JsonWorkspace.Create()`
                if (!matchesType)
                {
                    foreach (VariableDeclaratorSyntax declarator in localDecl.Declaration.Variables)
                    {
                        if (declarator.Initializer?.Value.ToString().Contains(WorkspaceTypeName) == true)
                        {
                            matchesType = true;
                            break;
                        }
                    }
                }

                if (matchesType)
                {
                    foreach (VariableDeclaratorSyntax declarator in localDecl.Declaration.Variables)
                    {
                        return declarator.Identifier.Text;
                    }
                }
            }
        }

        return null;
    }

    private static StatementSyntax CreateWorkspaceDeclaration(
        string variableName,
        SyntaxTriviaList leadingTrivia)
    {
        return SyntaxFactory.ParseStatement(
            $"using var {variableName} = JsonWorkspace.Create();\r\n")
            .WithLeadingTrivia(leadingTrivia);
    }
}