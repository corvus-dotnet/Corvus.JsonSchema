// <copyright file="ParserTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Jsonata.Ast;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Jsonata.Tests;

[TestClass]
public class ParserTests
{
    [TestMethod]
    public void SimpleFieldAccess()
    {
        var ast = Parser.Parse("foo");
        var path = Assert.IsInstanceOfType<PathNode>(ast);
        Assert.AreEqual(1, (path.Steps).Count());
        var name = Assert.IsInstanceOfType<NameNode>(path.Steps[0]);
        Assert.AreEqual("foo", name.Value);
    }

    [TestMethod]
    public void DottedPath()
    {
        var ast = Parser.Parse("Account.Order.Product");
        var path = Assert.IsInstanceOfType<PathNode>(ast);
        Assert.AreEqual(3, path.Steps.Count);
        Assert.AreEqual("Account", ((NameNode)path.Steps[0]).Value);
        Assert.AreEqual("Order", ((NameNode)path.Steps[1]).Value);
        Assert.AreEqual("Product", ((NameNode)path.Steps[2]).Value);
    }

    [TestMethod]
    public void NumericLiteral()
    {
        var ast = Parser.Parse("42");
        var num = Assert.IsInstanceOfType<NumberNode>(ast);
        Assert.AreEqual(42.0, num.Value);
    }

    [TestMethod]
    public void StringLiteral()
    {
        var ast = Parser.Parse("\"hello\"");
        var str = Assert.IsInstanceOfType<StringNode>(ast);
        Assert.AreEqual("hello", str.Value);
    }

    [TestMethod]
    public void BooleanLiteral()
    {
        var ast = Parser.Parse("true");
        var val = Assert.IsInstanceOfType<ValueNode>(ast);
        Assert.AreEqual("true", val.Value);
    }

    [TestMethod]
    public void NullLiteral()
    {
        var ast = Parser.Parse("null");
        var val = Assert.IsInstanceOfType<ValueNode>(ast);
        Assert.AreEqual("null", val.Value);
    }

    [TestMethod]
    public void VariableReference()
    {
        var ast = Parser.Parse("$x");
        var variable = Assert.IsInstanceOfType<VariableNode>(ast);
        Assert.AreEqual("x", variable.Name);
    }

    [TestMethod]
    public void ContextVariable()
    {
        var ast = Parser.Parse("$");
        var variable = Assert.IsInstanceOfType<VariableNode>(ast);
        Assert.AreEqual("", variable.Name);
    }

    [TestMethod]
    public void Addition()
    {
        var ast = Parser.Parse("1 + 2");
        var binary = Assert.IsInstanceOfType<BinaryNode>(ast);
        Assert.AreEqual("+", binary.Operator);
        Assert.AreEqual(1.0, ((NumberNode)binary.Lhs).Value);
        Assert.AreEqual(2.0, ((NumberNode)binary.Rhs).Value);
    }

    [TestMethod]
    public void OperatorPrecedence()
    {
        // 1 + 2 * 3 should parse as 1 + (2 * 3) due to precedence
        var ast = Parser.Parse("1 + 2 * 3");
        var add = Assert.IsInstanceOfType<BinaryNode>(ast);
        Assert.AreEqual("+", add.Operator);
        Assert.AreEqual(1.0, ((NumberNode)add.Lhs).Value);
        var mul = Assert.IsInstanceOfType<BinaryNode>(add.Rhs);
        Assert.AreEqual("*", mul.Operator);
        Assert.AreEqual(2.0, ((NumberNode)mul.Lhs).Value);
        Assert.AreEqual(3.0, ((NumberNode)mul.Rhs).Value);
    }

    [TestMethod]
    public void ComparisonOperator()
    {
        var ast = Parser.Parse("x > 5");
        var binary = Assert.IsInstanceOfType<BinaryNode>(ast);
        Assert.AreEqual(">", binary.Operator);
    }

    [TestMethod]
    public void BooleanOperators()
    {
        var ast = Parser.Parse("a and b or c");

        // 'and' (30) binds tighter than 'or' (25), so: (a and b) or c
        var orNode = Assert.IsInstanceOfType<BinaryNode>(ast);
        Assert.AreEqual("or", orNode.Operator);
        var andNode = Assert.IsInstanceOfType<BinaryNode>(orNode.Lhs);
        Assert.AreEqual("and", andNode.Operator);
    }

    [TestMethod]
    public void StringConcatenation()
    {
        var ast = Parser.Parse("\"hello\" & \" \" & \"world\"");
        var binary = Assert.IsInstanceOfType<BinaryNode>(ast);
        Assert.AreEqual("&", binary.Operator);
    }

    [TestMethod]
    public void UnaryMinus()
    {
        // Unary minus on a literal gets folded into the number
        var ast = Parser.Parse("-5");
        var num = Assert.IsInstanceOfType<NumberNode>(ast);
        Assert.AreEqual(-5.0, num.Value);
    }

