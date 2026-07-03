// <copyright file="SourceCredentialStoreBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the source-credential CREATE write seam (POST /credentials → <see cref="ISourceCredentialStore.AddAsync"/>),
/// end-to-end over the in-memory reference store (no driver / no I/O noise), starting from an already-parsed request body
/// — i.e. the per-request work the handler does between "the framework parsed the body" and "the store holds the bytes".
/// Both arms mirror the handler's lifecycle (build the seam input, add, then <c>using</c>-dispose the returned pooled
/// document exactly as <c>HandleCreateCredentialAsync</c> does) and each uses its own store so the unique
/// (sourceName, environment, tags) key does not 409 on repeat — no extra delete/cleanup op in the measured region.
/// <list type="bullet">
/// <item><see cref="Create_FromRecord"/> — the old record seam: the parsed body is read field-by-field into a
/// <see cref="SourceCredentialDefinition"/> record (a <see cref="List{T}"/> of <see cref="SecretReferenceDefinition"/>
/// plus a <see cref="List{T}"/> of <see cref="CredentialConfigDefinition"/>, each element a managed string transcoded
/// off the body's UTF-8) before the store re-serialises it — a bytes→string→bytes u-turn.</item>
/// <item><see cref="Create_FromDraft"/> — the new draft seam: the body's already-parsed JSON values are carried
/// bytes-to-bytes into a draft <see cref="SourceCredentialBinding"/> (no list, no per-field strings) which the store
/// completes with the server-stamped id/createdBy/createdAt/etag.</item>
/// </list>
/// The management/usage <see cref="SecurityTagSet"/>s the handler resolves from the caller's <c>AccessContext</c> are
/// precomputed in setup so the benchmark isolates the persistence seam (record vs draft), not the access-tag resolution.
/// </summary>
[MemoryDiagnoser]
public class SourceCredentialStoreBenchmarks
{
    private const string Actor = "bench";

    // A representative POST /credentials request body, already parsed (the HTTP framework parsed it before the handler
    // runs), so the benchmark measures the seam, not the parse. Held as a pooled document, disposed in cleanup.
    private static readonly byte[] BodyJson =
        """
        {
          "sourceName": "petstore",
          "environment": "production",
          "authKind": "apiKey",
          "secretRefs": [ { "name": "value", "ref": "keyvault://petstore-apikey#3" } ],
          "config": [ { "key": "parameterName", "value": "X-Api-Key" } ],
          "description": "Pet store API key."
        }
        """u8.ToArray();

    private ParsedJsonDocument<Models.CredentialBindingCreate> body = null!;
    private SecurityTagSet managementTags;
    private SecurityTagSet usageTags;

    [GlobalSetup]
    public void Setup()
    {
        this.body = ParsedJsonDocument<Models.CredentialBindingCreate>.Parse(BodyJson);

        // Resolved by the handler from the caller's AccessContext (internal tenant tag + usage grants); precomputed here
        // so the measured region is the persistence seam, not the access-tag resolution (which both seams share).
        this.managementTags = SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "contoso")]);
        this.usageTags = SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "contoso")]);
    }

    [GlobalCleanup]
    public void Cleanup() => this.body.Dispose();

    /// <summary>The old record seam: read the parsed body into a <see cref="SourceCredentialDefinition"/> record (two
    /// lists + per-field strings), then persist it (the store re-serialises the record to the document).</summary>
    [Benchmark(Baseline = true)]
    public void Create_FromRecord()
    {
        var store = new InMemorySourceCredentialStore();
        Models.CredentialBindingCreate b = this.body.RootElement;

        var secretRefs = new List<SecretReferenceDefinition>();
        foreach (Models.SecretReference reference in b.SecretRefs.EnumerateArray())
        {
            secretRefs.Add(new SecretReferenceDefinition((string)reference.Name, (string)reference.Ref));
        }

        List<CredentialConfigDefinition>? config = null;
        if (b.Config.IsNotUndefined() && b.Config.GetArrayLength() > 0)
        {
            config = new List<CredentialConfigDefinition>();
            foreach (Models.CredentialConfigEntry entry in b.Config.EnumerateArray())
            {
                config.Add(new CredentialConfigDefinition((string)entry.Key, (string)entry.Value));
            }
        }

        var definition = new SourceCredentialDefinition(
            (string)b.SourceName,
            (string)b.Environment,
            SourceCredentialKindExtensions.Parse((string)b.AuthKind),
            secretRefs,
            config,
            b.Description.IsNotUndefined() ? (string)b.Description : null,
            ManagementTags: this.managementTags,
            UsageTags: this.usageTags);

        // Dispose the returned pooled document exactly as HandleCreateCredentialAsync does (its `using`).
        using ParsedJsonDocument<SourceCredentialBinding> created =
            store.AddAsync(definition, Actor, default).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>The new draft seam: carry the body's already-parsed JSON values bytes-to-bytes into a draft binding (no
    /// list, no per-field strings), then persist it (the store completes it with the server-stamped fields).</summary>
    [Benchmark]
    public void Create_FromDraft()
    {
        var store = new InMemorySourceCredentialStore();
        Models.CredentialBindingCreate b = this.body.RootElement;

        using ParsedJsonDocument<SourceCredentialBinding> draft = SourceCredentialBinding.Draft(
            sourceName: (JsonElement)b.SourceName,
            environment: (JsonElement)b.Environment,
            authKind: (JsonElement)b.AuthKind,
            secretRefs: (JsonElement)b.SecretRefs,
            config: (JsonElement)b.Config,
            description: (JsonElement)b.Description,
            expiresAt: default,
            rotatedAt: default,
            managementTags: this.managementTags,
            usageTags: this.usageTags);

        // Dispose the returned pooled document exactly as HandleCreateCredentialAsync does (its `using`).
        using ParsedJsonDocument<SourceCredentialBinding> created =
            store.AddAsync(draft.RootElement, Actor, default).AsTask().GetAwaiter().GetResult();
    }
}
