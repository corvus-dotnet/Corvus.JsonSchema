// <copyright file="EnumStringSetValidationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace Corvus.Text.Json.Tests.EnumStringSetValidation;

/// <summary>
/// Tests for string enum validation with 4+ values, which triggers the EnumStringSet hash-based
/// code path instead of sequential SequenceEqual comparisons.
/// </summary>
[Trait("Optimization", "EnumStringSet")]
public class StringEnumWith5Values : IClassFixture<StringEnumWith5Values.Fixture>
{
    private readonly Fixture _fixture;

    public StringEnumWith5Values(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("\"alpha\"")]
    [InlineData("\"bravo\"")]
    [InlineData("\"charlie\"")]
    [InlineData("\"delta\"")]
    [InlineData("\"echo\"")]
    public void ValidEnumValueIsAccepted(string json)
    {
        var instance = _fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("\"foxtrot\"")]
    [InlineData("\"ALPHA\"")]
    [InlineData("\"Alpha\"")]
    [InlineData("\"\"")]
    [InlineData("\"alph\"")]
    [InlineData("\"alphaa\"")]
    public void InvalidStringValueIsRejected(string json)
    {
        var instance = _fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    public void NonStringValueIsRejected(string json)
    {
        var instance = _fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\enum-string-set-5.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"string\", \"enum\": [\"alpha\", \"bravo\", \"charlie\", \"delta\", \"echo\"]}",
                "Corvus.Text.Json.Tests.EnumStringSetValidation",
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
/// Tests for string enum validation at the exact threshold boundary (4 values triggers hash set).
/// </summary>
[Trait("Optimization", "EnumStringSet")]
public class StringEnumWith4Values : IClassFixture<StringEnumWith4Values.Fixture>
{
    private readonly Fixture _fixture;

    public StringEnumWith4Values(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("\"red\"")]
    [InlineData("\"green\"")]
    [InlineData("\"blue\"")]
    [InlineData("\"yellow\"")]
    public void ValidEnumValueIsAccepted(string json)
    {
        var instance = _fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("\"orange\"")]
    [InlineData("\"RED\"")]
    public void InvalidStringValueIsRejected(string json)
    {
        var instance = _fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\enum-string-set-4.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"string\", \"enum\": [\"red\", \"green\", \"blue\", \"yellow\"]}",
                "Corvus.Text.Json.Tests.EnumStringSetValidation",
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
/// Tests for heterogeneous enum with 4+ string values plus other types.
/// The string portion should use EnumStringSet, other types use standard matching.
/// </summary>
[Trait("Optimization", "EnumStringSet")]
public class HeterogeneousEnumWith5StringValues : IClassFixture<HeterogeneousEnumWith5StringValues.Fixture>
{
    private readonly Fixture _fixture;

    public HeterogeneousEnumWith5StringValues(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("\"alpha\"")]
    [InlineData("\"bravo\"")]
    [InlineData("\"charlie\"")]
    [InlineData("\"delta\"")]
    [InlineData("\"echo\"")]
    public void ValidStringEnumValueIsAccepted(string json)
    {
        var instance = _fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("null")]
    public void ValidNonStringEnumValueIsAccepted(string json)
    {
        var instance = _fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("\"foxtrot\"")]
    [InlineData("\"\"")]
    public void InvalidStringValueIsRejected(string json)
    {
        var instance = _fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("99")]
    [InlineData("false")]
    [InlineData("[]")]
    public void InvalidNonStringValueIsRejected(string json)
    {
        var instance = _fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\enum-heterogeneous-5-strings.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"enum\": [\"alpha\", \"bravo\", \"charlie\", \"delta\", \"echo\", 42, true, null]}",
                "Corvus.Text.Json.Tests.EnumStringSetValidation",
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
/// Tests for string enum with values of varying lengths to exercise the hash function's
/// different code paths (short keys ≤7 bytes vs longer keys).
/// </summary>
[Trait("Optimization", "EnumStringSet")]
public class StringEnumWithVaryingLengths : IClassFixture<StringEnumWithVaryingLengths.Fixture>
{
    private readonly Fixture _fixture;

    public StringEnumWithVaryingLengths(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("\"a\"")]
    [InlineData("\"ab\"")]
    [InlineData("\"abc\"")]
    [InlineData("\"abcd\"")]
    [InlineData("\"abcde\"")]
    [InlineData("\"abcdef\"")]
    [InlineData("\"abcdefg\"")]
    [InlineData("\"abcdefgh\"")]
    [InlineData("\"a-longer-string-value\"")]
    public void ValidEnumValueIsAccepted(string json)
    {
        var instance = _fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"x\"")]
    [InlineData("\"abcdefgx\"")]
    [InlineData("\"a-longer-string-valuex\"")]
    public void InvalidStringValueIsRejected(string json)
    {
        var instance = _fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\enum-string-set-varying-lengths.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"string\", \"enum\": [\"a\", \"ab\", \"abc\", \"abcd\", \"abcde\", \"abcdef\", \"abcdefg\", \"abcdefgh\", \"a-longer-string-value\"]}",
                "Corvus.Text.Json.Tests.EnumStringSetValidation",
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
/// Tests for integer enum validation with 4+ values, which triggers the switch-based
/// code path instead of sequential AreEqualNormalizedJsonNumbers comparisons.
/// </summary>
[Trait("Optimization", "EnumIntegerSwitch")]
public class IntegerEnumWith5Values : IClassFixture<IntegerEnumWith5Values.Fixture>
{
    private readonly Fixture _fixture;

    public IntegerEnumWith5Values(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    [InlineData("5")]
    public void ValidIntegerEnumValueIsAccepted(string json)
    {
        var instance = _fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("2.0")]
    [InlineData("5.0")]
    public void EquivalentDecimalFormIsAccepted(string json)
    {
        var instance = _fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("6")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("99")]
    public void InvalidNumericValueIsRejected(string json)
    {
        var instance = _fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("\"1\"")]
    [InlineData("true")]
    [InlineData("null")]
    public void NonNumericValueIsRejected(string json)
    {
        var instance = _fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\enum-integer-switch-5.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"integer\", \"enum\": [1, 2, 3, 4, 5]}",
                "Corvus.Text.Json.Tests.EnumStringSetValidation",
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
/// Tests for integer enum with negative values and zero to exercise switch edge cases.
/// </summary>
[Trait("Optimization", "EnumIntegerSwitch")]
public class IntegerEnumWithNegativeValues : IClassFixture<IntegerEnumWithNegativeValues.Fixture>
{
    private readonly Fixture _fixture;

    public IntegerEnumWithNegativeValues(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("-100")]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("100")]
    public void ValidEnumValueIsAccepted(string json)
    {
        var instance = _fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("-101")]
    [InlineData("-2")]
    [InlineData("2")]
    [InlineData("99")]
    public void InvalidNumericValueIsRejected(string json)
    {
        var instance = _fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\enum-integer-switch-negatives.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"enum\": [-100, -1, 0, 1, 100]}",
                "Corvus.Text.Json.Tests.EnumStringSetValidation",
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
/// Tests for heterogeneous enum with both integer and non-integer numeric values.
/// Non-integer values prevent the switch optimization; sequential comparison is used instead.
/// </summary>
[Trait("Optimization", "EnumIntegerSwitch")]
public class MixedNumericEnum : IClassFixture<MixedNumericEnum.Fixture>
{
    private readonly Fixture _fixture;

    public MixedNumericEnum(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3.14")]
    [InlineData("4")]
    [InlineData("2.718")]
    public void ValidEnumValueIsAccepted(string json)
    {
        var instance = _fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("5")]
    [InlineData("3.15")]
    [InlineData("0")]
    public void InvalidNumericValueIsRejected(string json)
    {
        var instance = _fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\enum-mixed-numeric.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"number\", \"enum\": [1, 2, 3.14, 4, 2.718]}",
                "Corvus.Text.Json.Tests.EnumStringSetValidation",
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