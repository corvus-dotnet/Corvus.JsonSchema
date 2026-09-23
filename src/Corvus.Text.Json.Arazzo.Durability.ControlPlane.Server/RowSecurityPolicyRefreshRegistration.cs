// <copyright file="RowSecurityPolicyRefreshRegistration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>The record of a policy a host refreshes on a bounded interval, which the mapping consults before it accepts
/// that policy as the reach policy of a secured deployment.</summary>
/// <param name="Policy">The policy the host refreshes.</param>
/// <param name="Interval">The refresh bound.</param>
internal sealed record RowSecurityPolicyRefreshRegistration(PersistentRowSecurityPolicy Policy, TimeSpan Interval);