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
/// Maps the runner's serverless checkpoint surface — <c>GET</c>/<c>POST /environments/{environment}/runs/{runId}/checkpoint</c>
/// (the run's full address, ADR 0065 decision 9) — onto an
/// endpoint route builder (ADR 0055). A baked, Native-AOT function advances a run out of process and loads and saves
/// its checkpoint here rather than binding a store SDK; the <see cref="WorkflowCheckpointCoordinator"/> terminates
/// those calls into the real store. This is the server half of the wire contract the function-side
/// <c>HttpWorkflowStateStore</c> speaks: octet-stream checkpoint bytes, an <c>ETag</c>, and the
/// <see cref="WriteSequenceHeader"/> monotonic write-sequence.
/// </summary>
public static class WorkflowCheckpointEndpoints
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
    /// <param name="requireAuthorization">Whether to require an authenticated principal in addition to the token (every mode but Open). The two compose: the ambient principal says a caller belongs to the deployment, the token says which run it may touch.</param>
    /// <param name="authenticateCheckpointToken">The run-scoped checkpoint-token authenticator (ADR 0062): given the request's full run address and the presented bearer token, returns whether it authorises checkpoints for that run at that address. A request without a valid token is a 401 — this is how the checkpoint surface authenticates a serverless function's callback (e.g. <c>(address, token) =&gt; CheckpointToken.TryValidate(secret, token, address, now)</c>). It is required rather than optional because the caller is a machine acting for one run, holding no principal of its own: without it the surface's only gate is the host's ambient authorization, which admits any authenticated caller to any run.</param>
    /// <param name="checkpoints">The host's checkpoint coordinator. Pass the same instance every checkpoint-authoring surface in this host uses — ADR 0065 decision 6 requires the per-run single-flight interlock to be per run, not per component, and the coordinator holds that interlock in memory. When <see langword="null"/> a private one is built, which is correct only for a host mapping this surface alone.</param>
    /// <returns>The same endpoint route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapWorkflowCheckpointEndpoints(this IEndpointRouteBuilder endpoints, IWorkflowCheckpointStore store, bool requireAuthorization, Func<WorkflowRunAddress, string, bool> authenticateCheckpointToken, WorkflowCheckpointCoordinator? checkpoints = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(authenticateCheckpointToken);

        WorkflowCheckpointCoordinator coordinator = checkpoints ?? new WorkflowCheckpointCoordinator(store);

        IEndpointConventionBuilder get = endpoints.MapGet("/environments/{environment}/runs/{runId}/checkpoint", async (HttpContext context) =>
        {
            if (Address(context) is not { } address)
            {
                await WriteAddressProblemAsync(context).ConfigureAwait(false);
                return;
            }

            if (!Authenticated(context, address, authenticateCheckpointToken))
            {
                return;
            }

            CheckpointLoad? loaded = await coordinator.LoadAsync(address, context.RequestAborted).ConfigureAwait(false);
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

        IEndpointConventionBuilder post = endpoints.MapPost("/environments/{environment}/runs/{runId}/checkpoint", async (HttpContext context) =>
        {
            if (Address(context) is not { } address)
            {
                await WriteAddressProblemAsync(context).ConfigureAwait(false);
                return;
            }

            if (!Authenticated(context, address, authenticateCheckpointToken))
            {
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
                // The one projection also reports the environment the body claims, which the coordinator checks against
                // the address on every save (ADR 0065 decision 9).
                ReadOnlyMemory<byte> checkpointUtf8 = rented.AsMemory(0, length);
                if (!WorkflowCheckpointSerializer.TryProjectIndex(checkpointUtf8, out WorkflowRunIndexEntry index, out string? claimedEnvironment))
                {
                    await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "The request body is not a valid checkpoint document.").ConfigureAwait(false);
                    return;
                }

                // ADR 0065 decision 6 (H40): the accept rule the coordinator applies runs off this header, while its
                // re-seed after a slot eviction or restart reads the persisted sequence from the stored body. Those
                // two must be the same number, or a body claiming one sequence under an accepted header of another
                // diverges the store: a body omitting the sequence re-seeds to zero (accepting header 1 forever, an
                // in-place rewrite), and a body carrying long.MaxValue re-seeds to an overflowed negative that no
                // positive header can match (bricking the run). The body must carry the sequence and it must equal
                // the header.
                if (!WorkflowCheckpointSerializer.TryReadSequence(checkpointUtf8, out long bodySequence) || bodySequence != sequence)
                {
                    await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "The checkpoint body's sequence is missing or does not match the request header.").ConfigureAwait(false);
                    return;
                }

                CheckpointSaveResult result = await coordinator.SaveAsync(address, checkpointUtf8, index, claimedEnvironment, sequence, context.RequestAborted).ConfigureAwait(false);
                context.Response.Headers[WriteSequenceHeader] = result.AcceptedSequence.ToString(CultureInfo.InvariantCulture);
                if (result.Outcome == CheckpointSaveOutcome.Applied)
                {
                    context.Response.StatusCode = StatusCodes.Status204NoContent;
                    return;
                }

                // ADR 0065 decision 6: a superseded save answers 409 carrying the accepted sequence, never a 204. The
                // two used to be the same response on the grounds that both "succeed from the caller's view", which is
                // exactly the confusion the ADR forbids — a caller told its write is durable when it was dropped
                // records a checkpoint the store does not hold, and nothing later can reconcile the two.
                await WriteSupersededAsync(context, result).ConfigureAwait(false);
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

    // The request must carry a valid run-scoped bearer token; without one it is a 401. This runs whatever the host's
    // ambient authorization is, because the two answer different questions: ambient authorization says the caller
    // belongs to the deployment, and only the token says which run — at which address — it is entitled to read and
    // overwrite.
    private static bool Authenticated(HttpContext context, in WorkflowRunAddress address, Func<WorkflowRunAddress, string, bool> authenticate)
    {
        string? token = BearerToken(context.Request);
        if (token is null || !authenticate(address, token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            return false;
        }

        return true;
    }

    private static string? BearerToken(HttpRequest request)
    {
        if (request.Headers.TryGetValue("Authorization", out StringValues values))
        {
            foreach (string? value in values)
            {
                if (value?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
                {
                    string token = value["Bearer ".Length..].Trim();
                    if (token.Length > 0)
                    {
                        return token;
                    }
                }
            }
        }

        return null;
    }

    // The grammar gate (ADR 0065 §9): a non-conforming id or environment is refused here, before the token is honoured
    // and before any store touch, exactly as the contract-generated surfaces refuse them through the RunId and
    // EnvironmentName schemas' patterns. Both halves of the address gate, because the environment is half the key.
    private static WorkflowRunAddress? Address(HttpContext context)
    {
        if (context.Request.RouteValues.TryGetValue("runId", out object? rawId) && rawId is string id && WorkflowRunId.IsWellFormed(id)
            && context.Request.RouteValues.TryGetValue("environment", out object? rawEnvironment) && rawEnvironment is string environment && Environments.EnvironmentName.IsWellFormed(environment))
        {
            return new WorkflowRunAddress(environment, new WorkflowRunId(id));
        }

        return null;
    }

    private static Task WriteAddressProblemAsync(HttpContext context)
        => WriteProblemAsync(context, StatusCodes.Status400BadRequest, "The 'runId' parameter must be exactly 32 lowercase hexadecimal characters, and the 'environment' parameter must satisfy the environment-name grammar (ADR 0065 \u00a79).");

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

    // The superseded response. It carries the accepted sequence in the body as well as the header so a caller can tell
    // a duplicate resend (its own sequence is one behind) from a genuine divergence (it is further behind, or ahead)
    // without a second round trip. The problem type matches the runner API contract's.
    private static Task WriteSupersededAsync(HttpContext context, CheckpointSaveResult result)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        context.Response.ContentType = "application/problem+json";

        // Every interpolated value is a fixed string or an integer, so this document needs no JSON escaping.
        return context.Response.WriteAsync(
            $"{{\"type\":\"https://corvus-oss.org/arazzo/runner/problems/checkpoint-superseded\"," +
            $"\"title\":\"Checkpoint superseded\",\"status\":409," +
            $"\"detail\":\"The proposed sequence was not the persisted sequence plus one. Nothing was written.\"," +
            $"\"acceptedSequence\":{result.AcceptedSequence.ToString(CultureInfo.InvariantCulture)}}}",
            context.RequestAborted);
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