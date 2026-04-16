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
        Assert.Contains("CreateArrayBuilder", result);
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

        Assert.Contains("CreateObjectBuilder", result);
        Assert.Contains("SetProperty", result);
    }
}
