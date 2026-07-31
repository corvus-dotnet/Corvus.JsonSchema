// <copyright file="MicroGuestDeployer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Aot;

namespace Corvus.Text.Json.Arazzo.Durability.MicroGuest.Deploy;

/// <summary>
/// The micro-guest <see cref="IServerlessDeployer"/> (ADR 0063). It bakes a version's verified native guest binary into
/// a Unikraft initrd CPIO and instructs the warm micro-guest sidecar to evolve a snapshotted Hyperlight micro-VM
/// sandbox for the (environment, version): the sidecar boots the kernel, loads the guest, snapshots, and thereafter
/// restores that snapshot hermetically per advance. The returned function URL is the sidecar's local invoke endpoint,
/// which speaks the same invocation contract as a deployed cloud function, so the runner's serverless execution
/// backend and the dispatch-ready gate work unchanged.
/// </summary>
/// <remarks>
/// <para>The sidecar admin contract this deployer drives (and the sidecar implements):</para>
/// <list type="bullet">
/// <item><description><c>PUT {base}/sandboxes/{id}/initrd</c> — the initrd CPIO as <c>application/octet-stream</c>;
/// the sidecar stages it for the sandbox.</description></item>
/// <item><description><c>PUT {base}/sandboxes/{id}</c> — the sandbox configuration as JSON
/// (<c>{"memoryMib", "allowedHosts": ["host:port"], "environment": {"NAME": "value"}}</c>); the sidecar builds the
/// sandbox from the staged initrd (baking the environment pairs and its own guest-facing endpoint into the frozen
/// argv), evolves and snapshots it, and returns <c>{"invokeUrl"}</c>. Re-PUT replaces the sandbox (a redeploy).</description></item>
/// </list>
/// <para>The egress allowlist sent as <c>allowedHosts</c> is exactly the hosts the run may reach — the runner's
/// checkpoint surface and the environment's source hosts — the tighter-than-cloud posture ADR 0063 decides. A sidecar
/// failure is returned as a <see cref="ServerlessDeployResult.Failure"/> rather than thrown, matching how the runner's
/// deploy service treats a deploy failure.</para>
/// </remarks>
public sealed class MicroGuestDeployer : IServerlessDeployer
{
    private const int MaxSandboxIdLength = 64;

    private readonly MicroGuestDeployerOptions options;
    private readonly HttpMessageHandler httpHandler;

    /// <summary>Initializes a new instance of the <see cref="MicroGuestDeployer"/> class.</summary>
    /// <param name="options">The sidecar address, guest layout, and per-sandbox posture to deploy with.</param>
    /// <param name="httpHandler">The HTTP message handler the sidecar requests run over; defaults to a
    /// <see cref="SocketsHttpHandler"/> held for the deployer's life (connection pooling). Injectable for tests.</param>
    public MicroGuestDeployer(MicroGuestDeployerOptions options, HttpMessageHandler? httpHandler = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options;
        this.httpHandler = httpHandler ?? new SocketsHttpHandler();
    }

