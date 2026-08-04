// <copyright file="RunnerApiOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// The deployment's bounds on the runner API: how long a lease may be held, how many candidate runs one claim will try,
/// and how large a checkpoint may be.
/// </summary>
public sealed class RunnerApiOptions
{
    /// <summary>Gets or sets the lease duration granted when a runner requests none. Defaults to one minute, matching the dispatcher's own default.</summary>
    public TimeSpan DefaultLease { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the longest lease the deployment grants. A runner asking for more is granted this rather than
    /// refused, because the request is a preference and refusing it would strand a runner that merely wants fewer
    /// renewals. Defaults to one hour, the contract's own ceiling.
    /// </summary>
    public TimeSpan MaximumLease { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets how many claimable candidates one claim attempt will try before answering "nothing claimable".
    /// A candidate is skipped when another runner leases it first or when it turns out not to be claimable under the
    /// lease, and without a bound a busy queue would let one request walk the whole index. Defaults to 16.
    /// </summary>
    public int ClaimCandidates { get; set; } = 16;

    /// <summary>
    /// Gets or sets how many runs one wait-index sweep will claim when the runner asks for no limit, and the most it
    /// will claim whatever it asks for. Every run a sweep returns is leased from that moment, so a sweep that took a
    /// large fan-out at once would hold each lease for as long as the runner needed to work through the rest of the
    /// batch one at a time. Defaults to 16.
    /// </summary>
    public int MaximumSweep { get; set; } = 16;

    /// <summary>
    /// Gets or sets the largest checkpoint the deployment accepts. A generous cap, so a lying or hostile
    /// <c>Content-Length</c> cannot force an unbounded rent; a real checkpoint is far smaller. Defaults to 64 MiB.
    /// </summary>
    public int MaximumCheckpointBytes { get; set; } = 64 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the claim type carrying the machine principal's identity, or <see langword="null"/> to resolve it as
    /// <see cref="MachinePrincipal"/> does. Defaults to <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// The default is what makes a runner's registration and its claims agree on who it is: the control plane binds an
    /// authorization to the principal <see cref="MachinePrincipal"/> resolves, and this API must resolve the same one or
    /// the runner is bound to nothing. Naming a claim here overrides that entirely and is for an issuer whose claims
    /// mean something other than the standard ones.
    /// </remarks>
    public string? PrincipalClaimType { get; set; }

    /// <summary>
    /// Gets or sets the page size for the hosted-versions listing when a runner asks for none, and the most it will
    /// return whatever it asks for. Defaults to 200.
    /// </summary>
    public int MaximumHostedPage { get; set; } = 200;

    /// <summary>Bounds a requested lease duration to the deployment's range.</summary>
    /// <param name="requested">The requested duration, or <see langword="null"/> when the runner asked for none.</param>
    /// <returns>The duration to grant.</returns>
    public TimeSpan BoundLease(TimeSpan? requested)
        => requested is not { } duration || duration <= TimeSpan.Zero
            ? this.DefaultLease
            : duration > this.MaximumLease ? this.MaximumLease : duration;

    /// <summary>Bounds a requested hosted-versions page size.</summary>
    /// <param name="requested">The page size the runner asked for, or <see langword="null"/> when it asked for none.</param>
    /// <returns>The page size to serve.</returns>
    public int BoundHostedPage(int? requested)
        => requested is not { } count || count <= 0
            ? this.MaximumHostedPage
            : count > this.MaximumHostedPage ? this.MaximumHostedPage : count;

    /// <summary>Bounds how many runs one wait-index sweep may claim.</summary>
    /// <param name="requested">The limit the runner asked for, or <see langword="null"/> when it asked for none.</param>
    /// <returns>The number to claim at most.</returns>
    public int BoundSweep(int? requested)
        => requested is not { } count || count <= 0
            ? this.MaximumSweep
            : count > this.MaximumSweep ? this.MaximumSweep : count;
}