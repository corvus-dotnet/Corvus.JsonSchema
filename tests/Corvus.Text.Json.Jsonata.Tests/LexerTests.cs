// <copyright file="LexerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

public class LexerTests
{
    [Fact]
    public void SimpleFieldName()
    {
        var lexer = new Lexer("foo");
        var token = lexer.Next();
        Assert.Equal(TokenType.Name, token.Type);
        Assert.Equal("foo", token.Value);
        Assert.Equal(0, token.Position);
        Assert.Equal(TokenType.End, lexer.Next().Type);
    }

    [Fact]
    public void NumberLiteral()
    {
        var lexer = new Lexer("42");
        var token = lexer.Next();
        Assert.Equal(TokenType.Number, token.Type);
        Assert.Equal("42", token.Value);
        Assert.Equal(42.0, token.NumericValue);
    }

    [Fact]
    public void NegativeSignIsOperator()
    {
        // The lexer produces '-' as an operator; the parser handles unary negation
        var lexer = new Lexer("-3.14");
        var token = lexer.Next();
        Assert.Equal(TokenType.Operator, token.Type);
        Assert.Equal("-", token.Value);
        var num = lexer.Next();
        Assert.Equal(TokenType.Number, num.Type);
        Assert.Equal(3.14, num.NumericValue);
    }

    [Fact]
    public void ScientificNotation()
    {
        var lexer = new Lexer("1.5e10");
        var token = lexer.Next();
        Assert.Equal(TokenType.Number, token.Type);
        Assert.Equal(1.5e10, token.NumericValue);
    }

    [Fact]
    public void StringLiteralDoubleQuoted()
    {
        var lexer = new Lexer("""
            "hello world"
            """.Trim());
        var token = lexer.Next();
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("hello world", token.Value);
    }

    [Fact]
    public void StringLiteralSingleQuoted()
    {
        var lexer = new Lexer("'hello'");
        var token = lexer.Next();
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("hello", token.Value);
    }

    [Fact]
    public void StringWithEscapeSequences()
    {
        var lexer = new Lexer("""
            "line1\nline2\ttab\\backslash"
            """.Trim());
        var token = lexer.Next();
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("line1\nline2\ttab\\backslash", token.Value);
    }

    [Fact]
    public void StringWithUnicodeEscape()
    {
        var lexer = new Lexer("""
            "\u0041\u0042"
            """.Trim());
        var token = lexer.Next();
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("AB", token.Value);
    }

    [Fact]
    public void Variable()
    {
        var lexer = new Lexer("$count");
        var token = lexer.Next();
        Assert.Equal(TokenType.Variable, token.Type);
        Assert.Equal("count", token.Value);
    }

    [Fact]
    public void DollarAlone()
    {
        var lexer = new Lexer("$");
        var token = lexer.Next();
        Assert.Equal(TokenType.Variable, token.Type);
        Assert.Equal("", token.Value);
    }

    [Fact]
    public void ValueLiterals()
    {
        var lexer = new Lexer("true false null");
        Assert.Equal(TokenType.Value, lexer.Next().Type);
        Assert.Equal(TokenType.Value, lexer.Next().Type);
        Assert.Equal(TokenType.Value, lexer.Next().Type);
        Assert.Equal(TokenType.End, lexer.Next().Type);
    }

    [Fact]
    public void KeywordsAsOperators()
    {
        var lexer = new Lexer("and or in");
        Assert.Equal(TokenType.Operator, lexer.Next().Type);
        Assert.Equal(TokenType.Operator, lexer.Next().Type);
        Assert.Equal(TokenType.Operator, lexer.Next().Type);
    }

    [Fact]
    public void SingleCharOperators()
    {
        var lexer = new Lexer(".[]{}(),;:?+-*/%|=<>^&@#");
        string[] expected = [".", "[", "]", "{", "}", "(", ")", ",", ";", ":", "?", "+", "-", "*", "/", "%", "|", "=", "<", ">", "^", "&", "@", "#"];
        foreach (var op in expected)
        {
            var token = lexer.Next(prefixMode: true);
            Assert.Equal(TokenType.Operator, token.Type);
            Assert.Equal(op, token.Value);
        }
    }

