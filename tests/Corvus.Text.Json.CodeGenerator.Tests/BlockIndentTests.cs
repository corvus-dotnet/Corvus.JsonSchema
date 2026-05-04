// <copyright file="BlockIndentTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.CodeGeneration;
using CG = Corvus.Json.CodeGeneration.CodeGenerator;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests for AppendBlockIndent, AppendBlockIndentWithHashOutdent,
/// AppendParagraphs, and ConditionallyAppend extension methods.
/// </summary>
public static class BlockIndentTests
{
    private static CG CreateGenerator()
    {
        return new CG(CSharpLanguageProvider.Default, CancellationToken.None);
    }

    #region AppendBlockIndent

    [Fact]
    public static void AppendBlockIndent_SingleLine_AppendsWithIndent()
    {
        CG gen = CreateGenerator();
        gen.PushIndent();
        gen.AppendBlockIndent("hello");
        string result = gen.ToString();
        Assert.Contains("hello", result);
    }

    [Fact]
    public static void AppendBlockIndent_MultiLine_AppendsAllLines()
    {
        CG gen = CreateGenerator();
        gen.AppendBlockIndent("line1\nline2\nline3");
        string result = gen.ToString();
        Assert.Contains("line1", result);
        Assert.Contains("line2", result);
        Assert.Contains("line3", result);
    }

    [Fact]
    public static void AppendBlockIndent_OmitLastLineEnd_DoesNotAppendNewlineAfterLast()
    {
        CG gen = CreateGenerator();
        gen.AppendBlockIndent("line1\nline2", omitLastLineEnd: true);
        string result = gen.ToString();
        Assert.Contains("line1", result);
        Assert.Contains("line2", result);
        // The last line should not end with a newline (it uses AppendIndent not AppendLineIndent)
        Assert.False(result.EndsWith("\n", StringComparison.Ordinal));
    }

    [Fact]
    public static void AppendBlockIndent_WindowsLineEndings_NormalizedCorrectly()
    {
        CG gen = CreateGenerator();
        gen.AppendBlockIndent("line1\r\nline2\r\nline3");
        string result = gen.ToString();
        Assert.Contains("line1", result);
        Assert.Contains("line2", result);
        Assert.Contains("line3", result);
    }

    #endregion

    #region AppendBlockIndentWithHashOutdent

    [Fact]
    public static void AppendBlockIndentWithHashOutdent_RegularLines_AppendsWithIndent()
    {
        CG gen = CreateGenerator();
        gen.PushIndent();
        gen.AppendBlockIndentWithHashOutdent("regular line\nanother line");
        string result = gen.ToString();
        Assert.Contains("regular line", result);
        Assert.Contains("another line", result);
    }

    [Fact]
    public static void AppendBlockIndentWithHashOutdent_HashLine_AppendsWithoutIndent()
    {
        CG gen = CreateGenerator();
        gen.PushIndent();
        gen.AppendBlockIndentWithHashOutdent("#if NET9_0_OR_GREATER\ncode here\n#endif");
        string result = gen.ToString();
        // Hash lines should not be indented
        Assert.Contains("#if NET9_0_OR_GREATER", result);
        Assert.Contains("code here", result);
        Assert.Contains("#endif", result);
    }

    [Fact]
    public static void AppendBlockIndentWithHashOutdent_OmitLastLineEnd_HashLast()
    {
        CG gen = CreateGenerator();
        gen.PushIndent();
        gen.AppendBlockIndentWithHashOutdent("code\n#endif", omitLastLineEnd: true);
        string result = gen.ToString();
        Assert.Contains("code", result);
        Assert.Contains("#endif", result);
        Assert.False(result.EndsWith("\n", StringComparison.Ordinal));
    }

    [Fact]
    public static void AppendBlockIndentWithHashOutdent_OmitLastLineEnd_RegularLast()
    {
        CG gen = CreateGenerator();
        gen.PushIndent();
        gen.AppendBlockIndentWithHashOutdent("#if DEBUG\nlastline", omitLastLineEnd: true);
        string result = gen.ToString();
        Assert.Contains("#if DEBUG", result);
        Assert.Contains("lastline", result);
        Assert.False(result.EndsWith("\n", StringComparison.Ordinal));
    }

    #endregion

    #region AppendParagraphs

    [Fact]
    public static void AppendParagraphs_SingleParagraph_AppendsPara()
    {
        CG gen = CreateGenerator();
        gen.AppendParagraphs("A single paragraph.");
        string result = gen.ToString();
        Assert.Contains("/// <para>", result);
        Assert.Contains("A single paragraph.", result);
        Assert.Contains("/// </para>", result);
    }

