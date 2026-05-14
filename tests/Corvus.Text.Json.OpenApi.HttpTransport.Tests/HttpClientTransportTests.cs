// <copyright file="HttpClientTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Net;
using System.Text;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.OpenApi.HttpTransport.Tests;

[TestClass]
public class HttpClientTransportTests
{
    [TestMethod]
    public async Task SendAsync_Get_ReturnsBodyBytes()
    {
        byte[] expected = """{"id":1,"name":"Fido"}"""u8.ToArray();
        using HttpClient client = CreateMockClient(HttpStatusCode.OK, expected);
        await using HttpClientTransport transport = new(client);

        ApiRequest request = new("/pets/1", "GET");
        using ApiResponse response = await transport.SendAsync(request);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);
        CollectionAssert.AreEqual(expected, response.Body.ToArray());
    }

    [TestMethod]
    public async Task SendAsync_Post_SendsBodyAndContentType()
    {
        byte[] requestBody = """{"name":"Rex"}"""u8.ToArray();
        byte[] responseBody = """{"id":2}"""u8.ToArray();

        HttpRequestMessage? captured = null;
        byte[]? capturedBody = null;

        using HttpClient client = CreateMockClient(
            HttpStatusCode.Created,
            responseBody,
            onRequest: async req =>
            {
                captured = req;
                if (req.Content is not null)
                {
                    capturedBody = await req.Content.ReadAsByteArrayAsync();
                }
            });

        await using HttpClientTransport transport = new(client);

        ApiRequest request = new ApiRequest("/pets", "POST")
            .WithBody(requestBody, "application/json");

        using ApiResponse response = await transport.SendAsync(request);

        Assert.AreEqual(201, response.StatusCode);
        Assert.IsNotNull(captured);
        Assert.AreEqual(HttpMethod.Post, captured.Method);
        Assert.IsNotNull(capturedBody);
        CollectionAssert.AreEqual(requestBody, capturedBody);
        Assert.AreEqual("application/json", captured.Content?.Headers.ContentType?.MediaType);
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

        ApiRequest request = new ApiRequest("/pets", "GET")
            .WithQueryParameter("limit", "10")
            .WithQueryParameter("offset", "20");

        using ApiResponse response = await transport.SendAsync(request);

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

        ApiRequest request = new ApiRequest("/pets", "GET")
            .WithHeader("X-Custom", "value123");

        using ApiResponse response = await transport.SendAsync(request);

        Assert.IsNotNull(captured);
        Assert.IsTrue(captured.Headers.Contains("X-Custom"));
        Assert.AreEqual("value123", captured.Headers.GetValues("X-Custom").First());
    }

    [TestMethod]
    public async Task SendAsync_ResponseHeaders_AreReturned()
    {
        using HttpClient client = CreateMockClient(
            HttpStatusCode.OK,
            "[]"u8.ToArray(),
            responseHeaders: new Dictionary<string, string> { ["X-Request-Id"] = "abc-123" });

        await using HttpClientTransport transport = new(client);

        ApiRequest request = new("/pets", "GET");
        using ApiResponse response = await transport.SendAsync(request);

        Assert.IsTrue(response.Headers.ContainsKey("X-Request-Id"));
        Assert.AreEqual("abc-123", response.Headers["X-Request-Id"]);
    }

    [TestMethod]
    public async Task SendAsync_NonSuccessStatus_ResponseCaptured()
    {
        using HttpClient client = CreateMockClient(
            HttpStatusCode.NotFound,
            """{"error":"not found"}"""u8.ToArray());

        await using HttpClientTransport transport = new(client);

        ApiRequest request = new("/pets/999", "GET");
        using ApiResponse response = await transport.SendAsync(request);

        Assert.AreEqual(404, response.StatusCode);
        Assert.IsFalse(response.IsSuccess);
    }

    [TestMethod]
    public async Task SendAsync_EmptyBody_ReturnsEmptyMemory()
    {
        using HttpClient client = CreateMockClient(HttpStatusCode.NoContent, []);
        await using HttpClientTransport transport = new(client);

        ApiRequest request = new("/pets/1", "DELETE");
        using ApiResponse response = await transport.SendAsync(request);

        Assert.AreEqual(204, response.StatusCode);
        Assert.AreEqual(0, response.Body.Length);
    }

    [TestMethod]
    public async Task SendAsync_LargeBody_BufferGrowsCorrectly()
    {
        // Create a body larger than the initial 4096 buffer
        byte[] largeBody = new byte[10_000];
        Random.Shared.NextBytes(largeBody);

        using HttpClient client = CreateMockClient(HttpStatusCode.OK, largeBody);
        await using HttpClientTransport transport = new(client);

        ApiRequest request = new("/data", "GET");
        using ApiResponse response = await transport.SendAsync(request);

        Assert.AreEqual(200, response.StatusCode);
        CollectionAssert.AreEqual(largeBody, response.Body.ToArray());
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

        ApiRequest request = new ApiRequest("/search", "GET")
            .WithQueryParameter("q", "hello world&foo=bar");

        using ApiResponse response = await transport.SendAsync(request);

        Assert.IsNotNull(captured);
        string? uri = captured.RequestUri?.OriginalString;
        Assert.IsNotNull(uri);

        // Should be URL-encoded
        Assert.IsFalse(uri.Contains("hello world", StringComparison.Ordinal), "Space should be encoded");
        Assert.IsTrue(uri.Contains("q=", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SendAsync_Put_UsesCorrectMethod()
    {
        HttpRequestMessage? captured = null;

        using HttpClient client = CreateMockClient(
            HttpStatusCode.OK,
            "[]"u8.ToArray(),
            onRequest: req => { captured = req; return Task.CompletedTask; });

        await using HttpClientTransport transport = new(client);

        ApiRequest request = new ApiRequest("/pets/1", "PUT")
            .WithBody("""{"name":"Updated"}"""u8.ToArray());

        using ApiResponse response = await transport.SendAsync(request);

        Assert.IsNotNull(captured);
        Assert.AreEqual(HttpMethod.Put, captured.Method);
    }

    private static HttpClient CreateMockClient(
        HttpStatusCode statusCode,
        byte[] responseBody,
        Func<HttpRequestMessage, Task>? onRequest = null,
        Dictionary<string, string>? responseHeaders = null)
    {
        MockHandler handler = new(statusCode, responseBody, onRequest, responseHeaders);
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
    }

    private sealed class MockHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode statusCode;
        private readonly byte[] responseBody;
        private readonly Func<HttpRequestMessage, Task>? onRequest;
        private readonly Dictionary<string, string>? responseHeaders;

        public MockHandler(
            HttpStatusCode statusCode,
            byte[] responseBody,
            Func<HttpRequestMessage, Task>? onRequest = null,
            Dictionary<string, string>? responseHeaders = null)
        {
            this.statusCode = statusCode;
            this.responseBody = responseBody;
            this.onRequest = onRequest;
            this.responseHeaders = responseHeaders;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (this.onRequest is not null)
            {
                await this.onRequest(request).ConfigureAwait(false);
            }

            HttpResponseMessage response = new(this.statusCode)
            {
                Content = new ByteArrayContent(this.responseBody),
            };

            if (this.responseHeaders is not null)
            {
                foreach (KeyValuePair<string, string> header in this.responseHeaders)
                {
                    response.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            return response;
        }
    }
}