    [Fact]
    public void DoubleCharOperators()
    {
        var lexer = new Lexer(".. := != >= <= ** ~> ?: ??");
        string[] expected = ["..", ":=", "!=", ">=", "<=", "**", "~>", "?:", "??"];
        foreach (var op in expected)
        {
            var token = lexer.Next(prefixMode: true);
            Assert.Equal(TokenType.Operator, token.Type);
            Assert.Equal(op, token.Value);
        }
    }

    [Fact]
    public void BacktickQuotedName()
    {
        var lexer = new Lexer("`field name`");
        var token = lexer.Next();
        Assert.Equal(TokenType.Name, token.Type);
        Assert.Equal("field name", token.Value);
    }

    [Fact]
    public void RegexLiteral()
    {
        var lexer = new Lexer("/[a-z]+/im");
        var token = lexer.Next(prefixMode: false);
        Assert.Equal(TokenType.Regex, token.Type);
        Assert.Equal("[a-z]+", token.RegexPattern);
        Assert.Equal("im", token.RegexFlags);
    }

    [Fact]
    public void RegexVsDivision()
    {
        // When prefixMode is true, / is treated as a division operator
        var lexer = new Lexer("/2");
        var token = lexer.Next(prefixMode: true);
        Assert.Equal(TokenType.Operator, token.Type);
        Assert.Equal("/", token.Value);
    }

    [Fact]
    public void SkipsComments()
    {
        var lexer = new Lexer("foo /* this is a comment */ bar");
        var first = lexer.Next();
        Assert.Equal("foo", first.Value);
        var second = lexer.Next();
        Assert.Equal("bar", second.Value);
    }

    [Fact]
    public void UnterminatedCommentThrows()
    {
        var lexer = new Lexer("foo /* no end");
        lexer.Next();
        var ex = Assert.Throws<JsonataException>(() => lexer.Next());
        Assert.Equal("S0106", ex.Code);
    }

    [Fact]
    public void UnterminatedStringThrows()
    {
        var lexer = new Lexer("\"hello");
        var ex = Assert.Throws<JsonataException>(() => lexer.Next());
        Assert.Equal("S0101", ex.Code);
    }

    [Fact]
    public void IllegalEscapeSequenceThrows()
    {
        var lexer = new Lexer("\"\\x\"");
        var ex = Assert.Throws<JsonataException>(() => lexer.Next());
        Assert.Equal("S0103", ex.Code);
    }

    [Fact]
    public void UnterminatedBacktickThrows()
    {
        var lexer = new Lexer("`no end");
        var ex = Assert.Throws<JsonataException>(() => lexer.Next());
        Assert.Equal("S0105", ex.Code);
    }

    [Fact]
    public void WhitespaceVariations()
    {
        var lexer = new Lexer("  foo\tbar\n  baz  ");
        Assert.Equal("foo", lexer.Next().Value);
        Assert.Equal("bar", lexer.Next().Value);
        Assert.Equal("baz", lexer.Next().Value);
        Assert.Equal(TokenType.End, lexer.Next().Type);
    }

    [Fact]
    public void ComplexExpression()
    {
        var lexer = new Lexer("Account.Order.Product.Price");
        Assert.Equal("Account", lexer.Next().Value);
        Assert.Equal(".", lexer.Next(prefixMode: true).Value);
        Assert.Equal("Order", lexer.Next().Value);
        Assert.Equal(".", lexer.Next(prefixMode: true).Value);
        Assert.Equal("Product", lexer.Next().Value);
        Assert.Equal(".", lexer.Next(prefixMode: true).Value);
        Assert.Equal("Price", lexer.Next().Value);
        Assert.Equal(TokenType.End, lexer.Next().Type);
    }

    [Fact]
    public void FunctionCallExpression()
    {
        var lexer = new Lexer("$sum(Account.Order.Product.Price)");
        Assert.Equal(TokenType.Variable, lexer.Next().Type);
        Assert.Equal("(", lexer.Next(prefixMode: true).Value);
        Assert.Equal("Account", lexer.Next().Value);
        Assert.Equal(".", lexer.Next(prefixMode: true).Value);
        Assert.Equal("Order", lexer.Next().Value);
        Assert.Equal(".", lexer.Next(prefixMode: true).Value);
        Assert.Equal("Product", lexer.Next().Value);
        Assert.Equal(".", lexer.Next(prefixMode: true).Value);
        Assert.Equal("Price", lexer.Next().Value);
        Assert.Equal(")", lexer.Next(prefixMode: true).Value);
    }
}