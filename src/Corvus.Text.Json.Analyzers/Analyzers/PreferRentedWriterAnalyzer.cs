// <copyright file="PreferRentedWriterAnalyzer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Corvus.Text.Json.Analyzers;

/// <summary>
/// CTJ009: Detects <c>new Utf8JsonWriter(...)</c> when a <c>JsonWorkspace</c> is in scope,
/// suggesting the workspace's <c>RentWriter</c>/<c>RentWriterAndBuffer</c> methods instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferRentedWriterAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [DiagnosticDescriptors.PreferRentedWriter];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;

        // Check if we are creating a Utf8JsonWriter.
        ITypeSymbol? type = context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type;
        if (type is null ||
            type.Name != "Utf8JsonWriter" ||
            type.ContainingNamespace?.ToDisplayString() != "Corvus.Text.Json")
        {
            return;
        }

        // Walk up to find the containing method or lambda body.
        SyntaxNode? containingBody = FindContainingBody(creation);
        if (containingBody is null)
        {
            return;
        }

        // Check if any local variable or parameter in the containing scope has type JsonWorkspace.
        if (HasJsonWorkspaceInScope(creation, containingBody, context.SemanticModel))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.PreferRentedWriter,
                    creation.GetLocation()));
        }
    }

    private static SyntaxNode? FindContainingBody(SyntaxNode node)
    {
        SyntaxNode? current = node.Parent;
        while (current is not null)
        {
            if (current is MethodDeclarationSyntax method)
            {
                return (SyntaxNode?)method.Body ?? method.ExpressionBody;
            }

            if (current is LocalFunctionStatementSyntax localFunc)
            {
                return (SyntaxNode?)localFunc.Body ?? localFunc.ExpressionBody;
            }

            if (current is LambdaExpressionSyntax lambda)
            {
                return lambda.Body;
            }

            if (current is AccessorDeclarationSyntax accessor)
            {
                return (SyntaxNode?)accessor.Body ?? accessor.ExpressionBody;
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool HasJsonWorkspaceInScope(
        SyntaxNode creation,
        SyntaxNode containingBody,
        SemanticModel semanticModel)
    {
        // Check local variable declarations and parameters in the containing body.
        foreach (SyntaxNode descendant in containingBody.DescendantNodes())
        {
            // Local variable declarations: var workspace = JsonWorkspace.Create();
            if (descendant is VariableDeclaratorSyntax declarator &&
                declarator.Initializer is not null)
            {
                // Only consider declarations that appear before the creation expression.
                if (declarator.SpanStart >= creation.SpanStart)
                {
                    continue;
                }

                ITypeSymbol? varType = semanticModel.GetTypeInfo(declarator.Initializer.Value).Type;
                if (IsJsonWorkspace(varType))
                {
                    return true;
                }
            }
        }

        // Check parameters of the containing method.
        SyntaxNode? containingMember = containingBody.Parent;
        if (containingMember is MethodDeclarationSyntax method)
        {
            foreach (ParameterSyntax param in method.ParameterList.Parameters)
            {
                IParameterSymbol? paramSymbol = semanticModel.GetDeclaredSymbol(param);
                if (paramSymbol is not null && IsJsonWorkspace(paramSymbol.Type))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsJsonWorkspace(ITypeSymbol? type)
    {
        return type?.Name == "JsonWorkspace" &&
               type.ContainingNamespace?.ToDisplayString() == "Corvus.Text.Json";
    }
}