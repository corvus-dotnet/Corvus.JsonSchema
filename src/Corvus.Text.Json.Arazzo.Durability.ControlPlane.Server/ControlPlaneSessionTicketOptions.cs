// <copyright file="ControlPlaneSessionTicketOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>Options for <see cref="ControlPlaneSessionTickets"/> (ADR 0075).</summary>
public sealed class ControlPlaneSessionTicketOptions
{
    /// <summary>
    /// Gets or sets the longest a session may last from sign-in, however much it is used. It is also how long a sign-out
    /// everywhere is remembered, since by then every session it could revoke has ended on its own. Eight hours by
    /// default.
    /// </summary>
    public TimeSpan MaximumLifetime { get; set; } = TimeSpan.FromHours(8);

    /// <summary>Gets or sets the prefix of every cache key the store writes, so it can share a cache.</summary>
    public string KeyPrefix { get; set; } = "arazzo:session:";
}