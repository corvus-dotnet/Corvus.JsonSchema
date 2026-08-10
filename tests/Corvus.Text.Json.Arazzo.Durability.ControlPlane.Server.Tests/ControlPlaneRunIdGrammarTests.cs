// <copyright file="ControlPlaneRunIdGrammarTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Pins ADR 0065 §9's run-id grammar at the control-plane ingress: a run id is exactly 32 lowercase hex characters,
/// validated at every ingress before any store touch. Each run-addressed operation must refuse a non-conforming id
/// with 400 at the generated validation layer, and a conforming-but-absent id must keep the non-disclosing 404 — the
/// grammar narrows the key space the handlers ever see without changing what they answer inside it.
/// </summary>
[TestClass]
public sealed class ControlPlaneRunIdGrammarTests
{
    // Exactly 32 lowercase hex — the native mint (Guid "n") and the grammar ADR 0065 §9 pins.
    private const string ConformingAbsentId = "0123456789abcdef0123456789abcdef";

    [TestMethod]
    [DataRow("run-1", DisplayName = "non-hex (the pre-grammar fixture idiom)")]
    [DataRow("0123456789abcdef0123456789abcdef0", DisplayName = "33 hex characters")]
    [DataRow("0123456789abcdef0123456789abcde", DisplayName = "31 hex characters")]
    [DataRow("0123456789ABCDEF0123456789ABCDEF", DisplayName = "uppercase hex")]
    public async Task A_run_id_outside_the_grammar_is_refused_at_every_run_addressed_ingress(string runId)
    {
        await using Host host = await Host.StartAsync();

        (await host.Client.GetAsync($"/runs/{runId}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.Client.GetAsync($"/runs/{runId}/steps")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.Client.PostAsync($"/runs/{runId}/resume", Json("""{}"""))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.Client.PostAsync($"/runs/{runId}/cancel", Json("""{"reason":"grammar"}"""))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task A_conforming_but_absent_run_id_keeps_the_non_disclosing_404()
    {
        await using Host host = await Host.StartAsync();

        (await host.Client.GetAsync($"/runs/{ConformingAbsentId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private sealed class Host(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public static async Task<Host> StartAsync()
        {
            var store = new InMemoryWorkflowStateStore();
            var management = new SecuredWorkflowManagement(store, "ops");
            var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops");

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            WebApplication app = builder.Build();
            app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.Open);
            await app.StartAsync();

            return new Host(app, app.GetTestClient());
        }

        public async ValueTask DisposeAsync()
        {
            this.Client.Dispose();
            await app.DisposeAsync();
        }
    }
}
