// <copyright file="GoogleResponseParseBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Directories.Google;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Locks the allocation floor of <see cref="GooglePrincipalDirectory"/>'s Directory API response parse (design §16.5.4) —
/// the per-search hot operation once the access token is cached. The string path flattens each entity and builds the
/// identity through <c>FromTags</c> (a string per value); the bytes-to-bytes path (a span mapper) captures only the value /
/// label + the declared attribute as UTF-8 and writes the identity straight into a pooled buffer, applying a span transform
/// (slicing the leading <c>/</c> off <c>orgUnitPath</c>) with no string at all.
/// </summary>
public class GoogleResponseParseBenchmarks
{
    private static readonly GoogleResource UsersResource = GoogleResource.Users;

    private static readonly byte[] UsersBody = Encoding.UTF8.GetBytes(
        """
        {"kind":"admin#directory#users","users":[
          {"primaryEmail":"alice","name":{"givenName":"Alice","familyName":"Smith","fullName":"Alice Smith"},"id":"id-alice","orgUnitPath":"/acme","isAdmin":false,"suspended":false},
          {"primaryEmail":"albert","name":{"givenName":"Albert","familyName":"Jones","fullName":"Albert Jones"},"id":"id-albert","orgUnitPath":"/acme","isAdmin":false,"suspended":false},
          {"primaryEmail":"bob","name":{"givenName":"Bob","familyName":"Brown","fullName":"Bob Brown"},"id":"id-bob","orgUnitPath":"/globex","isAdmin":false,"suspended":false},
          {"primaryEmail":"carol","name":{"givenName":"Carol","familyName":"White","fullName":"Carol White"},"id":"id-carol","orgUnitPath":"/acme","isAdmin":false,"suspended":false},
          {"primaryEmail":"dave","name":{"givenName":"Dave","familyName":"Green","fullName":"Dave Green"},"id":"id-dave","orgUnitPath":"/globex","isAdmin":false,"suspended":false}
        ]}
        """);

    private static readonly DirectoryPrincipalProjector Projector = new(
        DirectoryIdentityMapper.FromFunc(static record => record.Kind switch
        {
            GranteeKind.Person => new ResolvedPrincipal(GranteeKind.Person, record.Id, record.DisplayName, SecurityTagSet.FromTags([new SecurityTag("sys:tenant", (record.Attribute("orgUnitPath") ?? string.Empty).TrimStart('/')), new SecurityTag("sys:sub", record.Id)])),
            _ => (ResolvedPrincipal?)null,
        }),
        "bench-issuer");

    // The span (bytes-to-bytes) mapper — writes the identity from the record's UTF-8 spans, slicing the leading '/' off
    // orgUnitPath in spans (the transform the user pushed for: no string even for a transform).
    private static readonly DirectoryPrincipalProjector SpanProjector = new(
        DirectorySpanIdentityMapper.FromIdentity(
            ["orgUnitPath"],
            static (DirectoryRecordView record, ref IdentityBuilder identity) =>
            {
                ReadOnlySpan<byte> orgUnit = record.AttributeUtf8("orgUnitPath"u8);
                if (orgUnit.Length > 0 && orgUnit[0] == (byte)'/')
                {
                    orgUnit = orgUnit[1..];
                }

                identity.Add("sys:tenant"u8, orgUnit);
                identity.Add("sys:sub"u8, record.ValueUtf8);
                return true;
            }),
        "bench-issuer");

    // A fixed ambient-dimension provider (§16.5.5) adding sys:region=eu — stands in for a request-context tenant/region
    // resolved per request, so the benchmark can exercise ambient stamping without an HttpContext. The two *_Ambient
    // variants prove the ambient append preserves each path's allocation property: the span path stays bytes-to-bytes
    // (only the one extra dimension's bytes), the string path pays one more FromTags.
    private static readonly IAmbientIdentityDimensions Ambient = new StaticAmbientIdentityDimensions([new SecurityTag("sys:region", "eu")]);

    private static readonly DirectoryPrincipalProjector ProjectorAmbient = new(
        DirectoryIdentityMapper.FromFunc(static record => record.Kind switch
        {
            GranteeKind.Person => new ResolvedPrincipal(GranteeKind.Person, record.Id, record.DisplayName, SecurityTagSet.FromTags([new SecurityTag("sys:tenant", (record.Attribute("orgUnitPath") ?? string.Empty).TrimStart('/')), new SecurityTag("sys:sub", record.Id)])),
            _ => (ResolvedPrincipal?)null,
        }),
        "bench-issuer",
        Ambient);

    private static readonly DirectoryPrincipalProjector SpanProjectorAmbient = new(
        DirectorySpanIdentityMapper.FromIdentity(
            ["orgUnitPath"],
            static (DirectoryRecordView record, ref IdentityBuilder identity) =>
            {
                ReadOnlySpan<byte> orgUnit = record.AttributeUtf8("orgUnitPath"u8);
                if (orgUnit.Length > 0 && orgUnit[0] == (byte)'/')
                {
                    orgUnit = orgUnit[1..];
                }

                identity.Add("sys:tenant"u8, orgUnit);
                identity.Add("sys:sub"u8, record.ValueUtf8);
                return true;
            }),
        "bench-issuer",
        Ambient);

    /// <summary>The string path — flatten each entity and build the identity through FromTags (a string per value).</summary>
    /// <returns>The resolved-principal count (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int ParseUsers() => GooglePrincipalDirectory.ProjectResponse(GranteeKind.Person, UsersResource, UsersBody, 10, Projector).Count;

    /// <summary>The bytes-to-bytes path — a span mapper captures only the value + orgUnitPath and writes the identity (with the '/' span transform) straight from spans.</summary>
    /// <returns>The resolved-principal count.</returns>
    [Benchmark]
    public int ParseUsers_Span() => GooglePrincipalDirectory.ProjectResponseSpan(GranteeKind.Person, UsersResource, UsersBody, 10, SpanProjector).Count;

    /// <summary>The string path with an ambient dimension (§16.5.5) stamped — proves the ambient append on the string path costs only one more FromTags pass.</summary>
    /// <returns>The resolved-principal count.</returns>
    [Benchmark]
    public int ParseUsers_Ambient() => GooglePrincipalDirectory.ProjectResponse(GranteeKind.Person, UsersResource, UsersBody, 10, ProjectorAmbient).Count;

    /// <summary>The bytes-to-bytes path with an ambient dimension (§16.5.5) stamped — proves the ambient append stays in spans (no string u-turn), adding only the one dimension's bytes.</summary>
    /// <returns>The resolved-principal count.</returns>
    [Benchmark]
    public int ParseUsers_Span_Ambient() => GooglePrincipalDirectory.ProjectResponseSpan(GranteeKind.Person, UsersResource, UsersBody, 10, SpanProjectorAmbient).Count;
}