// <copyright file="WorkspaceApiBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the designer workspace request path (workflow-designer design §4.1) end-to-end at the handler boundary —
/// the per-request work between "ASP.NET parsed the request" and "the result body is materialised", over the in-memory
/// reference store (no Kestrel / no sockets / no I/O noise) with a per-request <see cref="JsonWorkspace"/> disposed each
/// op. The allocation-campaign baselines for the workspace seams (ledger rows A–F):
/// <list type="bullet">
/// <item><see cref="List_Page"/> — GET /workspace/workflows: a 20-item summary page (row A evidence).</item>
/// <item><see cref="Create"/> — POST /workspace/workflows: document-supplied create (row B; fresh store per op).</item>
/// <item><see cref="Save"/> — PUT /workspace/workflows/{id}: the etag-guarded save round-trip (row A; fresh store per op so the seeded etag stays fresh).</item>
/// <item><see cref="Attach_Inline"/> — PUT …/sources/{name}: the attach read-modify-write (row D; idempotent replace, shared store).</item>
/// <item><see cref="Operations_Surface"/> — GET …/sources/{name}/operations: the operation-surface projection (row F).</item>
/// <item><see cref="Validate"/> — POST …/validate: the schema + semantic diagnostics passes over a clean document (row C).</item>
/// </list>
/// </summary>
[MemoryDiagnoser]
public class WorkspaceApiBenchmarks
{
    private const string Actor = "bench";
    private const int Seeded = 20;

    // A representative Arazzo document (already parsed by ASP.NET when the handler runs).
    private static readonly byte[] ArazzoDocJson =
        """
        {
          "arazzo": "1.1.0",
          "info": { "title": "Nightly reconcile", "version": "1.0.0" },
          "sourceDescriptions": [{ "name": "pets", "url": "./pets.json", "type": "openapi" }],
          "workflows": [{
            "workflowId": "nightly-reconcile",
            "steps": [
              { "stepId": "fetch", "operationId": "listPets", "successCriteria": [{ "condition": "$statusCode == 200" }], "outputs": { "pets": "$response.body" } },
              { "stepId": "reconcile", "operationId": "updatePet", "onFailure": [{ "name": "retry-it", "type": "retry", "stepId": "reconcile", "retryAfter": 5, "retryLimit": 2 }] }
            ],
            "outputs": { "all": "$steps.fetch.outputs.pets" }
          }]
        }
        """u8.ToArray();

    // A representative OpenAPI source (a few operations, $ref-composed schemas) for attach + surface projection.
    private static readonly byte[] PetstoreJson =
        """
        {
          "openapi": "3.1.0",
          "info": { "title": "Petstore", "version": "1.0" },
          "components": {
            "schemas": {
              "Pet": { "type": "object", "properties": { "id": { "type": "string" }, "name": { "type": "string" }, "tag": { "$ref": "#/components/schemas/Tag" } } },
              "Tag": { "type": "object", "properties": { "key": { "type": "string" }, "value": { "type": "string" } } }
            }
          },
          "paths": {
            "/pets": {
              "get": {
                "operationId": "listPets",
                "summary": "List pets",
                "parameters": [{ "name": "limit", "in": "query", "schema": { "type": "integer" } }],
                "responses": {
                  "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "array", "items": { "$ref": "#/components/schemas/Pet" } } } } },
                  "default": { "description": "unexpected" }
                }
              },
              "post": {
                "operationId": "createPet",
                "requestBody": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Pet" } } } },
                "responses": { "201": { "description": "created", "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Pet" } } } } }
              }
            },
            "/pets/{petId}": {
              "parameters": [{ "name": "petId", "in": "path", "required": true, "schema": { "type": "string" } }],
              "put": {
                "operationId": "updatePet",
                "requestBody": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Pet" } } } },
                "responses": { "200": { "description": "ok" }, "404": { "description": "missing" } }
              }
            }
          }
        }
        """u8.ToArray();

    private static readonly byte[] CreateBodyJson = BuildCreateBody();
    private static readonly byte[] SaveBodyJson = BuildSaveBody();
    private static readonly byte[] AttachBodyJson = BuildAttachBody();

    private ArazzoControlPlaneWorkspaceHandler sharedHandler = null!;
    private ArazzoControlPlaneSourcesHandler fetchHandler = null!;
    private ParsedJsonDocument<Models.FetchSourceRequest> fetchBody = null!;
    private string sharedId = null!;
    private ParsedJsonDocument<Models.WorkingCopyCreate> createBody = null!;
    private ParsedJsonDocument<Models.WorkingCopyCreate> blankCreateBody = null!;
    private ParsedJsonDocument<Models.WorkingCopyUpdate> saveBody = null!;
    private ParsedJsonDocument<Models.AttachSourceRequest> attachBody = null!;

