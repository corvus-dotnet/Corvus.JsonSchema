// <copyright file="ReadSideAudit.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Frozen;
using System.Diagnostics;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// The two tiers of the read-side audit that belong to every operation at once (ADR 0070). Tier two: a <c>GET</c> that
/// names a resource in its path and answers with a not-found is a refusal record. Tier three: any other read that
/// answers is metered, on a counter by action, tenant and outcome, and appends nothing, since lists, searches, counts
/// and index-row gets are most of the traffic and none of them discloses a payload. Tier one, the reads that do, record
/// themselves before they answer, and are left out of the meter here. The control plane answers a row outside the caller's reach exactly as it answers a
/// row that is not there (ADR 0004), and the store does not tell them apart either, so the record does not try to: it is
/// the probe, by whom and for what, and its volume is the signal.
/// </summary>
/// <remarks>
/// It is a filter over the mapped operations and not a call in each handler, so that an operation added later is covered
/// without its author remembering to be. It reads the response's status after the handler has answered, records or
/// counts, and changes nothing: neither a refusal record nor the meter ever fails or alters the request it describes.
/// An operation added later that discloses a payload has to record itself and be named in <see cref="Disclosures"/>.
/// </remarks>
internal sealed class ReadSideAudit(ControlPlaneAccess access, GovernanceAuditor auditor)
{
    private const int MaxTargetIdLength = 128;

    // The operations that disclose a payload and record themselves before they answer (tier one).
    private static readonly FrozenSet<string> Disclosures = new[] { "getRunSteps", "getDebugRun", "getCredential" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Records refusals and meters reads on an endpoint, where it is a read.</summary>
    /// <param name="endpoint">The descriptor for the operation being mapped.</param>
    /// <param name="builder">The endpoint convention builder.</param>
    public void Configure(in EndpointDescriptor endpoint, IEndpointConventionBuilder builder)
    {
        // Every read on this surface is a GET, searches and counts included.
        if (!string.Equals(endpoint.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        bool namesResource = endpoint.RouteTemplate.Contains('{');

        string action = endpoint.OperationId ?? endpoint.RouteTemplate;
        string targetKind = TargetKind(endpoint.RouteTemplate);
        builder.AddEndpointFilter(async (context, next) =>
        {
            object? result = await next(context).ConfigureAwait(false);
            HttpContext http = context.HttpContext;
            int status = http.Response.StatusCode;
            if (status == StatusCodes.Status404NotFound)
            {
                if (!namesResource)
                {
                    return result;
                }

                await auditor.RefusedReadAsync(action, access.AuditSubject(), targetKind, TargetId(http.Request.RouteValues), http.Request.RouteValues.TryGetValue("environment", out object? environment) ? environment as string : null).ConfigureAwait(false);
            }
            else if (!Disclosures.Contains(action))
            {
                var tags = new TagList
                {
                    { ArazzoTelemetry.ActionTag, action },
                    { ArazzoTelemetry.OutcomeTag, status is >= 200 and < 300 ? "ok" : "error" },
                };
                if (access.AuditSubject().OwnerGroup is { } tenant)
                {
                    tags.Add(ArazzoTelemetry.TenantTag, tenant);
                }

                ArazzoTelemetry.Reads.Add(1, tags);
            }

            return result;
        });
    }

    // The kind of resource is the path's first segment (runs, credentials, environments), which is controlled vocabulary.
    private static string TargetKind(string routeTemplate)
    {
        ReadOnlySpan<char> path = routeTemplate.AsSpan().TrimStart('/');
        int end = path.IndexOf('/');
        return new string(end < 0 ? path : path[..end]);
    }

    // The id asked after is whatever the caller put in the path, so it is bounded: an identifier, never a payload.
    private static string TargetId(RouteValueDictionary routeValues)
    {
        var id = new StringBuilder();
        foreach (KeyValuePair<string, object?> value in routeValues)
        {
            if (value.Value is string text)
            {
                if (id.Length > 0)
                {
                    id.Append('/');
                }

                id.Append(text);
            }
        }

        return id.Length <= MaxTargetIdLength ? id.ToString() : id.ToString(0, MaxTargetIdLength);
    }
}