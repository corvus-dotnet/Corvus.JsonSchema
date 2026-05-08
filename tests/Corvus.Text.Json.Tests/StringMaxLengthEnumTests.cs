// <copyright file="StringMaxLengthEnumTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests.StringMaxLengthEnumValidation;

/// <summary>
/// Tests that schemas combining type:string + maxLength + enum generate valid code.
/// Regression test for a bug where unescapedUtf8JsonString was declared in the type-check
/// else clause scope but referenced by the enum handler after that scope closed.
/// </summary>
[TestCategory("CodeGen")]
[TestClass]
public class StringMaxLengthEnumValues
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
    [DataRow("\"foo\"")]
    [DataRow("\"bar\"")]
    [DataRow("\"baz\"")]
    public void ValidEnumValueIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("\"qux\"")]
    [DataRow("\"quux\"")]
    public void InvalidEnumValueIsRejected(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void TooLongStringIsRejected()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance("\"toolongvalue\"");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void NonStringIsRejected()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance("42");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\string-maxlength-enum.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"string\", \"maxLength\": 10, \"enum\": [\"foo\", \"bar\", \"baz\"]}",
                "Corvus.Text.Json.Tests.StringMaxLengthEnumValidation",
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
