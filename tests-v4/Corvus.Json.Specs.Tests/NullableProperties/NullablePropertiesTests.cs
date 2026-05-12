// <copyright file="NullablePropertiesTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Specs.Tests.Infrastructure;
using Drivers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.NullableProperties;

[TestCategory("NullableProperties")]
[TestClass]
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

    [TestMethod]
    [DataRow("""{"foo": "p1", "bar": "p2" }""", "p1", "p2")]
    [DataRow("""{"foo": "p1", "bar": null }""", "p1", "null")]
    [DataRow("""{"foo": "p1" }""", "p1", "null")]
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

        Assert.IsTrue(JsonSchemaBuilderDriver.CompareStringValue(generatedType, instance, "Foo", fooValue));
        Assert.IsTrue(JsonSchemaBuilderDriver.CompareNullableStringValue(generatedType, instance, "Bar", barValue));
    }

    [TestMethod]
    [DataRow("""{"foo": "p1", "bar": "p2" }""", "p1", "p2")]
    [DataRow("""{"foo": "p1", "bar": null }""", "p1", "Null")]
    [DataRow("""{"foo": "p1" }""", "p1", "Undefined")]
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

        Assert.IsTrue(JsonSchemaBuilderDriver.CompareStringValue(generatedType, instance, "Foo", fooValue));
        Assert.IsTrue(JsonSchemaBuilderDriver.CompareNullableStringValue(generatedType, instance, "Bar", barValue));
    }
}