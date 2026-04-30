// <copyright file="RangeRegexOptimizationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace Corvus.Text.Json.Tests.RegexOptimization;

/// <summary>
/// Tests for range regex pattern optimization with the <c>pattern</c> keyword.
/// The pattern <c>^.{1,256}$</c> matches strings between 1 and 256 characters long.
/// </summary>
[Trait("Optimization", "RegexRange")]
public class RangePatternKeyword : IClassFixture<RangePatternKeyword.Fixture>
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^.{1,10}$"
        }
        """;

    private readonly Fixture fixture;

    public RangePatternKeyword(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("\"a\"")]
    [InlineData("\"hello\"")]
    [InlineData("\"1234567890\"")]
    public void StringWithinRangeIsAccepted(string json)
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

    [Fact]
    public void StringExceedingMaxIsRejected()
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance("\"12345678901\"");
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
                "tests\\custom\\range-pattern-1-10.json",
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
/// Tests for range regex with larger bounds matching the Vercel benchmark pattern.
/// </summary>
[Trait("Optimization", "RegexRange")]
public class RangePatternKeywordLarge : IClassFixture<RangePatternKeywordLarge.Fixture>
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^.{1,256}$"
        }
        """;

    private readonly Fixture fixture;

    public RangePatternKeywordLarge(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("\"a\"")]
    [InlineData("\"hello world\"")]
    public void StringWithinRangeIsAccepted(string json)
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
                "tests\\custom\\range-pattern-1-256.json",
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
/// Tests for range regex with Unicode codepoints (ensures rune counting, not byte counting).
/// </summary>
[Trait("Optimization", "RegexRange")]
public class RangePatternKeywordUnicode : IClassFixture<RangePatternKeywordUnicode.Fixture>
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^.{1,3}$"
        }
        """;

    private readonly Fixture fixture;

    public RangePatternKeywordUnicode(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("\"\\u00e9\"")]
    [InlineData("\"ab\"")]
    [InlineData("\"\\u00e9\\u00e8\\u00ea\"")]
    public void UnicodeStringsWithinRuneRangeAreAccepted(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("\"abcd\"")]
    [InlineData("\"\\u00e9\\u00e8\\u00ea\\u00eb\"")]
    public void StringsExceedingRuneRangeAreRejected(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
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
                "tests\\custom\\range-pattern-1-3-unicode.json",
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
/// Tests for range regex optimization with the <c>patternProperties</c> keyword.
/// </summary>
[Trait("Optimization", "RegexRange")]
public class RangePatternProperties : IClassFixture<RangePatternProperties.Fixture>
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "patternProperties": {
                "^.{1,5}$": { "type": "string" }
            },
            "additionalProperties": false
        }
        """;

    private readonly Fixture fixture;

    public RangePatternProperties(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("""{"a": "v"}""")]
    [InlineData("""{"abc": "v", "de": "w"}""")]
    [InlineData("""{}""")]
    public void ObjectWithShortKeyNamesIsAccepted(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("""{"a": "v", "toolong": "w"}""")]
    public void ObjectWithTooLongKeyNameIsRejected(string json)
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
                "tests\\custom\\range-patternproperties-1-5.json",
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