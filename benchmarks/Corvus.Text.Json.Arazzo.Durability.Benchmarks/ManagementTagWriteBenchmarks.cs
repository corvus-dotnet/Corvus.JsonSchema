// <copyright file="ManagementTagWriteBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Breaks down the per-request allocation of the credential-binding-write management-tag resolution
/// (<c>ArazzoControlPlaneCredentialsHandler</c> create/update): <c>ReadTags(body.ManagementTags)</c> →
/// <c>new List&lt;SecurityTag&gt;(InternalTags())</c> + <c>AddRange</c> → <c>SecurityTagSet.FromTags(merged)</c>. Each
/// element is isolated so the actual numbers (not estimates) are visible. The fixture is two operator-supplied management
/// tags plus one deployment-internal tag — a representative create.
/// </summary>
[MemoryDiagnoser]
public class ManagementTagWriteBenchmarks
{
    private static readonly byte[] BodyJson =
        """
        {
          "managementTags": [
            { "key": "team", "value": "platform-engineering" },
            { "key": "costCenter", "value": "cc-100423" }
          ]
        }
        """u8.ToArray();

    // The deployment-internal tags the policy stamps (GetInternalTags) — string-sourced upstream, replicated here as a
    // fixed list (the owner's tenant identity).
    private static readonly IReadOnlyList<SecurityTag> InternalTags = [new SecurityTag("sys:tenant", "acme")];

    private ParsedJsonDocument<Models.CredentialBindingCreate> body = null!;

    // Precomputed inputs so the per-element arms isolate exactly one step's allocation.
    private IReadOnlyList<SecurityTag> userManagement = null!;
    private List<SecurityTag> merged = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.body = ParsedJsonDocument<Models.CredentialBindingCreate>.Parse(BodyJson);
        this.userManagement = ReadTags(this.body.RootElement.ManagementTags);
        this.merged = new List<SecurityTag>(InternalTags);
        this.merged.AddRange(this.userManagement);
    }

    [GlobalCleanup]
    public void Cleanup() => this.body.Dispose();

    /// <summary>The whole current path: ReadTags + the merged list + FromTags.</summary>
    /// <returns>The tag count (defeats dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Full_ReadMergeFromTags()
    {
        IReadOnlyList<SecurityTag> user = ReadTags(this.body.RootElement.ManagementTags);
        var management = new List<SecurityTag>(InternalTags);
        management.AddRange(user);
        SecurityTagSet set = SecurityTagSet.FromTags(management);
        return set.IsEmpty ? 0 : 1;
    }

    /// <summary>The shipped path: validation reads a non-owning SecurityTagSet view (0-alloc) and the set is built directly
    /// from InternalTags + the user tags' UTF-8 spans — no ReadTags list, no merged list, just the genuine owned byte[].</summary>
    /// <returns>The tag count.</returns>
    [Benchmark]
    public int After_ViewBuildDirect()
    {
        SecurityTagSet userView = SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(this.body.RootElement.ManagementTags).Memory);
        var state = new MgmtState(InternalTags, userView);
        SecurityTagSet set = SecurityTagSet.Build(in state, WriteManagementTags);
        return set.IsEmpty ? 0 : 1;
    }

    /// <summary>Element 1: ReadTags — a List&lt;SecurityTag&gt; + a managed key and value string per tag.</summary>
    /// <returns>The tag count.</returns>
    [Benchmark]
    public int Part_ReadTags() => ReadTags(this.body.RootElement.ManagementTags).Count;

    /// <summary>Element 2: the merged list — new List&lt;SecurityTag&gt;(InternalTags) + AddRange(user).</summary>
    /// <returns>The tag count.</returns>
    [Benchmark]
    public int Part_MergeList()
    {
        var management = new List<SecurityTag>(InternalTags);
        management.AddRange(this.userManagement);
        return management.Count;
    }

    /// <summary>Element 3: FromTags — the owned byte[] the SecurityTagSet result carries (the genuine leaf).</summary>
    /// <returns>The tag count.</returns>
    [Benchmark]
    public int Part_FromTags()
    {
        SecurityTagSet set = SecurityTagSet.FromTags(this.merged);
        return set.IsEmpty ? 0 : 1;
    }

    // Verbatim copies of the handler's shipped write path, so the After arm measures the same code.
    private static void WriteManagementTags(ref IdentityBuilder builder, in MgmtState state)
    {
        Span<byte> keyBuffer = stackalloc byte[256];
        foreach (SecurityTag tag in state.InternalTags)
        {
            int written = Encoding.UTF8.GetBytes(tag.Key, keyBuffer);
            builder.Add(keyBuffer[..written], tag.Value);
        }

        SecurityTagSet.Utf8Enumerator e = state.UserTags.EnumerateUtf8();
        try
        {
            while (e.MoveNext())
            {
                builder.Add(e.CurrentKey, e.CurrentValue);
            }
        }
        finally
        {
            e.Dispose();
        }
    }

    // A verbatim copy of the handler's private ReadTags, so the benchmark measures the same code.
    private static List<SecurityTag> ReadTags(Models.CredentialBindingCreate.CredentialSecurityTagArray tags)
    {
        var list = new List<SecurityTag>();
        if (tags.IsNotUndefined())
        {
            foreach (Models.CredentialSecurityTag tag in tags.EnumerateArray())
            {
                list.Add(new SecurityTag((string)tag.Key, (string)tag.Value));
            }
        }

        return list;
    }

    private readonly ref struct MgmtState(IReadOnlyList<SecurityTag> internalTags, SecurityTagSet userTags)
    {
        public IReadOnlyList<SecurityTag> InternalTags { get; } = internalTags;

        public SecurityTagSet UserTags { get; } = userTags;
    }
}
