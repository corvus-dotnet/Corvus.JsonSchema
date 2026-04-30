// Copyright (c) Matthew Adams. All rights reserved.
// Licensed under the Apache-2.0 license.

using System.Text.Json;
using Corvus.Text.Json.Tests.GeneratedModels.OptionalAsNullable.Draft202012;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests that verify the OptionalAsNullable=NullOrUndefined behavior.
/// When enabled, optional properties return <c>T?</c> (nullable struct) and
/// absent properties return <see langword="null"/> instead of an undefined value.
/// </summary>
public class GeneratedNullableOptionalTests
{
    #region Nullable optional property — present returns value

    [Fact]
    public void NullableOptional_PresentEmail_ReturnsValue()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"Alice","age":30,"email":"a@b.com"}""");

        JsonEmail? email = doc.RootElement.Email;
        Assert.NotNull(email);
        Assert.Equal("a@b.com", email.Value.ToString());
    }

    [Fact]
    public void NullableOptional_PresentIsActive_ReturnsValue()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"Alice","age":30,"isActive":true}""");

        JsonBoolean? isActive = doc.RootElement.IsActive;
        Assert.NotNull(isActive);
        Assert.True((bool)isActive.Value);
    }

    #endregion

    #region Nullable optional property — absent returns null

    [Fact]
    public void NullableOptional_AbsentEmail_ReturnsNull()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"Alice","age":30}""");

        Assert.Null(doc.RootElement.Email);
    }

    [Fact]
    public void NullableOptional_AbsentIsActive_ReturnsNull()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"Alice","age":30}""");

        Assert.Null(doc.RootElement.IsActive);
    }

    #endregion

    #region Nullable optional — mutable set and remove

    [Fact]
    public void NullableOptional_Mutable_SetEmail_ThenRemove_ReturnsNull()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"Alice","age":30,"email":"a@b.com"}""");
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;

        // Email is present — verify non-null
        Assert.Equal("a@b.com", root.Email?.ToString());

        // Remove it — verify null
        root.RemoveEmail();
        Assert.Null(root.Email);
    }

    [Fact]
    public void NullableOptional_Mutable_SetUndefined_RemovesAndReturnsNull()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<ObjectWithMixedProperties>.Parse("""{"name":"Alice","age":30,"isActive":true}""");
        using JsonDocumentBuilder<ObjectWithMixedProperties.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        ObjectWithMixedProperties.Mutable root = builder.RootElement;
        root.SetIsActive(default);
        Assert.Null(root.IsActive);
    }

    #endregion

    #region Nullable optional — defaults with OptionalAsNullable

    [Fact]
    public void NullableOptional_DefaultProperty_MissingReturnsDefaultNotNull()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test"}""");

        ObjectWithDefaultProperties.StatusEntity? status = doc.RootElement.Status;
        Assert.NotNull(status);
        Assert.Equal("active", (string)status.Value);
    }

    [Fact]
    public void NullableOptional_PropertyWithoutDefault_MissingReturnsNull()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithDefaultProperties>.Parse("""{"name":"test"}""");

        JsonString? label = doc.RootElement.Label;
        Assert.Null(label);
    }

    #endregion

    #region Nullable optional — nested objects

    [Fact]
    public void NullableOptional_NestedObject_AbsentNotes_ReturnsNull()
    {
        using var doc =
            ParsedJsonDocument<NestedObject>.Parse("""{"address":{"street":"123 Main","city":"X","zip":"00000"}}""");

        Assert.Null(doc.RootElement.Notes);
    }

    [Fact]
    public void NullableOptional_NestedObject_PresentNotes_ReturnsValue()
    {
        using var doc =
            ParsedJsonDocument<NestedObject>.Parse("""{"address":{"street":"123 Main","city":"X","zip":"00000"},"notes":"hello"}""");

        Assert.NotNull(doc.RootElement.Notes);
        Assert.Equal("hello", doc.RootElement.Notes?.ToString());
    }

    #endregion

    #region Nullable optional — composition

    [Fact]
    public void NullableOptional_CompositionAllOf_OptionalProperties_ReturnNullWhenAbsent()
    {
        using var doc =
            ParsedJsonDocument<CompositionAllOf>.Parse("{}");

        Assert.Null(doc.RootElement.FirstName);
        Assert.Null(doc.RootElement.LastName);
    }

    [Fact]
    public void NullableOptional_CompositionAllOf_OptionalProperties_ReturnValueWhenPresent()
    {
        using var doc =
            ParsedJsonDocument<CompositionAllOf>.Parse("""{"firstName":"Alice","lastName":"Smith"}""");

        Assert.Equal("Alice", doc.RootElement.FirstName?.ToString());
        Assert.Equal("Smith", doc.RootElement.LastName?.ToString());
    }

    #endregion

    #region Nullable optional — array property

    [Fact]
    public void NullableOptional_AbsentArrayProperty_ReturnsNull()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithArrayProperty>.Parse("""{"tags":["a"]}""");

        Assert.Null(doc.RootElement.Scores);
    }

    [Fact]
    public void NullableOptional_PresentArrayProperty_ReturnsValue()
    {
        using var doc =
            ParsedJsonDocument<ObjectWithArrayProperty>.Parse("""{"tags":["a"],"scores":[1.0,2.0]}""");

        Assert.NotNull(doc.RootElement.Scores);
        Assert.Equal(2, doc.RootElement.Scores!.Value.GetArrayLength());
    }

    #endregion

    #region Nullable optional — indexer with optional array item property

    [Fact]
    public void NullableOptional_Indexer_OptionalLabel_ReturnsNullWhenAbsent()
    {
        using var doc =
            ParsedJsonDocument<ArrayOfItems>.Parse("""[{"id":1}]""");

        Assert.Null(doc.RootElement[0].Label);
    }

    [Fact]
    public void NullableOptional_Indexer_OptionalLabel_ReturnsValueWhenPresent()
    {
        using var doc =
            ParsedJsonDocument<ArrayOfItems>.Parse("""[{"id":1,"label":"hello"}]""");

        Assert.Equal("hello", (string)doc.RootElement[0].Label!);
    }

    #endregion

    #region Round-trip with nullable optional

    [Fact]
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
        Assert.Equal("test@test.com", roundTrip.RootElement.Email?.ToString());
        Assert.True((bool)roundTrip.RootElement.IsActive!.Value);
    }

    #endregion
}