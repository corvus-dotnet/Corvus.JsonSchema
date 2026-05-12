// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests.MigrationEquivalenceTests;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using V4 = MigrationModels.V4;
using V5 = MigrationModels.V5;

/// <summary>
/// Verifies that V4 and V5 allOf composition types support equivalent implicit conversions.
/// </summary>
/// <remarks>
/// <para>V4 and V5: <c>allOf</c> composition supports implicit conversion from composite to constituents.</para>
/// <para>This behaves like implementing multiple interfaces in C# — the composite type "is a" each constituent type.</para>
/// </remarks>
[TestClass]
public class AllOfConversionEquivalenceTests
{
    private const string CompositeJson = """
        {
          "name": "Test Item",
          "description": "A test description",
          "count": 42,
          "budget": 123.45
        }
        """;

    [TestMethod]
    public void V4_ImplicitConversionToRequiredName()
    {
        var v4Composite = V4.MigrationComposite.Parse(CompositeJson);
        
        // Implicit conversion to allOf constituent type
        V4.MigrationComposite.RequiredName v4Base = v4Composite;
        
        Assert.AreEqual("Test Item", (string)v4Base.Name);
        Assert.AreEqual("A test description", (string?)v4Base.Description);
    }

    [TestMethod]
    public void V5_ImplicitConversionToRequiredName()
    {
        using var parsed = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationComposite>.Parse(CompositeJson);
        V5.MigrationComposite v5Composite = parsed.RootElement;
        
        // Implicit conversion to allOf constituent type (same as V4)
        V5.MigrationComposite.RequiredName v5Base = v5Composite;
        
        Assert.AreEqual("Test Item", (string)v5Base.Name);
        Assert.AreEqual("A test description", (string?)v5Base.Description);
    }

    [TestMethod]
    public void V4_ImplicitConversionToRequiredCount()
    {
        var v4Composite = V4.MigrationComposite.Parse(CompositeJson);
        
        // Implicit conversion to other allOf constituent type
        V4.MigrationComposite.RequiredCountEntity v4Count = v4Composite;
        
        Assert.AreEqual(42, (int)v4Count.CountValue);
    }

    [TestMethod]
    public void V5_ImplicitConversionToRequiredCount()
    {
        using var parsed = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationComposite>.Parse(CompositeJson);
        V5.MigrationComposite v5Composite = parsed.RootElement;
        
        // Implicit conversion to other allOf constituent type (same as V4)
        V5.MigrationComposite.RequiredCount v5Count = v5Composite;
        
        Assert.AreEqual(42, (long)v5Count.Count);
    }

    [TestMethod]
    public void V4_ExplicitConversionFromRequiredName()
    {
        const string baseJson = """{ "name": "Just Name", "description": "Only name properties" }""";
        var v4Base = V4.MigrationComposite.RequiredName.Parse(baseJson);
        
        // Explicit conversion from constituent to composite
        var v4Composite = (V4.MigrationComposite)v4Base;
        
        Assert.AreEqual("Just Name", (string)v4Composite.Name);
    }

    [TestMethod]
    public void V5_ExplicitConversionFromRequiredName()
    {
        const string baseJson = """{ "name": "Just Name", "description": "Only name properties" }""";
        using var parsed = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationComposite.RequiredName>.Parse(baseJson);
        V5.MigrationComposite.RequiredName v5Base = parsed.RootElement;
        
        // Explicit conversion from constituent to composite (same as V4)
        var v5Composite = (V5.MigrationComposite)v5Base;
        
        Assert.AreEqual("Just Name", (string)v5Composite.Name);
    }

    [TestMethod]
    public void V4_AllPropertiesAccessibleViaCompositeAndConstituents()
    {
        var v4Composite = V4.MigrationComposite.Parse(CompositeJson);
        
        // Access via composite
        Assert.AreEqual("Test Item", (string)v4Composite.Name);
        Assert.AreEqual(42, (int)v4Composite.CountValue);
        Assert.AreEqual(123.45, (double)v4Composite.Budget);
        
        // Access via RequiredName constituent
        V4.MigrationComposite.RequiredName v4Name = v4Composite;
        Assert.AreEqual("Test Item", (string)v4Name.Name);
        
        // Access via RequiredCountEntity constituent
        V4.MigrationComposite.RequiredCountEntity v4Count = v4Composite;
        Assert.AreEqual(42, (int)v4Count.CountValue);
    }

    [TestMethod]
    public void V5_AllPropertiesAccessibleViaCompositeAndConstituents()
    {
        using var parsed = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationComposite>.Parse(CompositeJson);
        V5.MigrationComposite v5Composite = parsed.RootElement;
        
        // Access via composite (same as V4)
        Assert.AreEqual("Test Item", (string)v5Composite.Name);
        Assert.AreEqual(42, (long)v5Composite.Count);
        Assert.AreEqual(123.45, (double)v5Composite.Budget);
        
        // Access via RequiredName constituent
        V5.MigrationComposite.RequiredName v5Name = v5Composite;
        Assert.AreEqual("Test Item", (string)v5Name.Name);
        
        // Access via RequiredCount constituent
        V5.MigrationComposite.RequiredCount v5Count = v5Composite;
        Assert.AreEqual(42, (long)v5Count.Count);
    }
}