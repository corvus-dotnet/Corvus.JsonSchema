// <copyright file="TraceContextPropagator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;

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
/// (the inject path is a no-op that returns headers unchanged).
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
    /// A new <see cref="JsonElement"/> containing the original headers plus
    /// <c>traceparent</c> and <c>tracestate</c> properties; or the original headers
    /// unchanged if there is no activity to propagate.
    /// </returns>
    /// <remarks>
    /// The returned element is a non-disposable value created via <c>JsonElement.ParseValue</c>.
    /// </remarks>
    public static JsonElement Inject(in JsonElement headers, Activity? activity)
    {
        if (activity is null || activity.Id is null)
        {
            return headers;
        }

        string traceparent = activity.Id;
        string? tracestate = activity.TraceStateString;

        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonDocumentBuilder<JsonElement.Mutable> builder;

        if (headers.ValueKind == JsonValueKind.Object)
        {
            builder = headers.CreateBuilder(workspace);
        }
        else
        {
            builder = JsonElement.CreateObjectBuilder(workspace);
        }

        using (builder)
        {
            JsonElement.Mutable root = builder.RootElement;
            root.SetProperty(TraceparentPropertyName, traceparent);

            if (tracestate is not null)
            {
                root.SetProperty(TracestatePropertyName, tracestate);
            }

            return JsonElement.ParseValue(root.ToString());
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
}