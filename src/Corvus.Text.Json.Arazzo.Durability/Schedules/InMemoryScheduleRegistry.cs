// <copyright file="InMemoryScheduleRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace Corvus.Text.Json.Arazzo.Durability.Schedules;

/// <summary>
/// The in-memory <see cref="IScheduleRegistry"/> — the reference implementation the conformance suite pins,
/// used by single-process hosts and tests. The dictionary's atomic add is the uniqueness gate.
/// </summary>
public sealed class InMemoryScheduleRegistry : IScheduleRegistry
{
    private readonly ConcurrentDictionary<string, ScheduleRegistration> registrations = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public ValueTask RegisterAsync(string scheduleId, ScheduleRegistration registration, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);
        cancellationToken.ThrowIfCancellationRequested();
        if (this.registrations.TryAdd(scheduleId, registration))
        {
            return ValueTask.CompletedTask;
        }

        if (this.registrations.TryGetValue(scheduleId, out ScheduleRegistration existing) && existing == registration)
        {
            return ValueTask.CompletedTask;
        }

        throw ThrowHelper.GetScheduleRegistrationConflictException(scheduleId);
    }

    /// <inheritdoc/>
    public ValueTask<ScheduleRegistration?> ResolveAsync(string scheduleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);
        cancellationToken.ThrowIfCancellationRequested();
        return new(this.registrations.TryGetValue(scheduleId, out ScheduleRegistration registration) ? registration : null);
    }

    /// <inheritdoc/>
    public ValueTask UnregisterAsync(string scheduleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);
        cancellationToken.ThrowIfCancellationRequested();
        this.registrations.TryRemove(scheduleId, out _);
        return ValueTask.CompletedTask;
    }
}