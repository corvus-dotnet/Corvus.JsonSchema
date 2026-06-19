// <copyright file="AmbientIdentityStamp.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Strips a provider's governed dimensions from an identity and re-stamps its resolved ambient values (design §16.5.5) —
/// the string-path counterpart of <c>DirectoryIssuer.Stamp</c>, generalised from one hard-coded <c>sys:iss</c> to the
/// set of mapper-immutable ambient dimensions an <see cref="IAmbientIdentityDimensions"/> provider governs. Used by every
/// <em>string</em> stamping moment (a directory string mapper via the projector, the non-directory
/// <c>ResolveGranteeIdentity</c>, and the runtime <c>GetInternalTags</c>) so a grantee resolved within a tenant context
/// and the caller in that context are stamped identically from the one provider.
/// </summary>
/// <remarks>
/// <strong>Mapper-immutable, fail-closed.</strong> A tag whose key the provider governs is removed from the upstream
/// output <em>even when the context resolves no value</em> for it, so a mapper can neither omit nor forge an ambient
/// dimension. When the provider is <see langword="null"/> (a deployment with no ambient dimensions) the input is
/// returned unchanged — zero allocation, behaviour identical to before.
/// </remarks>
public static class AmbientIdentityStamp
{
    /// <summary>
    /// Returns <paramref name="tags"/> with the provider's governed keys stripped and its resolved ambient tags appended;
    /// returns <paramref name="tags"/> unchanged when there is no provider, nothing governed, and nothing resolved.
    /// </summary>
    /// <param name="provider">The ambient-dimension provider, or <see langword="null"/> for none.</param>
    /// <param name="tags">The upstream identity tags.</param>
    /// <returns>The stamped tag list.</returns>
    public static IReadOnlyList<SecurityTag> Apply(IAmbientIdentityDimensions? provider, IReadOnlyList<SecurityTag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        if (provider is null)
        {
            return tags;
        }

        AmbientDimensionSet ambient = provider.Resolve();
        IReadOnlyCollection<string> governed = provider.GovernedKeys;
        if (ambient.IsEmpty && governed.Count == 0)
        {
            return tags;
        }

        var merged = new List<SecurityTag>(tags.Count + ambient.Tags.Count);
        foreach (SecurityTag tag in tags)
        {
            if (!Governs(governed, tag.Key))
            {
                merged.Add(tag);
            }
        }

        merged.AddRange(ambient.Tags);
        return merged;
    }

    /// <summary>
    /// Returns <paramref name="identity"/> with the provider's governed keys stripped and its resolved ambient tags
    /// appended; returns <paramref name="identity"/> unchanged when there is no provider, nothing governed, and nothing
    /// resolved.
    /// </summary>
    /// <param name="provider">The ambient-dimension provider, or <see langword="null"/> for none.</param>
    /// <param name="identity">The upstream identity set.</param>
    /// <returns>The stamped set.</returns>
    public static SecurityTagSet Apply(IAmbientIdentityDimensions? provider, SecurityTagSet identity)
    {
        if (provider is null)
        {
            return identity;
        }

        AmbientDimensionSet ambient = provider.Resolve();
        IReadOnlyCollection<string> governed = provider.GovernedKeys;
        if (ambient.IsEmpty && governed.Count == 0)
        {
            return identity;
        }

        List<SecurityTag> tags = identity.ToList();
        tags.RemoveAll(t => Governs(governed, t.Key));
        tags.AddRange(ambient.Tags);
        return SecurityTagSet.FromTags(tags);
    }

    private static bool Governs(IReadOnlyCollection<string> governed, string key)
    {
        foreach (string governedKey in governed)
        {
            if (string.Equals(governedKey, key, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}