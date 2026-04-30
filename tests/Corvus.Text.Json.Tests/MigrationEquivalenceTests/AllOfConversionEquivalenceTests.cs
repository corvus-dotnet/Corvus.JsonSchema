// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests.MigrationEquivalenceTests;

using Xunit;

using V4 = MigrationModels.V4;
using V5 = MigrationModels.V5;

/// <summary>
/// Verifies that V4 and V5 allOf composition types support equivalent implicit conversions.
/// </summary>
/// <remarks>
/// <para>V4 and V5: <c>allOf</c> composition supports implicit conversion from composite to constituents.</para>
/// <para>This behaves like implementing multiple interfaces in C# — the composite type "is a" each constituent type.</para>
/// </remarks>
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

    [Fact]
    public void V4_ImplicitConversionToRequiredName()
    {
        var v4Composite = V4.MigrationComposite.Parse(CompositeJson);
        
        // Implicit conversion to allOf constituent type
        V4.MigrationComposite.RequiredName v4Base = v4Composite;
        
        Assert.Equal("Test Item", (string)v4Base.Name);
        Assert.Equal("A test description", (string?)v4Base.Description);
    }

    [Fact]
    public void V5_ImplicitConversionToRequiredName()
    {
        using var parsed = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationComposite>.Parse(CompositeJson);
        V5.MigrationComposite v5Composite = parsed.RootElement;
        
        // Implicit conversion to allOf constituent type (same as V4)
        V5.MigrationComposite.RequiredName v5Base = v5Composite;
        
        Assert.Equal("Test Item", (string)v5Base.Name);
        Assert.Equal("A test description", (string?)v5Base.Description);
    }

    [Fact]
    public void V4_ImplicitConversionToRequiredCount()
    {
        var v4Composite = V4.MigrationComposite.Parse(CompositeJson);
        
        // Implicit conversion to other allOf constituent type
        V4.MigrationComposite.RequiredCountEntity v4Count = v4Composite;
        
        Assert.Equal(42, (int)v4Count.CountValue);
    }

    [Fact]
    public void V5_ImplicitConversionToRequiredCount()
    {
        using var parsed = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationComposite>.Parse(CompositeJson);
        V5.MigrationComposite v5Composite = parsed.RootElement;
        
        // Implicit conversion to other allOf constituent type (same as V4)
        V5.MigrationComposite.RequiredCount v5Count = v5Composite;
        
        Assert.Equal(42, (long)v5Count.Count);
    }

    [Fact]
    public void V4_ExplicitConversionFromRequiredName()
    {
        const string baseJson = """{ "name": "Just Name", "description": "Only name properties" }""";
        var v4Base = V4.MigrationComposite.RequiredName.Parse(baseJson);
        
        // Explicit conversion from constituent to composite
        var v4Composite = (V4.MigrationComposite)v4Base;
        
        Assert.Equal("Just Name", (string)v4Composite.Name);
    }

    [Fact]
    public void V5_ExplicitConversionFromRequiredName()
    {
        const string baseJson = """{ "name": "Just Name", "description": "Only name properties" }""";
        using var parsed = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationComposite.RequiredName>.Parse(baseJson);
        V5.MigrationComposite.RequiredName v5Base = parsed.RootElement;
        
        // Explicit conversion from constituent to composite (same as V4)
        var v5Composite = (V5.MigrationComposite)v5Base;
        
        Assert.Equal("Just Name", (string)v5Composite.Name);
    }

    [Fact]
    public void V4_AllPropertiesAccessibleViaCompositeAndConstituents()
    {
        var v4Composite = V4.MigrationComposite.Parse(CompositeJson);
        
        // Access via composite
        Assert.Equal("Test Item", (string)v4Composite.Name);
        Assert.Equal(42, (int)v4Composite.CountValue);
        Assert.Equal(123.45, (double)v4Composite.Budget);
        
        // Access via RequiredName constituent
        V4.MigrationComposite.RequiredName v4Name = v4Composite;
        Assert.Equal("Test Item", (string)v4Name.Name);
        
        // Access via RequiredCountEntity constituent
        V4.MigrationComposite.RequiredCountEntity v4Count = v4Composite;
        Assert.Equal(42, (int)v4Count.CountValue);
    }

    [Fact]
    public void V5_AllPropertiesAccessibleViaCompositeAndConstituents()
    {
        using var parsed = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationComposite>.Parse(CompositeJson);
        V5.MigrationComposite v5Composite = parsed.RootElement;
        
        // Access via composite (same as V4)
        Assert.Equal("Test Item", (string)v5Composite.Name);
        Assert.Equal(42, (long)v5Composite.Count);
        Assert.Equal(123.45, (double)v5Composite.Budget);
        
        // Access via RequiredName constituent
        V5.MigrationComposite.RequiredName v5Name = v5Composite;
        Assert.Equal("Test Item", (string)v5Name.Name);
        
        // Access via RequiredCount constituent
        V5.MigrationComposite.RequiredCount v5Count = v5Composite;
        Assert.Equal(42, (long)v5Count.Count);
    }
}