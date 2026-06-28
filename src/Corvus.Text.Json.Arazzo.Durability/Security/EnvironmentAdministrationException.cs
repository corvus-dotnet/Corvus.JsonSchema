// <copyright file="EnvironmentAdministrationException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Thrown when an administration operation on a deployment environment is refused because the caller is not one of its
/// administrators (design §7.7): an environment is governed by its explicit administrator set (creating it grants the
/// creator administration), and only an administrator may manage it or change its administration. An unknown environment
/// and a non-administrator are refused identically (non-disclosing). Maps to HTTP 403 at the control-plane surface.
/// </summary>
public sealed class EnvironmentAdministrationException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="EnvironmentAdministrationException"/> class.</summary>
    /// <param name="environmentName">The environment the caller does not administer.</param>
    public EnvironmentAdministrationException(string environmentName)
        : base($"The environment '{environmentName}' is administered by a different identity, or has no established administration; only an administrator may manage it.")
        => this.EnvironmentName = environmentName;

    /// <summary>Gets the environment the caller does not administer.</summary>
    public string EnvironmentName { get; }
}