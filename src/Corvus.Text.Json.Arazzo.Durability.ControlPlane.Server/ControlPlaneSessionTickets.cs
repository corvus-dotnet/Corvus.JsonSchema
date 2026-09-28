// <copyright file="ControlPlaneSessionTickets.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Buffers.Text;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Keeps BFF session tickets on the server, in the host's <see cref="IDistributedCache"/>, so that signing out revokes
/// the session and a user can sign out everywhere (ADR 0075).
/// </summary>
/// <remarks>
/// <para>
/// The cookie carries only a random session key. The ticket, with its claims and the tokens the OIDC handler saved, is
/// serialized and data-protected under its own purpose before it reaches the cache, so the cache never holds a readable
/// token. Signing out removes it, so a copy of the cookie finds nothing.
/// </para>
/// <para>
/// A session's start is stamped when its ticket is first stored and kept through every renewal. A session older than
/// <see cref="ControlPlaneSessionTicketOptions.MaximumLifetime"/> is refused. Sign-out everywhere records, per subject,
/// the time it happened, and a session that began at or before that time is refused. The record is kept for the
/// maximum lifetime, after which every session it could refuse has ended anyway. No index of a subject's sessions is
/// kept.
/// </para>
/// <para>
/// The cache must not evict entries before they expire: an evicted revocation would let the sessions it ended back in.
/// Redis's default <c>maxmemory-policy</c>, <c>noeviction</c>, keeps them.
/// </para>
/// </remarks>
public sealed class ControlPlaneSessionTickets : ITicketStore, IControlPlaneSessionRevocation
{
    /// <summary>The authentication property recording when the session began.</summary>
    public const string SessionBeganKey = ".arazzo.session.began";

    private const string ProtectorPurpose = "Corvus.Text.Json.Arazzo.ControlPlane.SessionTickets";

    private readonly IDistributedCache cache;
    private readonly IDataProtector protector;
    private readonly TimeSpan maximumLifetime;
    private readonly string ticketPrefix;
    private readonly string epochPrefix;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="ControlPlaneSessionTickets"/> class.</summary>
    /// <param name="cache">The cache the host picked to hold the tickets.</param>
    /// <param name="dataProtection">Protects each ticket before it reaches the cache.</param>
    /// <param name="options">The maximum session lifetime and the cache key prefix.</param>
    /// <param name="timeProvider">The clock; defaults to <see cref="TimeProvider.System"/>.</param>
    public ControlPlaneSessionTickets(IDistributedCache cache, IDataProtectionProvider dataProtection, ControlPlaneSessionTicketOptions options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(dataProtection);
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaximumLifetime <= TimeSpan.Zero)
        {
            ServerThrowHelper.ThrowSessionMaximumLifetimeNotPositive(options.MaximumLifetime);
        }

        this.cache = cache;
        this.protector = dataProtection.CreateProtector(ProtectorPurpose);
        this.maximumLifetime = options.MaximumLifetime;
        this.ticketPrefix = options.KeyPrefix + "ticket:";
        this.epochPrefix = options.KeyPrefix + "epoch:";
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Gets the subject sign-out everywhere is keyed by: the principal's <see cref="ClaimTypes.NameIdentifier"/> claim,
    /// or its <c>sub</c> claim.
    /// </summary>
    /// <param name="principal">The signed-in principal.</param>
    /// <returns>The subject, or <see langword="null"/> when the principal names none.</returns>
    public static string? SubjectOf(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value is { Length: > 0 } nameIdentifier
            ? nameIdentifier
            : principal.FindFirst("sub")?.Value is { Length: > 0 } sub ? sub : null;
    }

    /// <inheritdoc/>
    public Task<string> StoreAsync(AuthenticationTicket ticket) => this.StoreAsync(ticket, CancellationToken.None);