    [GlobalSetup]
    public void Setup()
    {
        // The shared store: 20 seeded working copies; one carries an inline petstore attachment for
        // the read-only surface/validate benchmarks and the idempotent attach-replace.
        var store = new InMemoryWorkspaceWorkflowStore();
        for (int i = 0; i < Seeded; i++)
        {
            using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceWorkflow.Draft($"wc {i:D3}", ArazzoDocJson, default, null, null, default);
            using ParsedJsonDocument<WorkspaceWorkflow> added = store.AddAsync(draft.RootElement, Actor, default).AsTask().GetAwaiter().GetResult();
            if (i == 0)
            {
                this.sharedId = added.RootElement.IdValue;
            }
        }

        this.sharedHandler = new ArazzoControlPlaneWorkspaceHandler(store, actor: Actor);
        this.createBody = ParsedJsonDocument<Models.WorkingCopyCreate>.Parse(CreateBodyJson);
        this.blankCreateBody = ParsedJsonDocument<Models.WorkingCopyCreate>.Parse("{}"u8.ToArray());
        this.saveBody = ParsedJsonDocument<Models.WorkingCopyUpdate>.Parse(SaveBodyJson);
        this.attachBody = ParsedJsonDocument<Models.AttachSourceRequest>.Parse(AttachBodyJson);

        // The fetch benchmark: a stub outbound endpoint serving the petstore, measuring OUR seam only.
        this.fetchHandler = CreateFetchHandler(new SourceDocumentFetcher(new HttpClient(new StubSpecHandler())));
        this.fetchBody = ParsedJsonDocument<Models.FetchSourceRequest>.Parse("{\"url\":\"https://specs.example/petstore.json\"}"u8.ToArray());

        // Seed the shared working copy's attachment once (the attach benchmark then replaces it each op).
        this.Attach_Inline().GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.fetchBody.Dispose();
        this.createBody.Dispose();
        this.blankCreateBody.Dispose();
        this.saveBody.Dispose();
        this.attachBody.Dispose();
    }

