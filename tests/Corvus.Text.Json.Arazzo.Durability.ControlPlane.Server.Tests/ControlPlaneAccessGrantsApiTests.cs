// <copyright file="ControlPlaneAccessGrantsApiTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.OpenApi;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;
using Stj = System.Text.Json;
using VerbGrant = Corvus.Text.Json.Arazzo.Durability.Security.SecurityBindingDocument.VerbGrantInfo;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Tests the access-grants overview endpoint (<c>GET /access/grants?grantee=&lt;token&gt;</c>, design §6.1): the
/// grantee token round-trips a resolved grantee's full JSON, and the handler projects the bindings whose claim the
/// grantee satisfies, the base workflows its identity administers, and the source credentials its identity may use.
/// Modelled on the direct-handler tests in <see cref="ControlPlaneSecurityApiTests"/> (construct the handler over
/// in-memory stores, call <c>HandleGetAccessGrantsAsync</c> with a workspace, and re-parse the result body with
/// System.Text.Json to assert over the wire shape).
/// </summary>
[TestClass]
public sealed class ControlPlaneAccessGrantsApiTests
{
    // A resolved person grantee whose identity is the operator-facing (sys:-stripped) wire form every grantee endpoint
    // returns (design §16.5.4 — an identity is described back over its sys: tags as {dimension,value} grants via
    // DescribeUsageScope/TryDescribeUsageGrant, e.g. sub, NOT sys:sub). The handler resolves it back to the internal sys:
    // tag set (ControlPlaneAccess.ResolveUsageGrantInto: sub -> sys:sub), so its digest keys the administered-workflows
    // reverse index and the credential IsUsableBy match against the stored sys: identity; the binding match derives the
    // operator-facing claim directly (sub matches claimType sub). (With the pre-revision handler, which added the wire
    // dimension verbatim, the digest was over sub -> never matched the stored sys:sub -> administers/credentials empty.)
    private const string GranteeJson =
        """{"kind":"person","value":"u-1042","identity":[{"dimension":"sub","value":"u-1042"}],"source":"observed","complete":true}""";

    [TestMethod]
    public async Task Get_access_grants_summary_echoes_the_grantee_and_the_paged_sub_resources_project_bindings_workflows_and_credentials()
    {
        ArazzoControlPlaneSecurityHandler handler = await CreateSeededHandlerAsync();

        using JsonWorkspace workspace = JsonWorkspace.Create();

        // The summary echoes the grantee verbatim; the unbounded lists are the keyset-paged sub-resources below (they are
        // NOT on the summary any more).
        var summaryParams = new GetAccessGrantsParams { Grantee = EncodeGranteeToken(GranteeJson, workspace) };
        GetAccessGrantsResult summary = await handler.HandleGetAccessGrantsAsync(summaryParams, workspace);
        summary.StatusCode.ShouldBe(200);
        using Stj.JsonDocument summaryDoc = ReadBody(summary.Body);
        summaryDoc.RootElement.GetProperty("grantee").GetProperty("value").GetString().ShouldBe("u-1042");
        summaryDoc.RootElement.TryGetProperty("bindings", out _).ShouldBeFalse("the reach list is the /reach sub-resource, not the summary");

        // reach: only the binding whose claim the grantee satisfies (sub=u-1042); the team=platform binding is dropped.
        using Stj.JsonDocument reach = await GetReachAsync(handler, GranteeJson, workspace);
        reach.RootElement.GetProperty("bindings").EnumerateArray()
            .Select(b => b.GetProperty("claimType").GetString()).ShouldBe(["sub"]);
        reach.RootElement.GetProperty("bindings").EnumerateArray()
            .Select(b => b.GetProperty("claimValue").GetString()).ShouldBe(["u-1042"]);

        // administered: the base workflow the grantee's identity administers (a reverse-index lookup keyed by its digest).
        using Stj.JsonDocument administered = await GetAdministeredAsync(handler, GranteeJson, workspace);
        administered.RootElement.GetProperty("administers").EnumerateArray()
            .Select(a => a.GetProperty("baseWorkflowId").GetString()).ShouldBe(["orders-workflow"]);

        // credentials: the usable credential (the grantee carries its sys:sub usage tag); the sys:tenant credential the
        // grantee cannot use is dropped.
        using Stj.JsonDocument credentials = await GetCredentialsAsync(handler, GranteeJson, workspace);
        Stj.JsonElement usage = credentials.RootElement.GetProperty("credentialUsage");
        usage.GetArrayLength().ShouldBe(1);
        usage[0].GetProperty("sourceName").GetString().ShouldBe("orders-api");
        usage[0].GetProperty("environment").GetString().ShouldBe("production");
    }

