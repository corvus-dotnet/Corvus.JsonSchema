// <copyright file="DependentSchemasEvaluateEarlierPropertiesTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests.DependentSchemasEvaluateEarlierProperties;

/// <summary>
/// unevaluatedProperties sees the properties a dependent schema evaluates, wherever the dependent property is in the
/// object (the V4 engine applied a dependent schema only when its enumeration reached the dependent property).
/// </summary>
[TestCategory("CodeGen")]
[TestClass]
public class DependentSchemasEvaluateEarlierPropertiesValues
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
    public void TestTheDependentPropertyBeforeThePropertyItsSchemaEvaluates()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("""{"foo": 1, "bar": 2}""");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestTheDependentPropertyAfterThePropertyItsSchemaEvaluates()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("""{"bar": 2, "foo": 1}""");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAPropertyNeitherEvaluatesBeforeTheDependentProperty()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("""{"baz": 3, "bar": 2, "foo": 1}""");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithoutTheDependentPropertyItsSchemaDoesNotApply()
    {
        DynamicJsonElement dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("""{"bar": 2}""");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\dependent-schemas-evaluate-earlier-properties.json",
                """
                {
                    "$schema": "https://json-schema.org/draft/2020-12/schema",
                    "properties": {
                        "foo": true
                    },
                    "dependentSchemas": {
                        "foo": {
                            "properties": {
                                "bar": true
                            }
                        }
                    },
                    "unevaluatedProperties": false
                }
                """,
                "Corvus.Text.Json.Tests.DependentSchemasEvaluateEarlierProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                optionalAsNullable: true,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}