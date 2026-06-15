// <copyright file="SecretMaterial.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Live secret material resolved from a <see cref="SecretRef"/> by an <see cref="ISecretResolver"/> (design §13).
/// This is the one place secret bytes exist in process: it is <strong>memory-only</strong> — never persisted, logged,
/// or returned across the control-plane boundary — and is scrubbed when disposed (the runner disposes it once it has
/// built the derived auth artifact, retaining only that artifact where possible per §13.4).
/// </summary>
/// <remarks>
/// The bytes are held in a single mutable buffer so they can be zeroed on <see cref="Dispose"/>; prefer
/// <see cref="Utf8"/> (which keeps the secret in that scrubable buffer) over <see cref="Reveal"/> (which copies it into
/// an immutable, GC-managed <see cref="string"/> that cannot be scrubbed).
/// </remarks>
public sealed class SecretMaterial : IDisposable
{
    private byte[]? utf8;

    /// <summary>Initializes a new instance of the <see cref="SecretMaterial"/> class, taking ownership of the buffer.</summary>
    /// <param name="utf8">The UTF-8 secret bytes; the instance owns and will scrub this buffer.</param>
    public SecretMaterial(byte[] utf8)
        => this.utf8 = utf8 ?? throw new ArgumentNullException(nameof(utf8));

    /// <summary>Creates secret material from a string secret (the bytes are copied into an owned, scrubable buffer).</summary>
    /// <param name="value">The secret value.</param>
    /// <returns>The secret material.</returns>
    public static SecretMaterial FromString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new SecretMaterial(Encoding.UTF8.GetBytes(value));
    }

    /// <summary>Gets the secret as UTF-8 bytes in the scrubable buffer.</summary>
    /// <exception cref="ObjectDisposedException">The material has been scrubbed.</exception>
    public ReadOnlySpan<byte> Utf8 => this.utf8 ?? throw new ObjectDisposedException(nameof(SecretMaterial));

    /// <summary>Copies the secret into an immutable <see cref="string"/>. Prefer <see cref="Utf8"/> where possible —
    /// the returned string cannot be scrubbed and lives until the GC reclaims it.</summary>
    /// <returns>The secret value.</returns>
    /// <exception cref="ObjectDisposedException">The material has been scrubbed.</exception>
    public string Reveal() => Encoding.UTF8.GetString(this.Utf8);

    /// <summary>Scrubs the secret bytes (zeroes the buffer) and releases it.</summary>
    public void Dispose()
    {
        if (this.utf8 is { } buffer)
        {
            CryptographicOperations.ZeroMemory(buffer);
            this.utf8 = null;
        }
    }
}