// <copyright file="ParserTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Jsonata.Ast;
using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

public class ParserTests
{
    [Fact]
    public void SimpleFieldAccess()
    {
        var ast = Parser.Parse("foo");
        var path = Assert.IsType<PathNode>(ast);
        Assert.Single(path.Steps);
        var name = Assert.IsType<NameNode>(path.Steps[0]);
        Assert.Equal("foo", name.Value);
    }

    [Fact]
    public void DottedPath()
    {
        var ast = Parser.Parse("Account.Order.Product");
        var path = Assert.IsType<PathNode>(ast);
        Assert.Equal(3, path.Steps.Count);
        Assert.Equal("Account", ((NameNode)path.Steps[0]).Value);
        Assert.Equal("Order", ((NameNode)path.Steps[1]).Value);
        Assert.Equal("Product", ((NameNode)path.Steps[2]).Value);
    }

    [Fact]
    public void NumericLiteral()
    {
        var ast = Parser.Parse("42");
        var num = Assert.IsType<NumberNode>(ast);
        Assert.Equal(42.0, num.Value);
    }

    [Fact]
    public void StringLiteral()
    {
        var ast = Parser.Parse("\"hello\"");
        var str = Assert.IsType<StringNode>(ast);
        Assert.Equal("hello", str.Value);
    }

    [Fact]
    public void BooleanLiteral()
    {
        var ast = Parser.Parse("true");
        var val = Assert.IsType<ValueNode>(ast);
        Assert.Equal("true", val.Value);
    }

    [Fact]
    public void NullLiteral()
    {
        var ast = Parser.Parse("null");
        var val = Assert.IsType<ValueNode>(ast);
        Assert.Equal("null", val.Value);
    }

    [Fact]
    public void VariableReference()
    {
        var ast = Parser.Parse("$x");
        var variable = Assert.IsType<VariableNode>(ast);
        Assert.Equal("x", variable.Name);
    }

    [Fact]
    public void ContextVariable()
    {
        var ast = Parser.Parse("$");
        var variable = Assert.IsType<VariableNode>(ast);
        Assert.Equal("", variable.Name);
    }

    [Fact]
    public void Addition()
    {
        var ast = Parser.Parse("1 + 2");
        var binary = Assert.IsType<BinaryNode>(ast);
        Assert.Equal("+", binary.Operator);
        Assert.Equal(1.0, ((NumberNode)binary.Lhs).Value);
        Assert.Equal(2.0, ((NumberNode)binary.Rhs).Value);
    }

    [Fact]
    public void OperatorPrecedence()
    {
        // 1 + 2 * 3 should parse as 1 + (2 * 3) due to precedence
        var ast = Parser.Parse("1 + 2 * 3");
        var add = Assert.IsType<BinaryNode>(ast);
        Assert.Equal("+", add.Operator);
        Assert.Equal(1.0, ((NumberNode)add.Lhs).Value);
        var mul = Assert.IsType<BinaryNode>(add.Rhs);
        Assert.Equal("*", mul.Operator);
        Assert.Equal(2.0, ((NumberNode)mul.Lhs).Value);
        Assert.Equal(3.0, ((NumberNode)mul.Rhs).Value);
    }

    [Fact]
    public void ComparisonOperator()
    {
        var ast = Parser.Parse("x > 5");
        var binary = Assert.IsType<BinaryNode>(ast);
        Assert.Equal(">", binary.Operator);
    }

    [Fact]
    public void BooleanOperators()
    {
        var ast = Parser.Parse("a and b or c");

        // 'and' (30) binds tighter than 'or' (25), so: (a and b) or c
        var orNode = Assert.IsType<BinaryNode>(ast);
        Assert.Equal("or", orNode.Operator);
        var andNode = Assert.IsType<BinaryNode>(orNode.Lhs);
        Assert.Equal("and", andNode.Operator);
    }

    [Fact]
    public void StringConcatenation()
    {
        var ast = Parser.Parse("\"hello\" & \" \" & \"world\"");
        var binary = Assert.IsType<BinaryNode>(ast);
        Assert.Equal("&", binary.Operator);
    }

    [Fact]
    public void UnaryMinus()
    {
        // Unary minus on a literal gets folded into the number
        var ast = Parser.Parse("-5");
        var num = Assert.IsType<NumberNode>(ast);
        Assert.Equal(-5.0, num.Value);
    }

    [Fact]
    public void UnaryMinusOnExpression()
    {
        var ast = Parser.Parse("-x");
        var unary = Assert.IsType<UnaryNode>(ast);
        Assert.Equal("-", unary.Operator);
    }

