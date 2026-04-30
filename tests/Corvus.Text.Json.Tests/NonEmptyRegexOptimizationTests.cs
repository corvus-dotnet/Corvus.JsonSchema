// <copyright file="NonEmptyRegexOptimizationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace Corvus.Text.Json.Tests.RegexOptimization;

/// <summary>
/// Tests for non-empty regex pattern optimization with the <c>pattern</c> keyword.
/// The pattern <c>.+</c> matches any non-empty string.
/// </summary>
[Trait("Optimization", "RegexNonEmpty")]
public class NonEmptyPatternKeyword : IClassFixture<NonEmptyPatternKeyword.Fixture>
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": ".+"
        }
        """;

    private readonly Fixture fixture;

    public NonEmptyPatternKeyword(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("\"hello\"")]
    [InlineData("\"x\"")]
    [InlineData("\"a very long string with many characters\"")]
    [InlineData("\"unicode: \\u00e9\"")]
    public void NonEmptyStringIsAccepted(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Fact]
    public void EmptyStringIsRejected()
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance("\"\"");
        Assert.False(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("42")]
    [InlineData("null")]
    public void NonStringValueIsRejected(string json)
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
                "tests\\custom\\nonempty-pattern-dotplus.json",
                Schema,
                "Corvus.Text.Json.Tests.RegexOptimization",
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
/// Tests for non-empty regex optimization with anchored <c>^.+$</c> variant.
/// </summary>
[Trait("Optimization", "RegexNonEmpty")]
public class NonEmptyPatternKeywordAnchored : IClassFixture<NonEmptyPatternKeywordAnchored.Fixture>
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^.+$"
        }
        """;

    private readonly Fixture fixture;

    public NonEmptyPatternKeywordAnchored(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("\"hello\"")]
    [InlineData("\"x\"")]
    public void NonEmptyStringIsAccepted(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Fact]
    public void EmptyStringIsRejected()
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance("\"\"");
        Assert.False(instance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\nonempty-pattern-anchored.json",
                Schema,
                "Corvus.Text.Json.Tests.RegexOptimization",
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
/// Tests for non-empty regex optimization with the <c>patternProperties</c> keyword.
/// A <c>.+</c> pattern property should match all non-empty property names.
/// </summary>
[Trait("Optimization", "RegexNonEmpty")]
public class NonEmptyPatternProperties : IClassFixture<NonEmptyPatternProperties.Fixture>
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "patternProperties": {
                ".+": { "type": "number" }
            },
            "additionalProperties": false
        }
        """;

    private readonly Fixture fixture;

    public NonEmptyPatternProperties(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("""{"foo": 1}""")]
    [InlineData("""{"a": 1, "b": 2}""")]
    [InlineData("""{}""")]
    public void ObjectWithAllNumberValuesIsAccepted(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("""{"foo": "bar"}""")]
    [InlineData("""{"a": 1, "b": "not a number"}""")]
    public void ObjectWithNonNumberValuesIsRejected(string json)
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
                "tests\\custom\\nonempty-patternproperties.json",
                Schema,
                "Corvus.Text.Json.Tests.RegexOptimization",
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