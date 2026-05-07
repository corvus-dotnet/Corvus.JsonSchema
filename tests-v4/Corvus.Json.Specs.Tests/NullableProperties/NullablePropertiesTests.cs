// <copyright file="NullablePropertiesTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Specs.Tests.Infrastructure;
using Drivers;
using Xunit;

namespace Corvus.Json.Specs.Tests.NullableProperties;

[Trait("spec", "NullableProperties")]
public class NullablePropertiesTests
{
    private const string ObjectSchema = """
        {
            "type": "object",
            "required": ["foo"],
            "properties": {
                "foo": { "type": "string" },
                "bar": { "type": ["string", "null"] }
            }
        }
        """;

    [Theory]
    [InlineData("""{"foo": "p1", "bar": "p2" }""", "p1", "p2")]
    [InlineData("""{"foo": "p1", "bar": null }""", "p1", "null")]
    [InlineData("""{"foo": "p1" }""", "p1", "null")]
    public async Task CreateNullableOptionalProperties(string inputData, string fooValue, string barValue)
    {
        using var driver = DriverFactory.CreateDraft202012Driver();
        Type generatedType = await driver.GenerateTypeForVirtualFile(
            ObjectSchema,
            "CorvusNullablePropertiesCodeGenerationDraft202012Feature.CreateNullableOptionalProperties.json",
            "CorvusNullablePropertiesCodeGenerationDraft202012Feature",
            "CreateNullableOptionalProperties",
            validateFormat: false,
            optionalAsNullable: true,
            useImplicitOperatorString: false);

        using var doc = JsonDocument.Parse(inputData);
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(generatedType, doc.RootElement);

        Assert.True(JsonSchemaBuilderDriver.CompareStringValue(generatedType, instance, "Foo", fooValue));
        Assert.True(JsonSchemaBuilderDriver.CompareNullableStringValue(generatedType, instance, "Bar", barValue));
    }

    [Theory]
    [InlineData("""{"foo": "p1", "bar": "p2" }""", "p1", "p2")]
    [InlineData("""{"foo": "p1", "bar": null }""", "p1", "Null")]
    [InlineData("""{"foo": "p1" }""", "p1", "Undefined")]
    public async Task CreateNotNullableOptionalProperties(string inputData, string fooValue, string barValue)
    {
        using var driver = DriverFactory.CreateDraft202012Driver();
        Type generatedType = await driver.GenerateTypeForVirtualFile(
            ObjectSchema,
            "CorvusNullablePropertiesCodeGenerationDraft202012Feature.CreateNotNullableOptionalProperties.json",
            "CorvusNullablePropertiesCodeGenerationDraft202012Feature",
            "CreateNotNullableOptionalProperties",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: false);

        using var doc = JsonDocument.Parse(inputData);
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(generatedType, doc.RootElement);

        Assert.True(JsonSchemaBuilderDriver.CompareStringValue(generatedType, instance, "Foo", fooValue));
        Assert.True(JsonSchemaBuilderDriver.CompareNullableStringValue(generatedType, instance, "Bar", barValue));
    }
}