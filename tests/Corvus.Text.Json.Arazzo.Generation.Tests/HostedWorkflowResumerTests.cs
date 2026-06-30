// <copyright file="HostedWorkflowResumerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.Arazzo.Generation;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Generation.Tests;

/// <summary>
/// Proves the <see cref="HostedWorkflowResumer"/> ties the pieces together: it resolves a run's workflow id to
/// a runnable catalog version, loads + verifies its compiled executor through the loader, and drives the run to
/// completion as the <see cref="WorkflowResumer"/> the durable worker expects — all against a real
/// provider-built, catalogued executor.
/// </summary>
[TestClass]
public class HostedWorkflowResumerTests
{
    // Base workflow id "adopt" — the catalog rewrites it to "adopt-v1" and bakes the executor.
    private const string WorkflowJson = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "Adopt", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adopt",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    private const string PetstoreOpenApi = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Pets", "version": "1.0.0" },
          "paths": {
            "/pets/{petId}": {
              "get": {
                "operationId": "getPet",
                "parameters": [ { "name": "petId", "in": "path", "required": true, "schema": { "type": "string" } } ],
                "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "object", "properties": { "name": { "type": "string" } } } } } } }
              }
            }
          }
        }
        """;

    [TestMethod]
    public async Task Resolves_a_runnable_catalog_version_loads_it_and_runs_the_run_to_completion()
    {
        var catalog = new InMemoryWorkflowCatalogStore(executorProvider: new WorkflowExecutorProvider());
        using ParsedJsonDocument<CatalogVersion> versionDoc = await catalog.AddAsync("adopt", Package(), Meta(), default);
        CatalogVersion version = versionDoc.RootElement;
        version.Ref.WorkflowId.ShouldBe("adopt-v1");
        ((bool)version.Runnable).ShouldBeTrue();

        var runStore = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        using WorkflowRun run = WorkflowRun.CreateNew(runStore, "run-1", version.Ref.WorkflowId, inputs.RootElement);

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        using var loader = new WorkflowExecutorLoader();
        var resumer = new HostedWorkflowResumer(catalog, loader, (d, _tags) => new WorkflowTransports(d.Sources.ToDictionary(s => s, _ => (IApiTransport)transport, System.StringComparer.Ordinal), null));

        // Drive it through the WorkflowResumer delegate the durable worker would call.
        WorkflowResumer resume = resumer.AsResumer();
        WorkflowRunResultKind kind = await resume(run, default);

        kind.ShouldBe(WorkflowRunResultKind.Completed);
        transport.Requests[0].Path.ShouldBe("/pets/42");

        // The version is now cached in the loader for subsequent runs.
        loader.TryGet("adopt", 1, out _).ShouldBeTrue();
    }

    [TestMethod]
    public async Task An_expired_source_credential_faults_the_run_as_credentials_expired()
    {
        var catalog = new InMemoryWorkflowCatalogStore(executorProvider: new WorkflowExecutorProvider());
        using ParsedJsonDocument<CatalogVersion> versionDoc = await catalog.AddAsync("adopt", Package(), Meta(), default);
        CatalogVersion version = versionDoc.RootElement;

        var runStore = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        using WorkflowRun run = WorkflowRun.CreateNew(runStore, "run-1", version.Ref.WorkflowId, inputs.RootElement);

        // The source's transport raises a credential-expired condition at bind time (as the runner cache would for an
        // expired binding). The durable executor must catch it and fault the run as a typed, resumable credentials
        // fault — not propagate an opaque exception.
        var transport = new ThrowingApiTransport(new SourceCredentialExpiredException("petstore"));

        using var loader = new WorkflowExecutorLoader();
        var resumer = new HostedWorkflowResumer(catalog, loader, (d, _tags) => new WorkflowTransports(d.Sources.ToDictionary(s => s, _ => (IApiTransport)transport, System.StringComparer.Ordinal), null));

        WorkflowResumer resume = resumer.AsResumer();
        WorkflowRunResultKind kind = await resume(run, default);

        kind.ShouldBe(WorkflowRunResultKind.Faulted);
        run.Status.ShouldBe(WorkflowRunStatus.Faulted);
        run.Fault.ShouldNotBeNull();
        run.Fault!.Value.Error.ShouldBe(SourceCredentialExpiredException.ErrorType);
        run.Fault!.Value.StepId.ShouldBe("petstore");
    }

    [TestMethod]
    public async Task Throws_when_the_version_is_not_runnable()
    {
        // No executor provider → the catalogued version carries no executor.
        var catalog = new InMemoryWorkflowCatalogStore();
        using (await catalog.AddAsync("adopt", Package(), Meta(), default))
        {
        }

        var runStore = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        using WorkflowRun run = WorkflowRun.CreateNew(runStore, "run-1", "adopt-v1", inputs.RootElement);

        using var loader = new WorkflowExecutorLoader();
        var resumer = new HostedWorkflowResumer(catalog, loader, (d, _tags) => new WorkflowTransports(d.Sources.ToDictionary(s => s, _ => (IApiTransport)new MockApiTransport(), System.StringComparer.Ordinal), null));

        await Should.ThrowAsync<InvalidOperationException>(async () => await resumer.ResumeAsync(run, default));
    }

    // A workflow whose step projects a NESTED (object + array) response-body value as its output — unlike the
    // scalar-string outputs the other durable tests use. The durable executor clones the projected value into the
    // run workspace, disposes the response, then a checkpoint serialises the staged outputs; this exercises that a
    // nested staged output survives the response's disposal (the lifetime live multi-step execution depends on).
    private const string NestedOutputWorkflowJson = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "Adopt", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adopt",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "profile": "$response.body#/profile" }
                }
              ],
              "outputs": { "profile": "$steps.getPet.outputs.profile" }
            }
          ]
        }
        """;

    private const string NestedOutputPetstoreOpenApi = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Pets", "version": "1.0.0" },
          "paths": {
            "/pets/{petId}": {
              "get": {
                "operationId": "getPet",
                "parameters": [ { "name": "petId", "in": "path", "required": true, "schema": { "type": "string" } } ],
                "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "object", "properties": { "name": { "type": "string" }, "profile": { "type": "object", "properties": { "breed": { "type": "string" }, "tags": { "type": "array", "items": { "type": "string" } } } } } } } } } }
              }
            }
          }
        }
        """;

    [TestMethod]
    public async Task Runs_a_workflow_whose_step_projects_a_nested_response_body_output_to_completion()
    {
        var catalog = new InMemoryWorkflowCatalogStore(executorProvider: new WorkflowExecutorProvider());
        byte[] package = WorkflowPackage.Pack(
            Encoding.UTF8.GetBytes(NestedOutputWorkflowJson),
            [new("petstore", Encoding.UTF8.GetBytes(NestedOutputPetstoreOpenApi))]);
        using ParsedJsonDocument<CatalogVersion> versionDoc = await catalog.AddAsync("adopt", package, Meta(), default);
        CatalogVersion version = versionDoc.RootElement;
        ((bool)version.Runnable).ShouldBeTrue();

        var runStore = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        using WorkflowRun run = WorkflowRun.CreateNew(runStore, "run-1", version.Ref.WorkflowId, inputs.RootElement);

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido","profile":{"breed":"Labrador","tags":["good-boy","house-trained"]}}""");

        using var loader = new WorkflowExecutorLoader();
        var resumer = new HostedWorkflowResumer(catalog, loader, (d, _tags) => new WorkflowTransports(d.Sources.ToDictionary(s => s, _ => (IApiTransport)transport, System.StringComparer.Ordinal), null));

        WorkflowRunResultKind kind = await resumer.AsResumer()(run, default);

        kind.ShouldBe(WorkflowRunResultKind.Completed);

        // The nested output survived the response's disposal and was checkpointed, so it is durably readable.
        using WorkflowRun? completed = await WorkflowRun.ResumeAsync(runStore, "run-1");
        completed.ShouldNotBeNull();
        completed!.TryGetStepOutputs("getPet", out JsonElement getPetOutputs).ShouldBeTrue();
        getPetOutputs.TryGetProperty("profile"u8, out JsonElement profile).ShouldBeTrue();
        profile.TryGetProperty("breed"u8, out JsonElement breed).ShouldBeTrue();
        breed.GetString().ShouldBe("Labrador");
    }

    // A two-step workflow mirroring the onboard-customer shape that live execution faulted on: step 2 binds
    // step 1's scalar output as a path parameter, sends a request body, and projects NESTED outputs. The
    // checkpoint after step 2 serialises BOTH steps' staged outputs — step 1's (scalar, cloned earlier) and
    // step 2's (nested) — which is where live execution threw ObjectDisposedException.
    private const string TwoStepWorkflowJson = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "Onboard", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "api", "url": "./api.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "onboard",
              "inputs": { "type": "object", "properties": { "email": { "type": "string" } } },
              "steps": [
                {
                  "stepId": "createAccount",
                  "operationId": "createAccount",
                  "outputs": { "accountId": "$response.body#/accountId" }
                },
                {
                  "stepId": "verifyIdentity",
                  "operationId": "verifyIdentity",
                  "parameters": [ { "name": "accountId", "in": "path", "value": "$steps.createAccount.outputs.accountId" } ],
                  "requestBody": { "contentType": "application/json", "payload": { "fullName": "Ada Lovelace" } },
                  "outputs": { "score": "$response.body#/score", "applicant": "$response.body#/applicant" }
                }
              ]
            }
          ]
        }
        """;

    private const string TwoStepOpenApi = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Api", "version": "1.0.0" },
          "paths": {
            "/accounts": {
              "post": {
                "operationId": "createAccount",
                "responses": { "201": { "description": "created", "content": { "application/json": { "schema": { "type": "object", "properties": { "accountId": { "type": "string" } } } } } } }
              }
            },
            "/accounts/{accountId}/identity": {
              "post": {
                "operationId": "verifyIdentity",
                "parameters": [ { "name": "accountId", "in": "path", "required": true, "schema": { "type": "string" } } ],
                "requestBody": { "content": { "application/json": { "schema": { "type": "object", "properties": { "fullName": { "type": "string" } } } } } },
                "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "object", "properties": { "score": { "type": "number" }, "applicant": { "type": "object", "properties": { "fullName": { "type": "string" }, "country": { "type": "string" } } } } } } } } }
              }
            }
          }
        }
        """;

    [TestMethod]
    public async Task Runs_a_two_step_workflow_with_cross_step_param_request_body_and_nested_outputs_to_completion()
    {
        var catalog = new InMemoryWorkflowCatalogStore(executorProvider: new WorkflowExecutorProvider());
        byte[] package = WorkflowPackage.Pack(
            Encoding.UTF8.GetBytes(TwoStepWorkflowJson),
            [new("api", Encoding.UTF8.GetBytes(TwoStepOpenApi))]);
        using ParsedJsonDocument<CatalogVersion> versionDoc = await catalog.AddAsync("onboard", package, Meta(), default);
        CatalogVersion version = versionDoc.RootElement;
        ((bool)version.Runnable).ShouldBeTrue();

        var runStore = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"email":"ada@example.com"}"""));
        using WorkflowRun run = WorkflowRun.CreateNew(runStore, "run-1", version.Ref.WorkflowId, inputs.RootElement);

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Post, "/accounts", 201, """{"accountId":"acc-42"}""");
        transport.SetResponse(OperationMethod.Post, "/accounts/{accountId}/identity", 200, """{"score":0.92,"applicant":{"fullName":"Ada Lovelace","country":"GB"}}""");

        using var loader = new WorkflowExecutorLoader();
        var resumer = new HostedWorkflowResumer(catalog, loader, (d, _tags) => new WorkflowTransports(d.Sources.ToDictionary(s => s, _ => (IApiTransport)transport, System.StringComparer.Ordinal), null));

        WorkflowRunResultKind kind = await resumer.AsResumer()(run, default);

        kind.ShouldBe(WorkflowRunResultKind.Completed);
        transport.Requests[1].Path.ShouldBe("/accounts/acc-42/identity");

        using WorkflowRun? completed = await WorkflowRun.ResumeAsync(runStore, "run-1");
        completed.ShouldNotBeNull();
        completed!.TryGetStepOutputs("verifyIdentity", out JsonElement verifyOutputs).ShouldBeTrue();
        verifyOutputs.TryGetProperty("applicant"u8, out JsonElement applicant).ShouldBeTrue();
        applicant.TryGetProperty("country"u8, out JsonElement country).ShouldBeTrue();
        country.GetString().ShouldBe("GB");
    }

    private static CatalogMetadata Meta() => new(new CatalogOwner("Team", "team@example.com"), "alice");

    private static byte[] Package()
        => WorkflowPackage.Pack(
            Encoding.UTF8.GetBytes(WorkflowJson),
            [new("petstore", Encoding.UTF8.GetBytes(PetstoreOpenApi))]);

    // A transport that fails every call with a fixed exception — used to drive the executor's bind-time fault path.
    private sealed class ThrowingApiTransport(Exception exception) : IApiTransport
    {
        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => throw exception;

        public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(in TRequest request, in TBody body, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TBody : struct, Corvus.Text.Json.Internal.IJsonElement<TBody>
            where TResponse : struct, IApiResponse<TResponse>
            => throw exception;

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, System.IO.Stream body, string contentType, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => throw exception;

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Func<System.IO.Stream, CancellationToken, ValueTask> bodyWriter, string contentType, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => throw exception;

        public ValueTask DisposeAsync() => default;
    }
}
