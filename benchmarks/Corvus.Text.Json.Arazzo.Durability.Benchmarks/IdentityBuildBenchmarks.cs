// <copyright file="IdentityBuildBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Locks the allocation floor of building a <see cref="SecurityTagSet"/> from a UTF-8 source (design §16.5.4) — the
/// identity a directory adapter assembles per resolved principal. The source is the unescaped UTF-8 value bytes a JSON
/// reader already holds; the question is whether they make a u-turn through the managed heap on the way into the
/// identity. <see cref="FromTags_String"/> is the current path: materialize a <see cref="string"/> per value, build a
/// <see cref="SecurityTag"/> array, call <see cref="SecurityTagSet.FromTags"/>. <see cref="Build_Spans"/> is the
/// bytes-to-bytes path: <see cref="SecurityTagSet.Build{TState}"/> writes each value span straight into the pooled buffer.
/// Both produce the identical persisted bytes; only the construction allocations differ.
/// </summary>
public class IdentityBuildBenchmarks
{
    // The unescaped UTF-8 value bytes a directory response reader hands over (e.g. reader.GetUtf8String().Span).
    private static readonly byte[] TenantBytes = "acme"u8.ToArray();
    private static readonly byte[] SubBytes = "alice"u8.ToArray();

    private static readonly Values State = new(TenantBytes, SubBytes);

    /// <summary>The current path — materialise a string per value, build a SecurityTag array, then FromTags.</summary>
    /// <returns>The persisted byte length (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int FromTags_String()
    {
        string tenant = Encoding.UTF8.GetString(TenantBytes);
        string sub = Encoding.UTF8.GetString(SubBytes);
        return SecurityTagSet.FromTags([new SecurityTag("sys:tenant", tenant), new SecurityTag("sys:sub", sub)]).RawJson.Length;
    }

    /// <summary>The bytes-to-bytes path — write each value span straight into the pooled buffer, no string, no tag list.</summary>
    /// <returns>The persisted byte length.</returns>
    [Benchmark]
    public int Build_Spans()
        => SecurityTagSet.Build(
            in State,
            static (ref IdentityBuilder builder, in Values values) =>
            {
                builder.Add("sys:tenant"u8, values.Tenant.Span);
                builder.Add("sys:sub"u8, values.Sub.Span);
            }).RawJson.Length;

    private readonly struct Values(ReadOnlyMemory<byte> tenant, ReadOnlyMemory<byte> sub)
    {
        public ReadOnlyMemory<byte> Tenant { get; } = tenant;

        public ReadOnlyMemory<byte> Sub { get; } = sub;
    }
}