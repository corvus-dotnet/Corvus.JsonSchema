// <copyright file="AnyOfDiscriminatorValidationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace Corvus.Text.Json.Tests.AnyOfDiscriminatorValidation;

/// <summary>
/// Tests for anyOf discriminator-based short-circuiting with 5 branches (triggers EnumStringMap hash-based dispatch).
/// </summary>
[Trait("Optimization", "AnyOfDiscriminator")]
public class AnyOfDiscriminatorWith5Branches : IClassFixture<AnyOfDiscriminatorWith5Branches.Fixture>
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

    private readonly Fixture fixture;

    public AnyOfDiscriminatorWith5Branches(Fixture fixture)
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
        // matching branch is evaluated, and it fails → anyOf fails.
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
[Trait("Optimization", "AnyOfDiscriminator")]
public class AnyOfDiscriminatorWith3Branches : IClassFixture<AnyOfDiscriminatorWith3Branches.Fixture>
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

    private readonly Fixture fixture;

    public AnyOfDiscriminatorWith3Branches(Fixture fixture)
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
[Trait("Optimization", "AnyOfDiscriminator")]
public class AnyOfDiscriminatorWith2Branches : IClassFixture<AnyOfDiscriminatorWith2Branches.Fixture>
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

    private readonly Fixture fixture;

    public AnyOfDiscriminatorWith2Branches(Fixture fixture)
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
[Trait("Optimization", "AnyOfDiscriminator")]
public class AnyOfWithNonRequiredDiscriminator : IClassFixture<AnyOfWithNonRequiredDiscriminator.Fixture>
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

    private readonly Fixture fixture;

    public AnyOfWithNonRequiredDiscriminator(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("""{"kind": "alpha", "value": 42}""")]
    [InlineData("""{"kind": "bravo", "value": "hello"}""")]
    public void MatchingBranchIsAccepted(string json)
    {
        // With non-required discriminator, fast path dispatches when property is present.
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Fact]
    public void MissingDiscriminatorFallsThroughToSequential()
    {
        // Without 'kind' present, discriminator fast path falls through to sequential.
        // {"value": 42} matches branch 0 (value is number).
        // anyOf: at least one match → pass.
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