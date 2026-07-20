// <copyright file="ControlPlaneRunStarter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.Arazzo.Runner;

/// <summary>
/// A governed <see cref="WorkflowStartHandler"/>: fires a workflow run through the control plane's authenticated HTTP
/// API rather than writing to the run store directly. The durable scheduler (#896) — a schedule is a durable run —
/// invokes this for each due occurrence, so a scheduled start is subject to the same admission control, environment
/// pinning, reach checks, and audit as an operator-initiated start (design §5.5). It authenticates as the runner's
/// machine principal (the same client-credentials principal the runner registers under) and carries the occurrence's
/// idempotency key as an <c>Idempotency-Key</c> header, so a re-fire after a runner recycle starts the target at most
/// once.
/// </summary>
/// <remarks>
/// Every scheduled target fires into the one environment this starter is bound to — the environment the runner serves,
/// which is the environment its schedule runs are pinned to (env-pinned dispatch claims only that runner's own
/// environment), so the target run lands in the same environment as the schedule. This is a scaling path (a fire per
/// occurrence per entity), so it reuses one injected <see cref="HttpClient"/> and a cached machine-principal token (the
/// single-flight <see cref="IHttpAuthenticationProvider"/> refreshes proactively, not per fire), and serializes the
/// inputs body into an <see cref="System.Buffers.ArrayPool{T}"/>-rented buffer rather than a fresh array per fire.
/// </remarks>
public sealed class ControlPlaneRunStarter
{
    private readonly HttpClient http;
    private readonly IHttpAuthenticationProvider authentication;
    private readonly Uri baseUri;
    private readonly string environment;

    /// <summary>Initializes a new instance of the <see cref="ControlPlaneRunStarter"/> class.</summary>
    /// <param name="http">The HTTP client used to POST the run (its lifetime is owned by the caller; reused across fires).</param>
    /// <param name="authentication">Applies the runner's machine-principal bearer token to each request (a cached, single-flight provider).</param>
    /// <param name="controlPlaneBaseUrl">The control plane's base URL (the Aspire-injected <c>controlplane</c> endpoint).</param>
    /// <param name="environment">The single deployment environment scheduled targets fire into (the runner's environment).</param>
    public ControlPlaneRunStarter(HttpClient http, IHttpAuthenticationProvider authentication, string controlPlaneBaseUrl, string environment)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentException.ThrowIfNullOrWhiteSpace(controlPlaneBaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);
        this.http = http;
        this.authentication = authentication;

        // The control-plane API is mounted under /arazzo/v1 (the same prefix the CLI, designer, and registrar call).
        this.baseUri = new Uri(controlPlaneBaseUrl.EndsWith('/') ? controlPlaneBaseUrl : controlPlaneBaseUrl + "/");
        this.environment = environment;
    }

    /// <summary>Gets this starter as the <see cref="WorkflowStartHandler"/> the scheduler drives.</summary>
    /// <returns>The start-handler delegate.</returns>
    public WorkflowStartHandler AsStartHandler() => this.StartAsync;

    /// <summary>
    /// Starts (idempotently) a run of the request's target version through the governed run endpoint, returning its id.
    /// </summary>
    /// <param name="request">The start request (the versioned target id, its inputs, and the occurrence's idempotency key).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The id of the run for this request (the same id on a re-fire carrying the same idempotency key).</returns>
    /// <exception cref="InvalidOperationException">The target id is not a versioned id, or the control plane refused the start.</exception>
    public async ValueTask<WorkflowRunId> StartAsync(WorkflowStartRequest request, CancellationToken cancellationToken)
    {
        (string baseWorkflowId, int versionNumber) = ParseVersionedId(request.WorkflowId);
        var uri = new Uri(
            this.baseUri,
            $"arazzo/v1/catalog/{Uri.EscapeDataString(baseWorkflowId)}/versions/{versionNumber.ToString(CultureInfo.InvariantCulture)}/runs?environment={Uri.EscapeDataString(this.environment)}");

        // Serialize the inputs into a pooled UTF-8 buffer (returned once the send has completed — the send is the
        // guarantee the content has been read), so a fire allocates no per-request body array.
        using PooledUtf8 body = PersistedJson.RentDocument(
            request.Inputs, static (Utf8JsonWriter writer, in JsonElement inputs) => inputs.WriteTo(writer));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new ReadOnlyMemoryContent(body.Memory),
        };
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        httpRequest.Headers.TryAddWithoutValidation("Idempotency-Key", request.IdempotencyKey);
        await this.authentication.AuthenticateAsync(httpRequest, cancellationToken).ConfigureAwait(false);

        using HttpResponseMessage response = await this.http.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        byte[] responseBody = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Starting a run of '{request.WorkflowId}' in environment '{this.environment}' was refused ({(int)response.StatusCode} {response.StatusCode}): {Encoding.UTF8.GetString(responseBody)}");
        }

        return new WorkflowRunId(ReadRunId(responseBody));
    }

    // Reads the run id from the WorkflowRunAccepted body ({ "runId": "...", "status": "...", "workflowId": "..." })
    // with the Corvus reader (no document allocation); unknown members are skipped.
    private static string ReadRunId(ReadOnlySpan<byte> body)
    {
        var reader = new Utf8JsonReader(body);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new InvalidOperationException("The run-accepted response was not a JSON object.");
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("runId"u8))
            {
                reader.Read();
                return reader.GetString() ?? throw new InvalidOperationException("The run-accepted response carried a null runId.");
            }

            reader.Read();
            reader.Skip();
        }

        throw new InvalidOperationException("The run-accepted response carried no runId.");
    }

    private static (string BaseWorkflowId, int VersionNumber) ParseVersionedId(string workflowId)
    {
        int suffix = workflowId.LastIndexOf("-v", StringComparison.Ordinal);
        if (suffix > 0 && int.TryParse(workflowId.AsSpan(suffix + 2), NumberStyles.None, CultureInfo.InvariantCulture, out int version))
        {
            return (workflowId[..suffix], version);
        }

        throw new InvalidOperationException($"The target workflow id '{workflowId}' is not a versioned id of the form '{{base}}-v{{n}}'.");
    }
}