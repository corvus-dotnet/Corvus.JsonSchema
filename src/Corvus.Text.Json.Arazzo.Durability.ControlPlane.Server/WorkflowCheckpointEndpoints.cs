// <copyright file="WorkflowCheckpointEndpoints.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Maps the runner's serverless checkpoint surface — <c>GET</c>/<c>POST /runs/{runId}/checkpoint</c> — onto an
/// endpoint route builder (ADR 0055). A baked, Native-AOT function advances a run out of process and loads and saves
/// its checkpoint here rather than binding a store SDK; the <see cref="WorkflowCheckpointCoordinator"/> terminates
/// those calls into the real store. This is the server half of the wire contract the function-side
/// <c>HttpWorkflowStateStore</c> speaks: octet-stream checkpoint bytes, an <c>ETag</c>, and the
/// <see cref="WriteSequenceHeader"/> monotonic write-sequence.
/// </summary>
internal static class WorkflowCheckpointEndpoints
{
    /// <summary>The header carrying a checkpoint's monotonic write-sequence on a save, and the last applied one on a load. Matches the function-side <c>HttpWorkflowStateStore.WriteSequenceHeader</c>.</summary>
    internal const string WriteSequenceHeader = "X-Arazzo-Checkpoint-Seq";

    private const string CheckpointContentType = "application/octet-stream";

    // A generous cap so a lying or hostile Content-Length cannot force an unbounded rent; a real checkpoint is far smaller.
    private const int MaxCheckpointBytes = 64 * 1024 * 1024;

    /// <summary>
    /// Maps the checkpoint load/save endpoints, backed by <paramref name="store"/>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="store">The real state store a checkpoint terminates into.</param>
    /// <param name="requireAuthorization">Whether to require an authenticated principal (every mode but Open); a dedicated checkpoint scope is deferred to the deploy work.</param>
    /// <returns>The same endpoint route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapWorkflowCheckpointEndpoints(this IEndpointRouteBuilder endpoints, IWorkflowCheckpointStore store, bool requireAuthorization)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(store);

        var coordinator = new WorkflowCheckpointCoordinator(store);

        IEndpointConventionBuilder get = endpoints.MapGet("/runs/{runId}/checkpoint", async (HttpContext context) =>
        {
            if (RunId(context) is not { } id)
            {
                await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "The required parameter 'runId' is missing.").ConfigureAwait(false);
                return;
            }

            CheckpointLoad? loaded = await coordinator.LoadAsync(id, context.RequestAborted).ConfigureAwait(false);
            if (loaded is not { } load)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = CheckpointContentType;
            if (load.Etag.Value is { } etag)
            {
                context.Response.Headers.ETag = QuoteEtag(etag);
            }

            context.Response.Headers[WriteSequenceHeader] = load.LastAppliedSequence.ToString(CultureInfo.InvariantCulture);
            await context.Response.BodyWriter.WriteAsync(load.Checkpoint, context.RequestAborted).ConfigureAwait(false);
        });

        IEndpointConventionBuilder post = endpoints.MapPost("/runs/{runId}/checkpoint", async (HttpContext context) =>
        {
            if (RunId(context) is not { } id)
            {
                await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "The required parameter 'runId' is missing.").ConfigureAwait(false);
                return;
            }

            if (ReadSequence(context) is not { } sequence)
            {
                await WriteProblemAsync(context, StatusCodes.Status400BadRequest, $"The required '{WriteSequenceHeader}' header is missing or not a positive integer.").ConfigureAwait(false);
                return;
            }

            if (context.Request.ContentLength is > MaxCheckpointBytes)
            {
                await WriteProblemAsync(context, StatusCodes.Status413PayloadTooLarge, "The checkpoint exceeds the maximum size.").ConfigureAwait(false);
                return;
            }

            (byte[] rented, int length) = await ReadBodyAsync(context.Request, context.RequestAborted).ConfigureAwait(false);
            try
            {
                if (length > MaxCheckpointBytes)
                {
                    await WriteProblemAsync(context, StatusCodes.Status413PayloadTooLarge, "The checkpoint exceeds the maximum size.").ConfigureAwait(false);
                    return;
                }

                // Project (and thereby validate) the index from the received bytes here, so a malformed body is a clean
                // 400 and the coordinator only ever handles a well-formed checkpoint. The same bytes are saved verbatim.
                ReadOnlyMemory<byte> checkpointUtf8 = rented.AsMemory(0, length);
                if (!WorkflowCheckpointSerializer.TryProjectIndex(checkpointUtf8, out WorkflowRunIndexEntry index))
                {
                    await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "The request body is not a valid checkpoint document.").ConfigureAwait(false);
                    return;
                }

                CheckpointSaveOutcome outcome = await coordinator.SaveAsync(id, checkpointUtf8, index, sequence, context.RequestAborted).ConfigureAwait(false);
                context.Response.StatusCode = outcome == CheckpointSaveOutcome.Conflict
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status204NoContent; // Applied or Superseded — both succeed from the caller's view.
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        });

        if (requireAuthorization)
        {
            get.RequireAuthorization();
            post.RequireAuthorization();
        }

        return endpoints;
    }

    private static WorkflowRunId? RunId(HttpContext context)
    {
        if (context.Request.RouteValues.TryGetValue("runId", out object? raw) && raw is string value && value.Length > 0)
        {
            return new WorkflowRunId(value);
        }

        return null;
    }

    private static long? ReadSequence(HttpContext context)
        => context.Request.Headers.TryGetValue(WriteSequenceHeader, out StringValues values)
            && long.TryParse(values.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long sequence)
            && sequence >= 1
            ? sequence
            : null;

    // An etag on the wire is a quoted entity-tag; the store's etag is a bare token, so quote it unless it already is.
    private static string QuoteEtag(string value)
        => value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value : $"\"{value}\"";

    // Reads the request body into a pooled buffer. Returns the rented array (the caller returns it to the pool) and the
    // number of bytes read. The common path is a single exact rent from a known Content-Length; the fallback grows a
    // rented buffer for a chunked (unknown-length) body.
    private static async ValueTask<(byte[] Rented, int Length)> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength is > 0 and <= MaxCheckpointBytes)
        {
            int length = (int)request.ContentLength.Value;
            byte[] exact = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                await request.Body.ReadExactlyAsync(exact.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
                return (exact, length);
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(exact);
                throw;
            }
        }

        byte[] rented = ArrayPool<byte>.Shared.Rent(8192);
        int total = 0;
        try
        {
            int read;
            do
            {
                if (total == rented.Length)
                {
                    byte[] bigger = ArrayPool<byte>.Shared.Rent(rented.Length * 2);
                    Array.Copy(rented, bigger, total);
                    ArrayPool<byte>.Shared.Return(rented);
                    rented = bigger;
                }

                read = await request.Body.ReadAsync(rented.AsMemory(total), cancellationToken).ConfigureAwait(false);
                total += read;
            }
            while (read > 0 && total <= MaxCheckpointBytes); // stop at end-of-stream, or one read past the cap (the caller then rejects an over-cap body).

            return (rented, total);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(rented);
            throw;
        }
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string detail)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        string title = status switch
        {
            StatusCodes.Status400BadRequest => "Bad Request",
            StatusCodes.Status413PayloadTooLarge => "Payload Too Large",
            _ => "Error",
        };

        // The detail strings are fixed and contain no double quotes or backslashes, so this fixed document needs no
        // JSON escaping.
        return context.Response.WriteAsync(
            $"{{\"type\":\"about:blank\",\"title\":\"{title}\",\"status\":{status},\"detail\":\"{detail}\"}}",
            context.RequestAborted);
    }
}