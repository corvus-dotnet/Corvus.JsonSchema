// <copyright file="OneOfNumericDiscriminatorValidationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests.OneOfNumericDiscriminatorValidation;

/// <summary>
/// Tests for oneOf discriminator with integer const values (CmakePresets version pattern).
/// </summary>
[TestCategory("OneOfDiscriminator")]
[TestClass]
public class OneOfNumericDiscriminatorBasic
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "oneOf": [
                {
                    "type": "object",
                    "properties": { "version": { "const": 1 }, "name": { "type": "string" } },
                    "required": ["version", "name"]
                },
                {
                    "type": "object",
                    "properties": { "version": { "const": 2 }, "name": { "type": "string" }, "extra": { "type": "string" } },
                    "required": ["version", "name"]
                },
                {
                    "type": "object",
                    "properties": { "version": { "const": 3 }, "data": { "type": "object" } },
                    "required": ["version"]
                }
            ]
        }
        """;

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
    [DataRow("""{"version": 1, "name": "hello"}""")]
    [DataRow("""{"version": 2, "name": "world"}""")]
    [DataRow("""{"version": 2, "name": "world", "extra": "data"}""")]
    [DataRow("""{"version": 3}""")]
    [DataRow("""{"version": 3, "data": {}}""")]
    public void ValidInstanceIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"version": 4, "name": "invalid"}""")]
    [DataRow("""{"version": 1}""")]
    [DataRow("""{"name": "no version"}""")]
    [DataRow("""{"version": "1", "name": "string version"}""")]
    public void InvalidInstanceIsRejected(string json)
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
                "tests\\custom\\oneof-numeric-discriminator-basic.json",
                Schema,
                "Corvus.Text.Json.Tests.OneOfNumericDiscriminatorValidation",
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

/// <summary>
/// Tests for anyOf discriminator with integer const values.
/// </summary>
[TestCategory("AnyOfDiscriminator")]
[TestClass]
public class AnyOfNumericDiscriminatorBasic
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "anyOf": [
                {
                    "type": "object",
                    "properties": { "level": { "const": 1 }, "name": { "type": "string" } },
                    "required": ["level", "name"]
                },
                {
                    "type": "object",
                    "properties": { "level": { "const": 2 }, "items": { "type": "array" } },
                    "required": ["level"]
                },
                {
                    "type": "object",
                    "properties": { "level": { "const": 3 } },
                    "required": ["level"]
                }
            ]
        }
        """;

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
    [DataRow("""{"level": 1, "name": "hello"}""")]
    [DataRow("""{"level": 2}""")]
    [DataRow("""{"level": 2, "items": []}""")]
    [DataRow("""{"level": 3}""")]
    public void ValidInstanceIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"level": 4}""")]
    [DataRow("""{"level": 1}""")]
    [DataRow("""{"name": "no level"}""")]
    public void InvalidInstanceIsRejected(string json)
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
                "tests\\custom\\anyof-numeric-discriminator-basic.json",
                Schema,
                "Corvus.Text.Json.Tests.OneOfNumericDiscriminatorValidation",
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

/// <summary>
/// Tests for oneOf discriminator with negative and large number const values.
/// </summary>
[TestCategory("OneOfDiscriminator")]
[TestClass]
public class OneOfNumericDiscriminatorWithNonTrivialNumbers
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "oneOf": [
                {
                    "type": "object",
                    "properties": { "id": { "const": -1 }, "desc": { "type": "string" } },
                    "required": ["id"]
                },
                {
                    "type": "object",
                    "properties": { "id": { "const": 0 }, "desc": { "type": "string" } },
                    "required": ["id"]
                },
                {
                    "type": "object",
                    "properties": { "id": { "const": 100 }, "desc": { "type": "string" } },
                    "required": ["id"]
                }
            ]
        }
        """;

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
    [DataRow("""{"id": -1}""")]
    [DataRow("""{"id": -1, "desc": "sentinel"}""")]
    [DataRow("""{"id": 0}""")]
    [DataRow("""{"id": 100, "desc": "big"}""")]
    public void ValidInstanceIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"id": 1}""")]
    [DataRow("""{"id": -2}""")]
    [DataRow("""{"id": "string"}""")]
    public void InvalidInstanceIsRejected(string json)
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
                "tests\\custom\\oneof-numeric-discriminator-nontrivial.json",
                Schema,
                "Corvus.Text.Json.Tests.OneOfNumericDiscriminatorValidation",
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

/// <summary>
/// Tests for anyOf partial discriminator combining numeric const with a non-object branch.
/// </summary>
[TestCategory("AnyOfDiscriminator")]
[TestClass]
public class AnyOfPartialNumericDiscriminator
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "anyOf": [
                {
                    "type": "null"
                },
                {
                    "type": "object",
                    "properties": { "version": { "const": 1 }, "name": { "type": "string" } },
                    "required": ["version", "name"]
                },
                {
                    "type": "object",
                    "properties": { "version": { "const": 2 }, "data": { "type": "object" } },
                    "required": ["version"]
                }
            ]
        }
        """;

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
    [DataRow("null")]
    [DataRow("""{"version": 1, "name": "hello"}""")]
    [DataRow("""{"version": 2}""")]
    [DataRow("""{"version": 2, "data": {}}""")]
    public void ValidInstanceIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"version": 3}""")]
    [DataRow("""{"version": 1}""")]
    [DataRow("42")]
    public void InvalidInstanceIsRejected(string json)
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
                "tests\\custom\\anyof-partial-numeric-discriminator.json",
                Schema,
                "Corvus.Text.Json.Tests.OneOfNumericDiscriminatorValidation",
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