    [TestMethod]
    public void UnaryMinusOnExpression()
    {
        var ast = Parser.Parse("-x");
        var unary = Assert.IsInstanceOfType<UnaryNode>(ast);
        Assert.AreEqual("-", unary.Operator);
    }

    [TestMethod]
    public void Wildcard()
    {
        var ast = Parser.Parse("Account.*");
        var path = Assert.IsInstanceOfType<PathNode>(ast);
        Assert.AreEqual(2, path.Steps.Count);
        Assert.IsInstanceOfType<NameNode>(path.Steps[0]);
        Assert.IsInstanceOfType<WildcardNode>(path.Steps[1]);
    }

    [TestMethod]
    public void DescendantWildcard()
    {
        var ast = Parser.Parse("**.Price");
        var path = Assert.IsInstanceOfType<PathNode>(ast);
        Assert.IsInstanceOfType<DescendantNode>(path.Steps[0]);
        Assert.IsInstanceOfType<NameNode>(path.Steps[1]);
    }

    [TestMethod]
    public void ArrayConstructor()
    {
        var ast = Parser.Parse("[1, 2, 3]");
        var arr = Assert.IsInstanceOfType<ArrayConstructorNode>(ast);
        Assert.AreEqual(3, arr.Expressions.Count);
    }

    [TestMethod]
    public void ObjectConstructor()
    {
        var ast = Parser.Parse("{\"name\": \"John\", \"age\": 30}");
        var obj = Assert.IsInstanceOfType<ObjectConstructorNode>(ast);
        Assert.AreEqual(2, obj.Pairs.Count);
    }

    [TestMethod]
    public void FunctionCallSimple()
    {
        var ast = Parser.Parse("$sum(prices)");
        var func = Assert.IsInstanceOfType<FunctionCallNode>(ast);
        Assert.IsInstanceOfType<VariableNode>(func.Procedure);
        Assert.AreEqual("sum", ((VariableNode)func.Procedure).Name);
        Assert.AreEqual(1, (func.Arguments).Count());
        Assert.IsInstanceOfType<PathNode>(func.Arguments[0]);
    }

    [TestMethod]
    public void FunctionCallOnPath()
    {
        var ast = Parser.Parse("$count(Account.Order)");
        Assert.IsInstanceOfType<FunctionCallNode>(ast);
        var func = (FunctionCallNode)ast;
        Assert.IsInstanceOfType<VariableNode>(func.Procedure);
        Assert.AreEqual("count", ((VariableNode)func.Procedure).Name);
        Assert.AreEqual(1, (func.Arguments).Count());
    }

    [TestMethod]
    public void LambdaDefinition()
    {
        var ast = Parser.Parse("function($x, $y){ $x + $y }");
        var lambda = Assert.IsInstanceOfType<LambdaNode>(ast);
        Assert.AreEqual(2, lambda.Parameters.Count);
        Assert.AreEqual("x", lambda.Parameters[0]);
        Assert.AreEqual("y", lambda.Parameters[1]);
        Assert.IsInstanceOfType<BinaryNode>(lambda.Body);
    }

    [TestMethod]
    public void TernaryCondition()
    {
        var ast = Parser.Parse("x > 0 ? \"positive\" : \"non-positive\"");
        var cond = Assert.IsInstanceOfType<ConditionNode>(ast);
        Assert.IsInstanceOfType<BinaryNode>(cond.Condition);
        Assert.IsInstanceOfType<StringNode>(cond.Then);
        Assert.IsInstanceOfType<StringNode>(cond.Else);
    }

    [TestMethod]
    public void TernaryWithoutElse()
    {
        var ast = Parser.Parse("x > 0 ? \"positive\"");
        var cond = Assert.IsInstanceOfType<ConditionNode>(ast);
        Assert.IsNotNull(cond.Then);
        Assert.IsNull(cond.Else);
    }

    [TestMethod]
    public void VariableBinding()
    {
        var ast = Parser.Parse("$x := 42");
        var bind = Assert.IsInstanceOfType<BindNode>(ast);
        Assert.IsInstanceOfType<VariableNode>(bind.Lhs);
        Assert.IsInstanceOfType<NumberNode>(bind.Rhs);
    }

    [TestMethod]
    public void BlockExpression()
    {
        var ast = Parser.Parse("($x := 1; $y := 2; $x + $y)");
        var block = Assert.IsInstanceOfType<BlockNode>(ast);
        Assert.AreEqual(3, block.Expressions.Count);
    }

    [TestMethod]
    public void ChainOperator()
    {
        var ast = Parser.Parse("Account.Order ~> $sum");
        var apply = Assert.IsInstanceOfType<ApplyNode>(ast);
        Assert.IsInstanceOfType<PathNode>(apply.Lhs);
        Assert.IsInstanceOfType<VariableNode>(apply.Rhs);
    }

