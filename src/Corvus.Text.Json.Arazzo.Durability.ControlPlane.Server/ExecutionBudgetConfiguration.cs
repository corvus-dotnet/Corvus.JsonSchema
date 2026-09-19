// <copyright file="ExecutionBudgetConfiguration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Reads a deployment's execution-budget ceiling (ADR 0068) from the host's configuration.
/// </summary>
/// <remarks>
/// <para>
/// The ceiling is a property of the running deployment and not of any record in its stores, so it is the host's
/// configuration. The section is <see cref="SectionName"/>, and its keys are the limits as the API names them:
/// <c>maxSteps</c>, <c>wallClockSeconds</c>, <c>maxSubWorkflowDepth</c>, <c>retryAfterCeilingSeconds</c>,
/// <c>stepTimeoutSeconds</c> and <c>maxResponseBytes</c>. A limit the section does not name is the platform default's.
/// </para>
/// <para>
/// The host hands the ceiling to its <see cref="SecuredWorkflowManagement"/> and to nothing else. The control plane
/// reads it back from <see cref="ISecuredWorkflowManagement.ExecutionBudgetCeiling"/>, so there is one definition.
/// </para>
/// </remarks>
public static class ExecutionBudgetConfiguration
{
    /// <summary>The configuration section the ceiling is read from.</summary>
    public const string SectionName = "Arazzo:ExecutionBudgetCeiling";

    /// <summary>Reads the ceiling from <see cref="SectionName"/>.</summary>
    /// <param name="configuration">The host's configuration.</param>
    /// <returns>The ceiling: the platform default, with each limit the section names replacing the default's.</returns>
    /// <exception cref="InvalidOperationException">A limit is not a whole number.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A limit is outside its admissible range.</exception>
    /// <remarks>A malformed or inadmissible limit stops the host. A deployment that asked for a limit it cannot have
    /// must not run on a different one without anyone having decided that.</remarks>
    public static ExecutionBudget ReadCeiling(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        IConfigurationSection section = configuration.GetSection(SectionName);
        return ExecutionBudget.CeilingFrom(
            maxSteps: (int?)Read(section, ExecutionBudget.JsonPropertyNames.MaxSteps, int.MaxValue),
            wallClockSeconds: Read(section, ExecutionBudget.JsonPropertyNames.WallClockSeconds, long.MaxValue),
            maxSubWorkflowDepth: (int?)Read(section, ExecutionBudget.JsonPropertyNames.MaxSubWorkflowDepth, int.MaxValue),
            retryAfterCeilingSeconds: Read(section, ExecutionBudget.JsonPropertyNames.RetryAfterCeilingSeconds, long.MaxValue),
            stepTimeoutSeconds: Read(section, ExecutionBudget.JsonPropertyNames.StepTimeoutSeconds, long.MaxValue),
            maxResponseBytes: Read(section, ExecutionBudget.JsonPropertyNames.MaxResponseBytes, long.MaxValue));
    }

    private static long? Read(IConfigurationSection section, string name, long maximum)
    {
        if (section[name] is not { Length: > 0 } text)
        {
            return null;
        }

        if (!long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value) || value > maximum)
        {
            throw new InvalidOperationException($"The configured execution-budget ceiling '{SectionName}:{name}' must be a whole number, but was '{text}'.");
        }

        return value;
    }
}