    /// <inheritdoc/>
    public async Task<string> StoreAsync(AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        // Sign-out everywhere reaches a session through its subject, so a session with none is not begun.
        if (SubjectOf(ticket.Principal) is null)
        {
            ServerThrowHelper.ThrowSessionHasNoSubject();
        }

        ticket.Properties.Items.TryAdd(SessionBeganKey, this.timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
        string key = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        await this.WriteAsync(key, ticket, cancellationToken).ConfigureAwait(false);
        return key;
    }

    /// <inheritdoc/>
    public Task RenewAsync(string key, AuthenticationTicket ticket) => this.RenewAsync(key, ticket, CancellationToken.None);

    /// <inheritdoc/>
    public Task RenewAsync(string key, AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(ticket);
        return this.WriteAsync(key, ticket, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<AuthenticationTicket?> RetrieveAsync(string key) => this.RetrieveAsync(key, CancellationToken.None);

    /// <inheritdoc/>
    public async Task<AuthenticationTicket?> RetrieveAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        byte[]? stored = await this.cache.GetAsync(this.ticketPrefix + key, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return null;
        }

        AuthenticationTicket? ticket;
        try
        {
            ticket = TicketSerializer.Default.Deserialize(this.protector.Unprotect(stored));
        }
        catch (CryptographicException)
        {
            // Protected under a key the ring no longer holds, or not ours at all: the session cannot be resumed.
            ticket = null;
        }

        if (ticket is null
            || !TryGetSessionBegan(ticket, out DateTimeOffset began)
            || this.timeProvider.GetUtcNow() - began >= this.maximumLifetime
            || SubjectOf(ticket.Principal) is not string subject
            || await this.RevokedAtAsync(subject, cancellationToken).ConfigureAwait(false) is DateTimeOffset revoked && began <= revoked)
        {
            await this.cache.RemoveAsync(this.ticketPrefix + key, cancellationToken).ConfigureAwait(false);
            return null;
        }

        return ticket;
    }

    /// <inheritdoc/>
    public Task RemoveAsync(string key) => this.RemoveAsync(key, CancellationToken.None);

    /// <inheritdoc/>
    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        return this.cache.RemoveAsync(this.ticketPrefix + key, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask RevokeAllAsync(string subject, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(subject);

        byte[] revokedAt = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(revokedAt, this.timeProvider.GetUtcNow().UtcTicks);
        await this.cache.SetAsync(
            this.EpochKey(subject),
            revokedAt,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = this.maximumLifetime },
            cancellationToken).ConfigureAwait(false);
    }

    private static bool TryGetSessionBegan(AuthenticationTicket ticket, out DateTimeOffset began)
    {
        began = default;
        return ticket.Properties.Items.TryGetValue(SessionBeganKey, out string? value)
            && DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out began);
    }

    private async Task WriteAsync(string key, AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        // A ticket's entry outlives neither its own expiry nor its session's maximum lifetime.
        DateTimeOffset ends = TryGetSessionBegan(ticket, out DateTimeOffset began)
            ? began + this.maximumLifetime
            : this.timeProvider.GetUtcNow() + this.maximumLifetime;
        if (ticket.Properties.ExpiresUtc is DateTimeOffset expires && expires < ends)
        {
            ends = expires;
        }

        byte[] protectedTicket = this.protector.Protect(TicketSerializer.Default.Serialize(ticket));
        await this.cache.SetAsync(
            this.ticketPrefix + key,
            protectedTicket,
            new DistributedCacheEntryOptions { AbsoluteExpiration = ends },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<DateTimeOffset?> RevokedAtAsync(string subject, CancellationToken cancellationToken)
    {
        byte[]? stored = await this.cache.GetAsync(this.EpochKey(subject), cancellationToken).ConfigureAwait(false);
        return stored is { Length: sizeof(long) }
            ? new DateTimeOffset(BinaryPrimitives.ReadInt64BigEndian(stored), TimeSpan.Zero)
            : null;
    }

    // The subject is hashed into the key, so any subject a provider issues makes a key of one shape and length.
    private string EpochKey(string subject)
        => this.epochPrefix + Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(subject)));
}