// <copyright file="SecurityRulePage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// One keyset page of security rules (design §14.2): the rules for the page, ordered by <c>name</c>, plus an opaque
/// <see cref="NextPageToken"/> to fetch the next page (empty when this is the last page). The <see cref="Rules"/> are
/// pooled documents the caller owns, and the token (when present) is held in a pooled buffer — <see cref="Dispose"/> the
/// page once read to return both to the pool.
/// </summary>
/// <remarks>
/// The token is built bytes-native: <see cref="Create(PooledDocumentList{SecurityRuleDocument}, string)"/> Base64URL-encodes
/// the last row's name straight into a pooled buffer (no token string), exposed as <see cref="NextPageToken"/> UTF-8 the
/// handler writes verbatim into the response. The buffer outlives the synchronous response build and is returned on
/// <see cref="Dispose"/>.
/// </remarks>
public sealed class SecurityRulePage : IDisposable
{
    private byte[]? rentedToken;

    private SecurityRulePage(PooledDocumentList<SecurityRuleDocument> rules, ReadOnlyMemory<byte> nextPageToken, byte[]? rentedToken)
    {
        this.Rules = rules;
        this.NextPageToken = nextPageToken;
        this.rentedToken = rentedToken;
    }

    /// <summary>Gets the page's rules, ordered by <c>name</c>.</summary>
    public PooledDocumentList<SecurityRuleDocument> Rules { get; }

    /// <summary>Gets the opaque continuation token (UTF-8) to fetch the next page, or empty if this is the last page.</summary>
    public ReadOnlyMemory<byte> NextPageToken { get; }

    /// <summary>Creates a last page (no continuation token).</summary>
    /// <param name="rules">The page's rules.</param>
    /// <returns>The page.</returns>
    public static SecurityRulePage Create(PooledDocumentList<SecurityRuleDocument> rules)
        => new(rules, default, null);

    /// <summary>Creates a page with a continuation token, encoding the last row's <paramref name="lastName"/> straight into
    /// a pooled buffer (no intermediate token string).</summary>
    /// <param name="rules">The page's rules.</param>
    /// <param name="lastName">The last row's rule name (the keyset cursor the next page resumes after).</param>
    /// <returns>The page, owning the pooled token buffer.</returns>
    public static SecurityRulePage Create(PooledDocumentList<SecurityRuleDocument> rules, string lastName)
    {
        int maxLength = SecurityRuleContinuationToken.GetMaxEncodedLength(lastName);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            int written = SecurityRuleContinuationToken.EncodeToUtf8(lastName, buffer);
            return new SecurityRulePage(rules, buffer.AsMemory(0, written), buffer);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buffer);
            throw;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Rules.Dispose();
        if (this.rentedToken is { } buffer)
        {
            this.rentedToken = null;
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}