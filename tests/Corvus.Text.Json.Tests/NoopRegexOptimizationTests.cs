// <copyright file="NoopRegexOptimizationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace Corvus.Text.Json.Tests.RegexOptimization;

/// <summary>
/// Tests for noop regex pattern optimization with the <c>pattern</c> keyword.
/// The pattern <c>.*</c> matches any string; we verify that validation still
/// works correctly when the regex is replaced with inline always-true logic.
/// </summary>
[Trait("Optimization", "RegexNoop")]
public class NoopPatternKeyword : IClassFixture<NoopPatternKeyword.Fixture>
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": ".*"
        }
        """;

    private readonly Fixture fixture;

    public NoopPatternKeyword(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("\"hello\"")]
    [InlineData("\"\"")]
    [InlineData("\"a very long string with lots of characters 1234567890!@#$%\"")]
    [InlineData("\"unicode: \\u00e9\\u00e8\\u00ea\"")]
    public void StringValueAlwaysMatchesNoopPattern(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("[1, 2]")]
    [InlineData("{\"a\": 1}")]
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
                "tests\\custom\\noop-pattern-dotstar.json",
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
/// Tests for noop regex optimization with the anchored <c>^.*$</c> variant.
/// </summary>
[Trait("Optimization", "RegexNoop")]
public class NoopPatternKeywordAnchored : IClassFixture<NoopPatternKeywordAnchored.Fixture>
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^.*$"
        }
        """;

    private readonly Fixture fixture;

    public NoopPatternKeywordAnchored(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("\"hello\"")]
    [InlineData("\"\"")]
    public void StringValueAlwaysMatchesAnchoredNoopPattern(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
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
                "tests\\custom\\noop-pattern-anchored.json",
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
/// Tests for noop regex optimization with the <c>[\s\S]*</c> variant
/// (character class matching any character, zero or more times).
/// </summary>
[Trait("Optimization", "RegexNoop")]
public class NoopPatternKeywordCharClass : IClassFixture<NoopPatternKeywordCharClass.Fixture>
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "[\\s\\S]*"
        }
        """;

    private readonly Fixture fixture;

    public NoopPatternKeywordCharClass(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("\"hello world\"")]
    [InlineData("\"\"")]
    [InlineData("\"\\n\\t\"")]
    public void StringValueAlwaysMatchesCharClassNoopPattern(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Fact]
    public void NonStringValueIsRejected()
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance("42");
        Assert.False(instance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\noop-pattern-charclass.json",
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
/// Tests for noop regex optimization with the <c>patternProperties</c> keyword.
/// A <c>.*</c> pattern property should match all properties unconditionally.
/// </summary>
[Trait("Optimization", "RegexNoop")]
public class NoopPatternProperties : IClassFixture<NoopPatternProperties.Fixture>
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "patternProperties": {
                ".*": { "type": "string" }
            },
            "additionalProperties": false
        }
        """;

    private readonly Fixture fixture;

    public NoopPatternProperties(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("""{"foo": "bar"}""")]
    [InlineData("""{"a": "b", "c": "d"}""")]
    [InlineData("""{}""")]
    public void ObjectWithAllStringValuesIsAccepted(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("""{"foo": 42}""")]
    [InlineData("""{"a": "valid", "b": 123}""")]
    public void ObjectWithNonStringValuesIsRejected(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("\"not an object\"")]
    [InlineData("42")]
    public void NonObjectValueIsRejected(string json)
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
                "tests\\custom\\noop-patternproperties-dotstar.json",
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