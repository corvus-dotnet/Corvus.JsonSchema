// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests.MigrationEquivalenceTests;

using System.Buffers;
using Xunit;

using V4 = MigrationModels.V4;
using V5 = MigrationModels.V5;

/// <summary>
/// Verifies that V4 and V5 serialization produces equivalent JSON output.
/// </summary>
/// <remarks>
/// <para>V4: <c>entity.WriteTo(System.Text.Json.Utf8JsonWriter)</c></para>
/// <para>V5: <c>entity.WriteTo(Corvus.Text.Json.Utf8JsonWriter)</c></para>
/// <para>Both: <c>entity.ToString()</c> returns JSON</para>
/// </remarks>
public class SerializationEquivalenceTests
{
    private const string PersonJson = """{"name":"Jo","age":30}""";
    private const string ArrayJson = """[{"id":1,"label":"first"},{"id":2}]""";

    [Fact]
    public void V4_WriteTo_ProducesValidJson()
    {
        var v4 = V4.MigrationPerson.Parse(PersonJson);
        ArrayBufferWriter<byte> buffer = new();
        using System.Text.Json.Utf8JsonWriter writer = new(buffer);
        v4.WriteTo(writer);
        writer.Flush();

        // Re-parse to verify
        System.Text.Json.Utf8JsonReader reader = new(buffer.WrittenSpan);
        Assert.True(System.Text.Json.JsonDocument.TryParseValue(ref reader, out _));
    }

    [Fact]
    public void V4_WriteTo_ProducesValidJson_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        ArrayBufferWriter<byte> buffer = new();
        using System.Text.Json.Utf8JsonWriter writer = new(buffer);
        v4.WriteTo(writer);
        writer.Flush();

        System.Text.Json.Utf8JsonReader reader = new(buffer.WrittenSpan);
        Assert.True(System.Text.Json.JsonDocument.TryParseValue(ref reader, out _));
    }

    [Fact]
    public void V5_WriteTo_ProducesValidJson()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        // V5 uses Corvus.Text.Json.Utf8JsonWriter (not System.Text.Json)
        ArrayBufferWriter<byte> buffer = new();
        using Corvus.Text.Json.Utf8JsonWriter writer = new(buffer);
        v5.WriteTo(writer);
        writer.Flush();

        System.Text.Json.Utf8JsonReader reader = new(buffer.WrittenSpan);
        Assert.True(System.Text.Json.JsonDocument.TryParseValue(ref reader, out _));
    }

    [Fact]
    public void V4_ToString_RoundTrips()
    {
        var v4 = V4.MigrationPerson.Parse(PersonJson);
        string serialized = v4.ToString();
        var reparsed = V4.MigrationPerson.Parse(serialized);
        Assert.Equal("Jo", (string)reparsed.Name);
        Assert.Equal(30, (int)reparsed.Age);
    }

    [Fact]
    public void V4_ToString_RoundTrips_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        string serialized = v4.ToString();
        using var parsedReparsed = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(serialized);
        V4.MigrationPerson reparsed = parsedReparsed.Instance;
        Assert.Equal("Jo", (string)reparsed.Name);
        Assert.Equal(30, (int)reparsed.Age);
    }

    [Fact]
    public void V5_ToString_RoundTrips()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        string serialized = v5.ToString();
        using var parsedV5Reparsed = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(serialized);
        V5.MigrationPerson reparsed = parsedV5Reparsed.RootElement;
        Assert.Equal("Jo", (string)reparsed.Name);
        Assert.Equal(30, (int)reparsed.Age);
    }

    [Fact]
    public void V4_ArrayToString_RoundTrips()
    {
        var v4 = V4.MigrationItemArray.Parse(ArrayJson);
        string serialized = v4.ToString();
        var reparsed = V4.MigrationItemArray.Parse(serialized);
        Assert.Equal(2, reparsed.GetArrayLength());
        Assert.Equal(1, (int)reparsed[0].Id);
    }

    [Fact]
    public void V4_ArrayToString_RoundTrips_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        V4.MigrationItemArray v4 = parsedV4.Instance;
        string serialized = v4.ToString();
        using var parsedReparsed = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse(serialized);
        V4.MigrationItemArray reparsed = parsedReparsed.Instance;
        Assert.Equal(2, reparsed.GetArrayLength());
        Assert.Equal(1, (int)reparsed[0].Id);
    }

    [Fact]
    public void V5_ArrayToString_RoundTrips()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        V5.MigrationItemArray v5 = parsedV5.RootElement;
        string serialized = v5.ToString();
        using var parsedV5Reparsed = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray>.Parse(serialized);
        V5.MigrationItemArray reparsed = parsedV5Reparsed.RootElement;
        Assert.Equal(2, reparsed.GetArrayLength());
        Assert.Equal(1, (int)reparsed[0].Id);
    }

    [Fact]
    public void BothEngines_ProduceSameJson()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;

        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;

        Assert.Equal(v4.ToString(), v5.ToString());
    }

    [Fact]
    public void BothEngines_ArrayProduceSameJson()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        V4.MigrationItemArray v4 = parsedV4.Instance;

        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        V5.MigrationItemArray v5 = parsedV5.RootElement;

        Assert.Equal(v4.ToString(), v5.ToString());
    }

}