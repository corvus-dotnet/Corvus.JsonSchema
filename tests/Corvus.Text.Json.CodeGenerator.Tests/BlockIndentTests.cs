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
[TestClass]
    public class BlockIndentTests
{
    private static CG CreateGenerator()
    {
        return new CG(CSharpLanguageProvider.Default, CancellationToken.None);
    }

    #region AppendBlockIndent

    [TestMethod]
    public void AppendBlockIndent_SingleLine_AppendsWithIndent()
    {
        CG gen = CreateGenerator();
        gen.PushIndent();
        gen.AppendBlockIndent("hello");
        string result = gen.ToString();
        StringAssert.Contains(result, "hello");
    }

    [TestMethod]
    public void AppendBlockIndent_MultiLine_AppendsAllLines()
    {
        CG gen = CreateGenerator();
        gen.AppendBlockIndent("line1\nline2\nline3");
        string result = gen.ToString();
        StringAssert.Contains(result, "line1");
        StringAssert.Contains(result, "line2");
        StringAssert.Contains(result, "line3");
    }

    [TestMethod]
    public void AppendBlockIndent_OmitLastLineEnd_DoesNotAppendNewlineAfterLast()
    {
        CG gen = CreateGenerator();
        gen.AppendBlockIndent("line1\nline2", omitLastLineEnd: true);
        string result = gen.ToString();
        StringAssert.Contains(result, "line1");
        StringAssert.Contains(result, "line2");
        // The last line should not end with a newline (it uses AppendIndent not AppendLineIndent)
        Assert.IsFalse(result.EndsWith("\n", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AppendBlockIndent_WindowsLineEndings_NormalizedCorrectly()
    {
        CG gen = CreateGenerator();
        gen.AppendBlockIndent("line1\r\nline2\r\nline3");
        string result = gen.ToString();
        StringAssert.Contains(result, "line1");
        StringAssert.Contains(result, "line2");
        StringAssert.Contains(result, "line3");
    }

    #endregion

    #region AppendBlockIndentWithHashOutdent

    [TestMethod]
    public void AppendBlockIndentWithHashOutdent_RegularLines_AppendsWithIndent()
    {
        CG gen = CreateGenerator();
        gen.PushIndent();
        gen.AppendBlockIndentWithHashOutdent("regular line\nanother line");
        string result = gen.ToString();
        StringAssert.Contains(result, "regular line");
        StringAssert.Contains(result, "another line");
    }

    [TestMethod]
    public void AppendBlockIndentWithHashOutdent_HashLine_AppendsWithoutIndent()
    {
        CG gen = CreateGenerator();
        gen.PushIndent();
        gen.AppendBlockIndentWithHashOutdent("#if NET9_0_OR_GREATER\ncode here\n#endif");
        string result = gen.ToString();
        // Hash lines should not be indented
        StringAssert.Contains(result, "#if NET9_0_OR_GREATER");
        StringAssert.Contains(result, "code here");
        StringAssert.Contains(result, "#endif");
    }

    [TestMethod]
    public void AppendBlockIndentWithHashOutdent_OmitLastLineEnd_HashLast()
    {
        CG gen = CreateGenerator();
        gen.PushIndent();
        gen.AppendBlockIndentWithHashOutdent("code\n#endif", omitLastLineEnd: true);
        string result = gen.ToString();
        StringAssert.Contains(result, "code");
        StringAssert.Contains(result, "#endif");
        Assert.IsFalse(result.EndsWith("\n", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AppendBlockIndentWithHashOutdent_OmitLastLineEnd_RegularLast()
    {
        CG gen = CreateGenerator();
        gen.PushIndent();
        gen.AppendBlockIndentWithHashOutdent("#if DEBUG\nlastline", omitLastLineEnd: true);
        string result = gen.ToString();
        StringAssert.Contains(result, "#if DEBUG");
        StringAssert.Contains(result, "lastline");
        Assert.IsFalse(result.EndsWith("\n", StringComparison.Ordinal));
    }

    #endregion

    #region AppendParagraphs

    [TestMethod]
    public void AppendParagraphs_SingleParagraph_AppendsPara()
    {
        CG gen = CreateGenerator();
        gen.AppendParagraphs("A single paragraph.");
        string result = gen.ToString();
        StringAssert.Contains(result, "/// <para>");
        StringAssert.Contains(result, "A single paragraph.");
        StringAssert.Contains(result, "/// </para>");
    }

    [TestMethod]
    public void AppendParagraphs_MultipleParagraphs_SplitsOnBlankLines()
    {
        CG gen = CreateGenerator();
        gen.AppendParagraphs("First paragraph.\n\nSecond paragraph.");
        string result = gen.ToString();
        // Should have two <para> sections
        int paraCount = result.Split("/// <para>").Length - 1;
        Assert.IsTrue(paraCount >= 2, $"Expected at least 2 paragraphs, got {paraCount}");
    }

    #endregion

    #region ConditionallyAppend

    [TestMethod]
    public void ConditionallyAppend_ConditionTrue_ExecutesAppend()
    {
        CG gen = CreateGenerator();
        gen.ConditionallyAppend(true, g => g.Append("appended"));
        string result = gen.ToString();
        StringAssert.Contains(result, "appended");
    }

    [TestMethod]
    public void ConditionallyAppend_ConditionFalse_DoesNotAppend()
    {
        CG gen = CreateGenerator();
        gen.ConditionallyAppend(false, g => g.Append("not-here"));
        string result = gen.ToString();
        Assert.DoesNotContain("not-here", result);
    }

    #endregion

    #region AppendAutoGeneratedHeader

    [TestMethod]
    public void AppendAutoGeneratedHeader_AppendsAutoGenComment()
    {
        CG gen = CreateGenerator();
        gen.AppendAutoGeneratedHeader();
        string result = gen.ToString();
        StringAssert.Contains(result, "auto-generated", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region AppendUsings

    [TestMethod]
    public void AppendUsings_AppendsUsingStatements()
    {
        CG gen = CreateGenerator();
        gen.AppendUsings(
            new ConditionalCodeSpecification("System"),
            new ConditionalCodeSpecification("System.Text.Json"));
        string result = gen.ToString();
        StringAssert.Contains(result, "using System;");
        StringAssert.Contains(result, "using System.Text.Json;");
    }

    #endregion

    #region AppendParameterList

    [TestMethod]
    public void AppendParameterList_LessThan3_SingleLine()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        gen.AppendParameterList(
            new MethodParameter("", "int", "a"),
            new MethodParameter("", "string", "b"));
        string result = gen.ToString();
        StringAssert.Contains(result, "int a");
        StringAssert.Contains(result, "string b");
    }

    [TestMethod]
    public void AppendParameterList_3OrMore_MultiLine()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        gen.AppendParameterList(
            new MethodParameter("", "int", "a"),
            new MethodParameter("", "string", "b"),
            new MethodParameter("ref", "bool", "c"));
        string result = gen.ToString();
        StringAssert.Contains(result, "int a");
        StringAssert.Contains(result, "string b");
        StringAssert.Contains(result, "ref bool c");
    }

    [TestMethod]
    public void AppendParameterListSingleLine_Empty_EmitsBrackets()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        CodeGeneratorExtensions.AppendParameterListSingleLine(gen, []);
        string result = gen.ToString();
        StringAssert.Contains(result, "()");
    }

    [TestMethod]
    public void AppendParameterListIndent_Empty_EmitsBrackets()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        CodeGeneratorExtensions.AppendParameterListIndent(gen, []);
        string result = gen.ToString();
        StringAssert.Contains(result, "()");
    }

    [TestMethod]
    public void AppendParameterIndent_NullableType_AppendsQuestionMark()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var param = new MethodParameter("", "string", "value", typeIsNullable: true);
        gen.AppendParameterIndent(param);
        string result = gen.ToString();
        StringAssert.Contains(result, "string?");
        StringAssert.Contains(result, "value");
    }

    [TestMethod]
    public void AppendParameter_NullableType_AppendsQuestionMark()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var param = new MethodParameter("", "object", "item", typeIsNullable: true);
        gen.AppendParameter(param);
        string result = gen.ToString();
        StringAssert.Contains(result, "object?");
        StringAssert.Contains(result, "item");
    }

    [TestMethod]
    public void AppendParameter_WithDefaultValue_AppendsDefault()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var param = new MethodParameter("", "int", "count", defaultValue: "0");
        gen.AppendParameter(param);
        string result = gen.ToString();
        StringAssert.Contains(result, "int count = 0");
    }

    [TestMethod]
    public void AppendParameterIndent_WithModifiers_AppendsModifiers()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var param = new MethodParameter("ref readonly", "Span<byte>", "buffer");
        gen.AppendParameterIndent(param);
        string result = gen.ToString();
        StringAssert.Contains(result, "ref readonly");
        StringAssert.Contains(result, "Span<byte>");
    }

    #endregion
}
