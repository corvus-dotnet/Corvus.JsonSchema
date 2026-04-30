// <copyright file="NodeBuilderExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Jsonata.Ast;

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Fluent builder extensions for constructing AST nodes.
/// </summary>
internal static class NodeBuilderExtensions
{
    public static BlockNode WithExpressions(this BlockNode node, List<JsonataNode> expressions)
    {
        node.Expressions.AddRange(expressions);
        return node;
    }

    public static ArrayConstructorNode WithExpressions(this ArrayConstructorNode node, List<JsonataNode> expressions)
    {
        node.Expressions.AddRange(expressions);
        return node;
    }

    public static ObjectConstructorNode WithPairs(this ObjectConstructorNode node, List<(JsonataNode Key, JsonataNode Value)> pairs)
    {
        node.Pairs.AddRange(pairs);
        return node;
    }

    public static FunctionCallNode WithArguments(this FunctionCallNode node, List<JsonataNode> arguments)
    {
        node.Arguments.AddRange(arguments);
        return node;
    }

    public static PartialNode WithArguments(this PartialNode node, List<JsonataNode> arguments)
    {
        node.Arguments.AddRange(arguments);
        return node;
    }

    public static SortNode WithTerms(this SortNode node, List<SortTerm> terms)
    {
        node.Terms.AddRange(terms);
        return node;
    }

    public static LambdaNode WithParameters(this LambdaNode node, List<string> parameters)
    {
        node.Parameters.AddRange(parameters);
        return node;
    }
}