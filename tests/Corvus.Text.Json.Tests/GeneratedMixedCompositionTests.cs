// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for generated types that use mixtures of composition keywords
/// (allOf+anyOf, properties+oneOf, allOf+if/then/else).
/// Exercises: Match on combined composition, Apply on allOf with anyOf present,
/// property access through composed types, and mutable operations on mixed schemas.
/// </summary>
public class GeneratedMixedCompositionTests
{
    #region AllOf + AnyOf — Match dispatches to anyOf variants

    [Fact]
    public void AllOfWithAnyOf_Match_WhenAdminRole_MatchesRequiredRole()
    {
        using var doc =
            ParsedJsonDocument<AllOfWithAnyOf>.Parse("""{"id":"a1","role":"admin","level":5}""");

        string result = doc.RootElement.Match(
            matchRequiredRole: static (in v) => "admin:level=" + v.Level.ToString(),
            matchAllOfWithAnyOfRequiredRole: static (in _) => "user",
            defaultMatch: static (in _) => "default");

        Assert.Equal("admin:level=5", result);
    }

    [Fact]
    public void AllOfWithAnyOf_Match_WhenUserRole_MatchesAllOfWithAnyOfRequiredRole()
    {
        using var doc =
            ParsedJsonDocument<AllOfWithAnyOf>.Parse("""{"id":"u1","role":"user","email":"u@test.com"}""");

        string result = doc.RootElement.Match(
            matchRequiredRole: static (in _) => "admin",
            matchAllOfWithAnyOfRequiredRole: static (in v) => "user:email=" + v.Email.ToString(),
            defaultMatch: static (in _) => "default");

        Assert.Equal("user:email=u@test.com", result);
    }

    [Fact]
    public void AllOfWithAnyOf_Match_WhenNeither_CallsDefault()
    {
        using var doc =
            ParsedJsonDocument<AllOfWithAnyOf>.Parse("""{"id":"x1","other":"value"}""");

        string result = doc.RootElement.Match(
            matchRequiredRole: static (in _) => "admin",
            matchAllOfWithAnyOfRequiredRole: static (in _) => "user",
            defaultMatch: static (in _) => "default");

        Assert.Equal("default", result);
    }

    [Fact]
    public void AllOfWithAnyOf_Apply_MergesAllOfProperties()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<AllOfWithAnyOf>.Parse("""{"role":"admin","level":3}""");
        using JsonDocumentBuilder<AllOfWithAnyOf.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        using var idDoc =
            ParsedJsonDocument<AllOfWithAnyOf.RequiredId>.Parse("""{"id":"merged-1"}""");

        AllOfWithAnyOf.Mutable root = builder.RootElement;
        root.Apply(idDoc.RootElement);
        string json = root.ToString();

