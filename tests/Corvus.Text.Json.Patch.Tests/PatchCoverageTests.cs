// <copyright file="PatchCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Patch;
using Xunit;

namespace Corvus.Text.Json.Patch.Tests;

/// <summary>
/// Coverage tests targeting char/string overloads, move dispatch paths,
/// copy dispatch paths, and error branches in JsonPatchExtensions and PatchBuilder.
/// </summary>
public class PatchCoverageTests
{
    private static readonly string LongKey1 = new('x', 140);
    private static readonly string LongKey2 = new('y', 140);

    #region PatchBuilder char overloads

    [Fact]
    public void PatchBuilder_Add_CharOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder pb = root.BeginPatch(workspace)
            .Add("/b".AsSpan(), "v"u8);
        JsonPatchDocument patch = pb.GetPatchAndDispose();
        Assert.True(root.TryApplyPatch(patch));
        Assert.Equal("v", root.GetProperty("b").GetString());
    }

    [Fact]
    public void PatchBuilder_Add_StringOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder pb = root.BeginPatch(workspace)
            .Add("/b", "v"u8);
        JsonPatchDocument patch = pb.GetPatchAndDispose();
        Assert.True(root.TryApplyPatch(patch));
        Assert.Equal("v", root.GetProperty("b").GetString());
    }

    [Fact]
    public void PatchBuilder_Remove_CharOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1,"b":2}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder pb = root.BeginPatch(workspace)
            .Remove("/b".AsSpan());
        JsonPatchDocument patch = pb.GetPatchAndDispose();
        Assert.True(root.TryApplyPatch(patch));
        Assert.False(root.TryGetProperty("b"u8, out _));
    }

    [Fact]
    public void PatchBuilder_Remove_StringOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1,"b":2}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder pb = root.BeginPatch(workspace)
            .Remove("/b");
        JsonPatchDocument patch = pb.GetPatchAndDispose();
        Assert.True(root.TryApplyPatch(patch));
        Assert.False(root.TryGetProperty("b"u8, out _));
    }

    [Fact]
    public void PatchBuilder_Replace_CharOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder pb = root.BeginPatch(workspace)
            .Replace("/a".AsSpan(), 99);
        JsonPatchDocument patch = pb.GetPatchAndDispose();
        Assert.True(root.TryApplyPatch(patch));
        Assert.Equal(99, root.GetProperty("a").GetInt32());
    }

    [Fact]
    public void PatchBuilder_Replace_StringOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder pb = root.BeginPatch(workspace)
            .Replace("/a", 99);
        JsonPatchDocument patch = pb.GetPatchAndDispose();
        Assert.True(root.TryApplyPatch(patch));
        Assert.Equal(99, root.GetProperty("a").GetInt32());
    }

    [Fact]
    public void PatchBuilder_Move_CharOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":"v","b":{}}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder pb = root.BeginPatch(workspace)
            .Move("/a".AsSpan(), "/b/c".AsSpan());
        JsonPatchDocument patch = pb.GetPatchAndDispose();
        Assert.True(root.TryApplyPatch(patch));
        Assert.False(root.TryGetProperty("a"u8, out _));
        Assert.Equal("v", root.GetProperty("b").GetProperty("c").GetString());
    }

    [Fact]
    public void PatchBuilder_Move_StringOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":"v","b":{}}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder pb = root.BeginPatch(workspace)
            .Move("/a", "/b/c");
        JsonPatchDocument patch = pb.GetPatchAndDispose();
        Assert.True(root.TryApplyPatch(patch));
        Assert.False(root.TryGetProperty("a"u8, out _));
        Assert.Equal("v", root.GetProperty("b").GetProperty("c").GetString());
    }

    [Fact]
    public void PatchBuilder_Copy_CharOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":"v"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder pb = root.BeginPatch(workspace)
            .Copy("/a".AsSpan(), "/b".AsSpan());
        JsonPatchDocument patch = pb.GetPatchAndDispose();
        Assert.True(root.TryApplyPatch(patch));
        Assert.Equal("v", root.GetProperty("a").GetString());
        Assert.Equal("v", root.GetProperty("b").GetString());
    }

    [Fact]
    public void PatchBuilder_Copy_StringOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":"v"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder pb = root.BeginPatch(workspace)
            .Copy("/a", "/b");
        JsonPatchDocument patch = pb.GetPatchAndDispose();
        Assert.True(root.TryApplyPatch(patch));
        Assert.Equal("v", root.GetProperty("a").GetString());
        Assert.Equal("v", root.GetProperty("b").GetString());
    }

    [Fact]
    public void PatchBuilder_Test_CharOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":"v"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder pb = root.BeginPatch(workspace)
            .Test("/a".AsSpan(), "v"u8);
        JsonPatchDocument patch = pb.GetPatchAndDispose();
        Assert.True(root.TryApplyPatch(patch));
    }

    [Fact]
    public void PatchBuilder_Test_StringOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":"v"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder pb = root.BeginPatch(workspace)
            .Test("/a", "v"u8);
        JsonPatchDocument patch = pb.GetPatchAndDispose();
        Assert.True(root.TryApplyPatch(patch));
    }

    [Fact]
    public void PatchBuilder_Dispose_WithoutGetPatch()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        // Dispose without calling GetPatchAndDispose — covers the safety Dispose() path
        PatchBuilder pb = root.BeginPatch(workspace)
            .Add("/b"u8, "v"u8);
        pb.Dispose();

        // Double-dispose should be safe
        pb.Dispose();
    }

    #endregion

    #region Standalone char/string API overloads

    [Fact]
    public void TryAdd_CharOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = "hello";

        Assert.True(root.TryAdd("/b".AsSpan(), in value));
        Assert.Equal("hello", root.GetProperty("b").GetString());
    }

    [Fact]
    public void TryAdd_StringOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = "hello";

        Assert.True(root.TryAdd("/b", in value));
        Assert.Equal("hello", root.GetProperty("b").GetString());
    }

    [Fact]
    public void TryRemove_CharOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1,"b":2}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.True(root.TryRemove("/b".AsSpan()));
        Assert.False(root.TryGetProperty("b"u8, out _));
    }

    [Fact]
    public void TryRemove_StringOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1,"b":2}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.True(root.TryRemove("/b"));
        Assert.False(root.TryGetProperty("b"u8, out _));
    }

    [Fact]
    public void TryReplace_CharOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 99;

        Assert.True(root.TryReplace("/a".AsSpan(), in value));
        Assert.Equal(99, root.GetProperty("a").GetInt32());
    }

    [Fact]
    public void TryReplace_StringOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 99;

        Assert.True(root.TryReplace("/a", in value));
        Assert.Equal(99, root.GetProperty("a").GetInt32());
    }

    [Fact]
    public void TryMove_CharOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":"v","b":{}}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.True(root.TryMove("/a".AsSpan(), "/b/c".AsSpan()));
        Assert.False(root.TryGetProperty("a"u8, out _));
        Assert.Equal("v", root.GetProperty("b").GetProperty("c").GetString());
    }

    [Fact]
    public void TryMove_StringOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":"v","b":{}}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.True(root.TryMove("/a", "/b/c"));
        Assert.False(root.TryGetProperty("a"u8, out _));
        Assert.Equal("v", root.GetProperty("b").GetProperty("c").GetString());
    }

    [Fact]
    public void TryCopy_CharOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":"v"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.True(root.TryCopy("/a".AsSpan(), "/b".AsSpan()));
        Assert.Equal("v", root.GetProperty("a").GetString());
        Assert.Equal("v", root.GetProperty("b").GetString());
    }

    [Fact]
    public void TryCopy_StringOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":"v"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.True(root.TryCopy("/a", "/b"));
        Assert.Equal("v", root.GetProperty("a").GetString());
        Assert.Equal("v", root.GetProperty("b").GetString());
    }

    [Fact]
    public void TryTest_CharOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":"v"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement expected = JsonElement.ParseValue("\"v\"");

        Assert.True(root.TryTest("/a".AsSpan(), in expected));
    }

    [Fact]
    public void TryTest_StringOverload()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":"v"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement expected = JsonElement.ParseValue("\"v\"");

        Assert.True(root.TryTest("/a", in expected));
    }

    #endregion

    #region Long-path tests (>256 UTF-8 bytes, triggers ArrayPool rent/return)

    // Total path = "/" + LongKey1 + "/" + LongKey2 = 1 + 140 + 1 + 140 = 282 bytes > 256
    // Each segment is 140 bytes, well under the 256-byte segment buffer.

    private static string LongPathNested => "/" + LongKey1 + "/" + LongKey2;

    private static string BuildNestedJson(string innerValue = "1")
        => "{\"" + LongKey1 + "\":{\"" + LongKey2 + "\":" + innerValue + "}}";

    [Fact]
    public void TryAdd_CharOverload_LongPath()
    {
        // Add a sibling next to LongKey2 inside the nested object
        string json = BuildNestedJson();
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 42;

        Assert.True(root.TryAdd(("/" + LongKey1 + "/newProp").AsSpan(), in value));
    }

    [Fact]
    public void TryRemove_CharOverload_LongPath()
    {
        string json = BuildNestedJson();
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.True(root.TryRemove(LongPathNested.AsSpan()));
    }

    [Fact]
    public void TryReplace_CharOverload_LongPath()
    {
        string json = BuildNestedJson();
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 99;

        Assert.True(root.TryReplace(LongPathNested.AsSpan(), in value));
    }

    [Fact]
    public void TryMove_CharOverload_LongPath()
    {
        string json = "{\"" + LongKey1 + "\":{\"" + LongKey2 + "\":1},\"dest\":{}}";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.True(root.TryMove(LongPathNested.AsSpan(), "/dest/v".AsSpan()));
    }

    [Fact]
    public void TryCopy_CharOverload_LongPath()
    {
        string json = BuildNestedJson();
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.True(root.TryCopy(LongPathNested.AsSpan(), "/copy".AsSpan()));
    }

    [Fact]
    public void TryTest_CharOverload_LongPath()
    {
        string json = BuildNestedJson();
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement expected = JsonElement.ParseValue("1");

        Assert.True(root.TryTest(LongPathNested.AsSpan(), in expected));
    }

    #endregion

    #region Move dispatch paths (TryApplyMove via TryApplyPatch)

    [Fact]
    public void Move_ArrayToArray_Append()
    {
        // Move array[0] to end of another array
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"src":[10,20],"dst":[1,2]}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder pb = root.BeginPatch(workspace)
            .Move("/src/0"u8, "/dst/-"u8);
        JsonPatchDocument patch = pb.GetPatchAndDispose();
        Assert.True(root.TryApplyPatch(patch));
        Assert.Equal(1, root.GetProperty("src").GetArrayLength());
        Assert.Equal(3, root.GetProperty("dst").GetArrayLength());
        Assert.Equal(10, root.GetProperty("dst")[2].GetInt32());
    }

    [Fact]
    public void Move_ArrayToArray_Index()
    {
        // Move array[1] to index 0 of another array
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"src":[10,20],"dst":[1,2]}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder pb = root.BeginPatch(workspace)
            .Move("/src/1"u8, "/dst/0"u8);
        JsonPatchDocument patch = pb.GetPatchAndDispose();
        Assert.True(root.TryApplyPatch(patch));
        Assert.Equal(1, root.GetProperty("src").GetArrayLength());
        Assert.Equal(3, root.GetProperty("dst").GetArrayLength());
        Assert.Equal(20, root.GetProperty("dst")[0].GetInt32());
    }

    [Fact]
    public void Move_ArrayToObject()
    {
        // Move array item to object property
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"src":[10,20],"dst":{}}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder pb = root.BeginPatch(workspace)
            .Move("/src/0"u8, "/dst/val"u8);
        JsonPatchDocument patch = pb.GetPatchAndDispose();
        Assert.True(root.TryApplyPatch(patch));
        Assert.Equal(1, root.GetProperty("src").GetArrayLength());
        Assert.Equal(10, root.GetProperty("dst").GetProperty("val").GetInt32());
    }

    [Fact]
    public void Move_ObjectToArray_Append()
    {
        // Move object property to end of array
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"src":{"val":10},"dst":[1,2]}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder pb = root.BeginPatch(workspace)
            .Move("/src/val"u8, "/dst/-"u8);
        JsonPatchDocument patch = pb.GetPatchAndDispose();
        Assert.True(root.TryApplyPatch(patch));
        Assert.False(root.GetProperty("src").TryGetProperty("val"u8, out _));
        Assert.Equal(3, root.GetProperty("dst").GetArrayLength());
        Assert.Equal(10, root.GetProperty("dst")[2].GetInt32());
    }

    [Fact]
    public void Move_ObjectToArray_Index()
    {
        // Move object property to specific index in array
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"src":{"val":10},"dst":[1,2]}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder pb = root.BeginPatch(workspace)
            .Move("/src/val"u8, "/dst/1"u8);
        JsonPatchDocument patch = pb.GetPatchAndDispose();
        Assert.True(root.TryApplyPatch(patch));
        Assert.False(root.GetProperty("src").TryGetProperty("val"u8, out _));
        Assert.Equal(3, root.GetProperty("dst").GetArrayLength());
        Assert.Equal(10, root.GetProperty("dst")[1].GetInt32());
    }

    [Fact]
    public void Move_ObjectToObject()
    {
        // Move property between objects
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"src":{"val":10},"dst":{}}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using PatchBuilder pb = root.BeginPatch(workspace)
            .Move("/src/val"u8, "/dst/val"u8);
        JsonPatchDocument patch = pb.GetPatchAndDispose();
        Assert.True(root.TryApplyPatch(patch));
        Assert.False(root.GetProperty("src").TryGetProperty("val"u8, out _));
        Assert.Equal(10, root.GetProperty("dst").GetProperty("val").GetInt32());
    }

    #endregion

    #region Move error paths

    [Fact]
    public void Move_FromRoot_Fails()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.False(root.TryMove(""u8, "/b"u8));
    }

    [Fact]
    public void Move_InvalidSource_Fails()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.False(root.TryMove("/nonexistent"u8, "/b"u8));
    }

    [Fact]
    public void Move_ArrayIndex_OutOfBounds_Fails()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"src":[1],"dst":{}}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.False(root.TryMove("/src/5"u8, "/dst/v"u8));
    }

    #endregion

    #region Copy dispatch paths

    [Fact]
    public void TryCopy_ToArrayAppend()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"val":10,"arr":[1]}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.True(root.TryCopy("/val"u8, "/arr/-"u8));
        Assert.Equal(2, root.GetProperty("arr").GetArrayLength());
        Assert.Equal(10, root.GetProperty("arr")[1].GetInt32());
    }

    [Fact]
    public void TryCopy_ToArrayIndex()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"val":10,"arr":[1,2]}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.True(root.TryCopy("/val"u8, "/arr/0"u8));
        Assert.Equal(3, root.GetProperty("arr").GetArrayLength());
        Assert.Equal(10, root.GetProperty("arr")[0].GetInt32());
    }

    [Fact]
    public void TryCopy_InvalidSource_Fails()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.False(root.TryCopy("/nonexistent"u8, "/b"u8));
    }

    #endregion

    #region Replace dispatch paths

    [Fact]
    public void TryReplace_RootElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 42;

        Assert.True(root.TryReplace(""u8, in value));
        Assert.Equal(JsonValueKind.Number, root.ValueKind);
    }

    [Fact]
    public void TryReplace_ArrayElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 99;

        Assert.True(root.TryReplace("/1"u8, in value));
        Assert.Equal(99, root[1].GetInt32());
    }

    [Fact]
    public void TryReplace_ArrayIndex_OutOfBounds_Fails()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("[1,2]");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 99;

        Assert.False(root.TryReplace("/5"u8, in value));
    }

    #endregion

    #region Remove edge paths

    [Fact]
    public void TryRemove_ArrayElement()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("[10,20,30]");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.True(root.TryRemove("/1"u8));
        Assert.Equal(2, root.GetArrayLength());
    }

    [Fact]
    public void TryRemove_ArrayIndex_OutOfBounds_Fails()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.False(root.TryRemove("/5"u8));
    }

    [Fact]
    public void TryRemove_NonExistentProperty_Fails()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.False(root.TryRemove("/nonexistent"u8));
    }

    #endregion

    #region TryApplyPatch internal dispatch (Move/Copy/Test through patch document)

    [Fact]
    public void PatchApply_MoveFromRoot_Fails()
    {
        // Move with from="" should fail (cannot move root)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        // Build a patch document manually with from=""
        using ParsedJsonDocument<JsonPatchDocument> patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(
            """[{"op":"move","from":"","path":"/b"}]""");
        Assert.False(root.TryApplyPatch(patchDoc.RootElement));
    }

    [Fact]
    public void PatchApply_MoveToRoot_Fails()
    {
        // Move with path="" should fail (cannot move to root)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using ParsedJsonDocument<JsonPatchDocument> patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(
            """[{"op":"move","from":"/a","path":""}]""");
        Assert.False(root.TryApplyPatch(patchDoc.RootElement));
    }

    [Fact]
    public void PatchApply_Move_InvalidDestIndex_Fails()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"src":[10],"dst":[1]}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using ParsedJsonDocument<JsonPatchDocument> patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(
            """[{"op":"move","from":"/src/0","path":"/dst/99"}]""");
        Assert.False(root.TryApplyPatch(patchDoc.RootElement));
    }

    [Fact]
    public void PatchApply_Move_ObjectToArray_InvalidDestIndex_Fails()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"src":{"v":1},"dst":[1]}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using ParsedJsonDocument<JsonPatchDocument> patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(
            """[{"op":"move","from":"/src/v","path":"/dst/99"}]""");
        Assert.False(root.TryApplyPatch(patchDoc.RootElement));
    }

    [Fact]
    public void PatchApply_Copy_ToObject()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":10}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using ParsedJsonDocument<JsonPatchDocument> patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(
            """[{"op":"copy","from":"/a","path":"/b"}]""");
        Assert.True(root.TryApplyPatch(patchDoc.RootElement));
        Assert.Equal(10, root.GetProperty("b").GetInt32());
    }

    [Fact]
    public void PatchApply_Copy_ToArrayAppend()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"v":10,"arr":[1]}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using ParsedJsonDocument<JsonPatchDocument> patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(
            """[{"op":"copy","from":"/v","path":"/arr/-"}]""");
        Assert.True(root.TryApplyPatch(patchDoc.RootElement));
        Assert.Equal(2, root.GetProperty("arr").GetArrayLength());
        Assert.Equal(10, root.GetProperty("arr")[1].GetInt32());
    }

    [Fact]
    public void PatchApply_Copy_ToArrayIndex()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"v":10,"arr":[1,2]}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using ParsedJsonDocument<JsonPatchDocument> patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(
            """[{"op":"copy","from":"/v","path":"/arr/0"}]""");
        Assert.True(root.TryApplyPatch(patchDoc.RootElement));
        Assert.Equal(3, root.GetProperty("arr").GetArrayLength());
        Assert.Equal(10, root.GetProperty("arr")[0].GetInt32());
    }

    [Fact]
    public void PatchApply_Test_Passes()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":"hello"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using ParsedJsonDocument<JsonPatchDocument> patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(
            """[{"op":"test","path":"/a","value":"hello"}]""");
        Assert.True(root.TryApplyPatch(patchDoc.RootElement));
    }

    [Fact]
    public void PatchApply_Test_Fails()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":"hello"}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using ParsedJsonDocument<JsonPatchDocument> patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(
            """[{"op":"test","path":"/a","value":"world"}]""");
        Assert.False(root.TryApplyPatch(patchDoc.RootElement));
    }

    [Fact]
    public void PatchApply_Test_NonExistentPath_Fails()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using ParsedJsonDocument<JsonPatchDocument> patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(
            """[{"op":"test","path":"/nope","value":1}]""");
        Assert.False(root.TryApplyPatch(patchDoc.RootElement));
    }

    [Fact]
    public void PatchApply_Add_RootReplacement()
    {
        // Covers TryAddValueFromSpan root-replacement path (lines 958-960)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using ParsedJsonDocument<JsonPatchDocument> patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(
            """[{"op":"add","path":"","value":42}]""");
        Assert.True(root.TryApplyPatch(patchDoc.RootElement));
        Assert.Equal(JsonValueKind.Number, root.ValueKind);
    }

    [Fact]
    public void PatchApply_Replace_RootReplacement()
    {
        // Covers TryApplyReplace root-replacement path (lines 735-738)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using ParsedJsonDocument<JsonPatchDocument> patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(
            """[{"op":"replace","path":"","value":42}]""");
        Assert.True(root.TryApplyPatch(patchDoc.RootElement));
        Assert.Equal(JsonValueKind.Number, root.ValueKind);
    }

    [Fact]
    public void PatchApply_Remove_Root_Fails()
    {
        // Covers TryApplyRemove empty-path check (lines 685-688)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using ParsedJsonDocument<JsonPatchDocument> patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(
            """[{"op":"remove","path":""}]""");
        Assert.False(root.TryApplyPatch(patchDoc.RootElement));
    }

    #endregion

    #region TryValidateAndApplyPatch

    [Fact]
    public void TryValidateAndApplyPatch_InvalidPatch_Fails()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        // Not a valid patch document (missing "op")
        using ParsedJsonDocument<JsonPatchDocument> patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(
            """[{"path":"/a","value":1}]""");
        Assert.False(root.TryValidateAndApplyPatch(patchDoc.RootElement));
    }

    [Fact]
    public void TryValidateAndApplyPatch_ValidPatch_Succeeds()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        using ParsedJsonDocument<JsonPatchDocument> patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(
            """[{"op":"add","path":"/b","value":2}]""");
        Assert.True(root.TryValidateAndApplyPatch(patchDoc.RootElement));
        Assert.Equal(2, root.GetProperty("b").GetInt32());
    }

    #endregion

    #region TryParseArrayIndex edge cases

    [Fact]
    public void TryAdd_LeadingZeroIndex_Fails()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 99;

        // "/01" has a leading zero → invalid per RFC 6901
        Assert.False(root.TryAdd("/01"u8, in value));
    }

    [Fact]
    public void TryRemove_NonNumericArrayIndex_Fails()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        // "/abc" is not a valid array index
        Assert.False(root.TryRemove("/abc"u8));
    }

    #endregion

    #region Additional coverage: long-path ArrayPool rent in char overloads

    // The TryAdd long-path test above uses a 148-byte path. We need >256 bytes for ArrayPool rent.
    [Fact]
    public void TryAdd_CharOverload_LongPathRent()
    {
        // Path = "/" + LongKey1 + "/" + LongKey2 = 282 bytes > 256
        string json = BuildNestedJson("""{"z":1}""");
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 99;

        // Add new property inside the deeply-nested object
        string addPath = "/" + LongKey1 + "/" + LongKey2 + "/newProp";
        Assert.True(root.TryAdd(addPath.AsSpan(), in value));
    }

    [Fact]
    public void TryMove_CharOverload_LongPathBothPaths()
    {
        // Both from and dest paths >256 bytes
        string longKey3 = new('z', 140);
        string json = "{\"" + LongKey1 + "\":{\"" + LongKey2 + "\":1},\"" + longKey3 + "\":{\"dest\":{}}}";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        string fromPath = "/" + LongKey1 + "/" + LongKey2;
        string toPath = "/" + longKey3 + "/dest/val";
        Assert.True(root.TryMove(fromPath.AsSpan(), toPath.AsSpan()));
    }

    [Fact]
    public void TryCopy_CharOverload_LongPathBothPaths()
    {
        string longKey3 = new('z', 140);
        string json = "{\"" + LongKey1 + "\":{\"" + LongKey2 + "\":1},\"" + longKey3 + "\":{}}";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        string fromPath = "/" + LongKey1 + "/" + LongKey2;
        string toPath = "/" + longKey3 + "/copyVal";
        Assert.True(root.TryCopy(fromPath.AsSpan(), toPath.AsSpan()));
    }

    #endregion

    #region Diff GrowBuffer (path exceeds initial buffer, triggers GrowBuffer)

    [Fact]
    public void Diff_LongPropertyName_TriggersGrowBuffer()
    {
        // Initial buffer is 1024 bytes (InitialPathBufferSize). A 1100-char property name
        // makes the pointer path >1024 bytes, triggering GrowBuffer in AppendSegment.
        string longProp = new('p', 1100);
        string source = "{\"" + longProp + "\":1}";
        string target = "{\"" + longProp + "\":2}";

        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse(source);
        using ParsedJsonDocument<JsonElement> targetDoc = ParsedJsonDocument<JsonElement>.Parse(target);

        JsonPatchDocument patch = JsonDiffExtensions.CreatePatch(sourceDoc.RootElement, targetDoc.RootElement, workspace);
        Assert.Equal(1, patch.GetArrayLength());
    }

    [Fact]
    public void Diff_LongPropertyName_Array_TriggersGrowBuffer()
    {
        // Array diff at a path exceeding 1024 bytes triggers AppendArrayIndex GrowBuffer.
        // Arrays must be SAME length so element-by-element diff happens (different length → whole replace).
        string longProp = new('p', 1100);
        string source = "{\"" + longProp + "\":[1,2]}";
        string target = "{\"" + longProp + "\":[3,2]}";

        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse(source);
        using ParsedJsonDocument<JsonElement> targetDoc = ParsedJsonDocument<JsonElement>.Parse(target);

        JsonPatchDocument patch = JsonDiffExtensions.CreatePatch(sourceDoc.RootElement, targetDoc.RootElement, workspace);
        Assert.True(patch.GetArrayLength() > 0);
    }

    [Fact]
    public void Diff_DeeplyNested_TriggersGrowBuffer()
    {
        // Max parse depth is 64. Use 5-char keys so path = 64 * 6 = 384 bytes > 256.
        string source = BuildDeeplyNested(60, "1");
        string target = BuildDeeplyNested(60, "2");

        var options = new JsonDocumentOptions { MaxDepth = 128 };
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse(source, options);
        using ParsedJsonDocument<JsonElement> targetDoc = ParsedJsonDocument<JsonElement>.Parse(target, options);

        JsonPatchDocument patch = JsonDiffExtensions.CreatePatch(sourceDoc.RootElement, targetDoc.RootElement, workspace);
        Assert.True(patch.GetArrayLength() > 0);
    }

    [Fact]
    public void Diff_DeeplyNestedArray_TriggersGrowBuffer()
    {
        string source = BuildDeeplyNestedWithArray(60, "[1,2]");
        string target = BuildDeeplyNestedWithArray(60, "[1,2,3]");

        var options = new JsonDocumentOptions { MaxDepth = 128 };
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse(source, options);
        using ParsedJsonDocument<JsonElement> targetDoc = ParsedJsonDocument<JsonElement>.Parse(target, options);

        JsonPatchDocument patch = JsonDiffExtensions.CreatePatch(sourceDoc.RootElement, targetDoc.RootElement, workspace);
        Assert.True(patch.GetArrayLength() > 0);
    }

    private static string BuildDeeplyNested(int depth, string leafValue)
    {
        // Use 5-char keys so each level = "/abcde" = 6 bytes; 60 levels = 360 bytes > 256.
        string open = string.Concat(Enumerable.Repeat("{\"abcde\":", depth));
        string close = new('}', depth);
        return open + leafValue + close;
    }

    private static string BuildDeeplyNestedWithArray(int depth, string leafArray)
    {
        string open = string.Concat(Enumerable.Repeat("{\"abcde\":", depth));
        string close = new('}', depth);
        return open + leafArray + close;
    }

    #endregion

    #region Additional TryApplyPatch error paths

    [Fact]
    public void PatchApply_Remove_ArrayElement()
    {
        string json = """[1,2,3]""";
        string patchJson = """[{"op":"remove","path":"/1"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.True(root.TryApplyPatch(in patch));
        Assert.Equal("[1,3]", root.ToString());
    }

    [Fact]
    public void PatchApply_Remove_ArrayOutOfBounds_Fails()
    {
        string json = """[1,2]""";
        string patchJson = """[{"op":"remove","path":"/5"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Replace_ArrayElement()
    {
        string json = """[1,2,3]""";
        string patchJson = """[{"op":"replace","path":"/1","value":99}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.True(root.TryApplyPatch(in patch));
        Assert.Equal("[1,99,3]", root.ToString());
    }

    [Fact]
    public void PatchApply_Replace_ArrayOutOfBounds_Fails()
    {
        string json = """[1]""";
        string patchJson = """[{"op":"replace","path":"/5","value":99}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Replace_NonExistentProperty_Fails()
    {
        string json = """{"a":1}""";
        string patchJson = """[{"op":"replace","path":"/missing","value":99}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Move_ArrayToArray_Append()
    {
        string json = """{"src":[10,20],"dest":[1,2]}""";
        string patchJson = """[{"op":"move","from":"/src/0","path":"/dest/-"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.True(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Move_ArrayToArray_Index()
    {
        string json = """{"src":[10,20],"dest":[1,2]}""";
        string patchJson = """[{"op":"move","from":"/src/0","path":"/dest/1"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.True(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Move_ArrayToObject()
    {
        string json = """{"src":[10,20],"dest":{}}""";
        string patchJson = """[{"op":"move","from":"/src/0","path":"/dest/val"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.True(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Move_ObjectToArray_Append()
    {
        string json = """{"src":{"v":10},"dest":[1,2]}""";
        string patchJson = """[{"op":"move","from":"/src/v","path":"/dest/-"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.True(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Move_ObjectToArray_Index()
    {
        string json = """{"src":{"v":10},"dest":[1,2]}""";
        string patchJson = """[{"op":"move","from":"/src/v","path":"/dest/0"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.True(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Move_ObjectToObject()
    {
        string json = """{"src":{"v":10},"dest":{}}""";
        string patchJson = """[{"op":"move","from":"/src/v","path":"/dest/val"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.True(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Move_ArrayOutOfBounds_Fails()
    {
        string json = """{"src":[10],"dest":[]}""";
        string patchJson = """[{"op":"move","from":"/src/5","path":"/dest/0"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Move_ArrayToDest_OutOfBounds_Fails()
    {
        string json = """{"src":[10],"dest":[1]}""";
        string patchJson = """[{"op":"move","from":"/src/0","path":"/dest/99"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Move_ObjToDest_OutOfBounds_Fails()
    {
        string json = """{"src":{"v":1},"dest":[1]}""";
        string patchJson = """[{"op":"move","from":"/src/v","path":"/dest/99"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Copy_ToRoot()
    {
        string json = """{"a":42}""";
        string patchJson = """[{"op":"copy","from":"/a","path":""}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.True(root.TryApplyPatch(in patch));
        Assert.Equal("42", root.ToString());
    }

    [Fact]
    public void PatchApply_Copy_InvalidSource_Fails()
    {
        string json = """{"a":1}""";
        string patchJson = """[{"op":"copy","from":"/missing","path":"/b"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Copy_ToArrayAppend_Internal()
    {
        string json = """{"src":99,"dest":[1]}""";
        string patchJson = """[{"op":"copy","from":"/src","path":"/dest/-"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.True(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Copy_ToArrayIndex_Internal()
    {
        string json = """{"src":99,"dest":[1,2]}""";
        string patchJson = """[{"op":"copy","from":"/src","path":"/dest/0"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.True(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Add_ToArrayAppend()
    {
        string json = """[1,2]""";
        string patchJson = """[{"op":"add","path":"/-","value":3}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.True(root.TryApplyPatch(in patch));
        Assert.Equal("[1,2,3]", root.ToString());
    }

    [Fact]
    public void PatchApply_Add_ToArrayIndex()
    {
        string json = """[1,2]""";
        string patchJson = """[{"op":"add","path":"/0","value":99}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.True(root.TryApplyPatch(in patch));
        Assert.Equal("[99,1,2]", root.ToString());
    }

    [Fact]
    public void PatchApply_Remove_InvalidPath_Fails()
    {
        string json = """{"a":1}""";
        string patchJson = """[{"op":"remove","path":"/a/b/c"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Replace_InvalidPath_Fails()
    {
        string json = """{"a":1}""";
        string patchJson = """[{"op":"replace","path":"/a/b/c","value":2}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Move_InvalidFromPath_Fails()
    {
        string json = """{"a":1}""";
        string patchJson = """[{"op":"move","from":"/a/b/c","path":"/dest"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Move_InvalidDestPath_Fails()
    {
        string json = """{"src":1}""";
        string patchJson = """[{"op":"move","from":"/src","path":"/a/b/c"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void Diff_WithTildeAndSlashInKeys()
    {
        string source = """{"a~b":1,"c/d":2}""";
        string target = """{"a~b":10,"c/d":20}""";

        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse(source);
        using ParsedJsonDocument<JsonElement> targetDoc = ParsedJsonDocument<JsonElement>.Parse(target);

        JsonPatchDocument patch = JsonDiffExtensions.CreatePatch(sourceDoc.RootElement, targetDoc.RootElement, workspace);
        Assert.Equal(2, patch.GetArrayLength());
    }

    #endregion

    #region Byte overload error paths + internal TryApply* edge cases

    [Fact]
    public void TryRemove_InvalidParentPath_Byte()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.False(root.TryRemove("/missing/child"u8));
    }

    [Fact]
    public void TryRemove_ParentIsScalar_Byte()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":42}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.False(root.TryRemove("/a/child"u8));
    }

    [Fact]
    public void TryReplace_InvalidParentPath_Byte()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 99;

        Assert.False(root.TryReplace("/missing/child"u8, in value));
    }

    [Fact]
    public void TryReplace_NonNumericArrayIndex_Byte()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""[1,2,3]""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 99;

        Assert.False(root.TryReplace("/abc"u8, in value));
    }

    [Fact]
    public void TryReplace_ParentIsScalar_Byte()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":42}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement.Source value = 99;

        Assert.False(root.TryReplace("/a/child"u8, in value));
    }

    [Fact]
    public void TryMove_InvalidFromParent_Byte()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.False(root.TryMove("/missing/x"u8, "/b"u8));
    }

    [Fact]
    public void TryMove_ArrayFromIndex_ParseFails_Byte()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":[1],"b":{}}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.False(root.TryMove("/a/abc"u8, "/b/x"u8));
    }

    [Fact]
    public void TryMove_ArrayFromIndex_OutOfBounds_Byte()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":[1],"b":{}}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.False(root.TryMove("/a/99"u8, "/b/x"u8));
    }

    [Fact]
    public void TryMove_ObjectRemoveFails_Byte()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":{"x":1},"b":{}}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        // "missing" doesn't exist in "a", RemoveProperty fails
        Assert.False(root.TryMove("/a/missing"u8, "/b/y"u8));
    }

    [Fact]
    public void TryMove_FromScalarParent_Byte()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":42}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.False(root.TryMove("/a/child"u8, "/b"u8));
    }

    [Fact]
    public void TryTest_InvalidPath_Byte()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement expected = JsonElement.ParseValue("1");

        Assert.False(root.TryTest("/missing"u8, in expected));
    }

    [Fact]
    public void PatchApply_Move_ArrayToDest_NonNumericIndex_Fails()
    {
        // TryApplyMove: Array→Array, dest index parse fails (L841-842)
        string json = """{"src":[10],"dest":[1]}""";
        string patchJson = """[{"op":"move","from":"/src/0","path":"/dest/abc"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Move_ArrayToScalar_Fails()
    {
        // TryApplyMove: Array source, dest parent is scalar (L860)
        string json = """{"src":[10],"val":42}""";
        string patchJson = """[{"op":"move","from":"/src/0","path":"/val/x"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Move_ObjToArrayNonNumeric_Fails()
    {
        // TryApplyMove: Object→Array, dest index parse fails (L873-874)
        string json = """{"src":{"v":1},"dest":[1]}""";
        string patchJson = """[{"op":"move","from":"/src/v","path":"/dest/abc"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Move_ObjToScalar_Fails()
    {
        // TryApplyMove: Object source, dest parent is scalar (L890)
        string json = """{"src":{"v":1},"val":42}""";
        string patchJson = """[{"op":"move","from":"/src/v","path":"/val/x"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Move_FromScalar_Fails()
    {
        // TryApplyMove: source parent is scalar (L893)
        string json = """{"a":42}""";
        string patchJson = """[{"op":"move","from":"/a/child","path":"/b"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Copy_InvalidSourcePath_Fails()
    {
        // TryCopyValueFromSpan: TryResolveParent fails (L1014-1015)
        string json = """{"a":1}""";
        string patchJson = """[{"op":"copy","from":"/a","path":"/missing/child"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Copy_ArrayNonNumericIndex_Fails()
    {
        // TryCopyValueFromSpan: Array, TryParseArrayIndex fails (L1029-1030)
        string json = """{"src":1,"dest":[1,2]}""";
        string patchJson = """[{"op":"copy","from":"/src","path":"/dest/abc"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Copy_ArrayIndexOutOfBounds_Fails()
    {
        // TryCopyValueFromSpan: Array index out of bounds (L1034-1035)
        string json = """{"src":1,"dest":[1]}""";
        string patchJson = """[{"op":"copy","from":"/src","path":"/dest/99"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Copy_DestParentIsScalar_Fails()
    {
        // TryCopyValueFromSpan: parent is scalar (L1048)
        string json = """{"src":1,"val":42}""";
        string patchJson = """[{"op":"copy","from":"/src","path":"/val/x"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Move_AddToRootViaEmptyDestPath_Fails()
    {
        // TryApplyMove: empty dest path → "cannot move to root" → false (L796-798)
        string json = """{"src":{"v":42}}""";
        string patchJson = """[{"op":"move","from":"/src/v","path":""}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Remove_ParentIsScalar_Fails()
    {
        // TryApplyRemove: parent is neither array nor object (L719)
        string json = """{"a":42}""";
        string patchJson = """[{"op":"remove","path":"/a/child"}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void PatchApply_Replace_ParentIsScalar_Fails()
    {
        // TryApplyReplace: parent is scalar (L770)
        string json = """{"a":42}""";
        string patchJson = """[{"op":"replace","path":"/a/child","value":1}]""";
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        Assert.False(root.TryApplyPatch(in patch));
    }

    [Fact]
    public void Diff_LongPath_Array_TriggersAppendArrayIndexGrow()
    {
        // Two 510-char properties so path = 1+510+1+510 = 1022 bytes.
        // AppendArrayIndex needs 1022+11 = 1033 > 1024 → GrowBuffer in AppendArrayIndex.
        string key1 = new('a', 510);
        string key2 = new('b', 510);
        string source = "{\"" + key1 + "\":{\"" + key2 + "\":[1,2]}}";
        string target = "{\"" + key1 + "\":{\"" + key2 + "\":[1,2,3]}}";

        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse(source);
        using ParsedJsonDocument<JsonElement> targetDoc = ParsedJsonDocument<JsonElement>.Parse(target);

        JsonPatchDocument patch = JsonDiffExtensions.CreatePatch(sourceDoc.RootElement, targetDoc.RootElement, workspace);
        Assert.True(patch.GetArrayLength() > 0);
    }

    [Fact]
    public void TryMove_AddToScalarParent_Byte()
    {
        // TryAddValueFromSpan: parent is scalar (L999)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"src":1,"val":42}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.False(root.TryMove("/src"u8, "/val/x"u8));
    }

    #endregion

    #region Final edge cases: malformed ops, invalid pointers, empty segments

    // Short ops (L82-83) and unknown ops (L141) trigger Debug.Assert — they represent
    // invalid API usage (pre-validated patch documents). Not testable without Debug.Assert failure.

    [Fact]
    public void TryRemove_InvalidPointer_NoSlash_Byte()
    {
        // TryResolveParent: lastSlash < 0 (L1104-1105)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.False(root.TryRemove("abc"u8));
    }

    [Fact]
    public void TryTest_EmptyPath_ComparesRoot_Byte()
    {
        // TryResolvePointer: empty pointer → root (L1078-1080)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement expected = JsonElement.ParseValue("""{"a":1}""");

        Assert.True(root.TryTest(""u8, in expected));
    }

    [Fact]
    public void TryTest_InvalidPointer_NoSlash_Byte()
    {
        // TryResolvePointer: TryCreateJsonPointer fails (L1084-1086)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        JsonElement expected = JsonElement.ParseValue("1");

        Assert.False(root.TryTest("abc"u8, in expected));
    }

    [Fact]
    public void TryRemove_EmptyLastSegment_Byte()
    {
        // TryParseArrayIndex: empty segment (L1144-1145)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"arr":[1,2]}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        // Path "/arr/" → parent is arr (array), last segment is empty
        Assert.False(root.TryRemove("/arr/"u8));
    }

    [Fact]
    public void TryMove_ToRoot_Byte()
    {
        // TryAddValueFromSpan: empty dest → ReplaceRoot (L958-960)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":42,"b":99}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.True(root.TryMove("/a"u8, ""u8));
        Assert.Equal("42", root.ToString());
    }

    [Fact]
    public void TryMove_CharOverload_LongDestPath()
    {
        // L498-500: ArrayPool return for long dest path in TryMove char overload
        // Two segments each ≤256 bytes, total >256 bytes to trigger ArrayPool rent
        string longDest = "/" + new string('z', 140) + "/" + new string('y', 140);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        // Parent of dest doesn't exist, so move fails — but the ArrayPool rent/return is exercised
        Assert.False(root.TryMove("/a".AsSpan(), longDest.AsSpan()));
    }

    [Fact]
    public void TryCopy_CharOverload_LongDestPath()
    {
        // L573-575: ArrayPool return for long dest path in TryCopy char overload
        string longDest = "/" + new string('z', 140) + "/" + new string('y', 140);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        Assert.False(root.TryCopy("/a".AsSpan(), longDest.AsSpan()));
    }

    #endregion

    #region Move/Copy from array error paths

    [Fact]
    public void TryMove_FromArrayNonNumericSegment_Fails()
    {
        // L431-435: TryMove from array path with non-numeric segment
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"arr":[1,2,3],"b":0}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        // "foo" is not a valid array index — TryParseArrayIndex fails
        Assert.False(root.TryMove("/arr/foo"u8, "/b"u8));
    }

    [Fact]
    public void TryMove_FromArrayOutOfBounds_Fails()
    {
        // L438-440: TryMove from array with index out of bounds
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"arr":[1,2,3],"b":0}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        // index 10 is out of bounds for 3-element array
        Assert.False(root.TryMove("/arr/10"u8, "/b"u8));
    }

    [Fact]
    public void TryMove_FromNonContainerParent_Fails()
    {
        // L452-454: fromParent is not array or object (it's a scalar)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"a":"hello","b":0}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        // /a is a string, /a/0 tries to traverse into it
        Assert.False(root.TryMove("/a/0"u8, "/b"u8));
    }

    [Fact]
    public void PatchApply_Move_FromArrayNonNumericSegment_Fails()
    {
        // L822-824: TryApplyMove from array path with non-numeric segment (via TryApplyPatch)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"arr":[1,2,3],"b":0}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        string patchJson = """[{"op":"move","from":"/arr/foo","path":"/b"}]""";
        using ParsedJsonDocument<JsonPatchDocument> patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(patchJson);
        Assert.False(root.TryApplyPatch(patchDoc.RootElement));
    }

    [Fact]
    public void PatchApply_Move_FromArrayOutOfBounds_Fails()
    {
        // L827-829: TryApplyMove from array with index out of bounds (via TryApplyPatch)
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"arr":[1,2,3],"b":0}""");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;

        string patchJson = """[{"op":"move","from":"/arr/10","path":"/b"}]""";
        using ParsedJsonDocument<JsonPatchDocument> patchDoc = ParsedJsonDocument<JsonPatchDocument>.Parse(patchJson);
        Assert.False(root.TryApplyPatch(patchDoc.RootElement));
    }

    // Note: L81-83 (opBytes.Length < 5) is unreachable in Debug builds because
    // Debug.Assert(patch.EvaluateSchema()) fires first. It's a defensive guard for
    // Release-mode callers who pass an invalid patch.

    #endregion
}
