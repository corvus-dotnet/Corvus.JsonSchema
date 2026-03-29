// <copyright file="OneOfNumericDiscriminatorValidationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace Corvus.Text.Json.Tests.OneOfNumericDiscriminatorValidation;

/// <summary>
/// Tests for oneOf discriminator with integer const values (CmakePresets version pattern).
/// </summary>
[Trait("Optimization", "OneOfDiscriminator")]
public class OneOfNumericDiscriminatorBasic : IClassFixture<OneOfNumericDiscriminatorBasic.Fixture>
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

    private readonly Fixture fixture;

    public OneOfNumericDiscriminatorBasic(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("""{"version": 1, "name": "hello"}""")]
    [InlineData("""{"version": 2, "name": "world"}""")]
    [InlineData("""{"version": 2, "name": "world", "extra": "data"}""")]
    [InlineData("""{"version": 3}""")]
    [InlineData("""{"version": 3, "data": {}}""")]
    public void ValidInstanceIsAccepted(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("""{"version": 4, "name": "invalid"}""")]
    [InlineData("""{"version": 1}""")]
    [InlineData("""{"name": "no version"}""")]
    [InlineData("""{"version": "1", "name": "string version"}""")]
    public void InvalidInstanceIsRejected(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

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
[Trait("Optimization", "AnyOfDiscriminator")]
public class AnyOfNumericDiscriminatorBasic : IClassFixture<AnyOfNumericDiscriminatorBasic.Fixture>
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

    private readonly Fixture fixture;

    public AnyOfNumericDiscriminatorBasic(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("""{"level": 1, "name": "hello"}""")]
    [InlineData("""{"level": 2}""")]
    [InlineData("""{"level": 2, "items": []}""")]
    [InlineData("""{"level": 3}""")]
    public void ValidInstanceIsAccepted(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("""{"level": 4}""")]
    [InlineData("""{"level": 1}""")]
    [InlineData("""{"name": "no level"}""")]
    public void InvalidInstanceIsRejected(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

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
[Trait("Optimization", "OneOfDiscriminator")]
public class OneOfNumericDiscriminatorWithNonTrivialNumbers : IClassFixture<OneOfNumericDiscriminatorWithNonTrivialNumbers.Fixture>
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

    private readonly Fixture fixture;

    public OneOfNumericDiscriminatorWithNonTrivialNumbers(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("""{"id": -1}""")]
    [InlineData("""{"id": -1, "desc": "sentinel"}""")]
    [InlineData("""{"id": 0}""")]
    [InlineData("""{"id": 100, "desc": "big"}""")]
    public void ValidInstanceIsAccepted(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("""{"id": 1}""")]
    [InlineData("""{"id": -2}""")]
    [InlineData("""{"id": "string"}""")]
    public void InvalidInstanceIsRejected(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

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
[Trait("Optimization", "AnyOfDiscriminator")]
public class AnyOfPartialNumericDiscriminator : IClassFixture<AnyOfPartialNumericDiscriminator.Fixture>
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

    private readonly Fixture fixture;

    public AnyOfPartialNumericDiscriminator(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("null")]
    [InlineData("""{"version": 1, "name": "hello"}""")]
    [InlineData("""{"version": 2}""")]
    [InlineData("""{"version": 2, "data": {}}""")]
    public void ValidInstanceIsAccepted(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("""{"version": 3}""")]
    [InlineData("""{"version": 1}""")]
    [InlineData("42")]
    public void InvalidInstanceIsRejected(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

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