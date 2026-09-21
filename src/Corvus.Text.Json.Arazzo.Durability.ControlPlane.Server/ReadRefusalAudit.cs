// <copyright file="ReadRefusalAudit.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Records a refused read (ADR 0070, tier two) for every operation at once: a <c>GET</c> that names a resource in its
/// path and answers with a not-found. The control plane answers a row outside the caller's reach exactly as it answers a
/// row that is not there (ADR 0004), and the store does not tell them apart either, so the record does not try to: it is
/// the probe, by whom and for what, and its volume is the signal.
/// </summary>
/// <remarks>
/// It is a filter over the mapped operations and not a call in each handler, so that an operation added later is covered
/// without its author remembering to be. It reads the response's status after the handler has answered, records, and
/// changes nothing: a refusal record never fails or alters the request it describes.
/// </remarks>
internal sealed class ReadRefusalAudit(ControlPlaneAccess access, GovernanceAuditor auditor)
{
    private const int MaxTargetIdLength = 128;

    /// <summary>Records refusals on an endpoint, where it is a read that names a resource.</summary>
    /// <param name="endpoint">The descriptor for the operation being mapped.</param>
    /// <param name="builder">The endpoint convention builder.</param>
    public void Configure(in EndpointDescriptor endpoint, IEndpointConventionBuilder builder)
    {
        if (!string.Equals(endpoint.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase) || !endpoint.RouteTemplate.Contains('{'))
        {
            return;
        }

        string action = endpoint.OperationId ?? endpoint.RouteTemplate;
        string targetKind = TargetKind(endpoint.RouteTemplate);
        builder.AddEndpointFilter(async (context, next) =>
        {
            object? result = await next(context).ConfigureAwait(false);
            HttpContext http = context.HttpContext;
            if (http.Response.StatusCode == StatusCodes.Status404NotFound)
            {
                await auditor.RefusedReadAsync(action, access.AuditSubject(), targetKind, TargetId(http.Request.RouteValues), http.Request.RouteValues.TryGetValue("environment", out object? environment) ? environment as string : null).ConfigureAwait(false);
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