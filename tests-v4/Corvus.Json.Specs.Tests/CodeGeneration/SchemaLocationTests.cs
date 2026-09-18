// <copyright file="SchemaLocationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.Specs.Tests.Infrastructure;
using Drivers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.CodeGeneration;

/// <summary>
/// Issue #957. The schema location pushed by generated <c>Validate</c> methods, and the generated
/// <c>SchemaLocation</c> property, must be a reference the V4 <see cref="ValidationContext"/> can
/// compose keyword locations onto, in the same <c>file#/pointer</c> shape as every 4.x release.
/// </summary>
[TestClass]
public class SchemaLocationTests
{
    private const string FileName = "issue957-swagger.json";
    private const string RootPath = "#/parameters/FooId";
    private const string ExpectedSchemaLocation = FileName + RootPath;

    private static JsonSchemaBuilderDriver? s_driver;
    private static Type? s_generatedType;
    private static IReadOnlyCollection<GeneratedCodeFile>? s_generatedCode;

    [ClassInitialize]
    public static async Task ClassInit(TestContext context)
    {
        s_driver = DriverFactory.CreateAdditionalDraft4Driver();
        s_generatedCode = await s_driver.GenerateCodeForJsonSchemaTestSuite(
            FileName,
            RootPath,
            "Issue957",
            "SchemaLocationCode",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            rebaseAsRoot: false);
        s_generatedType = await s_driver.GenerateTypeForJsonSchemaTestSuite(
            FileName,
            RootPath,
            "Issue957",
            "SchemaLocationType",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            rebaseAsRoot: false);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        s_driver?.Dispose();
    }

    [TestMethod]
    public void GeneratedValidateMethod_PushesFileRelativeSchemaLocation()
    {
        GeneratedCodeFile validateFile = s_generatedCode!.Single(f => f.FileName.EndsWith(".Validate.cs", StringComparison.Ordinal) && f.FileName.StartsWith("FooId", StringComparison.Ordinal));

        StringAssert.Contains(validateFile.FileContent, $"result = result.PushSchemaLocation(\"{ExpectedSchemaLocation}\");");
    }

    [TestMethod]
    public void GeneratedSchemaLocationProperty_IsFileRelativeSchemaLocation()
    {
        object? schemaLocation = s_generatedType!.GetProperty("SchemaLocation")!.GetValue(null);

        Assert.AreEqual(ExpectedSchemaLocation, schemaLocation);
    }

    [TestMethod]
    public void ValidatingAnInvalidInstance_ReportsTheSchemaLocationOfTheFailingKeyword()
    {
        using var doc = JsonDocument.Parse("\"notAnInteger\"");
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(s_generatedType!, doc.RootElement);

        ValidationContext result = instance.Validate(ValidationContext.ValidContext.UsingResults().UsingStack(), ValidationLevel.Detailed);

        Assert.IsFalse(result.IsValid);
        ValidationResult failure = result.Results.Single(r => !r.Valid);
        Assert.IsNotNull(failure.Location);
        Assert.AreEqual(ExpectedSchemaLocation + "/type", failure.Location.Value.SchemaLocation.ToString());
        Assert.AreEqual("#/type", failure.Location.Value.ValidationLocation.ToString());
        Assert.AreEqual("#", failure.Location.Value.DocumentLocation.ToString());
    }
}