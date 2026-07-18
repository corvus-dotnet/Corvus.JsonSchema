// <copyright file="ControlPlaneRunnerAuthorizationsApiTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Tests the control-plane runner-authorization API (design §5.5) over <c>/environments/{name}/runners</c> and
/// <c>/runnerAuthorizations</c>: a runner enters <c>Pending</c> on self-registration (seeded directly into the store, since
/// registration is not one of these endpoints) and is dispatchable only once an administrator of the target environment
/// authorizes it; authorization is revocable. The roster list and the approver inbox span the environments the caller
/// administers. Authorized by the per-environment administrator gate (200/403/404/409), not a global capability scope.
/// </summary>
[TestClass]
public sealed class ControlPlaneRunnerAuthorizationsApiTests
{
    [TestMethod]
    public async Task Authorizing_a_pending_runner_as_an_administrator_makes_it_authorized()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", null, default);
        await using Scoped host = await StartAsync(runnerAuth);

        // acme provisions 'production', granting itself administration of it.
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        using Stj.JsonDocument authorized = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme"));
        authorized.RootElement.GetProperty("status").GetString().ShouldBe("Authorized");
        authorized.RootElement.GetProperty("runnerId").GetString().ShouldBe("runner-1");
        authorized.RootElement.GetProperty("decidedBy").GetString().ShouldBe("acme");
    }

    [TestMethod]
    public async Task Authorizing_an_already_authorized_runner_is_idempotent()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", null, default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // A second authorize returns the existing Authorized record unchanged.
        using Stj.JsonDocument again = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme"));
        again.RootElement.GetProperty("status").GetString().ShouldBe("Authorized");
    }

    [TestMethod]
    public async Task Authorizing_as_a_non_administrator_of_the_environment_is_forbidden()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", null, default);
        await using Scoped host = await StartAsync(runnerAuth);

        // acme provisions (and administers) 'production'; globex administers nothing here.
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "globex")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task Authorizing_for_an_unknown_environment_is_not_found()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", null, default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        // 'nowhere' does not exist / is outside reach → 404 (before the runner record is consulted).
        (await host.SendAsync(HttpMethod.Post, "/environments/nowhere/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Authorizing_a_runner_that_never_registered_pre_authorizes_it()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        // Pre-authorization (§5.5): the admin allow-lists a runner that has NOT registered yet → an Authorized record is
        // created directly, attributed to the admin (createdBy + decidedBy = acme), rather than 404-ing.
        using Stj.JsonDocument preauth = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-expected/authorization", "acme"));
        preauth.RootElement.GetProperty("status").GetString().ShouldBe("Authorized");
        preauth.RootElement.GetProperty("runnerId").GetString().ShouldBe("runner-expected");
        preauth.RootElement.GetProperty("createdBy").GetString().ShouldBe("acme");
        preauth.RootElement.GetProperty("decidedBy").GetString().ShouldBe("acme");

        // When the runner later registers with a matching id, EnsurePendingAsync leaves the Authorized row unchanged, so
        // the pre-authorized runner is dispatchable immediately with no second approval.
        using ParsedJsonDocument<EnvironmentRunnerAuthorization> afterRegister = await runnerAuth.EnsurePendingAsync("production", "runner-expected", "runner-expected", null, default);
        afterRegister.RootElement.IsAuthorized.ShouldBeTrue();
    }

    [TestMethod]
    public async Task Revoking_an_authorized_runner_as_an_administrator_makes_it_revoked()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", null, default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using Stj.JsonDocument revoked = await ReadJsonAsync(await host.SendAsync(HttpMethod.Delete, "/environments/production/runners/runner-1/authorization", "acme"));
        revoked.RootElement.GetProperty("status").GetString().ShouldBe("Revoked");
    }

    [TestMethod]
    public async Task Quarantining_an_authorized_runner_makes_it_quarantined()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", null, default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using Stj.JsonDocument quarantined = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/quarantine", "acme"));
        quarantined.RootElement.GetProperty("status").GetString().ShouldBe("Quarantined");
        quarantined.RootElement.GetProperty("decidedBy").GetString().ShouldBe("acme");
    }

    [TestMethod]
    public async Task Reinstating_a_quarantined_runner_authorizes_it_without_re_registration()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", null, default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/quarantine", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Reinstate is the ordinary authorize verb applied to a quarantined runner — no re-registration required.
        using Stj.JsonDocument reinstated = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme"));
        reinstated.RootElement.GetProperty("status").GetString().ShouldBe("Authorized");
    }

    [TestMethod]
    public async Task Quarantining_a_pending_runner_conflicts()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", null, default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        // A Pending runner is not dispatching, so there is nothing to drain — quarantine is a 409, not a silent no-op.
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/quarantine", "acme")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task Quarantining_a_revoked_runner_conflicts_so_it_cannot_be_downgraded_to_temporary()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", null, default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendAsync(HttpMethod.Delete, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // A permanent removal must not be silently downgraded to a temporary exclusion.
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/quarantine", "acme")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task Re_authorizing_a_revoked_runner_returns_it_to_service()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", null, default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendAsync(HttpMethod.Delete, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Revoke is not terminal: a deliberate re-authorization returns a revoked runner to service.
        using Stj.JsonDocument reauthorized = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme"));
        reauthorized.RootElement.GetProperty("status").GetString().ShouldBe("Authorized");
    }

    [TestMethod]
    public async Task Quarantining_as_a_non_administrator_is_forbidden()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", null, default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/quarantine", "globex")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task Revoking_a_runner_fences_the_in_flight_run_it_leases()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", null, default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The runner holds a live lease on a run it is executing. A peer cannot take it while the lease is live.
        (await host.StateStore.AcquireLeaseAsync("run-1", "runner-1", System.TimeSpan.FromMinutes(5), default)).ShouldNotBeNull();
        (await host.StateStore.AcquireLeaseAsync("run-1", "peer", System.TimeSpan.FromMinutes(5), default)).ShouldBeNull();

        // Revoking the runner fences its in-flight work: the lease is expired, so a peer reclaims the run at once.
        (await host.SendAsync(HttpMethod.Delete, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.StateStore.AcquireLeaseAsync("run-1", "peer", System.TimeSpan.FromMinutes(5), default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task The_roster_filters_by_quarantined_status()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-a", "runner", null, default);
        await runnerAuth.EnsurePendingAsync("production", "runner-b", "runner", null, default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-a/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-b/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-a/quarantine", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using Stj.JsonDocument quarantined = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/runners?status=Quarantined", "acme"));
        Stj.JsonElement entry = quarantined.RootElement.GetProperty("authorizations").EnumerateArray().Single();
        entry.GetProperty("runnerId").GetString().ShouldBe("runner-a");
        entry.GetProperty("status").GetString().ShouldBe("Quarantined");
    }

    [TestMethod]
    public async Task Listing_an_environments_runners_as_an_administrator_lists_the_seeded_runner_and_filters_by_status()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-pending", "runner", null, default);
        await runnerAuth.EnsurePendingAsync("production", "runner-authorized", "runner", null, default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        // Authorize one of the two so the status filter has something to discriminate.
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-authorized/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The unfiltered roster lists both runners.
        using (Stj.JsonDocument all = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/runners", "acme")))
        {
            all.RootElement.GetProperty("authorizations").EnumerateArray().Select(r => r.GetProperty("runnerId").GetString()).OrderBy(id => id).ShouldBe(["runner-authorized", "runner-pending"]);
        }

        // ?status=Pending filters to the still-pending runner only.
        using Stj.JsonDocument pending = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production/runners?status=Pending", "acme"));
        Stj.JsonElement entry = pending.RootElement.GetProperty("authorizations").EnumerateArray().Single();
        entry.GetProperty("runnerId").GetString().ShouldBe("runner-pending");
        entry.GetProperty("status").GetString().ShouldBe("Pending");
    }

    [TestMethod]
    public async Task Listing_an_environments_runners_as_a_non_administrator_is_forbidden()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", null, default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await host.SendAsync(HttpMethod.Get, "/environments/production/runners", "globex")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task The_approver_inbox_returns_pending_authorizations_for_the_environments_the_caller_administers()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", null, default);
        await using Scoped host = await StartAsync(runnerAuth);

        // acme administers 'production'; the inbox (no environment, defaulting to Pending) surfaces its pending runner.
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        using Stj.JsonDocument inbox = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/runnerAuthorizations", "acme"));
        Stj.JsonElement entry = inbox.RootElement.GetProperty("authorizations").EnumerateArray().Single();
        entry.GetProperty("environment").GetString().ShouldBe("production");
        entry.GetProperty("runnerId").GetString().ShouldBe("runner-1");
        entry.GetProperty("status").GetString().ShouldBe("Pending");
    }

    [TestMethod]
    public async Task The_approver_inbox_is_empty_for_a_caller_who_administers_nothing()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", null, default);
        await using Scoped host = await StartAsync(runnerAuth);

        // acme provisions (and administers) 'production'; globex administers nothing, so its inbox is empty.
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        using Stj.JsonDocument inbox = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/runnerAuthorizations", "globex"));
        inbox.RootElement.GetProperty("authorizations").EnumerateArray().ShouldBeEmpty();
    }

    [TestMethod]
    public async Task The_runner_authorization_lifecycle_emits_governance_audit_spans()
    {
        // §850: every runner decision — the act that decides which compute may claim and execute work — leaves a
        // governance-audit trace, with the prior state naming the act (reinstate a quarantined runner vs authorize a
        // fresh one).
        using GovernanceAuditSpans audit = GovernanceAuditSpans.Capture();
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", null, default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/quarantine", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendAsync(HttpMethod.Delete, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        audit.Outcomes("runner-1@production").ShouldBe(["authorized", "quarantined", "reinstated", "revoked"]);
    }

    [TestMethod]
    public async Task A_non_administrator_runner_decision_is_audited_as_refused()
    {
        using GovernanceAuditSpans audit = GovernanceAuditSpans.Capture();
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", null, default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "globex")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        audit.Outcomes("runner-1@production").ShouldBe(["refused-not-administrator"]);
        audit.Spans.Single().OperationName.ShouldBe("runner.authorize");
    }

    /// <summary>Captures the runner-authorization governance-audit spans (design §850) on the Arazzo <see cref="ActivitySource"/>.</summary>
    private sealed class GovernanceAuditSpans : IDisposable
    {
        private readonly List<Activity> spans = [];
        private readonly ActivityListener listener;

        private GovernanceAuditSpans()
        {
            this.listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == ArazzoTelemetry.ActivitySourceName,
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    if (activity.OperationName.StartsWith("runner.", StringComparison.Ordinal))
                    {
                        lock (this.spans)
                        {
                            this.spans.Add(activity);
                        }
                    }
                },
            };
            ActivitySource.AddActivityListener(this.listener);
        }

        // A snapshot of the captured spans, in stop order.
        public IReadOnlyList<Activity> Spans
        {
            get
            {
                lock (this.spans)
                {
                    return [.. this.spans];
                }
            }
        }

        public static GovernanceAuditSpans Capture() => new();

        // The ordered outcomes recorded for the given runner (runnerId@environment), across its lifecycle.
        public IReadOnlyList<string> Outcomes(string targetId)
        {
            lock (this.spans)
            {
                return [.. this.spans.Where(s => (string?)s.GetTagItem(ArazzoTelemetry.TargetIdTag) == targetId).Select(s => (string)s.GetTagItem(ArazzoTelemetry.OutcomeTag)!)];
            }
        }

        public void Dispose() => this.listener.Dispose();
    }

    private static async Task<Stj.JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task<Scoped> StartAsync(IEnvironmentRunnerAuthorizationStore runnerAuthorizations)
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops", credentials: null, administrators: new InMemoryWorkflowAdministratorStore());

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(ScopeTenantSubAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ScopeTenantSubAuthHandler>(ScopeTenantSubAuthHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        // Pass the workflow state store so the runner-authorization handler picks up its lease-administration capability
        // (the §5.5 revocation fence): revoking a runner expires the leases it holds. The store is exposed to the test so a
        // fence assertion can check that a revoked runner's lease is reclaimable.
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.Scoped, rowSecurity: new TenantIdentityPolicy(), environmentRunnerAuthorizationStore: runnerAuthorizations, workflowStateStore: store);
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient(), store);
    }

    // Maps X-Tenant to both the deployment governance identity (sys:tenant=<t>) and the requester subject (sub=<t>), with
    // full read reach, so create-grants-admin + the administrator gate AND the decidedBy audit actor are driven per caller.
    private sealed class TenantIdentityPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal)
        {
            string? tenant = principal?.FindFirst("tenant")?.Value;
            return string.IsNullOrEmpty(tenant) ? [] : [new SecurityTag(SecurityShell.DefaultInternalPrefix + "tenant", tenant)];
        }
    }

    private sealed class Scoped(WebApplication app, HttpClient client, InMemoryWorkflowStateStore stateStore) : IAsyncDisposable
    {
        public InMemoryWorkflowStateStore StateStore => stateStore;

        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string tenant)
            => this.SendCoreAsync(new HttpRequestMessage(method, path), tenant);

        public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string tenant)
            => this.SendCoreAsync(new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, tenant);

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, string tenant)
        {
            using (request)
            {
                // Any X-Scopes value authenticates (the runner-authorization endpoints require authentication, not a
                // specific scope); X-Tenant becomes both the governance identity and the deciding subject.
                request.Headers.Add(ScopeTenantSubAuthHandler.ScopeHeader, "authenticated");
                request.Headers.Add(ScopeTenantSubAuthHandler.TenantHeader, tenant);
                return await client.SendAsync(request);
            }
        }
    }

    private sealed class ScopeTenantSubAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "ScopesTenantSub";
        public const string ScopeHeader = "X-Scopes";
        public const string TenantHeader = "X-Tenant";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.ContainsKey(ScopeHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            // The presence of X-Scopes authenticates; the caller is granted the full capability-scope set the harness's
            // scoped endpoints require (creating an environment and authorize/revoke need environments:write; the runner
            // roster list needs environments:read) — the authorization actually under test is the per-tenant environment
            // administrator gate, not which scope is held.
            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", "environments:read environments:write availability:read availability:write credentials:write"));
            if (this.Request.Headers.TryGetValue(TenantHeader, out Microsoft.Extensions.Primitives.StringValues tenant))
            {
                identity.AddClaim(new Claim("tenant", tenant.ToString()));
                identity.AddClaim(new Claim("sub", tenant.ToString()));
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}