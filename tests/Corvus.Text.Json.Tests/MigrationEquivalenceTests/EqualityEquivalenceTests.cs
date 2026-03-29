// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests.MigrationEquivalenceTests;

using Corvus.Json;
using Xunit;

using V4 = MigrationModels.V4;
using V5 = MigrationModels.V5;

/// <summary>
/// Verifies that V4 and V5 equality semantics produce equivalent results.
/// </summary>
/// <remarks>
/// <para>V4: <c>Equals(in T)</c>, <c>operator ==</c> / <c>!=</c></para>
/// <para>V5: <c>Equals(in T)</c>, <c>operator ==</c> / <c>!=</c></para>
/// </remarks>
public class EqualityEquivalenceTests
{
    private const string PersonJson = """{"name":"Jo","age":30}""";
    private const string PersonJson2 = """{"name":"Bob","age":25}""";
    private const string ArrayJson = """[{"id":1,"label":"first"},{"id":2,"label":"second"}]""";
    private const string UnionStringJson = "\"hello\"";
    private const string EnumJson = "\"active\"";

    [Fact]
    public void V4_Equals_SameObject()
    {
        var v4a = V4.MigrationPerson.Parse(PersonJson);
        var v4b = V4.MigrationPerson.Parse(PersonJson);
        Assert.True(v4a.Equals(v4b));
        Assert.True(v4a == v4b);
    }

    [Fact]
    public void V4_Equals_SameObject_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedA = ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        using var parsedB = ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        Assert.True(parsedA.Instance.Equals(parsedB.Instance));
        Assert.True(parsedA.Instance == parsedB.Instance);
    }

    [Fact]
    public void V5_Equals_SameObject()
    {
        using var docA = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        using var docB = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        Assert.True(docA.RootElement.Equals(docB.RootElement));
        Assert.True(docA.RootElement == docB.RootElement);
    }

    [Fact]
    public void V4_NotEquals_DifferentObject()
    {
        var v4a = V4.MigrationPerson.Parse(PersonJson);
        var v4b = V4.MigrationPerson.Parse(PersonJson2);
        Assert.False(v4a.Equals(v4b));
        Assert.True(v4a != v4b);
    }

    [Fact]
    public void V4_NotEquals_DifferentObject_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedA = ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        using var parsedB = ParsedValue<V4.MigrationPerson>.Parse(PersonJson2);
        Assert.False(parsedA.Instance.Equals(parsedB.Instance));
        Assert.True(parsedA.Instance != parsedB.Instance);
    }

    [Fact]
    public void V5_NotEquals_DifferentObject()
    {
        using var docA = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        using var docB = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson2);
        Assert.False(docA.RootElement.Equals(docB.RootElement));
        Assert.True(docA.RootElement != docB.RootElement);
    }

    [Fact]
    public void V4_Equals_Union()
    {
        var v4a = V4.MigrationUnion.Parse(UnionStringJson);
        var v4b = V4.MigrationUnion.Parse(UnionStringJson);
        Assert.True(v4a.Equals(v4b));
    }

    [Fact]
    public void V4_Equals_Union_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedA = ParsedValue<V4.MigrationUnion>.Parse(UnionStringJson);
        using var parsedB = ParsedValue<V4.MigrationUnion>.Parse(UnionStringJson);
        Assert.True(parsedA.Instance.Equals(parsedB.Instance));
    }

    [Fact]
    public void V5_Equals_Union()
    {
        using var docA = ParsedJsonDocument<V5.MigrationUnion>.Parse(UnionStringJson);
        using var docB = ParsedJsonDocument<V5.MigrationUnion>.Parse(UnionStringJson);
        Assert.True(docA.RootElement.Equals(docB.RootElement));
    }

    [Fact]
    public void V4_Equals_Enum()
    {
        var v4a = V4.MigrationStatusEnum.Parse(EnumJson);
        var v4b = V4.MigrationStatusEnum.Parse(EnumJson);
        Assert.True(v4a.Equals(v4b));
    }

    [Fact]
    public void V4_Equals_Enum_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedA = ParsedValue<V4.MigrationStatusEnum>.Parse(EnumJson);
        using var parsedB = ParsedValue<V4.MigrationStatusEnum>.Parse(EnumJson);
        Assert.True(parsedA.Instance.Equals(parsedB.Instance));
    }

    [Fact]
    public void V5_Equals_Enum()
    {
        using var docA = ParsedJsonDocument<V5.MigrationStatusEnum>.Parse(EnumJson);
        using var docB = ParsedJsonDocument<V5.MigrationStatusEnum>.Parse(EnumJson);
        Assert.True(docA.RootElement.Equals(docB.RootElement));
    }

    [Fact]
    public void V4_Equals_Array()
    {
        var v4a = V4.MigrationItemArray.Parse(ArrayJson);
        var v4b = V4.MigrationItemArray.Parse(ArrayJson);
        Assert.True(v4a.Equals(v4b));
    }

    [Fact]
    public void V4_Equals_Array_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedA = ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        using var parsedB = ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        Assert.True(parsedA.Instance.Equals(parsedB.Instance));
    }

    [Fact]
    public void V5_Equals_Array()
    {
        using var docA = ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        using var docB = ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        Assert.True(docA.RootElement.Equals(docB.RootElement));
    }

    [Fact]
    public void BothEngines_Equals_SameResult()
    {
        // Both engines should agree on equality for equivalent JSON
        using var parsedV4A = ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        using var parsedV4B = ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        using var docV5A = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        using var docV5B = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);

        Assert.Equal(parsedV4A.Instance.Equals(parsedV4B.Instance), docV5A.RootElement.Equals(docV5B.RootElement));
    }

    [Fact]
    public void BothEngines_NotEquals_SameResult()
    {
        // Both engines should agree on inequality for different JSON
        using var parsedV4A = ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        using var parsedV4B = ParsedValue<V4.MigrationPerson>.Parse(PersonJson2);
        using var docV5A = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        using var docV5B = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson2);

        Assert.Equal(parsedV4A.Instance.Equals(parsedV4B.Instance), docV5A.RootElement.Equals(docV5B.RootElement));
    }
}