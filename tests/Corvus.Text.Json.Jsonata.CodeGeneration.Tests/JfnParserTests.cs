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
}