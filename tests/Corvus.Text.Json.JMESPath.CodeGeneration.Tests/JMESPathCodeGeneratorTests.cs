// <copyright file="JMESPathCodeGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JMESPath.CodeGeneration;
using Xunit;

namespace Corvus.Text.Json.JMESPath.CodeGeneration.Tests;

/// <summary>
/// Tests for <see cref="JMESPathCodeGenerator"/>.
/// </summary>
public class JMESPathCodeGeneratorTests
{
    [Fact]
    public void Generate_SimpleProperty_ContainsEvaluateMethod()
    {
        string result = JMESPathCodeGenerator.Generate(
            "foo.bar",
            "SimplePropertyExpr",
            "Test.Generated");

        Assert.Contains("public static JsonElement Evaluate(in JsonElement data, JsonWorkspace workspace)", result);
        Assert.Contains("namespace Test.Generated;", result);
        Assert.Contains("class SimplePropertyExpr", result);
    }

    [Fact]
    public void Generate_ListProjection_ContainsArrayBuilder()
    {
        string result = JMESPathCodeGenerator.Generate(
            "people[*].name",
            "ProjectionExpr",
            "Test.Generated");

        Assert.Contains("class ProjectionExpr", result);
        Assert.Contains("CreateBuilder", result);
        Assert.Contains("ArrayBuilder", result);
        Assert.Contains("EnumerateArray", result);
    }

    [Fact]
    public void Generate_Literal_ContainsParseValue()
    {
        string result = JMESPathCodeGenerator.Generate(
            "`42`",
            "LiteralExpr",
            "Test.Generated");

        Assert.Contains("class LiteralExpr", result);
        Assert.Contains("ParseValue", result);
    }

    [Fact]
    public void Generate_Function_ContainsHelperCall()
    {
        string result = JMESPathCodeGenerator.Generate(
            "length(items)",
            "LengthExpr",
            "Test.Generated");

        Assert.Contains("JMESPathCodeGenHelpers.Length", result);
    }

    [Fact]
    public void Generate_SortBy_EmitsExprRefMethod()
    {
        string result = JMESPathCodeGenerator.Generate(
            "sort_by(items, &age)",
            "SortByExpr",
            "Test.Generated");

        Assert.Contains("ExprRef0", result);
        Assert.Contains("JMESPathCodeGenHelpers.SortBy", result);
    }

    [Fact]
    public void Generate_Filter_ContainsIsTruthy()
    {
        string result = JMESPathCodeGenerator.Generate(
            "items[?active]",
            "FilterExpr",
            "Test.Generated");

        Assert.Contains("IsTruthy", result);
    }

    [Fact]
    public void Generate_Comparison_ContainsDeepEquals()
    {
        string result = JMESPathCodeGenerator.Generate(
            "items[?name == 'test']",
            "CompareExpr",
            "Test.Generated");

        Assert.Contains("DeepEquals", result);
    }

    [Fact]
    public void Generate_MultiSelectHash_ContainsObjectBuilder()
    {
        string result = JMESPathCodeGenerator.Generate(
            "{a: foo, b: bar}",
            "HashExpr",
            "Test.Generated");

        Assert.Contains("CreateBuilder", result);
        Assert.Contains("ObjectBuilder", result);
        Assert.Contains("AddProperty", result);
    }

    [Fact]
    public void Generate_SumMultiSelect_InlinesSumWithoutArray()
    {
        string result = JMESPathCodeGenerator.Generate(
            "sum([a, b, c])",
            "SumInlineExpr",
            "Test.Generated");

        // Should inline the sum as double accumulation, not create an array.
        Assert.Contains("__sum_", result);
        Assert.Contains("GetDouble", result);
        Assert.DoesNotContain("CreateBuilder", result);
    }

    [Fact]
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

        Assert.Equal(1, count);
        Assert.DoesNotContain("CreateBuilder", result);
    }
}
