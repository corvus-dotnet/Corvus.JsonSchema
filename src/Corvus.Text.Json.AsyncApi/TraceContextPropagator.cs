// <copyright file="TraceContextPropagator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text.Json;

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Injects and extracts W3C trace context (traceparent / tracestate) from
/// <see cref="JsonElement"/> message headers, enabling distributed tracing
/// across message broker boundaries.
/// </summary>
/// <remarks>
/// <para>
/// On publish, <see cref="Inject"/> adds <c>traceparent</c> and optionally
/// <c>tracestate</c> properties to the headers JSON object. On subscribe,
/// <see cref="TryExtractParentContext"/> reads them back and returns an
/// <see cref="ActivityContext"/> suitable for creating a child/linked span.
/// </para>
/// <para>
/// This class is zero-allocation when no <see cref="Activity.Current"/> exists
/// (the inject path is a no-op that returns the headers unchanged).
/// </para>
/// </remarks>
internal static class TraceContextPropagator
{
    private static readonly byte[] TraceparentPropertyName = "traceparent"u8.ToArray();
    private static readonly byte[] TracestatePropertyName = "tracestate"u8.ToArray();

    /// <summary>
    /// Injects the current activity's trace context into the headers JSON object.
    /// </summary>
    /// <param name="headers">The existing headers. May be <c>default</c> (Undefined).</param>
    /// <param name="activity">The activity whose context to inject, or <see langword="null"/> to skip injection.</param>
    /// <returns>
    /// A scope whose <see cref="InjectedHeaders.Headers"/> contains the original headers plus
    /// <c>traceparent</c> and <c>tracestate</c> properties, or the original headers
    /// unchanged if there is no activity to propagate. The caller must dispose the scope
    /// after the element's last use.
    /// </returns>
    /// <remarks>
    /// The element is a view over a workspace the scope owns (the same shape the generated
    /// producers use), so the inner transport serializes it exactly once, directly from the
    /// built document. The workspace is unrented because the element crosses awaits.
    /// </remarks>
    public static InjectedHeaders Inject(in JsonElement headers, Activity? activity)
    {
        if (activity is null || activity.Id is null)
        {
            return new InjectedHeaders(headers, null);
        }

        string traceparent = activity.Id;
        string? tracestate = activity.TraceStateString;

        JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        try
        {
            JsonDocumentBuilder<JsonElement.Mutable> builder = headers.ValueKind == JsonValueKind.Object
                ? headers.CreateBuilder(workspace)
                : JsonElement.CreateObjectBuilder(workspace);

            JsonElement.Mutable root = builder.RootElement;
            root.SetProperty(TraceparentPropertyName, traceparent);

            if (tracestate is not null)
            {
                root.SetProperty(TracestatePropertyName, tracestate);
            }

            return new InjectedHeaders(root, workspace);
        }
        catch
        {
            workspace.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Attempts to extract a W3C trace context from the headers JSON object.
    /// </summary>
    /// <param name="headers">The message headers to inspect.</param>
    /// <param name="parentContext">
    /// When this method returns <see langword="true"/>, contains the parsed
    /// <see cref="ActivityContext"/> from the headers.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a valid <c>traceparent</c> was found and parsed;
    /// <see langword="false"/> otherwise.
    /// </returns>
    public static bool TryExtractParentContext(in JsonElement headers, out ActivityContext parentContext)
    {
        parentContext = default;

        if (headers.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!headers.TryGetProperty(TraceparentPropertyName, out JsonElement traceparentProp) ||
            traceparentProp.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? traceparent = traceparentProp.GetString();
        if (traceparent is null)
        {
            return false;
        }

        string? tracestate = null;
        if (headers.TryGetProperty(TracestatePropertyName, out JsonElement tracestateProp) &&
            tracestateProp.ValueKind == JsonValueKind.String)
        {
            tracestate = tracestateProp.GetString();
        }

        return ActivityContext.TryParse(traceparent, tracestate, out parentContext);
    }

    /// <summary>
    /// The result of <see cref="Inject"/>: the headers element to publish, valid until
    /// this scope is disposed.
    /// </summary>
    /// <remarks>
    /// On the no-activity path the scope owns nothing and <see cref="Dispose"/> is a no-op;
    /// otherwise it owns the workspace backing the element.
    /// </remarks>
    public readonly struct InjectedHeaders : IDisposable
    {
        private readonly JsonWorkspace? workspace;

        internal InjectedHeaders(JsonElement headers, JsonWorkspace? workspace)
        {
            this.Headers = headers;
            this.workspace = workspace;
        }

        /// <summary>
        /// Gets the headers element carrying the injected trace context.
        /// </summary>
        public JsonElement Headers { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.workspace?.Dispose();
        }
    }
}