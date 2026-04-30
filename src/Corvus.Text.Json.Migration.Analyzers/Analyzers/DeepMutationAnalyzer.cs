// <copyright file="DeepMutationAnalyzer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Corvus.Text.Json.Migration.Analyzers;

/// <summary>
/// CVJ021: Detects nested V4 <c>With*()</c> reconstruction chains that can be replaced
/// with a single deep property setter on a V5 mutable builder.
/// <para>
/// Inline pattern: <c>parent.WithChild(parent.Child.WithProp(value))</c>.
/// </para>
/// <para>
/// Unrolled pattern:
/// <code>
/// var child = parent.Child;
/// var updatedChild = child.WithProp(value);
/// var updated = parent.WithChild(updatedChild);
/// </code>
/// </para>
/// Both simplify to <c>builder.RootElement.Child.SetProp(value)</c> in V5.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeepMutationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.DeepMutationMigration);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Analyse each method/block for unrolled patterns, and individual
        // invocations for inline patterns.
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // We only care about With*() calls.
        if (!TryGetMutationMethodInfo(invocation, out MemberAccessExpressionSyntax? outerMember, out string? outerMethodName))
        {
            return;
        }

        // Check receiver is an IJsonValue type.
        ITypeSymbol? receiverType = context.SemanticModel.GetTypeInfo(outerMember!.Expression, context.CancellationToken).Type;
        if (!V4TypeHelper.ImplementsIJsonValueOrUnresolved(receiverType, context.SemanticModel.Compilation))
        {
            return;
        }

        // Case 1: Inline — argument to With*() is itself a With*() invocation.
        // e.g. person.WithAddress(person.Address.WithCity("Manchester"))
        if (TryDetectInlineNested(invocation, outerMethodName!, out string? v4Pattern, out string? v5Suggestion))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DeepMutationMigration,
                invocation.GetLocation(),
                v4Pattern,
                v5Suggestion));
            return;
        }

        // Case 2: Unrolled — argument is a local variable assigned from a With*() call.
        if (TryDetectUnrolledNested(context, invocation, outerMember!, outerMethodName!, out v4Pattern, out v5Suggestion))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DeepMutationMigration,
                invocation.GetLocation(),
                v4Pattern,
                v5Suggestion));
        }
    }

    private static bool TryDetectInlineNested(
        InvocationExpressionSyntax outerInvocation,
        string outerMethodName,
        out string? v4Pattern,
        out string? v5Suggestion)
    {
        v4Pattern = null;
        v5Suggestion = null;

        // Check all arguments for a nested mutation call.
        foreach (ArgumentSyntax argument in outerInvocation.ArgumentList.Arguments)
        {
            ExpressionSyntax arg = argument.Expression;

            // Strip .AsAny or other member access wrapping the inner invocation.
            if (arg is MemberAccessExpressionSyntax wrappedAccess)
            {
                arg = wrappedAccess.Expression;
            }

            if (arg is InvocationExpressionSyntax innerInvocation &&
                TryGetMutationMethodInfo(innerInvocation, out _, out string? innerMethodName))
            {
                string outerPath = BuildV5PropertyPath(outerMethodName, outerInvocation);
                string innerSetter = BuildV5SetterCall(innerMethodName!, innerInvocation);

                v4Pattern = $".{outerMethodName}(...{innerMethodName}(...))";
                v5Suggestion = $"builder.RootElement{JoinPath(outerPath)}{JoinPath(innerSetter)}";
                return true;
            }
        }

        return false;
    }

    private static bool TryDetectUnrolledNested(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax outerInvocation,
        MemberAccessExpressionSyntax outerMember,
        string outerMethodName,
        out string? v4Pattern,
        out string? v5Suggestion)
    {
        v4Pattern = null;
        v5Suggestion = null;

        // Find the containing block for local variable tracking.
        BlockSyntax? block = outerInvocation.FirstAncestorOrSelf<BlockSyntax>();
        if (block is null)
        {
            return false;
        }

        // Check all arguments for a local variable assigned from a mutation call.
        foreach (ArgumentSyntax argument in outerInvocation.ArgumentList.Arguments)
        {
            ExpressionSyntax arg = argument.Expression;

            // Strip .AsAny or other member access wrapping the identifier.
            if (arg is MemberAccessExpressionSyntax wrappedAccess &&
                wrappedAccess.Expression is IdentifierNameSyntax wrappedId)
            {
                arg = wrappedId;
            }

            if (arg is not IdentifierNameSyntax argIdentifier)
            {
                continue;
            }

            string argName = argIdentifier.Identifier.Text;

            InvocationExpressionSyntax? innerMutationCall = FindMutationAssignment(block, argName);
            if (innerMutationCall is not null &&
                TryGetMutationMethodInfo(innerMutationCall, out _, out string? innerMethodName))
            {
                string outerPath = BuildV5PropertyPath(outerMethodName, outerInvocation);
                string innerSetter = BuildV5SetterCall(innerMethodName!, innerMutationCall);

                v4Pattern = $".{outerMethodName}({argName}) where {argName} = ...{innerMethodName}(...)";
                v5Suggestion = $"builder.RootElement{JoinPath(outerPath)}{JoinPath(innerSetter)}";
                return true;
            }
        }

        return false;
    }

    private static InvocationExpressionSyntax? FindMutationAssignment(
        BlockSyntax block,
        string variableName)
    {
        // Look for: var x = expr.With*(...); or Type x = expr.With*(...);
        foreach (StatementSyntax statement in block.Statements)
        {
            if (statement is LocalDeclarationStatementSyntax localDecl)
            {
                foreach (VariableDeclaratorSyntax declarator in localDecl.Declaration.Variables)
                {
                    if (declarator.Identifier.Text == variableName &&
                        declarator.Initializer?.Value is InvocationExpressionSyntax initInvocation &&
                        TryGetMutationMethodInfo(initInvocation, out _, out _))
                    {
                        return initInvocation;
                    }
                }
            }
            else if (statement is ExpressionStatementSyntax exprStmt &&
                     exprStmt.Expression is AssignmentExpressionSyntax assignment &&
                     assignment.Left is IdentifierNameSyntax assignTarget &&
                     assignTarget.Identifier.Text == variableName &&
                     assignment.Right is InvocationExpressionSyntax assignInvocation &&
                     TryGetMutationMethodInfo(assignInvocation, out _, out _))
            {
                return assignInvocation;
            }
        }

        return null;
    }

    private static bool TryGetMutationMethodInfo(
        InvocationExpressionSyntax invocation,
        out MemberAccessExpressionSyntax? memberAccess,
        out string? methodName)
    {
        memberAccess = null;
        methodName = null;

        if (invocation.Expression is not MemberAccessExpressionSyntax ma)
        {
            return false;
        }

        string name = ma.Name.Identifier.Text;

        // With*() — generated typed property mutators
        if (name.StartsWith("With", System.StringComparison.Ordinal) && name.Length > 4)
        {
            memberAccess = ma;
            methodName = name;
            return true;
        }

        // SetProperty / RemoveProperty — untyped object mutators
        // Add / AddRange / Insert / InsertRange / SetItem / RemoveAt /
        // Remove / RemoveRange / Replace — functional array mutators
        if (name is "SetProperty" or "RemoveProperty"
                 or "Add" or "AddRange" or "Insert" or "InsertRange"
                 or "SetItem" or "RemoveAt" or "Remove" or "RemoveRange"
                 or "Replace")
        {
            memberAccess = ma;
            methodName = name;
            return true;
        }

        return false;
    }

    private static string BuildV5PropertyPath(string methodName, InvocationExpressionSyntax invocation)
    {
        // WithAddress → Address (typed property name from the method name)
        if (methodName.StartsWith("With", System.StringComparison.Ordinal) && methodName.Length > 4)
        {
            return methodName.Substring(4);
        }

        // SetProperty("key", value) → ["key"] (extract the string key from arg 0)
        if (methodName == "SetProperty" && invocation.ArgumentList.Arguments.Count > 0)
        {
            string keyText = invocation.ArgumentList.Arguments[0].Expression.ToString();
            return $"[{keyText}]";
        }

        // SetItem(index, value) → [index] (extract the index from arg 0)
        if (methodName == "SetItem" && invocation.ArgumentList.Arguments.Count > 0)
        {
            string indexText = invocation.ArgumentList.Arguments[0].Expression.ToString();
            return $"[{indexText}]";
        }

        return methodName;
    }

    private static string BuildV5SetterCall(string methodName, InvocationExpressionSyntax invocation)
    {
        // WithCity → SetCity(...) (typed setter from the method name)
        if (methodName.StartsWith("With", System.StringComparison.Ordinal) && methodName.Length > 4)
        {
            return "Set" + methodName.Substring(4) + "(...)";
        }

        // SetProperty("key", value) → SetProperty("key", ...) (keep the string key)
        if (methodName == "SetProperty" && invocation.ArgumentList.Arguments.Count > 0)
        {
            string keyText = invocation.ArgumentList.Arguments[0].Expression.ToString();
            return $"SetProperty({keyText}, ...)";
        }

        // RemoveProperty("key") → RemoveProperty("key")
        if (methodName == "RemoveProperty" && invocation.ArgumentList.Arguments.Count > 0)
        {
            string keyText = invocation.ArgumentList.Arguments[0].Expression.ToString();
            return $"RemoveProperty({keyText})";
        }

        // Array mutators: Add → AddItem, Insert → InsertItem, others keep name
        if (methodName == "Add")
        {
            return "AddItem(...)";
        }

        if (methodName == "Insert")
        {
            return "InsertItem(...)";
        }

        if (methodName == "AddRange")
        {
            return "loop of AddItem(...)";
        }

        if (methodName == "InsertRange")
        {
            return "loop of InsertItem(...)";
        }

        if (methodName is "SetItem" or "RemoveAt" or "Remove" or "RemoveRange" or "Replace")
        {
            return methodName + "(...)";
        }

        return methodName + "(...)";
    }

    private static string JoinPath(string segment)
    {
        // Bracketed indexer paths like ["key"] don't need a leading dot.
        if (segment.Length > 0 && segment[0] == '[')
        {
            return segment;
        }

        return "." + segment;
    }
}