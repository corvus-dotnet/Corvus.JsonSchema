// <copyright file="PatchBuilderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Patch;
using Xunit;

namespace Corvus.Text.Json.Patch.Tests;

public class PatchBuilderTests
{
    [Fact]
    public void BuildAndApplyAddOperation()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder patch = root.BeginPatch()
            .Add("/baz"u8, JsonElement.ParseValue("\"qux\""));

        Assert.True(patch.TryApply(ref root));
        Assert.Equal("qux", root.GetProperty("baz").GetString());
        Assert.Equal("bar", root.GetProperty("foo").GetString());
    }

    [Fact]
    public void BuildAndApplyRemoveOperation()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar", "baz": "qux"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder patch = root.BeginPatch()
            .Remove("/baz"u8);

        Assert.True(patch.TryApply(ref root));
        Assert.False(root.TryGetProperty("baz"u8, out _));
        Assert.Equal("bar", root.GetProperty("foo").GetString());
    }

    [Fact]
    public void BuildAndApplyReplaceOperation()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder patch = root.BeginPatch()
            .Replace("/foo"u8, JsonElement.ParseValue("""42"""));

        Assert.True(patch.TryApply(ref root));
        Assert.Equal(42, root.GetProperty("foo").GetInt32());
    }

    [Fact]
    public void BuildAndApplyMoveOperation()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": {"bar": "baz"}, "qux": {"corge": "grault"}}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder patch = root.BeginPatch()
            .Move("/foo/bar"u8, "/qux/thud"u8);

        Assert.True(patch.TryApply(ref root));
        Assert.Equal("baz", root.GetProperty("qux").GetProperty("thud").GetString());
        Assert.False(root.GetProperty("foo").TryGetProperty("bar"u8, out _));
    }

    [Fact]
    public void BuildAndApplyCopyOperation()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder patch = root.BeginPatch()
            .Copy("/foo"u8, "/baz"u8);

        Assert.True(patch.TryApply(ref root));
        Assert.Equal("bar", root.GetProperty("foo").GetString());
        Assert.Equal("bar", root.GetProperty("baz").GetString());
    }

    [Fact]
    public void BuildAndApplyTestOperation()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder patch = root.BeginPatch()
            .Test("/foo"u8, JsonElement.ParseValue("\"bar\""));

        Assert.True(patch.TryApply(ref root));
    }

    [Fact]
    public void BuildAndApplyTestOperationFailsOnMismatch()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder patch = root.BeginPatch()
            .Test("/foo"u8, JsonElement.ParseValue("\"baz\""));

        Assert.False(patch.TryApply(ref root));
    }

    [Fact]
    public void BuildAndApplyMultipleOperations()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar", "baz": "qux"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder patch = root.BeginPatch()
            .Remove("/baz"u8)
            .Add("/hello"u8, JsonElement.ParseValue("\"world\""))
            .Replace("/foo"u8, JsonElement.ParseValue("42"));

        Assert.True(patch.TryApply(ref root));
        Assert.False(root.TryGetProperty("baz"u8, out _));
        Assert.Equal("world", root.GetProperty("hello").GetString());
        Assert.Equal(42, root.GetProperty("foo").GetInt32());
    }
}

public class IndividualOperationTests
{
    [Fact]
    public void TryAddToObject()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = "qux";

        Assert.True(root.TryAdd("/baz"u8, in value));
        Assert.Equal("qux", root.GetProperty("baz").GetString());
    }

    [Fact]
    public void TryAddToArray()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 4;

        Assert.True(root.TryAdd("/-"u8, in value));
        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(4, root[3].GetInt32());
    }

    [Fact]
    public void TryRemoveProperty()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar", "baz": "qux"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        Assert.True(root.TryRemove("/baz"u8));
        Assert.False(root.TryGetProperty("baz"u8, out _));
    }

    [Fact]
    public void TryRemoveArrayElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        Assert.True(root.TryRemove("/1"u8));
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(3, root[1].GetInt32());
    }

    [Fact]
    public void TryReplaceProperty()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 42;

        Assert.True(root.TryReplace("/foo"u8, in value));
        Assert.Equal(42, root.GetProperty("foo").GetInt32());
    }

    [Fact]
    public void TryReplaceNonExistentFails()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 42;

        Assert.False(root.TryReplace("/baz"u8, in value));
    }

    [Fact]
    public void TryMoveProperty()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar", "baz": {}}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        Assert.True(root.TryMove("/foo"u8, "/baz/qux"u8));
        Assert.False(root.TryGetProperty("foo"u8, out _));
        Assert.Equal("bar", root.GetProperty("baz").GetProperty("qux").GetString());
    }

    [Fact]
    public void TryCopyProperty()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        Assert.True(root.TryCopy("/foo"u8, "/baz"u8));
        Assert.Equal("bar", root.GetProperty("foo").GetString());
        Assert.Equal("bar", root.GetProperty("baz").GetString());
    }

    [Fact]
    public void TryTestMatching()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement expected = JsonElement.ParseValue("\"bar\"");

        Assert.True(root.TryTest("/foo"u8, in expected));
    }

    [Fact]
    public void TryTestNonMatching()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement expected = JsonElement.ParseValue("\"baz\"");

        Assert.False(root.TryTest("/foo"u8, in expected));
    }

    [Fact]
    public void TryRemoveRootFails()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        Assert.False(root.TryRemove(""u8));
    }

    [Fact]
    public void TryAddReplaceRoot()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse("""{"foo": "bar"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 42;

        Assert.True(root.TryAdd(""u8, in value));
        Assert.Equal(JsonValueKind.Number, root.ValueKind);
        Assert.Equal(42, root.GetInt32());
    }
}