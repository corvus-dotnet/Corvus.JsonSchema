// <copyright file="AccessGrantsProjectionBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.OpenApi;
using Microsoft.AspNetCore.Http;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;
using VerbGrant = Corvus.Text.Json.Arazzo.Durability.Security.SecurityBindingDocument.VerbGrantInfo;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Allocation profile of the access-grants overview projection (design §6.1): a resolved grantee is aggregated into the
/// <c>AccessGrantsOverview</c> response body — the bindings whose claim it satisfies, the base workflows its identity
/// administers, and the source credentials it may use — and materialised into the response workspace exactly as the
/// handler does (the terminal <c>GetAccessGrantsResult.Ok</c> step — a single <c>CreateBuilder</c> materialisation — is
/// modelled in every arm). The fair comparison is <see cref="Naive_ManagedProjection"/> (baseline) versus
/// <see cref="BytesNative_Projection"/>: both emit the SAME overview shape from the SAME pre-seeded fixture, so the delta
/// isolates the projection MECHANISM. <see cref="Handler_EndToEnd"/> is the informational full-request-path number.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="Naive_ManagedProjection"/> — the anti-pattern baseline: the three matched collections are held as
/// managed <c>List&lt;string&gt;</c> / POCOs (the administered ids and the credential (sourceName, environment) keys as
/// managed strings; the grantee echo reconstructed field-by-field from a managed view) and the whole body is built
/// through capturing builder closures (an outer object closure plus a per-item closure for every array element),
/// materialised once into a per-op workspace.</item>
/// <item><see cref="BytesNative_Projection"/> — the handler's projection technique over the SAME pre-seeded fixture: the
/// body built closure-free through context-threaded <c>Build&lt;TContext&gt;</c> (one context threaded through the outer
/// object and each array, no capturing lambda), each binding summary carried bytes-native (<c>Models.JsonString.From</c>
/// over the stored binding's raw accessors) and the grantee echoed through a congruent whole-document <c>From</c> wrap.
/// Materialised once into a per-op workspace, exactly as the baseline is.</item>
/// <item><see cref="Handler_EndToEnd"/> — the shipped handler: <c>HandleGetAccessGrantsAsync</c> end-to-end — the token
/// decoded into a pooled buffer, the grantee parsed as an owned document, the stores scanned, the matched collections
/// projected closure-free and the body handed to <c>Ok</c> for a single-pass materialisation. Informational (the full
/// request path), not the projection baseline.</item>
/// </list>
/// The projection arms build from pre-materialised managed collections (they do not re-read the stores), so the
/// baseline→bytes-native delta isolates the projection mechanism (managed-string + closure apparatus versus the
/// handler's context-threaded bytes-native path) rather than the store I/O.
/// </remarks>
[MemoryDiagnoser]
public class AccessGrantsProjectionBenchmarks
{
    private const int BindingCount = 4;

    // A resolved person grantee whose sole identity grant is {sub, u-1042}. The default policy resolves it to the
    // internal identity sys:sub=u-1042, which keys the administered-workflows index and the credential usage match.
    private const string GranteeJson =
        """{"kind":"person","value":"u-1042","identity":[{"dimension":"sub","value":"u-1042"}],"source":"observed","complete":true}""";

    // A representative stored binding (claim + claimValue + the three verb grants + order + audit fields), reused as each
    // of the naive arm's matched bindings — the same field set the real arm's stored bindings carry.
    private static readonly byte[] StoredBindingJson =
        """
        {
          "id": "binding-1",
          "claimType": "sub",
          "claimValue": "u-1042",
          "read": { "unrestricted": true },
          "write": { "unrestricted": false },
          "purge": { "unrestricted": false },
          "order": 10,
          "createdBy": "ops",
          "createdAt": "1970-01-01T00:00:00+00:00",
          "etag": "etag-1"
        }
        """u8.ToArray();

    private ArazzoControlPlaneSecurityHandler handler = null!;
    private string granteeToken = null!;

    // The pre-materialised managed fixtures the naive arm projects from.
    private ParsedJsonDocument<SecurityBindingDocument> bindingTemplate = null!;
    private SecurityBindingDocument[] naiveBindings = null!;
    private List<string> naiveAdministered = null!;
    private List<(string SourceName, string Environment)> naiveCredentials = null!;
    private GranteeView naiveGrantee;

    // The grantee source the bytes-native arm echoes: parsed once from the same grantee JSON and wrapped through a
    // congruent whole-document From (the handler's technique), the counterpart of the naive arm's managed reconstruction.
    private ParsedJsonDocument<Models.ResolvedGrantee> granteeDocument = null!;

