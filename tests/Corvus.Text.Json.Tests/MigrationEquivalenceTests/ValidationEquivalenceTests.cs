// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests.MigrationEquivalenceTests;

using Corvus.Json;
using Xunit;

using V4 = MigrationModels.V4;
using V5 = MigrationModels.V5;

/// <summary>
/// Verifies that V4 <c>Validate()</c> and V5 <c>EvaluateSchema()</c> agree on valid/invalid instances.
/// </summary>
/// <remarks>
/// <para>V4: <c>entity.Validate(ValidationContext.ValidContext, ValidationLevel.Flag).IsValid</c></para>
/// <para>V5: <c>entity.EvaluateSchema()</c> returns <c>bool</c></para>
/// </remarks>
public class ValidationEquivalenceTests
{
    [Fact]
    public void V4_ValidPerson_IsValid()
    {
        var v4 = V4.MigrationPerson.Parse("""{"name":"Jo","age":30}""");
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void V4_ValidPerson_IsValid_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse("""{"name":"Jo","age":30}""");
        V4.MigrationPerson v4 = parsedV4.Instance;
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void V5_ValidPerson_IsValid()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse("""{"name":"Jo","age":30}""");
        V5.MigrationPerson v5 = parsedV5.RootElement;
        Assert.True(v5.EvaluateSchema());
    }

    [Fact]
    public void V4_MissingRequiredProperty_IsInvalid()
    {
        // Missing required "name"
        var v4 = V4.MigrationPerson.Parse("""{"age":30}""");
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void V4_MissingRequiredProperty_IsInvalid_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse("""{"age":30}""");
        V4.MigrationPerson v4 = parsedV4.Instance;
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void V5_MissingRequiredProperty_IsInvalid()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse("""{"age":30}""");
        V5.MigrationPerson v5 = parsedV5.RootElement;
        Assert.False(v5.EvaluateSchema());
    }

    [Fact]
    public void V4_InvalidRange_IsInvalid()
    {
        // Age > maximum (150)
        var v4 = V4.MigrationPerson.Parse("""{"name":"Jo","age":200}""");
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void V4_InvalidRange_IsInvalid_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse("""{"name":"Jo","age":200}""");
        V4.MigrationPerson v4 = parsedV4.Instance;
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void V5_InvalidRange_IsInvalid()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse("""{"name":"Jo","age":200}""");
        V5.MigrationPerson v5 = parsedV5.RootElement;
        Assert.False(v5.EvaluateSchema());
    }

