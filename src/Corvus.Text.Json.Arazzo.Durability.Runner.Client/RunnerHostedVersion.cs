// <copyright file="RunnerHostedVersion.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client;

/// <summary>
/// A version this runner may execute, as the control plane resolved it from the runner's environment bindings.
/// </summary>
/// <param name="BaseWorkflowId">The unversioned workflow identity.</param>
/// <param name="VersionNumber">The version number within the base workflow.</param>
/// <param name="Hash">The version's content hash, which the loaded executor is verified against.</param>
/// <remarks>
/// The versioned id a claim matches on is <c>{BaseWorkflowId}-v{VersionNumber}</c>. It is composed here rather than
/// carried on the wire, because the server would otherwise allocate it per version on every poll to restate what these
/// two fields already say.
/// </remarks>
public readonly record struct RunnerHostedVersion(string BaseWorkflowId, int VersionNumber, string Hash)
{
    /// <summary>Gets the versioned workflow id to present when claiming.</summary>
    /// <returns>The versioned workflow id.</returns>
    public string ToWorkflowId() => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{this.BaseWorkflowId}-v{this.VersionNumber}");
}