    [GlobalSetup]
    public void Setup()
    {
        SecurityTagSet granteeIdentity = SecurityTagSet.FromTags([new SecurityTag("sys:sub", "u-1042")]);

        // Seed the stores the real arm reads: BindingCount matching bindings, two administered workflows, one usable credential.
        var policyStore = new InMemorySecurityPolicyStore();
        for (int i = 0; i < BindingCount; i++)
        {
            using ParsedJsonDocument<SecurityBindingDocument> draft = SecurityBindingDocument.Draft("sub", "u-1042", VerbGrant.Full, VerbGrant.None, VerbGrant.None, order: (i + 1) * 10);
            policyStore.AddBindingAsync(draft.RootElement, "ops", default).AsTask().GetAwaiter().GetResult().Dispose();
        }

        var credentialStore = new InMemorySourceCredentialStore();
        credentialStore.AddAsync(
            new SourceCredentialDefinition(
                "orders-api",
                "production",
                SourceCredentialKind.ApiKey,
                [new SecretReferenceDefinition("value", "keyvault://orders-apikey#1")],
                UsageTags: granteeIdentity),
            "ops",
            default).AsTask().GetAwaiter().GetResult().Dispose();

        var administratorStore = new InMemoryWorkflowAdministratorStore();
        using (JsonWorkspace seedWorkspace = JsonWorkspace.Create())
        {
            WorkflowAdministrators.AdministratorIdentity admin =
                WorkflowAdministrators.BuildIdentity(seedWorkspace, granteeIdentity, default, hasKind: false, default, hasLabel: false);
            foreach (string baseWorkflowId in new[] { "orders-workflow", "fulfilment-workflow" })
            {
                administratorStore.PutAsync(baseWorkflowId, [admin], WorkflowEtag.None, "ops", default).AsTask().GetAwaiter().GetResult().Dispose();
            }
        }

        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), new InMemoryWorkflowStateStore(), "ops", administrators: administratorStore);
        var policy = new PersistentRowSecurityPolicy(policyStore);
        var access = new ControlPlaneAccess(new HttpContextAccessor(), policy);
        this.handler = new ArazzoControlPlaneSecurityHandler(policyStore, policy, access, catalog, credentialStore);
        this.granteeToken = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(GranteeJson));

        // Pre-materialise the naive managed fixtures (one parsed binding template reused BindingCount times, and the
        // administered ids / credential keys / grantee echo as managed strings and a managed view).
        this.bindingTemplate = ParsedJsonDocument<SecurityBindingDocument>.Parse(StoredBindingJson);
        this.naiveBindings = new SecurityBindingDocument[BindingCount];
        for (int i = 0; i < BindingCount; i++)
        {
            this.naiveBindings[i] = this.bindingTemplate.RootElement;
        }

        this.naiveAdministered = ["orders-workflow", "fulfilment-workflow"];
        this.naiveCredentials = [("orders-api", "production")];
        this.naiveGrantee = new GranteeView("person", "u-1042", "observed", true, [("sub", "u-1042")]);

        // The bytes-native arm echoes the grantee through a whole-document From wrap (as the handler does), so parse the
        // same grantee JSON once into an owned document the arm re-wraps each op.
        this.granteeDocument = ParsedJsonDocument<Models.ResolvedGrantee>.Parse(Encoding.UTF8.GetBytes(GranteeJson));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.bindingTemplate.Dispose();
        this.granteeDocument.Dispose();
    }

    /// <summary>The anti-pattern baseline: the three matched collections held as managed <c>List&lt;string&gt;</c> / POCOs
    /// and the whole overview built through capturing builder closures; one materialisation into a per-op workspace.</summary>
    /// <returns>The binding count (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Naive_ManagedProjection()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Models.AccessGrantsOverview.Source body = Models.AccessGrantsOverview.Build((ref Models.AccessGrantsOverview.Builder b) => b.Create(
            administers: Models.AccessGrantsOverview.AccessGrantsAdministeredWorkflowArray.Build((ref Models.AccessGrantsOverview.AccessGrantsAdministeredWorkflowArray.Builder ab) =>
            {
                foreach (string baseWorkflowId in this.naiveAdministered)
                {
                    string id = baseWorkflowId;
                    ab.AddItem(Models.AccessGrantsAdministeredWorkflow.Build((ref Models.AccessGrantsAdministeredWorkflow.Builder ib) => ib.Create(baseWorkflowId: id)));
                }
            }),
            bindings: Models.AccessGrantsOverview.SecurityBindingSummaryArray.Build((ref Models.AccessGrantsOverview.SecurityBindingSummaryArray.Builder ab) =>
            {
                foreach (SecurityBindingDocument binding in this.naiveBindings)
                {
                    ab.AddItem(ClosureBindingSummary(binding));
                }
            }),
            credentialUsage: Models.AccessGrantsOverview.AccessGrantsCredentialUsageArray.Build((ref Models.AccessGrantsOverview.AccessGrantsCredentialUsageArray.Builder ab) =>
            {
                foreach ((string sourceName, string environment) in this.naiveCredentials)
                {
                    string s = sourceName;
                    string e = environment;
                    ab.AddItem(Models.AccessGrantsCredentialUsage.Build((ref Models.AccessGrantsCredentialUsage.Builder ib) => ib.Create(environment: e, sourceName: s)));
                }
            }),
            grantee: NaiveGrantee(this.naiveGrantee)));
        return Models.AccessGrantsOverview.CreateBuilder(workspace, body, 30).RootElement.Bindings.GetArrayLength();
    }

    /// <summary>The handler's projection technique over the SAME pre-seeded fixture: the overview built closure-free
    /// through context-threaded <c>Build&lt;TContext&gt;</c> (no capturing lambda), each binding summary carried
    /// bytes-native and the grantee echoed through a whole-document <c>From</c> wrap; one materialisation into a per-op
    /// workspace, exactly as the baseline.</summary>
    /// <returns>The binding count (prevents dead-code elimination).</returns>
    [Benchmark]
    public int BytesNative_Projection()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        var context = new ProjectionContext(this.naiveAdministered, this.naiveBindings, this.naiveCredentials);
        Models.AccessGrantsOverview.Source<ProjectionContext> body = Models.AccessGrantsOverview.Build(
            in context,
            administers: Models.AccessGrantsOverview.AccessGrantsAdministeredWorkflowArray.Build(in context, BuildAdministeredWorkflows),
            bindings: Models.AccessGrantsOverview.SecurityBindingSummaryArray.Build(in context, BuildAccessBindings),
            credentialUsage: Models.AccessGrantsOverview.AccessGrantsCredentialUsageArray.Build(in context, BuildCredentialUsages),
            grantee: (Models.ResolvedGrantee.Source)Models.ResolvedGrantee.From(this.granteeDocument.RootElement));
        return Models.AccessGrantsOverview.CreateBuilder(workspace, body, 30).RootElement.Bindings.GetArrayLength();
    }

    /// <summary>The shipped handler end-to-end: the token decoded + the grantee parsed as an owned document, the matched
    /// collections projected closure-free (context-threaded, bytes-native) and materialised once by <c>Ok</c>.
    /// Informational — the full request path, not the projection baseline.</summary>
    /// <returns>The binding count.</returns>
    [Benchmark]
    public int Handler_EndToEnd()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        var parameters = new GetAccessGrantsParams { Grantee = HeaderValueParser.ParseString<Models.JsonString>(this.granteeToken, workspace) };
        GetAccessGrantsResult result = this.handler.HandleGetAccessGrantsAsync(parameters, workspace).GetAwaiter().GetResult();
        return result.Body.GetProperty("bindings"u8).GetArrayLength();
    }

    // ── naive closure projection (managed strings + capturing builder closures) ────────────────────────────────────────

    private static Models.SecurityBindingSummary.Source ClosureBindingSummary(SecurityBindingDocument binding)
        => Models.SecurityBindingSummary.Build((ref Models.SecurityBindingSummary.Builder b) =>
        {
            Models.JsonString.Source claimValue = default;
            if (binding.ClaimValue.IsNotUndefined())
            {
                claimValue = Models.JsonString.From(binding.ClaimValue);
            }

            Models.JsonString.Source description = default;
            if (binding.Description.IsNotUndefined())
            {
                description = Models.JsonString.From(binding.Description);
            }

            Models.JsonString.Source lastUpdatedBy = default;
            if (binding.LastUpdatedBy.IsNotUndefined())
            {
                lastUpdatedBy = Models.JsonString.From(binding.LastUpdatedBy);
            }

            Models.JsonDateTime.Source lastUpdatedAt = default;
            if (binding.UpdatedAtValue is { } ua)
            {
                lastUpdatedAt = ua;
            }

            b.Create(
                claimType: Models.JsonString.From(binding.ClaimType),
                createdAt: binding.CreatedAtValue,
                createdBy: Models.JsonString.From(binding.CreatedBy),
                etag: Models.JsonString.From(binding.Etag),
                id: Models.JsonString.From(binding.Id),
                order: binding.OrderValue,
                purge: Models.VerbGrant.From(binding.Purge),
                read: Models.VerbGrant.From(binding.Read),
                write: Models.VerbGrant.From(binding.Write),
                claimValue: claimValue,
                description: description,
                lastUpdatedAt: lastUpdatedAt,
                lastUpdatedBy: lastUpdatedBy);
        });

    private static Models.ResolvedGrantee.Source NaiveGrantee(GranteeView view)
        => Models.ResolvedGrantee.Build((ref Models.ResolvedGrantee.Builder b) => b.Create(
            complete: view.Complete,
            identity: Models.ResolvedGrantee.AdministratorIdentityArray.Build((ref Models.ResolvedGrantee.AdministratorIdentityArray.Builder ab) =>
            {
                foreach ((string dimension, string value) in view.Identity)
                {
                    string d = dimension;
                    string v = value;
                    ab.AddItem(Models.AdministratorIdentity.Build((ref Models.AdministratorIdentity.Builder ib) => ib.Create(d, v)));
                }
            }),
            kind: view.Kind,
            source: view.Source,
            value: view.Value));

    // ── bytes-native projection (context-threaded static methods, no closures) ─────────────────────────────────────────
    // Replicates ArazzoControlPlaneSecurityHandler's private BuildAdministeredWorkflows / BuildAccessBindings /
    // BuildCredentialUsages / BuildBindingSummary (private to the handler), threading one context through the outer
    // object and each array so the build allocates no capturing delegate.

    private static void BuildAdministeredWorkflows(in ProjectionContext ctx, ref Models.AccessGrantsOverview.AccessGrantsAdministeredWorkflowArray.Builder array)
    {
        foreach (string baseWorkflowId in ctx.Administered)
        {
            array.AddItem(Models.AccessGrantsAdministeredWorkflow.Build(in baseWorkflowId, BuildAdministeredWorkflow));
        }
    }

    private static void BuildAdministeredWorkflow(in string baseWorkflowId, ref Models.AccessGrantsAdministeredWorkflow.Builder b)
        => b.Create(baseWorkflowId: baseWorkflowId);

    private static void BuildAccessBindings(in ProjectionContext ctx, ref Models.AccessGrantsOverview.SecurityBindingSummaryArray.Builder array)
    {
        foreach (SecurityBindingDocument binding in ctx.Bindings)
        {
            array.AddItem(Models.SecurityBindingSummary.Build(in binding, BuildBindingSummary));
        }
    }

    // The binding summary carried bytes-native (Models.JsonString.From over the stored binding's raw accessors; optional
    // scalars propagate Undefined straight through From — an absent field is omitted, no ternary), mirroring the handler.
    private static void BuildBindingSummary(in SecurityBindingDocument binding, ref Models.SecurityBindingSummary.Builder b)
        => b.Create(
            claimType: Models.JsonString.From(binding.ClaimType),
            createdAt: binding.CreatedAtValue,
            createdBy: Models.JsonString.From(binding.CreatedBy),
            etag: Models.JsonString.From(binding.Etag),
            id: Models.JsonString.From(binding.Id),
            order: binding.OrderValue,
            purge: Models.VerbGrant.From(binding.Purge),
            read: Models.VerbGrant.From(binding.Read),
            write: Models.VerbGrant.From(binding.Write),
            claimValue: Models.JsonString.From(binding.ClaimValue),
            description: Models.JsonString.From(binding.Description),
            lastUpdatedAt: Models.JsonDateTime.From(binding.LastUpdatedAt),
            lastUpdatedBy: Models.JsonString.From(binding.LastUpdatedBy));

    private static void BuildCredentialUsages(in ProjectionContext ctx, ref Models.AccessGrantsOverview.AccessGrantsCredentialUsageArray.Builder array)
    {
        foreach ((string sourceName, string environment) in ctx.Credentials)
        {
            var key = new CredentialKey(sourceName, environment);
            array.AddItem(Models.AccessGrantsCredentialUsage.Build(in key, BuildCredentialUsage));
        }
    }

    // Each key's strings encode to UTF-8 once inside the item's Create (a void mutate) rather than through a
    // Source-returning Build — a span-bearing temp would otherwise fail ref-safety escape analysis.
    private static void BuildCredentialUsage(in CredentialKey key, ref Models.AccessGrantsCredentialUsage.Builder b)
        => b.Create(environment: key.Environment, sourceName: key.SourceName);

    // The pre-seeded matched collections, threaded as one context through the overview build (no closure) — the same
    // fixture the baseline projects from, so the delta is purely the projection mechanism.
    private readonly ref struct ProjectionContext(
        IReadOnlyList<string> administered,
        IReadOnlyList<SecurityBindingDocument> bindings,
        IReadOnlyList<(string SourceName, string Environment)> credentials)
    {
        public IReadOnlyList<string> Administered { get; } = administered;

        public IReadOnlyList<SecurityBindingDocument> Bindings { get; } = bindings;

        public IReadOnlyList<(string SourceName, string Environment)> Credentials { get; } = credentials;
    }

    // One usable credential's (sourceName, environment) key, threaded per item so the usage build stays closure-free.
    private readonly struct CredentialKey(string sourceName, string environment)
    {
        public string SourceName { get; } = sourceName;

        public string Environment { get; } = environment;
    }

    // A managed view of the grantee the naive arm reconstructs the echo from (the anti-pattern's materialised strings).
    private readonly record struct GranteeView(string Kind, string Value, string Source, bool Complete, IReadOnlyList<(string Dimension, string Value)> Identity);
}