// <copyright file="MatchLambdaCaptureAnalyzer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Corvus.Text.Json.Analyzers;

/// <summary>
/// CTJ003: Detects non-static lambda arguments passed to <c>Match</c> and <c>MatchEvery</c> methods.
/// <list type="bullet">
/// <item><description>Non-capturing lambdas can simply be made <c>static</c>.</description></item>
/// <item><description>Capturing lambdas passed to <c>Match</c> can be converted to the
/// <c>Match&lt;TContext, TResult&gt;</c> overload to avoid closure allocation.</description></item>
/// <item><description>Capturing lambdas passed to <c>MatchEvery</c> can thread the captured state
/// through the accumulator instead.</description></item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MatchLambdaCaptureAnalyzer : DiagnosticAnalyzer
{
    internal const string CapturesKey = "Captures";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [DiagnosticDescriptors.MatchLambdaShouldBeStatic];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Check method name is "Match" or "MatchEvery".
        string? methodName = GetMethodName(invocation);
        if (methodName is not ("Match" or "MatchEvery"))
        {
            return;
        }

        // Verify this is actually a call to a Match/MatchEvery method (resolve the symbol).
        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        // Check that the method has the single-type-argument signature: Match<TOut> (not the
        // Match<TIn, TOut> overload, which already has context) or MatchEvery<TAccumulator>.
        if (method.Name is not ("Match" or "MatchEvery") || method.TypeArguments.Length != 1)
        {
            return;
        }

        // Check each argument for non-static lambdas.
        foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is not ParenthesizedLambdaExpressionSyntax lambda)
            {
                continue;
            }

            // Skip lambdas already marked static.
            if (lambda.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                continue;
            }

            // Analyze whether the lambda captures any variables.
            SyntaxNode bodyNode = lambda.Body;
            DataFlowAnalysis? dataFlow = bodyNode switch
            {
                BlockSyntax block => context.SemanticModel.AnalyzeDataFlow(block),
                StatementSyntax stmt => context.SemanticModel.AnalyzeDataFlow(stmt),
                ExpressionSyntax bodyExpr => context.SemanticModel.AnalyzeDataFlow(bodyExpr),
                _ => null,
            };
            bool captures = dataFlow?.CapturedInside.Length > 0;

            string message = (captures, method.Name) switch
            {
                (false, _) => "consider adding the 'static' modifier",
                (true, "MatchEvery") => "consider threading captured state through the accumulator to avoid closure allocation",
                (true, _) => "consider using Match<TContext, TResult> to avoid closure allocation",
            };

            ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
                .Add(CapturesKey, captures.ToString());

            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.MatchLambdaShouldBeStatic,
                    lambda.GetLocation(),
                    properties,
                    method.Name,
                    message));
        }
    }

    private static string? GetMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.Text,
            _ => null,
        };
    }
}