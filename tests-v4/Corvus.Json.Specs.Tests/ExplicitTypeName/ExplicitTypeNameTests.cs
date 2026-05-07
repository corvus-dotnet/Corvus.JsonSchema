// <copyright file="ExplicitTypeNameTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Specs.Tests.Infrastructure;
using Drivers;
using Xunit;

namespace Corvus.Json.Specs.Tests.ExplicitTypeName;

[Trait("spec", "ExplicitTypeName")]
public class ExplicitTypeNameTests
{
    private const string CorvusTypeNameSchema = """
        {
            "type": "array",
            "prefixItems": [
                {
                    "$corvusTypeName": "PositiveInt32",
                    "type": "integer",
                    "format": "int32",
                    "minimum": 1
                },
                { "type": "string" },
                {
                    "type": "string",
                    "format": "date-time"
                }
            ],
            "unevaluatedItems": false
        }
        """;

    private const string InvalidCorvusTypeNameSchema = """
        {
            "type": "array",
            "prefixItems": [
                {
                    "$corvusTypeName": "",
                    "type": "integer",
                    "format": "int32",
                    "minimum": 1
                },
                { "type": "string" },
                {
                    "type": "string",
                    "format": "date-time"
                }
            ],
            "unevaluatedItems": false
        }
        """;

    [Theory]
    [InlineData("""[1, "hello", "2012-04-23T18:25:43.511Z"]""", true)]
    [InlineData("""[0, "hello", "2012-04-23T18:25:43.511Z"]""", false)]
    public async Task CorvusTypeName(string inputData, bool expectedValid)
    {
        using var driver = DriverFactory.CreateDraft202012Driver();
        Type generatedType = await driver.GenerateTypeForVirtualFile(
            CorvusTypeNameSchema,
            "CorvusTypeNameCodeGenerationDraft202012Feature.CorvusTypeName.json",
            "CorvusTypeNameCodeGenerationDraft202012Feature",
            "CorvusTypeName",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: false);

        using var doc = JsonDocument.Parse(inputData);
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(generatedType, doc.RootElement);
        ValidationContext result = instance.Validate(ValidationContext.ValidContext);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData("""[1, "hello", "2012-04-23T18:25:43.511Z"]""", true)]
    [InlineData("""[0, "hello", "2012-04-23T18:25:43.511Z"]""", false)]
    public async Task InvalidCorvusTypeNameIsIgnored(string inputData, bool expectedValid)
    {
        using var driver = DriverFactory.CreateDraft202012Driver();
        Type generatedType = await driver.GenerateTypeForVirtualFile(
            InvalidCorvusTypeNameSchema,
            "CorvusTypeNameCodeGenerationDraft202012Feature.InvalidCorvusTypeNameIsIgnored.json",
            "CorvusTypeNameCodeGenerationDraft202012Feature",
            "InvalidCorvusTypeNameIsIgnored",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: false);

        using var doc = JsonDocument.Parse(inputData);
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(generatedType, doc.RootElement);
        ValidationContext result = instance.Validate(ValidationContext.ValidContext);

        Assert.Equal(expectedValid, result.IsValid);
    }
}