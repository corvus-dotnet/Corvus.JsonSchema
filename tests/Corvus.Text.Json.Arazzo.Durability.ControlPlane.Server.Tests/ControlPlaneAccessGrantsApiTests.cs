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
    // A resolved person grantee whose identity is the internal sys: form the wire returns (design §16.5.4): sys:sub=u-1042.
    // That sys: identity keys the administered-workflows reverse index and the credential IsUsableBy match directly; the
    // binding match derives the operator-facing claim from it (sys:sub -> sub) to match a binding on claimType sub. (With
    // the pre-revision handler, which compared the sys: dimension to the claim as-is, sys:sub != sub dropped the binding.)
    private const string GranteeJson =
        """{"kind":"person","value":"u-1042","identity":[{"dimension":"sys:sub","value":"u-1042"}],"source":"observed","complete":true}""";

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