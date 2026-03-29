// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests.MigrationEquivalenceTests;

using Xunit;

using V4 = MigrationModels.V4;
using V5 = MigrationModels.V5;

/// <summary>
/// Verifies that V4 and V5 closed tuple access produces equivalent results.
/// </summary>
/// <remarks>
/// <para>V4: <c>tuple.Item1</c> (named accessor), <c>tuple.Item2</c>, <c>tuple.Item3</c></para>
/// <para>V5: <c>tuple.Item1</c> (typed property), <c>tuple.Item2</c>, <c>tuple.Item3</c> — or <c>tuple[0]</c> (index accessor)</para>
/// </remarks>
public class TupleEquivalenceTests
{
    private const string TupleJson = """["hello",42,true]""";

    [Fact]
    public void V4_AccessElement0_String()
    {
        var v4 = V4.MigrationTuple.Parse(TupleJson);
        string value = (string)v4.Item1;
        Assert.Equal("hello", value);
    }

    [Fact]
    public void V4_AccessElement0_String_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationTuple>.Parse(TupleJson);
        V4.MigrationTuple v4 = parsedV4.Instance;
        string value = (string)v4.Item1;
        Assert.Equal("hello", value);
    }

    [Fact]
    public void V5_AccessElement0_String()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationTuple>.Parse(TupleJson);
        V5.MigrationTuple v5 = parsedV5.RootElement;
        // V5: typed Item1 property — direct parity with V4
        Assert.True(v5.Item1.TryGetValue(out string? value));
        Assert.Equal("hello", value);
    }

    [Fact]
    public void V4_AccessElement1_Int()
    {
        var v4 = V4.MigrationTuple.Parse(TupleJson);
        int value = (int)v4.Item2;
        Assert.Equal(42, value);
    }

    [Fact]
    public void V4_AccessElement1_Int_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationTuple>.Parse(TupleJson);
        V4.MigrationTuple v4 = parsedV4.Instance;
        int value = (int)v4.Item2;
        Assert.Equal(42, value);
    }

    [Fact]
    public void V5_AccessElement1_Int()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationTuple>.Parse(TupleJson);
        V5.MigrationTuple v5 = parsedV5.RootElement;
        // V5: typed Item2 property with implicit int conversion
        Assert.Equal(42, (int)v5.Item2);
    }

    [Fact]
    public void V4_AccessElement2_Bool()
    {
        var v4 = V4.MigrationTuple.Parse(TupleJson);
        bool value = (bool)v4.Item3;
        Assert.True(value);
    }

    [Fact]
    public void V4_AccessElement2_Bool_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationTuple>.Parse(TupleJson);
        V4.MigrationTuple v4 = parsedV4.Instance;
        bool value = (bool)v4.Item3;
        Assert.True(value);
    }

    [Fact]
    public void V5_AccessElement2_Bool()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationTuple>.Parse(TupleJson);
        V5.MigrationTuple v5 = parsedV5.RootElement;
        // V5: typed Item3 property with TryGetValue
        Assert.True(v5.Item3.TryGetValue(out bool value));
        Assert.True(value);
    }

    [Fact]
    public void V5_AccessViaIndexer()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationTuple>.Parse(TupleJson);
        V5.MigrationTuple v5 = parsedV5.RootElement;
        // V5 also supports int indexer returning JsonElement
        Assert.Equal(Corvus.Text.Json.JsonValueKind.String, v5[0].ValueKind);
        Assert.Equal(Corvus.Text.Json.JsonValueKind.Number, v5[1].ValueKind);
        Assert.Equal(Corvus.Text.Json.JsonValueKind.True, v5[2].ValueKind);
    }

    [Fact]
    public void V4_TupleLength()
    {
        var v4 = V4.MigrationTuple.Parse(TupleJson);
        Assert.Equal(3, v4.GetArrayLength());
    }

    [Fact]
    public void V4_TupleLength_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationTuple>.Parse(TupleJson);
        V4.MigrationTuple v4 = parsedV4.Instance;
        Assert.Equal(3, v4.GetArrayLength());
    }

    [Fact]
    public void V5_TupleLength()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationTuple>.Parse(TupleJson);
        V5.MigrationTuple v5 = parsedV5.RootElement;
        Assert.Equal(3, v5.GetArrayLength());
    }

    [Fact]
    public void BothEngines_SameTupleElements()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationTuple>.Parse(TupleJson);
        V4.MigrationTuple v4 = parsedV4.Instance;

        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationTuple>.Parse(TupleJson);
        V5.MigrationTuple v5 = parsedV5.RootElement;

        Assert.Equal((string)v4.Item1, (string)v5.Item1);
        Assert.Equal((int)v4.Item2, (int)v5.Item2);
        Assert.Equal((bool)v4.Item3, (bool)v5.Item3);
    }

    [Fact]
    public void V4_TupleCreate()
    {
        // V4: static Create(item1, item2, item3) builds a tuple.
        var v4 = V4.MigrationTuple.Create(
            (Corvus.Json.JsonString)"hello",
            V4.MigrationTuple.PrefixItems1Entity.Parse("42"),
            (Corvus.Json.JsonBoolean)true);
        Assert.Equal("hello", (string)v4.Item1);
        Assert.Equal(42, (int)v4.Item2);
        Assert.True((bool)v4.Item3);
    }

    [Fact]
    public void V5_TupleCreate()
    {
        // V5: workspace builder — equivalent to V4 Create.
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using JsonDocumentBuilder<V5.MigrationTuple.Mutable> builder =
            V5.MigrationTuple.CreateBuilder(
                workspace,
                V5.MigrationTuple.Build(
                    static (ref b) => b.CreateTuple(
                        item1: "hello",
                        item2: 42,
                        item3: true)));

        V5.MigrationTuple result = builder.RootElement;
        Assert.True(result.Item1.TryGetValue(out string? s));
        Assert.Equal("hello", s);
        Assert.Equal(42, (int)result.Item2);
        Assert.True((bool)result.Item3);
    }

    [Fact]
    public void V4_TupleToValueTuple()
    {
        // V4: implicit conversion to C# ValueTuple — explicit cast then deconstruct.
        var v4 = V4.MigrationTuple.Parse(TupleJson);
        (Corvus.Json.JsonString first, V4.MigrationTuple.PrefixItems1Entity second, Corvus.Json.JsonBoolean third) =
            ((Corvus.Json.JsonString, V4.MigrationTuple.PrefixItems1Entity, Corvus.Json.JsonBoolean))v4;
        Assert.Equal("hello", (string)first);
        Assert.Equal(42, (int)second);
        Assert.True((bool)third);
    }

    [Fact]
    public void V5_TupleDestructure_ViaIndexer()
    {
        // V5 has no ValueTuple operator — access via typed properties instead.
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationTuple>.Parse(TupleJson);
        V5.MigrationTuple v5 = parsedV5.RootElement;
        Assert.Equal("hello", (string)v5.Item1);
        Assert.Equal(42, (int)v5.Item2);
        Assert.True((bool)v5.Item3);
    }
}