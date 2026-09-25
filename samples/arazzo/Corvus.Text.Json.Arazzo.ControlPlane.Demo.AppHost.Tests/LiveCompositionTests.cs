// <copyright file="LiveCompositionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

// The Durability reference brings Corvus.Text.Json into the enclosing namespace scope, ahead of any alias; this test
// reads the API with the framework reader, as it always has, so it names it.
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.ControlPlane.Demo.AppHost.Tests;

/// <summary>
/// The two-process live-execution loop, end to end: the REAL Aspire composition (control plane +
/// a separate Runner.Demo process + Postgres/Keycloak/Vault/NATS containers + the example services)
/// executes a §18 debug run for real. This is the automated form of the hand-verified recipe in
/// <c>ControlPlane.Demo/docs/live-execution.md</c>: the control plane only MARKS runs claimable
/// (R5), so every advance observed here was made by the separate runner process — its registration
/// heartbeat, dispatch loop, and draft-run pump are all on the hook.
/// </summary>
/// <remarks>
/// Opt-in: heavy (a full container composition; minutes, not seconds). Set
/// <c>ARAZZO_APPHOST_E2E=1</c> and have a container runtime (podman/docker) available, then:
/// <c>dotnet run --project samples/arazzo/Corvus.Text.Json.Arazzo.ControlPlane.Demo.AppHost.Tests -f net10.0</c>.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class LiveCompositionTests
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    [TestMethod]
    [Timeout(600_000)]
    public async Task The_real_runner_process_registers_and_advances_a_debug_run_to_completion()
    {
        if (Environment.GetEnvironmentVariable("ARAZZO_APPHOST_E2E") != "1")
        {
            Assert.Inconclusive("Set ARAZZO_APPHOST_E2E=1 (and have a container runtime) to run the full two-process composition e2e.");
        }

        IDistributedApplicationTestingBuilder appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Corvus_Text_Json_Arazzo_ControlPlane_Demo_AppHost>();
        await using DistributedApplication app = await appHost.BuildAsync();
        await app.StartAsync();

        ResourceNotificationService notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications.WaitForResourceHealthyAsync("controlplane", default).WaitAsync(StartupTimeout);
        await notifications.WaitForResourceHealthyAsync("runner", default).WaitAsync(StartupTimeout);

        using HttpClient http = app.CreateHttpClient("controlplane");
        http.DefaultRequestHeaders.Add("X-Api-Key", "demo-admin-key");

        // 1. RunnerRegistrationService: the separate runner process self-registered and heartbeats;
        //    the control plane's registry lists it against the development environment.
        Stj.JsonElement runners = await PollAsync(
            http, "/arazzo/v1/runners?limit=50",
            doc => doc.GetProperty("runners").GetArrayLength() > 0,
            "the runner process registers itself");
        runners.GetProperty("runners").EnumerateArray()
            .Any(r => r.GetProperty("environment").GetString() == "development")
            .ShouldBeTrue("the $draft runner serves the development environment");

        // 1b. Authenticated registration (design §16.4): the runner registered through the control plane's authenticated
        //     endpoint as its machine principal, so its authorization for the development environment binds to the
        //     arazzo-runner client id (its azp), not the self-asserted runnerId. Only a real token, correctly scoped, gets a
        //     runner this far — a store-direct self-assertion would carry no principal. (The demo's auto-authorization service
        //     then moves it Pending -> Authorized; the bound principal carries through the decision.)
        Stj.JsonElement developmentRunners = await PollAsync(
            http, "/arazzo/v1/environments/development/runners?limit=50",
            doc => doc.GetProperty("authorizations").EnumerateArray().Any(a => a.TryGetProperty("principal", out _)),
            "the runner's authorization binds a machine principal");
        developmentRunners.GetProperty("authorizations").EnumerateArray()
            .Any(a => a.TryGetProperty("principal", out Stj.JsonElement p) && p.GetString() == "arazzo-runner")
            .ShouldBeTrue("the bound machine principal is the arazzo-runner client (design §16.4)");

        // 2. A working copy from the seeded catalog: the same document the designer would open.
        using HttpResponseMessage created = await http.PostAsJsonAsync(
            "/arazzo/v1/workspace/workflows",
            new { fromBaseWorkflowId = "onboard-customer", fromVersionNumber = 1, name = "apphost-e2e" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        string workingCopyId;
        string workflowId;
        using (Stj.JsonDocument wc = Stj.JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
        {
            workingCopyId = wc.RootElement.GetProperty("id").GetString()!;
            workflowId = wc.RootElement.GetProperty("document").GetProperty("workflows")[0].GetProperty("workflowId").GetString()!;
        }

        try
        {
            // 3. Start a debug run paused after each step. The enqueue response is UN-ADVANCED (R5):
            //    the control plane marks the run claimable; only the runner process may advance it.
            using HttpResponseMessage started = await http.PostAsJsonAsync(
                $"/arazzo/v1/workspace/workflows/{workingCopyId}/debug-runs",
                new
                {
                    workflowId,
                    environment = "development",
                    inputs = new { fullName = "AppHost E2E", email = "e2e@example.com", plan = "free" },
                    pause = new { afterEachStep = true },
                });
            started.StatusCode.ShouldBe(HttpStatusCode.Created);
            string debugRunId;
            using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await started.Content.ReadAsStringAsync()))
            {
                debugRunId = run.RootElement.GetProperty("debugRunId").GetString()!;
                run.RootElement.GetProperty("status").GetString().ShouldBe("running", "the enqueue response is un-advanced — a runner advances it out-of-band");
            }

            // 4. Pump get-debug-run exactly as the dock does. The pause lands because the REAL
            //    runner claimed the run, compiled the draft, and executed step 1 against the real
            //    onboarding service with its Vault-resolved credential.
            Stj.JsonElement paused = await PollAsync(
                http, $"/arazzo/v1/workspace/workflows/{workingCopyId}/debug-runs/{debugRunId}",
                doc => doc.GetProperty("status").GetString() is "paused" or "faulted",
                "the separate runner process advances the run to its first pause");
            paused.GetProperty("status").GetString().ShouldBe("paused");
            paused.GetProperty("trace").GetProperty("steps")[0].GetProperty("stepId").GetString().ShouldBe("createAccount");

            // 5. A bare resume releases the single-step pause; the runner takes the run to completion.
            using HttpResponseMessage resumed = await http.PostAsJsonAsync(
                $"/arazzo/v1/workspace/workflows/{workingCopyId}/debug-runs/{debugRunId}/resume", new { });
            resumed.StatusCode.ShouldBe(HttpStatusCode.OK);
            Stj.JsonElement completed = await PollAsync(
                http, $"/arazzo/v1/workspace/workflows/{workingCopyId}/debug-runs/{debugRunId}",
                doc => doc.GetProperty("status").GetString() is "completed" or "faulted" or "suspended",
                "the runner advances the resumed run to its terminal state");
            completed.GetProperty("status").GetString().ShouldBe("completed");
            completed.GetProperty("trace").GetProperty("steps").GetArrayLength().ShouldBe(4, "all four onboarding steps executed for real");
        }
        finally
        {
            using HttpResponseMessage cleanup = await http.DeleteAsync($"/arazzo/v1/workspace/workflows/{workingCopyId}");
            _ = cleanup; // the composition is ephemeral, but leave the workspace as we found it anyway
        }
    }

    [TestMethod]
    [Timeout(600_000)]
    public async Task A_sealed_start_of_a_production_run_is_opened_and_advanced_by_the_production_runner()
    {
        if (Environment.GetEnvironmentVariable("ARAZZO_APPHOST_E2E") != "1")
        {
            Assert.Inconclusive("Set ARAZZO_APPHOST_E2E=1 (and have a container runtime) to run the full two-process composition e2e.");
        }

        IDistributedApplicationTestingBuilder appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.Corvus_Text_Json_Arazzo_ControlPlane_Demo_AppHost>();
        await using DistributedApplication app = await appHost.BuildAsync();
        await app.StartAsync();
        ResourceNotificationService notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications.WaitForResourceHealthyAsync("controlplane", default).WaitAsync(StartupTimeout);
        await notifications.WaitForResourceHealthyAsync("runner-production", default).WaitAsync(StartupTimeout);
        using HttpClient http = app.CreateHttpClient("controlplane");
        http.DefaultRequestHeaders.Add("X-Api-Key", "demo-admin-key");

        // The initiator's handoff the AppHost wrote: the tenant operator's initiator key and the seal key's pinned
        // fingerprint, exactly what `arazzo-runs start --sealed` takes (ADR 0065 decision 9).
        string handoff = Environment.GetEnvironmentVariable("ARAZZO_INITIATOR_HANDOFF_DIR")!;
        using var initiator = System.Security.Cryptography.ECDsa.Create();
        initiator.ImportFromPem(await File.ReadAllTextAsync(Path.Combine(handoff, "production-initiator.key.pem")));
        string pinnedFingerprint = (await File.ReadAllTextAsync(Path.Combine(handoff, "production-seal-key.fingerprint"))).Trim();

        // The published seal key, pinned by fingerprint before anything is sealed to it.
        Stj.JsonElement keys = await PollAsync(http, "/arazzo/v1/environments/production/keys?state=Active", doc => doc.GetProperty("keys").GetArrayLength() > 0, "production's seal key generation is registered");
        Stj.JsonElement generation = keys.GetProperty("keys")[0];
        byte[] sealSpki = generation.GetProperty("sealPublicKey").GetBytesFromBase64();
        RunStartInitiator.SealKeyFingerprint(sealSpki).ShouldBe(pinnedFingerprint, "the key the control plane publishes is the one the AppHost provisioned");

        // Seal the onboarding inputs to it, sign as the initiator, and post the seal: the control plane never sees them.
        string runId = RunStartInitiator.NewRunId();
        SealedInputs sealedInputs = RunStartInitiator.Seal(sealSpki, generation.GetProperty("keyId").GetString()!, "production", "onboard-customer", 2, runId, """{"email":"sealed@example.com","fullName":"Sealed Start","plan":"pro"}"""u8, initiator);
        using var body = new ByteArrayContent(SealedRunStart.Serialize(runId, sealedInputs));
        body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        using HttpResponseMessage accepted = await http.PostAsync("/arazzo/v1/catalog/onboard-customer/versions/2/runs/sealed?environment=production", body);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted, await accepted.Content.ReadAsStringAsync());

        // runner-production opens the seal, validates the inputs and runs the onboarding to completion; the run says
        // it started sealed.
        Stj.JsonElement done = await PollAsync(http, $"/arazzo/v1/runs/{runId}", doc => doc.GetProperty("status").GetString() is "Completed" or "Faulted", "the production runner opens the sealed start and advances the run");
        done.GetProperty("status").GetString().ShouldBe("Completed");
        done.GetProperty("sealedStart").GetBoolean().ShouldBeTrue();
    }

    private static async Task<Stj.JsonElement> PollAsync(HttpClient http, string path, Func<Stj.JsonElement, bool> settled, string what)
    {
        Stj.JsonElement last = default;
        for (int i = 0; i < 300; i++)
        {
            using HttpResponseMessage response = await http.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using Stj.JsonDocument doc = Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                last = doc.RootElement.Clone();
                if (settled(last))
                {
                    return last;
                }
            }

            await Task.Delay(200);
        }

        Assert.Fail($"Timed out waiting for: {what}. Last: {last}");
        return default;
    }
}
