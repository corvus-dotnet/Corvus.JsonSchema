// <copyright file="LexerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

public class LexerTests
{
    [Fact]
    public void SimpleFieldName()
    {
        byte[] utf8 = "foo"u8.ToArray();
        var lexer = new Lexer(utf8);
        var token = lexer.Next();
        Assert.Equal(TokenType.Name, token.Type);
        Assert.Equal("foo", token.GetValue(utf8));
        Assert.Equal(0, token.Position);
        Assert.Equal(TokenType.End, lexer.Next().Type);
    }

    [Fact]
    public void NumberLiteral()
    {
        var lexer = new Lexer("42"u8.ToArray());
        var token = lexer.Next();
        Assert.Equal(TokenType.Number, token.Type);
        Assert.Equal(42.0, token.NumericValue);
    }

    [Fact]
    public void NegativeSignIsOperator()
    {
        var lexer = new Lexer("-3.14"u8.ToArray());
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
        var lexer = new Lexer("1.5e10"u8.ToArray());
        var token = lexer.Next();
        Assert.Equal(TokenType.Number, token.Type);
        Assert.Equal(1.5e10, token.NumericValue);
    }

    [Fact]
    public void StringLiteralDoubleQuoted()
    {
        var lexer = new Lexer(Encoding.UTF8.GetBytes("""
            "hello world"
            """.Trim()));
        var token = lexer.Next();
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("hello world", token.Value);
    }

    [Fact]
    public void StringLiteralSingleQuoted()
    {
        var lexer = new Lexer("'hello'"u8.ToArray());
        var token = lexer.Next();
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("hello", token.Value);
    }

    [Fact]
    public void StringWithEscapeSequences()
    {
        var lexer = new Lexer(Encoding.UTF8.GetBytes("""
            "line1\nline2\ttab\\backslash"
            """.Trim()));
        var token = lexer.Next();
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("line1\nline2\ttab\\backslash", token.Value);
    }

    [Fact]
    public void StringWithUnicodeEscape()
    {
        var lexer = new Lexer(Encoding.UTF8.GetBytes("""
            "\u0041\u0042"
            """.Trim()));
        var token = lexer.Next();
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("AB", token.Value);
    }

    [Fact]
    public void Variable()
    {
        byte[] utf8 = "$count"u8.ToArray();
        var lexer = new Lexer(utf8);
        var token = lexer.Next();
        Assert.Equal(TokenType.Variable, token.Type);
        Assert.Equal("count", token.GetValue(utf8));
    }

    [Fact]
    public void DollarAlone()
    {
        byte[] utf8 = "$"u8.ToArray();
        var lexer = new Lexer(utf8);
        var token = lexer.Next();
        Assert.Equal(TokenType.Variable, token.Type);
        Assert.Equal("", token.GetValue(utf8));
    }

    [Fact]
    public void ValueLiterals()
    {
        var lexer = new Lexer("true false null"u8.ToArray());
        Assert.Equal(TokenType.Value, lexer.Next().Type);
        Assert.Equal(TokenType.Value, lexer.Next().Type);
        Assert.Equal(TokenType.Value, lexer.Next().Type);
        Assert.Equal(TokenType.End, lexer.Next().Type);
    }

    [Fact]
    public void KeywordsAsOperators()
    {
        var lexer = new Lexer("and or in"u8.ToArray());
        Assert.Equal(TokenType.Operator, lexer.Next().Type);
        Assert.Equal(TokenType.Operator, lexer.Next().Type);
        Assert.Equal(TokenType.Operator, lexer.Next().Type);
    }

    [Fact]
    public void SingleCharOperators()
    {
        var lexer = new Lexer(".[]{}(),;:?+-*/%|=<>^&@#"u8.ToArray());
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
        var lexer = new Lexer(".. := != >= <= ** ~> ?: ??"u8.ToArray());
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
        byte[] utf8 = "`field name`"u8.ToArray();
        var lexer = new Lexer(utf8);
        var token = lexer.Next();
        Assert.Equal(TokenType.Name, token.Type);
        Assert.Equal("field name", token.GetValue(utf8));
    }

    [Fact]
    public void RegexLiteral()
    {
        var lexer = new Lexer("/[a-z]+/im"u8.ToArray());
        var token = lexer.Next(prefixMode: false);
        Assert.Equal(TokenType.Regex, token.Type);
        Assert.Equal("[a-z]+", token.RegexPattern);
        Assert.Equal("im", token.RegexFlags);
    }

