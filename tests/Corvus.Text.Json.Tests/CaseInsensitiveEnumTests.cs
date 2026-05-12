// <copyright file="CaseInsensitiveEnumTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests.CaseInsensitiveEnumValidation;

/// <summary>
/// Tests that enum schemas with values that differ only by casing (e.g. "Microsoft" and "microsoft")
/// generate valid code with deduplicated EnumValues member names.
/// </summary>
[TestCategory("CodeGen")]
[TestClass]
public class CaseInsensitiveEnumValues
{
    private static Fixture? s_fixture;
    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        s_fixture = new Fixture();
        await s_fixture.InitializeAsync();
    }

    [ClassCleanup]
    public static void ClassCleanupMethod()
    {
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    [DataRow("\"Chromium\"")]
    [DataRow("\"Google\"")]
    [DataRow("\"Microsoft\"")]
    [DataRow("\"chromium\"")]
    [DataRow("\"google\"")]
    [DataRow("\"microsoft\"")]
    public void ValidEnumValueIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("\"CHROMIUM\"")]
    [DataRow("\"GOOGLE\"")]
    [DataRow("\"MICROSOFT\"")]
    [DataRow("\"Chrome\"")]
    [DataRow("\"unknown\"")]
    [DataRow("\"\"")]
    public void InvalidStringValueIsRejected(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("42")]
    [DataRow("true")]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("{}")]
    public void NonStringValueIsRejected(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\case-insensitive-enum.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"string\", \"enum\": [\"Chromium\", \"Google\", \"Microsoft\", \"chromium\", \"google\", \"microsoft\"]}",
                "Corvus.Text.Json.Tests.CaseInsensitiveEnumValidation",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
