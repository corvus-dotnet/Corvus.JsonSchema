// <copyright file="CredentialSummaryProjectionBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Allocation profile of the credential-summary LIST projection (design §13): a page of stored
/// <see cref="SourceCredentialBinding"/>s (each with secretRefs, config, managementTags and a usage tag) is projected
/// into the <c>CredentialBindingList</c> response body and materialised into the response workspace exactly as the
/// handler does (the terminal <c>ListCredentialsResult.Ok</c> step — a single <c>CreateBuilder</c> materialisation — is
/// modelled in both arms). Both arms emit the identical response bytes and perform the identical non-congruent
/// transforms (the derived <c>credentialStatus</c> and the inverse-mapped <c>usageGrants</c> are fixed constants,
/// computed once in setup and fed to both arms); only the projection MECHANISM differs, so the delta isolates the
/// closure-vs-context cost.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="PerItemClosure"/> — the pre-refactor shape: the list body, each summary, the four nested arrays
/// (secretRefs/config/managementTags/usageGrants) and each leaf item are built through capturing builder closures (an
/// outer summary closure per binding + four nested array closures + per-item closures). One materialisation.</item>
/// <item><see cref="ContextThreaded"/> — the shipped shape: the whole body is a context-threaded
/// <c>CredentialBindingList.Source&lt;TContext&gt;</c> (the generated <c>Build&lt;TContext&gt;</c> + static build
/// methods, the per-projection and per-item state carried in ref-struct contexts — no closure) materialised once via
/// the generic <c>CreateBuilder&lt;TContext&gt;</c> (the <c>Ok&lt;TContext&gt;</c> one-pass boundary).</item>
/// </list>
/// </remarks>
[MemoryDiagnoser]
public class CredentialSummaryProjectionBenchmarks
{
    private const int PageSize = 10;

    // A representative stored binding: secretRefs + config + managementTags + a usage tag, plus optional fields.
    private static readonly byte[] StoredJson =
        """
        {
          "id": "petstore@production",
          "sourceName": "petstore",
          "environment": "production",
          "authKind": "apiKey",
          "secretRefs": [ { "name": "value", "ref": "keyvault://petstore-apikey#3" } ],
          "config": [ { "key": "parameterName", "value": "X-Api-Key" } ],
          "managementTags": [ { "key": "sys:tenant", "value": "acme" } ],
          "usageTags": [ { "key": "sys:workflow", "value": "nightly-reconcile" } ],
          "description": "Pet store API key.",
          "createdBy": "alice",
          "createdAt": "1970-01-01T00:00:00+00:00",
          "lastUpdatedBy": "bob",
          "lastUpdatedAt": "1970-01-01T08:00:00+00:00",
          "etag": "etag-1"
        }
        """u8.ToArray();

    // The non-congruent transforms are computed once in setup and held constant across both arms (so the measured delta
    // is purely the closure-vs-context mechanism, not the derive/describe work): the derived credentialStatus and the
    // operator-facing usageGrants the stored usage scope describes back to (the shared row-security policy leaf).
    private static readonly CredentialStatus Status = CredentialStatus.Valid;

    private static readonly IReadOnlyList<CredentialUsageGrant> DescribedGrants = [new CredentialUsageGrant("sys:workflow", "nightly-reconcile")];

    private ParsedJsonDocument<SourceCredentialBinding> document = null!;
    private SourceCredentialBinding[] bindings = null!;
    private JsonWorkspace workspace = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.document = ParsedJsonDocument<SourceCredentialBinding>.Parse(StoredJson);
        this.bindings = new SourceCredentialBinding[PageSize];
        for (int i = 0; i < PageSize; i++)
        {
            this.bindings[i] = this.document.RootElement;
        }

