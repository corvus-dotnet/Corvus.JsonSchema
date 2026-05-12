// <copyright file="AnyOfPartialDiscriminatorValidationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests.AnyOfPartialDiscriminatorValidation;

/// <summary>
/// Tests for anyOf partial discriminator with a non-object first branch (CmakePresets condition pattern).
/// The schema has anyOf: [boolean, object+const, object+const, ...]. The first branch has no properties,
/// so discriminator seeding must try subsequent branches. Object instances dispatch via the discriminator;
/// non-object instances fall through to sequential evaluation.
/// </summary>
[TestCategory("AnyOfDiscriminator")]
[TestClass]
public class AnyOfPartialDiscriminatorWithBooleanFirstBranch
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "anyOf": [
                {
                    "type": "boolean"
                },
                {
                    "type": "object",
                    "properties": { "type": { "const": "const" }, "value": { "type": "string" } },
                    "required": ["type", "value"]
                },
                {
                    "type": "object",
                    "properties": { "type": { "const": "equals" }, "lhs": { "type": "string" }, "rhs": { "type": "string" } },
                    "required": ["type", "lhs", "rhs"]
                },
                {
                    "type": "object",
                    "properties": { "type": { "const": "notEquals" }, "lhs": { "type": "string" }, "rhs": { "type": "string" } },
                    "required": ["type", "lhs", "rhs"]
                },
                {
                    "type": "object",
                    "properties": { "type": { "const": "matches" }, "string": { "type": "string" }, "regex": { "type": "string" } },
                    "required": ["type", "string", "regex"]
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
    [DataRow("true")]
    [DataRow("false")]
    public void BooleanInstanceMatchesUndiscriminatedBranch(string json)
    {
        // Boolean values have no properties, so the discriminator fast path
        // falls through to sequential evaluation which matches branch 0.
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"type": "const", "value": "hello"}""")]
    [DataRow("""{"type": "equals", "lhs": "a", "rhs": "b"}""")]
    [DataRow("""{"type": "notEquals", "lhs": "a", "rhs": "b"}""")]
    [DataRow("""{"type": "matches", "string": "abc", "regex": "^a"}""")]
    public void ObjectWithMatchingDiscriminatorIsAccepted(string json)
    {
        // Object instances are dispatched via the discriminator property "type".
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"type": "const"}""")]
    [DataRow("""{"type": "equals", "lhs": "a"}""")]
    public void MatchingDiscriminatorButMissingRequiredFieldIsRejected(string json)
    {
        // Discriminator selects the right branch, but required fields are missing.
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"type": "unknown"}""")]
    [DataRow("""{"type": ""}""")]
    public void UnrecognizedDiscriminatorValueIsRejected(string json)
    {
        // No branch matches the discriminator value, and the instance is not boolean.
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void NullInstanceIsRejected()
    {
        // null is neither boolean nor object — no branch matches.
        var instance = s_fixture!.DynamicJsonType.ParseInstance("null");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void StringInstanceIsRejected()
    {
        // A string is neither boolean nor object — no branch matches.
        var instance = s_fixture!.DynamicJsonType.ParseInstance("""  "hello"  """);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void NumberInstanceIsRejected()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance("42");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void EmptyObjectIsRejected()
    {
        // Object without discriminator property — falls through to sequential,
        // no branch matches (boolean branch fails for object, object branches
        // require the "type" discriminator).
        var instance = s_fixture!.DynamicJsonType.ParseInstance("{}");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\anyof-partial-discriminator-boolean-first.json",
                Schema,
                "Corvus.Text.Json.Tests.AnyOfPartialDiscriminatorValidation",
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
/// Tests for anyOf partial discriminator with a null first branch.
/// Pattern: anyOf: [null, object+const, object+const].
/// </summary>
[TestCategory("AnyOfDiscriminator")]
[TestClass]
public class AnyOfPartialDiscriminatorWithNullFirstBranch
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
                    "properties": { "kind": { "const": "active" }, "since": { "type": "string" } },
                    "required": ["kind", "since"]
                },
                {
                    "type": "object",
                    "properties": { "kind": { "const": "inactive" }, "reason": { "type": "string" } },
                    "required": ["kind", "reason"]
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
    public void NullInstanceMatchesUndiscriminatedBranch()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance("null");
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"kind": "active", "since": "2024-01-01"}""")]
    [DataRow("""{"kind": "inactive", "reason": "retired"}""")]
    public void ObjectWithMatchingDiscriminatorIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"kind": "unknown"}""")]
    public void UnrecognizedDiscriminatorValueIsRejected(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void BooleanInstanceIsRejected()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance("true");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\anyof-partial-discriminator-null-first.json",
                Schema,
                "Corvus.Text.Json.Tests.AnyOfPartialDiscriminatorValidation",
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
/// Tests for anyOf where the non-object branch is in the MIDDLE, not the first position.
/// Pattern: anyOf: [object+const, boolean, object+const].
/// Ensures discriminator seeding works regardless of branch ordering.
/// </summary>
[TestCategory("AnyOfDiscriminator")]
[TestClass]
public class AnyOfPartialDiscriminatorWithBooleanMiddleBranch
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "anyOf": [
                {
                    "type": "object",
                    "properties": { "mode": { "const": "auto" }, "timeout": { "type": "number" } },
                    "required": ["mode"]
                },
                {
                    "type": "boolean"
                },
                {
                    "type": "object",
                    "properties": { "mode": { "const": "manual" }, "steps": { "type": "array" } },
                    "required": ["mode"]
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
    [DataRow("""{"mode": "auto", "timeout": 30}""")]
    [DataRow("""{"mode": "manual", "steps": [1, 2]}""")]
    public void ObjectWithMatchingDiscriminatorIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("true")]
    [DataRow("false")]
    public void BooleanInstanceMatchesMiddleBranch(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"mode": "unknown"}""")]
    public void UnrecognizedDiscriminatorValueIsRejected(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void NumberInstanceIsRejected()
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
                "tests\\custom\\anyof-partial-discriminator-boolean-middle.json",
                Schema,
                "Corvus.Text.Json.Tests.AnyOfPartialDiscriminatorValidation",
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
/// Negative test: anyOf where partial discrimination should NOT apply because there is only
/// one object branch with a const discriminator (minimum is 2 discriminated branches).
/// </summary>
[TestCategory("AnyOfDiscriminator")]
[TestClass]
public class AnyOfPartialDiscriminatorInsufficientBranches
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "anyOf": [
                {
                    "type": "boolean"
                },
                {
                    "type": "null"
                },
                {
                    "type": "object",
                    "properties": { "kind": { "const": "only" }, "value": { "type": "string" } },
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
    public void BooleanInstanceIsAccepted()
    {
        // Falls through to sequential — boolean branch matches.
        var instance = s_fixture!.DynamicJsonType.ParseInstance("true");
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    public void NullInstanceIsAccepted()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance("null");
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    public void ObjectWithConstIsAccepted()
    {
        // Only one discriminable branch — sequential evaluation finds it.
        var instance = s_fixture!.DynamicJsonType.ParseInstance("""{"kind": "only", "value": "hello"}""");
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    public void NumberInstanceIsRejected()
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
                "tests\\custom\\anyof-partial-discriminator-insufficient.json",
                Schema,
                "Corvus.Text.Json.Tests.AnyOfPartialDiscriminatorValidation",
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
/// Tests for anyOf with many discriminated branches and one non-object branch (triggers hash map).
/// Pattern: anyOf: [boolean, 5 × object+const] — exceeds the MinEnumValuesForHashSet threshold.
/// </summary>
[TestCategory("AnyOfDiscriminator")]
[TestClass]
public class AnyOfPartialDiscriminatorHashMap
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "anyOf": [
                {
                    "type": "boolean"
                },
                {
                    "type": "object",
                    "properties": { "op": { "const": "add" }, "path": { "type": "string" }, "value": {} },
                    "required": ["op", "path", "value"]
                },
                {
                    "type": "object",
                    "properties": { "op": { "const": "remove" }, "path": { "type": "string" } },
                    "required": ["op", "path"]
                },
                {
                    "type": "object",
                    "properties": { "op": { "const": "replace" }, "path": { "type": "string" }, "value": {} },
                    "required": ["op", "path", "value"]
                },
                {
                    "type": "object",
                    "properties": { "op": { "const": "move" }, "from": { "type": "string" }, "path": { "type": "string" } },
                    "required": ["op", "from", "path"]
                },
                {
                    "type": "object",
                    "properties": { "op": { "const": "test" }, "path": { "type": "string" }, "value": {} },
                    "required": ["op", "path", "value"]
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
    [DataRow("true")]
    [DataRow("false")]
    public void BooleanInstanceMatchesUndiscriminatedBranch(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"op": "add", "path": "/foo", "value": 1}""")]
    [DataRow("""{"op": "remove", "path": "/foo"}""")]
    [DataRow("""{"op": "replace", "path": "/foo", "value": 2}""")]
    [DataRow("""{"op": "move", "from": "/old", "path": "/new"}""")]
    [DataRow("""{"op": "test", "path": "/foo", "value": 3}""")]
    public void ObjectWithMatchingDiscriminatorIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"op": "copy"}""")]
    [DataRow("""{"op": "unknown"}""")]
    public void UnrecognizedDiscriminatorValueIsRejected(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void NumberInstanceIsRejected()
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
                "tests\\custom\\anyof-partial-discriminator-hash-map.json",
                Schema,
                "Corvus.Text.Json.Tests.AnyOfPartialDiscriminatorValidation",
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
