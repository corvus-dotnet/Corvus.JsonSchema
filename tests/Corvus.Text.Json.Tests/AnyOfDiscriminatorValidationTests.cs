// <copyright file="AnyOfDiscriminatorValidationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests.AnyOfDiscriminatorValidation;

/// <summary>
/// Tests for anyOf discriminator-based short-circuiting with 5 branches (triggers EnumStringMap hash-based dispatch).
/// </summary>
[TestCategory("AnyOfDiscriminator")]
[TestClass]
public class AnyOfDiscriminatorWith5Branches
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "anyOf": [
                {
                    "type": "object",
                    "properties": { "kind": { "enum": ["alpha"] }, "value": { "type": "number" } },
                    "required": ["kind"]
                },
                {
                    "type": "object",
                    "properties": { "kind": { "enum": ["bravo"] }, "value": { "type": "string" } },
                    "required": ["kind"]
                },
                {
                    "type": "object",
                    "properties": { "kind": { "enum": ["charlie"] }, "value": { "type": "boolean" } },
                    "required": ["kind"]
                },
                {
                    "type": "object",
                    "properties": { "kind": { "enum": ["delta"] }, "value": { "type": "array" } },
                    "required": ["kind"]
                },
                {
                    "type": "object",
                    "properties": { "kind": { "enum": ["echo"] }, "value": { "type": "object" } },
                    "required": ["kind"]
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
    [DataRow("""{"kind": "alpha", "value": 42}""")]
    [DataRow("""{"kind": "bravo", "value": "hello"}""")]
    [DataRow("""{"kind": "charlie", "value": true}""")]
    [DataRow("""{"kind": "delta", "value": [1, 2]}""")]
    [DataRow("""{"kind": "echo", "value": {"x": 1}}""")]
    public void MatchingDiscriminatorAndBranchIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"kind": "alpha", "value": "wrong"}""")]
    [DataRow("""{"kind": "bravo", "value": 42}""")]
    public void MatchingDiscriminatorButFailingBranchIsRejected(string json)
    {
        // The discriminator selects the right branch, but the branch validation fails
        // because 'value' has the wrong type. With discriminator dispatch, only the
        // matching branch is evaluated, and it fails → anyOf fails.
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"kind": "unknown"}""")]
    [DataRow("""{"kind": "ALPHA"}""")]
    [DataRow("""{"kind": ""}""")]
    public void UnrecognizedDiscriminatorValueIsRejected(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void MissingDiscriminatorPropertyIsRejected()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance("""{"value": 42}""");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void NonStringDiscriminatorValueIsRejected()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance("""{"kind": 123}""");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void NonObjectInstanceIsRejected()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance("""42""");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void EmptyObjectIsRejected()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance("""{}""");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\anyof-discriminator-5-branches.json",
                Schema,
                "Corvus.Text.Json.Tests.AnyOfDiscriminatorValidation",
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
/// Tests for anyOf discriminator-based short-circuiting with 3 branches (uses SequenceEqual, below hash threshold).
/// </summary>
[TestCategory("AnyOfDiscriminator")]
[TestClass]
public class AnyOfDiscriminatorWith3Branches
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "anyOf": [
                {
                    "type": "object",
                    "properties": { "type": { "const": "point" }, "x": { "type": "number" }, "y": { "type": "number" } },
                    "required": ["type", "x", "y"]
                },
                {
                    "type": "object",
                    "properties": { "type": { "const": "line" }, "start": { "type": "object" }, "end": { "type": "object" } },
                    "required": ["type", "start", "end"]
                },
                {
                    "type": "object",
                    "properties": { "type": { "const": "circle" }, "center": { "type": "object" }, "radius": { "type": "number" } },
                    "required": ["type", "center", "radius"]
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
    [DataRow("""{"type": "point", "x": 1.0, "y": 2.0}""")]
    [DataRow("""{"type": "line", "start": {}, "end": {}}""")]
    [DataRow("""{"type": "circle", "center": {}, "radius": 5.0}""")]
    public void MatchingDiscriminatorAndBranchIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"type": "point"}""")]
    public void MatchingDiscriminatorButMissingRequiredFieldIsRejected(string json)
    {
        // Discriminator matches "point" branch, but x and y are missing
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"type": "rectangle"}""")]
    [DataRow("""{"type": ""}""")]
    public void UnrecognizedDiscriminatorValueIsRejected(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void MissingDiscriminatorPropertyIsRejected()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance("""{"x": 1, "y": 2}""");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\anyof-discriminator-3-branches.json",
                Schema,
                "Corvus.Text.Json.Tests.AnyOfDiscriminatorValidation",
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
/// Tests for anyOf with 2 branches (minimum discriminator case).
/// </summary>
[TestCategory("AnyOfDiscriminator")]
[TestClass]
public class AnyOfDiscriminatorWith2Branches
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "anyOf": [
                {
                    "type": "object",
                    "properties": { "status": { "enum": ["active"] }, "since": { "type": "string" } },
                    "required": ["status"]
                },
                {
                    "type": "object",
                    "properties": { "status": { "enum": ["inactive"] }, "reason": { "type": "string" } },
                    "required": ["status"]
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
    [DataRow("""{"status": "active", "since": "2024-01-01"}""")]
    [DataRow("""{"status": "inactive", "reason": "retired"}""")]
    public void MatchingDiscriminatorIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"status": "pending"}""")]
    [DataRow("""{"status": ""}""")]
    public void UnrecognizedDiscriminatorValueIsRejected(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void MissingDiscriminatorIsRejected()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance("""{"since": "2024-01-01"}""");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\anyof-discriminator-2-branches.json",
                Schema,
                "Corvus.Text.Json.Tests.AnyOfDiscriminatorValidation",
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
/// Tests for anyOf with non-required discriminator properties.
/// The discriminator fast path still applies but falls through to sequential
/// when the discriminator property is absent from the instance.
/// </summary>
[TestCategory("AnyOfDiscriminator")]
[TestClass]
public class AnyOfWithNonRequiredDiscriminator
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "anyOf": [
                {
                    "type": "object",
                    "properties": { "kind": { "enum": ["alpha"] }, "value": { "type": "number" } }
                },
                {
                    "type": "object",
                    "properties": { "kind": { "enum": ["bravo"] }, "value": { "type": "string" } }
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
    [DataRow("""{"kind": "alpha", "value": 42}""")]
    [DataRow("""{"kind": "bravo", "value": "hello"}""")]
    public void MatchingBranchIsAccepted(string json)
    {
        // With non-required discriminator, fast path dispatches when property is present.
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    public void MissingDiscriminatorFallsThroughToSequential()
    {
        // Without 'kind' present, discriminator fast path falls through to sequential.
        // {"value": 42} matches branch 0 (value is number).
        // anyOf: at least one match → pass.
        var instance = s_fixture!.DynamicJsonType.ParseInstance("""{"value": 42}""");
        Assert.IsTrue(instance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\anyof-no-discriminator.json",
                Schema,
                "Corvus.Text.Json.Tests.AnyOfDiscriminatorValidation",
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