        this.workspace = JsonWorkspace.Create();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.document.Dispose();
        this.workspace.Dispose();
    }

    /// <summary>The pre-refactor shape: the list, each summary, the four nested arrays and each leaf item are built
    /// through capturing builder closures; one materialisation into the response workspace.</summary>
    /// <returns>The summary count (defeats dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int PerItemClosure()
    {
        Models.CredentialBindingList.Source body = Models.CredentialBindingList.Build((ref Models.CredentialBindingList.Builder b) => b.Create(
            credentials: Models.CredentialBindingList.CredentialBindingSummaryArray.Build((ref Models.CredentialBindingList.CredentialBindingSummaryArray.Builder ab) =>
            {
                foreach (SourceCredentialBinding binding in this.bindings)
                {
                    ab.AddItem(ClosureSummary(binding));
                }
            })));
        return Models.CredentialBindingList.CreateBuilder(this.workspace, body, 30).RootElement.Credentials.GetArrayLength();
    }

    /// <summary>The shipped shape: the whole body is a context-threaded <c>CredentialBindingList.Source&lt;TContext&gt;</c>
    /// (no closure) materialised once via the generic <c>CreateBuilder&lt;TContext&gt;</c> (the <c>Ok&lt;TContext&gt;</c>
    /// one-pass boundary).</summary>
    /// <returns>The summary count.</returns>
    [Benchmark]
    public int ContextThreaded()
    {
        var ctx = new ListContext(this.bindings);
        Models.CredentialBindingList.Source<ListContext> body = Models.CredentialBindingList.Build(
            in ctx,
            credentials: Models.CredentialBindingList.CredentialBindingSummaryArray.Build(in ctx, BuildSummaries));
        return Models.CredentialBindingList.CreateBuilder(this.workspace, in body, 30).RootElement.Credentials.GetArrayLength();
    }

    // ── pre-refactor closure projection ────────────────────────────────────────────────────────────────────────────

    private static Models.CredentialBindingSummary.Source ClosureSummary(SourceCredentialBinding binding)
        => Models.CredentialBindingSummary.Build((ref Models.CredentialBindingSummary.Builder b) =>
        {
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
            if (binding.LastUpdatedAtValue is { } ua)
            {
                lastUpdatedAt = ua;
            }

            Models.JsonDateTime.Source expiresAt = default;
            if (binding.ExpiresAtOrNull is { } ea)
            {
                expiresAt = ea;
            }

            Models.JsonDateTime.Source rotatedAt = default;
            if (binding.RotatedAtOrNull is { } ra)
            {
                rotatedAt = ra;
            }

            Models.CredentialBindingSummary.CredentialConfigEntryArray.Source config = default;
            if (binding.Config.IsNotUndefined() && binding.Config.GetArrayLength() > 0)
            {
                config = Models.CredentialBindingSummary.CredentialConfigEntryArray.Build((ref Models.CredentialBindingSummary.CredentialConfigEntryArray.Builder ab) =>
                {
                    foreach (SourceCredentialBinding.CredentialConfigEntry entry in binding.Config.EnumerateArray())
                    {
                        ab.AddItem(ClosureConfigEntry(entry));
                    }
                });
            }

            Models.CredentialBindingSummary.CredentialSecurityTagArray.Source managementTags = default;
            if (!binding.ManagementTagsValue.IsEmpty)
            {
                managementTags = Models.CredentialBindingSummary.CredentialSecurityTagArray.Build((ref Models.CredentialBindingSummary.CredentialSecurityTagArray.Builder ab) =>
                {
                    foreach (SecurityTag tag in binding.ManagementTagsValue)
                    {
                        ab.AddItem(ClosureSecurityTag(tag));
                    }
                });
            }

            Models.CredentialUsageGrantee.Source usageGrantee = default;
            if (DescribedGrants.Count > 0)
            {
                usageGrantee = Models.CredentialUsageGrantee.Build((ref Models.CredentialUsageGrantee.Builder gb) => gb.Create(
                    identity: Models.CredentialUsageGrantee.CredentialUsageGrantArray.Build((ref Models.CredentialUsageGrantee.CredentialUsageGrantArray.Builder ab) =>
                    {
                        foreach (CredentialUsageGrant grant in DescribedGrants)
                        {
                            ab.AddItem(ClosureUsageGrant(grant));
                        }
                    }),
                    kind: "workflow",
                    label: "Nightly reconcile"));
            }

            b.Create(
                authKind: binding.AuthKindValue.ToJsonToken(),
                createdAt: binding.CreatedAtValue,
                createdBy: Models.JsonString.From(binding.CreatedBy),
                credentialStatus: StatusToken(Status),
                environment: Models.JsonString.From(binding.Environment),
                etag: Models.JsonString.From(binding.Etag),
                id: Models.JsonString.From(binding.Id),
                secretRefs: ClosureSecretRefs(binding),
                sourceName: Models.JsonString.From(binding.SourceName),
                config: config,
                description: description,
                expiresAt: expiresAt,
                lastUpdatedAt: lastUpdatedAt,
                lastUpdatedBy: lastUpdatedBy,
                managementTags: managementTags,
                rotatedAt: rotatedAt,
                usageGrantee: usageGrantee);
        });

    private static Models.CredentialBindingSummary.SecretReferenceArray.Source ClosureSecretRefs(SourceCredentialBinding binding)
        => Models.CredentialBindingSummary.SecretReferenceArray.Build((ref Models.CredentialBindingSummary.SecretReferenceArray.Builder ab) =>
        {
            foreach (SourceCredentialBinding.SecretReference reference in binding.SecretRefs.EnumerateArray())
            {
                ab.AddItem(Models.SecretReference.Build((ref Models.SecretReference.Builder sb) => sb.Create(Models.JsonString.From(reference.Name), Models.JsonString.From(reference.Ref))));
            }
        });

    private static Models.CredentialConfigEntry.Source ClosureConfigEntry(SourceCredentialBinding.CredentialConfigEntry entry)
        => Models.CredentialConfigEntry.Build((ref Models.CredentialConfigEntry.Builder b) => b.Create(Models.JsonString.From(entry.Key), Models.JsonString.From(entry.Value)));

    private static Models.CredentialSecurityTag.Source ClosureSecurityTag(SecurityTag tag)
        => Models.CredentialSecurityTag.Build((ref Models.CredentialSecurityTag.Builder b) => b.Create(tag.Key, tag.Value));

    private static Models.CredentialUsageGrant.Source ClosureUsageGrant(CredentialUsageGrant grant)
        => Models.CredentialUsageGrant.Build((ref Models.CredentialUsageGrant.Builder b) => b.Create(grant.Dimension, grant.Value));

    // ── shipped context-threaded projection (mirrors ArazzoControlPlaneCredentialsHandler) ─────────────────────────

    private static void BuildSummaries(in ListContext ctx, ref Models.CredentialBindingList.CredentialBindingSummaryArray.Builder array)
    {
        foreach (SourceCredentialBinding binding in ctx.Bindings)
        {
            var summaryCtx = new SummaryContext(binding);
            array.AddItem(Models.CredentialBindingSummary.Build(in summaryCtx, BuildSummary));
        }
    }

    private static void BuildSummary(in SummaryContext ctx, ref Models.CredentialBindingSummary.Builder b)
    {
        SourceCredentialBinding binding = ctx.Binding;

        bool hasConfig = binding.Config.IsNotUndefined() && binding.Config.GetArrayLength() > 0;
        bool hasManagementTags = !binding.ManagementTagsValue.IsEmpty;
        bool hasUsageGrants = DescribedGrants.Count > 0;

        b.Create(
            in ctx,
            authKind: binding.AuthKindValue.ToJsonToken(),
            createdAt: binding.CreatedAtValue,
            createdBy: Models.JsonString.From(binding.CreatedBy),
            credentialStatus: StatusToken(Status),
            environment: Models.JsonString.From(binding.Environment),
            etag: Models.JsonString.From(binding.Etag),
            id: Models.JsonString.From(binding.Id),
            secretRefs: Models.CredentialBindingSummary.SecretReferenceArray.Build(in ctx, BuildSecretRefs),
            sourceName: Models.JsonString.From(binding.SourceName),
            config: hasConfig ? Models.CredentialBindingSummary.CredentialConfigEntryArray.Build(in ctx, BuildConfig) : default,
            description: Models.JsonString.From(binding.Description),
            expiresAt: Models.JsonDateTime.From(binding.ExpiresAt),
            lastUpdatedAt: Models.JsonDateTime.From(binding.LastUpdatedAt),
            lastUpdatedBy: Models.JsonString.From(binding.LastUpdatedBy),
            managementTags: hasManagementTags ? Models.CredentialBindingSummary.CredentialSecurityTagArray.Build(in ctx, BuildManagementTags) : default,
            rotatedAt: Models.JsonDateTime.From(binding.RotatedAt),
            usageGrantee: hasUsageGrants ? Models.CredentialUsageGrantee.Build(in ctx, BuildUsageGrantee) : default);
    }

    private static void BuildSecretRefs(in SummaryContext ctx, ref Models.CredentialBindingSummary.SecretReferenceArray.Builder array)
    {
        foreach (SourceCredentialBinding.SecretReference reference in ctx.Binding.SecretRefs.EnumerateArray())
        {
            array.AddItem(Models.SecretReference.Build(in reference, BuildSecretRef));
        }
    }

    private static void BuildSecretRef(in SourceCredentialBinding.SecretReference reference, ref Models.SecretReference.Builder b)
        => b.Create(name: Models.JsonString.From(reference.Name), refValue: Models.JsonString.From(reference.Ref));

    private static void BuildConfig(in SummaryContext ctx, ref Models.CredentialBindingSummary.CredentialConfigEntryArray.Builder array)
    {
        foreach (SourceCredentialBinding.CredentialConfigEntry entry in ctx.Binding.Config.EnumerateArray())
        {
            array.AddItem(Models.CredentialConfigEntry.Build(in entry, BuildConfigEntry));
        }
    }

    private static void BuildConfigEntry(in SourceCredentialBinding.CredentialConfigEntry entry, ref Models.CredentialConfigEntry.Builder b)
        => b.Create(key: Models.JsonString.From(entry.Key), value: Models.JsonString.From(entry.Value));

    private static void BuildManagementTags(in SummaryContext ctx, ref Models.CredentialBindingSummary.CredentialSecurityTagArray.Builder array)
    {
        foreach (SecurityTag tag in ctx.Binding.ManagementTagsValue)
        {
            array.AddItem(Models.CredentialSecurityTag.Build(in tag, BuildSecurityTag));
        }
    }

    private static void BuildSecurityTag(in SecurityTag tag, ref Models.CredentialSecurityTag.Builder b)
        => b.Create(key: tag.Key, value: tag.Value);

    private static void BuildUsageGrantee(in SummaryContext ctx, ref Models.CredentialUsageGrantee.Builder grantee)
        => grantee.Create(
            in ctx,
            identity: Models.CredentialUsageGrantee.CredentialUsageGrantArray.Build(in ctx, BuildUsageGrants),
            kind: "workflow"u8,
            label: (Models.JsonString.Source)"Nightly reconcile"u8);

    private static void BuildUsageGrants(in SummaryContext ctx, ref Models.CredentialUsageGrantee.CredentialUsageGrantArray.Builder array)
    {
        foreach (CredentialUsageGrant grant in DescribedGrants)
        {
            array.AddItem(Models.CredentialUsageGrant.Build(in grant, BuildUsageGrant));
        }
    }

    private static void BuildUsageGrant(in CredentialUsageGrant grant, ref Models.CredentialUsageGrant.Builder b)
        => b.Create(dimension: grant.Dimension, value: grant.Value);

    private static string StatusToken(CredentialStatus status) => status switch
    {
        CredentialStatus.Valid => "valid",
        CredentialStatus.ExpiringSoon => "expiringSoon",
        CredentialStatus.Expired => "expired",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown credential status."),
    };

    private readonly ref struct ListContext(SourceCredentialBinding[] bindings)
    {
        public SourceCredentialBinding[] Bindings { get; } = bindings;
    }

    private readonly ref struct SummaryContext(SourceCredentialBinding binding)
    {
        public SourceCredentialBinding Binding { get; } = binding;
    }
}
