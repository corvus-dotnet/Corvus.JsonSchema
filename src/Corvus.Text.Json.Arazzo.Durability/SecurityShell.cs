// <copyright file="SecurityShell.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A deployment's access-control shell (design §14.3): the mandated wrapper rule(s) every principal is
/// constrained by, plus the reserved key prefix marking deployment-owned <b>internal</b> security tags. The
/// shell makes the deployment's isolation inescapable — internal tags are deployment-set (not user-editable),
/// stripped from client responses, and rejected on user input, and the wrapper rule is ANDed with the
/// principal's user rule so a user rule can only narrow <em>within</em> the shell (defense in depth).
/// </summary>
/// <remarks>
/// The mandated rules typically reference internal tags against claims (e.g. <c>sys:tenant == $claim.tenant</c>),
/// so a shared-hosting deployment enforces tenant isolation on every query and single-row check regardless of
/// the user's own rule. The prefix and the wrapper are per-deployment configuration (like the auth scheme,
/// §14.1).
/// </remarks>
public sealed class SecurityShell
{
    /// <summary>The default reserved key prefix for deployment-internal security tags.</summary>
    public const string DefaultInternalPrefix = "sys:";

    private readonly IReadOnlyList<SecurityRule> mandatedRules;
    private byte[]? internalPrefixUtf8;

    /// <summary>Initializes a new instance of the <see cref="SecurityShell"/> class.</summary>
    /// <param name="mandatedRules">The wrapper rules every principal is constrained by (ANDed with the user rule); empty for a shell that only reserves the prefix.</param>
    /// <param name="internalPrefix">The reserved key prefix marking deployment-internal tags (default <see cref="DefaultInternalPrefix"/>).</param>
    public SecurityShell(IReadOnlyList<SecurityRule> mandatedRules, string internalPrefix = DefaultInternalPrefix)
    {
        ArgumentNullException.ThrowIfNull(mandatedRules);
        ArgumentException.ThrowIfNullOrEmpty(internalPrefix);
        this.mandatedRules = mandatedRules;
        this.InternalPrefix = internalPrefix;
    }

    /// <summary>Gets the reserved key prefix that marks deployment-internal security tags.</summary>
    public string InternalPrefix { get; }

    /// <summary>Gets the reserved internal prefix as UTF-8 — computed once and cached, so the span-based validation never
    /// re-encodes it per tag.</summary>
    private ReadOnlySpan<byte> InternalPrefixUtf8 => this.internalPrefixUtf8 ??= Encoding.UTF8.GetBytes(this.InternalPrefix);

    /// <summary>Whether a tag is a deployment-internal tag (its key carries the reserved prefix).</summary>
    /// <param name="tag">The security tag.</param>
    /// <returns><see langword="true"/> if the tag is internal.</returns>
    public bool IsInternal(SecurityTag tag) => tag.Key.StartsWith(this.InternalPrefix, StringComparison.Ordinal);

    /// <summary>Whether a tag key (as unescaped UTF-8) carries the reserved internal prefix — the span counterpart of
    /// <see cref="IsInternal(SecurityTag)"/>.</summary>
    /// <param name="keyUtf8">The tag key as unescaped UTF-8.</param>
    /// <returns><see langword="true"/> if the key carries the reserved prefix.</returns>
    public bool IsInternal(ReadOnlySpan<byte> keyUtf8) => keyUtf8.StartsWith(this.InternalPrefixUtf8);

    /// <summary>Validates user-supplied security tags string-free (the span counterpart of
    /// <see cref="ValidateUserTags(IReadOnlyList{SecurityTag})"/>): an end-user may not create a tag whose key carries the
    /// reserved internal prefix. The offending key is realized as a managed string only on the (rare) rejection path.</summary>
    /// <param name="userTags">The user-supplied security tags.</param>
    /// <exception cref="ArgumentException">A user tag uses the reserved internal prefix.</exception>
    public void ValidateUserTags(SecurityTagSet userTags)
    {
        SecurityTagSet.Utf8Enumerator e = userTags.EnumerateUtf8();
        try
        {
            while (e.MoveNext())
            {
                if (this.IsInternal(e.CurrentKey))
                {
                    throw new ArgumentException(
                        $"Security tag key '{Encoding.UTF8.GetString(e.CurrentKey)}' uses the reserved internal prefix '{this.InternalPrefix}'; that keyspace is owned by the deployment.",
                        nameof(userTags));
                }
            }
        }
        finally
        {
            e.Dispose();
        }
    }

    /// <summary>
    /// Validates user-supplied security tags: an end-user may not create a tag whose key carries the reserved
    /// internal prefix (that keyspace belongs to the deployment).
    /// </summary>
    /// <param name="userTags">The security tags supplied through the user-facing API.</param>
    /// <exception cref="ArgumentException">A user tag uses the reserved internal prefix.</exception>
    public void ValidateUserTags(IReadOnlyList<SecurityTag> userTags)
    {
        ArgumentNullException.ThrowIfNull(userTags);
        foreach (SecurityTag tag in userTags)
        {
            if (this.IsInternal(tag))
            {
                throw new ArgumentException(
                    $"Security tag key '{tag.Key}' uses the reserved internal prefix '{this.InternalPrefix}'; that keyspace is owned by the deployment.",
                    nameof(userTags));
            }
        }
    }

    /// <summary>Returns only the non-internal (user-visible) tags, for client-facing responses.</summary>
    /// <param name="tags">A row's full security tags (internal + user).</param>
    /// <returns>The user-visible tags; internal tags are stripped.</returns>
    public IReadOnlyList<SecurityTag> StripInternal(IReadOnlyList<SecurityTag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        var visible = new List<SecurityTag>(tags.Count);
        foreach (SecurityTag tag in tags)
        {
            if (!this.IsInternal(tag))
            {
                visible.Add(tag);
            }
        }

        return visible;
    }

    /// <summary>
    /// Builds the effective row-authorization filter for a principal: the deployment's mandated wrapper rules
    /// AND the principal's resolved user rules, evaluated against the principal's claims.
    /// </summary>
    /// <param name="userRules">The principal's resolved user rule(s) (empty restricts to just the wrapper).</param>
    /// <param name="claims">The principal's claims (name → values).</param>
    /// <returns>The composed filter the store applies (and single-row checks use). Deny-by-default: if both the
    /// wrapper and the user rules are empty the filter admits nothing — an unrestricted principal must be granted
    /// <see cref="AccessContext.System"/> (a <see langword="null"/> reach), not an empty filter.</returns>
    public SecurityFilter BuildFilter(IReadOnlyList<SecurityRule> userRules, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
    {
        ArgumentNullException.ThrowIfNull(userRules);
        return new SecurityFilter([.. this.mandatedRules, .. userRules], claims);
    }
}