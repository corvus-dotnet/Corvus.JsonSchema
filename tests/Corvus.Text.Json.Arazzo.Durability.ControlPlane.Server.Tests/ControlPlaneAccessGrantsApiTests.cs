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
    public async Task Get_access_grants_projects_matching_bindings_administered_workflows_and_usable_credentials()
    {
        ArazzoControlPlaneSecurityHandler handler = await CreateSeededHandlerAsync();

        using JsonWorkspace workspace = JsonWorkspace.Create();
        var parameters = new GetAccessGrantsParams { Grantee = EncodeGranteeToken(GranteeJson, workspace) };
        GetAccessGrantsResult result = await handler.HandleGetAccessGrantsAsync(parameters, workspace);
        result.StatusCode.ShouldBe(200);

        using Stj.JsonDocument doc = ReadResultBody(result);

        // The grantee is echoed verbatim.
        doc.RootElement.GetProperty("grantee").GetProperty("value").GetString().ShouldBe("u-1042");

        // bindings: only the binding whose claim the grantee satisfies (sub=u-1042); the team=platform binding is dropped.
        doc.RootElement.GetProperty("bindings").EnumerateArray()
            .Select(b => b.GetProperty("claimType").GetString()).ShouldBe(["sub"]);
        doc.RootElement.GetProperty("bindings").EnumerateArray()
            .Select(b => b.GetProperty("claimValue").GetString()).ShouldBe(["u-1042"]);

        // administers: the base workflow the grantee's identity administers (a reverse-index lookup keyed by its digest).
        doc.RootElement.GetProperty("administers").EnumerateArray()
            .Select(a => a.GetProperty("baseWorkflowId").GetString()).ShouldBe(["orders-workflow"]);

        // credentialUsage: the usable credential (the grantee carries its sys:sub usage tag); the sys:tenant credential
        // the grantee cannot use is dropped.
        Stj.JsonElement usage = doc.RootElement.GetProperty("credentialUsage");
        usage.GetArrayLength().ShouldBe(1);
        usage[0].GetProperty("sourceName").GetString().ShouldBe("orders-api");
        usage[0].GetProperty("environment").GetString().ShouldBe("production");
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

        using Stj.JsonDocument doc = ReadResultBody(result);

        // capabilities: resolved exactly as the runtime resolver would — the genesis-like binding's scopes are active,
        // the eligible-only binding's scope is eligible (with its expiry), and the expired binding confers nothing.
        Stj.JsonElement capabilities = doc.RootElement.GetProperty("capabilities");
        capabilities.EnumerateArray().Select(c => c.GetProperty("scope").GetString())
            .ShouldBe(["runs:purge", "runs:read", "runs:write"]);
        capabilities.EnumerateArray().Select(c => c.GetProperty("eligible").GetBoolean())
            .ShouldBe([true, false, false]);
        capabilities[0].TryGetProperty("expiresAt", out _).ShouldBeTrue("the eligible grant is time-boxed");
        capabilities[1].TryGetProperty("expiresAt", out _).ShouldBeFalse("the genesis-like grant never expires");

        // administersEnvironments: the environment reverse index keyed by the resolved identity (membership).
        doc.RootElement.GetProperty("administersEnvironments").EnumerateArray()
            .Select(e => e.GetProperty("environment").GetString()).ShouldBe(["production"]);

        // credentialUsage: the production credential usage-scoped to the admin group.
        Stj.JsonElement usage = doc.RootElement.GetProperty("credentialUsage");
        usage.GetArrayLength().ShouldBe(1);
        usage[0].GetProperty("sourceName").GetString().ShouldBe("onboarding");
        usage[0].GetProperty("environment").GetString().ShouldBe("production");

        // The binding summaries surface where the capabilities come from: scopes + eligibleOnly ride along.
        Stj.JsonElement bindings = doc.RootElement.GetProperty("bindings");
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
    public async Task A_malformed_or_empty_grantee_token_is_a_bad_request()
    {
        ArazzoControlPlaneSecurityHandler handler = await CreateSeededHandlerAsync();

        using JsonWorkspace workspace = JsonWorkspace.Create();

        // An empty token → 400.
        var empty = new GetAccessGrantsParams { Grantee = ParseRawGrantee(string.Empty, workspace) };
        (await handler.HandleGetAccessGrantsAsync(empty, workspace)).StatusCode.ShouldBe(400);

        // A token that is not valid base64url → 400.
        var malformed = new GetAccessGrantsParams { Grantee = ParseRawGrantee("not-a-valid-token!!", workspace) };
        (await handler.HandleGetAccessGrantsAsync(malformed, workspace)).StatusCode.ShouldBe(400);
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
    // (mirrors ControlPlaneSecurityApiTests.ReadResultBody).
    private static Stj.JsonDocument ReadResultBody(GetAccessGrantsResult result)
        => Stj.JsonDocument.Parse(JsonMarshal.GetRawUtf8Value(result.Body).Memory);
}