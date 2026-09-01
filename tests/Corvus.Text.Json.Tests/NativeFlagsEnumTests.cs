// <copyright file="NativeFlagsEnumTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using Corvus.Text.Json.Tests.GeneratedModels.NativeEnums.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DisabledModels = Corvus.Text.Json.Tests.GeneratedModels.NativeEnums.Disabled.Draft202012;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for the native C# [Flags] enum emitted for objects whose declared properties are all
/// boolean (issue #948): the nested <c>Flags</c> enum, the conversions, the <c>Source</c>
/// conversion, the detection negatives, and the <c>nativeEnums</c> option off switch.
/// </summary>
[TestClass]
public class NativeFlagsEnumTests
{
    [TestMethod]
    public void Flags_ShapeFollowsAlphabeticalPropertyOrder()
    {
        Assert.IsNotNull(typeof(FlagsOptions.Flags).GetCustomAttribute<FlagsAttribute>());
        CollectionAssert.AreEqual(new[] { "None", "Option1", "Option2", "SomeOtherOption" }, Enum.GetNames(typeof(FlagsOptions.Flags)));
        Assert.AreEqual(0, (int)FlagsOptions.Flags.None);
        Assert.AreEqual(1, (int)FlagsOptions.Flags.Option1);
        Assert.AreEqual(2, (int)FlagsOptions.Flags.Option2);
        Assert.AreEqual(4, (int)FlagsOptions.Flags.SomeOtherOption);
    }

    [TestMethod]
    public void InstanceToFlags_SetsABitPerTrueProperty()
    {
        using var doc = ParsedJsonDocument<FlagsOptions>.Parse("""{"option1":true,"some-other-option":true}""");

        FlagsOptions.Flags flags = doc.RootElement;
        Assert.AreEqual(FlagsOptions.Flags.Option1 | FlagsOptions.Flags.SomeOtherOption, flags);
        Assert.IsTrue(flags.HasFlag(FlagsOptions.Flags.Option1));
        Assert.IsFalse(flags.HasFlag(FlagsOptions.Flags.Option2));
    }

    [TestMethod]
    public void InstanceToFlags_FalseAndAbsentPropertiesAreClear()
    {
        using var doc = ParsedJsonDocument<FlagsOptions>.Parse("""{"option1":false}""");

        FlagsOptions.Flags flags = doc.RootElement;
        Assert.AreEqual(FlagsOptions.Flags.None, flags);
    }

    [TestMethod]
    public void InstanceToFlags_ExtraPropertiesAreIgnored()
    {
        using var doc = ParsedJsonDocument<FlagsOptions>.Parse("""{"option1":true,"unknown":123}""");

        FlagsOptions.Flags flags = doc.RootElement;
        Assert.AreEqual(FlagsOptions.Flags.Option1, flags);
    }

    [TestMethod]
    public void InstanceToFlags_NonBooleanPropertyValueIsClear()
    {
        using var doc = ParsedJsonDocument<FlagsOptions>.Parse("""{"option1":"yes","option2":true}""");

        FlagsOptions.Flags flags = doc.RootElement;
        Assert.AreEqual(FlagsOptions.Flags.Option2, flags);
    }

    [TestMethod]
    public void InstanceToFlags_NonObjectValue_Throws()
    {
        using var doc = ParsedJsonDocument<FlagsOptions>.Parse("42");

        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            FlagsOptions.Flags flags = doc.RootElement;
            _ = flags;
        });
    }

    [TestMethod]
    public void TryGetFlags_NonObjectValue_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<FlagsOptions>.Parse("null");

        Assert.IsFalse(doc.RootElement.TryGetFlags(out _));
    }

    [TestMethod]
    public void TryGetFlags_Object_ReturnsTrueWithTheFlags()
    {
        using var doc = ParsedJsonDocument<FlagsOptions>.Parse("""{"option2":true}""");

        Assert.IsTrue(doc.RootElement.TryGetFlags(out FlagsOptions.Flags flags));
        Assert.AreEqual(FlagsOptions.Flags.Option2, flags);
    }

    [TestMethod]
    public void Mutable_InstanceToFlags_MatchesTheValue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<FlagsOptions>.Parse("""{"option1":true,"option2":true}""");
        using JsonDocumentBuilder<FlagsOptions.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        FlagsOptions.Flags flags = builder.RootElement;
        Assert.AreEqual(FlagsOptions.Flags.Option1 | FlagsOptions.Flags.Option2, flags);
        Assert.IsTrue(builder.RootElement.TryGetFlags(out FlagsOptions.Flags tried));
        Assert.AreEqual(FlagsOptions.Flags.Option1 | FlagsOptions.Flags.Option2, tried);
    }

    [TestMethod]
    public void SourceConversion_CreateAcceptsTheFlagsDirectly()
    {
        using var doc = FlagsParent.Create(settings: FlagsOptions.Flags.Option1 | FlagsOptions.Flags.SomeOtherOption);

        Assert.AreEqual("""{"settings":{"option1":true,"option2":false,"some-other-option":true}}""", doc.RootElement.ToString());
    }

    [TestMethod]
    public void SourceConversion_NoneWritesEveryPropertyFalse()
    {
        using var doc = FlagsParent.Create(settings: FlagsOptions.Flags.None);

        Assert.AreEqual("""{"settings":{"option1":false,"option2":false,"some-other-option":false}}""", doc.RootElement.ToString());
    }

    [TestMethod]
    public void NoneCollision_PropertyNamedNoneIsSuffixed()
    {
        CollectionAssert.AreEqual(new[] { "None", "None1", "Other" }, Enum.GetNames(typeof(FlagsNoneCollision.Flags)));
        Assert.AreEqual(0, (int)FlagsNoneCollision.Flags.None);
        Assert.AreEqual(1, (int)FlagsNoneCollision.Flags.None1);
        Assert.AreEqual(2, (int)FlagsNoneCollision.Flags.Other);
    }

    [TestMethod]
    public void RequiredBooleanProperty_StillDetectsAsFlags()
    {
        using var doc = ParsedJsonDocument<FlagsWithRequired>.Parse("""{"enabled":true}""");

        FlagsWithRequired.Flags flags = doc.RootElement;
        Assert.AreEqual(FlagsWithRequired.Flags.Enabled, flags);
    }

    [TestMethod]
    public void NonFlagsShapes_DoNotEmitFlags()
    {
        Assert.IsNull(typeof(NotFlagsMixed).GetNestedType("Flags"));
        Assert.IsNull(typeof(NotFlagsSingle).GetNestedType("Flags"));
        Assert.IsNull(typeof(NotFlagsPattern).GetNestedType("Flags"));
        Assert.IsNull(typeof(NotFlagsOpen).GetNestedType("Flags"));
        Assert.IsNull(typeof(NotFlagsConst).GetNestedType("Flags"));
        Assert.IsNull(typeof(NotFlagsTooMany).GetNestedType("Flags"));
    }

    [TestMethod]
    public void NativeEnumsNone_DoesNotEmitFlags()
    {
        Assert.IsNull(typeof(DisabledModels.DisabledFlags).GetNestedType("Flags"));
    }
}