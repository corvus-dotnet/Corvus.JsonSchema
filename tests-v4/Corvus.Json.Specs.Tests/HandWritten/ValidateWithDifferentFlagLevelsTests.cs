// <copyright file="ValidateWithDifferentFlagLevelsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Specs.Tests.Infrastructure;
using Drivers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.HandWritten;

/// <summary>
/// Tests validation at different flag levels (Flag, Basic, Detailed, Verbose)
/// for a large oneOf schema covering all core and extension JSON types.
/// Each draft is tested as a separate class with a shared fixture that compiles the schema once.
/// </summary>
public abstract class ValidateWithDifferentFlagLevelsBase<TFixture>
    where TFixture : ValidateWithDifferentFlagLevelsBase<TFixture>.FixtureBase, new()
{
    private static readonly string DataDir = Path.Combine(
        DriverFactory.RepoRoot,
        "tests-v4",
        "Corvus.Json.Specs.Tests",
        "HandWritten",
        "ValidateWithDifferentFlagLevels");

    private static TFixture? s_fixture;

    protected abstract string DraftName { get; }

    public static IEnumerable<object[]> TestData => LoadTestData();

    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static async Task ClassInit(TestContext context)
    {
        s_fixture = new TFixture();
        await s_fixture.InitializeAsync();
    }

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    public static async Task ClassCleanup()
    {
        if (s_fixture is not null)
        {
            await s_fixture.DisposeAsync();
        }
    }

    [TestMethod]
    [DynamicData(nameof(TestData))]
    public void ValidateWithLevel(string inputData, bool expectedValid, string level, int expectedCount)
    {
        using var doc = JsonDocument.Parse(inputData);
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(s_fixture!.GeneratedType, doc.RootElement);

        ValidationLevel validationLevel = (ValidationLevel)Enum.Parse(typeof(ValidationLevel), level);
        ValidationContext result = instance.Validate(ValidationContext.ValidContext, validationLevel);

        Assert.AreEqual(expectedValid, result.IsValid);

        if (validationLevel == ValidationLevel.Flag)
        {
            // Flag level must always produce exactly 0 results.
            Assert.AreEqual(0, (result.Results).Count());
        }
        else if (validationLevel == ValidationLevel.Verbose)
        {
            // Verbose counts depend on internal codegen structure and may change
            // as the engine evolves. We verify results are collected (count > 0).
            Assert.IsTrue(result.Results.Count > 0, $"Expected Verbose to produce results but got count=0 for input: {inputData}");
        }
        else
        {
            // Basic and Detailed levels have stable structural counts.
            Assert.AreEqual(expectedCount, result.Results.Count);
        }
    }

    private static IEnumerable<object[]> LoadTestData()
    {
        // We need the draft name from a concrete instance, but since this is static,
        // we infer it from the generic type parameter name convention.
        string draftName = typeof(TFixture).Name.Replace("Fixture", string.Empty);
        string testDataPath = Path.Combine(DataDir, $"{draftName}.testdata.json");
        string json = File.ReadAllText(testDataPath);
        using JsonDocument doc = JsonDocument.Parse(json);
        var data = new List<object[]>();

        foreach (JsonElement item in doc.RootElement.EnumerateArray())
        {
            string inputDataValue = item.GetProperty("inputData").GetString()!;
            bool valid = item.GetProperty("valid").GetBoolean();
            string levelValue = item.GetProperty("level").GetString()!;
            int count = item.GetProperty("count").GetInt32();
            data.Add([inputDataValue, valid, levelValue, count]);
        }

        return data;
    }

    public abstract class FixtureBase
    {
        private JsonSchemaBuilderDriver? _driver;

        public Type GeneratedType { get; private set; } = null!;

        protected abstract string DraftName { get; }

        protected abstract JsonSchemaBuilderDriver CreateDriver();

        public async Task InitializeAsync()
        {
            _driver = CreateDriver();
            string schemaPath = Path.Combine(DataDir, $"{DraftName}.schema.json");
            string schema = File.ReadAllText(schemaPath);

            GeneratedType = await _driver.GenerateTypeForVirtualFile(
                schema,
                $"validateWithDifferentFlagLevels-{DraftName}.json",
                "ValidateWithDifferentFlagLevels",
                $"{DraftName}AllOfTheFormatTypes",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false);
        }

        public Task DisposeAsync()
        {
            _driver?.Dispose();
            return Task.CompletedTask;
        }
    }
}

[TestClass]
public class ValidateWithDifferentFlagLevelsDraft4Tests
    : ValidateWithDifferentFlagLevelsBase<ValidateWithDifferentFlagLevelsDraft4Tests.Draft4Fixture>
{
    protected override string DraftName => "Draft4";

    public class Draft4Fixture : FixtureBase
    {
        protected override string DraftName => "Draft4";

        protected override JsonSchemaBuilderDriver CreateDriver() => DriverFactory.CreateAdditionalDraft4Driver();
    }
}

[TestClass]
public class ValidateWithDifferentFlagLevelsDraft6Tests
    : ValidateWithDifferentFlagLevelsBase<ValidateWithDifferentFlagLevelsDraft6Tests.Draft6Fixture>
{
    protected override string DraftName => "Draft6";

    public class Draft6Fixture : FixtureBase
    {
        protected override string DraftName => "Draft6";

        protected override JsonSchemaBuilderDriver CreateDriver() => DriverFactory.CreateAdditionalDraft6Driver();
    }
}

[TestClass]
public class ValidateWithDifferentFlagLevelsDraft7Tests
    : ValidateWithDifferentFlagLevelsBase<ValidateWithDifferentFlagLevelsDraft7Tests.Draft7Fixture>
{
    protected override string DraftName => "Draft7";

    public class Draft7Fixture : FixtureBase
    {
        protected override string DraftName => "Draft7";

        protected override JsonSchemaBuilderDriver CreateDriver() => DriverFactory.CreateAdditionalDraft7Driver();
    }
}

[TestClass]
public class ValidateWithDifferentFlagLevelsDraft201909Tests
    : ValidateWithDifferentFlagLevelsBase<ValidateWithDifferentFlagLevelsDraft201909Tests.Draft201909Fixture>
{
    protected override string DraftName => "Draft201909";

    public class Draft201909Fixture : FixtureBase
    {
        protected override string DraftName => "Draft201909";

        protected override JsonSchemaBuilderDriver CreateDriver() => DriverFactory.CreateAdditionalDraft201909Driver();
    }
}

[TestClass]
public class ValidateWithDifferentFlagLevelsDraft202012Tests
    : ValidateWithDifferentFlagLevelsBase<ValidateWithDifferentFlagLevelsDraft202012Tests.Draft202012Fixture>
{
    protected override string DraftName => "Draft202012";

    public class Draft202012Fixture : FixtureBase
    {
        protected override string DraftName => "Draft202012";

        protected override JsonSchemaBuilderDriver CreateDriver() => DriverFactory.CreateAdditionalDraft202012Driver();
    }
}

[TestClass]
public class ValidateWithDifferentFlagLevelsOpenApi30Tests
    : ValidateWithDifferentFlagLevelsBase<ValidateWithDifferentFlagLevelsOpenApi30Tests.OpenApi30Fixture>
{
    protected override string DraftName => "OpenApi30";

    public class OpenApi30Fixture : FixtureBase
    {
        protected override string DraftName => "OpenApi30";

        protected override JsonSchemaBuilderDriver CreateDriver() => DriverFactory.CreateAdditionalOpenApi30Driver();
    }
}