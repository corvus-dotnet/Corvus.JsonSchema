// <copyright file="IControlPlaneSessionRevocation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>Ends every session of a subject (ADR 0075).</summary>
public interface IControlPlaneSessionRevocation
{
    /// <summary>
    /// Ends every session the subject began up to now. A session the subject begins afterwards is unaffected.
    /// </summary>
    /// <param name="subject">The subject, as <see cref="ControlPlaneSessionTickets.SubjectOf"/> reads it from a principal.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the revocation is recorded.</returns>
    ValueTask RevokeAllAsync(string subject, CancellationToken cancellationToken = default);
}