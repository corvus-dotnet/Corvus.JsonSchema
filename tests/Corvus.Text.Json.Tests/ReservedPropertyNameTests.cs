// <copyright file="ReservedPropertyNameTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace Corvus.Text.Json.Tests.ReservedPropertyNameValidation;

/// <summary>
/// Tests that object schemas with properties whose names collide with generated class names
/// (e.g. "jsonSchema", "builder", "source", "mutable") generate valid code.
/// </summary>
[Trait("Category", "CodeGen")]
public class ReservedPropertyNameValues : IClassFixture<ReservedPropertyNameValues.Fixture>
{
    private readonly Fixture fixture;

    public ReservedPropertyNameValues(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void ValidInstanceIsAccepted()
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(
            "{\"jsonSchema\": \"hello\", \"builder\": \"world\", \"source\": \"foo\", \"mutable\": \"bar\"}");
        Assert.True(instance.EvaluateSchema());
    }

    [Fact]
    public void PartialInstanceIsAccepted()
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(
            "{\"jsonSchema\": \"test\"}");
        Assert.True(instance.EvaluateSchema());
    }

    [Fact]
    public void EmptyObjectIsAccepted()
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance("{}");
        Assert.True(instance.EvaluateSchema());
    }

    [Fact]
    public void InvalidPropertyTypeIsRejected()
    {
        var instance = this.fixture.DynamicJsonType.ParseInstance(
            "{\"jsonSchema\": 42}");
        Assert.False(instance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\reserved-property-names.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"object\", \"properties\": {\"jsonSchema\": {\"type\": \"string\"}, \"builder\": {\"type\": \"string\"}, \"source\": {\"type\": \"string\"}, \"mutable\": {\"type\": \"string\"}}}",
                "Corvus.Text.Json.Tests.ReservedPropertyNameValidation",
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