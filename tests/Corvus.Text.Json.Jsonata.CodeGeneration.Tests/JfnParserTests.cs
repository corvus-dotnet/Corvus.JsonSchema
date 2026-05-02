// <copyright file="JfnParserTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Jsonata.CodeGeneration;
using Xunit;

namespace Corvus.Text.Json.Jsonata.CodeGeneration.Tests;

/// <summary>
/// Tests for <see cref="JfnParser"/>.
/// </summary>
public class JfnParserTests
{
    [Fact]
    public void Parse_ExpressionForm_ParsesCorrectly()
    {
        const string content = """
            fn double_it(x) => x.GetDouble() * 2;
            """;

        IReadOnlyList<CustomFunction> fns = JfnParser.Parse(content);

        Assert.Single(fns);
        Assert.Equal("double_it", fns[0].Name);
        Assert.Equal(new[] { "x" }, fns[0].Parameters);
        Assert.Equal("x.GetDouble() * 2", fns[0].Body);
        Assert.True(fns[0].IsExpression);
    }

    [Fact]
    public void Parse_BlockForm_ParsesCorrectly()
    {
        const string content = """
            fn clamp(val, lo, hi)
            {
                double v = val.GetDouble();
                return v;
            }
            """;

        IReadOnlyList<CustomFunction> fns = JfnParser.Parse(content);

        Assert.Single(fns);
        Assert.Equal("clamp", fns[0].Name);
        Assert.Equal(new[] { "val", "lo", "hi" }, fns[0].Parameters);
        Assert.False(fns[0].IsExpression);
        Assert.Contains("double v = val.GetDouble();", fns[0].Body);
        Assert.Contains("return v;", fns[0].Body);
    }

    [Fact]
    public void Parse_MultipleFunctions_ParsesAll()
    {
        const string content = """
            fn add(a, b) => a.GetDouble() + b.GetDouble();
            fn negate(x) => -x.GetDouble();
            """;

        IReadOnlyList<CustomFunction> fns = JfnParser.Parse(content);

        Assert.Equal(2, fns.Count);
        Assert.Equal("add", fns[0].Name);
        Assert.Equal("negate", fns[1].Name);
    }

    [Fact]
    public void Parse_CommentsAndBlankLines_AreIgnored()
    {
        const string content = """
            // This is a comment

            fn identity(x) => x;

            // Another comment
            """;

        IReadOnlyList<CustomFunction> fns = JfnParser.Parse(content);

        Assert.Single(fns);
        Assert.Equal("identity", fns[0].Name);
    }

    [Fact]
    public void Parse_NoParameters_ReturnsEmptyArray()
    {
        const string content = """
            fn constant() => JsonElement.Undefined;
            """;

        IReadOnlyList<CustomFunction> fns = JfnParser.Parse(content);

        Assert.Single(fns);
        Assert.Empty(fns[0].Parameters);
    }

    [Fact]
    public void Parse_InvalidSyntax_Throws()
    {
        const string content = "not a function";

        Assert.Throws<FormatException>(() => JfnParser.Parse(content));
    }

    [Fact]
    public void Parse_MissingParens_Throws()
    {
        const string content = "fn noparens => x;";

        Assert.Throws<FormatException>(() => JfnParser.Parse(content));
    }

    [Fact]
    public void Parse_BlockFormBraceOnSameLine_ParsesCorrectly()
    {
        const string content = """
            fn test(x) {
                return x;
            }
            """;

        IReadOnlyList<CustomFunction> fns = JfnParser.Parse(content);

        Assert.Single(fns);
        Assert.Equal("test", fns[0].Name);
        Assert.False(fns[0].IsExpression);
        Assert.Contains("return x;", fns[0].Body);
    }

    // ═══════════════════════════════════════════════════════════
    // Coverage: error paths and edge cases (L77-204)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void Parse_MissingFunctionName_Throws()
    {
        // L76-78: empty function name
        const string content = "fn (x) => x;";
        Assert.Throws<FormatException>(() => JfnParser.Parse(content));
    }

    [Fact]
    public void Parse_MissingClosingParen_Throws()
    {
        // L82-84: no closing ')'
        const string content = "fn name(x => x;";
        Assert.Throws<FormatException>(() => JfnParser.Parse(content));
    }

    [Fact]
    public void Parse_EmptyExpressionBody_Throws()
    {
        // L103-105: empty body after =>
        const string content = "fn name() => ;";
        Assert.Throws<FormatException>(() => JfnParser.Parse(content));
    }

    [Fact]
    public void Parse_InvalidAfterSignature_Throws()
    {
        // L185-187: no => or { after function signature
        const string content = "fn name() invalid;";
        Assert.Throws<FormatException>(() => JfnParser.Parse(content));
    }

    [Fact]
    public void Parse_EofAfterSignature_Throws()
    {
        // L142-145: EOF before finding opening brace
        const string content = "fn name()";
        Assert.Throws<FormatException>(() => JfnParser.Parse(content));
    }

    [Fact]
    public void Parse_NonBraceAfterSigNewLine_Throws()
    {
        // L138-139: non-brace, non-blank after separate-line signature
        const string content = "fn name()\ninvalid";
        Assert.Throws<FormatException>(() => JfnParser.Parse(content));
    }

    [Fact]
    public void Parse_UnmatchedBraces_Throws()
    {
        // L176-178: unmatched opening brace
        const string content = "fn name() {\n  x = 1;";
        Assert.Throws<FormatException>(() => JfnParser.Parse(content));
    }

    [Fact]
    public void Parse_EmptyParameterName_Throws()
    {
        // L202-204: empty parameter in comma-separated list
        const string content = "fn name(, x) => x;";
        Assert.Throws<FormatException>(() => JfnParser.Parse(content));
    }

    [Fact]
    public void Parse_BlankLinesBeforeBrace_ParsesCorrectly()
    {
        // L126-129: blank lines and comments between sig and brace
        const string content = "fn test(x)\n\n// comment\n\n{\n  return x;\n}";

        IReadOnlyList<CustomFunction> fns = JfnParser.Parse(content);

        Assert.Single(fns);
        Assert.Equal("test", fns[0].Name);
        Assert.False(fns[0].IsExpression);
        Assert.Contains("return x;", fns[0].Body);
    }

    [Fact]
    public void Parse_NestedBraces_ParsesCorrectly()
    {
        // L158-161: nested braces in body increment/decrement brace depth
        const string content = "fn test(x) {\n  if (true) {\n    x = 1;\n  }\n}";

        IReadOnlyList<CustomFunction> fns = JfnParser.Parse(content);

        Assert.Single(fns);
        Assert.Equal("test", fns[0].Name);
        Assert.False(fns[0].IsExpression);
        Assert.Contains("if (true)", fns[0].Body);
        Assert.Contains("x = 1;", fns[0].Body);
    }
}