// <copyright file="WorkspaceWorkflowStoreBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the workspace-workflow (working copy) store seams (workflow-designer design §4.1) end-to-end over the
/// in-memory reference store (no driver / no I/O noise) — the per-request work a designer/handler does between "the
/// framework parsed the body" and "the store holds the bytes". A working copy carries the WHOLE Arazzo document (and,
/// via attach, its sources), so the read seam returns a materially larger payload than most control-plane rows; these
/// record the bytes-to-bytes seam's allocation as the baseline-to-beat for the backend fan-out.
///
/// <para><b>Ownership ledger (#803, the bytes-to-bytes discipline these prove).</b></para>
/// <list type="bullet">
/// <item><b>Create.</b> The body's already-parsed JSON values (name, document, designer state) are carried
/// bytes-to-bytes into a draft <see cref="WorkspaceWorkflow"/> (no per-field strings, the document written verbatim via
/// <c>WriteRawValue</c>); the store completes it with the server-stamped id/createdBy/createdAt/etag in one pooled pass.
/// The caller owns and disposes the returned pooled document, exactly as the handler's <c>using</c> does.</item>
/// <item><b>Get.</b> The store hands back one pooled <see cref="WorkspaceWorkflow"/> whose document is the stored bytes
/// (no re-encode); the caller owns and disposes it. No per-field materialisation.</item>
/// <item><b>Update.</b> A save reads the stored row, carries the immutable provenance/tags/audit forward bytes-to-bytes,
/// takes the draft's new document/name bytes-to-bytes, and stamps a fresh etag in one pooled pass under the expected
/// etag; the caller owns and disposes the returned document.</item>
/// <item><b>List.</b> One reach-filtered keyset page: each row's summary is projected minus its document; the page owns
/// its rows and the caller disposes the page.</item>
/// </list>
/// The management <see cref="SecurityTagSet"/> the handler resolves from the caller's <c>AccessContext</c> is precomputed
/// in setup so the measured region is the persistence seam, not the access-tag resolution.
/// </summary>
[MemoryDiagnoser]
public class WorkspaceWorkflowStoreBenchmarks
{
    private const string Actor = "bench";
    private const int SeededCount = 50;
    private const int PageSize = 20;

    // A representative working-copy document, already parsed (the HTTP framework parsed the create body before the
    // handler runs), so the benchmark measures the seam, not the parse. Two source descriptions, a typed-inputs
    // workflow, and a few operation-bound steps with success criteria — the shape the designer actually saves.
    private static readonly byte[] DocumentJson =
        """
        {
          "arazzo": "1.1.0",
          "info": { "title": "Order processing", "version": "1.0.0" },
          "sourceDescriptions": [
            { "name": "payments", "url": "./payments.openapi.json", "type": "openapi" },
            { "name": "orders", "url": "./orders.openapi.json", "type": "openapi" }
          ],
          "workflows": [
            {
              "workflowId": "place-order",
              "inputs": { "type": "object", "properties": { "orderId": { "type": "string" }, "amount": { "type": "number" } } },
              "steps": [
                { "stepId": "validate", "operationId": "validateOrder", "successCriteria": [ { "condition": "$statusCode == 200" } ], "outputs": { "validated": "$response.body#/validated" } },
                { "stepId": "authorize", "operationId": "authorizePayment", "successCriteria": [ { "condition": "$statusCode == 201" } ], "outputs": { "authorizationId": "$response.body#/authorizationId" } },
                { "stepId": "capture", "operationId": "capturePayment", "successCriteria": [ { "condition": "$statusCode == 200" } ], "outputs": { "receiptId": "$response.body#/receiptId" } }
              ]
            }
          ]
        }
        """u8.ToArray();

    private SecurityTagSet managementTags;
    private InMemoryWorkspaceWorkflowStore readStore = null!;
    private string readId = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Resolved by the handler from the caller's AccessContext (the internal tenant tag); precomputed here so the
        // measured region is the persistence seam, not the access-tag resolution.
        this.managementTags = SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "contoso")]);

        // A pre-seeded store for the read/list benchmarks (created once; Get/List do not mutate).
        this.readStore = new InMemoryWorkspaceWorkflowStore();
        for (int i = 0; i < SeededCount; i++)
        {
            using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceWorkflow.Draft($"copy-{i:D3}", DocumentJson, default, null, null, this.managementTags);
            using ParsedJsonDocument<WorkspaceWorkflow> added = this.readStore.AddAsync(draft.RootElement, Actor, default).AsTask().GetAwaiter().GetResult();
            if (i == 0)
            {
                this.readId = added.RootElement.IdValue;
            }
        }
    }

    /// <summary>The create seam: carry the body's already-parsed name + document bytes-to-bytes into a draft working copy
    /// (the document written verbatim), then persist it (the store mints the id and stamps the server fields).</summary>
    [Benchmark]
    public void Create_FromDraft()
    {
        var store = new InMemoryWorkspaceWorkflowStore();
        using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceWorkflow.Draft("Order processing", DocumentJson, default, null, null, this.managementTags);
        using ParsedJsonDocument<WorkspaceWorkflow> created = store.AddAsync(draft.RootElement, Actor, default).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>The read seam: fetch one working copy by id — the whole document comes back on a single read (dispose the
    /// pooled document as the handler's <c>using</c> does).</summary>
    [Benchmark]
    public void Get_ById()
    {
        using ParsedJsonDocument<WorkspaceWorkflow>? fetched = this.readStore.GetAsync(this.readId, AccessContext.System, default).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>The save seam: read-modify-write the seeded working copy unconditionally (the etag check is a cheap
    /// comparison; the allocation is the pooled carry-forward of the immutable fields plus the new document bytes).</summary>
    [Benchmark]
    public void Update_ReadModifyWrite()
    {
        using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceWorkflow.Draft("Order processing", DocumentJson, default, null, null, default);
        using ParsedJsonDocument<WorkspaceWorkflow>? saved = this.readStore.UpdateAsync(this.readId, draft.RootElement, WorkflowEtag.None, Actor, AccessContext.System, default).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>The list seam: one reach-filtered keyset page over the pre-seeded store (each row minus its document;
    /// dispose the page as a handler does).</summary>
    [Benchmark]
    public void List_Page()
    {
        using WorkspaceWorkflowPage page = this.readStore.ListAsync(AccessContext.System, PageSize, default, default).AsTask().GetAwaiter().GetResult();
    }
}