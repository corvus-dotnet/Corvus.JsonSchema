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
[TestClass]
    public class DocumentationExtensionsTests
{
    private static CG CreateGenerator()
    {
        return new CG(CSharpLanguageProvider.Default, CancellationToken.None);
    }

    [TestMethod]
    public void AppendTypeAsSeeCref_NonGenericType_AppendsDirectly()
    {
        CG gen = CreateGenerator();
        gen.AppendTypeAsSeeCref("JsonElement");
        string result = gen.ToString();
        StringAssert.Contains(result, "<see cref=\"JsonElement\"/>");
    }

    [TestMethod]
    public void AppendTypeAsSeeCref_GenericType_ReplacesAngleBrackets()
    {
        CG gen = CreateGenerator();
        gen.AppendTypeAsSeeCref("IList<string>");
        string result = gen.ToString();
        StringAssert.Contains(result, "<see cref=\"IList{string}\"/>");
    }

    [TestMethod]
    public void AppendTypeAsSeeCref_NestedGenericType_ReplacesAllBrackets()
    {
        CG gen = CreateGenerator();
        gen.AppendTypeAsSeeCref("Dictionary<string, List<int>>");
        string result = gen.ToString();
        StringAssert.Contains(result, "<see cref=\"Dictionary{string, List{int}}\"/>");
    }

    [TestMethod]
    public void AppendExample_AppendsCodeFormatted()
    {
        CG gen = CreateGenerator();
        gen.AppendExample("{\"type\": \"string\"}");
        string result = gen.ToString();
        StringAssert.Contains(result, "/// <example>");
        StringAssert.Contains(result, "/// </example>");
    }

    [TestMethod]
    public void AppendExamples_AppendsMultipleExamples()
    {
        CG gen = CreateGenerator();
        gen.AppendExamples(["\"hello\"", "42"]);
        string result = gen.ToString();
        StringAssert.Contains(result, "/// <para>");
        StringAssert.Contains(result, "/// Examples:");
        StringAssert.Contains(result, "/// </para>");
    }

    [TestMethod]
    public void AppendRemarks_WithLongDocumentation_AppendsRemarksSection()
    {
        CG gen = CreateGenerator();
        gen.AppendRemarks("This is a detailed description.", null);
        string result = gen.ToString();
        StringAssert.Contains(result, "/// <remarks>");
        StringAssert.Contains(result, "/// </remarks>");
    }

    [TestMethod]
    public void AppendRemarks_WithExamples_AppendsRemarksWithExamples()
    {
        CG gen = CreateGenerator();
        gen.AppendRemarks(null, ["\"test\""]);
        string result = gen.ToString();
        StringAssert.Contains(result, "/// <remarks>");
        StringAssert.Contains(result, "/// Examples:");
    }

    [TestMethod]
    public void AppendRemarks_NeitherDocNorExamples_NoOutput()
    {
        CG gen = CreateGenerator();
        gen.AppendRemarks(null, null);
        string result = gen.ToString();
        Assert.DoesNotContain("/// <remarks>", result);
    }

    [TestMethod]
    public void AppendSummary_AppendsSummarySection()
    {
        CG gen = CreateGenerator();
        gen.AppendSummary("A simple summary.");
        string result = gen.ToString();
        StringAssert.Contains(result, "/// <summary>");
        StringAssert.Contains(result, "A simple summary.");
        StringAssert.Contains(result, "/// </summary>");
    }
}
