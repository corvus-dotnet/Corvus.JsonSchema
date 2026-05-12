// <copyright file="SyncGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Specs.Tests.Infrastructure;
using Drivers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.SyncGenerator;

[TestCategory("SyncGenerator")]
[TestClass]
public class SyncGeneratorTests
{
    [TestMethod]
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
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
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
        Assert.IsFalse(result.IsValid);
    }
}