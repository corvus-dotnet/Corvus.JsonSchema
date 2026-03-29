// <copyright file="WithMutationAnalyzer.cs" company="Endjin Limited">
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

namespace Corvus.Text.Json.Migration.Analyzers;

/// <summary>
/// CVJ011: Detects V4 With*() and SetProperty/RemoveProperty mutation methods
/// that should use Set*() via a mutable builder in V5.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WithMutationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.WithMutationMigration);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeInvocation,
            SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            string methodName = memberAccess.Name.Identifier.Text;

            if (!TryGetV5MethodName(methodName, out string? v5Name))
            {
                return;
            }

            if (memberAccess.Expression is { } receiverExpression)
            {
                ITypeSymbol? receiverType = context.SemanticModel.GetTypeInfo(receiverExpression, context.CancellationToken).Type;
                if (!V4TypeHelper.ImplementsIJsonValueOrUnresolved(receiverType, context.SemanticModel.Compilation))
                {
                    return;
                }
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.WithMutationMigration,
                    memberAccess.Name.GetLocation(),
                    methodName,
                    v5Name));
        }
    }

    /// <summary>
    /// Returns the V5 method name for a V4 immutable mutation method, or <c>false</c>
    /// if the method is not a recognized mutation pattern.
    /// </summary>
    private static bool TryGetV5MethodName(string methodName, out string? v5Name)
    {
        if (methodName.StartsWith("With") && methodName.Length > 4 && char.IsUpper(methodName[4]))
        {
            v5Name = "Set" + methodName.Substring(4);
            return true;
        }

        if (methodName is "SetProperty" or "RemoveProperty")
        {
            v5Name = methodName;
            return true;
        }

        v5Name = null;
        return false;
    }
}