    /// <summary>GET /workspace/workflows → a 20-item summary page: the keyset store read, the field-selected
    /// summaries, and the result body materialised into a per-request workspace.</summary>
    [Benchmark]
    public async Task List_Page()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        ListWorkspaceWorkflowsResult result = await this.sharedHandler.HandleListWorkspaceWorkflowsAsync(default, workspace, default);
        _ = result.StatusCode;
    }

    /// <summary>POST /workspace/workflows → a document-supplied create: name derivation, the draft carried
    /// bytes-to-bytes, the persisted record, and the congruent whole-document response.</summary>
    [Benchmark]
    public async Task Create()
    {
        var store = new InMemoryWorkspaceWorkflowStore();
        var handler = new ArazzoControlPlaneWorkspaceHandler(store, actor: Actor);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        var parameters = new CreateWorkspaceWorkflowParams { Body = this.createBody.RootElement };
        CreateWorkspaceWorkflowResult result = await handler.HandleCreateWorkspaceWorkflowAsync(parameters, workspace, default);
        _ = result.StatusCode;
    }

    /// <summary>POST /workspace/workflows with an empty body → the blank create: the skeleton document
    /// and the derived 'untitled' name (row B's paths — the document-supplied create never hits them).</summary>
    [Benchmark]
    public async Task Create_Blank()
    {
        var store = new InMemoryWorkspaceWorkflowStore();
        var handler = new ArazzoControlPlaneWorkspaceHandler(store, actor: Actor);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        var parameters = new CreateWorkspaceWorkflowParams { Body = this.blankCreateBody.RootElement };
        CreateWorkspaceWorkflowResult result = await handler.HandleCreateWorkspaceWorkflowAsync(parameters, workspace, default);
        _ = result.StatusCode;
    }

    /// <summary>PUT /workspace/workflows/{id} → the etag-guarded save round-trip: the draft carried
    /// bytes-to-bytes over the stored record, re-persisted, and the congruent response. A fresh single-seed
    /// store per op keeps the constant expectedEtag fresh (the InMemory seed etag is deterministic).</summary>
    [Benchmark]
    public async Task Save()
    {
        var store = new InMemoryWorkspaceWorkflowStore();
        string id;
        using (ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceWorkflow.Draft("wc", ArazzoDocJson, default, null, null, default))
        using (ParsedJsonDocument<WorkspaceWorkflow> added = await store.AddAsync(draft.RootElement, Actor, default))
        {
            id = added.RootElement.IdValue;
        }

        var handler = new ArazzoControlPlaneWorkspaceHandler(store, actor: Actor);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<Models.JsonString> idValue = ParseString(id);
        var parameters = new UpdateWorkspaceWorkflowParams { Id = idValue.RootElement, Body = this.saveBody.RootElement };
        UpdateWorkspaceWorkflowResult result = await handler.HandleUpdateWorkspaceWorkflowAsync(parameters, workspace, default);
        _ = result.StatusCode;
    }

    /// <summary>PUT /workspace/workflows/{id}/sources/{name} → the attach read-modify-write: the working copy
    /// re-read, the replacement attachment set written, saved under the read etag, and the attachment response
    /// (an idempotent replace of the same name each op, over the shared store).</summary>
    [Benchmark]
    public async Task<int> Attach_Inline()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<Models.JsonString> idValue = ParseString(this.sharedId);
        using ParsedJsonDocument<Models.JsonString> nameValue = ParseString("pets");
        var parameters = new AttachWorkingCopySourceParams { Id = idValue.RootElement, Name = nameValue.RootElement, Body = this.attachBody.RootElement };
        AttachWorkingCopySourceResult result = await this.sharedHandler.HandleAttachWorkingCopySourceAsync(parameters, workspace, default);
        return result.StatusCode;
    }

    /// <summary>GET /workspace/workflows/{id}/sources/{name}/operations → the operation-surface projection over
    /// the inline petstore attachment (3 operations, $ref-composed schemas).</summary>
    [Benchmark]
    public async Task Operations_Surface()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<Models.JsonString> idValue = ParseString(this.sharedId);
        using ParsedJsonDocument<Models.JsonString> nameValue = ParseString("pets");
        var parameters = new ListWorkingCopySourceOperationsParams { Id = idValue.RootElement, Name = nameValue.RootElement };
        ListWorkingCopySourceOperationsResult result = await this.sharedHandler.HandleListWorkingCopySourceOperationsAsync(parameters, workspace, default);
        _ = result.StatusCode;
    }

    /// <summary>POST /sources/fetch → the server-side fetch seam over a stub endpoint: download into
    /// the pooled buffer, parse, detect, canonical digest, and the pooled response envelope.</summary>
    [Benchmark]
    public async Task Fetch_Json()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        var parameters = new FetchSourceDocumentParams { Body = this.fetchBody.RootElement };
        FetchSourceDocumentResult result = await this.fetchHandler.HandleFetchSourceDocumentAsync(parameters, workspace, default);
        _ = result.StatusCode;
    }

    /// <summary>POST /workspace/workflows/{id}/validate → both diagnostic passes over the stored (clean)
    /// document: the embedded-schema conformance validation plus the semantic analyzer walk.</summary>
    [Benchmark]
    public async Task Validate()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<Models.JsonString> idValue = ParseString(this.sharedId);
        var parameters = new ValidateWorkspaceWorkflowParams { Id = idValue.RootElement };
        ValidateWorkspaceWorkflowResult result = await this.sharedHandler.HandleValidateWorkspaceWorkflowAsync(parameters, workspace, default);
        _ = result.StatusCode;
    }

    private static ParsedJsonDocument<Models.JsonString> ParseString(string value)
        => ParsedJsonDocument<Models.JsonString>.Parse(System.Text.Encoding.UTF8.GetBytes($"\"{value}\""));

    private static ArazzoControlPlaneSourcesHandler CreateFetchHandler(SourceDocumentFetcher fetcher)
        => new(new Corvus.Text.Json.Arazzo.Durability.Sources.InMemorySourceStore(), new ControlPlaneAccess(), fetcher, Actor);

    /// <summary>The stub outbound endpoint: every URL serves the petstore JSON.</summary>
    private sealed class StubSpecHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(PetstoreJson) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") } },
            });
    }

    // Request bodies assemble once at static init (unmeasured) — plain concatenation is fine here.
    private static byte[] BuildCreateBody()
        => System.Text.Encoding.UTF8.GetBytes("{\"name\":\"bench wc\",\"document\":" + System.Text.Encoding.UTF8.GetString(ArazzoDocJson) + "}");

    private static byte[] BuildSaveBody()
        => System.Text.Encoding.UTF8.GetBytes("{\"document\":" + System.Text.Encoding.UTF8.GetString(ArazzoDocJson) + ",\"expectedEtag\":\"1\"}");

    private static byte[] BuildAttachBody()
        => System.Text.Encoding.UTF8.GetBytes("{\"document\":" + System.Text.Encoding.UTF8.GetString(PetstoreJson) + "}");
}