// <copyright file="SourceCredentialKey.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The uniqueness key for a source credential binding, shared by every <see cref="ISourceCredentialStore"/> backend so
/// they agree on identity. A binding is identified by (sourceName, environment) plus a <em>discriminator</em> over its
/// immutable management and usage tag sets, so two bindings for the same source/environment that differ in either scope
/// coexist while an exact duplicate is rejected (design §13/§14.2).
/// </summary>
public static class SourceCredentialKey
{
    // A control character that cannot appear in a tag key or value, separating the two canonical tag sets so the
    // discriminator is unambiguous.
    private const char Separator = '\u0001';

    /// <summary>Computes the binding discriminator from its management and usage tag sets.</summary>
    /// <param name="managementTags">The binding's management tags.</param>
    /// <param name="usageTags">The binding's usage tags.</param>
    /// <returns>The canonical, order-independent discriminator string.</returns>
    public static string Discriminator(SecurityTagSet managementTags, SecurityTagSet usageTags)
        => $"{Canonical(managementTags)}{Separator}{Canonical(usageTags)}";

    // Canonical, order-independent string form of a single tag set.
    private static string Canonical(SecurityTagSet tags)
    {
        if (tags.IsEmpty)
        {
            return string.Empty;
        }

        List<SecurityTag> list = tags.ToList();
        list.Sort(static (a, b) =>
        {
            int byKey = string.CompareOrdinal(a.Key, b.Key);
            return byKey != 0 ? byKey : string.CompareOrdinal(a.Value, b.Value);
        });
        return string.Join(";", list.Select(t => $"{t.Key}={t.Value}"));
    }
}