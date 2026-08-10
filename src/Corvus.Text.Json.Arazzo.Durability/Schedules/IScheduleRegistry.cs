// <copyright file="IScheduleRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Schedules;

/// <summary>
/// The deployment-global schedule registry: <c>scheduleId</c> (unique across the whole deployment, per the
/// schedules contract) to <see cref="ScheduleRegistration"/> (the environment the schedule is pinned to and
/// its scheduler run). The registry is both the uniqueness gate and the resolver for the schedules surface,
/// whose routes deliberately carry no environment.
/// </summary>
/// <remarks>
/// Registration happens <em>before</em> the scheduler run is created, so the registry's insert conflict is
/// what enforces the contract's global uniqueness atomically — under the composite <c>(environment, runId)</c>
/// run key (ADR 0065 §9) two environments can hold the same run id, so a run-key collision can no longer
/// observe a schedule created in another environment. A crash between registration and run creation
/// self-heals: re-creating the same schedule finds its own identical registration (idempotent) and the named
/// start converges on its own logical run.
/// </remarks>
public interface IScheduleRegistry
{
    /// <summary>
    /// Registers <paramref name="scheduleId"/> as resolving to <paramref name="registration"/>, atomically.
    /// Registering an id that already carries an <em>identical</em> registration succeeds and changes nothing
    /// (the crash-retry and redelivery convergence); an id registered to anything else refuses.
    /// </summary>
    /// <param name="scheduleId">The deployment-globally-unique schedule id.</param>
    /// <param name="registration">What the id resolves to.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the registration is durable.</returns>
    /// <exception cref="ScheduleRegistrationConflictException">The id is already registered to a different
    /// registration (another environment's schedule, or another run).</exception>
    ValueTask RegisterAsync(string scheduleId, ScheduleRegistration registration, CancellationToken cancellationToken);

    /// <summary>Resolves a schedule id to its registration.</summary>
    /// <param name="scheduleId">The schedule id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The registration, or <see langword="null"/> when the id is not registered.</returns>
    ValueTask<ScheduleRegistration?> ResolveAsync(string scheduleId, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a schedule id's registration. Removing an unregistered id is a no-op. Used to roll back a
    /// registration whose run creation was refused; a deleted (cancelled) schedule keeps its registration,
    /// because its scheduler run still occupies the derived address.
    /// </summary>
    /// <param name="scheduleId">The schedule id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the registration is removed.</returns>
    ValueTask UnregisterAsync(string scheduleId, CancellationToken cancellationToken);
}