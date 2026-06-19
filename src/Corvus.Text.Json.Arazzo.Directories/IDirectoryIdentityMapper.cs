// <copyright file="IDirectoryIdentityMapper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Directories;

/// <summary>
/// A deployment-supplied projection from a raw <see cref="DirectoryRecord"/> to a <see cref="ResolvedPrincipal"/> — the
/// principal's exact deployment-stamped <c>sys:</c> identity (design §16.5.4). This is the one place a deployment encodes
/// how <em>its</em> directory attributes / group memberships become the <c>sys:</c> tags that grants key on (e.g.
/// <c>sys:tenant</c> from a <c>department</c> attribute, <c>sys:sub</c> from <c>entryUUID</c>) — the same
/// "deployment owns <c>sys:</c> stamping" seam as a row-security policy's internal-tag resolver.
/// </summary>
/// <remarks>
/// Protocol adapters fetch records and call the mapper; they never hardcode the <c>sys:</c> mapping. Returning
/// <see langword="null"/> drops a record (e.g. a principal that maps to no <c>sys:</c> identity the deployment
/// recognises), so an unmappable directory entry is silently excluded rather than offered as an inert grantee.
/// </remarks>
public interface IDirectoryIdentityMapper
{
    /// <summary>Projects a raw directory record to its resolved <c>sys:</c> identity, or <see langword="null"/> to drop it.</summary>
    /// <param name="record">The raw record fetched from the directory.</param>
    /// <returns>The resolved principal, or <see langword="null"/> if the record maps to no recognised <c>sys:</c> identity.</returns>
    ResolvedPrincipal? Map(DirectoryRecord record);
}

/// <summary>Factory helpers for <see cref="IDirectoryIdentityMapper"/>.</summary>
public static class DirectoryIdentityMapper
{
    /// <summary>Adapts a delegate to an <see cref="IDirectoryIdentityMapper"/> (the lightweight inline form).</summary>
    /// <param name="map">The projection (returns <see langword="null"/> to drop a record).</param>
    /// <returns>A mapper that calls <paramref name="map"/>.</returns>
    public static IDirectoryIdentityMapper FromFunc(Func<DirectoryRecord, ResolvedPrincipal?> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return new DelegateMapper(map);
    }

    private sealed class DelegateMapper(Func<DirectoryRecord, ResolvedPrincipal?> map) : IDirectoryIdentityMapper
    {
        public ResolvedPrincipal? Map(DirectoryRecord record) => map(record);
    }
}