// <copyright file="CaseInsensitiveEnumTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace Corvus.Text.Json.Tests.CaseInsensitiveEnumValidation;

/// <summary>
/// Tests that enum schemas with values that differ only by casing (e.g. "Microsoft" and "microsoft")
/// generate valid code with deduplicated EnumValues member names.
/// </summary>
[Trait("Category", "CodeGen")]
public class CaseInsensitiveEnumValues : IClassFixture<CaseInsensitiveEnumValues.Fixture>
{
    private readonly Fixture fixture;

    public CaseInsensitiveEnumValues(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData("\"Chromium\"")]
    [InlineData("\"Google\"")]
    [InlineData("\"Microsoft\"")]
    [InlineData("\"chromium\"")]
    [InlineData("\"google\"")]
    [InlineData("\"microsoft\"")]
    public void ValidEnumValueIsAccepted(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
        Assert.True(instance.EvaluateSchema());
    }

    [Theory]
    [InlineData("\"CHROMIUM\"")]
    [InlineData("\"GOOGLE\"")]
    [InlineData("\"MICROSOFT\"")]
    [InlineData("\"Chrome\"")]
    [InlineData("\"unknown\"")]
    [InlineData("\"\"")]
    public void InvalidStringValueIsRejected(string json)
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(json);
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
                "tests\\custom\\case-insensitive-enum.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"string\", \"enum\": [\"Chromium\", \"Google\", \"Microsoft\", \"chromium\", \"google\", \"microsoft\"]}",
                "Corvus.Text.Json.Tests.CaseInsensitiveEnumValidation",
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