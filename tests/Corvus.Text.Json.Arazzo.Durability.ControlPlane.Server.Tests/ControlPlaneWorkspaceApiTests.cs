// <copyright file="ControlPlaneWorkspaceApiTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo.Durability;
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
/// Tests the control-plane workspace API (workflow-designer design §4.1): designer working copies over
/// <c>/workspace/workflows</c>, gated by the <c>workspace:read</c>/<c>workspace:write</c> scopes. A working copy is a
/// mutable Arazzo document saved as many times as needed during development without minting catalog versions; a save is
/// etag-guarded (<c>expectedEtag</c>) so a stale save conflicts rather than clobbering a collaborator's work.
/// </summary>
[TestClass]
public sealed class ControlPlaneWorkspaceApiTests
{
    private const string Write = "workspace:write";
    private const string Read = "workspace:read";

    private const string CreateBody =
        """{"name":"retry tuning","document":{"arazzo":"1.1.0","info":{"title":"Nightly reconcile"},"workflows":[{"workflowId":"nightly-reconcile","steps":[]}]},"designerState":{"nodes":{"step-1":{"x":40,"y":80}}}}""";

    [TestMethod]
    public async Task A_working_copy_has_a_full_create_get_list_save_delete_lifecycle()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());

        HttpResponseMessage created = await host.SendJsonAsync(HttpMethod.Post, "/workspace/workflows", CreateBody, Write);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        string id;
        string etag;
        using (Stj.JsonDocument doc = await ReadJsonAsync(created))
        {
            id = doc.RootElement.GetProperty("id").GetString()!;
            etag = doc.RootElement.GetProperty("etag").GetString()!;
            id.ShouldNotBeNullOrEmpty();
            etag.ShouldNotBeNullOrEmpty();
            doc.RootElement.GetProperty("name").GetString().ShouldBe("retry tuning");
            doc.RootElement.GetProperty("createdBy").GetString().ShouldNotBeNullOrEmpty();

            // The create response is the full working copy — the document and designer state round-trip verbatim.
            doc.RootElement.GetProperty("document").GetProperty("info").GetProperty("title").GetString().ShouldBe("Nightly reconcile");
            doc.RootElement.GetProperty("designerState").GetProperty("nodes").GetProperty("step-1").GetProperty("x").GetInt32().ShouldBe(40);
        }

        (await host.SendAsync(HttpMethod.Get, $"/workspace/workflows/{id}", Read)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The list entry is a summary: the document and designer state are omitted (the design's key projection decision).
        using (Stj.JsonDocument list = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/workspace/workflows", Read)))
        {
            Stj.JsonElement entry = list.RootElement.GetProperty("workingCopies").EnumerateArray().Single();
            entry.GetProperty("id").GetString().ShouldBe(id);
            entry.GetProperty("name").GetString().ShouldBe("retry tuning");
            entry.TryGetProperty("document", out _).ShouldBeFalse();
            entry.TryGetProperty("designerState", out _).ShouldBeFalse();
        }

        // A save replaces the document (and here the name), presenting the etag it read.
        HttpResponseMessage saved = await host.SendJsonAsync(
            HttpMethod.Put,
            $"/workspace/workflows/{id}",
            $$"""{"name":"retry tuning (v2)","document":{"arazzo":"1.1.0","info":{"title":"Nightly reconcile v2"},"workflows":[]},"expectedEtag":"{{etag}}"}""",
            Write);
        saved.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument doc = await ReadJsonAsync(saved))
        {
            doc.RootElement.GetProperty("name").GetString().ShouldBe("retry tuning (v2)");
            doc.RootElement.GetProperty("document").GetProperty("info").GetProperty("title").GetString().ShouldBe("Nightly reconcile v2");
            doc.RootElement.GetProperty("lastUpdatedBy").GetString().ShouldNotBeNullOrEmpty();
            doc.RootElement.GetProperty("etag").GetString().ShouldNotBe(etag);

            // The designer state was not resupplied — carried forward unchanged.
            doc.RootElement.GetProperty("designerState").GetProperty("nodes").GetProperty("step-1").GetProperty("y").GetInt32().ShouldBe(80);
        }

        (await host.SendAsync(HttpMethod.Delete, $"/workspace/workflows/{id}", Write)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.SendAsync(HttpMethod.Get, $"/workspace/workflows/{id}", Read)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task A_stale_save_conflicts_and_a_save_without_the_etag_is_rejected()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());
        (string id, string etag) = await CreateAsync(host);

        // First save wins and advances the etag.
        (await host.SendJsonAsync(
            HttpMethod.Put,
            $"/workspace/workflows/{id}",
            $$"""{"document":{"arazzo":"1.1.0","x-rev":2},"expectedEtag":"{{etag}}"}""",
            Write)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // A collaborator saving with the etag they read earlier conflicts — nothing is clobbered.
        HttpResponseMessage stale = await host.SendJsonAsync(
            HttpMethod.Put,
            $"/workspace/workflows/{id}",
            $$"""{"document":{"arazzo":"1.1.0","x-rev":3},"expectedEtag":"{{etag}}"}""",
            Write);
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        using (Stj.JsonDocument current = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, $"/workspace/workflows/{id}", Read)))
        {
            current.RootElement.GetProperty("document").GetProperty("x-rev").GetInt32().ShouldBe(2);
        }

        // The etag is not optional — a save that omits it is rejected outright.
        (await host.SendJsonAsync(
            HttpMethod.Put,
            $"/workspace/workflows/{id}",
            """{"document":{"arazzo":"1.1.0","x-rev":4}}""",
            Write)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Creating_with_both_a_document_and_a_from_version_is_rejected()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());
        (await host.SendJsonAsync(
            HttpMethod.Post,
            "/workspace/workflows",
            """{"document":{"arazzo":"1.1.0"},"fromBaseWorkflowId":"flow-1","fromVersionNumber":1}""",
            Write)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task A_blank_create_gets_a_skeleton_document_and_the_name_derives_from_the_document()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());

        // Nothing supplied → an 'untitled' skeleton the designer can open (deliberately not yet a valid Arazzo document —
        // working copies hold work in progress; validation is on demand).
        using (Stj.JsonDocument blank = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/workspace/workflows", "{}", Write)))
        {
            blank.RootElement.GetProperty("name").GetString().ShouldBe("untitled");
            blank.RootElement.GetProperty("document").GetProperty("arazzo").GetString().ShouldBe("1.1.0");
            blank.RootElement.GetProperty("document").GetProperty("info").GetProperty("title").GetString().ShouldBe("untitled");
        }

        // A document with no explicit name → the first workflowId names the working copy.
        using (Stj.JsonDocument derived = await ReadJsonAsync(await host.SendJsonAsync(
            HttpMethod.Post,
            "/workspace/workflows",
            """{"document":{"arazzo":"1.1.0","workflows":[{"workflowId":"pay-invoice","steps":[]}]}}""",
            Write)))
        {
            derived.RootElement.GetProperty("name").GetString().ShouldBe("pay-invoice");
        }
    }

    [TestMethod]
    public async Task Opening_from_a_catalog_version_copies_the_document_and_records_provenance()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());
        await host.Catalog.SeedVersionAsync("flow-1");

        HttpResponseMessage created = await host.SendJsonAsync(
            HttpMethod.Post,
            "/workspace/workflows",
            """{"fromBaseWorkflowId":"flow-1","fromVersionNumber":1}""",
            Write);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using (Stj.JsonDocument doc = await ReadJsonAsync(created))
        {
            doc.RootElement.GetProperty("baseWorkflowId").GetString().ShouldBe("flow-1");
            doc.RootElement.GetProperty("basedOnVersion").GetInt32().ShouldBe(1);

            // The catalog version's workflow document was copied in as the starting point.
            doc.RootElement.GetProperty("document").GetProperty("arazzo").GetString().ShouldBe("1.1.0");
            doc.RootElement.GetProperty("document").TryGetProperty("workflows", out _).ShouldBeTrue();
        }

        // The version number is required with the base workflow id...
        (await host.SendJsonAsync(HttpMethod.Post, "/workspace/workflows", """{"fromBaseWorkflowId":"flow-1"}""", Write))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // ...and a version that does not exist is not found.
        (await host.SendJsonAsync(HttpMethod.Post, "/workspace/workflows", """{"fromBaseWorkflowId":"flow-1","fromVersionNumber":99}""", Write))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Listing_keyset_pages_with_a_continuation_token()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());
        for (int i = 0; i < 3; i++)
        {
            (await host.SendJsonAsync(HttpMethod.Post, "/workspace/workflows", $$"""{"document":{"arazzo":"1.1.0"},"name":"wc {{i}}"}""", Write))
                .StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        string token;
        using (Stj.JsonDocument first = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/workspace/workflows?limit=2", Read)))
        {
            first.RootElement.GetProperty("workingCopies").GetArrayLength().ShouldBe(2);
            token = first.RootElement.GetProperty("nextPageToken").GetString()!;
            token.ShouldNotBeNullOrEmpty();
        }

        using (Stj.JsonDocument second = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, $"/workspace/workflows?limit=2&pageToken={Uri.EscapeDataString(token)}", Read)))
        {
            second.RootElement.GetProperty("workingCopies").GetArrayLength().ShouldBe(1);
            second.RootElement.TryGetProperty("nextPageToken", out _).ShouldBeFalse();
        }
    }

    [TestMethod]
    public async Task Validation_of_a_clean_document_is_valid_with_no_findings()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());
        string id = await CreateWithDocumentAsync(host, """
        {
          "arazzo": "1.1.0",
          "info": { "title": "Clean", "version": "1.0.0" },
          "sourceDescriptions": [{ "name": "pets", "url": "./pets.json", "type": "openapi" }],
          "workflows": [{ "workflowId": "w", "steps": [{ "stepId": "a", "operationId": "listPets" }] }]
        }
        """);

        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/sources/pets", $$"""{"document":{{PetstoreDoc}}}""", Write)).StatusCode.ShouldBe(HttpStatusCode.OK);


        using Stj.JsonDocument outcome = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, $"/workspace/workflows/{id}/validate", Read));
        outcome.RootElement.GetProperty("valid").GetBoolean().ShouldBeTrue();
        outcome.RootElement.GetProperty("diagnostics").GetArrayLength().ShouldBe(0);
    }

    [TestMethod]
    public async Task Validation_reports_schema_conformance_findings_with_pointers()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());

        // The blank skeleton is deliberately not yet valid Arazzo (that is the point of a working
        // copy) — validating it reports the missing pieces as positioned schema findings.
        string id;
        using (Stj.JsonDocument created = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/workspace/workflows", "{}", Write)))
        {
            id = created.RootElement.GetProperty("id").GetString()!;
        }

        using Stj.JsonDocument outcome = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, $"/workspace/workflows/{id}/validate", Read));
        outcome.RootElement.GetProperty("valid").GetBoolean().ShouldBeFalse();
        Stj.JsonElement[] findings = [.. outcome.RootElement.GetProperty("diagnostics").EnumerateArray()];
        findings.ShouldAllBe(f => f.GetProperty("category").GetString() == "schema");
        findings.ShouldAllBe(f => f.GetProperty("severity").GetString() == "error");
        findings.ShouldContain(f => f.GetProperty("instancePath").GetString() != null && f.GetProperty("schemaLocation").GetString() != null);
    }

    [TestMethod]
    public async Task Validation_reports_semantic_findings_the_schema_cannot_see()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());

        // Schema-valid, semantically broken: the goto targets a step that does not exist.
        string id = await CreateWithDocumentAsync(host, """
        {
          "arazzo": "1.1.0",
          "info": { "title": "Broken", "version": "1.0.0" },
          "sourceDescriptions": [{ "name": "pets", "url": "./pets.json", "type": "openapi" }],
          "workflows": [{
            "workflowId": "w",
            "steps": [{
              "stepId": "a",
              "operationId": "listPets",
              "onSuccess": [{ "name": "jump", "type": "goto", "stepId": "ghost" }]
            }]
          }]
        }
        """);

        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/sources/pets", $$"""{"document":{{PetstoreDoc}}}""", Write)).StatusCode.ShouldBe(HttpStatusCode.OK);


        using Stj.JsonDocument outcome = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, $"/workspace/workflows/{id}/validate", Read));
        outcome.RootElement.GetProperty("valid").GetBoolean().ShouldBeFalse();
        Stj.JsonElement finding = outcome.RootElement.GetProperty("diagnostics").EnumerateArray().Single();
        finding.GetProperty("category").GetString().ShouldBe("goto-target");
        finding.GetProperty("instancePath").GetString().ShouldBe("/workflows/0/steps/0/onSuccess/0");
        finding.GetProperty("message").GetString()!.ShouldContain("ghost");
    }

    [TestMethod]
    public async Task Validation_flags_payload_literals_the_operation_schema_can_never_accept()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());

        // "tru" on a boolean leaf is not a boolean and not a runtime expression — nothing at
        // runtime can make it valid; the missing required 'amount' can never be added either.
        string id = await CreateWithDocumentAsync(host, """
        {
          "arazzo": "1.1.0",
          "info": { "title": "Typed", "version": "1.0.0" },
          "sourceDescriptions": [{ "name": "payments", "url": "./payments.json", "type": "openapi" }],
          "workflows": [{
            "workflowId": "w",
            "steps": [{
              "stepId": "authorize",
              "operationId": "authorize",
              "requestBody": { "payload": { "capture": "tru", "reference": "$inputs.orderId" } }
            }]
          }]
        }
        """);

        const string PaymentsDoc = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Payments", "version": "1.0.0" },
          "paths": {
            "/authorize": {
              "post": {
                "operationId": "authorize",
                "requestBody": { "content": { "application/json": { "schema": {
                  "type": "object",
                  "required": ["amount"],
                  "properties": {
                    "capture": { "type": "boolean" },
                    "amount": { "type": "number" },
                    "reference": { "type": "string" }
                  } } } } },
                "responses": { "200": { "description": "ok" } }
              }
            }
          }
        }
        """;
        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/sources/payments", $$"""{"document":{{PaymentsDoc}}}""", Write)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using (Stj.JsonDocument outcome = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, $"/workspace/workflows/{id}/validate", Read)))
        {
            outcome.RootElement.GetProperty("valid").GetBoolean().ShouldBeFalse();
            Stj.JsonElement[] typing = [.. outcome.RootElement.GetProperty("diagnostics").EnumerateArray()
                .Where(d => d.GetProperty("category").GetString() == "payload-typing")];
            Stj.JsonElement error = typing.Single(d => d.GetProperty("severity").GetString() == "error");
            error.GetProperty("instancePath").GetString().ShouldBe("/workflows/0/steps/0/requestBody/payload/capture");
            error.GetProperty("message").GetString()!.ShouldContain("'tru' is neither a boolean nor a runtime expression");
            typing.ShouldContain(d => d.GetProperty("severity").GetString() == "warning" && d.GetProperty("message").GetString()!.Contains("'amount' is missing"));
        }

        // A statically-typed expression must MATCH: $inputs.orderId is a string by the workflow's
        // own inputs schema — on the boolean leaf it is an error, not a benefit of the doubt.
        string etag0;
        using (Stj.JsonDocument current = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, $"/workspace/workflows/{id}", Read)))
        {
            etag0 = current.RootElement.GetProperty("etag").GetString()!;
        }

        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}", $$"""
        {
          "expectedEtag": {{Stj.JsonSerializer.Serialize(etag0)}},
          "document": {
            "arazzo": "1.1.0",
            "info": { "title": "Typed", "version": "1.0.0" },
            "sourceDescriptions": [{ "name": "payments", "url": "./payments.json", "type": "openapi" }],
            "workflows": [{
              "workflowId": "w",
              "inputs": { "type": "object", "properties": { "orderId": { "type": "string" } } },
              "steps": [{
                "stepId": "authorize",
                "operationId": "authorize",
                "requestBody": { "payload": { "capture": "$inputs.orderId", "amount": 12 } }
              }]
            }]
          }
        }
        """, Write)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using (Stj.JsonDocument outcome = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, $"/workspace/workflows/{id}/validate", Read)))
        {
            Stj.JsonElement mismatch = outcome.RootElement.GetProperty("diagnostics").EnumerateArray()
                .Single(d => d.GetProperty("category").GetString() == "payload-typing" && d.GetProperty("severity").GetString() == "error");
            mismatch.GetProperty("message").GetString()!.ShouldContain("resolves to a string — the operation's schema requires a boolean");
        }

        // Expressions are exempt wherever they appear; a right-typed literal passes too.
        string etag;
        using (Stj.JsonDocument current = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, $"/workspace/workflows/{id}", Read)))
        {
            etag = current.RootElement.GetProperty("etag").GetString()!;
        }

        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}", $$"""
        {
          "expectedEtag": {{Stj.JsonSerializer.Serialize(etag)}},
          "document": {
            "arazzo": "1.1.0",
            "info": { "title": "Typed", "version": "1.0.0" },
            "sourceDescriptions": [{ "name": "payments", "url": "./payments.json", "type": "openapi" }],
            "workflows": [{
              "workflowId": "w",
              "steps": [{
                "stepId": "authorize",
                "operationId": "authorize",
                "requestBody": { "payload": { "capture": "$inputs.capture", "amount": 12 } }
              }]
            }]
          }
        }
        """, Write)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using (Stj.JsonDocument outcome = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, $"/workspace/workflows/{id}/validate", Read)))
        {
            outcome.RootElement.EnumerateObject().First(p => p.NameEquals("diagnostics")).Value.EnumerateArray()
                .ShouldNotContain(d => d.GetProperty("category").GetString() == "payload-typing");
        }
    }

    [TestMethod]
    public async Task Warnings_do_not_fail_validation()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());

        // xpath round-trips but this runtime does not evaluate it — a warning, not an error.
        string id = await CreateWithDocumentAsync(host, """
        {
          "arazzo": "1.1.0",
          "info": { "title": "Xpath", "version": "1.0.0" },
          "sourceDescriptions": [{ "name": "pets", "url": "./pets.json", "type": "openapi" }],
          "workflows": [{
            "workflowId": "w",
            "steps": [{
              "stepId": "a",
              "operationId": "listPets",
              "successCriteria": [{ "context": "$response.body", "type": "xpath", "condition": "//pet" }]
            }]
          }]
        }
        """);

        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/sources/pets", $$"""{"document":{{PetstoreDoc}}}""", Write)).StatusCode.ShouldBe(HttpStatusCode.OK);


        using Stj.JsonDocument outcome = await ReadJsonAsync(await host.SendAsync(HttpMethod.Post, $"/workspace/workflows/{id}/validate", Read));
        outcome.RootElement.GetProperty("valid").GetBoolean().ShouldBeTrue();
        Stj.JsonElement finding = outcome.RootElement.GetProperty("diagnostics").EnumerateArray().Single();
        finding.GetProperty("severity").GetString().ShouldBe("warning");
        finding.GetProperty("category").GetString().ShouldBe("criterion-type");
    }

    [TestMethod]
    public async Task Validating_a_missing_working_copy_is_not_found()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());
        (await host.SendAsync(HttpMethod.Post, "/workspace/workflows/nope/validate", Read)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private const string PetstoreDoc =
        """{"openapi":"3.1.0","info":{"title":"Petstore","version":"1.0"},"paths":{"/pets":{"get":{"operationId":"listPets","summary":"List pets","responses":{"200":{"description":"ok","content":{"application/json":{"schema":{"type":"array","items":{"type":"object","properties":{"name":{"type":"string"}}}}}}},"default":{"description":"unexpected"}}}}}}""";

    [TestMethod]
    public async Task An_inline_source_attaches_lists_without_its_document_and_projects_operations()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());
        (string id, string etag) = await CreateAsync(host);

        HttpResponseMessage attached = await host.SendJsonAsync(
            HttpMethod.Put, $"/workspace/workflows/{id}/sources/pets", $$"""{"document":{{PetstoreDoc}}}""", Write);
        attached.StatusCode.ShouldBe(HttpStatusCode.OK);
        string freshEtag;
        using (Stj.JsonDocument doc = await ReadJsonAsync(attached))
        {
            doc.RootElement.GetProperty("name").GetString().ShouldBe("pets");
            doc.RootElement.GetProperty("kind").GetString().ShouldBe("inline");
            doc.RootElement.GetProperty("type").GetString().ShouldBe("openapi"); // detected from the document
            doc.RootElement.TryGetProperty("document", out _).ShouldBeFalse();   // never echoed
            freshEtag = doc.RootElement.GetProperty("etag").GetString()!;        // the attach bumped the working copy
            freshEtag.ShouldNotBe(etag);
        }

        // The list omits documents; the working copy still saves with the FRESH etag.
        using (Stj.JsonDocument list = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, $"/workspace/workflows/{id}/sources", Read)))
        {
            Stj.JsonElement entry = list.RootElement.GetProperty("sources").EnumerateArray().Single();
            entry.GetProperty("name").GetString().ShouldBe("pets");
            entry.TryGetProperty("document", out _).ShouldBeFalse();
        }

        (await host.SendJsonAsync(
            HttpMethod.Put, $"/workspace/workflows/{id}",
            $$"""{"document":{"arazzo":"1.1.0","x-rev":2},"expectedEtag":"{{freshEtag}}"}""",
            Write)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The attachments survived the save; the operation surface projects raw schemas.
        using Stj.JsonDocument surface = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, $"/workspace/workflows/{id}/sources/pets/operations", Read));
        Stj.JsonElement op = surface.RootElement.GetProperty("operations").EnumerateArray().Single();
        op.GetProperty("kind").GetString().ShouldBe("openapi");
        op.GetProperty("operationId").GetString().ShouldBe("listPets");
        op.GetProperty("method").GetString().ShouldBe("GET");
        Stj.JsonElement responses = op.GetProperty("responses");
        responses.TryGetProperty("200", out Stj.JsonElement ok).ShouldBeTrue();
        ok.GetProperty("schema").GetProperty("items").GetProperty("properties").TryGetProperty("name", out _).ShouldBeTrue();
        responses.TryGetProperty("default", out _).ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_registry_attachment_resolves_the_registered_source_at_read_time()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());
        (string id, _) = await CreateAsync(host);
        (await host.SendJsonAsync(
            HttpMethod.Post, "/sources",
            $$"""{"name":"petstore","type":"openapi","document":{{PetstoreDoc}}}""",
            "sources:write")).StatusCode.ShouldBe(HttpStatusCode.Created);

        using (Stj.JsonDocument doc = await ReadJsonAsync(await host.SendJsonAsync(
            HttpMethod.Put, $"/workspace/workflows/{id}/sources/pets", """{"sourceName":"petstore"}""", Write)))
        {
            doc.RootElement.GetProperty("kind").GetString().ShouldBe("registry");
            doc.RootElement.GetProperty("sourceName").GetString().ShouldBe("petstore");
            doc.RootElement.GetProperty("type").GetString().ShouldBe("openapi"); // echoed from the registry
        }

        using (Stj.JsonDocument surface = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, $"/workspace/workflows/{id}/sources/pets/operations", Read)))
        {
            surface.RootElement.GetProperty("operations").EnumerateArray().Single().GetProperty("operationId").GetString().ShouldBe("listPets");
        }

        // The reference re-resolves on every read: deleting the registered source breaks it honestly.
        (await host.SendAsync(HttpMethod.Delete, "/sources/petstore", "sources:write")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.SendAsync(HttpMethod.Get, $"/workspace/workflows/{id}/sources/pets/operations", Read)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Attach_validation_rejects_ambiguity_and_unknown_references_and_detach_removes()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());
        (string id, _) = await CreateAsync(host);

        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/sources/pets", "{}", Write))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/sources/pets", $$"""{"sourceName":"x","document":{{PetstoreDoc}}}""", Write))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/sources/pets", """{"sourceName":"nope"}""", Write))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/sources/pets", """{"document":{"random":true}}""", Write))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest); // undetectable type, none supplied

        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/sources/pets", $$"""{"document":{{PetstoreDoc}}}""", Write))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.SendAsync(HttpMethod.Delete, $"/workspace/workflows/{id}/sources/nope", Write)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await host.SendAsync(HttpMethod.Delete, $"/workspace/workflows/{id}/sources/pets", Write)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using Stj.JsonDocument list = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, $"/workspace/workflows/{id}/sources", Read));
        list.RootElement.GetProperty("sources").GetArrayLength().ShouldBe(0);
    }

    [TestMethod]
    public async Task The_registry_side_operation_surface_projects_without_a_working_copy()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());
        (await host.SendJsonAsync(
            HttpMethod.Post, "/sources",
            $$"""{"name":"petstore","type":"openapi","document":{{PetstoreDoc}}}""",
            "sources:write")).StatusCode.ShouldBe(HttpStatusCode.Created);

        using (Stj.JsonDocument surface = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, "/sources/petstore/operations", "sources:read")))
        {
            surface.RootElement.GetProperty("operations").EnumerateArray().Single().GetProperty("operationId").GetString().ShouldBe("listPets");
        }

        (await host.SendAsync(HttpMethod.Get, "/sources/nope/operations", "sources:read")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task The_scopes_are_enforced()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());

        // No scope → unauthenticated → 401.
        (await host.SendAsync(HttpMethod.Get, "/workspace/workflows", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // A read scope cannot write → 403.
        (await host.SendJsonAsync(HttpMethod.Post, "/workspace/workflows", CreateBody, Read)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // A write scope cannot read in this fixture (distinct scopes) → 403 on the read endpoint.
        (await host.SendAsync(HttpMethod.Get, "/workspace/workflows", Write)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task Validate_flags_source_integrity_drift_between_document_and_attachments()
    {
        await using Scoped host = await StartAsync();

        // Declared 'ghost' has no attachment; attached 'petstore' is undeclared; 'noSuchOp'
        // resolves against no attached surface. All three drift findings must fire.
        const string driftDoc = """{"arazzo":"1.1.0","info":{"title":"t","version":"1"},"sourceDescriptions":[{"name":"ghost","url":"./ghost.json","type":"openapi"}],"workflows":[{"workflowId":"wf","steps":[{"stepId":"a","operationId":"noSuchOp"}]}]}""";
        string id = await CreateWithDocumentAsync(host, driftDoc);
        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/sources/petstore", $$"""{"document":{{PetstoreDoc}}}""", Write))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        using Stj.JsonDocument report = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/validate", "{}", Read));
        var findings = report.RootElement.GetProperty("diagnostics").EnumerateArray()
            .Where(d => d.GetProperty("category").GetString() == "workspace-sources")
            .Select(d => (Severity: d.GetProperty("severity").GetString(), Message: d.GetProperty("message").GetString()!))
            .ToList();
        findings.ShouldContain(f => f.Severity == "warning" && f.Message.Contains("'ghost' has no attachment"));
        findings.ShouldContain(f => f.Severity == "info" && f.Message.Contains("'petstore' is not declared"));
        findings.ShouldContain(f => f.Severity == "warning" && f.Message.Contains("'noSuchOp' is not found"));

        // Steps bind operations with NO sourceDescriptions at all → an error; the document is invalid.
        const string bareDoc = """{"arazzo":"1.1.0","info":{"title":"t","version":"1"},"workflows":[{"workflowId":"wf","steps":[{"stepId":"a","operationId":"x"}]}]}""";
        string bare = await CreateWithDocumentAsync(host, bareDoc);
        using Stj.JsonDocument bareReport = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{bare}/validate", "{}", Read));
        bareReport.RootElement.GetProperty("valid").GetBoolean().ShouldBeFalse();
        bareReport.RootElement.GetProperty("diagnostics").EnumerateArray()
            .ShouldContain(d => d.GetProperty("category").GetString() == "workspace-sources" && d.GetProperty("severity").GetString() == "error");
    }

    private static async Task<(string Id, string Etag)> CreateAsync(Scoped host)
    {
        using Stj.JsonDocument doc = await ReadJsonAsync(await host.SendJsonAsync(HttpMethod.Post, "/workspace/workflows", CreateBody, Write));
        return (doc.RootElement.GetProperty("id").GetString()!, doc.RootElement.GetProperty("etag").GetString()!);
    }

    private static async Task<string> CreateWithDocumentAsync(Scoped host, string documentJson)
    {
        HttpResponseMessage created = await host.SendJsonAsync(HttpMethod.Post, "/workspace/workflows", $$"""{"document":{{documentJson}}}""", Write);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using Stj.JsonDocument doc = await ReadJsonAsync(created);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    private static async Task<Stj.JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task<Scoped> StartAsync(ControlPlaneRowSecurityPolicy? rowSecurity = null)
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops");

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(ScopeAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ScopeAuthHandler>(ScopeAuthHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), rowSecurity is null ? ControlPlaneSecurityMode.ScopesOnly : ControlPlaneSecurityMode.Scoped, rowSecurity: rowSecurity);
        await app.StartAsync();

        return new Scoped(app, app.GetTestClient(), new CatalogSeeder(catalog));
    }

    /// <summary>A scoped policy giving every caller full reach (so working copies are visible) and a fixed deployment
    /// identity <c>sys:tenant=acme</c> (stamped onto working copies' management tags).</summary>
    private sealed class TenantPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;

        public override IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal) => [new SecurityTag("sys:tenant", "acme")];
    }

    /// <summary>Publishes minimal catalog versions directly through the secured catalog, so from-version creates have
    /// something to carry over.</summary>
    private sealed class CatalogSeeder(ISecuredWorkflowCatalog catalog)
    {
        public async Task SeedVersionAsync(string workflowId)
        {
            byte[] workflow = Encoding.UTF8.GetBytes($$"""
            {
              "arazzo": "1.1.0",
              "info": { "title": "Flow", "description": "A flow." },
              "sourceDescriptions": [],
              "workflows": [ { "workflowId": "{{workflowId}}", "steps": [] } ]
            }
            """);
            ReadOnlyMemory<byte> package = CatalogPackage.Build(workflow, []);
            using ParsedJsonDocument<CatalogVersion> version = await catalog.AddAsync(package, new CatalogOwner("Team", "team@example.com", null, null), default, default);
        }
    }

    private sealed class Scoped(WebApplication app, HttpClient client, CatalogSeeder catalog) : IAsyncDisposable
    {
        public CatalogSeeder Catalog { get; } = catalog;

        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? scope)
            => this.SendCoreAsync(new HttpRequestMessage(method, path), scope);

        public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string? scope)
            => this.SendCoreAsync(new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, scope);

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, string? scope)
        {
            using (request)
            {
                if (scope is not null)
                {
                    request.Headers.Add(ScopeAuthHandler.ScopeHeader, scope);
                }

                return await client.SendAsync(request);
            }
        }
    }

    private sealed class ScopeAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Scopes";
        public const string ScopeHeader = "X-Scopes";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.TryGetValue(ScopeHeader, out Microsoft.Extensions.Primitives.StringValues values))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", values.ToString()));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    [TestMethod]
    public async Task Attachment_reads_back_whole_for_restore_after_detach()
    {
        await using Scoped host = await StartAsync(new TenantPolicy());
        string id = await CreateWithDocumentAsync(host, """
        {
          "arazzo": "1.1.0",
          "info": { "title": "d", "version": "1.0.0" },
          "sourceDescriptions": [{ "name": "inline-src", "url": "./s.json", "type": "openapi" }],
          "workflows": [{ "workflowId": "w", "steps": [] }]
        }
        """);

        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/sources/inline-src", """
        { "document": { "openapi": "3.1.0", "info": { "title": "S", "version": "1" }, "paths": { "/x": { "get": { "operationId": "x", "responses": { "200": { "description": "ok" } } } } } } }
        """, Write)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using (Stj.JsonDocument attachment = await ReadJsonAsync(await host.SendAsync(HttpMethod.Get, $"/workspace/workflows/{id}/sources/inline-src", Read)))
        {
            attachment.RootElement.GetProperty("name").GetString().ShouldBe("inline-src");
            attachment.RootElement.GetProperty("document").GetProperty("paths").GetProperty("/x").GetProperty("get").GetProperty("operationId").GetString().ShouldBe("x");
        }

        (await host.SendAsync(HttpMethod.Delete, $"/workspace/workflows/{id}/sources/inline-src", Write)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.SendAsync(HttpMethod.Get, $"/workspace/workflows/{id}/sources/inline-src", Read)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