        using var roundTrip = ParsedJsonDocument<AllOfWithAnyOf>.Parse(json);
        Assert.Equal("merged-1", roundTrip.RootElement.Id.ToString());
    }

    [Fact]
    public void AllOfWithAnyOf_MutableMatch_DispatchesToAnyOfVariant()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<AllOfWithAnyOf>.Parse("""{"id":"a1","role":"admin","level":5}""");
        using JsonDocumentBuilder<AllOfWithAnyOf.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        AllOfWithAnyOf.Mutable root = builder.RootElement;
        string result = root.Match(
            matchRequiredRole: static (in v) => "admin:" + v.Level.ToString(),
            matchAllOfWithAnyOfRequiredRole: static (in _) => "user",
            defaultMatch: static (in _) => "default");

        Assert.Equal("admin:5", result);
    }

    #endregion

    #region Properties + OneOf — object with properties AND oneOf discrimination

    [Fact]
    public void PropertiesWithOneOf_Kind_AccessibleDirectly()
    {
        using var doc =
            ParsedJsonDocument<PropertiesWithOneOf>.Parse("""{"kind":"text","content":"hello"}""");

        Assert.Equal("text", doc.RootElement.Kind.ToString());
    }

    [Fact]
    public void PropertiesWithOneOf_Match_TextVariant_MatchesRequiredContent()
    {
        using var doc =
            ParsedJsonDocument<PropertiesWithOneOf>.Parse("""{"kind":"text","content":"hello"}""");

        string result = doc.RootElement.Match(
            matchRequiredContent: static (in v) => "text:" + v.Content.ToString(),
            matchRequiredValue: static (in _) => "number",
            defaultMatch: static (in _) => "default");

        Assert.Equal("text:hello", result);
    }

    [Fact]
    public void PropertiesWithOneOf_Match_NumberVariant_MatchesRequiredValue()
    {
        using var doc =
            ParsedJsonDocument<PropertiesWithOneOf>.Parse("""{"kind":"number","value":99}""");

        string result = doc.RootElement.Match(
            matchRequiredContent: static (in _) => "text",
            matchRequiredValue: static (in v) => "number:" + v.Value.ToString(),
            defaultMatch: static (in _) => "default");

        Assert.Equal("number:99", result);
    }

    [Fact]
    public void PropertiesWithOneOf_MutableSetKind_UpdatesSharedProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<PropertiesWithOneOf>.Parse("""{"kind":"text","content":"hello"}""");
        using JsonDocumentBuilder<PropertiesWithOneOf.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        PropertiesWithOneOf.Mutable root = builder.RootElement;
        root.SetKind("updated");
        Assert.Equal("updated", root.Kind.ToString());
    }

    #endregion

    #region AllOf + If/Then/Else — merged base + conditional narrowing

    [Fact]
    public void AllOfWithIfThenElse_Match_WhenPremium_CallsThenMatcher()
    {
        using var doc =
            ParsedJsonDocument<AllOfWithIfThenElse>.Parse("""{"type":"premium","discount":0.15}""");

        string result = doc.RootElement.Match(
            matchCorvusTextJsonTestsGeneratedModelsDraft202012AllOfWithIfThenElseRequiredDiscount:
                static (in v) => "then:discount=" + v.Discount.ToString(),
            matchCorvusTextJsonTestsGeneratedModelsDraft202012AllOfWithIfThenElseElseEntity:
                static (in _) => "else");

        Assert.StartsWith("then:discount=", result);
    }

    [Fact]
    public void AllOfWithIfThenElse_Match_WhenNotPremium_CallsElseMatcher()
    {
        using var doc =
            ParsedJsonDocument<AllOfWithIfThenElse>.Parse("""{"type":"standard","message":"no discount"}""");

        string result = doc.RootElement.Match(
            matchCorvusTextJsonTestsGeneratedModelsDraft202012AllOfWithIfThenElseRequiredDiscount:
                static (in _) => "then",
            matchCorvusTextJsonTestsGeneratedModelsDraft202012AllOfWithIfThenElseElseEntity:
                static (in v) => "else:msg=" + v.Message.ToString());

        Assert.Equal("else:msg=no discount", result);
    }

    [Fact]
    public void AllOfWithIfThenElse_Apply_MergesAllOfBase()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<AllOfWithIfThenElse>.Parse("""{"discount":0.1}""");
        using JsonDocumentBuilder<AllOfWithIfThenElse.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        using var typeDoc =
            ParsedJsonDocument<AllOfWithIfThenElse.RequiredType>.Parse("""{"type":"premium"}""");

        AllOfWithIfThenElse.Mutable root = builder.RootElement;
        root.Apply(typeDoc.RootElement);
        string json = root.ToString();

        using var roundTrip = ParsedJsonDocument<AllOfWithIfThenElse>.Parse(json);
        Assert.Equal("premium", roundTrip.RootElement.Type.ToString());
    }

    [Fact]
    public void AllOfWithIfThenElse_MutableSetType_ChangesType()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<AllOfWithIfThenElse>.Parse("""{"type":"standard","message":"hello"}""");
        using JsonDocumentBuilder<AllOfWithIfThenElse.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        AllOfWithIfThenElse.Mutable root = builder.RootElement;
        root.SetType("premium");
        Assert.Equal("premium", root.Type.ToString());
    }

    [Fact]
    public void AllOfWithIfThenElse_MutableSetDiscount_SetsOptionalProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<AllOfWithIfThenElse>.Parse("""{"type":"premium"}""");
        using JsonDocumentBuilder<AllOfWithIfThenElse.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        AllOfWithIfThenElse.Mutable root = builder.RootElement;
        root.SetDiscount(0.25);
        Assert.True(root.Discount.IsNotUndefined());
    }

    [Fact]
    public void AllOfWithIfThenElse_RemoveMessage_RemovesOptionalElseProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc =
            ParsedJsonDocument<AllOfWithIfThenElse>.Parse("""{"type":"standard","message":"hello"}""");
        using JsonDocumentBuilder<AllOfWithIfThenElse.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        AllOfWithIfThenElse.Mutable root = builder.RootElement;
        bool removed = root.RemoveMessage();

        Assert.True(removed);
        Assert.True(root.Message.IsUndefined());
    }

    #endregion
}