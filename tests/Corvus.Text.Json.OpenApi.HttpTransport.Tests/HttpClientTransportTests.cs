// <copyright file="HttpClientTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.OpenApi.HttpTransport.Tests;

[TestClass]
public class HttpClientTransportTests
{
    [TestMethod]
    public async Task SendAsync_Get_ReturnsStreamableResponse()
    {
        byte[] expected = """{"id":1,"name":"Fido"}"""u8.ToArray();
        using HttpClient client = CreateMockClient(HttpStatusCode.OK, expected);
        await using HttpClientTransport transport = new(client);

        ApiRequest request = new("/pets/1", OperationMethod.Get);
        await using ApiResponse response = await transport.SendAsync(in request);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);

        // Read the content stream
        using MemoryStream ms = new();
        await response.ContentStream.CopyToAsync(ms);
        CollectionAssert.AreEqual(expected, ms.ToArray());
    }

    [TestMethod]
    public async Task SendAsync_QueryParameters_AppendsToUri()
    {
        HttpRequestMessage? captured = null;

        using HttpClient client = CreateMockClient(
            HttpStatusCode.OK,
            "[]"u8.ToArray(),
            onRequest: req => { captured = req; return Task.CompletedTask; });

        await using HttpClientTransport transport = new(client);

        ApiRequest request = new("/pets", OperationMethod.Get);
        request.AddQueryParameter("limit", "10");
        request.AddQueryParameter("offset", "20");

        await using ApiResponse response = await transport.SendAsync(in request);

        Assert.IsNotNull(captured);
        string? uri = captured.RequestUri?.OriginalString;
        Assert.IsNotNull(uri);
        Assert.IsTrue(uri.Contains("limit=10", StringComparison.Ordinal), $"Expected 'limit=10' in '{uri}'");
        Assert.IsTrue(uri.Contains("offset=20", StringComparison.Ordinal), $"Expected 'offset=20' in '{uri}'");
        Assert.IsTrue(uri.Contains("/pets?", StringComparison.Ordinal), $"Expected '/pets?' in '{uri}'");
    }

    [TestMethod]
    public async Task SendAsync_Headers_AreForwarded()
    {
        HttpRequestMessage? captured = null;

        using HttpClient client = CreateMockClient(
            HttpStatusCode.OK,
            "[]"u8.ToArray(),
            onRequest: req => { captured = req; return Task.CompletedTask; });

        await using HttpClientTransport transport = new(client);

        ApiRequest request = new("/pets", OperationMethod.Get);
        request.AddHeader("X-Custom", "value123");

        await using ApiResponse response = await transport.SendAsync(in request);

        Assert.IsNotNull(captured);
        Assert.IsTrue(captured.Headers.Contains("X-Custom"));
        Assert.AreEqual("value123", captured.Headers.GetValues("X-Custom").First());
    }

    [TestMethod]
    public async Task SendAsync_NonSuccessStatus_ResponseCaptured()
    {
        using HttpClient client = CreateMockClient(
            HttpStatusCode.NotFound,
            """{"error":"not found"}"""u8.ToArray());

        await using HttpClientTransport transport = new(client);

        ApiRequest request = new("/pets/999", OperationMethod.Get);
        await using ApiResponse response = await transport.SendAsync(in request);

        Assert.AreEqual(404, response.StatusCode);
        Assert.IsFalse(response.IsSuccess);
    }

    [TestMethod]
    public async Task SendAsync_EmptyBody_ReturnsEmptyStream()
    {
        using HttpClient client = CreateMockClient(HttpStatusCode.NoContent, []);
        await using HttpClientTransport transport = new(client);

        ApiRequest request = new("/pets/1", OperationMethod.Delete);
        await using ApiResponse response = await transport.SendAsync(in request);

        Assert.AreEqual(204, response.StatusCode);

        using MemoryStream ms = new();
        await response.ContentStream.CopyToAsync(ms);
        Assert.AreEqual(0, ms.Length);
    }

    [TestMethod]
    public async Task DisposeAsync_WithDisposeClient_DisposesHttpClient()
    {
        MockHandler handler = new(HttpStatusCode.OK, []);
        HttpClient client = new(handler);

        HttpClientTransport transport = new(client, disposeClient: true);
        await transport.DisposeAsync();

        // Verify the client was disposed by trying to use it — should throw ObjectDisposedException
        Assert.ThrowsExactly<ObjectDisposedException>(
            () => client.GetAsync(new Uri("http://localhost/test")).GetAwaiter().GetResult());
    }

    [TestMethod]
    public async Task DisposeAsync_WithoutDisposeClient_DoesNotDisposeHttpClient()
    {
        using MockHandler handler = new(HttpStatusCode.OK, "[]"u8.ToArray());
        using HttpClient client = new(handler) { BaseAddress = new Uri("http://localhost") };

        HttpClientTransport transport = new(client, disposeClient: false);
        await transport.DisposeAsync();

        // Client should still be usable
        HttpResponseMessage response = await client.GetAsync(new Uri("http://localhost/test"));
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task SendAsync_QueryParametersWithSpecialChars_AreEncoded()
    {
        HttpRequestMessage? captured = null;

        using HttpClient client = CreateMockClient(
            HttpStatusCode.OK,
            "[]"u8.ToArray(),
            onRequest: req => { captured = req; return Task.CompletedTask; });

        await using HttpClientTransport transport = new(client);

        ApiRequest request = new("/search", OperationMethod.Get);
        request.AddQueryParameter("q", "hello world&foo=bar");

        await using ApiResponse response = await transport.SendAsync(in request);

        Assert.IsNotNull(captured);
        string? uri = captured.RequestUri?.OriginalString;
        Assert.IsNotNull(uri);

        // Should be URL-encoded
        Assert.IsFalse(uri.Contains("hello world", StringComparison.Ordinal), "Space should be encoded");
        Assert.IsTrue(uri.Contains("q=", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SendAsync_GenericOverload_WritesBodyViaWriteTo()
    {
        HttpRequestMessage? captured = null;
        byte[]? capturedBody = null;

        using HttpClient client = CreateMockClient(
            HttpStatusCode.Created,
            """{"id":2}"""u8.ToArray(),
            onRequest: async req =>
            {
                captured = req;
                if (req.Content is not null)
                {
                    capturedBody = await req.Content.ReadAsByteArrayAsync();
                }
            });

        await using HttpClientTransport transport = new(client);

        // Create a JsonElement body from JSON
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse("""{"name":"Rex"}""");
        JsonElement body = doc.RootElement;

        ApiRequest request = new("/pets", OperationMethod.Post);
        await using ApiResponse response = await transport.SendAsync(in request, in body);

        Assert.AreEqual(201, response.StatusCode);
        Assert.IsNotNull(captured);
        Assert.AreEqual(HttpMethod.Post, captured.Method);
        Assert.IsNotNull(capturedBody);

        // The body should be valid JSON containing the name
        string bodyStr = Encoding.UTF8.GetString(capturedBody);
        Assert.IsTrue(bodyStr.Contains("Rex", StringComparison.Ordinal));
        Assert.AreEqual("application/json", captured.Content?.Headers.ContentType?.MediaType);
    }

    [TestMethod]
    public async Task EnsureSuccess_ThrowsOnNonSuccessStatus()
    {
        using HttpClient client = CreateMockClient(HttpStatusCode.InternalServerError, "[]"u8.ToArray());
        await using HttpClientTransport transport = new(client);

        ApiRequest request = new("/fail", OperationMethod.Get);
        await using ApiResponse response = await transport.SendAsync(in request);

        Assert.ThrowsExactly<ApiException>(() => response.EnsureSuccess());
    }

    private static HttpClient CreateMockClient(
        HttpStatusCode statusCode,
        byte[] responseBody,
        Func<HttpRequestMessage, Task>? onRequest = null)
    {
        MockHandler handler = new(statusCode, responseBody, onRequest);
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
    }

    private sealed class MockHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode statusCode;
        private readonly byte[] responseBody;
        private readonly Func<HttpRequestMessage, Task>? onRequest;

        public MockHandler(
            HttpStatusCode statusCode,
            byte[] responseBody,
            Func<HttpRequestMessage, Task>? onRequest = null)
        {
            this.statusCode = statusCode;
            this.responseBody = responseBody;
            this.onRequest = onRequest;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (this.onRequest is not null)
            {
                await this.onRequest(request).ConfigureAwait(false);
            }

            return new HttpResponseMessage(this.statusCode)
            {
                Content = new ByteArrayContent(this.responseBody),
            };
        }
    }
}