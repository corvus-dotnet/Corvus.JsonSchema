// <copyright file="RunnerRefusalAudit.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// Records the runner API's refusals (ADR 0071): the seam between the control plane and a runner it does not trust
/// (ADR 0065) stops being silent. A request with no resolvable machine principal, a runner that is revoked or
/// quarantined or not bound to the environment, a lease or an epoch that no longer matches, a document that is not
/// there, and a quota that is spent each leave a record naming the runner's principal, the operation and the run.
/// </summary>
/// <remarks>
/// <para>
/// It is one filter over the mapped operations and not a call at each of the thirty places a handler refuses, so an
/// operation or a refusal added later is covered. What it can see is how the request was answered, so the outcome is as
/// fine as the status: <c>refused-no-principal</c> and <c>refused-forbidden</c> for a 403, by whether the request named
/// a machine principal at all, <c>refused-conflict</c> for a 409, <c>refused-not-found</c> for a 404 and
/// <c>refused-quota</c> for a 429. Which forbidden, a revoked runner or an environment it is not bound to, and which
/// conflict, a lost lease or a superseded write, is in the response the runner was given and not in the record.
/// </para>
/// <para>
/// A runner calls this API in a loop, so it can cause refusals at will, and a runner that has lost its lease or spent
/// its quota does so many times a second. The records are therefore bounded for each principal, with what was over the
/// bound recorded as a count, and none of it ever changes how the request is answered.
/// </para>
/// </remarks>
internal sealed class RunnerRefusalAudit(RunnerPrincipalAccessor principals, GovernanceAuditor auditor)
{
    private const string NoPrincipal = "(no machine principal)";

    /// <summary>Records refusals on an endpoint.</summary>
    /// <param name="endpoint">The descriptor for the operation being mapped.</param>
    /// <param name="builder">The endpoint convention builder.</param>
    public void Configure(in EndpointDescriptor endpoint, IEndpointConventionBuilder builder)
    {
        string action = "runner." + (endpoint.OperationId ?? endpoint.RouteTemplate);
        bool isRead = string.Equals(endpoint.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase);
        builder.AddEndpointFilter(async (context, next) =>
        {
            object? result = await next(context).ConfigureAwait(false);
            HttpContext http = context.HttpContext;
            string? principal = principals.Resolve();
            string? outcome = http.Response.StatusCode switch
            {
                StatusCodes.Status403Forbidden => principal is null ? "refused-no-principal" : "refused-forbidden",
                StatusCodes.Status409Conflict => "refused-conflict",
                StatusCodes.Status404NotFound => "refused-not-found",
                StatusCodes.Status429TooManyRequests => "refused-quota",
                _ => null,
            };
            if (outcome is null)
            {
                return result;
            }

            RouteValueDictionary route = http.Request.RouteValues;
            string runId = route.TryGetValue("runId", out object? run) && run is string id ? id : "-";
            string? environment = route.TryGetValue("environment", out object? env) ? env as string : null;
            var actor = new AuditSubject(principal ?? NoPrincipal, null);
            if (isRead && http.Response.StatusCode == StatusCodes.Status404NotFound)
            {
                await auditor.RefusedReadAsync(action, actor, "run", runId, environment).ConfigureAwait(false);
            }
            else
            {
                await auditor.RefusalAsync(action, actor, "run", runId, outcome, environment).ConfigureAwait(false);
            }

            return result;
        });
    }
}