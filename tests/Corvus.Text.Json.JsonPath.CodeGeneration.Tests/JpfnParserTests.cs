// <copyright file="JpfnParserTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonPath.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JsonPath.CodeGeneration.Tests;

/// <summary>
/// Tests for <see cref="JpfnParser"/> error paths and edge cases.
/// </summary>
[TestClass]
public class JpfnParserTests
{
    [TestMethod]
    public void MissingFnKeyword_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => JpfnParser.Parse("notfn foo() : value => 1;"));
    }

    [TestMethod]
    public void MissingOpenParen_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => JpfnParser.Parse("fn foo) : value => 1;"));
    }

    [TestMethod]
    public void MissingFunctionName_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => JpfnParser.Parse("fn (value x) : value => x;"));
    }

    [TestMethod]
    public void MissingCloseParen_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => JpfnParser.Parse("fn foo(value x : value => x;"));
    }

    [TestMethod]
    public void MissingColonBeforeReturnType_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => JpfnParser.Parse("fn foo(value x) value => x;"));
    }

    [TestMethod]
    public void UnknownReturnType_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => JpfnParser.Parse("fn foo(value x) : nodes => x;"));
    }

    [TestMethod]
    public void EmptyExpressionBody_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => JpfnParser.Parse("fn foo(value x) : value => ;"));
    }

    [TestMethod]
    public void UnmatchedBraceInBlockBody_Throws()
    {
        const string input = """
            fn foo(value x) : value
            {
                return x;
            """;
        Assert.ThrowsExactly<FormatException>(() => JpfnParser.Parse(input));
    }

    [TestMethod]
    public void EmptyParameter_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => JpfnParser.Parse("fn foo(value x, ) : value => x;"));
    }

    [TestMethod]
    public void TrailingSpaceAfterType_TreatedAsNameOnly()
    {
        // After Trim(), "value " becomes "value" — treated as untyped param named "value"
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse("fn foo(value ) : value => 1;");
        Assert.AreEqual(1, (result).Count());
        Assert.AreEqual("value", result[0].Parameters[0].Name);
        Assert.AreEqual(FunctionParamType.Value, result[0].Parameters[0].Type);
    }

    [TestMethod]
    public void UnknownParameterType_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => JpfnParser.Parse("fn foo(badtype x) : value => x;"));
    }

    [TestMethod]
    public void UnexpectedTokenAfterReturnType_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => JpfnParser.Parse("fn foo() : value xyz"));
    }

    [TestMethod]
    public void EofAfterSignature_Throws()
    {
        // Block form: signature with no opening brace and no more lines
        Assert.ThrowsExactly<FormatException>(() => JpfnParser.Parse("fn foo() : value"));
    }

    [TestMethod]
    public void BlockFormBraceOnSameLine_Parses()
    {
        const string input = """
            fn foo(value x) : value {
                return x;
            }
            """;
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse(input);
        Assert.AreEqual(1, (result).Count());
        Assert.AreEqual("foo", result[0].Name);
        Assert.IsFalse(result[0].IsExpression);
    }

    [TestMethod]
    public void ExpressionFormWithNoTrailingSemicolon_Parses()
    {
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse("fn id(value x) : value => x");
        Assert.AreEqual(1, (result).Count());
        Assert.AreEqual("id", result[0].Name);
        Assert.IsTrue(result[0].IsExpression);
        Assert.AreEqual("x", result[0].Body);
    }

    [TestMethod]
    public void ParameterTypeDefault_IsValue()
    {
        // When only a name is given (no type), should default to value
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse("fn id(x) : value => x");
        Assert.AreEqual(1, (result).Count());
        Assert.AreEqual(FunctionParamType.Value, result[0].Parameters[0].Type);
    }

    [TestMethod]
    public void LogicalReturnType_Parses()
    {
        const string input = "fn always() : logical => true";
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse(input);
        Assert.AreEqual(1, (result).Count());
        Assert.AreEqual(FunctionParamType.Logical, result[0].ReturnType);
    }

    [TestMethod]
    public void LogicalParameterType_Parses()
    {
        const string input = "fn check(logical flag) : value => flag ? 1 : 0";
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse(input);
        Assert.AreEqual(1, (result).Count());
        Assert.AreEqual(FunctionParamType.Logical, result[0].Parameters[0].Type);
        Assert.AreEqual("flag", result[0].Parameters[0].Name);
    }

    [TestMethod]
    public void NodesParameterType_Parses()
    {
        const string input = "fn first(nodes n) : value => n[0]";
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse(input);
        Assert.AreEqual(1, (result).Count());
        Assert.AreEqual(FunctionParamType.Nodes, result[0].Parameters[0].Type);
    }

    [TestMethod]
    public void CommentsAndBlankLinesSkipped()
    {
        const string input = """
            // This is a comment

            fn id(value x) : value => x
            // Another comment
            fn dbl(value x) : value => x

            """;
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse(input);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void EmptyInput_ReturnsEmpty()
    {
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse("");
        Assert.AreEqual(0, (result).Count());
    }

    [TestMethod]
    public void BlockFormWithNestedBraces()
    {
        const string input = """
            fn check(value x) : logical
            {
                if (true) { return true; }
                return false;
            }
            """;
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse(input);
        Assert.AreEqual(1, (result).Count());
        Assert.IsFalse(result[0].IsExpression);
    }

    [TestMethod]
    public void BlockFormWithCommentBeforeBrace()
    {
        const string input = """
            fn check() : value
            // comment before brace
            {
                return default;
            }
            """;
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse(input);
        Assert.AreEqual(1, (result).Count());
        Assert.IsFalse(result[0].IsExpression);
    }

    [TestMethod]
    public void BlockFormNonBraceLineAfterSignature_Throws()
    {
        // After the signature, the next non-blank/non-comment line should be '{'
        const string input = """
            fn check() : value
            not_a_brace
            """;
        Assert.ThrowsExactly<FormatException>(() => JpfnParser.Parse(input));
    }
}