    [Fact]
    public void V4_ValidEnum_IsValid()
    {
        var v4 = V4.MigrationStatusEnum.Parse("\"active\"");
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void V4_ValidEnum_IsValid_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationStatusEnum>.Parse("\"active\"");
        V4.MigrationStatusEnum v4 = parsedV4.Instance;
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void V5_ValidEnum_IsValid()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"active\"");
        V5.MigrationStatusEnum v5 = parsedV5.RootElement;
        Assert.True(v5.EvaluateSchema());
    }

    [Fact]
    public void V4_InvalidEnum_IsInvalid()
    {
        var v4 = V4.MigrationStatusEnum.Parse("\"unknown\"");
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void V4_InvalidEnum_IsInvalid_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationStatusEnum>.Parse("\"unknown\"");
        V4.MigrationStatusEnum v4 = parsedV4.Instance;
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void V5_InvalidEnum_IsInvalid()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"unknown\"");
        V5.MigrationStatusEnum v5 = parsedV5.RootElement;
        Assert.False(v5.EvaluateSchema());
    }

    [Fact]
    public void V4_ValidIntVector_IsValid()
    {
        var v4 = V4.MigrationIntVector.Parse("""[1,2,3]""");
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void V4_ValidIntVector_IsValid_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationIntVector>.Parse("""[1,2,3]""");
        V4.MigrationIntVector v4 = parsedV4.Instance;
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void V5_ValidIntVector_IsValid()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationIntVector>.Parse("""[1,2,3]""");
        V5.MigrationIntVector v5 = parsedV5.RootElement;
        Assert.True(v5.EvaluateSchema());
    }

    [Fact]
    public void V4_IntVectorWrongSize_IsInvalid()
    {
        // minItems: 3, maxItems: 3 — only 2 elements
        var v4 = V4.MigrationIntVector.Parse("""[1,2]""");
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void V4_IntVectorWrongSize_IsInvalid_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationIntVector>.Parse("""[1,2]""");
        V4.MigrationIntVector v4 = parsedV4.Instance;
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void V5_IntVectorWrongSize_IsInvalid()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationIntVector>.Parse("""[1,2]""");
        V5.MigrationIntVector v5 = parsedV5.RootElement;
        Assert.False(v5.EvaluateSchema());
    }

    [Fact]
    public void V4_ValidNestedObject_IsValid()
    {
        var v4 = V4.MigrationNested.Parse("""{"name":"Jo","address":{"street":"Main St","city":"NY"}}""");
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void V4_ValidNestedObject_IsValid_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationNested>.Parse("""{"name":"Jo","address":{"street":"Main St","city":"NY"}}""");
        V4.MigrationNested v4 = parsedV4.Instance;
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void V5_ValidNestedObject_IsValid()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationNested>.Parse("""{"name":"Jo","address":{"street":"Main St","city":"NY"}}""");
        V5.MigrationNested v5 = parsedV5.RootElement;
        Assert.True(v5.EvaluateSchema());
    }

    [Fact]
    public void V4_InvalidZipCodePattern_IsInvalid()
    {
        var v4 = V4.MigrationNested.Parse("""{"name":"Jo","address":{"street":"Main St","city":"NY","zipCode":"ABC"}}""");
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void V4_InvalidZipCodePattern_IsInvalid_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationNested>.Parse("""{"name":"Jo","address":{"street":"Main St","city":"NY","zipCode":"ABC"}}""");
        V4.MigrationNested v4 = parsedV4.Instance;
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void V5_InvalidZipCodePattern_IsInvalid()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationNested>.Parse("""{"name":"Jo","address":{"street":"Main St","city":"NY","zipCode":"ABC"}}""");
        V5.MigrationNested v5 = parsedV5.RootElement;
        Assert.False(v5.EvaluateSchema());
    }

    [Fact]
    public void V4_ValidTuple_IsValid()
    {
        var v4 = V4.MigrationTuple.Parse("""["hello",42,true]""");
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void V4_ValidTuple_IsValid_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationTuple>.Parse("""["hello",42,true]""");
        V4.MigrationTuple v4 = parsedV4.Instance;
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void V5_ValidTuple_IsValid()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationTuple>.Parse("""["hello",42,true]""");
        V5.MigrationTuple v5 = parsedV5.RootElement;
        Assert.True(v5.EvaluateSchema());
    }

    [Fact]
    public void V4_TupleExtraItems_IsInvalid()
    {
        // items: false — no additional items allowed
        var v4 = V4.MigrationTuple.Parse("""["hello",42,true,"extra"]""");
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void V4_TupleExtraItems_IsInvalid_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationTuple>.Parse("""["hello",42,true,"extra"]""");
        V4.MigrationTuple v4 = parsedV4.Instance;
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void V5_TupleExtraItems_IsInvalid()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationTuple>.Parse("""["hello",42,true,"extra"]""");
        V5.MigrationTuple v5 = parsedV5.RootElement;
        Assert.False(v5.EvaluateSchema());
    }

    [Fact]
    public void BothEngines_ValidPerson_BothValid()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse("""{"name":"Jo","age":30}""");
        V4.MigrationPerson v4 = parsedV4.Instance;

        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse("""{"name":"Jo","age":30}""");
        V5.MigrationPerson v5 = parsedV5.RootElement;

        ValidationContext v4Result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.Equal(v4Result.IsValid, v5.EvaluateSchema());
    }

    [Fact]
    public void BothEngines_MissingRequiredProperty_BothInvalid()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse("""{"age":30}""");
        V4.MigrationPerson v4 = parsedV4.Instance;

        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse("""{"age":30}""");
        V5.MigrationPerson v5 = parsedV5.RootElement;

        ValidationContext v4Result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.Equal(v4Result.IsValid, v5.EvaluateSchema());
    }

    [Fact]
    public void V4_DetailedValidation_HasResults()
    {
        using var parsedV4 =
            Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse("""{"age":200}""");
        V4.MigrationPerson v4 = parsedV4.Instance;

        ValidationContext result = v4.Validate(
            ValidationContext.ValidContext,
            ValidationLevel.Detailed);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Results);

        // All reported failures should be non-valid
        foreach (ValidationResult r in result.Results)
        {
            if (!r.Valid)
            {
                Assert.NotNull(r.Message);
                Assert.NotEmpty(r.Message);
            }
        }
    }

    [Fact]
    public void V5_DetailedValidation_HasResults()
    {
        using var parsedV5 =
            Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse("""{"age":200}""");
        V5.MigrationPerson v5 = parsedV5.RootElement;

        using var collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);

        bool isValid = v5.EvaluateSchema(collector);

        Assert.False(isValid);
        Assert.True(collector.GetResultCount() > 0);

        // All reported failures should have a message
        foreach (JsonSchemaResultsCollector.Result r in collector.EnumerateResults())
        {
            if (!r.IsMatch)
            {
                Assert.False(r.Message.IsEmpty);
            }
        }
    }

    [Fact]
    public void V4_DetailedValidation_ValidInstance_NoFailures()
    {
        using var parsedV4 =
            Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse("""{"name":"Jo","age":30}""");
        V4.MigrationPerson v4 = parsedV4.Instance;

        ValidationContext result = v4.Validate(
            ValidationContext.ValidContext,
            ValidationLevel.Detailed);

        Assert.True(result.IsValid);

        // No failure results
        foreach (ValidationResult r in result.Results)
        {
            Assert.True(r.Valid);
        }
    }

    [Fact]
    public void V5_DetailedValidation_ValidInstance_NoFailures()
    {
        using var parsedV5 =
            Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse("""{"name":"Jo","age":30}""");
        V5.MigrationPerson v5 = parsedV5.RootElement;

        using var collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);

        bool isValid = v5.EvaluateSchema(collector);

        Assert.True(isValid);

        // No failure results
        foreach (JsonSchemaResultsCollector.Result r in collector.EnumerateResults())
        {
            Assert.True(r.IsMatch);
        }
    }

    [Fact]
    public void BothEngines_DetailedValidation_BothHaveFailureResults()
    {
        using var parsedV4 =
            Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse("""{"age":200}""");
        V4.MigrationPerson v4 = parsedV4.Instance;

        using var parsedV5 =
            Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse("""{"age":200}""");
        V5.MigrationPerson v5 = parsedV5.RootElement;

        // V4
        ValidationContext v4Result = v4.Validate(
            ValidationContext.ValidContext,
            ValidationLevel.Detailed);

        // V5
        using var collector =
            JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        bool v5IsValid = v5.EvaluateSchema(collector);

        // Both should be invalid
        Assert.False(v4Result.IsValid);
        Assert.False(v5IsValid);

        // Both should have failure results
        Assert.Contains(v4Result.Results, r => !r.Valid);

        bool v5HasFailure = false;
        foreach (JsonSchemaResultsCollector.Result r in collector.EnumerateResults())
        {
            if (!r.IsMatch)
            {
                v5HasFailure = true;
                break;
            }
        }

        Assert.True(v5HasFailure);
    }
}