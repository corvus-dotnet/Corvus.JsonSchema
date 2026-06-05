// Copyright (c) Matthew Adams. All rights reserved.
// Licensed under the Apache-2.0 license.

using Corvus.Json;
using Corvus.Text.Json.Tests.GeneratedModels.V4NonNullDefaulted.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for the V4 engine's <c>OptionalAsNullable=NullOrUndefinedExceptNonNullDefaulted</c> behaviour.
/// Optional properties with a non-null <c>default</c> are generated as non-nullable <c>T</c>; optional
/// properties without a default, or with a <c>default</c> of JSON <c>null</c>, remain nullable <c>T?</c>.
/// </summary>
[TestClass]
public class GeneratedV4NonNullDefaultedTests
{
    #region Non-null default — generated as non-nullable T

    [TestMethod]
    public void NonNullDefault_StatusMissing_ReturnsDefault()
    {
        ObjectWithDefaultProperties value = ObjectWithDefaultProperties.Parse("""{"name":"test"}""");

        // The explicit non-nullable type is a compile-time assertion of non-nullability.
        ObjectWithDefaultProperties.StatusEntity status = value.Status;
        Assert.AreEqual("active", (string)status);
    }

    [TestMethod]
    public void NonNullDefault_StatusPresent_ReturnsValue()
    {
        ObjectWithDefaultProperties value = ObjectWithDefaultProperties.Parse("""{"name":"test","status":"archived"}""");

        ObjectWithDefaultProperties.StatusEntity status = value.Status;
        Assert.AreEqual("archived", (string)status);
    }

    [TestMethod]
    public void NonNullDefault_StatusExplicitNull_ReturnsNullKindNotDefault()
    {
        // A non-nullable property cannot return C# null; an explicit JSON null surfaces as a Null-kind value.
        ObjectWithDefaultProperties value = ObjectWithDefaultProperties.Parse("""{"name":"test","status":null}""");

        ObjectWithDefaultProperties.StatusEntity status = value.Status;
        Assert.IsTrue(status.IsNull());
    }

    #endregion

    #region No default — remains nullable T?

    [TestMethod]
    public void NoDefault_LabelMissing_ReturnsNull()
    {
        ObjectWithDefaultProperties value = ObjectWithDefaultProperties.Parse("""{"name":"test"}""");

        ObjectWithDefaultProperties.LabelEntity? label = value.Label;
        Assert.IsNull(label);
    }

    [TestMethod]
    public void NoDefault_LabelExplicitNull_ReturnsNull()
    {
        ObjectWithDefaultProperties value = ObjectWithDefaultProperties.Parse("""{"name":"test","label":null}""");

        Assert.IsNull(value.Label);
    }

    [TestMethod]
    public void NoDefault_LabelPresent_ReturnsValue()
    {
        ObjectWithDefaultProperties value = ObjectWithDefaultProperties.Parse("""{"name":"test","label":"hi"}""");

        Assert.IsNotNull(value.Label);
        Assert.AreEqual("hi", (string)value.Label!.Value);
    }

    #endregion

    #region Null default — flips back to nullable T?, C# null when absent

    [TestMethod]
    public void NullDefault_NicknameMissing_ReturnsNull()
    {
        // The default is JSON null, so the property stays nullable and an absent property returns C# null.
        ObjectWithDefaultProperties value = ObjectWithDefaultProperties.Parse("""{"name":"test"}""");

        ObjectWithDefaultProperties.NicknameEntity? nickname = value.Nickname;
        Assert.IsNull(nickname);
    }

    [TestMethod]
    public void NullDefault_NicknameExplicitNull_ReturnsNull()
    {
        ObjectWithDefaultProperties value = ObjectWithDefaultProperties.Parse("""{"name":"test","nickname":null}""");

        Assert.IsNull(value.Nickname);
    }

    [TestMethod]
    public void NullDefault_NicknamePresent_ReturnsValue()
    {
        ObjectWithDefaultProperties value = ObjectWithDefaultProperties.Parse("""{"name":"test","nickname":"bob"}""");

        Assert.IsNotNull(value.Nickname);
    }

    #endregion
}