    [Fact]
    public void RegexVsDivision()
    {
        var lexer = new Lexer("/2"u8.ToArray());
        var token = lexer.Next(prefixMode: true);
        Assert.Equal(TokenType.Operator, token.Type);
        Assert.Equal("/", token.Value);
    }

    [Fact]
    public void SkipsComments()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("foo /* this is a comment */ bar");
        var lexer = new Lexer(utf8);
        var first = lexer.Next();
        Assert.Equal("foo", first.GetValue(utf8));
        var second = lexer.Next();
        Assert.Equal("bar", second.GetValue(utf8));
    }

    [Fact]
    public void UnterminatedCommentThrows()
    {
        var lexer = new Lexer("foo /* no end"u8.ToArray());
        lexer.Next();
        JsonataException ex;
        try
        {
            lexer.Next();
            ex = null!;
            Assert.Fail("Expected JsonataException");
        }
        catch (JsonataException e)
        {
            ex = e;
        }

        Assert.Equal("S0106", ex.Code);
    }

    [Fact]
    public void UnterminatedStringThrows()
    {
        var lexer = new Lexer("\"hello"u8.ToArray());
        JsonataException ex;
        try
        {
            lexer.Next();
            ex = null!;
            Assert.Fail("Expected JsonataException");
        }
        catch (JsonataException e)
        {
            ex = e;
        }

        Assert.Equal("S0101", ex.Code);
    }

    [Fact]
    public void IllegalEscapeSequenceThrows()
    {
        var lexer = new Lexer("\"\\x\""u8.ToArray());
        JsonataException ex;
        try
        {
            lexer.Next();
            ex = null!;
            Assert.Fail("Expected JsonataException");
        }
        catch (JsonataException e)
        {
            ex = e;
        }

        Assert.Equal("S0103", ex.Code);
    }

    [Fact]
    public void UnterminatedBacktickThrows()
    {
        var lexer = new Lexer("`no end"u8.ToArray());
        JsonataException ex;
        try
        {
            lexer.Next();
            ex = null!;
            Assert.Fail("Expected JsonataException");
        }
        catch (JsonataException e)
        {
            ex = e;
        }

        Assert.Equal("S0105", ex.Code);
    }

    [Fact]
    public void WhitespaceVariations()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("  foo\tbar\n  baz  ");
        var lexer = new Lexer(utf8);
        Assert.Equal("foo", lexer.Next().GetValue(utf8));
        Assert.Equal("bar", lexer.Next().GetValue(utf8));
        Assert.Equal("baz", lexer.Next().GetValue(utf8));
        Assert.Equal(TokenType.End, lexer.Next().Type);
    }

    [Fact]
    public void ComplexExpression()
    {
        byte[] utf8 = "Account.Order.Product.Price"u8.ToArray();
        var lexer = new Lexer(utf8);
        Assert.Equal("Account", lexer.Next().GetValue(utf8));
        Assert.Equal(".", lexer.Next(prefixMode: true).Value);
        Assert.Equal("Order", lexer.Next().GetValue(utf8));
        Assert.Equal(".", lexer.Next(prefixMode: true).Value);
        Assert.Equal("Product", lexer.Next().GetValue(utf8));
        Assert.Equal(".", lexer.Next(prefixMode: true).Value);
        Assert.Equal("Price", lexer.Next().GetValue(utf8));
        Assert.Equal(TokenType.End, lexer.Next().Type);
    }

    [Fact]
    public void FunctionCallExpression()
    {
        byte[] utf8 = "$sum(Account.Order.Product.Price)"u8.ToArray();
        var lexer = new Lexer(utf8);
        Assert.Equal(TokenType.Variable, lexer.Next().Type);
        Assert.Equal("(", lexer.Next(prefixMode: true).Value);
        Assert.Equal("Account", lexer.Next().GetValue(utf8));
        Assert.Equal(".", lexer.Next(prefixMode: true).Value);
        Assert.Equal("Order", lexer.Next().GetValue(utf8));
        Assert.Equal(".", lexer.Next(prefixMode: true).Value);
        Assert.Equal("Product", lexer.Next().GetValue(utf8));
        Assert.Equal(".", lexer.Next(prefixMode: true).Value);
        Assert.Equal("Price", lexer.Next().GetValue(utf8));
        Assert.Equal(")", lexer.Next(prefixMode: true).Value);
    }
}