    [Fact]
    public void Wildcard()
    {
        var ast = Parser.Parse("Account.*");
        var path = Assert.IsType<PathNode>(ast);
        Assert.Equal(2, path.Steps.Count);
        Assert.IsType<NameNode>(path.Steps[0]);
        Assert.IsType<WildcardNode>(path.Steps[1]);
    }

    [Fact]
    public void DescendantWildcard()
    {
        var ast = Parser.Parse("**.Price");
        var path = Assert.IsType<PathNode>(ast);
        Assert.IsType<DescendantNode>(path.Steps[0]);
        Assert.IsType<NameNode>(path.Steps[1]);
    }

    [Fact]
    public void ArrayConstructor()
    {
        var ast = Parser.Parse("[1, 2, 3]");
        var arr = Assert.IsType<ArrayConstructorNode>(ast);
        Assert.Equal(3, arr.Expressions.Count);
    }

    [Fact]
    public void ObjectConstructor()
    {
        var ast = Parser.Parse("{\"name\": \"John\", \"age\": 30}");
        var obj = Assert.IsType<ObjectConstructorNode>(ast);
        Assert.Equal(2, obj.Pairs.Count);
    }

    [Fact]
    public void FunctionCallSimple()
    {
        var ast = Parser.Parse("$sum(prices)");
        var func = Assert.IsType<FunctionCallNode>(ast);
        Assert.IsType<VariableNode>(func.Procedure);
        Assert.Equal("sum", ((VariableNode)func.Procedure).Name);
        Assert.Single(func.Arguments);
        Assert.IsType<PathNode>(func.Arguments[0]);
    }

    [Fact]
    public void FunctionCallOnPath()
    {
        var ast = Parser.Parse("$count(Account.Order)");
        Assert.IsType<FunctionCallNode>(ast);
        var func = (FunctionCallNode)ast;
        Assert.IsType<VariableNode>(func.Procedure);
        Assert.Equal("count", ((VariableNode)func.Procedure).Name);
        Assert.Single(func.Arguments);
    }

    [Fact]
    public void LambdaDefinition()
    {
        var ast = Parser.Parse("function($x, $y){ $x + $y }");
        var lambda = Assert.IsType<LambdaNode>(ast);
        Assert.Equal(2, lambda.Parameters.Count);
        Assert.Equal("x", lambda.Parameters[0]);
        Assert.Equal("y", lambda.Parameters[1]);
        Assert.IsType<BinaryNode>(lambda.Body);
    }

    [Fact]
    public void TernaryCondition()
    {
        var ast = Parser.Parse("x > 0 ? \"positive\" : \"non-positive\"");
        var cond = Assert.IsType<ConditionNode>(ast);
        Assert.IsType<BinaryNode>(cond.Condition);
        Assert.IsType<StringNode>(cond.Then);
        Assert.IsType<StringNode>(cond.Else);
    }

    [Fact]
    public void TernaryWithoutElse()
    {
        var ast = Parser.Parse("x > 0 ? \"positive\"");
        var cond = Assert.IsType<ConditionNode>(ast);
        Assert.NotNull(cond.Then);
        Assert.Null(cond.Else);
    }

    [Fact]
    public void VariableBinding()
    {
        var ast = Parser.Parse("$x := 42");
        var bind = Assert.IsType<BindNode>(ast);
        Assert.IsType<VariableNode>(bind.Lhs);
        Assert.IsType<NumberNode>(bind.Rhs);
    }

    [Fact]
    public void BlockExpression()
    {
        var ast = Parser.Parse("($x := 1; $y := 2; $x + $y)");
        var block = Assert.IsType<BlockNode>(ast);
        Assert.Equal(3, block.Expressions.Count);
    }

    [Fact]
    public void ChainOperator()
    {
        var ast = Parser.Parse("Account.Order ~> $sum");
        var apply = Assert.IsType<ApplyNode>(ast);
        Assert.IsType<PathNode>(apply.Lhs);
        Assert.IsType<VariableNode>(apply.Rhs);
    }

    [Fact]
    public void SortExpression()
    {
        var ast = Parser.Parse("Account.Order^(>Price)");
        var path = Assert.IsType<PathNode>(ast);
        Assert.True(path.Steps.Count >= 2);
        Assert.IsType<SortNode>(path.Steps[path.Steps.Count - 1]);
        var sort = (SortNode)path.Steps[path.Steps.Count - 1];
        Assert.Single(sort.Terms);
        Assert.True(sort.Terms[0].Descending);
    }

