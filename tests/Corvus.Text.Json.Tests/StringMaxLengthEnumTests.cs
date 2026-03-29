// <copyright file="StringMaxLengthEnumTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace Corvus.Text.Json.Tests.StringMaxLengthEnumValidation;

/// <summary>
/// Tests that schemas combining type:string + maxLength + enum generate valid code.
/// Regression test for a bug where unescapedUtf8JsonString was declared in the type-check
/// else clause scope but referenced by the enum handler after that scope closed.
/// </summary>
[Trait("Category", "CodeGen")]
public class StringMaxLengthEnumValues : IClassFixture<StringMaxLengthEnumValues.Fixture>
{
    private readonly Fixture fixture;

    public StringMaxLengthEnumValues(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("\"foo\"")]
    [InlineData("\"bar\"")]
    [InlineData("\"baz\"")]
    public void ValidEnumValueIsAccepted(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("\"qux\"")]
    [InlineData("\"quux\"")]
    public void InvalidEnumValueIsRejected(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void TooLongStringIsRejected()
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance("\"toolongvalue\"");
        Assert.False(instance.EvaluateSchema());
    }

    [Fact]
    public void NonStringIsRejected()
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
                "tests\\custom\\string-maxlength-enum.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"string\", \"maxLength\": 10, \"enum\": [\"foo\", \"bar\", \"baz\"]}",
                "Corvus.Text.Json.Tests.StringMaxLengthEnumValidation",
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