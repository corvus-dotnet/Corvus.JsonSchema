// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Reflection;
using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Reflection-based tests that verify the code generator correctly emits or omits
/// object mutator methods (SetProperty, RemoveProperty, TryGetProperty, AddProperty)
/// based on the evaluation mode and pattern properties configuration.
/// <list type="bullet">
///   <item>Local patternProperties + additionalProperties: false → emit mutators</item>
///   <item>Composed patternProperties + additionalProperties: false → do NOT emit</item>
///   <item>Composed patternProperties + unevaluatedProperties: false → emit mutators</item>
///   <item>No patternProperties + additionalProperties: false → do NOT emit</item>
///   <item>No patternProperties + unevaluatedProperties: false → do NOT emit</item>
/// </list>
/// </summary>
public class PatternPropertyMethodPresenceTests
{
    /// <summary>
    /// Test cases for types whose Mutable should have SetProperty/RemoveProperty/TryGetProperty.
    /// </summary>
    public static TheoryData<Type, string> TypesWithMutators => new()
    {
        // Rule 1: local patternProperties + additionalProperties: false → emit
        { typeof(ObjectWithPatternProperties.Mutable), "local patterns + additionalProperties: false" },

        // Rule 4: composed patternProperties + unevaluatedProperties: false → emit
        { typeof(ObjectWithUnevaluatedPatternProperties.Mutable), "composed patterns + unevaluatedProperties: false" },
    };

    /// <summary>
    /// Test cases for types whose Mutable should NOT have SetProperty/RemoveProperty/TryGetProperty.
    /// </summary>
    public static TheoryData<Type, string> TypesWithoutMutators => new()
    {
        // Rule 2: composed patternProperties + additionalProperties: false → do NOT emit
        { typeof(ObjectWithRefPatternProperties.Mutable), "composed patterns via $ref + additionalProperties: false" },
        { typeof(ObjectWithAllOfPatternProperties.Mutable), "composed patterns via allOf/$ref + additionalProperties: false" },

        // No patternProperties + additionalProperties: false → do NOT emit
        { typeof(ClosedObjectNoPatterns.Mutable), "no patterns + additionalProperties: false" },

        // No patternProperties + unevaluatedProperties: false → do NOT emit
        { typeof(UnevaluatedClosedObjectNoPatterns.Mutable), "no patterns + unevaluatedProperties: false" },
    };

    /// <summary>
    /// Test cases for immutable types that should have TryGetProperty.
    /// </summary>
    public static TheoryData<Type, string> ImmutableTypesWithTryGetProperty => new()
    {
        { typeof(ObjectWithPatternProperties), "local patterns + additionalProperties: false" },
        { typeof(ObjectWithUnevaluatedPatternProperties), "composed patterns + unevaluatedProperties: false" },
    };

    /// <summary>
    /// Test cases for immutable types that should NOT have TryGetProperty.
    /// </summary>
    public static TheoryData<Type, string> ImmutableTypesWithoutTryGetProperty => new()
    {
        { typeof(ObjectWithRefPatternProperties), "composed patterns via $ref + additionalProperties: false" },
        { typeof(ObjectWithAllOfPatternProperties), "composed patterns via allOf/$ref + additionalProperties: false" },
        { typeof(ClosedObjectNoPatterns), "no patterns + additionalProperties: false" },
        { typeof(UnevaluatedClosedObjectNoPatterns), "no patterns + unevaluatedProperties: false" },
    };

    // -------------------------------------------------------
    // SetProperty on Mutable
    // -------------------------------------------------------

    [Theory]
    [MemberData(nameof(TypesWithMutators))]
    public void MutableType_HasSetProperty(Type mutableType, string scenario)
    {
        Assert.True(
            HasPublicInstanceMethod(mutableType, "SetProperty"),
            $"Expected SetProperty on {mutableType.Name} ({scenario})");
    }

    [Theory]
    [MemberData(nameof(TypesWithoutMutators))]
    public void MutableType_DoesNotHaveSetProperty(Type mutableType, string scenario)
    {
        Assert.False(
            HasPublicInstanceMethod(mutableType, "SetProperty"),
            $"Expected NO SetProperty on {mutableType.Name} ({scenario})");
    }

    // -------------------------------------------------------
    // RemoveProperty on Mutable
    // -------------------------------------------------------

    [Theory]
    [MemberData(nameof(TypesWithMutators))]
    public void MutableType_HasRemoveProperty(Type mutableType, string scenario)
    {
        Assert.True(
            HasPublicInstanceMethod(mutableType, "RemoveProperty"),
            $"Expected RemoveProperty on {mutableType.Name} ({scenario})");
    }

