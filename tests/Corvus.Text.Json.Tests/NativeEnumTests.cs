// <copyright file="NativeEnumTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Tests.GeneratedModels.NativeEnums.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DisabledModels = Corvus.Text.Json.Tests.GeneratedModels.NativeEnums.Disabled.Draft202012;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for the native C# enum emitted for pure string-enum schemas (issue #948):
/// the nested <c>KnownValues</c> enum, the conversions in both directions, the
/// <c>Source</c> conversion, and the <c>nativeEnums</c> option off switch.
/// </summary>
[TestClass]
public class NativeEnumTests
{
    [TestMethod]
    public void KnownValues_OrdinalsFollowSchemaOrder()
    {
        Assert.AreEqual(0, (int)NativeEnumColor.KnownValues.Red);
        Assert.AreEqual(1, (int)NativeEnumColor.KnownValues.Green);
        Assert.AreEqual(2, (int)NativeEnumColor.KnownValues.BlueIsh);
        CollectionAssert.AreEqual(new[] { "Red", "Green", "BlueIsh" }, Enum.GetNames(typeof(NativeEnumColor.KnownValues)));
    }

    [TestMethod]
    public void EnumToInstance_ProducesTheCorrespondingJsonString()
    {
        NativeEnumColor red = NativeEnumColor.KnownValues.Red;
        NativeEnumColor blueIsh = NativeEnumColor.KnownValues.BlueIsh;

        Assert.AreEqual("red", red.ToString());
        Assert.AreEqual("blue-ish", blueIsh.ToString());
        Assert.IsTrue(red == NativeEnumColor.EnumValues.Red);
    }

    [TestMethod]
    public void EnumToInstance_UndefinedMember_Throws()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            NativeEnumColor value = (NativeEnumColor.KnownValues)42;
            _ = value;
        });
    }

    [TestMethod]
    public void InstanceToEnum_MatchesTheParsedValue()
    {
        using var doc = ParsedJsonDocument<NativeEnumColor>.Parse("\"green\"");

        NativeEnumColor.KnownValues value = doc.RootElement;
        Assert.AreEqual(NativeEnumColor.KnownValues.Green, value);
    }

    [TestMethod]
    public void InstanceToEnum_OutOfEnumValue_Throws()
    {
        using var doc = ParsedJsonDocument<NativeEnumColor>.Parse("\"purple\"");

        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            NativeEnumColor.KnownValues value = doc.RootElement;
            _ = value;
        });
    }

    [TestMethod]
    public void TryGetKnownValue_KnownValue_ReturnsTrue()
    {
        using var doc = ParsedJsonDocument<NativeEnumColor>.Parse("\"blue-ish\"");

        Assert.IsTrue(doc.RootElement.TryGetKnownValue(out NativeEnumColor.KnownValues value));
        Assert.AreEqual(NativeEnumColor.KnownValues.BlueIsh, value);
    }

    [TestMethod]
    public void TryGetKnownValue_OutOfEnumValue_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<NativeEnumColor>.Parse("\"purple\"");

        Assert.IsFalse(doc.RootElement.TryGetKnownValue(out _));
    }

    [TestMethod]
    public void TryGetKnownValue_NonStringValue_ReturnsFalse()
    {
        using var doc = ParsedJsonDocument<NativeEnumColor>.Parse("42");

        Assert.IsFalse(doc.RootElement.TryGetKnownValue(out _));
    }

    [TestMethod]
    public void Mutable_InstanceToEnum_MatchesTheValue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<NativeEnumColor>.Parse("\"green\"");
        using JsonDocumentBuilder<NativeEnumColor.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        NativeEnumColor.KnownValues value = builder.RootElement;
        Assert.AreEqual(NativeEnumColor.KnownValues.Green, value);
        Assert.IsTrue(builder.RootElement.TryGetKnownValue(out NativeEnumColor.KnownValues tried));
        Assert.AreEqual(NativeEnumColor.KnownValues.Green, tried);
    }

    [TestMethod]
    public void SourceConversion_ObjectCreateAcceptsTheEnumDirectly()
    {
        using var doc = ObjectWithEnums.Create(color: ObjectWithEnums.Color.KnownValues.Red);

        Assert.AreEqual("""{"color":"red"}""", doc.RootElement.ToString());
    }

    [TestMethod]
    public void SourceConversion_ArrayBuilderAcceptsTheEnumDirectly()
    {
        using var doc = ObjectWithEnums.Create(
            colors: ObjectWithEnums.ColorArray.Build(static (ref ObjectWithEnums.ColorArray.Builder builder) =>
            {
                builder.AddItem(ObjectWithEnums.Color.KnownValues.Red);
                builder.AddItem(ObjectWithEnums.Color.KnownValues.BlueIsh);
            }));

        Assert.AreEqual("""{"colors":["red","blue-ish"]}""", doc.RootElement.ToString());
    }

    [TestMethod]
    public void CaseCollision_MembersAreSuffixedInSchemaOrder()
    {
        CollectionAssert.AreEqual(new[] { "MicrosoftValue", "MicrosoftValue1", "Google" }, Enum.GetNames(typeof(CaseCollisionEnum.KnownValues)));
    }

    [TestMethod]
    public void CaseCollision_EachMemberRoundTripsItsOwnCasing()
    {
        CaseCollisionEnum upper = CaseCollisionEnum.KnownValues.MicrosoftValue;
        CaseCollisionEnum lower = CaseCollisionEnum.KnownValues.MicrosoftValue1;

        Assert.AreEqual("Microsoft", upper.ToString());
        Assert.AreEqual("microsoft", lower.ToString());

        using var doc = ParsedJsonDocument<CaseCollisionEnum>.Parse("\"microsoft\"");
        CaseCollisionEnum.KnownValues value = doc.RootElement;
        Assert.AreEqual(CaseCollisionEnum.KnownValues.MicrosoftValue1, value);
    }

    [TestMethod]
    public void SingleValueEnum_DoesNotEmitKnownValues()
    {
        Assert.IsNull(typeof(SingleValueEnum).GetNestedType("KnownValues"));
    }

    [TestMethod]
    public void MixedValueEnum_DoesNotEmitKnownValues()
    {
        Assert.IsNull(typeof(MixedValueEnum).GetNestedType("KnownValues"));
    }

    [TestMethod]
    public void NativeEnumsNone_DoesNotEmitKnownValues()
    {
        Assert.IsNull(typeof(DisabledModels.DisabledColor).GetNestedType("KnownValues"));
    }
}