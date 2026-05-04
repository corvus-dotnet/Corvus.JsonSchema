// <copyright file="DocumentationExtensionsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.CodeGeneration;
using CG = Corvus.Json.CodeGeneration.CodeGenerator;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests for documentation-related code generator extensions,
/// exercising the generic-bracket-to-brace conversion in AppendTypeAsSeeCref.
/// </summary>
public static class DocumentationExtensionsTests
{
    private static CG CreateGenerator()
    {
        return new CG(CSharpLanguageProvider.Default, CancellationToken.None);
    }

    [Fact]
    public static void AppendTypeAsSeeCref_NonGenericType_AppendsDirectly()
    {
        CG gen = CreateGenerator();
        gen.AppendTypeAsSeeCref("JsonElement");
        string result = gen.ToString();
        Assert.Contains("<see cref=\"JsonElement\"/>", result);
    }

    [Fact]
    public static void AppendTypeAsSeeCref_GenericType_ReplacesAngleBrackets()
    {
        CG gen = CreateGenerator();
        gen.AppendTypeAsSeeCref("IList<string>");
        string result = gen.ToString();
        Assert.Contains("<see cref=\"IList{string}\"/>", result);
    }

    [Fact]
    public static void AppendTypeAsSeeCref_NestedGenericType_ReplacesAllBrackets()
    {
        CG gen = CreateGenerator();
        gen.AppendTypeAsSeeCref("Dictionary<string, List<int>>");
        string result = gen.ToString();
        Assert.Contains("<see cref=\"Dictionary{string, List{int}}\"/>", result);
    }

    [Fact]
    public static void AppendExample_AppendsCodeFormatted()
    {
        CG gen = CreateGenerator();
        gen.AppendExample("{\"type\": \"string\"}");
        string result = gen.ToString();
        Assert.Contains("/// <example>", result);
        Assert.Contains("/// </example>", result);
    }

    [Fact]
    public static void AppendExamples_AppendsMultipleExamples()
    {
        CG gen = CreateGenerator();
        gen.AppendExamples(["\"hello\"", "42"]);
        string result = gen.ToString();
        Assert.Contains("/// <para>", result);
        Assert.Contains("/// Examples:", result);
        Assert.Contains("/// </para>", result);
    }

    [Fact]
    public static void AppendRemarks_WithLongDocumentation_AppendsRemarksSection()
    {
        CG gen = CreateGenerator();
        gen.AppendRemarks("This is a detailed description.", null);
        string result = gen.ToString();
        Assert.Contains("/// <remarks>", result);
        Assert.Contains("/// </remarks>", result);
    }

    [Fact]
    public static void AppendRemarks_WithExamples_AppendsRemarksWithExamples()
    {
        CG gen = CreateGenerator();
        gen.AppendRemarks(null, ["\"test\""]);
        string result = gen.ToString();
        Assert.Contains("/// <remarks>", result);
        Assert.Contains("/// Examples:", result);
    }

    [Fact]
    public static void AppendRemarks_NeitherDocNorExamples_NoOutput()
    {
        CG gen = CreateGenerator();
        gen.AppendRemarks(null, null);
        string result = gen.ToString();
        Assert.DoesNotContain("/// <remarks>", result);
    }

    [Fact]
    public static void AppendSummary_AppendsSummarySection()
    {
        CG gen = CreateGenerator();
        gen.AppendSummary("A simple summary.");
        string result = gen.ToString();
        Assert.Contains("/// <summary>", result);
        Assert.Contains("A simple summary.", result);
        Assert.Contains("/// </summary>", result);
    }
}
