// <copyright file="FixtureTransport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Headers;
using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.OpenApi.Fixtures;

/// <summary>
/// The mock transport a fixture case runs over: it answers with the case's <c>response</c> and captures the request
/// the client sent.
/// </summary>
public sealed class FixtureTransport : IDisposable
{
    private readonly Handler handler;
    private readonly HttpClient client;

    private FixtureTransport(Handler handler)
    {
        this.handler = handler;
        this.client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        this.Transport = new HttpClientTransport(this.client);
    }

    /// <summary>
    /// Gets the transport to hand to the generated client.
    /// </summary>
    public HttpClientTransport Transport { get; }

    /// <summary>
    /// Gets the request the client sent, once it has.
    /// </summary>
    public HttpRequestMessage? CapturedRequest => this.handler.CapturedRequest;

    /// <summary>
    /// Gets the bytes of the request body the client sent, or <see langword="null"/> when it sent none.
    /// </summary>
    public byte[]? CapturedBody => this.handler.CapturedBody;

    /// <summary>
    /// Creates a transport that answers with a case's <c>response</c> object.
    /// </summary>
    /// <param name="response">The case's <c>response</c>.</param>
    /// <returns>The transport.</returns>
    public static FixtureTransport Create(JsonElement response)
    {
        int status = response.TryGetProperty("status", out JsonElement statusElement) ? statusElement.GetInt32() : 200;
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        if (response.TryGetProperty("headers", out JsonElement headerMap) && headerMap.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty<JsonElement> header in headerMap.EnumerateObject())
            {
                headers[header.Name] = header.Value.GetString()!;
            }
        }

        byte[] body;
        string? defaultContentType;
        if (response.TryGetProperty("body", out JsonElement json))
        {
            body = System.Text.Encoding.UTF8.GetBytes(json.GetRawText());
            defaultContentType = "application/json";
        }
        else if (response.TryGetProperty("bodyText", out JsonElement text))
        {
            body = System.Text.Encoding.UTF8.GetBytes(text.GetString()!);
            defaultContentType = "text/plain";
        }
        else if (response.TryGetProperty("bodyBase64", out JsonElement base64))
        {
            body = Convert.FromBase64String(base64.GetString()!);
            defaultContentType = "application/octet-stream";
        }
        else if (response.TryGetProperty("chunks", out JsonElement chunks))
        {
            System.Text.StringBuilder joined = new();
            foreach (JsonElement chunk in chunks.EnumerateArray())
            {
                joined.Append(chunk.GetString());
            }

            body = System.Text.Encoding.UTF8.GetBytes(joined.ToString());
            defaultContentType = null;
        }
        else
        {
            body = [];
            defaultContentType = null;
        }

        if (defaultContentType is not null && !headers.ContainsKey("Content-Type"))
        {
            headers["Content-Type"] = defaultContentType;
        }

        return new FixtureTransport(new Handler(status, headers, body));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.client.Dispose();
        this.handler.Dispose();
    }

    private sealed class Handler(int status, Dictionary<string, string> headers, byte[] body) : HttpMessageHandler
    {
        public HttpRequestMessage? CapturedRequest { get; private set; }

        public byte[]? CapturedBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.CapturedRequest = request;
            if (request.Content is not null)
            {
                this.CapturedBody = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            }

            HttpResponseMessage response = new((HttpStatusCode)status) { Content = new ByteArrayContent(body) };
            foreach ((string name, string value) in headers)
            {
                if (string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(value);
                }
                else if (!response.Headers.TryAddWithoutValidation(name, value))
                {
                    response.Content.Headers.TryAddWithoutValidation(name, value);
                }
            }

            return response;
        }
    }
}