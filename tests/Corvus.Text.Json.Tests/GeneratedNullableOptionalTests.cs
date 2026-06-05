// Copyright (c) Matthew Adams. All rights reserved.
// Licensed under the Apache-2.0 license.

using System.Text.Json;
using Corvus.Text.Json.Tests.GeneratedModels.OptionalAsNullable.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests that verify the OptionalAsNullable=NullOrUndefined behavior.
/// When enabled, optional properties return <c>T?</c> (nullable struct) and
/// absent properties return <see langword="null"/> instead of an undefined value.
/// </summary>
[TestClass]
public class GeneratedNullableOptionalTests
{
    #region Nullable optional property — present returns value

    [TestMethod]
    public void NullableOptional_PresentEmail_ReturnsValue()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"Alice","age":30,"email":"a@b.com"}""");

        JsonEmail? email = doc.RootElement.Email;
        Assert.IsNotNull(email);
        Assert.AreEqual("a@b.com", email.Value.ToString());
    }

    [TestMethod]
    public void NullableOptional_PresentIsActive_ReturnsValue()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"Alice","age":30,"isActive":true}""");

        JsonBoolean? isActive = doc.RootElement.IsActive;
        Assert.IsNotNull(isActive);
        Assert.IsTrue((bool)isActive.Value);
    }

    #endregion

    #region Nullable optional property — absent returns null

    [TestMethod]
    public void NullableOptional_AbsentEmail_ReturnsNull()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"Alice","age":30}""");

        Assert.IsNull(doc.RootElement.Email);
    }

    [TestMethod]
    public void NullableOptional_AbsentIsActive_ReturnsNull()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"Alice","age":30}""");

        Assert.IsNull(doc.RootElement.IsActive);
    }

    #endregion

    #region Nullable optional property — explicit JSON null returns null

    [TestMethod]
    public void NullableOptional_ExplicitNullEmail_ReturnsNull()
    {
        // The "NullOrUndefined" contract: an explicit JSON null maps to a C# null,
        // not to a Null-kind value. (Regression test for the V5 collapse bug.)
        using var doc =
            ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"Alice","age":30,"email":null}""");

        Assert.IsNull(doc.RootElement.Email);
    }

    [TestMethod]
    public void NullableOptional_ExplicitNullIsActive_ReturnsNull()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"Alice","age":30,"isActive":null}""");

        Assert.IsNull(doc.RootElement.IsActive);
    }

    #endregion

    #region Nullable optional — mutable set and remove

    [TestMethod]
    public void NullableOptional_Mutable_SetEmail_ThenRemove_ReturnsNull()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"Alice","age":30,"email":"a@b.com"}""");
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;

        // Email is present — verify non-null
        Assert.AreEqual("a@b.com", root.Email?.ToString());

        // Remove it — verify null
        root.RemoveEmail();
        Assert.IsNull(root.Email);
    }

    [TestMethod]
    public void NullableOptional_Mutable_SetUndefined_RemovesAndReturnsNull()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"Alice","age":30,"isActive":true}""");
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetIsActive(default);
        Assert.IsNull(root.IsActive);
    }

    #endregion

    #region Nullable optional — defaults with OptionalAsNullable

    [TestMethod]
    public void NullableOptional_DefaultProperty_MissingReturnsDefaultNotNull()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test"}""");

        ObjectWithDefaultProperties.StatusEntity? status = doc.RootElement.Status;
        Assert.IsNotNull(status);
        Assert.AreEqual("active", (string)status.Value);
    }

    [TestMethod]
    public void NullableOptional_PropertyWithoutDefault_MissingReturnsNull()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test"}""");

        JsonString? label = doc.RootElement.Label;
        Assert.IsNull(label);
    }

    #endregion

    #region Nullable optional — nested objects

    [TestMethod]
    public void NullableOptional_NestedObject_AbsentNotes_ReturnsNull()
    {
        using var doc =
            ParsedJsonDocument<NestedObject>.Parse("""{"address":{"street":"123 Main","city":"X","zip":"00000"}}""");

        Assert.IsNull(doc.RootElement.Notes);
    }

    [TestMethod]
    public void NullableOptional_NestedObject_PresentNotes_ReturnsValue()
    {
        using var doc =
            ParsedJsonDocument<NestedObject>.Parse("""{"address":{"street":"123 Main","city":"X","zip":"00000"},"notes":"hello"}""");

        Assert.IsNotNull(doc.RootElement.Notes);
        Assert.AreEqual("hello", doc.RootElement.Notes?.ToString());
    }

    #endregion

    #region Nullable optional — composition

    [TestMethod]
    public void NullableOptional_CompositionAllOf_OptionalProperties_ReturnNullWhenAbsent()
    {
        using var doc =
            ParsedJsonDocument<CompositionAllOf>.Parse("{}");

        Assert.IsNull(doc.RootElement.FirstName);
        Assert.IsNull(doc.RootElement.LastName);
    }

    [TestMethod]
    public void NullableOptional_CompositionAllOf_OptionalProperties_ReturnValueWhenPresent()
    {
        using var doc =
            ParsedJsonDocument<CompositionAllOf>.Parse("""{"firstName":"Alice","lastName":"Smith"}""");

        Assert.AreEqual("Alice", doc.RootElement.FirstName?.ToString());
        Assert.AreEqual("Smith", doc.RootElement.LastName?.ToString());
    }

    #endregion

    #region Nullable optional — array property

    [TestMethod]
    public void NullableOptional_AbsentArrayProperty_ReturnsNull()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithArrayProperty>.Parse("""{"tags":["a"]}""");

        Assert.IsNull(doc.RootElement.Scores);
    }

    [TestMethod]
    public void NullableOptional_PresentArrayProperty_ReturnsValue()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithArrayProperty>.Parse("""{"tags":["a"],"scores":[1.0,2.0]}""");

        Assert.IsNotNull(doc.RootElement.Scores);
        Assert.AreEqual(2, doc.RootElement.Scores!.Value.GetArrayLength());
    }

    #endregion

    #region Nullable optional — indexer with optional array item property

    [TestMethod]
    public void NullableOptional_Indexer_OptionalLabel_ReturnsNullWhenAbsent()
    {
        using var doc =
            ParsedJsonDocument<ArrayOfItems>.Parse("""[{"id":1}]""");

        Assert.IsNull(doc.RootElement[0].Label);
    }

    [TestMethod]
    public void NullableOptional_Indexer_OptionalLabel_ReturnsValueWhenPresent()
    {
        using var doc =
            ParsedJsonDocument<ArrayOfItems>.Parse("""[{"id":1,"label":"hello"}]""");

        Assert.AreEqual("hello", (string)doc.RootElement[0].Label!);
    }

    #endregion

    #region Round-trip with nullable optional

    [TestMethod]
    public void NullableOptional_RoundTrip_OptionalPropertiesPreserved()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"X","age":0}""");
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetEmail("test@test.com");
        root.SetIsActive(true);
        string json = root.ToString();

        using var roundTrip = ParsedJsonDocument<ObjectWithMixedProperties>.Parse(json);
        Assert.AreEqual("test@test.com", roundTrip.RootElement.Email?.ToString());
        Assert.IsTrue((bool)roundTrip.RootElement.IsActive!.Value);
    }

    #endregion
}
