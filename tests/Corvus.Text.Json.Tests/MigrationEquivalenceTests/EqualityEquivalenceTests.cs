// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests.MigrationEquivalenceTests;

using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using V4 = MigrationModels.V4;
using V5 = MigrationModels.V5;

/// <summary>
/// Verifies that V4 and V5 equality semantics produce equivalent results.
/// </summary>
/// <remarks>
/// <para>V4: <c>Equals(in T)</c>, <c>operator ==</c> / <c>!=</c></para>
/// <para>V5: <c>Equals(in T)</c>, <c>operator ==</c> / <c>!=</c></para>
/// </remarks>
[TestClass]
public class EqualityEquivalenceTests
{
    private const string PersonJson = """{"name":"Jo","age":30}""";
    private const string PersonJson2 = """{"name":"Bob","age":25}""";
    private const string ArrayJson = """[{"id":1,"label":"first"},{"id":2,"label":"second"}]""";
    private const string UnionStringJson = "\"hello\"";
    private const string EnumJson = "\"active\"";

    [TestMethod]
    public void V4_Equals_SameObject()
    {
        var v4a = V4.MigrationPerson.Parse(PersonJson);
        var v4b = V4.MigrationPerson.Parse(PersonJson);
        Assert.IsTrue(v4a.Equals(v4b));
        Assert.IsTrue(v4a == v4b);
    }

    [TestMethod]
    public void V4_Equals_SameObject_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedA = ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        using var parsedB = ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        Assert.IsTrue(parsedA.Instance.Equals(parsedB.Instance));
        Assert.IsTrue(parsedA.Instance == parsedB.Instance);
    }

    [TestMethod]
    public void V5_Equals_SameObject()
    {
        using var docA = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        using var docB = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        Assert.IsTrue(docA.RootElement.Equals(docB.RootElement));
        Assert.IsTrue(docA.RootElement == docB.RootElement);
    }

    [TestMethod]
    public void V4_NotEquals_DifferentObject()
    {
        var v4a = V4.MigrationPerson.Parse(PersonJson);
        var v4b = V4.MigrationPerson.Parse(PersonJson2);
        Assert.IsFalse(v4a.Equals(v4b));
        Assert.IsTrue(v4a != v4b);
    }

    [TestMethod]
    public void V4_NotEquals_DifferentObject_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedA = ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        using var parsedB = ParsedValue<V4.MigrationPerson>.Parse(PersonJson2);
        Assert.IsFalse(parsedA.Instance.Equals(parsedB.Instance));
        Assert.IsTrue(parsedA.Instance != parsedB.Instance);
    }

    [TestMethod]
    public void V5_NotEquals_DifferentObject()
    {
        using var docA = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        using var docB = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson2);
        Assert.IsFalse(docA.RootElement.Equals(docB.RootElement));
        Assert.IsTrue(docA.RootElement != docB.RootElement);
    }

    [TestMethod]
    public void V4_Equals_Union()
    {
        var v4a = V4.MigrationUnion.Parse(UnionStringJson);
        var v4b = V4.MigrationUnion.Parse(UnionStringJson);
        Assert.IsTrue(v4a.Equals(v4b));
    }

    [TestMethod]
    public void V4_Equals_Union_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedA = ParsedValue<V4.MigrationUnion>.Parse(UnionStringJson);
        using var parsedB = ParsedValue<V4.MigrationUnion>.Parse(UnionStringJson);
        Assert.IsTrue(parsedA.Instance.Equals(parsedB.Instance));
    }

    [TestMethod]
    public void V5_Equals_Union()
    {
        using var docA = ParsedJsonDocument<V5.MigrationUnion>.Parse(UnionStringJson);
        using var docB = ParsedJsonDocument<V5.MigrationUnion>.Parse(UnionStringJson);
        Assert.IsTrue(docA.RootElement.Equals(docB.RootElement));
    }

    [TestMethod]
    public void V4_Equals_Enum()
    {
        var v4a = V4.MigrationStatusEnum.Parse(EnumJson);
        var v4b = V4.MigrationStatusEnum.Parse(EnumJson);
        Assert.IsTrue(v4a.Equals(v4b));
    }

    [TestMethod]
    public void V4_Equals_Enum_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedA = ParsedValue<V4.MigrationStatusEnum>.Parse(EnumJson);
        using var parsedB = ParsedValue<V4.MigrationStatusEnum>.Parse(EnumJson);
        Assert.IsTrue(parsedA.Instance.Equals(parsedB.Instance));
    }

    [TestMethod]
    public void V5_Equals_Enum()
    {
        using var docA = ParsedJsonDocument<V5.MigrationStatusEnum>.Parse(EnumJson);
        using var docB = ParsedJsonDocument<V5.MigrationStatusEnum>.Parse(EnumJson);
        Assert.IsTrue(docA.RootElement.Equals(docB.RootElement));
    }

    [TestMethod]
    public void V4_Equals_Array()
    {
        var v4a = V4.MigrationItemArray.Parse(ArrayJson);
        var v4b = V4.MigrationItemArray.Parse(ArrayJson);
        Assert.IsTrue(v4a.Equals(v4b));
    }

    [TestMethod]
    public void V4_Equals_Array_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedA = ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        using var parsedB = ParsedValue<V4.MigrationItemArray>.Parse(ArrayJson);
        Assert.IsTrue(parsedA.Instance.Equals(parsedB.Instance));
    }

    [TestMethod]
    public void V5_Equals_Array()
    {
        using var docA = ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        using var docB = ParsedJsonDocument<V5.MigrationItemArray>.Parse(ArrayJson);
        Assert.IsTrue(docA.RootElement.Equals(docB.RootElement));
    }

    [TestMethod]
    public void BothEngines_Equals_SameResult()
    {
        // Both engines should agree on equality for equivalent JSON
        using var parsedV4A = ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        using var parsedV4B = ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        using var docV5A = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        using var docV5B = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);

        Assert.AreEqual(parsedV4A.Instance.Equals(parsedV4B.Instance), docV5A.RootElement.Equals(docV5B.RootElement));
    }

    [TestMethod]
    public void BothEngines_NotEquals_SameResult()
    {
        // Both engines should agree on inequality for different JSON
        using var parsedV4A = ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        using var parsedV4B = ParsedValue<V4.MigrationPerson>.Parse(PersonJson2);
        using var docV5A = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        using var docV5B = ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson2);

        Assert.AreEqual(parsedV4A.Instance.Equals(parsedV4B.Instance), docV5A.RootElement.Equals(docV5B.RootElement));
    }
}