    [Fact]
    public void TransformExpression()
    {
        var ast = Parser.Parse("|Account|{\"name\": \"new\"}|");
        var transform = Assert.IsType<TransformNode>(ast);
        Assert.NotNull(transform.Pattern);
        Assert.NotNull(transform.Update);
        Assert.Null(transform.Delete);
    }

    [Fact]
    public void TransformWithDelete()
    {
        var ast = Parser.Parse("|Account|{\"name\": \"new\"}, [\"old\"]|");
        var transform = Assert.IsType<TransformNode>(ast);
        Assert.NotNull(transform.Delete);
    }

    [Fact]
    public void RangeOperator()
    {
        var ast = Parser.Parse("[1..5]");
        var arr = Assert.IsType<ArrayConstructorNode>(ast);
        Assert.Single(arr.Expressions);
        var range = Assert.IsType<BinaryNode>(arr.Expressions[0]);
        Assert.Equal("..", range.Operator);
    }

    [Fact]
    public void PartialApplication()
    {
        var ast = Parser.Parse("$add(?, 5)");
        var partial = Assert.IsType<PartialNode>(ast);
        Assert.Equal(2, partial.Arguments.Count);
        Assert.IsType<PlaceholderNode>(partial.Arguments[0]);
    }

    [Fact]
    public void InOperator()
    {
        var ast = Parser.Parse("\"a\" in [\"a\", \"b\"]");
        var binary = Assert.IsType<BinaryNode>(ast);
        Assert.Equal("in", binary.Operator);
    }

    [Fact]
    public void KeepArrayModifier()
    {
        var ast = Parser.Parse("Account.Order.Product[]");
        var path = Assert.IsType<PathNode>(ast);
        Assert.True(path.KeepSingletonArray);
    }

    [Fact]
    public void NestedFunctionCalls()
    {
        var ast = Parser.Parse("$sum($map(Account.Order.Product.Price, $v))");
        var func = Assert.IsType<FunctionCallNode>(ast);
        Assert.Equal("sum", ((VariableNode)func.Procedure).Name);
    }

    [Fact]
    public void UnexpectedTokenThrows()
    {
        var ex = Assert.Throws<JsonataException>(() => Parser.Parse("1 + + 2"));
        Assert.NotNull(ex);
    }

    [Fact]
    public void EmptyExpression()
    {
        var ex = Assert.Throws<JsonataException>(() => Parser.Parse(""));
        Assert.NotNull(ex);
    }

    [Fact]
    public void NonVariableOnLeftOfBind()
    {
        var ex = Assert.Throws<JsonataException>(() => Parser.Parse("foo := 42"));
        Assert.Equal("S0212", ex.Code);
    }

    [Fact]
    public void RegexLiteral()
    {
        var ast = Parser.Parse("$match(str, /[a-z]+/i)");
        var func = Assert.IsType<FunctionCallNode>(ast);
        Assert.Equal(2, func.Arguments.Count);
        var regex = Assert.IsType<RegexNode>(func.Arguments[1]);
        Assert.Equal("[a-z]+", regex.Pattern);
        Assert.Equal("i", regex.Flags);
    }

    [Fact]
    public void CommentSkipping()
    {
        var ast = Parser.Parse("/* select price */ Account.Order.Price");
        var path = Assert.IsType<PathNode>(ast);
        Assert.Equal(3, path.Steps.Count);
    }

    [Fact]
    public void BacktickQuotedFieldInPath()
    {
        var ast = Parser.Parse("Account.`Order Date`.Year");
        var path = Assert.IsType<PathNode>(ast);
        Assert.Equal(3, path.Steps.Count);
        Assert.Equal("Order Date", ((NameNode)path.Steps[1]).Value);
    }

    [Fact]
    public void ElvisOperator()
    {
        var ast = Parser.Parse("x ?: \"default\"");
        var cond = Assert.IsType<ConditionNode>(ast);

        // Elvis desugars to: x ? x : "default"
        Assert.NotNull(cond.Then);
        Assert.NotNull(cond.Else);
    }

    [Fact]
    public void NullCoalesceOperator()
    {
        var ast = Parser.Parse("x ?? \"fallback\"");
        var cond = Assert.IsType<ConditionNode>(ast);

        // Desugars to: $exists(x) ? x : "fallback"
        Assert.IsType<FunctionCallNode>(cond.Condition);
    }
}