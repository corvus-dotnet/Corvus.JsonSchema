// <copyright file="ReservedPropertyNameTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests.ReservedPropertyNameValidation;

/// <summary>
/// Tests that object schemas with properties whose names collide with generated class names
/// (e.g. "jsonSchema", "builder", "source", "mutable") generate valid code.
/// </summary>
[TestCategory("CodeGen")]
[TestClass]
public class ReservedPropertyNameValues
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
    public void ValidInstanceIsAccepted()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(
            "{\"jsonSchema\": \"hello\", \"builder\": \"world\", \"source\": \"foo\", \"mutable\": \"bar\"}");
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    public void PartialInstanceIsAccepted()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(
            "{\"jsonSchema\": \"test\"}");
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    public void EmptyObjectIsAccepted()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance("{}");
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    public void InvalidPropertyTypeIsRejected()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(
            "{\"jsonSchema\": 42}");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\reserved-property-names.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"object\", \"properties\": {\"jsonSchema\": {\"type\": \"string\"}, \"builder\": {\"type\": \"string\"}, \"source\": {\"type\": \"string\"}, \"mutable\": {\"type\": \"string\"}}}",
                "Corvus.Text.Json.Tests.ReservedPropertyNameValidation",
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