    [TestMethod]
    public async Task Get_access_grants_enriches_administered_workflows_and_environments()
    {
        // §849: the administered rows carry a server-side summary so each reads without a per-row detail fetch.
        ArazzoControlPlaneSecurityHandler handler = await CreateEnrichedHandlerAsync();

        using JsonWorkspace workspace = JsonWorkspace.Create();

        // The administered-workflow row (the /administered sub-resource) carries its representative version's summary.
        using Stj.JsonDocument administered = await GetAdministeredAsync(handler, GranteeJson, workspace);
        Stj.JsonElement workflow = administered.RootElement.GetProperty("administers").EnumerateArray().Single();
        workflow.GetProperty("baseWorkflowId").GetString().ShouldBe("orders-workflow");
        workflow.GetProperty("title").GetString().ShouldBe("Orders");
        workflow.GetProperty("latestVersion").GetInt32().ShouldBe(1);
        workflow.GetProperty("owner").GetString().ShouldBe("Ops");
        workflow.TryGetProperty("status", out _).ShouldBeTrue("the representative version's status is surfaced");

        // The administered-environment row (on the summary) carries the environment summary + a bounded availability count.
        var summaryParams = new GetAccessGrantsParams { Grantee = EncodeGranteeToken(GranteeJson, workspace) };
        GetAccessGrantsResult summary = await handler.HandleGetAccessGrantsAsync(summaryParams, workspace);
        summary.StatusCode.ShouldBe(200);
        using Stj.JsonDocument doc = ReadBody(summary.Body);
        Stj.JsonElement environment = doc.RootElement.GetProperty("administersEnvironments").EnumerateArray().Single();
        environment.GetProperty("environment").GetString().ShouldBe("production");
        environment.GetProperty("displayName").GetString().ShouldBe("Production");
        environment.GetProperty("allowsDraftRuns").GetBoolean().ShouldBeFalse("the seeded environment does not allow draft runs");
        environment.GetProperty("availability").GetProperty("count").GetInt32().ShouldBe(1);
    }

