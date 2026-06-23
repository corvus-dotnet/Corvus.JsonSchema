// <copyright file="DescribeUsageScopeBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using BenchmarkDotNet.Attributes;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Allocation profile of <see cref="ControlPlaneRowSecurityPolicy.DescribeUsageScope"/> — the inverse usage-scope mapping
/// that turns a binding/administrator's stored internal <see cref="SecurityTagSet"/> back into operator-facing
/// (dimension, value) grants for the management API responses (credentials list, administrators, <c>whoami</c>, grantee
/// search — it runs per row of each). The default builds a <see cref="System.Collections.Generic.List{T}"/> of
/// <c>CredentialUsageGrant</c> records, and the <see cref="SecurityTagSet"/> enumerator materializes a managed
/// <see cref="string"/> for every tag key and value, plus a prefix-stripped substring per grant.
/// </summary>
[MemoryDiagnoser]
public class DescribeUsageScopeBenchmarks
{
    private static readonly ControlPlaneRowSecurityPolicy Policy = new BenchPolicy();

    // A representative stored identity: a multi-tag administrator/binding usage scope (tenant + subject + workflow), all
    // carrying the reserved internal prefix the inverse mapping strips.
    private static readonly SecurityTagSet UsageTags = SecurityTagSet.FromTags(
    [
        new SecurityTag("sys:tenant", "acme"),
        new SecurityTag("sys:sub", "alice@acme.example"),
        new SecurityTag("sys:workflow", "nightly-reconcile"),
    ]);

    /// <summary>The shipped default: a <c>List&lt;CredentialUsageGrant&gt;</c> + a managed key/value string per tag
    /// (enumerator) + a prefix-stripped substring per grant.</summary>
    /// <returns>The summed grant character count (defeats dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Materialize_ListOfGrants()
    {
        int n = 0;
        foreach (CredentialUsageGrant grant in Policy.DescribeUsageScope(UsageTags))
        {
            n += grant.Dimension.Length + grant.Value.Length;
        }

        return n;
    }

    /// <summary>The shipped span path: unescaped-UTF-8 enumeration + the policy's span-based prefix strip — no list, no
    /// per-tag managed strings.</summary>
    /// <returns>The summed grant byte count.</returns>
    [Benchmark]
    public int SpanEnumerated()
    {
        int n = 0;
        SecurityTagSet.Utf8Enumerator e = UsageTags.EnumerateUtf8();
        try
        {
            while (e.MoveNext())
            {
                if (Policy.TryDescribeUsageGrant(e.CurrentKey, out ReadOnlySpan<byte> dimension))
                {
                    n += dimension.Length + e.CurrentValue.Length;
                }
            }
        }
        finally
        {
            e.Dispose();
        }

        return n;
    }

    // The credentials-list path additionally re-wraps the binding's CTJ usageTags array into a SecurityTagSet per row: the
    // baseline's SecurityTagSet.CopyFrom allocates an owned byte[]; the shipped path wraps the array's raw UTF-8 with no
    // copy (FromOwnedJsonArray + JsonMarshal). So these arms include the CopyFrom byte[] the SecurityTagSet arms above do
    // not — the full per-row credentials cost.
    private static readonly byte[] BindingJson =
        """
        {
          "usageTags": [
            { "key": "sys:tenant", "value": "acme" },
            { "key": "sys:sub", "value": "alice@acme.example" },
            { "key": "sys:workflow", "value": "nightly-reconcile" }
          ]
        }
        """u8.ToArray();

    private static readonly ParsedJsonDocument<SourceCredentialBinding> BindingDocument = ParsedJsonDocument<SourceCredentialBinding>.Parse(BindingJson);

    /// <summary>The shipped credentials path before: per row, SecurityTagSet.CopyFrom (an owned byte[]) over the binding's
    /// usageTags, then the list + per-tag strings.</summary>
    /// <returns>The summed grant character count.</returns>
    [Benchmark]
    public int Credentials_CopyFromList()
    {
        int n = 0;
        foreach (CredentialUsageGrant grant in Policy.DescribeUsageScope(BindingDocument.RootElement.UsageTagsValue))
        {
            n += grant.Dimension.Length + grant.Value.Length;
        }

        return n;
    }

    /// <summary>The shipped credentials path: a non-owning SecurityTagSet view over the usageTags array's raw UTF-8 (no
    /// CopyFrom byte[]), enumerated as unescaped spans + the policy's span prefix strip — zero allocation.</summary>
    /// <returns>The summed grant byte count.</returns>
    [Benchmark]
    public int Credentials_SpanDirect()
    {
        int n = 0;
        SecurityTagSet usageTags = SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(BindingDocument.RootElement.UsageTags).Memory);
        SecurityTagSet.Utf8Enumerator e = usageTags.EnumerateUtf8();
        try
        {
            while (e.MoveNext())
            {
                if (Policy.TryDescribeUsageGrant(e.CurrentKey, out ReadOnlySpan<byte> dimension))
                {
                    n += dimension.Length + e.CurrentValue.Length;
                }
            }
        }
        finally
        {
            e.Dispose();
        }

        return n;
    }

    private sealed class BenchPolicy : ControlPlaneRowSecurityPolicy
    {
        public override AccessContext Resolve(ClaimsPrincipal? principal) => AccessContext.System;
    }
}
