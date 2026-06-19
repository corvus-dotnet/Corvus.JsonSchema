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
    /// <summary>
    /// The directory attributes this mapper reads to produce an identity, named in the <em>provider's</em> attribute
    /// notation (an LDAP attribute such as <c>departmentNumber</c>, a SCIM path such as
    /// <c>urn:ietf:params:scim:schemas:extension:enterprise:2.0:User:organization</c>, a Graph <c>$select</c> field). An
    /// adapter that can project (LDAP's search attribute list, SCIM <c>attributes</c>, Graph <c>$select</c>, Google
    /// <c>fields</c>) requests <strong>only</strong> these (plus the value/label attributes it needs itself), so it neither
    /// over-fetches over the wire nor over-materialises on parse. The default is empty, meaning <em>unspecified</em> — the
    /// adapter surfaces every attribute (the safe, general behaviour): correct for any mapper, at the cost of fetching more.
    /// Declaring it is opt-in and the declaration must match what <see cref="Map"/> actually reads (a mismatch projects an
    /// attribute away and yields a wrong identity — the same failure as misnaming it in <see cref="Map"/>), which is why it
    /// lives here, next to the reads.
    /// </summary>
    IReadOnlyCollection<string> RequiredAttributes => [];

    /// <summary>Projects a raw directory record to its resolved <c>sys:</c> identity, or <see langword="null"/> to drop it.</summary>
    /// <param name="record">The raw record fetched from the directory.</param>
    /// <returns>The resolved principal, or <see langword="null"/> if the record maps to no recognised <c>sys:</c> identity.</returns>
    ResolvedPrincipal? Map(DirectoryRecord record);
}

/// <summary>Factory helpers for <see cref="IDirectoryIdentityMapper"/>.</summary>
public static class DirectoryIdentityMapper
{
    /// <summary>Adapts a delegate to an <see cref="IDirectoryIdentityMapper"/> (the lightweight inline form) that surfaces every attribute (no projection).</summary>
    /// <param name="map">The projection (returns <see langword="null"/> to drop a record).</param>
    /// <returns>A mapper that calls <paramref name="map"/>.</returns>
    public static IDirectoryIdentityMapper FromFunc(Func<DirectoryRecord, ResolvedPrincipal?> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return new DelegateMapper(map, []);
    }

    /// <summary>
    /// Adapts a delegate to an <see cref="IDirectoryIdentityMapper"/> that declares the provider attributes it reads, so a
    /// projecting adapter fetches only those (see <see cref="IDirectoryIdentityMapper.RequiredAttributes"/>). Declare the
    /// same attributes <paramref name="map"/> reads, named in the provider's notation.
    /// </summary>
    /// <param name="requiredAttributes">The provider attribute tokens <paramref name="map"/> reads.</param>
    /// <param name="map">The projection (returns <see langword="null"/> to drop a record).</param>
    /// <returns>A mapper that calls <paramref name="map"/> and reports <paramref name="requiredAttributes"/>.</returns>
    public static IDirectoryIdentityMapper FromFunc(IReadOnlyCollection<string> requiredAttributes, Func<DirectoryRecord, ResolvedPrincipal?> map)
    {
        ArgumentNullException.ThrowIfNull(requiredAttributes);
        ArgumentNullException.ThrowIfNull(map);
        return new DelegateMapper(map, requiredAttributes);
    }

    private sealed class DelegateMapper(Func<DirectoryRecord, ResolvedPrincipal?> map, IReadOnlyCollection<string> requiredAttributes) : IDirectoryIdentityMapper
    {
        public IReadOnlyCollection<string> RequiredAttributes => requiredAttributes;

        public ResolvedPrincipal? Map(DirectoryRecord record) => map(record);
    }
}