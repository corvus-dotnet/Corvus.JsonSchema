// <copyright file="SyncGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Specs.Tests.Infrastructure;
using Drivers;
using Xunit;

namespace Corvus.Json.Specs.Tests.SyncGenerator;

[Trait("spec", "SyncGenerator")]
public class SyncGeneratorTests
{
    [Fact]
    public void SynchronousTypeGenerationValidString()
    {
        string schema = """
            {
                "type": "string"
            }
            """;

        using var driver = DriverFactory.CreateDraft202012Driver();
        Type generatedType = driver.SynchronouslyGenerateTypeForVirtualFile(
            schema,
            "SynchronousCodeGenerationDraft202012Feature.SynchronousTypeGeneration.json",
            "SynchronousCodeGenerationDraft202012Feature",
            "SynchronousTypeGeneration",
            validateFormat: false,
            optionalAsNullable: false);

        using var doc = JsonDocument.Parse("\"hello\"");
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(generatedType, doc.RootElement);
        ValidationContext result = instance.Validate(ValidationContext.ValidContext);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void SynchronousTypeGenerationInvalidNumber()
    {
        string schema = """
            {
                "type": "string"
            }
            """;

        using var driver = DriverFactory.CreateDraft202012Driver();
        Type generatedType = driver.SynchronouslyGenerateTypeForVirtualFile(
            schema,
            "SynchronousCodeGenerationDraft202012Feature.SynchronousTypeGeneration.json",
            "SynchronousCodeGenerationDraft202012Feature",
            "SynchronousTypeGeneration",
            validateFormat: false,
            optionalAsNullable: false);

        using var doc = JsonDocument.Parse("33");
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(generatedType, doc.RootElement);
        ValidationContext result = instance.Validate(ValidationContext.ValidContext);
        Assert.False(result.IsValid);
    }
}