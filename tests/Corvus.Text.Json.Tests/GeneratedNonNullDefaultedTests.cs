// Copyright (c) Matthew Adams. All rights reserved.
// Licensed under the Apache-2.0 license.

using System.Text.Json;
using Corvus.Text.Json.Tests.GeneratedModels.NullOrUndefinedExceptNonNullDefaulted.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for the <c>OptionalAsNullable=NullOrUndefinedExceptNonNullDefaulted</c> behaviour.
/// Optional properties with a non-null <c>default</c> are generated as non-nullable <c>T</c>;
/// optional properties without a default, or with a <c>default</c> of JSON <c>null</c>,
/// remain nullable <c>T?</c>.
/// </summary>
[TestClass]
public class GeneratedNonNullDefaultedTests
{
    #region Non-null default — generated as non-nullable T

    [TestMethod]
    public void NonNullDefault_StatusMissing_ReturnsDefault()
    {
        using var doc = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test"}""");

        // The explicit non-nullable type here is a compile-time assertion that the
        // property is generated as non-nullable.
        ObjectWithDefaultProperties.StatusEntity status = doc.RootElement.Status;
        Assert.AreEqual("active", (string)status);
    }

    [TestMethod]
    public void NonNullDefault_StatusPresent_ReturnsValue()
    {
        using var doc = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test","status":"archived"}""");

        ObjectWithDefaultProperties.StatusEntity status = doc.RootElement.Status;
        Assert.AreEqual("archived", (string)status);
    }

    [TestMethod]
    public void NonNullDefault_StatusExplicitNull_ReturnsNullKindNotDefault()
    {
        // A non-nullable property cannot return C# null, so an explicit JSON null
        // surfaces as a Null-kind value (not the default, not C# null).
        using var doc = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test","status":null}""");

        ObjectWithDefaultProperties.StatusEntity status = doc.RootElement.Status;
        Assert.AreEqual(JsonValueKind.Null, status.ValueKind);
    }

    [TestMethod]
    public void NonNullDefault_CountMissing_ReturnsDefault()
    {
        using var doc = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test"}""");

        ObjectWithDefaultProperties.CountEntity count = doc.RootElement.Count;
        Assert.AreEqual(0, (int)count);
    }

    #endregion

    #region No default — remains nullable T?

    [TestMethod]
    public void NoDefault_LabelMissing_ReturnsNull()
    {
        using var doc = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test"}""");

        JsonString? label = doc.RootElement.Label;
        Assert.IsNull(label);
    }

    [TestMethod]
    public void NoDefault_LabelExplicitNull_ReturnsNull()
    {
        using var doc = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test","label":null}""");

        Assert.IsNull(doc.RootElement.Label);
    }

    [TestMethod]
    public void NoDefault_LabelPresent_ReturnsValue()
    {
        using var doc = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test","label":"hi"}""");

        Assert.AreEqual("hi", doc.RootElement.Label?.ToString());
    }

    #endregion

    #region Null default — flips back to nullable T?, C# null when absent

    [TestMethod]
    public void NullDefault_NicknameMissing_ReturnsNull()
    {
        // The default is JSON null, so the property stays nullable and an absent
        // property returns C# null (not a Null-kind default instance).
        using var doc = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test"}""");

        ObjectWithDefaultProperties.NicknameEntity? nickname = doc.RootElement.Nickname;
        Assert.IsNull(nickname);
    }

    [TestMethod]
    public void NullDefault_NicknameExplicitNull_ReturnsNull()
    {
        using var doc = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test","nickname":null}""");

        Assert.IsNull(doc.RootElement.Nickname);
    }

    [TestMethod]
    public void NullDefault_NicknamePresent_ReturnsValue()
    {
        using var doc = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test","nickname":"bob"}""");

        Assert.IsNotNull(doc.RootElement.Nickname);
    }

    #endregion
}
