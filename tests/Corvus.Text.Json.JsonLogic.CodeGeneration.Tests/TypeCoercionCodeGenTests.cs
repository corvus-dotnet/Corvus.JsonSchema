// <copyright file="TypeCoercionCodeGenTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonLogic.CodeGeneration;
using Xunit;

namespace Corvus.Text.Json.JsonLogic.CodeGeneration.Tests;

/// <summary>
/// Tests that the code generator produces valid code for the Corvus extension type
/// coercion operators (asDouble, asLong, asBigNumber, asBigInteger) and missing_some.
/// </summary>
public class TypeCoercionCodeGenTests
{
    // ----- asDouble -----

    [Fact]
    public void Generate_AsDouble_FromString_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"asDouble":"42.5"}""",
            "AsDoubleRule",
            "Test.Generated");

        Assert.Contains("class AsDoubleRule", result);
        Assert.Contains("Evaluate", result);
    }

    [Fact]
    public void Generate_AsDouble_FromVar_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"asDouble":{"var":"x"}}""",
            "AsDoubleVarRule",
            "Test.Generated");

        Assert.Contains("class AsDoubleVarRule", result);
        Assert.Contains("\"x\"u8", result);
    }

    [Fact]
    public void Generate_AsDouble_InArithmetic_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"+":[{"asDouble":{"var":"price"}}, 10]}""",
            "AsDoubleArithRule",
            "Test.Generated");

        Assert.Contains("class AsDoubleArithRule", result);
    }

    // ----- asLong -----

    [Fact]
    public void Generate_AsLong_FromNumber_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"asLong":42.9}""",
            "AsLongRule",
            "Test.Generated");

        Assert.Contains("class AsLongRule", result);
    }

    [Fact]
    public void Generate_AsLong_FromString_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"asLong":"100"}""",
            "AsLongStrRule",
            "Test.Generated");

        Assert.Contains("class AsLongStrRule", result);
    }

    // ----- asBigNumber -----

    [Fact]
    public void Generate_AsBigNumber_FromString_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"asBigNumber":"12345678901234567890"}""",
            "AsBigNumberRule",
            "Test.Generated");

        Assert.Contains("class AsBigNumberRule", result);
    }

    [Fact]
    public void Generate_AsBigNumber_FromNumber_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"asBigNumber":42}""",
            "AsBigNumberNumRule",
            "Test.Generated");

        Assert.Contains("class AsBigNumberNumRule", result);
    }

    // ----- asBigInteger -----

    [Fact]
    public void Generate_AsBigInteger_FromString_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"asBigInteger":"12345678901234567890"}""",
            "AsBigIntegerRule",
            "Test.Generated");

        Assert.Contains("class AsBigIntegerRule", result);
    }

    [Fact]
    public void Generate_AsBigInteger_FromNumber_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"asBigInteger":42.9}""",
            "AsBigIntegerNumRule",
            "Test.Generated");

        Assert.Contains("class AsBigIntegerNumRule", result);
    }

    // ----- missing_some -----

    [Fact]
    public void Generate_MissingSome_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"missing_some":[1, ["a","b","c"]]}""",
            "MissingSomeRule",
            "Test.Generated");

        Assert.Contains("class MissingSomeRule", result);
    }

    [Fact]
    public void Generate_MissingSome_WithVar_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"missing_some":[2, ["name","email","phone"]]}""",
            "MissingSomeVarRule",
            "Test.Generated");

        Assert.Contains("class MissingSomeVarRule", result);
    }

    // ----- Coercion in conditional context -----

    [Fact]
    public void Generate_AsDoubleInIf_ProducesValidCode()
    {
        string result = JsonLogicCodeGenerator.Generate(
            """{"if":[{">":[{"asDouble":{"var":"amount"}}, 100]}, "high", "low"]}""",
            "AsDoubleIfRule",
            "Test.Generated");

        Assert.Contains("class AsDoubleIfRule", result);
        Assert.Contains("\"amount\"u8", result);
    }
}
