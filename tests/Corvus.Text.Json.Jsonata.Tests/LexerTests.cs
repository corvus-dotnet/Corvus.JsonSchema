// <copyright file="LexerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Jsonata.Tests;

[TestClass]
public class LexerTests
{
    [TestMethod]
    public void SimpleFieldName()
    {
        byte[] utf8 = "foo"u8.ToArray();
        var lexer = new Lexer(utf8);
        var token = lexer.Next();
        Assert.AreEqual(TokenType.Name, token.Type);
        Assert.AreEqual("foo", token.GetValue(utf8));
        Assert.AreEqual(0, token.Position);
        Assert.AreEqual(TokenType.End, lexer.Next().Type);
    }

    [TestMethod]
    public void NumberLiteral()
    {
        var lexer = new Lexer("42"u8.ToArray());
        var token = lexer.Next();
        Assert.AreEqual(TokenType.Number, token.Type);
        Assert.AreEqual(42.0, token.NumericValue);
    }

    [TestMethod]
    public void NegativeSignIsOperator()
    {
        var lexer = new Lexer("-3.14"u8.ToArray());
        var token = lexer.Next();
        Assert.AreEqual(TokenType.Operator, token.Type);
        Assert.AreEqual("-", token.Value);
        var num = lexer.Next();
        Assert.AreEqual(TokenType.Number, num.Type);
        Assert.AreEqual(3.14, num.NumericValue);
    }

    [TestMethod]
    public void ScientificNotation()
    {
        var lexer = new Lexer("1.5e10"u8.ToArray());
        var token = lexer.Next();
        Assert.AreEqual(TokenType.Number, token.Type);
        Assert.AreEqual(1.5e10, token.NumericValue);
    }

    [TestMethod]
    public void StringLiteralDoubleQuoted()
    {
        var lexer = new Lexer(Encoding.UTF8.GetBytes("""
            "hello world"
            """.Trim()));
        var token = lexer.Next();
        Assert.AreEqual(TokenType.String, token.Type);
        Assert.AreEqual("hello world", token.Value);
    }

    [TestMethod]
    public void StringLiteralSingleQuoted()
    {
        var lexer = new Lexer("'hello'"u8.ToArray());
        var token = lexer.Next();
        Assert.AreEqual(TokenType.String, token.Type);
        Assert.AreEqual("hello", token.Value);
    }

    [TestMethod]
    public void StringWithEscapeSequences()
    {
        var lexer = new Lexer(Encoding.UTF8.GetBytes("""
            "line1\nline2\ttab\\backslash"
            """.Trim()));
        var token = lexer.Next();
        Assert.AreEqual(TokenType.String, token.Type);
        Assert.AreEqual("line1\nline2\ttab\\backslash", token.Value);
    }

    [TestMethod]
    public void StringWithUnicodeEscape()
    {
        var lexer = new Lexer(Encoding.UTF8.GetBytes("""
            "\u0041\u0042"
            """.Trim()));
        var token = lexer.Next();
        Assert.AreEqual(TokenType.String, token.Type);
        Assert.AreEqual("AB", token.Value);
    }

    [TestMethod]
    public void Variable()
    {
        byte[] utf8 = "$count"u8.ToArray();
        var lexer = new Lexer(utf8);
        var token = lexer.Next();
        Assert.AreEqual(TokenType.Variable, token.Type);
        Assert.AreEqual("count", token.GetValue(utf8));
    }

    [TestMethod]
    public void DollarAlone()
    {
        byte[] utf8 = "$"u8.ToArray();
        var lexer = new Lexer(utf8);
        var token = lexer.Next();
        Assert.AreEqual(TokenType.Variable, token.Type);
        Assert.AreEqual("", token.GetValue(utf8));
    }

    [TestMethod]
    public void ValueLiterals()
    {
        var lexer = new Lexer("true false null"u8.ToArray());
        Assert.AreEqual(TokenType.Value, lexer.Next().Type);
        Assert.AreEqual(TokenType.Value, lexer.Next().Type);
        Assert.AreEqual(TokenType.Value, lexer.Next().Type);
        Assert.AreEqual(TokenType.End, lexer.Next().Type);
    }

    [TestMethod]
    public void KeywordsAsOperators()
    {
        var lexer = new Lexer("and or in"u8.ToArray());
        Assert.AreEqual(TokenType.Operator, lexer.Next().Type);
        Assert.AreEqual(TokenType.Operator, lexer.Next().Type);
        Assert.AreEqual(TokenType.Operator, lexer.Next().Type);
    }

