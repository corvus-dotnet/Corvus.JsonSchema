// <copyright file="SecretRef.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// A parsed reference to one secret held in an external secret store (design §13): <c>scheme://locator[#version]</c>.
/// A <see cref="SecretRef"/> is a <em>pointer</em>, never the secret itself — only the runner's <c>ISecretResolver</c>
/// dereferences it. Rotation is by changing the reference (typically its <see cref="Version"/>), so the control plane
/// and the Arazzo database hold references only and never read secret material.
/// </summary>
/// <remarks>
/// <para>The recognized schemes are <c>keyvault</c> (Azure Key Vault), <c>awssm</c> (AWS Secrets Manager),
/// <c>vault</c> (HashiCorp Vault), <c>env</c> (an environment variable) and <c>file</c> (a file path — e.g. a mounted
/// Kubernetes secret). The scheme separator may be <c>:</c> or <c>://</c>; both parse identically.</para>
/// <para>The optional <c>#version</c> fragment pins a specific version/label of the secret; absent, the resolver
/// reads the current version.</para>
/// </remarks>
public readonly struct SecretRef : IEquatable<SecretRef>
{
    private SecretRef(string raw, SecretScheme scheme, string locator, string? version)
    {
        this.Raw = raw;
        this.Scheme = scheme;
        this.Locator = locator;
        this.Version = version;
    }

    /// <summary>Gets the original, unparsed reference string.</summary>
    public string Raw { get; }

    /// <summary>Gets the secret store scheme.</summary>
    public SecretScheme Scheme { get; }

    /// <summary>Gets the scheme-specific locator (the store path / variable name / file path), without the version.</summary>
    public string Locator { get; }

    /// <summary>Gets the pinned version/label, or <see langword="null"/> to read the current version.</summary>
    public string? Version { get; }

    /// <summary>Parses a secret reference, throwing if it is malformed or names an unknown scheme.</summary>
    /// <param name="reference">The reference (<c>scheme://locator[#version]</c>).</param>
    /// <returns>The parsed reference.</returns>
    /// <exception cref="FormatException">The reference is malformed or names an unknown scheme.</exception>
    public static SecretRef Parse(string reference)
    {
        ArgumentException.ThrowIfNullOrEmpty(reference);
        return TryParse(reference, out SecretRef result)
            ? result
            : throw new FormatException($"'{reference}' is not a valid secret reference (expected scheme://locator[#version] with a known scheme).");
    }

    /// <summary>Tries to parse a secret reference.</summary>
    /// <param name="reference">The reference (<c>scheme://locator[#version]</c>).</param>
    /// <param name="result">The parsed reference on success.</param>
    /// <returns><see langword="true"/> if the reference is well-formed and names a known scheme.</returns>
    public static bool TryParse([NotNullWhen(true)] string? reference, out SecretRef result)
    {
        result = default;
        if (string.IsNullOrEmpty(reference))
        {
            return false;
        }

        int colon = reference.IndexOf(':');
        if (colon <= 0)
        {
            return false;
        }

        if (!TryParseScheme(reference.AsSpan(0, colon), out SecretScheme scheme))
        {
            return false;
        }

        // Skip the scheme separator: ':' optionally followed by '//'.
        int start = colon + 1;
        if (reference.AsSpan(start).StartsWith("//"))
        {
            start += 2;
        }

        string remainder = reference[start..];
        if (remainder.Length == 0)
        {
            return false;
        }

        string locator = remainder;
        string? version = null;
        int hash = remainder.IndexOf('#');
        if (hash >= 0)
        {
            locator = remainder[..hash];
            version = remainder[(hash + 1)..];
            if (locator.Length == 0 || version.Length == 0)
            {
                return false;
            }
        }

        result = new SecretRef(reference, scheme, locator, version);
        return true;
    }

    /// <summary>Validates that <paramref name="utf8"/> is a well-formed secret reference naming a known scheme, reading
    /// the value's UTF-8 directly with no string allocation — the bytes-to-bytes counterpart of
    /// <see cref="TryParse(string?, out SecretRef)"/> used to validate a draft binding's references at the write
    /// boundary (so a malformed or accidental inline-secret value is rejected without materialising it).</summary>
    /// <param name="utf8">The candidate reference as UTF-8 (<c>scheme://locator[#version]</c>).</param>
    /// <returns><see langword="true"/> if it is well-formed and names a known scheme.</returns>
    public static bool IsWellFormed(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty)
        {
            return false;
        }

        int colon = utf8.IndexOf((byte)':');
        if (colon <= 0 || !IsKnownScheme(utf8[..colon]))
        {
            return false;
        }

        // Skip the scheme separator: ':' optionally followed by '//'.
        int start = colon + 1;
        if (utf8[start..].StartsWith("//"u8))
        {
            start += 2;
        }

        ReadOnlySpan<byte> remainder = utf8[start..];
        if (remainder.IsEmpty)
        {
            return false;
        }

        int hash = remainder.IndexOf((byte)'#');
        if (hash < 0)
        {
            return true; // locator only, already known non-empty
        }

        // A '#': both the locator and the version fragment must be non-empty.
        return hash > 0 && hash < remainder.Length - 1;
    }

    /// <inheritdoc/>
    public bool Equals(SecretRef other) => string.Equals(this.Raw, other.Raw, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SecretRef other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => this.Raw is null ? 0 : StringComparer.Ordinal.GetHashCode(this.Raw);

    /// <inheritdoc/>
    public override string ToString() => this.Raw ?? string.Empty;

    private static bool IsKnownScheme(ReadOnlySpan<byte> token) =>
        token.SequenceEqual("keyvault"u8)
        || token.SequenceEqual("awssm"u8)
        || token.SequenceEqual("vault"u8)
        || token.SequenceEqual("env"u8)
        || token.SequenceEqual("file"u8);

    private static bool TryParseScheme(ReadOnlySpan<char> token, out SecretScheme scheme)
    {
        if (token.Equals("keyvault", StringComparison.Ordinal))
        {
            scheme = SecretScheme.KeyVault;
            return true;
        }

        if (token.Equals("awssm", StringComparison.Ordinal))
        {
            scheme = SecretScheme.AwsSecretsManager;
            return true;
        }

        if (token.Equals("vault", StringComparison.Ordinal))
        {
            scheme = SecretScheme.HashiCorpVault;
            return true;
        }

        if (token.Equals("env", StringComparison.Ordinal))
        {
            scheme = SecretScheme.Environment;
            return true;
        }

        if (token.Equals("file", StringComparison.Ordinal))
        {
            scheme = SecretScheme.File;
            return true;
        }

        scheme = default;
        return false;
    }
}