// <copyright file="AuditRecordFailure.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// What a request gets when the audit sink refused its record: for a mutation (ADR 0069), a 500 problem saying the action
/// was applied and is not in the audit chain; for a read that discloses a payload (ADR 0070), a 500 problem saying the
/// read was refused, nothing was disclosed, and it is safe to ask again.
/// </summary>
/// <remarks>
/// It is a 500 and not a 503. The action has committed, and gateways and clients retry a 503 on their own, which would
/// apply a mutation that is not idempotent a second time. The caller is told what happened and decides.
/// </remarks>
internal static class AuditRecordFailure
{
    /// <summary>The problem type of an action that was applied and could not be recorded.</summary>
    public const string ProblemType = "https://corvus-oss.org/arazzo/control-plane/problems/audit-record-failed";

    /// <summary>The problem type of a payload read that was refused because its record could not be appended.</summary>
    public const string ReadProblemType = "https://corvus-oss.org/arazzo/control-plane/problems/audit-read-record-failed";

    private const string ReadBody =
        "{\"type\":\"" + ReadProblemType + "\",\"title\":\"The read could not be recorded, so it was refused\",\"status\":500,"
        + "\"detail\":\"This read returns a payload, and its audit record could not be appended to the deployment's audit sink, so the read was refused and nothing was disclosed. It is safe to ask again. If it persists, report it to the deployment's operator.\"}";

    private const string Body =
        "{\"type\":\"" + ProblemType + "\",\"title\":\"The action was applied and could not be recorded\",\"status\":500,"
        + "\"detail\":\"The action this request made was applied. Its audit record could not be appended to the deployment's audit sink, so the action is not in the audit chain. Do not retry the request: check the state of the resource, and report this to the deployment's operator.\"}";

    /// <summary>Maps the failure on an endpoint.</summary>
    /// <param name="endpoint">The descriptor for the operation being mapped.</param>
    /// <param name="builder">The endpoint convention builder.</param>
    public static void Configure(in EndpointDescriptor endpoint, IEndpointConventionBuilder builder)
        => builder.AddEndpointFilter(static async (context, next) =>
        {
            try
            {
                return await next(context).ConfigureAwait(false);
            }
            catch (AuditAppendException ex) when (!context.HttpContext.Response.HasStarted)
            {
                HttpResponse response = context.HttpContext.Response;
                response.Clear();
                response.StatusCode = StatusCodes.Status500InternalServerError;
                response.ContentType = "application/problem+json";
                await response.WriteAsync(ex.Kind == AuditEntryKind.Read ? ReadBody : Body).ConfigureAwait(false);
                return null;
            }
        });

    /// <summary>Maps the failure on an endpoint, and gates the endpoint on the scopes its operation declares.</summary>
    /// <param name="endpoint">The descriptor for the operation being mapped.</param>
    /// <param name="builder">The endpoint convention builder.</param>
    public static void ConfigureWithDeclaredScopes(in EndpointDescriptor endpoint, IEndpointConventionBuilder builder)
    {
        Configure(in endpoint, builder);
        ControlPlaneAuthorization.RequireDeclaredScopes(in endpoint, builder);
    }
}