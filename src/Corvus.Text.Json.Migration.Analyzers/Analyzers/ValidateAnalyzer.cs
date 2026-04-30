// <copyright file="ValidateAnalyzer.cs" company="Endjin Limited">
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
/// CVJ003: Detects V4 <c>.IsValid()</c> and <c>.Validate(ValidationContext.ValidContext, ...)</c>
/// calls that should be replaced with <c>.EvaluateSchema()</c> in V5.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValidateAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.ValidateMigration);

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

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            string methodName = memberAccess.Name.Identifier.ValueText;

            if (methodName == "IsValid")
            {
                // .IsValid() with no arguments — equivalent to Validate(ValidContext, Flag)
                if (invocation.ArgumentList.Arguments.Count == 0 &&
                    IsJsonValueReceiverOrExtension(context, memberAccess, invocation))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ValidateMigration,
                        invocation.GetLocation(),
                        ImmutableDictionary<string, string?>.Empty,
                        "Replace '.IsValid()' with '.EvaluateSchema()'"));
                }
            }
            else if (methodName == "Validate")
            {
                ITypeSymbol? receiverType = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;
                if (!V4TypeHelper.ImplementsIJsonValueOrUnresolved(receiverType, context.SemanticModel.Compilation))
                {
                    return;
                }

                string message = GetValidateReplacementMessage(invocation, out string? resultsLevel);

                ImmutableDictionary<string, string?> properties = resultsLevel is not null
                    ? ImmutableDictionary<string, string?>.Empty.Add("ResultsLevel", resultsLevel)
                    : ImmutableDictionary<string, string?>.Empty;

                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ValidateMigration,
                    invocation.GetLocation(),
                    properties,
                    message));
            }
        }
    }

    private static string GetValidateReplacementMessage(InvocationExpressionSyntax invocation, out string? resultsLevel)
    {
        resultsLevel = null;
        int argCount = invocation.ArgumentList.Arguments.Count;

        // Validate(context) or Validate(context, ValidationLevel.Flag) → simple EvaluateSchema()
        if (argCount <= 1)
        {
            return "Replace '.Validate(ValidationContext.ValidContext)' with '.EvaluateSchema()'";
        }

        // Validate(context, level) — check the level argument
        ExpressionSyntax levelArg = invocation.ArgumentList.Arguments[1].Expression;
        string levelText = levelArg.ToString();

        if (levelText.EndsWith("Flag", System.StringComparison.Ordinal))
        {
            return "Replace '.Validate(ValidationContext.ValidContext, ValidationLevel.Flag)' with '.EvaluateSchema()'";
        }

        // For Basic/Detailed/Verbose, guide toward JsonSchemaResultsCollector
        resultsLevel = "Basic";
        if (levelText.EndsWith("Detailed", System.StringComparison.Ordinal))
        {
            resultsLevel = "Detailed";
        }
        else if (levelText.EndsWith("Verbose", System.StringComparison.Ordinal))
        {
            resultsLevel = "Verbose";
        }

        return $"Replace '.Validate(...)' with 'using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.{resultsLevel}); value.EvaluateSchema(collector);'";
    }

    private static bool IsJsonValueReceiverOrExtension(
        SyntaxNodeAnalysisContext context,
        MemberAccessExpressionSyntax memberAccess,
        InvocationExpressionSyntax invocation)
    {
        // Check if the receiver type implements IJsonValue
        ITypeSymbol? receiverType = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;
        if (V4TypeHelper.ImplementsIJsonValueOrUnresolved(receiverType, context.SemanticModel.Compilation))
        {
            return true;
        }

        // IsValid() may be an extension method (Corvus.Json.JsonValueExtensions.IsValid<T>)
        ISymbol? methodSymbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
        return methodSymbol is IMethodSymbol method &&
            method.IsExtensionMethod &&
            method.ContainingNamespace?.ToDisplayString() == "Corvus.Json";
    }
}