    [TestMethod]
    public void SortExpression()
    {
        var ast = Parser.Parse("Account.Order^(>Price)");
        var path = Assert.IsInstanceOfType<PathNode>(ast);
        Assert.IsTrue(path.Steps.Count >= 2);
        Assert.IsInstanceOfType<SortNode>(path.Steps[path.Steps.Count - 1]);
        var sort = (SortNode)path.Steps[path.Steps.Count - 1];
        Assert.AreEqual(1, (sort.Terms).Count());
        Assert.IsTrue(sort.Terms[0].Descending);
    }

    [TestMethod]
    public void TransformExpression()
    {
        var ast = Parser.Parse("|Account|{\"name\": \"new\"}|");
        var transform = Assert.IsInstanceOfType<TransformNode>(ast);
        Assert.IsNotNull(transform.Pattern);
        Assert.IsNotNull(transform.Update);
        Assert.IsNull(transform.Delete);
    }

    [TestMethod]
    public void TransformWithDelete()
    {
        var ast = Parser.Parse("|Account|{\"name\": \"new\"}, [\"old\"]|");
        var transform = Assert.IsInstanceOfType<TransformNode>(ast);
        Assert.IsNotNull(transform.Delete);
    }

    [TestMethod]
    public void RangeOperator()
    {
        var ast = Parser.Parse("[1..5]");
        var arr = Assert.IsInstanceOfType<ArrayConstructorNode>(ast);
        Assert.AreEqual(1, (arr.Expressions).Count());
        var range = Assert.IsInstanceOfType<BinaryNode>(arr.Expressions[0]);
        Assert.AreEqual("..", range.Operator);
    }

    [TestMethod]
    public void PartialApplication()
    {
        var ast = Parser.Parse("$add(?, 5)");
        var partial = Assert.IsInstanceOfType<PartialNode>(ast);
        Assert.AreEqual(2, partial.Arguments.Count);
        Assert.IsInstanceOfType<PlaceholderNode>(partial.Arguments[0]);
    }

    [TestMethod]
    public void InOperator()
    {
        var ast = Parser.Parse("\"a\" in [\"a\", \"b\"]");
        var binary = Assert.IsInstanceOfType<BinaryNode>(ast);
        Assert.AreEqual("in", binary.Operator);
    }

    [TestMethod]
    public void KeepArrayModifier()
    {
        var ast = Parser.Parse("Account.Order.Product[]");
        var path = Assert.IsInstanceOfType<PathNode>(ast);
        Assert.IsTrue(path.KeepSingletonArray);
    }

    [TestMethod]
    public void NestedFunctionCalls()
    {
        var ast = Parser.Parse("$sum($map(Account.Order.Product.Price, $v))");
        var func = Assert.IsInstanceOfType<FunctionCallNode>(ast);
        Assert.AreEqual("sum", ((VariableNode)func.Procedure).Name);
    }

    [TestMethod]
    public void UnexpectedTokenThrows()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() => Parser.Parse("1 + + 2"));
        Assert.IsNotNull(ex);
    }

    [TestMethod]
    public void EmptyExpression()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() => Parser.Parse(""));
        Assert.IsNotNull(ex);
    }

    [TestMethod]
    public void NonVariableOnLeftOfBind()
    {
        var ex = Assert.ThrowsExactly<JsonataException>(() => Parser.Parse("foo := 42"));
        Assert.AreEqual("S0212", ex.Code);
    }

    [TestMethod]
    public void RegexLiteral()
    {
        var ast = Parser.Parse("$match(str, /[a-z]+/i)");
        var func = Assert.IsInstanceOfType<FunctionCallNode>(ast);
        Assert.AreEqual(2, func.Arguments.Count);
        var regex = Assert.IsInstanceOfType<RegexNode>(func.Arguments[1]);
        Assert.AreEqual("[a-z]+", regex.Pattern);
        Assert.AreEqual("i", regex.Flags);
    }

    [TestMethod]
    public void CommentSkipping()
    {
        var ast = Parser.Parse("/* select price */ Account.Order.Price");
        var path = Assert.IsInstanceOfType<PathNode>(ast);
        Assert.AreEqual(3, path.Steps.Count);
    }

    [TestMethod]
    public void BacktickQuotedFieldInPath()
    {
        var ast = Parser.Parse("Account.`Order Date`.Year");
        var path = Assert.IsInstanceOfType<PathNode>(ast);
        Assert.AreEqual(3, path.Steps.Count);
        Assert.AreEqual("Order Date", ((NameNode)path.Steps[1]).Value);
    }

    [TestMethod]
    public void ElvisOperator()
    {
        var ast = Parser.Parse("x ?: \"default\"");
        var cond = Assert.IsInstanceOfType<ConditionNode>(ast);

        // Elvis desugars to: x ? x : "default"
        Assert.IsNotNull(cond.Then);
        Assert.IsNotNull(cond.Else);
    }

    [TestMethod]
    public void NullCoalesceOperator()
    {
        var ast = Parser.Parse("x ?? \"fallback\"");
        var cond = Assert.IsInstanceOfType<ConditionNode>(ast);

        // Desugars to: $exists(x) ? x : "fallback"
        Assert.IsInstanceOfType<FunctionCallNode>(cond.Condition);
    }
}