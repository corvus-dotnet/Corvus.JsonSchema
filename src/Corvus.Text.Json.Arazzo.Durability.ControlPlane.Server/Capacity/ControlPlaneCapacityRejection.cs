// <copyright file="ControlPlaneCapacityRejection.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Capacity;

/// <summary>
/// A capacity limit's refusal: which limit refused, what it was measured against, and where it stood when it did.
/// </summary>
/// <param name="Quota">The limit that refused, as the contract's <c>quota</c> field carries it.</param>
/// <param name="Counter">What it was measured against, as the contract's <c>counter</c> field carries it.</param>
/// <param name="Limit">The configured cap.</param>
/// <param name="Observed">What was counted, bounded at the cap. It is never a total above the cap, because the count
/// stops there.</param>
/// <remarks>
/// The names match the runner API's quota refusal, so one client shape reads both surfaces. What differs is what the
/// caller must do about it: a rate refusal clears by waiting, and this one clears only by releasing capacity.
/// </remarks>
public readonly record struct ControlPlaneCapacityRejection(string Quota, string Counter, int Limit, int Observed);