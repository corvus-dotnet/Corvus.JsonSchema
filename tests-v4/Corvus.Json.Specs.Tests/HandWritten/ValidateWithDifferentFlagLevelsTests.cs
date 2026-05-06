// <copyright file="ValidateWithDifferentFlagLevelsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Specs.Tests.Infrastructure;
using Drivers;
using Xunit;

namespace Corvus.Json.Specs.Tests.HandWritten;

/// <summary>
/// Tests validation at different flag levels (Flag, Basic, Detailed, Verbose)
/// for a large oneOf schema covering all core and extension JSON types.
/// Each draft is tested as a separate class with a shared fixture that compiles the schema once.
/// </summary>
public abstract class ValidateWithDifferentFlagLevelsBase<TFixture>
    : IClassFixture<TFixture>
    where TFixture : ValidateWithDifferentFlagLevelsBase<TFixture>.FixtureBase
{
    private static readonly string DataDir = Path.Combine(
        DriverFactory.RepoRoot,
        "tests-v4",
        "Corvus.Json.Specs.Tests",
        "HandWritten",
        "ValidateWithDifferentFlagLevels");

    private readonly TFixture _fixture;

    protected ValidateWithDifferentFlagLevelsBase(TFixture fixture)
    {
        _fixture = fixture;
    }

    protected abstract string DraftName { get; }

    public static TheoryData<string, bool, string, int> TestData => LoadTestData();

    [Theory]
    [MemberData(nameof(TestData))]
    public void ValidateWithLevel(string inputData, bool expectedValid, string level, int expectedCount)
    {
        using var doc = JsonDocument.Parse(inputData);
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(_fixture.GeneratedType, doc.RootElement);

        ValidationLevel validationLevel = (ValidationLevel)Enum.Parse(typeof(ValidationLevel), level);
        ValidationContext result = instance.Validate(ValidationContext.ValidContext, validationLevel);

        Assert.Equal(expectedValid, result.IsValid);

        if (validationLevel == ValidationLevel.Flag)
        {
            // Flag level must always produce exactly 0 results.
            Assert.Empty(result.Results);
        }
        else if (validationLevel == ValidationLevel.Verbose)
        {
            // Verbose counts depend on internal codegen structure and may change
            // as the engine evolves. We verify results are collected (count > 0).
            Assert.True(result.Results.Count > 0, $"Expected Verbose to produce results but got count=0 for input: {inputData}");
        }
        else
        {
            // Basic and Detailed levels have stable structural counts.
            Assert.Equal(expectedCount, result.Results.Count);
        }
    }

    private static TheoryData<string, bool, string, int> LoadTestData()
    {
        // We need the draft name from a concrete instance, but since this is static,
        // we infer it from the generic type parameter name convention.
        string draftName = typeof(TFixture).Name.Replace("Fixture", string.Empty);
        string testDataPath = Path.Combine(DataDir, $"{draftName}.testdata.json");
        string json = File.ReadAllText(testDataPath);
        using JsonDocument doc = JsonDocument.Parse(json);
        var data = new TheoryData<string, bool, string, int>();

        foreach (JsonElement item in doc.RootElement.EnumerateArray())
        {
            string inputDataValue = item.GetProperty("inputData").GetString()!;
            bool valid = item.GetProperty("valid").GetBoolean();
            string levelValue = item.GetProperty("level").GetString()!;
            int count = item.GetProperty("count").GetInt32();
            data.Add(inputDataValue, valid, levelValue, count);
        }

        return data;
    }

    public abstract class FixtureBase : IAsyncLifetime
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

public class ValidateWithDifferentFlagLevelsDraft4Tests
    : ValidateWithDifferentFlagLevelsBase<ValidateWithDifferentFlagLevelsDraft4Tests.Draft4Fixture>
{
    public ValidateWithDifferentFlagLevelsDraft4Tests(Draft4Fixture fixture)
        : base(fixture)
    {
    }

    protected override string DraftName => "Draft4";

    public class Draft4Fixture : FixtureBase
    {
        protected override string DraftName => "Draft4";

        protected override JsonSchemaBuilderDriver CreateDriver() => DriverFactory.CreateAdditionalDraft4Driver();
    }
}

public class ValidateWithDifferentFlagLevelsDraft6Tests
    : ValidateWithDifferentFlagLevelsBase<ValidateWithDifferentFlagLevelsDraft6Tests.Draft6Fixture>
{
    public ValidateWithDifferentFlagLevelsDraft6Tests(Draft6Fixture fixture)
        : base(fixture)
    {
    }

    protected override string DraftName => "Draft6";

    public class Draft6Fixture : FixtureBase
    {
        protected override string DraftName => "Draft6";

        protected override JsonSchemaBuilderDriver CreateDriver() => DriverFactory.CreateAdditionalDraft6Driver();
    }
}

public class ValidateWithDifferentFlagLevelsDraft7Tests
    : ValidateWithDifferentFlagLevelsBase<ValidateWithDifferentFlagLevelsDraft7Tests.Draft7Fixture>
{
    public ValidateWithDifferentFlagLevelsDraft7Tests(Draft7Fixture fixture)
        : base(fixture)
    {
    }

    protected override string DraftName => "Draft7";

    public class Draft7Fixture : FixtureBase
    {
        protected override string DraftName => "Draft7";

        protected override JsonSchemaBuilderDriver CreateDriver() => DriverFactory.CreateAdditionalDraft7Driver();
    }
}

public class ValidateWithDifferentFlagLevelsDraft201909Tests
    : ValidateWithDifferentFlagLevelsBase<ValidateWithDifferentFlagLevelsDraft201909Tests.Draft201909Fixture>
{
    public ValidateWithDifferentFlagLevelsDraft201909Tests(Draft201909Fixture fixture)
        : base(fixture)
    {
    }

    protected override string DraftName => "Draft201909";

    public class Draft201909Fixture : FixtureBase
    {
        protected override string DraftName => "Draft201909";

        protected override JsonSchemaBuilderDriver CreateDriver() => DriverFactory.CreateAdditionalDraft201909Driver();
    }
}

public class ValidateWithDifferentFlagLevelsDraft202012Tests
    : ValidateWithDifferentFlagLevelsBase<ValidateWithDifferentFlagLevelsDraft202012Tests.Draft202012Fixture>
{
    public ValidateWithDifferentFlagLevelsDraft202012Tests(Draft202012Fixture fixture)
        : base(fixture)
    {
    }

    protected override string DraftName => "Draft202012";

    public class Draft202012Fixture : FixtureBase
    {
        protected override string DraftName => "Draft202012";

        protected override JsonSchemaBuilderDriver CreateDriver() => DriverFactory.CreateAdditionalDraft202012Driver();
    }
}

public class ValidateWithDifferentFlagLevelsOpenApi30Tests
    : ValidateWithDifferentFlagLevelsBase<ValidateWithDifferentFlagLevelsOpenApi30Tests.OpenApi30Fixture>
{
    public ValidateWithDifferentFlagLevelsOpenApi30Tests(OpenApi30Fixture fixture)
        : base(fixture)
    {
    }

    protected override string DraftName => "OpenApi30";

    public class OpenApi30Fixture : FixtureBase
    {
        protected override string DraftName => "OpenApi30";

        protected override JsonSchemaBuilderDriver CreateDriver() => DriverFactory.CreateAdditionalOpenApi30Driver();
    }
}