    [Theory]
    [MemberData(nameof(TypesWithoutMutators))]
    public void MutableType_DoesNotHaveRemoveProperty(Type mutableType, string scenario)
    {
        Assert.False(
            HasPublicInstanceMethod(mutableType, "RemoveProperty"),
            $"Expected NO RemoveProperty on {mutableType.Name} ({scenario})");
    }

    // -------------------------------------------------------
    // TryGetProperty on Mutable
    // -------------------------------------------------------

    [Theory]
    [MemberData(nameof(TypesWithMutators))]
    public void MutableType_HasTryGetProperty(Type mutableType, string scenario)
    {
        Assert.True(
            HasPublicInstanceMethod(mutableType, "TryGetProperty"),
            $"Expected TryGetProperty on {mutableType.Name} ({scenario})");
    }

    [Theory]
    [MemberData(nameof(TypesWithoutMutators))]
    public void MutableType_DoesNotHaveTryGetProperty(Type mutableType, string scenario)
    {
        Assert.False(
            HasPublicInstanceMethod(mutableType, "TryGetProperty"),
            $"Expected NO TryGetProperty on {mutableType.Name} ({scenario})");
    }

    // -------------------------------------------------------
    // TryGetProperty on immutable type
    // -------------------------------------------------------

    [Theory]
    [MemberData(nameof(ImmutableTypesWithTryGetProperty))]
    public void ImmutableType_HasTryGetProperty(Type immutableType, string scenario)
    {
        Assert.True(
            HasPublicInstanceMethod(immutableType, "TryGetProperty"),
            $"Expected TryGetProperty on {immutableType.Name} ({scenario})");
    }

    [Theory]
    [MemberData(nameof(ImmutableTypesWithoutTryGetProperty))]
    public void ImmutableType_DoesNotHaveTryGetProperty(Type immutableType, string scenario)
    {
        Assert.False(
            HasPublicInstanceMethod(immutableType, "TryGetProperty"),
            $"Expected NO TryGetProperty on {immutableType.Name} ({scenario})");
    }

    // -------------------------------------------------------
    // AddProperty on Builder
    // -------------------------------------------------------

    [Fact]
    public void BuilderType_HasAddProperty_LocalPatternsWithAdditionalPropertiesFalse()
    {
        Assert.True(
            HasPublicInstanceMethod(typeof(ObjectWithPatternProperties.Builder), "AddProperty"),
            "Expected AddProperty on ObjectWithPatternProperties.Builder");
    }

    [Fact]
    public void BuilderType_HasAddProperty_ComposedPatternsWithUnevaluatedPropertiesFalse()
    {
        Assert.True(
            HasPublicInstanceMethod(typeof(ObjectWithUnevaluatedPatternProperties.Builder), "AddProperty"),
            "Expected AddProperty on ObjectWithUnevaluatedPatternProperties.Builder");
    }

    [Fact]
    public void BuilderType_DoesNotHaveAddProperty_ComposedPatternsViaRefWithAdditionalPropertiesFalse()
    {
        Assert.False(
            HasPublicInstanceMethod(typeof(ObjectWithRefPatternProperties.Builder), "AddProperty"),
            "Expected NO AddProperty on ObjectWithRefPatternProperties.Builder");
    }

    [Fact]
    public void BuilderType_DoesNotHaveAddProperty_ComposedPatternsViaAllOfWithAdditionalPropertiesFalse()
    {
        Assert.False(
            HasPublicInstanceMethod(typeof(ObjectWithAllOfPatternProperties.Builder), "AddProperty"),
            "Expected NO AddProperty on ObjectWithAllOfPatternProperties.Builder");
    }

    [Fact]
    public void BuilderType_DoesNotHaveAddProperty_NoPatternsWithAdditionalPropertiesFalse()
    {
        Assert.False(
            HasPublicInstanceMethod(typeof(ClosedObjectNoPatterns.Builder), "AddProperty"),
            "Expected NO AddProperty on ClosedObjectNoPatterns.Builder");
    }

    [Fact]
    public void BuilderType_DoesNotHaveAddProperty_NoPatternsWithUnevaluatedPropertiesFalse()
    {
        Assert.False(
            HasPublicInstanceMethod(typeof(UnevaluatedClosedObjectNoPatterns.Builder), "AddProperty"),
            "Expected NO AddProperty on UnevaluatedClosedObjectNoPatterns.Builder");
    }

    // -------------------------------------------------------
    // Helper
    // -------------------------------------------------------

    private static bool HasPublicInstanceMethod(Type type, string methodName)
    {
        return type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(m => m.Name == methodName);
    }
}