// <copyright file="ScheduleRegistrationConflictException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Schedules;

/// <summary>
/// Thrown by <see cref="IScheduleRegistry.RegisterAsync"/> when a schedule id is already registered to a
/// different registration. The message deliberately says nothing about the existing registration — not its
/// environment and not its run — because schedule ids are deployment-global and the caller may have no reach
/// over the environment holding the occupant.
/// </summary>
public sealed class ScheduleRegistrationConflictException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="ScheduleRegistrationConflictException"/> class.</summary>
    /// <param name="message">The exception message.</param>
    public ScheduleRegistrationConflictException(string message)
        : base(message)
    {
    }
}