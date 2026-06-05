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

    #region Mutable (object-backed) getters

    [TestMethod]
    public void Mutable_NonNullDefault_Status_PresentAndSet()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test","status":"archived"}""");
        using JsonDocumentBuilder<ObjectWithDefaultProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithDefaultProperties.Mutable root = builder.RootElement;

        // Non-nullable mutable getter, present path (object backing).
        ObjectWithDefaultProperties.StatusEntity.Mutable status = root.Status;
        Assert.AreEqual("archived", (string)status);

        root.SetStatus("active");
        Assert.AreEqual("active", (string)root.Status);
    }

    [TestMethod]
    public void Mutable_NoDefault_Label_PresentRemovedAbsent()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test","label":"hi"}""");
        using JsonDocumentBuilder<ObjectWithDefaultProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithDefaultProperties.Mutable root = builder.RootElement;

        // Nullable mutable getter, present path.
        Assert.AreEqual("hi", root.Label?.ToString());

        // Absent path returns C# null.
        root.RemoveLabel();
        Assert.IsNull(root.Label);
    }

    [TestMethod]
    public void Mutable_NullDefault_Nickname_PresentRemovedAbsent()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test","nickname":"bob"}""");
        using JsonDocumentBuilder<ObjectWithDefaultProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithDefaultProperties.Mutable root = builder.RootElement;

        Assert.IsNotNull(root.Nickname);

        root.RemoveNickname();
        Assert.IsNull(root.Nickname);
    }

    [TestMethod]
    public void Mutable_NoDefault_Label_ExplicitNull_ReturnsNull()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test","label":null}""");
        using JsonDocumentBuilder<ObjectWithDefaultProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        // Mutable nullable getter, present-null path collapses to C# null.
        Assert.IsNull(builder.RootElement.Label);
    }

    [TestMethod]
    public void Mutable_NullDefault_Nickname_ExplicitNull_ReturnsNull()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test","nickname":null}""");
        using JsonDocumentBuilder<ObjectWithDefaultProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        Assert.IsNull(builder.RootElement.Nickname);
    }

    [TestMethod]
    public void Mutable_NonNullDefault_Status_Absent_NotSynthesised()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test"}""");
        using JsonDocumentBuilder<ObjectWithDefaultProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        // The mutable view does not synthesise the schema default for an absent property
        // (the default is materialised on the immutable read), exercising the absent path.
        ObjectWithDefaultProperties.Mutable root = builder.RootElement;
        _ = root.Status;
        Assert.IsFalse(root.ToString().Contains("status"));
    }

    [TestMethod]
    public void Mutable_RoundTrip_NonNullableStatusPreserved()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test"}""");
        using JsonDocumentBuilder<ObjectWithDefaultProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithDefaultProperties.Mutable root = builder.RootElement;
        root.SetStatus("archived");
        string json = root.ToString();

        using var roundTrip = ParsedJsonDocument<ObjectWithDefaultProperties>.Parse(json);
        Assert.AreEqual("archived", (string)roundTrip.RootElement.Status);
    }

    #endregion
}
