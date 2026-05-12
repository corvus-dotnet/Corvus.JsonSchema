// <copyright file="FunctionalArrayAnalyzer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Corvus.Text.Json.Migration.Analyzers;

/// <summary>
/// CVJ012: Detects V4 functional array operations (Add, AddRange, Insert, InsertRange,
/// SetItem, RemoveAt, Remove, RemoveRange, Replace) that should use the mutable builder
/// equivalents in V5.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FunctionalArrayAnalyzer : DiagnosticAnalyzer
{
    private static readonly Dictionary<string, string> s_methodMapping = new()
    {
        ["Add"] = "AddItem",
        ["AddRange"] = "AddRange",
        ["Insert"] = "InsertItem",
        ["InsertRange"] = "InsertRange",
        ["SetItem"] = "SetItem",
        ["RemoveAt"] = "RemoveAt",
        ["Remove"] = "Remove",
        ["RemoveRange"] = "RemoveRange",
        ["Replace"] = "Replace",
    };

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.FunctionalArrayMigration);

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

            if (s_methodMapping.TryGetValue(methodName, out string? v5Name))
            {
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
                        DiagnosticDescriptors.FunctionalArrayMigration,
                        memberAccess.Name.GetLocation(),
                        methodName,
                        v5Name));
            }
        }
    }
}