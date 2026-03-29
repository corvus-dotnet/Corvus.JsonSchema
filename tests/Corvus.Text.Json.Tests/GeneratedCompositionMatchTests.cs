// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for generated Match() and Apply() patterns on composition types.
/// Exercises: anyOf Match, oneOf Match, allOf Match + Apply,
/// both with and without context parameters.
/// </summary>
public class GeneratedCompositionMatchTests
{
    #region anyOf Match

    [Fact]
    public void AnyOf_Match_WhenTextObject_CallsTextMatcher()
    {
        using var doc =
            ParsedJsonDocument<CompositionAnyOf>.Parse("""{"kind":"text","message":"hello"}""");

        string result = doc.RootElement.Match(
            matchRequiredKindAndMessage: static (in v) => "text:" + v.Message.ToString(),
            matchRequiredCodeAndKind: static (in _) => "numeric",
            defaultMatch: static (in _) => "default");

        Assert.Equal("text:hello", result);
    }

    [Fact]
    public void AnyOf_Match_WhenNumericObject_CallsNumericMatcher()
    {
        using var doc =
            ParsedJsonDocument<CompositionAnyOf>.Parse("""{"kind":"numeric","code":42}""");

        string result = doc.RootElement.Match(
            matchRequiredKindAndMessage: static (in _) => "text",
            matchRequiredCodeAndKind: static (in v) => "numeric:" + ((int)v.Code).ToString(),
            defaultMatch: static (in _) => "default");

        Assert.Equal("numeric:42", result);
    }

    [Fact]
    public void AnyOf_Match_WhenNeither_CallsDefaultMatcher()
    {
        using var doc =
            ParsedJsonDocument<CompositionAnyOf>.Parse("""{"other":"value"}""");

        string result = doc.RootElement.Match(
            matchRequiredKindAndMessage: static (in _) => "text",
            matchRequiredCodeAndKind: static (in _) => "numeric",
            defaultMatch: static (in _) => "default");

        Assert.Equal("default", result);
    }

    [Fact]
    public void AnyOf_MatchWithContext_PassesContext()
    {
        using var doc =
            ParsedJsonDocument<CompositionAnyOf>.Parse("""{"kind":"text","message":"hello"}""");

        string result = doc.RootElement.Match(
            "prefix",
            matchRequiredKindAndMessage: static (in v, in ctx) => ctx + ":" + v.Message.ToString(),
            matchRequiredCodeAndKind: static (in _, in _2) => "numeric",
            defaultMatch: static (in _, in _2) => "default");

        Assert.Equal("prefix:hello", result);
    }

    #endregion

    #region oneOf Match

    [Fact]
    public void OneOf_Match_WhenString_CallsStringMatcher()
    {
        using var doc =
            ParsedJsonDocument<CompositionOneOf>.Parse("\"hello\"");

        string result = doc.RootElement.Match(
            matchJsonString: static (in v) => "string:" + v.ToString(),
            matchJsonInt32: static (in _) => "number",
            matchJsonBoolean: static (in _) => "boolean",
            defaultMatch: static (in _) => "default");

        Assert.Equal("string:hello", result);
    }

    [Fact]
    public void OneOf_Match_WhenNumber_CallsNumberMatcher()
    {
        using var doc =
            ParsedJsonDocument<CompositionOneOf>.Parse("42");

        string result = doc.RootElement.Match(
            matchJsonString: static (in _) => "string",
            matchJsonInt32: static (in v) => "number:" + ((int)v).ToString(),
            matchJsonBoolean: static (in _) => "boolean",
            defaultMatch: static (in _) => "default");

        Assert.Equal("number:42", result);
    }

    [Fact]
    public void OneOf_Match_WhenBoolean_CallsBooleanMatcher()
    {
        using var doc =
            ParsedJsonDocument<CompositionOneOf>.Parse("true");

        string result = doc.RootElement.Match(
            matchJsonString: static (in _) => "string",
            matchJsonInt32: static (in _) => "number",
            matchJsonBoolean: static (in v) => "boolean:" + ((bool)v).ToString(),
            defaultMatch: static (in _) => "default");

        Assert.StartsWith("boolean:", result);
    }

    #endregion

    #region allOf Match + Apply

    [Fact]
    public void AllOf_Apply_MergesProperties()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<CompositionAllOf>.Parse("""{"firstName":"Alice"}""");
        using JsonDocumentBuilder<CompositionAllOf.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        using var allOf1Doc =
            ParsedJsonDocument<CompositionAllOf.AllOf1Entity>.Parse("""{"lastName":"Smith"}""");

        CompositionAllOf.Mutable root = builder.RootElement;
        root.Apply(allOf1Doc.RootElement);
        string json = root.ToString();

        using var roundTrip = ParsedJsonDocument<CompositionAllOf>.Parse(json);
        Assert.Equal("Alice", roundTrip.RootElement.FirstName.ToString());
        Assert.Equal("Smith", roundTrip.RootElement.LastName.ToString());
    }

    #endregion
}