// <copyright file="WorkflowVersionId.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The versioned workflow id a run executes, <c>{baseWorkflowId}-v{versionNumber}</c>: the one reading of it.
/// </summary>
/// <remarks>
/// The version is the digits after the LAST <c>-v</c>, so a base id may itself contain <c>-v</c>. It is digits only,
/// read in the invariant culture: a sign or whitespace is not part of a version, and the reading of an id must not
/// depend on the host's culture. There were four private copies of this, three of which read <c>flow-v-3</c> as
/// version minus three.
/// </remarks>
public static class WorkflowVersionId
{
    /// <summary>Splits a versioned workflow id into its base id and version number.</summary>
    /// <param name="workflowId">The versioned workflow id.</param>
    /// <param name="baseWorkflowId">The base workflow id, when the id is versioned.</param>
    /// <param name="versionNumber">The version number, when the id is versioned.</param>
    /// <returns><see langword="true"/> if <paramref name="workflowId"/> is a versioned id.</returns>
    public static bool TryParse(string? workflowId, out string baseWorkflowId, out int versionNumber)
    {
        if (workflowId is not null)
        {
            int suffix = workflowId.LastIndexOf("-v", StringComparison.Ordinal);
            if (suffix > 0 && int.TryParse(workflowId.AsSpan(suffix + 2), NumberStyles.None, CultureInfo.InvariantCulture, out versionNumber))
            {
                baseWorkflowId = workflowId[..suffix];
                return true;
            }
        }

        baseWorkflowId = string.Empty;
        versionNumber = 0;
        return false;
    }
}