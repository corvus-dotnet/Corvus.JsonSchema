// <copyright file="IDirectoryIdentitySpanMapper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.Directories;

/// <summary>
/// The bytes-to-bytes counterpart of <see cref="IDirectoryIdentityMapper"/> (design §16.5.4): a deployment writes a
/// principal's <c>sys:</c> identity straight into an <see cref="IdentityBuilder"/> from the captured UTF-8 spans of a
/// <see cref="DirectoryRecordView"/>, so no managed <see cref="string"/> is materialized per key or value. An adapter
/// whose source is UTF-8 (an HTTP/JSON directory) uses this when the supplied mapper implements it, and captures exactly
/// the declared <see cref="RequiredAttributes"/> (plus the grantee value/label it needs itself) — falling back to the
/// string <see cref="IDirectoryIdentityMapper"/> otherwise. The adapter owns <c>Kind</c>/<c>Value</c>/<c>Label</c> and
/// stamps the issuer; this contributes only the identity tags. It MUST NOT emit the reserved <c>sys:iss</c> tag (the
/// adapter stamps it).
/// </summary>
public interface IDirectoryIdentitySpanMapper
{
    /// <summary>Gets the provider attributes this mapper reads (in the adapter's flattened-key notation), so the adapter captures exactly those as spans; see <see cref="IDirectoryIdentityMapper.RequiredAttributes"/>.</summary>
    IReadOnlyCollection<string> RequiredAttributes { get; }

    /// <summary>Writes the record's <c>sys:</c> identity tags into <paramref name="identity"/> from its captured spans, or returns <see langword="false"/> to drop a record that maps to no recognised identity.</summary>
    /// <param name="record">The captured record view (a small readonly <see langword="ref"/> struct, passed by value).</param>
    /// <param name="identity">The identity builder to write tags into.</param>
    /// <returns><see langword="true"/> to keep the record; <see langword="false"/> to drop it.</returns>
    bool TryMapIdentity(DirectoryRecordView record, ref IdentityBuilder identity);
}

/// <summary>Writes a record's identity tags into <paramref name="identity"/>, or returns <see langword="false"/> to drop it — the delegate form of <see cref="IDirectoryIdentitySpanMapper.TryMapIdentity"/>.</summary>
/// <param name="record">The captured record view.</param>
/// <param name="identity">The identity builder.</param>
/// <returns><see langword="true"/> to keep, <see langword="false"/> to drop.</returns>
public delegate bool DirectoryIdentityAction(DirectoryRecordView record, ref IdentityBuilder identity);

/// <summary>Factory helpers for <see cref="IDirectoryIdentitySpanMapper"/>.</summary>
public static class DirectorySpanIdentityMapper
{
    /// <summary>
    /// Adapts a delegate to a span identity mapper (the bytes-to-bytes inline form). The result also implements
    /// <see cref="IDirectoryIdentityMapper"/> so it can be passed wherever a mapper is expected; its string
    /// <see cref="IDirectoryIdentityMapper.Map"/> throws, since a span mapper is meant for a UTF-8-sourced adapter's span
    /// path (use the string <see cref="DirectoryIdentityMapper.FromFunc(System.Func{DirectoryRecord, ResolvedPrincipal?})"/>
    /// for the string path).
    /// </summary>
    /// <param name="requiredAttributes">The provider attribute tokens <paramref name="map"/> reads (so the adapter captures exactly those).</param>
    /// <param name="map">Writes the identity tags from the record's spans (returns <see langword="false"/> to drop).</param>
    /// <returns>A mapper that calls <paramref name="map"/>.</returns>
    public static IDirectoryIdentityMapper FromIdentity(IReadOnlyCollection<string> requiredAttributes, DirectoryIdentityAction map)
    {
        ArgumentNullException.ThrowIfNull(requiredAttributes);
        ArgumentNullException.ThrowIfNull(map);
        return new SpanDelegateMapper(requiredAttributes, map);
    }

    private sealed class SpanDelegateMapper(IReadOnlyCollection<string> requiredAttributes, DirectoryIdentityAction map) : IDirectoryIdentityMapper, IDirectoryIdentitySpanMapper
    {
        public IReadOnlyCollection<string> RequiredAttributes => requiredAttributes;

        public bool TryMapIdentity(DirectoryRecordView record, ref IdentityBuilder identity) => map(record, ref identity);

        public ResolvedPrincipal? Map(DirectoryRecord record)
            => throw new NotSupportedException("This is a span identity mapper; use it with a UTF-8-sourced adapter's span path, or supply a string mapper via DirectoryIdentityMapper.FromFunc.");
    }
}