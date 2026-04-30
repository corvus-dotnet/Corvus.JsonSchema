// <copyright file="OneOfDiscriminatorValidationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace Corvus.Text.Json.Tests.OneOfDiscriminatorValidation;

/// <summary>
/// Tests for oneOf discriminator-based short-circuiting with 5 branches (triggers EnumStringMap hash-based dispatch).
/// Schema: oneOf with 5 branches, each requiring a "kind" property with a single distinct enum value.
/// </summary>
[Trait("Optimization", "OneOfDiscriminator")]
public class OneOfDiscriminatorWith5Branches : IClassFixture<OneOfDiscriminatorWith5Branches.Fixture>
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

    private readonly Fixture fixture;

    public OneOfDiscriminatorWith5Branches(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("""{"kind": "alpha", "value": 42}""")]
    [InlineData("""{"kind": "bravo", "value": "hello"}""")]
    [InlineData("""{"kind": "charlie", "value": true}""")]
    [InlineData("""{"kind": "delta", "value": [1, 2]}""")]
    [InlineData("""{"kind": "echo", "value": {"x": 1}}""")]
    public void MatchingDiscriminatorAndBranchIsAccepted(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("""{"kind": "alpha", "value": "wrong"}""")]
    [InlineData("""{"kind": "bravo", "value": 42}""")]
    public void MatchingDiscriminatorButFailingBranchIsRejected(string json)
    {
        // The discriminator selects the right branch, but the branch validation fails
        // because 'value' has the wrong type. With discriminator dispatch, only the
        // matching branch is evaluated, and it fails → oneOf fails.
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("""{"kind": "unknown"}""")]
    [InlineData("""{"kind": "ALPHA"}""")]
    [InlineData("""{"kind": ""}""")]
    public void UnrecognizedDiscriminatorValueIsRejected(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void MissingDiscriminatorPropertyIsRejected()
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance("""{"value": 42}""");
        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void NonStringDiscriminatorValueIsRejected()
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance("""{"kind": 123}""");
        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void NonObjectInstanceIsRejected()
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance("""42""");
        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void EmptyObjectIsRejected()
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance("""{}""");
        Assert.False(instance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

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
[Trait("Optimization", "OneOfDiscriminator")]
public class OneOfDiscriminatorWith3Branches : IClassFixture<OneOfDiscriminatorWith3Branches.Fixture>
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

    private readonly Fixture fixture;

    public OneOfDiscriminatorWith3Branches(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("""{"type": "point", "x": 1.0, "y": 2.0}""")]
    [InlineData("""{"type": "line", "start": {}, "end": {}}""")]
    [InlineData("""{"type": "circle", "center": {}, "radius": 5.0}""")]
    public void MatchingDiscriminatorAndBranchIsAccepted(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("""{"type": "point"}""")]
    public void MatchingDiscriminatorButMissingRequiredFieldIsRejected(string json)
    {
        // Discriminator matches "point" branch, but x and y are missing
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("""{"type": "rectangle"}""")]
    [InlineData("""{"type": ""}""")]
    public void UnrecognizedDiscriminatorValueIsRejected(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void MissingDiscriminatorPropertyIsRejected()
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance("""{"x": 1, "y": 2}""");
        Assert.False(instance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

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
[Trait("Optimization", "OneOfDiscriminator")]
public class OneOfDiscriminatorWith2Branches : IClassFixture<OneOfDiscriminatorWith2Branches.Fixture>
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

    private readonly Fixture fixture;

    public OneOfDiscriminatorWith2Branches(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("""{"status": "active", "since": "2024-01-01"}""")]
    [InlineData("""{"status": "inactive", "reason": "retired"}""")]
    public void MatchingDiscriminatorIsAccepted(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("""{"status": "pending"}""")]
    [InlineData("""{"status": ""}""")]
    public void UnrecognizedDiscriminatorValueIsRejected(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void MissingDiscriminatorIsRejected()
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance("""{"since": "2024-01-01"}""");
        Assert.False(instance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

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
[Trait("Optimization", "OneOfDiscriminator")]
public class OneOfWithoutDiscriminator : IClassFixture<OneOfWithoutDiscriminator.Fixture>
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

    private readonly Fixture fixture;

    public OneOfWithoutDiscriminator(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("""{"kind": "alpha", "value": 42}""")]
    [InlineData("""{"kind": "bravo", "value": "hello"}""")]
    public void MatchingBranchIsAccepted(string json)
    {
        // Without required, discriminator detection should not fire.
        // Both branches could match an object without 'kind', so standard oneOf applies.
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Fact]
    public void ObjectWithoutKindMatchesBothBranches()
    {
        // Without 'kind' required, {"value": 42} matches branch 0 (value is number).
        // But does it match branch 1? "value" is string in branch 1, so 42 fails there.
        // So this should match exactly 1 branch → pass.
        var instance = this.fixture.DynamicJsonType.ParseInstance("""{"value": 42}""");
        Assert.True(instance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

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
[Trait("Optimization", "OneOfDiscriminator")]
public class OneOfGeoJsonStyleDiscriminator : IClassFixture<OneOfGeoJsonStyleDiscriminator.Fixture>
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

    private readonly Fixture fixture;

    public OneOfGeoJsonStyleDiscriminator(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("""{"type": "Point", "coordinates": [0, 0]}""")]
    [InlineData("""{"type": "MultiPoint", "coordinates": [[0, 0]]}""")]
    [InlineData("""{"type": "LineString", "coordinates": [[0, 0], [1, 1]]}""")]
    [InlineData("""{"type": "MultiLineString", "coordinates": [[[0, 0], [1, 1]]]}""")]
    [InlineData("""{"type": "Polygon", "coordinates": [[[0, 0], [1, 0], [1, 1], [0, 0]]]}""")]
    [InlineData("""{"type": "MultiPolygon", "coordinates": [[[[0, 0], [1, 0], [1, 1], [0, 0]]]]}""")]
    [InlineData("""{"type": "GeometryCollection", "geometries": []}""")]
    [InlineData("""{"type": "Feature", "geometry": {}, "properties": {}}""")]
    [InlineData("""{"type": "FeatureCollection", "features": []}""")]
    public void AllGeoJsonTypesAreAccepted(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("""{"type": "Unknown", "coordinates": [0, 0]}""")]
    [InlineData("""{"type": "point", "coordinates": [0, 0]}""")]
    [InlineData("""{"type": "POINT", "coordinates": [0, 0]}""")]
    public void UnrecognizedTypeIsRejected(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void MissingTypePropertyIsRejected()
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance("""{"coordinates": [0, 0]}""");
        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void NonStringTypePropertyIsRejected()
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance("""{"type": 42, "coordinates": [0, 0]}""");
        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void MatchingTypeButMissingRequiredFieldIsRejected()
    {
        // Type is "Feature" but "geometry" and "properties" are missing
        var instance = this.fixture.DynamicJsonType.ParseInstance("""{"type": "Feature"}""");
        Assert.False(instance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

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