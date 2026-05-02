// <copyright file="JpfnParserTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonPath.CodeGeneration;
using Xunit;

namespace Corvus.Text.Json.JsonPath.CodeGeneration.Tests;

/// <summary>
/// Tests for <see cref="JpfnParser"/> error paths and edge cases.
/// </summary>
public class JpfnParserTests
{
    [Fact]
    public void MissingFnKeyword_Throws()
    {
        Assert.Throws<FormatException>(() => JpfnParser.Parse("notfn foo() : value => 1;"));
    }

    [Fact]
    public void MissingOpenParen_Throws()
    {
        Assert.Throws<FormatException>(() => JpfnParser.Parse("fn foo) : value => 1;"));
    }

    [Fact]
    public void MissingFunctionName_Throws()
    {
        Assert.Throws<FormatException>(() => JpfnParser.Parse("fn (value x) : value => x;"));
    }

    [Fact]
    public void MissingCloseParen_Throws()
    {
        Assert.Throws<FormatException>(() => JpfnParser.Parse("fn foo(value x : value => x;"));
    }

    [Fact]
    public void MissingColonBeforeReturnType_Throws()
    {
        Assert.Throws<FormatException>(() => JpfnParser.Parse("fn foo(value x) value => x;"));
    }

    [Fact]
    public void UnknownReturnType_Throws()
    {
        Assert.Throws<FormatException>(() => JpfnParser.Parse("fn foo(value x) : nodes => x;"));
    }

    [Fact]
    public void EmptyExpressionBody_Throws()
    {
        Assert.Throws<FormatException>(() => JpfnParser.Parse("fn foo(value x) : value => ;"));
    }

    [Fact]
    public void UnmatchedBraceInBlockBody_Throws()
    {
        const string input = """
            fn foo(value x) : value
            {
                return x;
            """;
        Assert.Throws<FormatException>(() => JpfnParser.Parse(input));
    }

    [Fact]
    public void EmptyParameter_Throws()
    {
        Assert.Throws<FormatException>(() => JpfnParser.Parse("fn foo(value x, ) : value => x;"));
    }

    [Fact]
    public void TrailingSpaceAfterType_TreatedAsNameOnly()
    {
        // After Trim(), "value " becomes "value" — treated as untyped param named "value"
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse("fn foo(value ) : value => 1;");
        Assert.Single(result);
        Assert.Equal("value", result[0].Parameters[0].Name);
        Assert.Equal(FunctionParamType.Value, result[0].Parameters[0].Type);
    }

    [Fact]
    public void UnknownParameterType_Throws()
    {
        Assert.Throws<FormatException>(() => JpfnParser.Parse("fn foo(badtype x) : value => x;"));
    }

    [Fact]
    public void UnexpectedTokenAfterReturnType_Throws()
    {
        Assert.Throws<FormatException>(() => JpfnParser.Parse("fn foo() : value xyz"));
    }

    [Fact]
    public void EofAfterSignature_Throws()
    {
        // Block form: signature with no opening brace and no more lines
        Assert.Throws<FormatException>(() => JpfnParser.Parse("fn foo() : value"));
    }

    [Fact]
    public void BlockFormBraceOnSameLine_Parses()
    {
        const string input = """
            fn foo(value x) : value {
                return x;
            }
            """;
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse(input);
        Assert.Single(result);
        Assert.Equal("foo", result[0].Name);
        Assert.False(result[0].IsExpression);
    }

    [Fact]
    public void ExpressionFormWithNoTrailingSemicolon_Parses()
    {
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse("fn id(value x) : value => x");
        Assert.Single(result);
        Assert.Equal("id", result[0].Name);
        Assert.True(result[0].IsExpression);
        Assert.Equal("x", result[0].Body);
    }

    [Fact]
    public void ParameterTypeDefault_IsValue()
    {
        // When only a name is given (no type), should default to value
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse("fn id(x) : value => x");
        Assert.Single(result);
        Assert.Equal(FunctionParamType.Value, result[0].Parameters[0].Type);
    }

    [Fact]
    public void LogicalReturnType_Parses()
    {
        const string input = "fn always() : logical => true";
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse(input);
        Assert.Single(result);
        Assert.Equal(FunctionParamType.Logical, result[0].ReturnType);
    }

    [Fact]
    public void LogicalParameterType_Parses()
    {
        const string input = "fn check(logical flag) : value => flag ? 1 : 0";
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse(input);
        Assert.Single(result);
        Assert.Equal(FunctionParamType.Logical, result[0].Parameters[0].Type);
        Assert.Equal("flag", result[0].Parameters[0].Name);
    }

    [Fact]
    public void NodesParameterType_Parses()
    {
        const string input = "fn first(nodes n) : value => n[0]";
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse(input);
        Assert.Single(result);
        Assert.Equal(FunctionParamType.Nodes, result[0].Parameters[0].Type);
    }

    [Fact]
    public void CommentsAndBlankLinesSkipped()
    {
        const string input = """
            // This is a comment

            fn id(value x) : value => x
            // Another comment
            fn dbl(value x) : value => x

            """;
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse(input);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void EmptyInput_ReturnsEmpty()
    {
        IReadOnlyList<CustomFunction> result = JpfnParser.Parse("");
        Assert.Empty(result);
    }

    [Fact]
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
        Assert.Single(result);
        Assert.False(result[0].IsExpression);
    }

    [Fact]
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
        Assert.Single(result);
        Assert.False(result[0].IsExpression);
    }

    [Fact]
    public void BlockFormNonBraceLineAfterSignature_Throws()
    {
        // After the signature, the next non-blank/non-comment line should be '{'
        const string input = """
            fn check() : value
            not_a_brace
            """;
        Assert.Throws<FormatException>(() => JpfnParser.Parse(input));
    }
}
