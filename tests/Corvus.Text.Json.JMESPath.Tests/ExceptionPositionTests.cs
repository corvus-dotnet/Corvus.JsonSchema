// <copyright file="ExceptionPositionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.JMESPath;
using Xunit;

namespace Corvus.Text.Json.JMESPath.Tests;

/// <summary>
/// Tests that <see cref="JMESPathException.Position"/> reports the correct
/// 0-based byte offset for lexer and parser errors.
/// </summary>
public class ExceptionPositionTests
{
    private static readonly JsonElement NullElement = JsonElement.ParseValue("null"u8);

    // ----- Lexer errors -----

    [Theory]
    [InlineData("#", 0)]          // unexpected character at start
    [InlineData("foo #", 4)]      // unexpected character after whitespace
    [InlineData("a.b.#", 4)]      // unexpected character mid-expression
    public void UnexpectedCharacter_ReportsCorrectPosition(string expression, int expectedPosition)
    {
        JMESPathException ex = Assert.Throws<JMESPathException>(
            () => JMESPathEvaluator.Default.Search(expression, NullElement));
        Assert.Equal(expectedPosition, ex.Position);
    }

    [Theory]
    [InlineData("[-a]", 1)]       // dash not followed by digit inside bracket
    public void DashWithoutDigit_ReportsStartPosition(string expression, int expectedPosition)
    {
        JMESPathException ex = Assert.Throws<JMESPathException>(
            () => JMESPathEvaluator.Default.Search(expression, NullElement));
        Assert.Equal(expectedPosition, ex.Position);
    }

    [Fact]
    public void UnterminatedRawString_ReportsStartPosition()
    {
        JMESPathException ex = Assert.Throws<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("'unterminated", NullElement));
        Assert.Equal(0, ex.Position);
    }

    [Fact]
    public void UnterminatedRawStringAfterExpression_ReportsCorrectPosition()
    {
        JMESPathException ex = Assert.Throws<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("foo.'bar", NullElement));
        Assert.Equal(4, ex.Position);
    }

    [Fact]
    public void UnterminatedQuotedIdentifier_ReportsStartPosition()
    {
        JMESPathException ex = Assert.Throws<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("\"unclosed", NullElement));
        Assert.Equal(0, ex.Position);
    }

    [Fact]
    public void UnterminatedQuotedIdentifierAfterDot_ReportsCorrectPosition()
    {
        JMESPathException ex = Assert.Throws<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("a.\"unclosed", NullElement));
        Assert.Equal(2, ex.Position);
    }

    [Fact]
    public void InvalidEscapeInQuotedIdentifier_ReportsPosition()
    {
        // "\x" — backslash at position 1, 'x' at position 2
        JMESPathException ex = Assert.Throws<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("\"\\x\"", NullElement));
        Assert.True(ex.Position >= 0, "Position should be set for invalid escape");
    }

    [Fact]
    public void UnterminatedLiteral_ReportsStartPosition()
    {
        JMESPathException ex = Assert.Throws<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("`unclosed", NullElement));
        Assert.Equal(0, ex.Position);
    }

    [Fact]
    public void UnterminatedLiteralAfterExpression_ReportsCorrectPosition()
    {
        JMESPathException ex = Assert.Throws<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("foo | `{}", NullElement));
        Assert.Equal(6, ex.Position);
    }

    [Fact]
    public void SingleEquals_ReportsPosition()
    {
        JMESPathException ex = Assert.Throws<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("a = b", NullElement));
        Assert.Equal(2, ex.Position);
    }

    [Fact]
    public void RawStringEscapeAtEndOfInput_ReportsStartPosition()
    {
        // Raw string with trailing backslash: 'hello\
        JMESPathException ex = Assert.Throws<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("'hello\\", NullElement));
        Assert.Equal(0, ex.Position);
    }

    // ----- Parser errors -----

    [Theory]
    [InlineData("foo bar", 4)]  // unexpected token 'bar' after valid expression
    [InlineData("a b", 2)]     // unexpected token after identifier
    public void UnexpectedTokenAfterExpression_ReportsPosition(string expression, int expectedPosition)
    {
        JMESPathException ex = Assert.Throws<JMESPathException>(
            () => JMESPathEvaluator.Default.Search(expression, NullElement));
        Assert.Equal(expectedPosition, ex.Position);
    }

    [Fact]
    public void DotFollowedByInvalidToken_ReportsPosition()
    {
        JMESPathException ex = Assert.Throws<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("foo.|", NullElement));
        Assert.True(ex.Position >= 4, "Position should be at or after the pipe");
    }

    [Fact]
    public void MissingHashKey_ReportsPosition()
    {
        // { 123: foo } — number instead of identifier for hash key
        JMESPathException ex = Assert.Throws<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("a.{123: foo}", NullElement));
        Assert.True(ex.Position >= 3, "Position should be inside the hash expression");
    }

    // ----- Default position (-1) -----

    [Fact]
    public void ConstructorWithoutPosition_HasDefaultMinusOne()
    {
        JMESPathException ex = new("test error");
        Assert.Equal(-1, ex.Position);
    }

    [Fact]
    public void ConstructorWithPosition_ReportsGivenPosition()
    {
        JMESPathException ex = new("test error", 42);
        Assert.Equal(42, ex.Position);
    }

    [Fact]
    public void InnerExceptionConstructor_HasDefaultMinusOne()
    {
        JMESPathException ex = new("test error", new InvalidOperationException());
        Assert.Equal(-1, ex.Position);
    }
}
