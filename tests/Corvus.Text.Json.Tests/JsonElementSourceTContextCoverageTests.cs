// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for <see cref="JsonElement.Source{TContext}"/> that exercise previously-uncovered
/// code paths identified through dotnet-coverage analysis. All 66 lines of Source{TContext}
/// were at 0% in the baseline.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="JsonElement.Source{TContext}"/> is a <c>readonly ref struct</c> that wraps either
/// an <see cref="JsonElement.ArrayBuilder"/> delegate, an <see cref="JsonElement.ObjectBuilder"/>
/// delegate, or a non-generic <see cref="JsonElement.Source"/>. Each test exercises one
/// <c>Kind</c> branch × one method combination.
/// </para>
/// <para>
/// All calls to <c>Source{TContext}</c> methods that accept <c>ref ComplexValueBuilder</c> are
/// routed through static helper methods to avoid CS8350 ref-struct scoping errors. The helpers
/// receive <c>Source{TContext}</c> as a by-value parameter, giving the compiler a clear scope
/// boundary for the ref struct's captured references.
/// </para>
/// </remarks>
[TestClass]
public class JsonElementSourceTContextCoverageTests
{
    #region Constructors and properties

    [TestMethod]
    public void Default_IsUndefined()
    {
        JsonElement.Source<int> source = default;
        Assert.IsTrue(source.IsUndefined);
    }

    [TestMethod]
    public void ArrayBuilderConstructor_IsNotUndefined()
    {
        var source = new JsonElement.Source<int>(
            42,
            static (in int ctx, ref JsonElement.ArrayBuilder ab) => ab.AddItem(ctx));

        Assert.IsFalse(source.IsUndefined);
    }

    [TestMethod]
    public void ObjectBuilderConstructor_IsNotUndefined()
    {
        var source = new JsonElement.Source<int>(
            7,
            static (in int ctx, ref JsonElement.ObjectBuilder ob) => ob.AddProperty("v"u8, ctx));

        Assert.IsFalse(source.IsUndefined);
    }

    #endregion

    #region AddAsItem — all three Kind branches

    [TestMethod]
    public void AddAsItem_ArrayBuilder_ProducesArray()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var doc = BuildFromSourceItem(
            workspace,
            new JsonElement.Source<int>(
                10,
                static (in int ctx, ref JsonElement.ArrayBuilder ab) =>
                {
                    ab.AddItem(ctx);
                    ab.AddItem(ctx + 1);
                }));

        Assert.AreEqual(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.AreEqual(2, doc.RootElement.GetArrayLength());
        Assert.AreEqual(10, doc.RootElement[0].GetInt32());
    }

    [TestMethod]
    public void AddAsItem_ObjectBuilder_ProducesObject()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var doc = BuildFromSourceItem(
            workspace,
            new JsonElement.Source<string>(
                "test",
                static (in string ctx, ref JsonElement.ObjectBuilder ob) =>
                {
                    ob.AddProperty("name"u8, ctx);
                }));

        Assert.AreEqual(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.AreEqual("test", doc.RootElement.GetProperty("name"u8).GetString());
    }

    [TestMethod]
    public void AddAsItem_SourceWrapping_DelegatesToInner()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var doc = BuildFromSourceItem(
            workspace,
            new JsonElement.Source<int>(
                new JsonElement.Source(
                    static (ref JsonElement.ObjectBuilder ob) =>
                    {
                        ob.AddProperty("wrapped"u8, true);
                    })));

        Assert.AreEqual(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.IsTrue(doc.RootElement.GetProperty("wrapped"u8).GetBoolean());
    }

    [TestMethod]
    public void AddAsItem_ImplicitOperator_FromNonGenericSource()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        // Implicit conversion from Source to Source<int>
        JsonElement.Source<int> source = new JsonElement.Source(
            static (ref JsonElement.ArrayBuilder ab) =>
            {
                ab.AddItem(99);
            });

        using var doc = BuildFromSourceItem(workspace, source);

        Assert.AreEqual(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.AreEqual(99, doc.RootElement[0].GetInt32());
    }

    #endregion

    #region AddAsProperty(utf8) — all three Kind branches

    [TestMethod]
    public void AddAsProperty_Utf8_ArrayBuilder()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var doc = BuildFromSourceUtf8Property(
            workspace,
            new JsonElement.Source<int>(
                5,
                static (in int ctx, ref JsonElement.ArrayBuilder ab) =>
                {
                    ab.AddItem(ctx);
                }));