    // Seeds a catalog version, an environment record, an availability entry, and the grantee's administration of both, then
    // constructs the handler WITH the environment + availability stores so the §849 administered-row enrichment runs.
    private static async Task<ArazzoControlPlaneSecurityHandler> CreateEnrichedHandlerAsync()
    {
        SecurityTagSet granteeIdentity = SecurityTagSet.FromTags([new SecurityTag("sys:sub", "u-1042")]);

        // Publish version 1 with the grantee's identity as its security tags: create-grants-admin (§15.2) then establishes
        // the grantee as the sole administrator of 'orders-workflow', so it appears (enriched) in the grantee's administers.
        var administratorStore = new InMemoryWorkflowAdministratorStore();
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), new InMemoryWorkflowStateStore(), "ops", administrators: administratorStore);
        ReadOnlyMemory<byte> package = WorkflowPackage.Pack(
            Encoding.UTF8.GetBytes("""{"arazzo":"1.1.0","info":{"title":"Orders","version":"1"},"workflows":[{"workflowId":"orders-workflow","steps":[]}]}"""), []);
        (await catalog.AddAsync(package, new CatalogOwner("Ops", "ops@example.com"), default, granteeIdentity, default)).Dispose();

        var environmentStore = new Corvus.Text.Json.Arazzo.Durability.Environments.InMemoryEnvironmentStore();
        using (ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.Environments.Environment> environmentDraft =
            Corvus.Text.Json.Arazzo.Durability.Environments.Environment.Draft("production", "Production", "The live environment.", SecurityTagSet.Empty))
        {
            (await environmentStore.AddAsync(environmentDraft.RootElement, "ops", default)).Dispose();
        }

        var envAdminStore = new InMemoryEnvironmentAdministratorStore();
        var environmentAdministration = new SecuredEnvironmentAdministration(envAdminStore);
        await environmentAdministration.EstablishAsync("production", granteeIdentity, default, false, default, false, default);

        var availabilityStore = new Corvus.Text.Json.Arazzo.Durability.Availability.InMemoryAvailabilityStore();
        (await availabilityStore.MakeAvailableAsync("orders-workflow", 1, "production", "ops", default)).Entry.Dispose();

        var policyStore = new InMemorySecurityPolicyStore();
        var policy = new PersistentRowSecurityPolicy(policyStore);
        var access = new ControlPlaneAccess(new HttpContextAccessor(), policy);
        return new ArazzoControlPlaneSecurityHandler(
            policyStore, policy, access, catalog, new InMemorySourceCredentialStore(), environmentAdministration,
            environmentStore: environmentStore, availabilityStore: availabilityStore);
    }

    // A resolved TEAM grantee — the live snag's shape (#96): the admin group's identity in the operator-facing wire
    // form ({group,iss}; the internal form is {sys:group,sys:iss}).
    private const string TeamGranteeJson =
        """{"kind":"team","value":"arazzo-admins","identity":[{"dimension":"group","value":"arazzo-admins"},{"dimension":"iss","value":"arazzo-keycloak"}],"source":"observed","complete":true}""";

    // The fixed instant the capability aggregation resolves expiry against.
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Get_access_grants_surfaces_capabilities_administered_environments_and_scoped_credentials_for_a_team()
    {
        ArazzoControlPlaneSecurityHandler handler = await CreateAdminTeamHandlerAsync();

        using JsonWorkspace workspace = JsonWorkspace.Create();
        var parameters = new GetAccessGrantsParams { Grantee = EncodeGranteeToken(TeamGranteeJson, workspace) };
        GetAccessGrantsResult result = await handler.HandleGetAccessGrantsAsync(parameters, workspace);
        result.StatusCode.ShouldBe(200);

        using Stj.JsonDocument doc = ReadBody(result.Body);

        // capabilities (on the summary): resolved exactly as the runtime resolver would — the genesis-like binding's scopes
        // are active, the eligible-only binding's scope is eligible (with its expiry), and the expired binding confers nothing.
        Stj.JsonElement capabilities = doc.RootElement.GetProperty("capabilities");
        capabilities.EnumerateArray().Select(c => c.GetProperty("scope").GetString())
            .ShouldBe(["runs:purge", "runs:read", "runs:write"]);
        capabilities.EnumerateArray().Select(c => c.GetProperty("eligible").GetBoolean())
            .ShouldBe([true, false, false]);
        capabilities[0].TryGetProperty("expiresAt", out _).ShouldBeTrue("the eligible grant is time-boxed");
        capabilities[1].TryGetProperty("expiresAt", out _).ShouldBeFalse("the genesis-like grant never expires");

        // administersEnvironments (on the summary): the environment reverse index keyed by the resolved identity (membership).
        doc.RootElement.GetProperty("administersEnvironments").EnumerateArray()
            .Select(e => e.GetProperty("environment").GetString()).ShouldBe(["production"]);

        // credentials (the /credentials sub-resource): the production credential usage-scoped to the admin group.
        using Stj.JsonDocument credentials = await GetCredentialsAsync(handler, TeamGranteeJson, workspace);
        Stj.JsonElement usage = credentials.RootElement.GetProperty("credentialUsage");
        usage.GetArrayLength().ShouldBe(1);
        usage[0].GetProperty("sourceName").GetString().ShouldBe("onboarding");
        usage[0].GetProperty("environment").GetString().ShouldBe("production");

        // The reach binding summaries (the /reach sub-resource) surface where the capabilities come from: scopes +
        // eligibleOnly ride along.
        using Stj.JsonDocument reach = await GetReachAsync(handler, TeamGranteeJson, workspace);
        Stj.JsonElement bindings = reach.RootElement.GetProperty("bindings");
        bindings.EnumerateArray().Count(b => b.TryGetProperty("scopes", out _)).ShouldBe(3);
        bindings.EnumerateArray().Count(b => b.TryGetProperty("eligibleOnly", out Stj.JsonElement e) && e.GetBoolean()).ShouldBe(1);
    }

    // Seeds the admin-team scenario (#96): a genesis-like scope-bearing binding, an eligible-only (PIM) binding, an
    // expired binding, environment administration for the group identity, and a production credential usage-scoped to
    // it. The clock is pinned so expiry resolution is deterministic.
    private static async Task<ArazzoControlPlaneSecurityHandler> CreateAdminTeamHandlerAsync()
    {
        SecurityTagSet adminIdentity = SecurityTagSet.FromTags(
            [new SecurityTag("sys:group", "arazzo-admins"), new SecurityTag("sys:iss", "arazzo-keycloak")]);

        var policyStore = new InMemorySecurityPolicyStore();
        using (ParsedJsonDocument<SecurityBindingDocument> genesis = SecurityBindingDocument.Draft(
            "group", "arazzo-admins", VerbGrant.Full, VerbGrant.Full, VerbGrant.Full, order: 10,
            scopes: ["runs:read", "runs:write"]))
        {
            (await policyStore.AddBindingAsync(genesis.RootElement, "ops", default)).Dispose();
        }

        using (ParsedJsonDocument<SecurityBindingDocument> eligible = SecurityBindingDocument.Draft(
            "group", "arazzo-admins", VerbGrant.None, VerbGrant.None, VerbGrant.None, order: 20,
            scopes: ["runs:purge"], expiresAt: Now.AddHours(1), eligibleOnly: true))
        {
            (await policyStore.AddBindingAsync(eligible.RootElement, "ops", default)).Dispose();
        }

        using (ParsedJsonDocument<SecurityBindingDocument> expired = SecurityBindingDocument.Draft(
            "group", "arazzo-admins", VerbGrant.Full, VerbGrant.None, VerbGrant.None, order: 30,
            scopes: ["catalog:read"], expiresAt: Now.AddHours(-1)))
        {
            (await policyStore.AddBindingAsync(expired.RootElement, "ops", default)).Dispose();
        }

        var credentialStore = new InMemorySourceCredentialStore();
        (await credentialStore.AddAsync(
            new SourceCredentialDefinition(
                "onboarding",
                "production",
                SourceCredentialKind.ApiKey,
                [new SecretReferenceDefinition("value", "vault://secret/arazzo/onboarding#api-key")],
                UsageTags: adminIdentity),
            "ops",
            default)).Dispose();

        var envAdminStore = new InMemoryEnvironmentAdministratorStore();
        var environmentAdministration = new SecuredEnvironmentAdministration(envAdminStore);
        await environmentAdministration.EstablishAsync("production", adminIdentity, default, false, default, false, default);

        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), new InMemoryWorkflowStateStore(), "ops", administrators: new InMemoryWorkflowAdministratorStore());
        var policy = new PersistentRowSecurityPolicy(policyStore);
        var access = new ControlPlaneAccess(new HttpContextAccessor(), policy);
        return new ArazzoControlPlaneSecurityHandler(
            policyStore, policy, access, catalog, credentialStore, environmentAdministration, new FixedTimeProvider(Now));
    }

    // A TimeProvider pinned to one instant, so the expiry-sensitive aggregation is deterministic.
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [TestMethod]
    public async Task Reach_keyset_pages_the_matched_bindings()
    {
        // Three bindings the grantee matches; a limit of 2 yields a first page of 2 + a continuation token, then a last
        // page of 1 with no token — the keyset contract the client walks.
        var policyStore = new InMemorySecurityPolicyStore();
        for (int order = 10; order < 13; order++)
        {
            using ParsedJsonDocument<SecurityBindingDocument> binding = SecurityBindingDocument.Draft(
                "sub", "u-1042", VerbGrant.Full, VerbGrant.None, VerbGrant.None, order: order);
            (await policyStore.AddBindingAsync(binding.RootElement, "ops", default)).Dispose();
        }

        var policy = new PersistentRowSecurityPolicy(policyStore);
        var access = new ControlPlaneAccess(new HttpContextAccessor(), policy);
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), new InMemoryWorkflowStateStore(), "ops", administrators: new InMemoryWorkflowAdministratorStore());
        var handler = new ArazzoControlPlaneSecurityHandler(policyStore, policy, access, catalog, new InMemorySourceCredentialStore());

        using JsonWorkspace workspace = JsonWorkspace.Create();

        var firstParams = new GetAccessGrantsReachParams
        {
            Grantee = EncodeGranteeToken(GranteeJson, workspace),
            Limit = HeaderValueParser.ParseNumber<Models.PageLimit>("2", workspace),
        };
        GetAccessGrantsReachResult first = await handler.HandleGetAccessGrantsReachAsync(firstParams, workspace);
        first.StatusCode.ShouldBe(200);
        using Stj.JsonDocument firstDoc = ReadBody(first.Body);
        firstDoc.RootElement.GetProperty("bindings").GetArrayLength().ShouldBe(2);
        string token = firstDoc.RootElement.GetProperty("nextPageToken").GetString()!;
        token.ShouldNotBeNullOrEmpty("more matched bindings remain, so a continuation token is emitted");

        var secondParams = new GetAccessGrantsReachParams
        {
            Grantee = EncodeGranteeToken(GranteeJson, workspace),
            Limit = HeaderValueParser.ParseNumber<Models.PageLimit>("2", workspace),
            PageToken = HeaderValueParser.ParseString<Models.JsonString>(token, workspace),
        };
        GetAccessGrantsReachResult second = await handler.HandleGetAccessGrantsReachAsync(secondParams, workspace);
        second.StatusCode.ShouldBe(200);
        using Stj.JsonDocument secondDoc = ReadBody(second.Body);
        secondDoc.RootElement.GetProperty("bindings").GetArrayLength().ShouldBe(1);
        secondDoc.RootElement.TryGetProperty("nextPageToken", out _).ShouldBeFalse("the last page omits the continuation token");
    }

    [TestMethod]
    public async Task A_malformed_or_empty_grantee_token_is_a_bad_request_on_every_endpoint()
    {
        ArazzoControlPlaneSecurityHandler handler = await CreateSeededHandlerAsync();

        using JsonWorkspace workspace = JsonWorkspace.Create();

        // The summary and each paged sub-resource share the grantee-decode, so all four reject an empty or malformed token.
        (await handler.HandleGetAccessGrantsAsync(new GetAccessGrantsParams { Grantee = ParseRawGrantee(string.Empty, workspace) }, workspace)).StatusCode.ShouldBe(400);
        (await handler.HandleGetAccessGrantsAsync(new GetAccessGrantsParams { Grantee = ParseRawGrantee("not-a-valid-token!!", workspace) }, workspace)).StatusCode.ShouldBe(400);

        (await handler.HandleGetAccessGrantsReachAsync(new GetAccessGrantsReachParams { Grantee = ParseRawGrantee(string.Empty, workspace) }, workspace)).StatusCode.ShouldBe(400);
        (await handler.HandleGetAccessGrantsReachAsync(new GetAccessGrantsReachParams { Grantee = ParseRawGrantee("not-a-valid-token!!", workspace) }, workspace)).StatusCode.ShouldBe(400);

        (await handler.HandleGetAccessGrantsAdministeredAsync(new GetAccessGrantsAdministeredParams { Grantee = ParseRawGrantee(string.Empty, workspace) }, workspace)).StatusCode.ShouldBe(400);
        (await handler.HandleGetAccessGrantsAdministeredAsync(new GetAccessGrantsAdministeredParams { Grantee = ParseRawGrantee("not-a-valid-token!!", workspace) }, workspace)).StatusCode.ShouldBe(400);

        (await handler.HandleGetAccessGrantsCredentialsAsync(new GetAccessGrantsCredentialsParams { Grantee = ParseRawGrantee(string.Empty, workspace) }, workspace)).StatusCode.ShouldBe(400);
        (await handler.HandleGetAccessGrantsCredentialsAsync(new GetAccessGrantsCredentialsParams { Grantee = ParseRawGrantee("not-a-valid-token!!", workspace) }, workspace)).StatusCode.ShouldBe(400);
    }

    // Seeds the in-memory stores and constructs the handler through its internal constructor (the only ctor that binds the
    // access/catalog/credentials the access-grants aggregation reads): a policy store with a binding the grantee matches
    // and one it does not, a credential store with a usable and a non-usable binding, and a catalog whose administrator
    // store establishes the grantee as the administrator of one base workflow.
    private static async Task<ArazzoControlPlaneSecurityHandler> CreateSeededHandlerAsync()
    {
        SecurityTagSet granteeIdentity = SecurityTagSet.FromTags([new SecurityTag("sys:sub", "u-1042")]);

        // Bindings: sub=u-1042 matches one of the grantee's {dimension,value} grants; team=platform does not.
        var policyStore = new InMemorySecurityPolicyStore();
        using (ParsedJsonDocument<SecurityBindingDocument> match = SecurityBindingDocument.Draft("sub", "u-1042", VerbGrant.Full, VerbGrant.None, VerbGrant.None, order: 10))
        {
            (await policyStore.AddBindingAsync(match.RootElement, "ops", default)).Dispose();
        }

        using (ParsedJsonDocument<SecurityBindingDocument> other = SecurityBindingDocument.Draft("team", "platform", VerbGrant.Full, VerbGrant.None, VerbGrant.None, order: 20))
        {
            (await policyStore.AddBindingAsync(other.RootElement, "ops", default)).Dispose();
        }

        // Credentials: the grantee carries sys:sub, so the sys:sub-tagged binding is usable; the sys:tenant-tagged one is not.
        var credentialStore = new InMemorySourceCredentialStore();
        (await credentialStore.AddAsync(
            new SourceCredentialDefinition(
                "orders-api",
                "production",
                SourceCredentialKind.ApiKey,
                [new SecretReferenceDefinition("value", "keyvault://orders-apikey#1")],
                UsageTags: granteeIdentity),
            "ops",
            default)).Dispose();
        (await credentialStore.AddAsync(
            new SourceCredentialDefinition(
                "billing-api",
                "production",
                SourceCredentialKind.ApiKey,
                [new SecretReferenceDefinition("value", "keyvault://billing-apikey#1")],
                UsageTags: SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "acme")])),
            "ops",
            default)).Dispose();

        // Administration: establish the grantee's identity as administrator of one base workflow (the PutAsync builds the
        // reverse index the aggregation queries by identity digest).
        var administratorStore = new InMemoryWorkflowAdministratorStore();
        using (JsonWorkspace seedWorkspace = JsonWorkspace.Create())
        {
            WorkflowAdministrators.AdministratorIdentity admin =
                WorkflowAdministrators.BuildIdentity(seedWorkspace, granteeIdentity, default, hasKind: false, default, hasLabel: false);
            (await administratorStore.PutAsync("orders-workflow", [admin], WorkflowEtag.None, "ops", default)).Dispose();
        }

        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), new InMemoryWorkflowStateStore(), "ops", administrators: administratorStore);
        var policy = new PersistentRowSecurityPolicy(policyStore);
        var access = new ControlPlaneAccess(new HttpContextAccessor(), policy);
        return new ArazzoControlPlaneSecurityHandler(policyStore, policy, access, catalog, credentialStore);
    }

    // Base64url-encodes the grantee JSON to its opaque token and binds it as the query parameter exactly as the generated
    // endpoint does (HeaderValueParser.ParseString over a workspace-owned document).
    private static Models.JsonString EncodeGranteeToken(string granteeJson, JsonWorkspace workspace)
    {
        string token = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(granteeJson));
        return HeaderValueParser.ParseString<Models.JsonString>(token, workspace);
    }

    // Binds a raw string as the grantee parameter without base64url-encoding it (an empty or malformed token).
    private static Models.JsonString ParseRawGrantee(string raw, JsonWorkspace workspace)
        => HeaderValueParser.ParseString<Models.JsonString>(raw, workspace);

    // Reads a result's CTJ body as UTF-8 and re-parses it with System.Text.Json so the test asserts over the wire shape
    // (mirrors ControlPlaneSecurityApiTests.ReadResultBody). Every result type exposes its body as a CTJ JsonElement.
    private static Stj.JsonDocument ReadBody(Corvus.Text.Json.JsonElement body)
        => Stj.JsonDocument.Parse(JsonMarshal.GetRawUtf8Value(body).Memory);

    // The keyset-paged sub-resources: call the handler for the grantee (no limit/token → first page) and re-parse the body.
    private static async Task<Stj.JsonDocument> GetReachAsync(ArazzoControlPlaneSecurityHandler handler, string granteeJson, JsonWorkspace workspace)
    {
        var parameters = new GetAccessGrantsReachParams { Grantee = EncodeGranteeToken(granteeJson, workspace) };
        GetAccessGrantsReachResult result = await handler.HandleGetAccessGrantsReachAsync(parameters, workspace);
        result.StatusCode.ShouldBe(200);
        return ReadBody(result.Body);
    }

    private static async Task<Stj.JsonDocument> GetAdministeredAsync(ArazzoControlPlaneSecurityHandler handler, string granteeJson, JsonWorkspace workspace)
    {
        var parameters = new GetAccessGrantsAdministeredParams { Grantee = EncodeGranteeToken(granteeJson, workspace) };
        GetAccessGrantsAdministeredResult result = await handler.HandleGetAccessGrantsAdministeredAsync(parameters, workspace);
        result.StatusCode.ShouldBe(200);
        return ReadBody(result.Body);
    }

    private static async Task<Stj.JsonDocument> GetCredentialsAsync(ArazzoControlPlaneSecurityHandler handler, string granteeJson, JsonWorkspace workspace)
    {
        var parameters = new GetAccessGrantsCredentialsParams { Grantee = EncodeGranteeToken(granteeJson, workspace) };
        GetAccessGrantsCredentialsResult result = await handler.HandleGetAccessGrantsCredentialsAsync(parameters, workspace);
        result.StatusCode.ShouldBe(200);
        return ReadBody(result.Body);
    }
}