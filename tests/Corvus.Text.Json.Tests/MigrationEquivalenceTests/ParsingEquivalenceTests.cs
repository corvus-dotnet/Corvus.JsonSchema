// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests.MigrationEquivalenceTests;

using Xunit;

using V4 = MigrationModels.V4;
using V5 = MigrationModels.V5;

/// <summary>
/// Verifies that V4 and V5 parsing produces equivalent results from the same JSON input.
/// </summary>
/// <remarks>
/// <para>V4: <c>MyType.Parse(jsonString)</c></para>
/// <para>V5: <c>MyType.ParseValue(jsonString)</c></para>
/// </remarks>
public class ParsingEquivalenceTests
{
    private const string PersonJson = """{"name":"Jo","age":30,"email":"jo@example.com","isActive":true}""";
    private const string ArrayJson = """[{"id":1,"label":"first"},{"id":2,"label":"second"}]""";
    private const string IntVectorJson = """[10,20,30]""";
    private const string UnionStringJson = "\"hello\"";
    private const string UnionIntJson = """42""";
    private const string UnionBoolJson = """true""";
    private const string EnumJson = "\"active\"";
    private const string TupleJson = """["hello",42,true]""";

    [Fact]
    public void V4_ParsePersonFromString()
    {
        var v4 = V4.MigrationPerson.Parse(PersonJson);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, v4.ValueKind);
        Assert.Equal("Jo", (string)v4.Name);
        Assert.Equal(30, (int)v4.Age);
    }

    [Fact]
    public void V4_ParsePersonFromString_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        Assert.Equal(System.Text.Json.JsonValueKind.Object, v4.ValueKind);
        Assert.Equal("Jo", (string)v4.Name);
        Assert.Equal(30, (int)v4.Age);
    }

    [Fact]
    public void V5_ParsePersonFromString()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.Object, v5.ValueKind);
        Assert.Equal("Jo", (string)v5.Name);
        Assert.Equal(30, (int)v5.Age);
    }

    [Fact]
    public void V4_ParseArrayFromString()
    {
        var v4 = V4.MigrationItemArray.Parse(ArrayJson);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, v4.ValueKind);
        Assert.Equal(2, v4.GetArrayLength());
    }

    [Fact]
    public void V4_ParseArrayFromString_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        V4.MigrationItemArray v4 = parsedV4.Instance;
        Assert.Equal(System.Text.Json.JsonValueKind.Array, v4.ValueKind);
        Assert.Equal(2, v4.GetArrayLength());
    }

    [Fact]
    public void V5_ParseArrayFromString()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        V5.MigrationItemArray v5 = parsedV5.RootElement;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.Array, v5.ValueKind);
        Assert.Equal(2, v5.GetArrayLength());
    }

    [Fact]
    public void V4_ParseIntVectorFromString()
    {
        var v4 = V4.MigrationIntVector.Parse(IntVectorJson);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, v4.ValueKind);
        Assert.Equal(3, v4.GetArrayLength());
    }

    [Fact]
    public void V4_ParseIntVectorFromString_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationIntVector>.Parse(IntVectorJson);
        V4.MigrationIntVector v4 = parsedV4.Instance;
        Assert.Equal(System.Text.Json.JsonValueKind.Array, v4.ValueKind);
        Assert.Equal(3, v4.GetArrayLength());
    }

    [Fact]
    public void V5_ParseIntVectorFromString()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationIntVector>.Parse(IntVectorJson);
        V5.MigrationIntVector v5 = parsedV5.RootElement;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.Array, v5.ValueKind);
        Assert.Equal(3, v5.GetArrayLength());
    }

    [Fact]
    public void V4_ParseUnionFromString_StringVariant()
    {
        var v4 = V4.MigrationUnion.Parse(UnionStringJson);
        Assert.Equal(System.Text.Json.JsonValueKind.String, v4.ValueKind);
    }

    [Fact]
    public void V4_ParseUnionFromString_StringVariant_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse(UnionStringJson);
        V4.MigrationUnion v4 = parsedV4.Instance;
        Assert.Equal(System.Text.Json.JsonValueKind.String, v4.ValueKind);
    }

    [Fact]
    public void V5_ParseUnionFromString_StringVariant()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse(UnionStringJson);
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.String, v5.ValueKind);
    }

    [Fact]
    public void V4_ParseUnionFromString_IntVariant()
    {
        var v4 = V4.MigrationUnion.Parse(UnionIntJson);
        Assert.Equal(System.Text.Json.JsonValueKind.Number, v4.ValueKind);
    }

    [Fact]
    public void V4_ParseUnionFromString_IntVariant_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse(UnionIntJson);
        V4.MigrationUnion v4 = parsedV4.Instance;
        Assert.Equal(System.Text.Json.JsonValueKind.Number, v4.ValueKind);
    }

    [Fact]
    public void V5_ParseUnionFromString_IntVariant()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse(UnionIntJson);
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.Number, v5.ValueKind);
    }

    [Fact]
    public void V4_ParseUnionFromString_BoolVariant()
    {
        var v4 = V4.MigrationUnion.Parse(UnionBoolJson);
        Assert.Equal(System.Text.Json.JsonValueKind.True, v4.ValueKind);
    }

    [Fact]
    public void V4_ParseUnionFromString_BoolVariant_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse(UnionBoolJson);
        V4.MigrationUnion v4 = parsedV4.Instance;
        Assert.Equal(System.Text.Json.JsonValueKind.True, v4.ValueKind);
    }

    [Fact]
    public void V5_ParseUnionFromString_BoolVariant()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse(UnionBoolJson);
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.True, v5.ValueKind);
    }

    [Fact]
    public void V4_ParseEnumFromString()
    {
        var v4 = V4.MigrationStatusEnum.Parse(EnumJson);
        Assert.Equal(System.Text.Json.JsonValueKind.String, v4.ValueKind);
    }

    [Fact]
    public void V4_ParseEnumFromString_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationStatusEnum>.Parse(EnumJson);
        V4.MigrationStatusEnum v4 = parsedV4.Instance;
        Assert.Equal(System.Text.Json.JsonValueKind.String, v4.ValueKind);
    }

    [Fact]
    public void V5_ParseEnumFromString()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationStatusEnum>.Parse(EnumJson);
        V5.MigrationStatusEnum v5 = parsedV5.RootElement;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.String, v5.ValueKind);
    }

    [Fact]
    public void V4_ParseTupleFromString()
    {
        var v4 = V4.MigrationTuple.Parse(TupleJson);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, v4.ValueKind);
        Assert.Equal(3, v4.GetArrayLength());
    }

    [Fact]
    public void V4_ParseTupleFromString_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationTuple>.Parse(TupleJson);
        V4.MigrationTuple v4 = parsedV4.Instance;
        Assert.Equal(System.Text.Json.JsonValueKind.Array, v4.ValueKind);
        Assert.Equal(3, v4.GetArrayLength());
    }

    [Fact]
    public void V5_ParseTupleFromString()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationTuple>.Parse(TupleJson);
        V5.MigrationTuple v5 = parsedV5.RootElement;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.Array, v5.ValueKind);
        Assert.Equal(3, v5.GetArrayLength());
    }

    [Fact]
    public void V4_ParseFromUtf8()
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(PersonJson);
        var v4 = V4.MigrationPerson.ParseValue(utf8);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, v4.ValueKind);
        Assert.Equal("Jo", (string)v4.Name);
    }

    [Fact]
    public void V4_ParseFromUtf8_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(PersonJson);
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(utf8);
        V4.MigrationPerson v4 = parsedV4.Instance;
        Assert.Equal(System.Text.Json.JsonValueKind.Object, v4.ValueKind);
        Assert.Equal("Jo", (string)v4.Name);
    }

    [Fact]
    public void V5_ParseFromUtf8()
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(PersonJson);
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(utf8);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.Object, v5.ValueKind);
        Assert.Equal("Jo", (string)v5.Name);
    }

    [Fact]
    public void V4_ParseFromReader()
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(PersonJson);
        System.Text.Json.Utf8JsonReader reader = new(utf8);
        // ParsedValue<T> does not offer a ref Utf8JsonReader overload, so we use the V4 API directly.
        var v4 = V4.MigrationPerson.ParseValue(ref reader);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, v4.ValueKind);
    }

    [Fact]
    public void V5_ParseFromReader()
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(PersonJson);
        Corvus.Text.Json.Utf8JsonReader reader = new(utf8);
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.ParseValue(ref reader);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.Object, v5.ValueKind);
    }

    [Fact]
    public void BothEngines_ParsePerson_SameResult()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;

        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;

        Assert.Equal((string)v4.Name, (string)v5.Name);
        Assert.Equal((int)v4.Age, (int)v5.Age);
    }

    [Fact]
    public void V4_ParseWithParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        Assert.Equal("Jo", (string)v4.Name);
        Assert.Equal(30, (int)v4.Age);
    }

    [Fact]
    public void V4_ParsePersonFromJsonDocument()
    {
        // Common V4 pattern: manage the underlying JsonDocument lifetime directly.
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(PersonJson);
        var v4 = V4.MigrationPerson.FromJson(jsonDoc.RootElement);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, v4.ValueKind);
        Assert.Equal("Jo", (string)v4.Name);
        Assert.Equal(30, (int)v4.Age);
    }

    [Fact]
    public void V4_ParseArrayFromJsonDocument()
    {
        // Common V4 pattern: manage the underlying JsonDocument lifetime directly.
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(ArrayJson);
        var v4 = V4.MigrationItemArray.FromJson(jsonDoc.RootElement);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, v4.ValueKind);
        Assert.Equal(2, v4.GetArrayLength());
    }

    [Fact]
    public void V4_ParseIntVectorFromJsonDocument()
    {
        // Common V4 pattern: manage the underlying JsonDocument lifetime directly.
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(IntVectorJson);
        var v4 = V4.MigrationIntVector.FromJson(jsonDoc.RootElement);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, v4.ValueKind);
        Assert.Equal(3, v4.GetArrayLength());
    }

    [Fact]
    public void V4_ParseUnionFromJsonDocument_StringVariant()
    {
        // Common V4 pattern: manage the underlying JsonDocument lifetime directly.
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(UnionStringJson);
        var v4 = V4.MigrationUnion.FromJson(jsonDoc.RootElement);
        Assert.Equal(System.Text.Json.JsonValueKind.String, v4.ValueKind);
    }

    [Fact]
    public void V4_ParseUnionFromJsonDocument_IntVariant()
    {
        // Common V4 pattern: manage the underlying JsonDocument lifetime directly.
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(UnionIntJson);
        var v4 = V4.MigrationUnion.FromJson(jsonDoc.RootElement);
        Assert.Equal(System.Text.Json.JsonValueKind.Number, v4.ValueKind);
    }

    [Fact]
    public void V4_ParseUnionFromJsonDocument_BoolVariant()
    {
        // Common V4 pattern: manage the underlying JsonDocument lifetime directly.
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(UnionBoolJson);
        var v4 = V4.MigrationUnion.FromJson(jsonDoc.RootElement);
        Assert.Equal(System.Text.Json.JsonValueKind.True, v4.ValueKind);
    }

    [Fact]
    public void V4_ParseEnumFromJsonDocument()
    {
        // Common V4 pattern: manage the underlying JsonDocument lifetime directly.
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(EnumJson);
        var v4 = V4.MigrationStatusEnum.FromJson(jsonDoc.RootElement);
        Assert.Equal(System.Text.Json.JsonValueKind.String, v4.ValueKind);
    }

    [Fact]
    public void V4_ParseTupleFromJsonDocument()
    {
        // Common V4 pattern: manage the underlying JsonDocument lifetime directly.
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(TupleJson);
        var v4 = V4.MigrationTuple.FromJson(jsonDoc.RootElement);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, v4.ValueKind);
        Assert.Equal(3, v4.GetArrayLength());
    }
}