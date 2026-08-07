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
    public async Task Authorizing_a_runner_that_never_registered_pre_authorizes_the_named_principal()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        // Pre-authorization (§5.5, hardened by ADR 0065 decision 2): the admin allow-lists a runner that has NOT
        // registered yet → an Authorized record is created directly, attributed to the admin (createdBy + decidedBy =
        // acme), rather than 404-ing. It names the machine principal that will register, so the record is bound from the
        // moment it exists.
        using Stj.JsonDocument preauth = await ReadJsonAsync(await host.SendJsonAsync(
            HttpMethod.Post,
            "/environments/production/runners/runner-expected/authorization",
            """{"expectedPrincipal":"svc-runner-a"}""",
            "acme"));
        preauth.RootElement.GetProperty("status").GetString().ShouldBe("Authorized");
        preauth.RootElement.GetProperty("runnerId").GetString().ShouldBe("runner-expected");
        preauth.RootElement.GetProperty("createdBy").GetString().ShouldBe("acme");
        preauth.RootElement.GetProperty("decidedBy").GetString().ShouldBe("acme");
        preauth.RootElement.GetProperty("principal").GetString().ShouldBe("svc-runner-a");

        // When the named runner later registers, the Authorized row is left unchanged, so it is dispatchable at once
        // with no second approval.
        using ParsedJsonDocument<EnvironmentRunnerAuthorization> afterRegister = await runnerAuth.EnsurePendingAsync("production", "runner-expected", "runner-expected", "svc-runner-a", default);
        afterRegister.RootElement.IsAuthorized.ShouldBeTrue();
    }

    [TestMethod]
    public async Task Pre_authorizing_without_naming_a_principal_is_refused()
    {
        // ADR 0065 decision 2: allow-listing a bare runner id hands the authorization to whichever principal registers
        // first, and the id is chosen by an administrator rather than derived from anything secret. The decision must
        // name who it is for.
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-expected/authorization", "acme"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Nothing was created, so the id is still free for a properly named pre-authorization.
        (await runnerAuth.GetAsync("production", "runner-expected", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Authorizing_a_registered_runner_needs_no_expected_principal()
    {
        // The name is only needed where there is nothing to compare against. A runner that has registered already proved
        // which principal owns its id, so requiring the administrator to repeat it would be ceremony.
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await PreAuthorizeAsync(host, "production", "runner-1", "svc-runner-a");
        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", RegisterBody("runner-1"), "svc-runner-a")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using Stj.JsonDocument authorized = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme"));
        authorized.RootElement.GetProperty("status").GetString().ShouldBe("Authorized");
        authorized.RootElement.GetProperty("principal").GetString().ShouldBe("svc-runner-a");
    }

    [TestMethod]
    public async Task Registering_into_a_pre_authorization_records_liveness_against_the_bound_principal()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await PreAuthorizeAsync(host, "production", "runner-1", "svc-runner-a");

        // The runner authenticates as a machine principal (the harness maps the caller's identity 'svc-runner-a' to the
        // token subject → principal) and registers into the decision already made about it. The authorization is returned
        // unchanged, still attributed to the administrator who made it, and still bound to the principal it named — the
        // runner contributes its liveness, never its own standing.
        using Stj.JsonDocument registered = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", RegisterBody("runner-1"), "svc-runner-a"));
        registered.RootElement.GetProperty("status").GetString().ShouldBe("Authorized");
        registered.RootElement.GetProperty("runnerId").GetString().ShouldBe("runner-1");
        registered.RootElement.GetProperty("principal").GetString().ShouldBe("svc-runner-a");
        registered.RootElement.GetProperty("createdBy").GetString().ShouldBe("acme");

        // The liveness registration is recorded in the registry, keyed on the runnerId, serving the environment.
        IReadOnlyList<RunnerRegistration> runners = await host.Registry.ListAsync(default);
        RunnerRegistration record = runners.Single();
        ((string)record.RunnerId).ShouldBe("runner-1");
        ((string)record.Environment).ShouldBe("production");
    }

    [TestMethod]
    public async Task Registering_a_runner_persists_its_advertised_isolation_model()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        // The runner advertises an Isolated execution backend (ADR 0058) in its self-description; the server stamp must
        // copy isolationModel through bytes-to-bytes, so the start gate can later match it against an environment.
        const string body = """{"runnerId":"runner-1","startedAt":"2026-06-01T09:00:00Z","maxConcurrency":4,"transports":[],"hostedVersions":[],"isolationModel":"Isolated"}""";
        await PreAuthorizeAsync(host, "production", "runner-1", "svc-runner-a");
        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", body, "svc-runner-a")).StatusCode.ShouldBe(HttpStatusCode.OK);

        RunnerRegistration record = (await host.Registry.ListAsync(default)).Single();
        record.IsolationModelValue.ShouldBe(RunIsolationModel.Isolated);
    }

    [TestMethod]
    public async Task Registering_an_under_isolated_runner_into_an_isolated_environment_is_refused()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);

        // The environment demands Isolated execution (ADR 0058).
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"secure","requiredIsolation":"Isolated"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        // An in-process runner (no advertised isolationModel ⇒ InProcess) cannot meet the environment's Isolated floor, so its
        // registration is refused at the door: it never enters the registry, so it can never be authorized nor claim a run —
        // closing the gap where an admitted InProcess runner would silently run isolated-required work in-process. The
        // authoritative isolation match still runs at the start gate; this stops the misconfiguration before it reaches dispatch.
        await PreAuthorizeAsync(host, "secure", "runner-1", "svc-runner-a");
        var refused = await host.SendJsonAsync(HttpMethod.Post, "/environments/secure/runners", RegisterBody("runner-1"), "svc-runner-a");
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using Stj.JsonDocument problem = await ReadJsonAsync(refused);
        problem.RootElement.GetProperty("type").GetString().ShouldEndWith("insufficient-isolation");

        // Refused before RegisterAsync: the registry holds no record for it. The administrator's pre-authorization is
        // untouched — the runner failed to meet the floor, which is a fact about the runner rather than a reason to
        // discard a decision an administrator made.
        (await host.Registry.ListAsync(default)).ShouldBeEmpty();
        using ParsedJsonDocument<EnvironmentRunnerAuthorization>? stillPreAuthorized = await runnerAuth.GetAsync("secure", "runner-1", default);
        stillPreAuthorized.ShouldNotBeNull();
        stillPreAuthorized!.RootElement.PrincipalEquals("svc-runner-a").ShouldBeTrue();
    }

    [TestMethod]
    public async Task Registering_an_isolated_runner_into_an_isolated_environment_is_accepted()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"secure","requiredIsolation":"Isolated"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        // A runner advertising an Isolated execution backend meets the floor and registers into its pre-authorization.
        const string isolatedBody = """{"runnerId":"runner-1","startedAt":"2026-06-01T09:00:00Z","maxConcurrency":4,"transports":[],"hostedVersions":[],"isolationModel":"Isolated"}""";
        await PreAuthorizeAsync(host, "secure", "runner-1", "svc-runner-a");
        using Stj.JsonDocument registered = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/environments/secure/runners", isolatedBody, "svc-runner-a"));
        registered.RootElement.GetProperty("status").GetString().ShouldBe("Authorized");
        (await host.Registry.ListAsync(default)).Single().IsolationModelValue.ShouldBe(RunIsolationModel.Isolated);
    }

    [TestMethod]
    public async Task Authorizing_a_runner_that_no_longer_meets_a_raised_isolation_floor_is_refused()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);

        // The environment starts InProcess, so an in-process runner registers cleanly against a Pending authorization.
        // The Pending row is written through the store rather than by registering: since ADR 0065 decision 2 an
        // administrator's decision must precede registration, and the decision endpoint records an authorized one, so
        // registration no longer produces Pending. The state itself still exists, and this is the case that needs it —
        // a runner an administrator has named but not yet approved.
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await runnerAuth.EnsurePendingAsync("production", "runner-1", "acme", "svc-runner-a", default)).Dispose();
        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", RegisterBody("runner-1"), "svc-runner-a")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // An administrator later raises the environment's isolation floor to Isolated (ADR 0058).
        (await host.SendJsonAsync(HttpMethod.Put, "/environments/production", """{"requiredIsolation":"Isolated"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Authorizing the now-under-isolated runner is refused (belt-and-braces for the register-time floor): it advertises
        // InProcess but the environment now requires Isolated, so it must not be granted dispatch authorization.
        var refused = await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme");
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using Stj.JsonDocument problem = await ReadJsonAsync(refused);
        problem.RootElement.GetProperty("type").GetString().ShouldEndWith("insufficient-isolation");

        // The runner stays unauthorized — the refusal did not flip it to Authorized.
        using ParsedJsonDocument<EnvironmentRunnerAuthorization>? auth = await runnerAuth.GetAsync("production", "runner-1", default);
        auth!.RootElement.IsAuthorized.ShouldBeFalse();
    }

    [TestMethod]
    public async Task Authorizing_an_isolated_runner_into_an_isolated_environment_succeeds()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"secure","requiredIsolation":"Isolated"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        const string isolatedBody = """{"runnerId":"runner-1","startedAt":"2026-06-01T09:00:00Z","maxConcurrency":4,"transports":[],"hostedVersions":[],"isolationModel":"Isolated"}""";
        await PreAuthorizeAsync(host, "secure", "runner-1", "svc-runner-a");
        (await host.SendJsonAsync(HttpMethod.Post, "/environments/secure/runners", isolatedBody, "svc-runner-a")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The runner meets the Isolated floor, so authorization proceeds normally.
        using Stj.JsonDocument authorized = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, "/environments/secure/runners/runner-1/authorization", "acme"));
        authorized.RootElement.GetProperty("status").GetString().ShouldBe("Authorized");
    }

    [TestMethod]
    public async Task Raising_isolation_while_an_under_isolated_runner_is_authorized_is_refused()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);

        // An in-process runner registers into an InProcess environment and an administrator authorizes it.
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await PreAuthorizeAsync(host, "production", "runner-1", "svc-runner-a");
        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", RegisterBody("runner-1"), "svc-runner-a")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Raising the environment's isolation floor to Isolated is refused: the authorized runner advertises InProcess and
        // would be stranded. The register-time and authorize-time floors cannot retract its existing grant, so the raise
        // itself is blocked until it is revoked or replaced.
        var refused = await host.SendJsonAsync(HttpMethod.Put, "/environments/production", """{"requiredIsolation":"Isolated"}""", "acme");
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using Stj.JsonDocument problem = await ReadJsonAsync(refused);
        problem.RootElement.GetProperty("type").GetString().ShouldEndWith("insufficient-isolation-raise");

        // The floor is unchanged — the refusal happened before the update was applied.
        using Stj.JsonDocument env = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/environments/production", "acme"));
        if (env.RootElement.TryGetProperty("requiredIsolation", out Stj.JsonElement iso))
        {
            iso.GetString().ShouldNotBe("Isolated");
        }
    }

    [TestMethod]
    public async Task Raising_isolation_when_the_authorized_runner_already_meets_it_succeeds()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        // An Isolated-backed runner registers (allowed into an InProcess environment) and is authorized.
        const string isolatedBody = """{"runnerId":"runner-1","startedAt":"2026-06-01T09:00:00Z","maxConcurrency":4,"transports":[],"hostedVersions":[],"isolationModel":"Isolated"}""";
        await PreAuthorizeAsync(host, "production", "runner-1", "svc-runner-a");
        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", isolatedBody, "svc-runner-a")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The authorized runner already provides Isolated, so raising the floor strands nobody — it succeeds.
        (await host.SendJsonAsync(HttpMethod.Put, "/environments/production", """{"requiredIsolation":"Isolated"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task Raising_isolation_with_no_authorized_runners_succeeds()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        // No runner is authorized, so raising the isolation floor strands nobody.
        (await host.SendJsonAsync(HttpMethod.Put, "/environments/production", """{"requiredIsolation":"Isolated"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task Raising_isolation_with_only_a_pending_under_isolated_runner_succeeds()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        // An in-process runner registers but is never authorized (stays Pending). Only Authorized runners fence the raise:
        // a Pending runner is not dispatching, and the authorize-time floor will refuse to authorize it into the raised
        // environment, so it cannot strand the environment. The raise therefore succeeds. The Pending row is written
        // through the store because registration no longer produces one (see the note in the isolation-floor test above).
        (await runnerAuth.EnsurePendingAsync("production", "runner-1", "acme", "svc-runner-a", default)).Dispose();
        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", RegisterBody("runner-1"), "svc-runner-a")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendJsonAsync(HttpMethod.Put, "/environments/production", """{"requiredIsolation":"Isolated"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task Re_registering_with_the_same_principal_keeps_the_authorization()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await PreAuthorizeAsync(host, "production", "runner-1", "svc-runner-a");
        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", RegisterBody("runner-1"), "svc-runner-a")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // An administrator authorizes the runner; a subsequent re-registration by the same principal must not reset it.
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using Stj.JsonDocument reregistered = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", RegisterBody("runner-1"), "svc-runner-a"));
        reregistered.RootElement.GetProperty("status").GetString().ShouldBe("Authorized");
        reregistered.RootElement.GetProperty("principal").GetString().ShouldBe("svc-runner-a");
    }

    [TestMethod]
    public async Task Registering_a_runnerId_owned_by_a_different_principal_is_refused()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await PreAuthorizeAsync(host, "production", "runner-1", "svc-runner-a");
        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", RegisterBody("runner-1"), "svc-runner-a")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // A different authenticated machine cannot take over a runnerId that a principal already owns (§16.4). It is
        // refused as not pre-authorized rather than as a conflict: telling a stranger that an id is taken is how it
        // learns which ids exist, and the id is an administrator's choice rather than a secret.
        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", RegisterBody("runner-1"), "svc-runner-b")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // The original binding is untouched.
        using ParsedJsonDocument<EnvironmentRunnerAuthorization>? bound = await runnerAuth.GetAsync("production", "runner-1", default);
        bound!.RootElement.PrincipalEquals("svc-runner-a").ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_refused_registration_leaves_the_victims_registry_row_untouched()
    {
        // ADR 0065 decision 2: an authorization row must exist before any registry row is written. The principal fence
        // lives on the authorization, so writing the liveness row first means a foreign principal overwrites the
        // victim's registration — its hosted versions, concurrency, isolation, and last-seen — and only then gets its
        // refusal. The refusal has to leave nothing behind.
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await PreAuthorizeAsync(host, "production", "runner-1", "svc-runner-a");
        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", RegisterBody("runner-1", maxConcurrency: 4), "svc-runner-a")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", RegisterBody("runner-1", maxConcurrency: 99), "svc-runner-b")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        RunnerRegistration? registered = await host.Registry.GetAsync("runner-1", default);
        registered.ShouldNotBeNull();
        ((int)registered!.Value.MaxConcurrency).ShouldBe(4);
    }

    [TestMethod]
    public async Task Registering_for_an_unknown_environment_is_not_found()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await host.SendJsonAsync(HttpMethod.Post, "/environments/nowhere/runners", RegisterBody("runner-1"), "svc-runner-a")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task The_pre_authorized_principal_registers_into_it_and_no_other_can()
    {
        // The squatting window ADR 0065 decision 2 closes: between an administrator's pre-authorization and the runner's
        // arrival, the id must not be claimable by anyone else.
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendJsonAsync(
            HttpMethod.Post,
            "/environments/production/runners/runner-expected/authorization",
            """{"expectedPrincipal":"svc-runner-a"}""",
            "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Anyone else presenting that id is refused, and leaves no registry row behind.
        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", RegisterBody("runner-expected"), "svc-runner-b"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await host.Registry.GetAsync("runner-expected", default)).ShouldBeNull();

        // The named principal registers into its own pre-authorization and is dispatchable at once.
        using Stj.JsonDocument registered = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", RegisterBody("runner-expected"), "svc-runner-a"));
        registered.RootElement.GetProperty("status").GetString().ShouldBe("Authorized");
        registered.RootElement.GetProperty("principal").GetString().ShouldBe("svc-runner-a");
    }

    // A minimal RunnerRegistrationRequest body: the runner's self-description (the server stamps environment/reachTags/lastSeenAt).
    private static string RegisterBody(string runnerId, int maxConcurrency = 4)
        => $$"""{"runnerId":"{{runnerId}}","startedAt":"2026-06-01T09:00:00Z","maxConcurrency":{{maxConcurrency}},"transports":[],"hostedVersions":[]}""";

    // An administrator pre-authorizes the id for the principal that will present it. Registration requires this to exist
    // (ADR 0065 decision 2), so it is the first step of every registration path rather than an alternative to one.
    private static async Task PreAuthorizeAsync(Scoped host, string environment, string runnerId, string principal)
        => (await host.SendJsonAsync(
            HttpMethod.Post,
            $"/environments/{environment}/runners/{runnerId}/authorization",
            $$"""{"expectedPrincipal":"{{principal}}"}""",
            "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

    // A registration body carrying an enrolment token, for a runner no administrator has named.
    private static string EnrolBody(string runnerId, string token)
        => $$"""{"runnerId":"{{runnerId}}","startedAt":"2026-06-01T09:00:00Z","maxConcurrency":4,"transports":[],"hostedVersions":[],"enrolmentToken":"{{token}}"}""";

    [TestMethod]
    public async Task A_runner_presenting_an_enrolment_token_enrols_itself_pending()
    {
        // The path that lets an environment scale its runners: no administrator named this instance in advance, and it
        // still cannot execute anything — it enters the approval queue bound to the principal that presented the token.
        byte[] secret = Encoding.UTF8.GetBytes("an-enrolment-secret-of-entirely-sufficient-length");
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth, secret);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        string token = EnrolmentToken.Issue(secret, "production", DateTimeOffset.UtcNow.AddMinutes(15));
        using Stj.JsonDocument enrolled = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", EnrolBody("runner-scaled-out", token), "svc-runner-a"));

        enrolled.RootElement.GetProperty("status").GetString().ShouldBe("Pending");
        enrolled.RootElement.GetProperty("principal").GetString().ShouldBe("svc-runner-a");
        ((string)(await host.Registry.GetAsync("runner-scaled-out", default))!.Value.RunnerId).ShouldBe("runner-scaled-out");
    }

    [TestMethod]
    public async Task An_enrolment_token_for_another_environment_enrols_nothing()
    {
        // Registration stays environment-scoped. A token delivered to one environment's runners must not admit them to
        // another, or the token would restore the blanket capability it exists to bound.
        byte[] secret = Encoding.UTF8.GetBytes("an-enrolment-secret-of-entirely-sufficient-length");
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth, secret);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"staging"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        string stagingToken = EnrolmentToken.Issue(secret, "staging", DateTimeOffset.UtcNow.AddMinutes(15));

        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", EnrolBody("runner-1", stagingToken), "svc-runner-a"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await runnerAuth.GetAsync("production", "runner-1", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task An_expired_enrolment_token_enrols_nothing()
    {
        byte[] secret = Encoding.UTF8.GetBytes("an-enrolment-secret-of-entirely-sufficient-length");
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth, secret);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        string expired = EnrolmentToken.Issue(secret, "production", DateTimeOffset.UtcNow.AddMinutes(-1));

        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", EnrolBody("runner-1", expired), "svc-runner-a"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task An_enrolment_token_cannot_take_over_another_principals_runner()
    {
        // The squatting fence has to survive the new door. A token admits a runner to an environment; it does not
        // transfer an id already bound to someone else, or enrolment would become the takeover the fence prevents.
        byte[] secret = Encoding.UTF8.GetBytes("an-enrolment-secret-of-entirely-sufficient-length");
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth, secret);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await PreAuthorizeAsync(host, "production", "runner-1", "svc-runner-a");

        string token = EnrolmentToken.Issue(secret, "production", DateTimeOffset.UtcNow.AddMinutes(15));

        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", EnrolBody("runner-1", token), "svc-runner-b"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using ParsedJsonDocument<EnvironmentRunnerAuthorization>? bound = await runnerAuth.GetAsync("production", "runner-1", default);
        bound!.RootElement.PrincipalEquals("svc-runner-a").ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_deployment_with_no_enrolment_secret_enrols_nobody()
    {
        // An absent secret means enrolment is off, not that anything presented is accepted.
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        string token = EnrolmentToken.Issue(Encoding.UTF8.GetBytes("an-enrolment-secret-of-entirely-sufficient-length"), "production", DateTimeOffset.UtcNow.AddMinutes(15));

        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", EnrolBody("runner-1", token), "svc-runner-a"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task An_enrolled_runner_becomes_dispatchable_only_when_an_administrator_authorizes_it()
    {
        // Register-then-approve, which is the flow the token exists to keep. Enrolment puts the runner in the queue; an
        // administrator still makes the decision that lets it execute.
        byte[] secret = Encoding.UTF8.GetBytes("an-enrolment-secret-of-entirely-sufficient-length");
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth, secret);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        string token = EnrolmentToken.Issue(secret, "production", DateTimeOffset.UtcNow.AddMinutes(15));
        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", EnrolBody("runner-1", token), "svc-runner-a")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The administrator needs no expectedPrincipal: enrolment already proved which principal holds the id.
        using Stj.JsonDocument authorized = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme"));
        authorized.RootElement.GetProperty("status").GetString().ShouldBe("Authorized");
        authorized.RootElement.GetProperty("principal").GetString().ShouldBe("svc-runner-a");
    }

    [TestMethod]
    public async Task Registering_without_a_pre_authorization_is_refused()
    {
        // ADR 0065 decision 2: `runners:register` is reach-scoped per environment and never the system context. The
        // authorization row IS that scoping — holding the scope lets a principal ask, and an administrator's decision
        // decides which environment and which id it may ask about. Without one there is nothing to register into.
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", RegisterBody("runner-1"), "svc-runner-a"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Nothing was created by the attempt: neither an authorization it could later grow into, nor a liveness row.
        (await runnerAuth.GetAsync("production", "runner-1", default)).ShouldBeNull();
        (await host.Registry.GetAsync("runner-1", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Every_registration_refusal_is_indistinguishable_from_the_others()
    {
        // The property, not the status code. A principal that may not register here learns nothing about why: whether
        // the environment exists, whether the id is taken, and whether someone else holds it are all the same answer.
        // Distinguishing them would turn registration into an enumeration oracle over environments and runner ids,
        // which is the squatting reconnaissance ADR 0065 decision 2 closes.
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await PreAuthorizeAsync(host, "production", "runner-taken", "svc-runner-a");

        HttpResponseMessage unknownEnvironment = await host.SendJsonAsync(HttpMethod.Post, "/environments/nowhere/runners", RegisterBody("runner-1"), "svc-runner-b");
        HttpResponseMessage noPreAuthorization = await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", RegisterBody("runner-1"), "svc-runner-b");
        HttpResponseMessage anothersPreAuthorization = await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", RegisterBody("runner-taken"), "svc-runner-b");

        unknownEnvironment.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        noPreAuthorization.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        anothersPreAuthorization.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        string unknownBody = await unknownEnvironment.Content.ReadAsStringAsync();
        (await noPreAuthorization.Content.ReadAsStringAsync()).ShouldBe(unknownBody);
        (await anothersPreAuthorization.Content.ReadAsStringAsync()).ShouldBe(unknownBody);

        // And the victim's binding is untouched by the attempt on it.
        using ParsedJsonDocument<EnvironmentRunnerAuthorization>? bound = await runnerAuth.GetAsync("production", "runner-taken", default);
        bound!.RootElement.PrincipalEquals("svc-runner-a").ShouldBeTrue();
    }

    [TestMethod]
    public async Task Withdrawing_a_mistyped_pre_authorization_frees_the_runner_id()
    {
        // The recovery path for the one decision no later decision can correct: expectedPrincipal binds when the record
        // is created and never moves, so a typo would otherwise make the id permanently unusable.
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendJsonAsync(
            HttpMethod.Post,
            "/environments/production/runners/runner-1/authorization",
            """{"expectedPrincipal":"svc-runner-typo"}""",
            "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await host.SendAsync(HttpMethod.Delete, "/environments/production/runners/runner-1/preAuthorization", "acme"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Re-pre-authorized against the right principal, which now registers into it.
        (await host.SendJsonAsync(
            HttpMethod.Post,
            "/environments/production/runners/runner-1/authorization",
            """{"expectedPrincipal":"svc-runner-a"}""",
            "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        using Stj.JsonDocument registered = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", RegisterBody("runner-1"), "svc-runner-a"));
        registered.RootElement.GetProperty("status").GetString().ShouldBe("Authorized");
        registered.RootElement.GetProperty("principal").GetString().ShouldBe("svc-runner-a");
    }

    [TestMethod]
    public async Task Withdrawing_is_refused_once_the_runner_has_registered()
    {
        // Withdrawal removes the record; revocation keeps it (ADR 0027). Letting withdrawal stand in for revocation
        // would erase the evidence the runner was ever authorized and leave its leases live.
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await PreAuthorizeAsync(host, "production", "runner-1", "svc-runner-a");
        (await host.SendJsonAsync(HttpMethod.Post, "/environments/production/runners", RegisterBody("runner-1"), "svc-runner-a")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await host.SendAsync(HttpMethod.Delete, "/environments/production/runners/runner-1/preAuthorization", "acme"))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // The record is untouched, so revoke is still available and still auditable.
        (await runnerAuth.GetAsync("production", "runner-1", default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Withdrawing_nothing_is_no_content()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await host.SendAsync(HttpMethod.Delete, "/environments/production/runners/never/preAuthorization", "acme"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [TestMethod]
    public async Task Withdrawing_as_a_non_administrator_is_forbidden()
    {
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendJsonAsync(
            HttpMethod.Post,
            "/environments/production/runners/runner-1/authorization",
            """{"expectedPrincipal":"svc-runner-a"}""",
            "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await host.SendAsync(HttpMethod.Delete, "/environments/production/runners/runner-1/preAuthorization", "globex"))
            .StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);

        (await runnerAuth.GetAsync("production", "runner-1", default)).ShouldNotBeNull();
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
        // The lease owner is the MACHINE PRINCIPAL, never the runner id (ADR 0065 decision 2: ownership derives from the
        // authenticated principal, so a compromised runner cannot change it by renaming itself). This test used to seed
        // the lease under the runner id, which is not what the runner API writes, so it passed while the fence expired
        // nothing in production. Binding a principal and leasing as that principal is what makes the assertion real.
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "runner-1", "runner", "svc-runner-a", default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The runner holds a live lease on a run it is executing. A peer cannot take it while the lease is live.
        (await host.StateStore.AcquireLeaseAsync("run-1", "svc-runner-a", System.TimeSpan.FromMinutes(5), default)).ShouldNotBeNull();
        (await host.StateStore.AcquireLeaseAsync("run-1", "peer", System.TimeSpan.FromMinutes(5), default)).ShouldBeNull();

        // Revoking the runner fences its in-flight work: the lease is expired, so a peer reclaims the run at once.
        (await host.SendAsync(HttpMethod.Delete, "/environments/production/runners/runner-1/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.StateStore.AcquireLeaseAsync("run-1", "peer", System.TimeSpan.FromMinutes(5), default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Revoking_a_runner_does_not_expire_leases_belonging_to_its_id_rather_than_its_principal()
    {
        // The complement of the test above, and the reason the fence cannot simply try both: a runner id is
        // client-supplied and an administrator may name any string. If the fence expired leases owned by the id as well,
        // a runner could be registered under an id equal to a VICTIM principal's name and its revocation would expire
        // the victim's in-flight work.
        var runnerAuth = new InMemoryEnvironmentRunnerAuthorizationStore();
        await runnerAuth.EnsurePendingAsync("production", "svc-victim", "runner", "svc-runner-a", default);
        await using Scoped host = await StartAsync(runnerAuth);
        (await host.SendJsonAsync(HttpMethod.Post, "/environments", """{"name":"production"}""", "acme")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await host.SendAsync(HttpMethod.Post, "/environments/production/runners/svc-victim/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // An unrelated principal whose name happens to equal the revoked runner's id holds a live lease.
        (await host.StateStore.AcquireLeaseAsync("run-2", "svc-victim", System.TimeSpan.FromMinutes(5), default)).ShouldNotBeNull();

        (await host.SendAsync(HttpMethod.Delete, "/environments/production/runners/svc-victim/authorization", "acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Untouched: the fence expires what the revoked runner owns, which is what its bound principal owns.
        (await host.StateStore.AcquireLeaseAsync("run-2", "peer", System.TimeSpan.FromMinutes(5), default)).ShouldBeNull();
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

    private static async Task<Scoped> StartAsync(IEnvironmentRunnerAuthorizationStore runnerAuthorizations, byte[]? enrolmentSecret = null)
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

        var registry = new InMemoryRunnerRegistry();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        // Pass the workflow state store so the runner-authorization handler picks up its lease-administration capability
        // (the §5.5 revocation fence): revoking a runner expires the leases it holds. The store is exposed to the test so a
        // fence assertion can check that a revoked runner's lease is reclaimable. The runner registry is likewise exposed so
        // the registration tests can confirm a registered runner's liveness record (design §16.4).
        app.MapArazzoControlPlane(management, catalog, registry, ControlPlaneSecurityMode.Scoped, rowSecurity: new TenantIdentityPolicy(), environmentRunnerAuthorizationStore: runnerAuthorizations, workflowStateStore: store, runnerEnrolmentSecret: enrolmentSecret ?? default(ReadOnlyMemory<byte>));
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient(), store, registry);
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

    private sealed class Scoped(WebApplication app, HttpClient client, InMemoryWorkflowStateStore stateStore, InMemoryRunnerRegistry registry) : IAsyncDisposable
    {
        public InMemoryWorkflowStateStore StateStore => stateStore;

        public InMemoryRunnerRegistry Registry => registry;

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
            // roster list needs environments:read; registering a runner needs runners:register) — the authorization actually
            // under test is the per-tenant environment administrator gate (or, for registration, the machine principal), not
            // which scope is held.
            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", "environments:read environments:write availability:read availability:write credentials:write runners:register"));
            if (this.Request.Headers.TryGetValue(TenantHeader, out Microsoft.Extensions.Primitives.StringValues tenant))
            {
                identity.AddClaim(new Claim("tenant", tenant.ToString()));
                identity.AddClaim(new Claim("sub", tenant.ToString()));
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}