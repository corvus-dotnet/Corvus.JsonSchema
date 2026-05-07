// <copyright file="ImplicitConversionToStringTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Specs.Tests.Infrastructure;
using Drivers;
using Xunit;

namespace Corvus.Json.Specs.Tests.ImplicitConversionToString;

[Trait("spec", "ImplicitConversionToString")]
public class ImplicitConversionToStringTests
{
    [Fact]
    public async Task CreateImplicitConversionToString()
    {
        string schema = """
            {
                "type": "string",
                "minLength": 1
            }
            """;

        using var driver = DriverFactory.CreateDraft202012Driver();
        Type generatedType = await driver.GenerateTypeForVirtualFile(
            schema,
            "ImplicitConversionToStringEnabledFeature.CreateImplicitConversionToString.json",
            "ImplicitConversionToStringEnabledFeature",
            "CreateImplicitConversionToString",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: true);

        using var doc = JsonDocument.Parse("\"foo\"");
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(generatedType, doc.RootElement);

        string? assignedString = GetImplicitString(generatedType, instance);
        Assert.Equal("foo", assignedString);
    }

    [Fact]
    public async Task CreateImplicitConversionToStringWithBuiltInStringType()
    {
        string schema = """
            {
                "type": "string"
            }
            """;

        using var driver = DriverFactory.CreateDraft202012Driver();
        Type generatedType = await driver.GenerateTypeForVirtualFile(
            schema,
            "ImplicitConversionToStringEnabledFeature.CreateImplicitConversionToStringWithWhatWouldNormallyBeABuiltInStringType.json",
            "ImplicitConversionToStringEnabledFeature",
            "CreateImplicitConversionToStringWithWhatWouldNormallyBeABuiltInStringType",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: true);

        using var doc = JsonDocument.Parse("\"foo\"");
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(generatedType, doc.RootElement);

        string? assignedString = GetImplicitString(generatedType, instance);
        Assert.Equal("foo", assignedString);
    }

    [Fact]
    public async Task CreateImplicitConversionToStringWithBuiltInStringFormatType()
    {
        string schema = """
            {
                "type": "string",
                "format": "uuid"
            }
            """;

        using var driver = DriverFactory.CreateDraft202012Driver();
        Type generatedType = await driver.GenerateTypeForVirtualFile(
            schema,
            "ImplicitConversionToStringEnabledFeature.CreateImplicitConversionToStringWithWhatWouldNormallyBeABuiltInStringFormatType.json",
            "ImplicitConversionToStringEnabledFeature",
            "CreateImplicitConversionToStringWithWhatWouldNormallyBeABuiltInStringFormatType",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: true);

        using var doc = JsonDocument.Parse("\"3af1928d-96f7-4272-a057-beb6194579e6\"");
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(generatedType, doc.RootElement);

        string? assignedString = GetImplicitString(generatedType, instance);
        Assert.Equal("3af1928d-96f7-4272-a057-beb6194579e6", assignedString);
    }

    private static string GetImplicitString(Type type, IJsonValue value)
    {
        return type
            .GetMethods()
            .SingleOrDefault(m => m.Name == "op_Implicit" && m.ReturnType == typeof(string))
            ?.Invoke(null, [value])
            is string s
                ? s
                : throw new InvalidOperationException("No string returned from the implicit conversion.");
    }
}