    [Fact]
    public static void AppendParagraphs_MultipleParagraphs_SplitsOnBlankLines()
    {
        CG gen = CreateGenerator();
        gen.AppendParagraphs("First paragraph.\n\nSecond paragraph.");
        string result = gen.ToString();
        // Should have two <para> sections
        int paraCount = result.Split("/// <para>").Length - 1;
        Assert.True(paraCount >= 2, $"Expected at least 2 paragraphs, got {paraCount}");
    }

    #endregion

    #region ConditionallyAppend

    [Fact]
    public static void ConditionallyAppend_ConditionTrue_ExecutesAppend()
    {
        CG gen = CreateGenerator();
        gen.ConditionallyAppend(true, g => g.Append("appended"));
        string result = gen.ToString();
        Assert.Contains("appended", result);
    }

    [Fact]
    public static void ConditionallyAppend_ConditionFalse_DoesNotAppend()
    {
        CG gen = CreateGenerator();
        gen.ConditionallyAppend(false, g => g.Append("not-here"));
        string result = gen.ToString();
        Assert.DoesNotContain("not-here", result);
    }

    #endregion

    #region AppendAutoGeneratedHeader

    [Fact]
    public static void AppendAutoGeneratedHeader_AppendsAutoGenComment()
    {
        CG gen = CreateGenerator();
        gen.AppendAutoGeneratedHeader();
        string result = gen.ToString();
        Assert.Contains("auto-generated", result, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region AppendUsings

    [Fact]
    public static void AppendUsings_AppendsUsingStatements()
    {
        CG gen = CreateGenerator();
        gen.AppendUsings(
            new ConditionalCodeSpecification("System"),
            new ConditionalCodeSpecification("System.Text.Json"));
        string result = gen.ToString();
        Assert.Contains("using System;", result);
        Assert.Contains("using System.Text.Json;", result);
    }

    #endregion

    #region AppendParameterList

    [Fact]
    public static void AppendParameterList_LessThan3_SingleLine()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        gen.AppendParameterList(
            new MethodParameter("", "int", "a"),
            new MethodParameter("", "string", "b"));
        string result = gen.ToString();
        Assert.Contains("int a", result);
        Assert.Contains("string b", result);
    }

    [Fact]
    public static void AppendParameterList_3OrMore_MultiLine()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        gen.AppendParameterList(
            new MethodParameter("", "int", "a"),
            new MethodParameter("", "string", "b"),
            new MethodParameter("ref", "bool", "c"));
        string result = gen.ToString();
        Assert.Contains("int a", result);
        Assert.Contains("string b", result);
        Assert.Contains("ref bool c", result);
    }

    [Fact]
    public static void AppendParameterListSingleLine_Empty_EmitsBrackets()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        CodeGeneratorExtensions.AppendParameterListSingleLine(gen, []);
        string result = gen.ToString();
        Assert.Contains("()", result);
    }

    [Fact]
    public static void AppendParameterListIndent_Empty_EmitsBrackets()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        CodeGeneratorExtensions.AppendParameterListIndent(gen, []);
        string result = gen.ToString();
        Assert.Contains("()", result);
    }

    [Fact]
    public static void AppendParameterIndent_NullableType_AppendsQuestionMark()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var param = new MethodParameter("", "string", "value", typeIsNullable: true);
        gen.AppendParameterIndent(param);
        string result = gen.ToString();
        Assert.Contains("string?", result);
        Assert.Contains("value", result);
    }

    [Fact]
    public static void AppendParameter_NullableType_AppendsQuestionMark()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var param = new MethodParameter("", "object", "item", typeIsNullable: true);
        gen.AppendParameter(param);
        string result = gen.ToString();
        Assert.Contains("object?", result);
        Assert.Contains("item", result);
    }

    [Fact]
    public static void AppendParameter_WithDefaultValue_AppendsDefault()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var param = new MethodParameter("", "int", "count", defaultValue: "0");
        gen.AppendParameter(param);
        string result = gen.ToString();
        Assert.Contains("int count = 0", result);
    }

    [Fact]
    public static void AppendParameterIndent_WithModifiers_AppendsModifiers()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var param = new MethodParameter("ref readonly", "Span<byte>", "buffer");
        gen.AppendParameterIndent(param);
        string result = gen.ToString();
        Assert.Contains("ref readonly", result);
        Assert.Contains("Span<byte>", result);
    }

    #endregion
}
