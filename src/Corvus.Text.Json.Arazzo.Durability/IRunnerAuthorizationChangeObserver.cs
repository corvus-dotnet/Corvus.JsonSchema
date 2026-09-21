// <copyright file="IRunnerAuthorizationChangeObserver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Told when a runner's authorization changes, so that whatever holds a cached answer about that runner drops it at
/// once and not when its cache window ends (ADR 0027). The control plane's runner-authorization surface calls it after
/// a decision is durable, and the runner API's binding resolution implements it, since both live in the control-plane
/// process.
/// </summary>
/// <remarks>
/// This makes a revocation immediate on the replica that handled it. Another replica of the control plane holds its own
/// cache and learns of the change when its window ends, which is the bound ADR 0027 states.
/// </remarks>
public interface IRunnerAuthorizationChangeObserver
{
    /// <summary>Drops anything cached about a machine principal's authorizations.</summary>
    /// <param name="principal">The machine principal whose authorization changed.</param>
    void AuthorizationChanged(string principal);
}