// <copyright file="ExceptionPositionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.JMESPath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JMESPath.Tests;

/// <summary>
/// Tests that <see cref="JMESPathException.Position"/> reports the correct
/// 0-based byte offset for lexer and parser errors.
/// </summary>
[TestClass]
public class ExceptionPositionTests
{
    private static readonly JsonElement NullElement = JsonElement.ParseValue("null"u8);

    // ----- Lexer errors -----

    [TestMethod]
    [DataRow("#", 0)]          // unexpected character at start
    [DataRow("foo #", 4)]      // unexpected character after whitespace
    [DataRow("a.b.#", 4)]      // unexpected character mid-expression
    public void UnexpectedCharacter_ReportsCorrectPosition(string expression, int expectedPosition)
    {
        JMESPathException ex = Assert.ThrowsExactly<JMESPathException>(
            () => JMESPathEvaluator.Default.Search(expression, NullElement));
        Assert.AreEqual(expectedPosition, ex.Position);
    }

    [TestMethod]
    [DataRow("[-a]", 1)]       // dash not followed by digit inside bracket
    public void DashWithoutDigit_ReportsStartPosition(string expression, int expectedPosition)
    {
        JMESPathException ex = Assert.ThrowsExactly<JMESPathException>(
            () => JMESPathEvaluator.Default.Search(expression, NullElement));
        Assert.AreEqual(expectedPosition, ex.Position);
    }

    [TestMethod]
    public void UnterminatedRawString_ReportsStartPosition()
    {
        JMESPathException ex = Assert.ThrowsExactly<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("'unterminated", NullElement));
        Assert.AreEqual(0, ex.Position);
    }

    [TestMethod]
    public void UnterminatedRawStringAfterExpression_ReportsCorrectPosition()
    {
        JMESPathException ex = Assert.ThrowsExactly<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("foo.'bar", NullElement));
        Assert.AreEqual(4, ex.Position);
    }

    [TestMethod]
    public void UnterminatedQuotedIdentifier_ReportsStartPosition()
    {
        JMESPathException ex = Assert.ThrowsExactly<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("\"unclosed", NullElement));
        Assert.AreEqual(0, ex.Position);
    }

    [TestMethod]
    public void UnterminatedQuotedIdentifierAfterDot_ReportsCorrectPosition()
    {
        JMESPathException ex = Assert.ThrowsExactly<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("a.\"unclosed", NullElement));
        Assert.AreEqual(2, ex.Position);
    }

    [TestMethod]
    public void InvalidEscapeInQuotedIdentifier_ReportsPosition()
    {
        // "\x" — backslash at position 1, 'x' at position 2
        JMESPathException ex = Assert.ThrowsExactly<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("\"\\x\"", NullElement));
        Assert.IsTrue(ex.Position >= 0, "Position should be set for invalid escape");
    }

    [TestMethod]
    public void UnterminatedLiteral_ReportsStartPosition()
    {
        JMESPathException ex = Assert.ThrowsExactly<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("`unclosed", NullElement));
        Assert.AreEqual(0, ex.Position);
    }

    [TestMethod]
    public void UnterminatedLiteralAfterExpression_ReportsCorrectPosition()
    {
        JMESPathException ex = Assert.ThrowsExactly<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("foo | `{}", NullElement));
        Assert.AreEqual(6, ex.Position);
    }

    [TestMethod]
    public void SingleEquals_ReportsPosition()
    {
        JMESPathException ex = Assert.ThrowsExactly<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("a = b", NullElement));
        Assert.AreEqual(2, ex.Position);
    }

    [TestMethod]
    public void RawStringEscapeAtEndOfInput_ReportsStartPosition()
    {
        // Raw string with trailing backslash: 'hello\
        JMESPathException ex = Assert.ThrowsExactly<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("'hello\\", NullElement));
        Assert.AreEqual(0, ex.Position);
    }

    // ----- Parser errors -----

    [TestMethod]
    [DataRow("foo bar", 4)]  // unexpected token 'bar' after valid expression
    [DataRow("a b", 2)]     // unexpected token after identifier
    public void UnexpectedTokenAfterExpression_ReportsPosition(string expression, int expectedPosition)
    {
        JMESPathException ex = Assert.ThrowsExactly<JMESPathException>(
            () => JMESPathEvaluator.Default.Search(expression, NullElement));
        Assert.AreEqual(expectedPosition, ex.Position);
    }

    [TestMethod]
    public void DotFollowedByInvalidToken_ReportsPosition()
    {
        JMESPathException ex = Assert.ThrowsExactly<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("foo.|", NullElement));
        Assert.IsTrue(ex.Position >= 4, "Position should be at or after the pipe");
    }

    [TestMethod]
    public void MissingHashKey_ReportsPosition()
    {
        // { 123: foo } — number instead of identifier for hash key
        JMESPathException ex = Assert.ThrowsExactly<JMESPathException>(
            () => JMESPathEvaluator.Default.Search("a.{123: foo}", NullElement));
        Assert.IsTrue(ex.Position >= 3, "Position should be inside the hash expression");
    }

    // ----- Default position (-1) -----

    [TestMethod]
    public void ConstructorWithoutPosition_HasDefaultMinusOne()
    {
        JMESPathException ex = new("test error");
        Assert.AreEqual(-1, ex.Position);
    }

    [TestMethod]
    public void ConstructorWithPosition_ReportsGivenPosition()
    {
        JMESPathException ex = new("test error", 42);
        Assert.AreEqual(42, ex.Position);
    }

    [TestMethod]
    public void InnerExceptionConstructor_HasDefaultMinusOne()
    {
        JMESPathException ex = new("test error", new InvalidOperationException());
        Assert.AreEqual(-1, ex.Position);
    }
}
