// <copyright file="OneOfDiscriminatorValidationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests.OneOfDiscriminatorValidation;

/// <summary>
/// Tests for oneOf discriminator-based short-circuiting with 5 branches (triggers EnumStringMap hash-based dispatch).
/// Schema: oneOf with 5 branches, each requiring a "kind" property with a single distinct enum value.
/// </summary>
[TestCategory("OneOfDiscriminator")]
[TestClass]
public class OneOfDiscriminatorWith5Branches
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "oneOf": [
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
        // matching branch is evaluated, and it fails → oneOf fails.
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
                "tests\\custom\\oneof-discriminator-5-branches.json",
                Schema,
                "Corvus.Text.Json.Tests.OneOfDiscriminatorValidation",
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
/// Tests for oneOf discriminator-based short-circuiting with 3 branches (uses SequenceEqual, below hash threshold).
/// </summary>
[TestCategory("OneOfDiscriminator")]
[TestClass]
public class OneOfDiscriminatorWith3Branches
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "oneOf": [
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
                "tests\\custom\\oneof-discriminator-3-branches.json",
                Schema,
                "Corvus.Text.Json.Tests.OneOfDiscriminatorValidation",
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
/// Tests for oneOf with 2 branches (minimum discriminator case).
/// </summary>
[TestCategory("OneOfDiscriminator")]
[TestClass]
public class OneOfDiscriminatorWith2Branches
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "oneOf": [
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
                "tests\\custom\\oneof-discriminator-2-branches.json",
                Schema,
                "Corvus.Text.Json.Tests.OneOfDiscriminatorValidation",
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
/// Tests for oneOf where discriminator detection should NOT trigger (optional property, not required).
/// </summary>
[TestCategory("OneOfDiscriminator")]
[TestClass]
public class OneOfWithoutDiscriminator
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "oneOf": [
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
        // Without required, discriminator detection should not fire.
        // Both branches could match an object without 'kind', so standard oneOf applies.
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    public void ObjectWithoutKindMatchesBothBranches()
    {
        // Without 'kind' required, {"value": 42} matches branch 0 (value is number).
        // But does it match branch 1? "value" is string in branch 1, so 42 fails there.
        // So this should match exactly 1 branch → pass.
        var instance = s_fixture!.DynamicJsonType.ParseInstance("""{"value": 42}""");
        Assert.IsTrue(instance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\oneof-no-discriminator.json",
                Schema,
                "Corvus.Text.Json.Tests.OneOfDiscriminatorValidation",
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
/// Tests for oneOf with GeoJSON-style discriminator (9 branches with "type" property, single-value enum).
/// </summary>
[TestCategory("OneOfDiscriminator")]
[TestClass]
public class OneOfGeoJsonStyleDiscriminator
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "oneOf": [
                {
                    "type": "object",
                    "properties": { "type": { "enum": ["Point"] }, "coordinates": { "type": "array" } },
                    "required": ["type", "coordinates"]
                },
                {
                    "type": "object",
                    "properties": { "type": { "enum": ["MultiPoint"] }, "coordinates": { "type": "array" } },
                    "required": ["type", "coordinates"]
                },
                {
                    "type": "object",
                    "properties": { "type": { "enum": ["LineString"] }, "coordinates": { "type": "array" } },
                    "required": ["type", "coordinates"]
                },
                {
                    "type": "object",
                    "properties": { "type": { "enum": ["MultiLineString"] }, "coordinates": { "type": "array" } },
                    "required": ["type", "coordinates"]
                },
                {
                    "type": "object",
                    "properties": { "type": { "enum": ["Polygon"] }, "coordinates": { "type": "array" } },
                    "required": ["type", "coordinates"]
                },
                {
                    "type": "object",
                    "properties": { "type": { "enum": ["MultiPolygon"] }, "coordinates": { "type": "array" } },
                    "required": ["type", "coordinates"]
                },
                {
                    "type": "object",
                    "properties": { "type": { "enum": ["GeometryCollection"] }, "geometries": { "type": "array" } },
                    "required": ["type", "geometries"]
                },
                {
                    "type": "object",
                    "properties": { "type": { "enum": ["Feature"] }, "geometry": { "type": "object" }, "properties": { "type": "object" } },
                    "required": ["type", "geometry", "properties"]
                },
                {
                    "type": "object",
                    "properties": { "type": { "enum": ["FeatureCollection"] }, "features": { "type": "array" } },
                    "required": ["type", "features"]
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
    [DataRow("""{"type": "Point", "coordinates": [0, 0]}""")]
    [DataRow("""{"type": "MultiPoint", "coordinates": [[0, 0]]}""")]
    [DataRow("""{"type": "LineString", "coordinates": [[0, 0], [1, 1]]}""")]
    [DataRow("""{"type": "MultiLineString", "coordinates": [[[0, 0], [1, 1]]]}""")]
    [DataRow("""{"type": "Polygon", "coordinates": [[[0, 0], [1, 0], [1, 1], [0, 0]]]}""")]
    [DataRow("""{"type": "MultiPolygon", "coordinates": [[[[0, 0], [1, 0], [1, 1], [0, 0]]]]}""")]
    [DataRow("""{"type": "GeometryCollection", "geometries": []}""")]
    [DataRow("""{"type": "Feature", "geometry": {}, "properties": {}}""")]
    [DataRow("""{"type": "FeatureCollection", "features": []}""")]
    public void AllGeoJsonTypesAreAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"type": "Unknown", "coordinates": [0, 0]}""")]
    [DataRow("""{"type": "point", "coordinates": [0, 0]}""")]
    [DataRow("""{"type": "POINT", "coordinates": [0, 0]}""")]
    public void UnrecognizedTypeIsRejected(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void MissingTypePropertyIsRejected()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance("""{"coordinates": [0, 0]}""");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void NonStringTypePropertyIsRejected()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance("""{"type": 42, "coordinates": [0, 0]}""");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void MatchingTypeButMissingRequiredFieldIsRejected()
    {
        // Type is "Feature" but "geometry" and "properties" are missing
        var instance = s_fixture!.DynamicJsonType.ParseInstance("""{"type": "Feature"}""");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\oneof-geojson-discriminator.json",
                Schema,
                "Corvus.Text.Json.Tests.OneOfDiscriminatorValidation",
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
