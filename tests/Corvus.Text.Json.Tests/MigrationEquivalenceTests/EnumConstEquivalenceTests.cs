// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests.MigrationEquivalenceTests;

using Corvus.Json;
using Xunit;

using V4 = MigrationModels.V4;
using V5 = MigrationModels.V5;

/// <summary>
/// Verifies that V4 and V5 enum value handling produces equivalent results.
/// </summary>
/// <remarks>
/// <para>V4: <c>MigrationStatusEnum.EnumValues.Active</c> (public named constants)</para>
/// <para>V5: Constants are private; use <c>ParseValue()</c> with the literal value</para>
/// </remarks>
public class EnumConstEquivalenceTests
{
    [Fact]
    public void V4_ParseValidEnumValue()
    {
        var v4 = V4.MigrationStatusEnum.Parse("\"active\"");
        Assert.Equal(System.Text.Json.JsonValueKind.String, v4.ValueKind);
        Assert.Equal("active", (string)v4);
    }

    [Fact]
    public void V4_ParseValidEnumValue_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationStatusEnum>.Parse("\"active\"");
        V4.MigrationStatusEnum v4 = parsedV4.Instance;
        Assert.Equal(System.Text.Json.JsonValueKind.String, v4.ValueKind);
        Assert.Equal("active", (string)v4);
    }

    [Fact]
    public void V5_ParseValidEnumValue()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"active\"");
        V5.MigrationStatusEnum v5 = parsedV5.RootElement;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.String, v5.ValueKind);
        Assert.Equal("active", (string)v5);
    }

    [Fact]
    public void V4_AllEnumValuesValid()
    {
        foreach (string enumValue in new[] { "active", "inactive", "pending" })
        {
            var v4 = V4.MigrationStatusEnum.Parse($"\"{enumValue}\"");
            ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
            Assert.True(result.IsValid, $"Expected '{enumValue}' to be valid in V4");
        }
    }

    [Fact]
    public void V4_AllEnumValuesValid_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        foreach (string enumValue in new[] { "active", "inactive", "pending" })
        {
            using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationStatusEnum>.Parse($"\"{enumValue}\"");
            V4.MigrationStatusEnum v4 = parsedV4.Instance;
            ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
            Assert.True(result.IsValid, $"Expected '{enumValue}' to be valid in V4");
        }
    }

    [Fact]
    public void V5_AllEnumValuesValid()
    {
        foreach (string enumValue in new[] { "active", "inactive", "pending" })
        {
            using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse($"\"{enumValue}\"");
            V5.MigrationStatusEnum v5 = parsedV5.RootElement;
            Assert.True(v5.EvaluateSchema(), $"Expected '{enumValue}' to be valid in V5");
        }
    }

    [Fact]
    public void V4_InvalidEnumValue()
    {
        var v4 = V4.MigrationStatusEnum.Parse("\"unknown\"");
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void V4_InvalidEnumValue_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationStatusEnum>.Parse("\"unknown\"");
        V4.MigrationStatusEnum v4 = parsedV4.Instance;
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void V5_InvalidEnumValue()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"unknown\"");
        V5.MigrationStatusEnum v5 = parsedV5.RootElement;
        Assert.False(v5.EvaluateSchema());
    }

    [Fact]
    public void V4_ExtractStringValue()
    {
        var v4 = V4.MigrationStatusEnum.Parse("\"pending\"");
        string extracted = (string)v4;
        Assert.Equal("pending", extracted);
    }

    [Fact]
    public void V4_ExtractStringValue_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationStatusEnum>.Parse("\"pending\"");
        V4.MigrationStatusEnum v4 = parsedV4.Instance;
        string extracted = (string)v4;
        Assert.Equal("pending", extracted);
    }

    [Fact]
    public void V5_ExtractStringValue()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"pending\"");
        V5.MigrationStatusEnum v5 = parsedV5.RootElement;
        string extracted = (string)v5;
        Assert.Equal("pending", extracted);
    }

    [Fact]
    public void V4_WrongType_IsInvalid()
    {
        var v4 = V4.MigrationStatusEnum.Parse("""42""");
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void V4_WrongType_IsInvalid_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationStatusEnum>.Parse("""42""");
        V4.MigrationStatusEnum v4 = parsedV4.Instance;
        ValidationContext result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void V5_WrongType_IsInvalid()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("""42""");
        V5.MigrationStatusEnum v5 = parsedV5.RootElement;
        Assert.False(v5.EvaluateSchema());
    }

    [Fact]
    public void BothEngines_ParseValidEnum_SameResult()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationStatusEnum>.Parse("\"active\"");
        V4.MigrationStatusEnum v4 = parsedV4.Instance;

        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"active\"");
        V5.MigrationStatusEnum v5 = parsedV5.RootElement;

        Assert.Equal((string)v4, (string)v5);
    }

    [Fact]
    public void BothEngines_InvalidEnum_SameValidationResult()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationStatusEnum>.Parse("\"unknown\"");
        V4.MigrationStatusEnum v4 = parsedV4.Instance;
        ValidationContext v4Result = v4.Validate(ValidationContext.ValidContext, ValidationLevel.Flag);

        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"unknown\"");
        V5.MigrationStatusEnum v5 = parsedV5.RootElement;

        Assert.Equal(v4Result.IsValid, v5.EvaluateSchema());
    }

    [Fact]
    public void V4_NamedEnumValues()
    {
        // V4: named static properties for each enum value in the EnumValues nested class.
        Assert.Equal("active", (string)V4.MigrationStatusEnum.EnumValues.Active);
        Assert.Equal("inactive", (string)V4.MigrationStatusEnum.EnumValues.Inactive);
        Assert.Equal("pending", (string)V4.MigrationStatusEnum.EnumValues.Pending);
    }

    [Fact]
    public void V5_NamedEnumValues()
    {
        // V5: named static properties for each enum value in the EnumValues nested class.
        Assert.Equal("active", (string)V5.MigrationStatusEnum.EnumValues.Active);
        Assert.Equal("inactive", (string)V5.MigrationStatusEnum.EnumValues.Inactive);
        Assert.Equal("pending", (string)V5.MigrationStatusEnum.EnumValues.Pending);
    }

    [Fact]
    public void BothEngines_NamedEnumValues_SameResult()
    {
        Assert.Equal(
            (string)V4.MigrationStatusEnum.EnumValues.Active,
            (string)V5.MigrationStatusEnum.EnumValues.Active);
        Assert.Equal(
            (string)V4.MigrationStatusEnum.EnumValues.Inactive,
            (string)V5.MigrationStatusEnum.EnumValues.Inactive);
        Assert.Equal(
            (string)V4.MigrationStatusEnum.EnumValues.Pending,
            (string)V5.MigrationStatusEnum.EnumValues.Pending);
    }

    [Fact]
    public void V4_EnumValuesUtf8()
    {
        // V4 Utf8 values include the JSON quotes.
        Assert.True(V4.MigrationStatusEnum.EnumValues.ActiveUtf8.SequenceEqual("\"active\""u8));
        Assert.True(V4.MigrationStatusEnum.EnumValues.InactiveUtf8.SequenceEqual("\"inactive\""u8));
        Assert.True(V4.MigrationStatusEnum.EnumValues.PendingUtf8.SequenceEqual("\"pending\""u8));
    }

    [Fact]
    public void V5_EnumValuesUtf8()
    {
        // V5 Utf8 values are the raw unquoted bytes.
        Assert.True(V5.MigrationStatusEnum.EnumValues.ActiveUtf8.SequenceEqual("active"u8));
        Assert.True(V5.MigrationStatusEnum.EnumValues.InactiveUtf8.SequenceEqual("inactive"u8));
        Assert.True(V5.MigrationStatusEnum.EnumValues.PendingUtf8.SequenceEqual("pending"u8));
    }

    [Fact]
    public void V5_EnumValues_AreValid()
    {
        // Each named enum value should pass schema validation.
        Assert.True(V5.MigrationStatusEnum.EnumValues.Active.EvaluateSchema());
        Assert.True(V5.MigrationStatusEnum.EnumValues.Inactive.EvaluateSchema());
        Assert.True(V5.MigrationStatusEnum.EnumValues.Pending.EvaluateSchema());
    }

    [Fact]
    public void V4_EnumMatch()
    {
        var v4 = V4.MigrationStatusEnum.Parse("\"active\"");
        string result = v4.Match(
            () => "is-active",
            () => "is-inactive",
            () => "is-pending",
            () => "unknown");
        Assert.Equal("is-active", result);
    }

    [Fact]
    public void V4_EnumMatch_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationStatusEnum>.Parse("\"active\"");
        V4.MigrationStatusEnum v4 = parsedV4.Instance;
        string result = v4.Match(
            () => "is-active",
            () => "is-inactive",
            () => "is-pending",
            () => "unknown");
        Assert.Equal("is-active", result);
    }

    [Fact]
    public void V5_EnumMatch()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"active\"");
        V5.MigrationStatusEnum v5 = parsedV5.RootElement;
        string result = v5.Match(
            () => "is-active",
            () => "is-inactive",
            () => "is-pending",
            () => "unknown");
        Assert.Equal("is-active", result);
    }

    [Fact]
    public void V4_GetString()
    {
        var v4 = V4.MigrationStatusEnum.Parse("\"active\"");
        string? value = v4.GetString();
        Assert.Equal("active", value);
    }

    [Fact]
    public void V4_GetString_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationStatusEnum>.Parse("\"active\"");
        V4.MigrationStatusEnum v4 = parsedV4.Instance;
        string? value = v4.GetString();
        Assert.Equal("active", value);
    }

    [Fact]
    public void V5_GetString_ViaExplicitCast()
    {
        // V5: use explicit cast to extract string — equivalent to V4 GetString().
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"active\"");
        V5.MigrationStatusEnum v5 = parsedV5.RootElement;
        string value = (string)v5;
        Assert.Equal("active", value);
    }

    [Fact]
    public void V4_TryGetString()
    {
        var v4 = V4.MigrationStatusEnum.Parse("\"active\"");
        Assert.True(v4.TryGetString(out string? value));
        Assert.Equal("active", value);
    }

    [Fact]
    public void V4_TryGetString_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationStatusEnum>.Parse("\"active\"");
        V4.MigrationStatusEnum v4 = parsedV4.Instance;
        Assert.True(v4.TryGetString(out string? value));
        Assert.Equal("active", value);
    }

    [Fact]
    public void V4_ConstructFromString()
    {
        // V4: implicit operator converts string to enum type.
        var v4 = (V4.MigrationStatusEnum)"active";
        Assert.Equal("active", (string)v4);
    }

    [Fact]
    public void V5_ConstructFromString()
    {
        // V5: use Parse() to construct from a JSON string literal.
        // Alternatively, use the named EnumValues constants.
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"active\"");
        V5.MigrationStatusEnum v5 = parsedV5.RootElement;
        Assert.Equal("active", (string)v5);
    }

    [Fact]
    public void V4_EqualsString()
    {
        var v4 = V4.MigrationStatusEnum.Parse("\"active\"");
        Assert.True(v4.EqualsString("active"));
        Assert.False(v4.EqualsString("inactive"));
    }

    [Fact]
    public void V4_EqualsString_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationStatusEnum>.Parse("\"active\"");
        V4.MigrationStatusEnum v4 = parsedV4.Instance;
        Assert.True(v4.EqualsString("active"));
        Assert.False(v4.EqualsString("inactive"));
    }

    [Fact]
    public void V5_EqualsString_ViaEquals()
    {
        // V5: use Equals<T>() for comparison — equivalent to V4 EqualsString().
        using var parsedV5A = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"active\"");
        using var parsedV5B = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"active\"");
        using var parsedV5C = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"inactive\"");

        Assert.True(parsedV5A.RootElement.Equals(parsedV5B.RootElement));
        Assert.False(parsedV5A.RootElement.Equals(parsedV5C.RootElement));
    }

    [Fact]
    public void V4_EnumMatchWithContext()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationStatusEnum>.Parse("\"inactive\"");
        V4.MigrationStatusEnum v4 = parsedV4.Instance;
        string result = v4.Match(
            42,
            ctx => $"active-{ctx}",
            ctx => $"inactive-{ctx}",
            ctx => $"pending-{ctx}",
            ctx => $"unknown-{ctx}");
        Assert.Equal("inactive-42", result);
    }

    [Fact]
    public void V4_EnumMatchWithoutContext()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationStatusEnum>.Parse("\"active\"");
        V4.MigrationStatusEnum v4 = parsedV4.Instance;
        string result = v4.Match(
            () => "is-active",
            () => "is-inactive",
            () => "is-pending",
            () => "unknown");
        Assert.Equal("is-active", result);
    }

    [Fact]
    public void V5_EnumMatchWithContext()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"inactive\"");
        V5.MigrationStatusEnum v5 = parsedV5.RootElement;
        string result = v5.Match(
            42,
            ctx => $"active-{ctx}",
            ctx => $"inactive-{ctx}",
            ctx => $"pending-{ctx}",
            ctx => $"unknown-{ctx}");
        Assert.Equal("inactive-42", result);
    }

    [Fact]
    public void BothEngines_EnumMatchWithContext_SameResult()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationStatusEnum>.Parse("\"pending\"");
        V4.MigrationStatusEnum v4 = parsedV4.Instance;

        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"pending\"");
        V5.MigrationStatusEnum v5 = parsedV5.RootElement;

        string v4Result = v4.Match(
            "ctx",
            ctx => $"active-{ctx}",
            ctx => $"inactive-{ctx}",
            ctx => $"pending-{ctx}",
            ctx => $"unknown-{ctx}");

        string v5Result = v5.Match(
            "ctx",
            ctx => $"active-{ctx}",
            ctx => $"inactive-{ctx}",
            ctx => $"pending-{ctx}",
            ctx => $"unknown-{ctx}");

        Assert.Equal(v4Result, v5Result);
    }

    [Fact]
    public void BothEngines_EnumMatchWithoutContext_SameResult()
    {
        string[] values = ["active", "inactive", "pending"];
        string[] expected = ["is-active", "is-inactive", "is-pending"];

        for (int i = 0; i < values.Length; i++)
        {
            using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationStatusEnum>.Parse($"\"{values[i]}\"");
            V4.MigrationStatusEnum v4 = parsedV4.Instance;
            string v4Result = v4.Match(
                () => "is-active",
                () => "is-inactive",
                () => "is-pending",
                () => "unknown");

            using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse($"\"{values[i]}\"");
            V5.MigrationStatusEnum v5 = parsedV5.RootElement;
            string v5Result = v5.Match(
                () => "is-active",
                () => "is-inactive",
                () => "is-pending",
                () => "unknown");

            Assert.Equal(expected[i], v4Result);
            Assert.Equal(v4Result, v5Result);
        }
    }

    [Fact]
    public void V5_EnumMatchDefault()
    {
        // An invalid enum value falls through to the default handler.
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"unknown\"");
        V5.MigrationStatusEnum v5 = parsedV5.RootElement;
        string result = v5.Match(
            () => "is-active",
            () => "is-inactive",
            () => "is-pending",
            () => "default");
        Assert.Equal("default", result);
    }

    [Fact]
    public void V5_EnumValueEquals_WithNamedConstants()
    {
        // ValueEquals compares the raw byte value against a named enum constant.
        V5.MigrationStatusEnum active = V5.MigrationStatusEnum.EnumValues.Active;
        Assert.True(active.ValueEquals("active"u8));
        Assert.False(active.ValueEquals("inactive"u8));
    }

    [Fact]
    public void V5_EqualityOperator()
    {
        using var parsedA = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"active\"");
        using var parsedB = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"active\"");
        using var parsedC = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"inactive\"");

        Assert.True(parsedA.RootElement == parsedB.RootElement);
        Assert.False(parsedA.RootElement == parsedC.RootElement);
        Assert.True(parsedA.RootElement != parsedC.RootElement);
        Assert.False(parsedA.RootElement != parsedB.RootElement);
    }

    [Fact]
    public void V5_EqualityOperator_WithNamedConstant()
    {
        V5.MigrationStatusEnum active = V5.MigrationStatusEnum.EnumValues.Active;
        using var parsed = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"active\"");

        Assert.True(active == parsed.RootElement);
        Assert.False(active != parsed.RootElement);
    }

    [Fact]
    public void V5_WriteTo()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"active\"");
        V5.MigrationStatusEnum v5 = parsedV5.RootElement;

        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (Corvus.Text.Json.Utf8JsonWriter writer = Corvus.Text.Json.Utf8JsonWriterCache.RentWriter(default, buffer))
        {
            v5.WriteTo(writer);
        }

        string json = System.Text.Encoding.UTF8.GetString(buffer.WrittenMemory.ToArray());
        Assert.Equal("\"active\"", json);
    }

    [Fact]
    public void V5_ToString()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"active\"");
        V5.MigrationStatusEnum v5 = parsedV5.RootElement;
        Assert.Equal("active", v5.ToString());
    }

    [Fact]
    public void V5_From_JsonElement()
    {
        // V5: construct from another IJsonElement<T> using From<T>.
        using var parsedJson = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse("\"active\"");
        var v5 = V5.MigrationStatusEnum.From(parsedJson.RootElement);
        Assert.Equal("active", (string)v5);
    }

    [Fact]
    public void BothEngines_ConstructFromString_SameResult()
    {
        // V4: implicit operator from string.
        var v4 = (V4.MigrationStatusEnum)"active";

        // V5: use named constant or parse.
        V5.MigrationStatusEnum v5 = V5.MigrationStatusEnum.EnumValues.Active;

        Assert.Equal((string)v4, (string)v5);
    }

    [Fact]
    public void V5_TryGetValue_String()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"active\"");
        V5.MigrationStatusEnum v5 = parsedV5.RootElement;
        Assert.True(v5.TryGetValue(out string? value));
        Assert.Equal("active", value);
    }

    [Fact]
    public void BothEngines_TryGetString_SameResult()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationStatusEnum>.Parse("\"active\"");
        V4.MigrationStatusEnum v4 = parsedV4.Instance;
        Assert.True(v4.TryGetString(out string? v4Value));

        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse("\"active\"");
        V5.MigrationStatusEnum v5 = parsedV5.RootElement;
        Assert.True(v5.TryGetValue(out string? v5Value));

        Assert.Equal(v4Value, v5Value);
    }

    [Fact]
    public void V5_EnumMatch_AllValues()
    {
        // Verify Match routes correctly for each enum value.
        string[] values = ["active", "inactive", "pending"];
        string[] expected = ["is-active", "is-inactive", "is-pending"];

        for (int i = 0; i < values.Length; i++)
        {
            using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse($"\"{values[i]}\"");
            V5.MigrationStatusEnum v5 = parsedV5.RootElement;
            string result = v5.Match(
                () => "is-active",
                () => "is-inactive",
                () => "is-pending",
                () => "unknown");
            Assert.Equal(expected[i], result);
        }
    }

    [Fact]
    public void V5_NamedEnumValues_MatchRoundTrip()
    {
        // Named enum values should match correctly through the Match method.
        string result = V5.MigrationStatusEnum.EnumValues.Active.Match(
            () => "is-active",
            () => "is-inactive",
            () => "is-pending",
            () => "unknown");
        Assert.Equal("is-active", result);
    }
}