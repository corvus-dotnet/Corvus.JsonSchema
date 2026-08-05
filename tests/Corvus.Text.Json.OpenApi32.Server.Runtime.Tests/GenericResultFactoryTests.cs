// <copyright file="GenericResultFactoryTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using CanonTests32.Server;
using CanonTests32.Server.Models;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.OpenApi32.Server.Runtime.Tests;

/// <summary>
/// Behaviour of the generated context-threaded materialisation surface: the model's
/// <c>CreateBuilder&lt;TContext&gt;(in Source&lt;TContext&gt;)</c> and the server result factory's
/// <c>Ok&lt;TContext&gt;(Source&lt;TContext&gt; body, workspace)</c>. Both let a caller assemble a body closure-free
/// (threading values through a ref-struct context and a <c>static</c> builder) and materialise it in a single pass —
/// producing a document byte-identical to the non-generic path.
/// </summary>
[TestClass]
public class GenericResultFactoryTests
{
    /// <summary>The model's context-threaded <c>CreateBuilder&lt;TContext&gt;</c> materialises the same document as the non-generic overload.</summary>
    [TestMethod]
    public void CreateBuilder_ContextThreaded_MaterialisesSameDocumentAsNonGeneric()
    {
        using JsonWorkspace ws = JsonWorkspace.Create();

        // Reference: the non-generic field-form path.
        ItemSchema reference = ItemSchema.CreateBuilder(ws, ItemSchema.Build(progress: 42, status: "active")).RootElement;

        // Under test: thread the values through a (value-tuple) context + static builder, then materialise via the new
        // CreateBuilder<TContext>(in Source<TContext>). No closure is captured.
        var context = (Progress: 42, Status: "active");
        ItemSchema.Source<(int Progress, string Status)> source = ItemSchema.Build(
            in context,
            static (in (int Progress, string Status) c, ref ItemSchema.Builder b) => b.Create(progress: c.Progress, status: c.Status));
        ItemSchema viaContext = ItemSchema.CreateBuilder(ws, in source).RootElement;

        Assert.AreEqual(42, ((JsonElement)viaContext).GetProperty("progress"u8).GetInt32());
        Assert.AreEqual("active", ((JsonElement)viaContext).GetProperty("status"u8).GetString());

        // Identical to the non-generic path.
        Assert.AreEqual(
            ((JsonElement)reference).GetProperty("progress"u8).GetInt32(),
            ((JsonElement)viaContext).GetProperty("progress"u8).GetInt32());
        Assert.AreEqual(
            ((JsonElement)reference).GetProperty("status"u8).GetString(),
            ((JsonElement)viaContext).GetProperty("status"u8).GetString());
    }

    /// <summary>The server result factory's <c>Ok&lt;TContext&gt;</c> materialises the same body as the non-generic <c>Ok</c>, routing the context-threaded body through a single materialisation.</summary>
    [TestMethod]
    public void Ok_ContextThreadedBody_ProducesSameBodyAsNonGeneric()
    {
        using JsonWorkspace wsNonGeneric = JsonWorkspace.Create();
        using JsonWorkspace wsGeneric = JsonWorkspace.Create();

        // Non-generic factory.
        GetEmptyServersResult nonGeneric = GetEmptyServersResult.Ok(GetEmptyServersOk.Build(ok: true), wsNonGeneric);

        // Generic factory: a closure-free body Source<TContext>, materialised once by Ok<TContext>.
        var context = new System.ValueTuple<bool>(true);
        GetEmptyServersOk.Source<System.ValueTuple<bool>> body = GetEmptyServersOk.Build(
            in context,
            static (in System.ValueTuple<bool> c, ref GetEmptyServersOk.Builder b) => b.Create(ok: c.Item1));
        GetEmptyServersResult generic = GetEmptyServersResult.Ok(body, wsGeneric);

        Assert.AreEqual(200, generic.StatusCode);
        Assert.AreEqual("application/json", generic.ContentType);
        Assert.IsTrue(generic.Body.GetProperty("ok"u8).GetBoolean());

        // Identical body to the non-generic path.
        Assert.AreEqual(
            nonGeneric.Body.GetProperty("ok"u8).GetBoolean(),
            generic.Body.GetProperty("ok"u8).GetBoolean());
    }

    /// <summary>An any-schema (universal <see cref="JsonElement"/>) body routes the generic factory through the core
    /// <c>JsonElement.CreateBuilder&lt;TContext&gt;(in Source&lt;TContext&gt;)</c> — the hand-written mirror added for parity.</summary>
    [TestMethod]
    public void Created_AnySchemaBody_RoutesThroughJsonElementCreateBuilder()
    {
        using JsonWorkspace ws = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("{\"rating\":5}");

        // JsonElement -> Source -> Source<TContext> (the any-schema body path), materialised by Created<TContext>.
        JsonElement.Source bodySource = doc.RootElement;
        JsonElement.Source<System.ValueTuple> body = bodySource;
        SubmitFeedbackResult result = SubmitFeedbackResult.Created(body, ws);

        Assert.AreEqual(201, result.StatusCode);
        Assert.AreEqual(5, result.Body.GetProperty("rating"u8).GetInt32());
    }
}