    [TestMethod]
    public void SingleCharOperators()
    {
        var lexer = new Lexer(".[]{}(),;:?+-*/%|=<>^&@#"u8.ToArray());
        string[] expected = [".", "[", "]", "{", "}", "(", ")", ",", ";", ":", "?", "+", "-", "*", "/", "%", "|", "=", "<", ">", "^", "&", "@", "#"];
        foreach (var op in expected)
        {
            var token = lexer.Next(prefixMode: true);
            Assert.AreEqual(TokenType.Operator, token.Type);
            Assert.AreEqual(op, token.Value);
        }
    }

    [TestMethod]
    public void DoubleCharOperators()
    {
        var lexer = new Lexer(".. := != >= <= ** ~> ?: ??"u8.ToArray());
        string[] expected = ["..", ":=", "!=", ">=", "<=", "**", "~>", "?:", "??"];
        foreach (var op in expected)
        {
            var token = lexer.Next(prefixMode: true);
            Assert.AreEqual(TokenType.Operator, token.Type);
            Assert.AreEqual(op, token.Value);
        }
    }

    [TestMethod]
    public void BacktickQuotedName()
    {
        byte[] utf8 = "`field name`"u8.ToArray();
        var lexer = new Lexer(utf8);
        var token = lexer.Next();
        Assert.AreEqual(TokenType.Name, token.Type);
        Assert.AreEqual("field name", token.GetValue(utf8));
    }

    [TestMethod]
    public void RegexLiteral()
    {
        var lexer = new Lexer("/[a-z]+/im"u8.ToArray());
        var token = lexer.Next(prefixMode: false);
        Assert.AreEqual(TokenType.Regex, token.Type);
        Assert.AreEqual("[a-z]+", token.RegexPattern);
        Assert.AreEqual("im", token.RegexFlags);
    }

    [TestMethod]
    public void RegexVsDivision()
    {
        var lexer = new Lexer("/2"u8.ToArray());
        var token = lexer.Next(prefixMode: true);
        Assert.AreEqual(TokenType.Operator, token.Type);
        Assert.AreEqual("/", token.Value);
    }

    [TestMethod]
    public void SkipsComments()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("foo /* this is a comment */ bar");
        var lexer = new Lexer(utf8);
        var first = lexer.Next();
        Assert.AreEqual("foo", first.GetValue(utf8));
        var second = lexer.Next();
        Assert.AreEqual("bar", second.GetValue(utf8));
    }

    [TestMethod]
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

        Assert.AreEqual("S0106", ex.Code);
    }

    [TestMethod]
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

        Assert.AreEqual("S0101", ex.Code);
    }

    [TestMethod]
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

        Assert.AreEqual("S0103", ex.Code);
    }

    [TestMethod]
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

        Assert.AreEqual("S0105", ex.Code);
    }

    [TestMethod]
    public void WhitespaceVariations()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("  foo\tbar\n  baz  ");
        var lexer = new Lexer(utf8);
        Assert.AreEqual("foo", lexer.Next().GetValue(utf8));
        Assert.AreEqual("bar", lexer.Next().GetValue(utf8));
        Assert.AreEqual("baz", lexer.Next().GetValue(utf8));
        Assert.AreEqual(TokenType.End, lexer.Next().Type);
    }

    [TestMethod]
    public void ComplexExpression()
    {
        byte[] utf8 = "Account.Order.Product.Price"u8.ToArray();
        var lexer = new Lexer(utf8);
        Assert.AreEqual("Account", lexer.Next().GetValue(utf8));
        Assert.AreEqual(".", lexer.Next(prefixMode: true).Value);
        Assert.AreEqual("Order", lexer.Next().GetValue(utf8));
        Assert.AreEqual(".", lexer.Next(prefixMode: true).Value);
        Assert.AreEqual("Product", lexer.Next().GetValue(utf8));
        Assert.AreEqual(".", lexer.Next(prefixMode: true).Value);
        Assert.AreEqual("Price", lexer.Next().GetValue(utf8));
        Assert.AreEqual(TokenType.End, lexer.Next().Type);
    }

    [TestMethod]
    public void FunctionCallExpression()
    {
        byte[] utf8 = "$sum(Account.Order.Product.Price)"u8.ToArray();
        var lexer = new Lexer(utf8);
        Assert.AreEqual(TokenType.Variable, lexer.Next().Type);
        Assert.AreEqual("(", lexer.Next(prefixMode: true).Value);
        Assert.AreEqual("Account", lexer.Next().GetValue(utf8));
        Assert.AreEqual(".", lexer.Next(prefixMode: true).Value);
        Assert.AreEqual("Order", lexer.Next().GetValue(utf8));
        Assert.AreEqual(".", lexer.Next(prefixMode: true).Value);
        Assert.AreEqual("Product", lexer.Next().GetValue(utf8));
        Assert.AreEqual(".", lexer.Next(prefixMode: true).Value);
        Assert.AreEqual("Price", lexer.Next().GetValue(utf8));
        Assert.AreEqual(")", lexer.Next(prefixMode: true).Value);
    }
}
