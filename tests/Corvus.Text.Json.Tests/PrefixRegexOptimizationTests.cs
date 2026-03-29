// <copyright file="PrefixRegexOptimizationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace Corvus.Text.Json.Tests.RegexOptimization;

/// <summary>
/// Tests for prefix regex pattern optimization with the <c>pattern</c> keyword.
/// The pattern <c>^x-</c> matches strings starting with "x-".
/// </summary>
[Trait("Optimization", "RegexPrefix")]
public class PrefixPatternKeyword : IClassFixture<PrefixPatternKeyword.Fixture>
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^x-"
        }
        """;

    private readonly Fixture fixture;

    public PrefixPatternKeyword(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("\"x-custom\"")]
    [InlineData("\"x-\"")]
    [InlineData("\"x-very-long-extension-name\"")]
    public void StringWithMatchingPrefixIsAccepted(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("\"hello\"")]
    [InlineData("\"\"")]
    [InlineData("\"X-uppercase\"")]
    [InlineData("\"y-wrong\"")]
    public void StringWithoutMatchingPrefixIsRejected(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
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
                "tests\\custom\\prefix-pattern-x-dash.json",
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
/// Tests for prefix regex optimization with the <c>patternProperties</c> keyword.
/// A <c>^/</c> pattern property should match property names starting with "/".
/// </summary>
[Trait("Optimization", "RegexPrefix")]
public class PrefixPatternProperties : IClassFixture<PrefixPatternProperties.Fixture>
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "name": { "type": "string" }
            },
            "patternProperties": {
                "^/": { "type": "object" }
            },
            "additionalProperties": false
        }
        """;

    private readonly Fixture fixture;

    public PrefixPatternProperties(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("""{"name": "api", "/users": {}}""")]
    [InlineData("""{"name": "api", "/users": {}, "/items": {}}""")]
    [InlineData("""{"name": "api"}""")]
    public void ObjectWithPrefixMatchedPropertiesIsAccepted(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("""{"name": "api", "/users": "not-object"}""")]
    [InlineData("""{"name": "api", "/users": 42}""")]
    public void PrefixMatchedPropertyWithWrongTypeIsRejected(string json)
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
                "tests\\custom\\prefix-patternproperties-slash.json",
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
/// Tests for prefix regex optimization with trailing <c>.*</c> (e.g. <c>^x-.*</c>).
/// </summary>
[Trait("Optimization", "RegexPrefix")]
public class PrefixPatternWithTrailingDotStar : IClassFixture<PrefixPatternWithTrailingDotStar.Fixture>
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^api/v1.*"
        }
        """;

    private readonly Fixture fixture;

    public PrefixPatternWithTrailingDotStar(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("\"api/v1\"")]
    [InlineData("\"api/v1/users\"")]
    [InlineData("\"api/v1/items/123\"")]
    public void StringWithMatchingPrefixIsAccepted(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("\"api/v2\"")]
    [InlineData("\"\"")]
    [InlineData("\"other\"")]
    public void StringWithoutMatchingPrefixIsRejected(string json)
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
                "tests\\custom\\prefix-pattern-with-dotstar.json",
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