        Assert.AreEqual(JsonValueKind.Array, doc.RootElement.GetProperty("items"u8).ValueKind);
    }

    [TestMethod]
    public void AddAsProperty_Utf8_ObjectBuilder()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var doc = BuildFromSourceUtf8Property(
            workspace,
            new JsonElement.Source<int>(
                7,
                static (in int ctx, ref JsonElement.ObjectBuilder ob) =>
                {
                    ob.AddProperty("n"u8, ctx);
                }));

        Assert.AreEqual(7, doc.RootElement.GetProperty("items"u8).GetProperty("n"u8).GetInt32());
    }

    [TestMethod]
    public void AddAsProperty_Utf8_SourceWrapping()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var doc = BuildFromSourceUtf8Property(
            workspace,
            new JsonElement.Source<int>(
                new JsonElement.Source(
                    static (ref JsonElement.ArrayBuilder ab) =>
                    {
                        ab.AddItem(42);
                    })));

        Assert.AreEqual(JsonValueKind.Array, doc.RootElement.GetProperty("items"u8).ValueKind);
    }

    #endregion

    #region AddAsPrebakedProperty — all three Kind branches

    [TestMethod]
    public void AddAsPrebakedProperty_ArrayBuilder()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var doc = BuildFromSourcePrebakedProperty(
            workspace,
            new JsonElement.Source<int>(
                3,
                static (in int ctx, ref JsonElement.ArrayBuilder ab) =>
                {
                    ab.AddItem(ctx);
                }));

        Assert.AreEqual(JsonValueKind.Array, doc.RootElement.GetProperty("arr"u8).ValueKind);
    }

    [TestMethod]
    public void AddAsPrebakedProperty_ObjectBuilder()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var doc = BuildFromSourcePrebakedProperty(
            workspace,
            new JsonElement.Source<int>(
                9,
                static (in int ctx, ref JsonElement.ObjectBuilder ob) =>
                {
                    ob.AddProperty("v"u8, ctx);
                }));

        Assert.AreEqual(9, doc.RootElement.GetProperty("arr"u8).GetProperty("v"u8).GetInt32());
    }

    [TestMethod]
    public void AddAsPrebakedProperty_SourceWrapping()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var doc = BuildFromSourcePrebakedProperty(
            workspace,
            new JsonElement.Source<int>(
                new JsonElement.Source(
                    static (ref JsonElement.ObjectBuilder ob) =>
                    {
                        ob.AddProperty("ok"u8, true);
                    })));

        Assert.IsTrue(doc.RootElement.GetProperty("arr"u8).GetProperty("ok"u8).GetBoolean());
    }

    #endregion

    #region AddAsProperty(string) — exercises string → char span dispatch for all Kinds

    [TestMethod]
    public void AddAsProperty_String_ArrayBuilder()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var doc = BuildFromSourceStringProperty(
            workspace,
            new JsonElement.Source<int>(
                2,
                static (in int ctx, ref JsonElement.ArrayBuilder ab) =>
                {
                    ab.AddItem(ctx);
                }));

        Assert.AreEqual(JsonValueKind.Array, doc.RootElement.GetProperty("list"u8).ValueKind);
    }

    [TestMethod]
    public void AddAsProperty_String_ObjectBuilder()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var doc = BuildFromSourceStringProperty(
            workspace,
            new JsonElement.Source<int>(
                4,
                static (in int ctx, ref JsonElement.ObjectBuilder ob) =>
                {
                    ob.AddProperty("n"u8, ctx);
                }));

        Assert.AreEqual(4, doc.RootElement.GetProperty("list"u8).GetProperty("n"u8).GetInt32());
    }

    [TestMethod]
    public void AddAsProperty_String_SourceWrapping()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var doc = BuildFromSourceStringProperty(
            workspace,
            new JsonElement.Source<int>(
                new JsonElement.Source(
                    static (ref JsonElement.ArrayBuilder ab) =>
                    {
                        ab.AddItem(100);
                    })));

        Assert.AreEqual(JsonValueKind.Array, doc.RootElement.GetProperty("list"u8).ValueKind);
    }

    #endregion

    #region Helpers — isolate Source<TContext> in method parameters to avoid CS8350

    private static JsonDocumentBuilder<JsonElement.Mutable> BuildFromSourceItem<TContext>(
        JsonWorkspace workspace,
        JsonElement.Source<TContext> source)
    {
        JsonDocumentBuilder<JsonElement.Mutable> documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        ComplexValueBuilder cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        source.AddAsItem(ref cvb);
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        return documentBuilder;
    }

    private static JsonDocumentBuilder<JsonElement.Mutable> BuildFromSourceUtf8Property<TContext>(
        JsonWorkspace workspace,
        JsonElement.Source<TContext> source)
    {
        JsonDocumentBuilder<JsonElement.Mutable> documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        ComplexValueBuilder cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        source.AddAsProperty("items"u8, ref cvb);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        return documentBuilder;
    }

    private static JsonDocumentBuilder<JsonElement.Mutable> BuildFromSourcePrebakedProperty<TContext>(
        JsonWorkspace workspace,
        JsonElement.Source<TContext> source)
    {
        JsonDocumentBuilder<JsonElement.Mutable> documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        ComplexValueBuilder cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();

        // Prebaked property name format: 4-byte LE header + JSON-quoted name bytes
        // Header = (quotedLength << 4) | DynamicValueType.QuotedUtf8String
        // For "arr": quoted = "\"arr\"" = 5 bytes, header = (5 << 4) | 1 = 0x51
        ReadOnlySpan<byte> prebaked = [0x51, 0x00, 0x00, 0x00, 0x22, 0x61, 0x72, 0x72, 0x22];
        source.AddAsPrebakedProperty(prebaked, ref cvb);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        return documentBuilder;
    }

    private static JsonDocumentBuilder<JsonElement.Mutable> BuildFromSourceStringProperty<TContext>(
        JsonWorkspace workspace,
        JsonElement.Source<TContext> source)
    {
        JsonDocumentBuilder<JsonElement.Mutable> documentBuilder = workspace.CreateBuilder<JsonElement.Mutable>(-1);
        ComplexValueBuilder cvb = ComplexValueBuilder.Create(documentBuilder, 30);
        cvb.StartObject();
        source.AddAsProperty("list", ref cvb);
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        return documentBuilder;
    }

    #endregion
}
