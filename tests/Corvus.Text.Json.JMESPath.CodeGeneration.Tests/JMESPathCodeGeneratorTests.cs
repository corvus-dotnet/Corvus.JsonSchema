// <copyright file="JMESPathCodeGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JMESPath.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JMESPath.CodeGeneration.Tests;

/// <summary>
/// Tests for <see cref="JMESPathCodeGenerator"/>.
/// </summary>
[TestClass]
public class JMESPathCodeGeneratorTests
{
    [TestMethod]
    public void Generate_SimpleProperty_ContainsEvaluateMethod()
    {
        string result = JMESPathCodeGenerator.Generate(
            "foo.bar",
            "SimplePropertyExpr",
            "Test.Generated");

        StringAssert.Contains(result, "public static JsonElement Evaluate(in JsonElement data, JsonWorkspace workspace)");
        StringAssert.Contains(result, "namespace Test.Generated;");
        StringAssert.Contains(result, "class SimplePropertyExpr");
    }

    [TestMethod]
    public void Generate_ListProjection_ContainsArrayBuilder()
    {
        string result = JMESPathCodeGenerator.Generate(
            "people[*].name",
            "ProjectionExpr",
            "Test.Generated");

        StringAssert.Contains(result, "class ProjectionExpr");
        StringAssert.Contains(result, "CreateBuilder");
        StringAssert.Contains(result, "ArrayBuilder");
        StringAssert.Contains(result, "EnumerateArray");
    }

    [TestMethod]
    public void Generate_Literal_ContainsParseValue()
    {
        string result = JMESPathCodeGenerator.Generate(
            "`42`",
            "LiteralExpr",
            "Test.Generated");

        StringAssert.Contains(result, "class LiteralExpr");
        StringAssert.Contains(result, "ParseValue");
    }

    [TestMethod]
    public void Generate_Function_ContainsHelperCall()
    {
        string result = JMESPathCodeGenerator.Generate(
            "length(items)",
            "LengthExpr",
            "Test.Generated");

        StringAssert.Contains(result, "JMESPathCodeGenHelpers.Length");
    }

    [TestMethod]
    public void Generate_SortBy_EmitsExprRefMethod()
    {
        string result = JMESPathCodeGenerator.Generate(
            "sort_by(items, &age)",
            "SortByExpr",
            "Test.Generated");

        StringAssert.Contains(result, "ExprRef0");
        StringAssert.Contains(result, "JMESPathCodeGenHelpers.SortBy");
    }

    [TestMethod]
    public void Generate_Filter_ContainsIsTruthy()
    {
        string result = JMESPathCodeGenerator.Generate(
            "items[?active]",
            "FilterExpr",
            "Test.Generated");

        StringAssert.Contains(result, "IsTruthy");
    }

    [TestMethod]
    public void Generate_Comparison_ContainsDeepEquals()
    {
        string result = JMESPathCodeGenerator.Generate(
            "items[?name == 'test']",
            "CompareExpr",
            "Test.Generated");

        StringAssert.Contains(result, "DeepEquals");
    }

    [TestMethod]
    public void Generate_MultiSelectHash_ContainsObjectBuilder()
    {
        string result = JMESPathCodeGenerator.Generate(
            "{a: foo, b: bar}",
            "HashExpr",
            "Test.Generated");

        StringAssert.Contains(result, "CreateBuilder");
        StringAssert.Contains(result, "ObjectBuilder");
        StringAssert.Contains(result, "AddProperty");
    }

    [TestMethod]
    public void Generate_SumMultiSelect_InlinesSumWithoutArray()
    {
        string result = JMESPathCodeGenerator.Generate(
            "sum([a, b, c])",
            "SumInlineExpr",
            "Test.Generated");

        // Should inline the sum as double accumulation, not create an array.
        StringAssert.Contains(result, "__sum_");
        StringAssert.Contains(result, "GetDouble");
        Assert.DoesNotContain("CreateBuilder", result);
    }

    [TestMethod]
    public void Generate_NestedSumMultiSelect_SingleDoubleToElement()
    {
        string result = JMESPathCodeGenerator.Generate(
            "sum([c, sum([b, a])])",
            "NestedSumExpr",
            "Test.Generated");

        // Recursive fusion: only one DoubleToElement for the outermost sum.
        int count = 0;
        int idx = 0;
        while ((idx = result.IndexOf("DoubleToElement", idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += "DoubleToElement".Length;
        }

        Assert.AreEqual(1, count);
        Assert.DoesNotContain("CreateBuilder", result);
    }
}
