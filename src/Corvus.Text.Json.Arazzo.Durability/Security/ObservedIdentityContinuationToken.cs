// <copyright file="ObservedIdentityContinuationToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Encodes and decodes the opaque continuation token used to keyset-page <see cref="IObservedIdentityStore.SearchAsync"/>
/// results (design §16.5.4). The contractual order is ascending <c>subjectValue</c> (the prefix-searched column), made
/// total by the <c>subjectKind</c> tie-breaker so two identities that share a value page deterministically. The token
/// carries the last row's <c>(subjectValue, subjectKind)</c> so the next request can seek strictly past it; it is opaque
/// and <b>backend-scoped</b> (a token is only ever presented back to the store that issued it).
/// </summary>
public static class ObservedIdentityContinuationToken
{
    // The null control char cannot appear in a subjectValue/subjectKind token, so it separates the two key parts.
    private static readonly char Separator = (char)0;

    /// <summary>Encodes the last page row's key into a continuation token.</summary>
    /// <param name="subjectValue">The last row's subject value.</param>
    /// <param name="subjectKind">The last row's subject kind token (the tie-breaker).</param>
    /// <returns>An opaque, URL-safe continuation token.</returns>
    public static string Encode(string subjectValue, string subjectKind)
    {
        ArgumentNullException.ThrowIfNull(subjectValue);
        ArgumentNullException.ThrowIfNull(subjectKind);
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(string.Concat(subjectValue, Separator.ToString(), subjectKind)));
    }

    /// <summary>Decodes a continuation token back to the <c>(subjectValue, subjectKind)</c> cursor to page after.</summary>
    /// <param name="token">A token from a previous page's <c>nextPageToken</c>, or <see langword="null"/>/empty for the first page.</param>
    /// <param name="cursor">The decoded cursor to page strictly after (exclusive).</param>
    /// <returns><see langword="true"/> if a cursor was decoded; <see langword="false"/> for the first page.</returns>
    /// <exception cref="FormatException">The token is not a valid continuation token.</exception>
    public static bool TryDecode(string? token, out (string SubjectValue, string SubjectKind) cursor)
    {
        cursor = default;
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token));
        }
        catch (FormatException ex)
        {
            throw new FormatException($"'{token}' is not a valid observed-identity page token.", ex);
        }

        string[] parts = decoded.Split(Separator);
        if (parts.Length != 2)
        {
            throw new FormatException($"'{token}' is not a valid observed-identity page token.");
        }

        cursor = (parts[0], parts[1]);
        return true;
    }
}