    /// <inheritdoc/>
    public async ValueTask<ServerlessDeployResult> DeployAsync(ServerlessDeployRequest request, CancellationToken cancellationToken)
    {
        string sandboxId = SandboxId(request);
        byte[] initrd = InitrdCpio.Build(request.NativeBinary, this.options.GuestBinaryPath);
        byte[] configuration = this.BuildSandboxConfiguration();

        using var client = new HttpClient(this.httpHandler, disposeHandler: false) { BaseAddress = this.options.SidecarBaseUrl };
        try
        {
            // Stage the initrd first, then evolve: the sidecar builds the sandbox from the staged image, so a failed
            // upload never replaces a live snapshot.
            using (var initrdContent = new ByteArrayContent(initrd))
            {
                initrdContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                using HttpResponseMessage staged = await client.PutAsync($"sandboxes/{sandboxId}/initrd", initrdContent, cancellationToken).ConfigureAwait(false);
                if (!staged.IsSuccessStatusCode)
                {
                    return await FailureAsync(sandboxId, "staging the initrd", staged, cancellationToken).ConfigureAwait(false);
                }
            }

            using var configurationContent = new ByteArrayContent(configuration);
            configurationContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using HttpResponseMessage evolved = await client.PutAsync($"sandboxes/{sandboxId}", configurationContent, cancellationToken).ConfigureAwait(false);
            if (!evolved.IsSuccessStatusCode)
            {
                return await FailureAsync(sandboxId, "evolving the sandbox", evolved, cancellationToken).ConfigureAwait(false);
            }

            byte[] body = await evolved.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (!TryReadInvokeUrl(body, out string invokeUrl))
            {
                return ServerlessDeployResult.Failure(
                    $"The micro-guest sidecar evolved sandbox '{sandboxId}' but returned no invokeUrl: {Encoding.UTF8.GetString(body)}");
            }

            return ServerlessDeployResult.Success(
                invokeUrl,
                $"Evolved micro-guest sandbox '{sandboxId}' ({this.options.MemorySizeMib} MiB) on the sidecar at '{this.options.SidecarBaseUrl}'.");
        }
        catch (HttpRequestException ex)
        {
            return ServerlessDeployResult.Failure(
                $"The micro-guest sidecar at '{this.options.SidecarBaseUrl}' could not be reached deploying sandbox '{sandboxId}': {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the deterministic sandbox id for a deploy target: <c>arazzo-mg-{base}-v{ver}-{env}-{rid}</c> with each
    /// part sanitized to <c>[a-zA-Z0-9-_]</c>; an over-long result is truncated with a short deterministic suffix so
    /// two distinct targets never collide (the same scheme as the Lambda deployer's function name).
    /// </summary>
    /// <param name="request">The deploy request whose target tuple names the sandbox.</param>
    /// <returns>The sandbox id.</returns>
    internal static string SandboxId(ServerlessDeployRequest request)
    {
        string body = $"arazzo-mg-{Sanitize(request.BaseWorkflowId)}-v{request.VersionNumber}-{Sanitize(request.Environment)}-{Sanitize(request.RuntimeIdentifier)}";
        if (body.Length <= MaxSandboxIdLength)
        {
            return body;
        }

        string suffix = ShortHash(body);
        int keep = MaxSandboxIdLength - 1 - suffix.Length;
        return $"{body[..keep]}-{suffix}";
    }

    // The sandbox configuration document: the micro-VM size, the egress allowlist (the checkpoint surface plus each
    // source host — exactly the hosts the run may reach), and the environment pairs the sidecar freezes into argv.
    internal byte[] BuildSandboxConfiguration()
    {
        List<string> allowedHosts = [HostAndPort(this.options.CheckpointSurfaceUrl)];
        foreach ((string name, string value) in this.options.GuestEnvironment)
        {
            if (name.StartsWith("ARAZZO_SOURCE__", StringComparison.Ordinal)
                && Uri.TryCreate(value, UriKind.Absolute, out Uri? sourceUrl))
            {
                string host = HostAndPort(sourceUrl);
                if (!allowedHosts.Contains(host, StringComparer.Ordinal))
                {
                    allowedHosts.Add(host);
                }
            }
        }

        return PersistedJson.ToArray(
            (Options: this.options, AllowedHosts: allowedHosts),
            static (Utf8JsonWriter writer, in (MicroGuestDeployerOptions Options, List<string> AllowedHosts) context) =>
            {
                writer.WriteStartObject();
                writer.WriteNumber("memoryMib"u8, context.Options.MemorySizeMib);

                writer.WriteStartArray("allowedHosts"u8);
                foreach (string host in context.AllowedHosts)
                {
                    writer.WriteStringValue(host);
                }

                writer.WriteEndArray();

                writer.WriteStartObject("environment"u8);
                foreach ((string name, string value) in context.Options.GuestEnvironment)
                {
                    writer.WriteString(name, value);
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            });
    }

    private static string HostAndPort(Uri url) => $"{url.Host}:{url.Port}";

    private static bool TryReadInvokeUrl(ReadOnlyMemory<byte> body, out string invokeUrl)
    {
        invokeUrl = string.Empty;
        try
        {
            using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(body);
            if (document.RootElement.TryGetProperty("invokeUrl"u8, out JsonElement urlElement)
                && urlElement.ValueKind == JsonValueKind.String
                && urlElement.GetString() is { Length: > 0 } url)
            {
                invokeUrl = url;
                return true;
            }
        }
        catch (JsonException)
        {
            // A malformed body is reported by the caller with the raw payload.
        }

        return false;
    }

    private static async ValueTask<ServerlessDeployResult> FailureAsync(string sandboxId, string stage, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ServerlessDeployResult.Failure(
            $"The micro-guest sidecar rejected {stage} for sandbox '{sandboxId}': {(int)response.StatusCode} {body}");
    }

    private static string Sanitize(string value)
    {
        return string.Create(value.Length, value, static (span, source) =>
        {
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                span[i] = IsSafe(c) ? c : '-';
            }
        });
    }

    private static bool IsSafe(char c)
        => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '_';

    private static string ShortHash(string value)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(value), hash);
        return Convert.ToHexStringLower(hash[..4]);
    }
}