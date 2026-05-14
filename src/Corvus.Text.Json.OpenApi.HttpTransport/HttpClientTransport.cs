// <copyright file="HttpClientTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.HttpTransport;

/// <summary>
/// An <see cref="IApiTransport"/> implementation backed by <see cref="HttpClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// Request bodies are written directly into the HTTP stream via
/// WriteTo — no intermediate buffer is allocated.
/// Response bodies are returned as a <see cref="Stream"/> for direct parsing
/// into <see cref="ParsedJsonDocument{T}"/>.
/// </para>
/// <para>
/// The transport does not own the <see cref="HttpClient"/> by default.
/// Pass <c>disposeClient: true</c> to the constructor if the transport should dispose
/// the client when it is itself disposed.
/// </para>
/// </remarks>
public sealed class HttpClientTransport : IApiTransport
{
    private readonly HttpClient httpClient;
    private readonly bool disposeClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpClientTransport"/> class.
    /// </summary>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for sending requests.</param>
    /// <param name="disposeClient">
    /// <see langword="true"/> to dispose <paramref name="httpClient"/> when this transport
    /// is disposed; <see langword="false"/> (the default) to leave it to the caller.
    /// </param>
    public HttpClientTransport(HttpClient httpClient, bool disposeClient = false)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.disposeClient = disposeClient;
    }

    /// <inheritdoc/>
    public ValueTask<ApiResponse> SendAsync(
        in ApiRequest request,
        CancellationToken cancellationToken = default)
    {
        HttpRequestMessage httpRequest = BuildHttpRequest(in request);
        return SendCoreAsync(httpRequest, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<ApiResponse> SendAsync<TBody>(
        in ApiRequest request,
        in TBody body,
        CancellationToken cancellationToken = default)
        where TBody : struct, IJsonElement<TBody>
    {
        HttpRequestMessage httpRequest = BuildHttpRequest(in request);
        httpRequest.Content = new JsonElementContent<TBody>(body);
        return SendCoreAsync(httpRequest, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (this.disposeClient)
        {
            this.httpClient.Dispose();
        }

        return default;
    }

    private static HttpMethod MapMethod(OperationMethod method) =>
        method switch
        {
            OperationMethod.Get => HttpMethod.Get,
            OperationMethod.Post => HttpMethod.Post,
            OperationMethod.Put => HttpMethod.Put,
            OperationMethod.Delete => HttpMethod.Delete,
            OperationMethod.Patch => HttpMethod.Patch,
            OperationMethod.Head => HttpMethod.Head,
            OperationMethod.Options => HttpMethod.Options,
            OperationMethod.Trace => HttpMethod.Trace,
            _ => new HttpMethod(method.ToString()),
        };

    private static HttpRequestMessage BuildHttpRequest(in ApiRequest request)
    {
        string uri = BuildResolvedPath(request.PathTemplate, request.PathParameters);

        ReadOnlySpan<ParameterEntry> queryParams = request.QueryParameters;
        if (queryParams.Length > 0)
        {
            uri = BuildUriWithQuery(uri, queryParams);
        }

        HttpRequestMessage httpRequest = new(MapMethod(request.Method), uri);

        foreach (ParameterEntry header in request.Headers)
        {
            httpRequest.Headers.TryAddWithoutValidation(
                header.Name,
                SerializeParameterValue(header));
        }

        return httpRequest;
    }

    private async ValueTask<ApiResponse> SendCoreAsync(
        HttpRequestMessage httpRequest,
        CancellationToken cancellationToken)
    {
        // Use ResponseHeadersRead so the content stream is available immediately
        // without buffering the entire response body.
        HttpResponseMessage httpResponse = await this.httpClient
            .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            Stream contentStream = await httpResponse.Content
#if NET
                .ReadAsStreamAsync(cancellationToken)
#else
                .ReadAsStreamAsync()
#endif
                .ConfigureAwait(false);

            return new ApiResponse(
                (int)httpResponse.StatusCode,
                contentStream,
                new HttpResponseOwner(httpRequest, httpResponse));
        }
        catch
        {
            httpResponse.Dispose();
            httpRequest.Dispose();
            throw;
        }
    }

    private static string BuildResolvedPath(
        string pathTemplate,
        ReadOnlySpan<ParameterEntry> pathParams)
    {
        if (pathParams.Length == 0)
        {
            return pathTemplate;
        }

        DefaultInterpolatedStringHandler handler = new(0, 0, null, stackalloc char[256]);
        ReadOnlySpan<char> remaining = pathTemplate;

        while (remaining.Length > 0)
        {
            int openBrace = remaining.IndexOf('{');
            if (openBrace < 0)
            {
                handler.AppendFormatted(remaining);
                break;
            }

            handler.AppendFormatted(remaining[..openBrace]);

            int closeBrace = remaining[(openBrace + 1)..].IndexOf('}');
            if (closeBrace < 0)
            {
                // Malformed template — emit the rest as-is
                handler.AppendFormatted(remaining[openBrace..]);
                break;
            }

            ReadOnlySpan<char> paramName = remaining[(openBrace + 1)..(openBrace + 1 + closeBrace)];

            // Find the matching path parameter
            bool found = false;
            foreach (ParameterEntry param in pathParams)
            {
                if (paramName.SequenceEqual(param.Name))
                {
                    string serialized = SerializeParameterValue(param);
                    handler.AppendFormatted(System.Uri.EscapeDataString(serialized));
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // No matching parameter — leave placeholder as-is
                handler.AppendLiteral("{");
                handler.AppendFormatted(paramName);
                handler.AppendLiteral("}");
            }

            remaining = remaining[(openBrace + 1 + closeBrace + 1)..];
        }

        return handler.ToStringAndClear();
    }

    private static string BuildUriWithQuery(
        string basePath,
        ReadOnlySpan<ParameterEntry> parameters)
    {
        DefaultInterpolatedStringHandler handler = new(0, 0, null, stackalloc char[256]);
        handler.AppendLiteral(basePath);
        handler.AppendLiteral("?");

        bool first = true;
        foreach (ParameterEntry param in parameters)
        {
            if (param.Explode && param.Value.ValueKind == JsonValueKind.Array)
            {
                // Exploded array: key=v1&key=v2&key=v3
                foreach (JsonElement item in param.Value.EnumerateArray())
                {
                    if (!first)
                    {
                        handler.AppendLiteral("&");
                    }

                    handler.AppendFormatted(System.Uri.EscapeDataString(param.Name));
                    handler.AppendLiteral("=");
                    handler.AppendFormatted(System.Uri.EscapeDataString(GetPrimitiveString(item)));
                    first = false;
                }
            }
            else if (param.Explode && param.Value.ValueKind == JsonValueKind.Object)
            {
                // Exploded object: key1=v1&key2=v2
                foreach (JsonProperty<JsonElement> prop in param.Value.EnumerateObject())
                {
                    if (!first)
                    {
                        handler.AppendLiteral("&");
                    }

                    handler.AppendFormatted(System.Uri.EscapeDataString(prop.Name));
                    handler.AppendLiteral("=");
                    handler.AppendFormatted(System.Uri.EscapeDataString(GetPrimitiveString(prop.Value)));
                    first = false;
                }
            }
            else
            {
                // Simple scalar, or non-exploded array/object
                if (!first)
                {
                    handler.AppendLiteral("&");
                }

                handler.AppendFormatted(System.Uri.EscapeDataString(param.Name));
                handler.AppendLiteral("=");
                handler.AppendFormatted(System.Uri.EscapeDataString(SerializeParameterValue(param)));
                first = false;
            }
        }

        return handler.ToStringAndClear();
    }

    /// <summary>
    /// Serializes a <see cref="ParameterEntry"/> value to a string using its style and explode settings.
    /// </summary>
    /// <remarks>
    /// This covers the subset of OpenAPI serialization styles used for query, path, and header parameters.
    /// Styles like <see cref="ParameterStyle.Label"/>, <see cref="ParameterStyle.Matrix"/>, and
    /// <see cref="ParameterStyle.DeepObject"/> are supported.
    /// </remarks>
    private static string SerializeParameterValue(in ParameterEntry entry)
    {
        JsonElement value = entry.Value;

        if (value.ValueKind is JsonValueKind.String or JsonValueKind.Number
            or JsonValueKind.True or JsonValueKind.False)
        {
            return entry.Style switch
            {
                ParameterStyle.Label => "." + GetPrimitiveString(value),
                ParameterStyle.Matrix => $";{entry.Name}={GetPrimitiveString(value)}",
                _ => GetPrimitiveString(value),
            };
        }

        if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
        {
            return string.Empty;
        }

        string separator = entry.Style switch
        {
            ParameterStyle.SpaceDelimited => "%20",
            ParameterStyle.PipeDelimited => "|",
            ParameterStyle.Label => ".",
            _ => ",",
        };

        if (value.ValueKind == JsonValueKind.Array)
        {
            return SerializeArray(value, separator, entry.Style, entry.Name, entry.Explode);
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            return SerializeObject(value, separator, entry.Style, entry.Name, entry.Explode);
        }

        return value.ToString();
    }

    private static string SerializeArray(
        JsonElement value,
        string separator,
        ParameterStyle style,
        string name,
        bool explode)
    {
        DefaultInterpolatedStringHandler handler = new(0, 0, null, stackalloc char[128]);
        bool first = true;

        string prefix = style switch
        {
            ParameterStyle.Label => ".",
            ParameterStyle.Matrix when explode => string.Empty,
            _ => string.Empty,
        };

        if (prefix.Length > 0)
        {
            handler.AppendLiteral(prefix);
        }

        foreach (JsonElement item in value.EnumerateArray())
        {
            if (!first)
            {
                if (style == ParameterStyle.Matrix && explode)
                {
                    handler.AppendFormatted($";{name}=");
                }
                else
                {
                    handler.AppendLiteral(separator);
                }
            }

            handler.AppendFormatted(GetPrimitiveString(item));
            first = false;
        }

        return handler.ToStringAndClear();
    }

    private static string SerializeObject(
        JsonElement value,
        string separator,
        ParameterStyle style,
        string name,
        bool explode)
    {
        DefaultInterpolatedStringHandler handler = new(0, 0, null, stackalloc char[128]);
        bool first = true;

        string prefix = style switch
        {
            ParameterStyle.Label => ".",
            ParameterStyle.Matrix when explode => string.Empty,
            _ => string.Empty,
        };

        if (prefix.Length > 0)
        {
            handler.AppendLiteral(prefix);
        }

        foreach (JsonProperty<JsonElement> prop in value.EnumerateObject())
        {
            if (!first)
            {
                if (explode)
                {
                    if (style == ParameterStyle.Matrix)
                    {
                        handler.AppendLiteral(";");
                    }
                    else
                    {
                        handler.AppendLiteral(separator);
                    }
                }
                else
                {
                    handler.AppendLiteral(separator);
                }
            }

            handler.AppendFormatted(prop.Name);
            handler.AppendLiteral(explode ? "=" : separator);
            handler.AppendFormatted(GetPrimitiveString(prop.Value));
            first = false;
        }

        return handler.ToStringAndClear();
    }

    private static string GetPrimitiveString(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => value.ToString(),
        };

    /// <summary>
    /// Custom <see cref="HttpContent"/> that writes the body directly from an
    /// <see cref="IJsonElement{T}"/> into the HTTP request stream via
    /// <see cref="Utf8JsonWriter"/>. No intermediate buffer is allocated.
    /// </summary>
    private sealed class JsonElementContent<T> : HttpContent
        where T : struct, IJsonElement<T>
    {
        private readonly T body;

        public JsonElementContent(in T body)
        {
            this.body = body;
            this.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8",
            };
        }

        protected override Task SerializeToStreamAsync(
            Stream stream,
            System.Net.TransportContext? context)
        {
            using Utf8JsonWriter writer = new(stream);
            this.body.WriteTo(writer);
            writer.Flush();
            return Task.CompletedTask;
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    /// <summary>
    /// Owns the <see cref="HttpRequestMessage"/> and <see cref="HttpResponseMessage"/>
    /// lifetime. Disposed when the <see cref="ApiResponse"/> is disposed.
    /// </summary>
    private sealed class HttpResponseOwner(
        HttpRequestMessage request,
        HttpResponseMessage response) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            response.Dispose();
            request.Dispose();
            return default;
        }
    }
}