// <copyright file="HttpClientTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
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

        TestGetRequest request = default;
        await using TestResponse response =
            await transport.SendAsync<TestGetRequest, TestResponse>(in request);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);
        Assert.AreEqual(expected.Length, response.ContentLength);
    }

    [TestMethod]
    public async Task SendAsync_Get_CorrectMethodOnWire()
    {
        HttpRequestMessage? captured = null;

        using HttpClient client = CreateMockClient(
            HttpStatusCode.OK,
            "[]"u8.ToArray(),
            onRequest: req => { captured = req; return Task.CompletedTask; });

        await using HttpClientTransport transport = new(client);

        TestGetRequest request = default;
        await using TestResponse response =
            await transport.SendAsync<TestGetRequest, TestResponse>(in request);

        Assert.IsNotNull(captured);
        Assert.AreEqual(HttpMethod.Get, captured.Method);
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

        TestQueryRequest request = new(limit: 10, offset: 20);
        await using TestResponse response =
            await transport.SendAsync<TestQueryRequest, TestResponse>(in request);

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

        TestHeaderRequest request = new("value123");
        await using TestResponse response =
            await transport.SendAsync<TestHeaderRequest, TestResponse>(in request);

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

        TestGetRequest request = default;
        await using TestResponse response =
            await transport.SendAsync<TestGetRequest, TestResponse>(in request);

        Assert.AreEqual(404, response.StatusCode);
        Assert.IsFalse(response.IsSuccess);
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

        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse("""{"name":"Rex"}""");
        JsonElement body = doc.RootElement;

        TestPostRequest request = default;
        await using TestResponse response =
            await transport.SendAsync<TestPostRequest, JsonElement, TestResponse>(in request, in body);

        Assert.AreEqual(201, response.StatusCode);
        Assert.IsNotNull(captured);
        Assert.AreEqual(HttpMethod.Post, captured.Method);
        Assert.IsNotNull(capturedBody);

        string bodyStr = Encoding.UTF8.GetString(capturedBody);
        Assert.IsTrue(bodyStr.Contains("Rex", StringComparison.Ordinal));
        Assert.AreEqual("application/json", captured.Content?.Headers.ContentType?.MediaType);
    }

    [TestMethod]
    public async Task DisposeAsync_WithDisposeClient_DisposesHttpClient()
    {
        MockHandler handler = new(HttpStatusCode.OK, []);
        HttpClient client = new(handler);

        HttpClientTransport transport = new(client, disposeClient: true);
        await transport.DisposeAsync();

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

        HttpResponseMessage response = await client.GetAsync(new Uri("http://localhost/test"));
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task SendAsync_PathParameters_ResolvedCorrectly()
    {
        HttpRequestMessage? captured = null;

        using HttpClient client = CreateMockClient(
            HttpStatusCode.OK,
            """{"id":42}"""u8.ToArray(),
            onRequest: req => { captured = req; return Task.CompletedTask; });

        await using HttpClientTransport transport = new(client);

        TestPathRequest request = new(42);
        await using TestResponse response =
            await transport.SendAsync<TestPathRequest, TestResponse>(in request);

        Assert.IsNotNull(captured);
        string? uri = captured.RequestUri?.OriginalString;
        Assert.IsNotNull(uri);
        Assert.IsTrue(uri.EndsWith("/pets/42", StringComparison.Ordinal), $"Expected URI ending with '/pets/42' but got '{uri}'");
    }

    [TestMethod]
    public async Task SendAsync_AllOptionalQueryParams_DiscardsQuestionMark()
    {
        HttpRequestMessage? captured = null;

        using HttpClient client = CreateMockClient(
            HttpStatusCode.OK,
            "[]"u8.ToArray(),
            onRequest: req => { captured = req; return Task.CompletedTask; });

        await using HttpClientTransport transport = new(client);

        TestAllOptionalQueryRequest request = default;
        await using TestResponse response =
            await transport.SendAsync<TestAllOptionalQueryRequest, TestResponse>(in request);

        Assert.IsNotNull(captured);
        string? uri = captured.RequestUri?.OriginalString;
        Assert.IsNotNull(uri);

        // When all optional query params return 0 bytes, the URI should not contain '?'
        Assert.IsTrue(uri.EndsWith("/pets", StringComparison.Ordinal), $"Expected URI ending with '/pets' but got '{uri}'");
        Assert.IsFalse(uri.Contains('?'), $"Expected no '?' in URI but got '{uri}'");
    }

    [TestMethod]
    public async Task SendAsync_PathAndQueryParameters_BothResolved()
    {
        HttpRequestMessage? captured = null;

        using HttpClient client = CreateMockClient(
            HttpStatusCode.OK,
            "[]"u8.ToArray(),
            onRequest: req => { captured = req; return Task.CompletedTask; });

        await using HttpClientTransport transport = new(client);

        TestPathAndQueryRequest request = new(42, 10);
        await using TestResponse response =
            await transport.SendAsync<TestPathAndQueryRequest, TestResponse>(in request);

        Assert.IsNotNull(captured);
        string? uri = captured.RequestUri?.OriginalString;
        Assert.IsNotNull(uri);
        Assert.IsTrue(uri.Contains("/pets/42", StringComparison.Ordinal), $"Expected '/pets/42' in '{uri}'");
        Assert.IsTrue(uri.Contains("?limit=10", StringComparison.Ordinal), $"Expected '?limit=10' in '{uri}'");
    }

    [TestMethod]
    public async Task SendAsync_PutMethod_MapsCorrectly()
    {
        HttpRequestMessage? captured = null;

        using HttpClient client = CreateMockClient(
            HttpStatusCode.OK,
            "[]"u8.ToArray(),
            onRequest: req => { captured = req; return Task.CompletedTask; });

        await using HttpClientTransport transport = new(client);

        TestPutRequest request = default;
        await using TestResponse response =
            await transport.SendAsync<TestPutRequest, TestResponse>(in request);

        Assert.IsNotNull(captured);
        Assert.AreEqual(HttpMethod.Put, captured.Method);
    }

    [TestMethod]
    public async Task SendAsync_DeleteMethod_MapsCorrectly()
    {
        HttpRequestMessage? captured = null;

        using HttpClient client = CreateMockClient(
            HttpStatusCode.OK,
            "[]"u8.ToArray(),
            onRequest: req => { captured = req; return Task.CompletedTask; });

        await using HttpClientTransport transport = new(client);

        TestDeleteRequest request = default;
        await using TestResponse response =
            await transport.SendAsync<TestDeleteRequest, TestResponse>(in request);

        Assert.IsNotNull(captured);
        Assert.AreEqual(HttpMethod.Delete, captured.Method);
    }

    [TestMethod]
    public async Task SendAsync_PatchMethod_MapsCorrectly()
    {
        HttpRequestMessage? captured = null;

        using HttpClient client = CreateMockClient(
            HttpStatusCode.OK,
            "[]"u8.ToArray(),
            onRequest: req => { captured = req; return Task.CompletedTask; });

        await using HttpClientTransport transport = new(client);

        TestPatchRequest request = default;
        await using TestResponse response =
            await transport.SendAsync<TestPatchRequest, TestResponse>(in request);

        Assert.IsNotNull(captured);
        Assert.AreEqual(HttpMethod.Patch, captured.Method);
    }

    [TestMethod]
    public async Task SendAsync_HeadMethod_MapsCorrectly()
    {
        HttpRequestMessage? captured = null;

        using HttpClient client = CreateMockClient(
            HttpStatusCode.OK,
            "[]"u8.ToArray(),
            onRequest: req => { captured = req; return Task.CompletedTask; });

        await using HttpClientTransport transport = new(client);

        TestHeadRequest request = default;
        await using TestResponse response =
            await transport.SendAsync<TestHeadRequest, TestResponse>(in request);

        Assert.IsNotNull(captured);
        Assert.AreEqual(HttpMethod.Head, captured.Method);
    }

    [TestMethod]
    public async Task SendAsync_OptionsMethod_MapsCorrectly()
    {
        HttpRequestMessage? captured = null;

        using HttpClient client = CreateMockClient(
            HttpStatusCode.OK,
            "[]"u8.ToArray(),
            onRequest: req => { captured = req; return Task.CompletedTask; });

        await using HttpClientTransport transport = new(client);

        TestOptionsRequest request = default;
        await using TestResponse response =
            await transport.SendAsync<TestOptionsRequest, TestResponse>(in request);

        Assert.IsNotNull(captured);
        Assert.AreEqual(HttpMethod.Options, captured.Method);
    }

    [TestMethod]
    public async Task SendAsync_TraceMethod_MapsCorrectly()
    {
        HttpRequestMessage? captured = null;

        using HttpClient client = CreateMockClient(
            HttpStatusCode.OK,
            "[]"u8.ToArray(),
            onRequest: req => { captured = req; return Task.CompletedTask; });

        await using HttpClientTransport transport = new(client);

        TestTraceRequest request = default;
        await using TestResponse response =
            await transport.SendAsync<TestTraceRequest, TestResponse>(in request);

        Assert.IsNotNull(captured);
        Assert.AreEqual(HttpMethod.Trace, captured.Method);
    }

    [TestMethod]
    public async Task SendCoreAsync_WhenCreateAsyncThrows_DisposesHttpResponse()
    {
        using HttpClient client = CreateMockClient(
            HttpStatusCode.OK,
            "[]"u8.ToArray());

        await using HttpClientTransport transport = new(client);

        TestGetRequest request = default;

        // ThrowingResponse.CreateAsync always throws, exercising the catch block
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await using ThrowingResponse response =
                await transport.SendAsync<TestGetRequest, ThrowingResponse>(in request);
        });
    }

    [TestMethod]
    public async Task SendAsync_CookieParameters_WrittenToHeader()
    {
        HttpRequestMessage? captured = null;

        using HttpClient client = CreateMockClient(
            HttpStatusCode.OK,
            "[]"u8.ToArray(),
            onRequest: req => { captured = req; return Task.CompletedTask; });

        await using HttpClientTransport transport = new(client);

        TestCookieRequest request = new("session", "abc123");
        await using TestResponse response =
            await transport.SendAsync<TestCookieRequest, TestResponse>(in request);

        Assert.IsNotNull(captured);
        Assert.IsTrue(captured.Headers.Contains("Cookie"));
        string cookie = captured.Headers.GetValues("Cookie").First();
        Assert.AreEqual("session=abc123", cookie);
    }

    [TestMethod]
    public async Task SendAsync_CookiesWriteZeroBytes_NoCookieHeader()
    {
        HttpRequestMessage? captured = null;

        using HttpClient client = CreateMockClient(
            HttpStatusCode.OK,
            "[]"u8.ToArray(),
            onRequest: req => { captured = req; return Task.CompletedTask; });

        await using HttpClientTransport transport = new(client);

        TestEmptyCookieRequest request = default;
        await using TestResponse response =
            await transport.SendAsync<TestEmptyCookieRequest, TestResponse>(in request);

        Assert.IsNotNull(captured);
        Assert.IsFalse(captured.Headers.Contains("Cookie"));
    }

    [TestMethod]
    public async Task SendAsync_AsyncApiMethod_CreatesCustomHttpMethod()
    {
        HttpRequestMessage? captured = null;

        using HttpClient client = CreateMockClient(
            HttpStatusCode.OK,
            "[]"u8.ToArray(),
            onRequest: req => { captured = req; return Task.CompletedTask; });

        await using HttpClientTransport transport = new(client);

        TestPublishRequest request = default;
        await using TestResponse response =
            await transport.SendAsync<TestPublishRequest, TestResponse>(in request);

        Assert.IsNotNull(captured);
        Assert.AreEqual("Publish", captured.Method.Method);
    }

    [TestMethod]
    public async Task SendAsync_ResponseHeaders_PassedAndReadable()
    {
        using MockHandler handler = new(
            HttpStatusCode.OK,
            """{"id":1}"""u8.ToArray(),
            responseHeaders: new Dictionary<string, string[]>
            {
                ["X-Request-Id"] = ["req-42"],
                ["X-Rate-Limit"] = ["100"],
            });
        using HttpClient client = new(handler) { BaseAddress = new Uri("http://localhost") };

        await using HttpClientTransport transport = new(client);

        TestGetRequest request = default;
        await using HeaderCapturingResponse response =
            await transport.SendAsync<TestGetRequest, HeaderCapturingResponse>(in request);

        Assert.AreEqual(200, response.StatusCode);
        Assert.AreEqual("req-42", response.CapturedRequestId);
        Assert.AreEqual("100", response.CapturedRateLimit);
    }

    [TestMethod]
    public async Task SendAsync_ResponseHeaders_MissingHeaderReturnsNull()
    {
        using MockHandler handler = new(
            HttpStatusCode.OK,
            """{"id":1}"""u8.ToArray(),
            responseHeaders: new Dictionary<string, string[]>
            {
                ["X-Request-Id"] = ["req-42"],
            });
        using HttpClient client = new(handler) { BaseAddress = new Uri("http://localhost") };

        await using HttpClientTransport transport = new(client);

        TestGetRequest request = default;
        await using HeaderCapturingResponse response =
            await transport.SendAsync<TestGetRequest, HeaderCapturingResponse>(in request);

        Assert.AreEqual("req-42", response.CapturedRequestId);
        Assert.IsNull(response.CapturedRateLimit);
    }

    [TestMethod]
    public async Task SendAsync_ResponseHeaders_MultipleValues_AreCommaJoined()
    {
        using MockHandler handler = new(
            HttpStatusCode.OK,
            """{"id":1}"""u8.ToArray(),
            responseHeaders: new Dictionary<string, string[]>
            {
                ["X-Request-Id"] = ["val-A", "val-B", "val-C"],
            });
        using HttpClient client = new(handler) { BaseAddress = new Uri("http://localhost") };

        await using HttpClientTransport transport = new(client);

        TestGetRequest request = default;
        await using HeaderCapturingResponse response =
            await transport.SendAsync<TestGetRequest, HeaderCapturingResponse>(in request);

        Assert.AreEqual("val-A, val-B, val-C", response.CapturedRequestId);
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
        private readonly Dictionary<string, string[]>? responseHeaders;

        public MockHandler(
            HttpStatusCode statusCode,
            byte[] responseBody,
            Func<HttpRequestMessage, Task>? onRequest = null,
            Dictionary<string, string[]>? responseHeaders = null)
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
                foreach (KeyValuePair<string, string[]> kvp in this.responseHeaders)
                {
                    response.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                }
            }

            return response;
        }
    }

    /// <summary>
    /// Minimal response for testing — just captures status and content length.
    /// </summary>
    private struct TestResponse : IApiResponse<TestResponse>
    {
        public int StatusCode { get; private set; }

        public bool IsSuccess => this.StatusCode >= 200 && this.StatusCode < 300;

        public int ContentLength { get; private set; }

        public static async ValueTask<TestResponse> CreateAsync(
            int statusCode,
            Stream contentStream,
            string? contentType = null,
            IResponseHeaders? responseHeaders = null,
            IAsyncDisposable? owner = null,
            CancellationToken cancellationToken = default)
        {
            using MemoryStream ms = new();
            await contentStream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);

            if (owner is not null)
            {
                await owner.DisposeAsync().ConfigureAwait(false);
            }

            return new TestResponse
            {
                StatusCode = statusCode,
                ContentLength = (int)ms.Length,
            };
        }

        public ValueTask DisposeAsync() => default;
    }

    /// <summary>
    /// Response type that always throws from CreateAsync — tests the catch block in SendCoreAsync.
    /// </summary>
    private struct ThrowingResponse : IApiResponse<ThrowingResponse>
    {
        public int StatusCode { get; private set; }

        public bool IsSuccess => false;

        public static ValueTask<ThrowingResponse> CreateAsync(
            int statusCode,
            Stream contentStream,
            string? contentType = null,
            IResponseHeaders? responseHeaders = null,
            IAsyncDisposable? owner = null,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated CreateAsync failure");
        }

        public ValueTask DisposeAsync() => default;
    }

    /// <summary>GET /pets/1 — no parameters.</summary>
    private readonly struct TestGetRequest : IApiRequest<TestGetRequest>
    {
        public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets/1"u8;

        public static OperationMethod Method => OperationMethod.Get;

        public static bool HasPathParameters => false;

        public static bool HasQueryParameters => false;

        public static bool HasHeaderParameters => false;

        public static bool HasCookieParameters => false;

        public void WriteResolvedPath(IBufferWriter<byte> writer) { }

        public int WriteQueryString(IBufferWriter<byte> writer) => 0;

        public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state) { }

        public int WriteCookies(IBufferWriter<byte> writer) => 0;
    }

    /// <summary>GET /pets with query parameters.</summary>
    private readonly struct TestQueryRequest : IApiRequest<TestQueryRequest>
    {
        private readonly int limit;
        private readonly int offset;

        public TestQueryRequest(int limit, int offset)
        {
            this.limit = limit;
            this.offset = offset;
        }

        public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets"u8;

        public static OperationMethod Method => OperationMethod.Get;

        public static bool HasPathParameters => false;

        public static bool HasQueryParameters => true;

        public static bool HasHeaderParameters => false;

        public static bool HasCookieParameters => false;

        public void WriteResolvedPath(IBufferWriter<byte> writer)
        {
            writer.Write("/pets"u8);
        }

        public int WriteQueryString(IBufferWriter<byte> writer)
        {
            byte[] query = Encoding.UTF8.GetBytes($"limit={this.limit}&offset={this.offset}");
            writer.Write(query);
            return query.Length;
        }

        public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state) { }

        public int WriteCookies(IBufferWriter<byte> writer) => 0;
    }

    /// <summary>GET /pets with X-Custom header.</summary>
    private readonly struct TestHeaderRequest : IApiRequest<TestHeaderRequest>
    {
        private readonly string headerValue;

        public TestHeaderRequest(string headerValue)
        {
            this.headerValue = headerValue;
        }

        public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets"u8;

        public static OperationMethod Method => OperationMethod.Get;

        public static bool HasPathParameters => false;

        public static bool HasQueryParameters => false;

        public static bool HasHeaderParameters => true;

        public static bool HasCookieParameters => false;

        public void WriteResolvedPath(IBufferWriter<byte> writer)
        {
            writer.Write("/pets"u8);
        }

        public int WriteQueryString(IBufferWriter<byte> writer) => 0;

        public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state)
        {
            callback("X-Custom"u8, Encoding.UTF8.GetBytes(this.headerValue), state);
        }

        public int WriteCookies(IBufferWriter<byte> writer) => 0;
    }

    /// <summary>POST /pets — body operation, no parameters.</summary>
    private readonly struct TestPostRequest : IApiRequest<TestPostRequest>
    {
        public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets"u8;

        public static OperationMethod Method => OperationMethod.Post;

        public static bool HasPathParameters => false;

        public static bool HasQueryParameters => false;

        public static bool HasHeaderParameters => false;

        public static bool HasCookieParameters => false;

        public void WriteResolvedPath(IBufferWriter<byte> writer) { }

        public int WriteQueryString(IBufferWriter<byte> writer) => 0;

        public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state) { }

        public int WriteCookies(IBufferWriter<byte> writer) => 0;
    }

    /// <summary>GET /pets/{petId} — path parameter.</summary>
    private readonly struct TestPathRequest : IApiRequest<TestPathRequest>
    {
        private readonly int petId;

        public TestPathRequest(int petId)
        {
            this.petId = petId;
        }

        public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets/{petId}"u8;

        public static OperationMethod Method => OperationMethod.Get;

        public static bool HasPathParameters => true;

        public static bool HasQueryParameters => false;

        public static bool HasHeaderParameters => false;

        public static bool HasCookieParameters => false;

        public void WriteResolvedPath(IBufferWriter<byte> writer)
        {
            byte[] path = Encoding.UTF8.GetBytes($"/pets/{this.petId}");
            writer.Write(path);
        }

        public int WriteQueryString(IBufferWriter<byte> writer) => 0;

        public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state) { }

        public int WriteCookies(IBufferWriter<byte> writer) => 0;
    }

    /// <summary>GET /pets with HasQueryParameters=true but all optional (returns 0 bytes).</summary>
    private readonly struct TestAllOptionalQueryRequest : IApiRequest<TestAllOptionalQueryRequest>
    {
        public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets"u8;

        public static OperationMethod Method => OperationMethod.Get;

        public static bool HasPathParameters => false;

        public static bool HasQueryParameters => true;

        public static bool HasHeaderParameters => false;

        public static bool HasCookieParameters => false;

        public void WriteResolvedPath(IBufferWriter<byte> writer)
        {
            writer.Write("/pets"u8);
        }

        public int WriteQueryString(IBufferWriter<byte> writer) => 0;

        public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state) { }

        public int WriteCookies(IBufferWriter<byte> writer) => 0;
    }

    /// <summary>PUT /pets — tests MapMethod for Put.</summary>
    private readonly struct TestPutRequest : IApiRequest<TestPutRequest>
    {
        public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets"u8;

        public static OperationMethod Method => OperationMethod.Put;

        public static bool HasPathParameters => false;

        public static bool HasQueryParameters => false;

        public static bool HasHeaderParameters => false;

        public static bool HasCookieParameters => false;

        public void WriteResolvedPath(IBufferWriter<byte> writer) { }

        public int WriteQueryString(IBufferWriter<byte> writer) => 0;

        public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state) { }

        public int WriteCookies(IBufferWriter<byte> writer) => 0;
    }

    /// <summary>DELETE /pets — tests MapMethod for Delete.</summary>
    private readonly struct TestDeleteRequest : IApiRequest<TestDeleteRequest>
    {
        public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets"u8;

        public static OperationMethod Method => OperationMethod.Delete;

        public static bool HasPathParameters => false;

        public static bool HasQueryParameters => false;

        public static bool HasHeaderParameters => false;

        public static bool HasCookieParameters => false;

        public void WriteResolvedPath(IBufferWriter<byte> writer) { }

        public int WriteQueryString(IBufferWriter<byte> writer) => 0;

        public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state) { }

        public int WriteCookies(IBufferWriter<byte> writer) => 0;
    }

    /// <summary>PATCH /pets — tests MapMethod for Patch.</summary>
    private readonly struct TestPatchRequest : IApiRequest<TestPatchRequest>
    {
        public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets"u8;

        public static OperationMethod Method => OperationMethod.Patch;

        public static bool HasPathParameters => false;

        public static bool HasQueryParameters => false;

        public static bool HasHeaderParameters => false;

        public static bool HasCookieParameters => false;

        public void WriteResolvedPath(IBufferWriter<byte> writer) { }

        public int WriteQueryString(IBufferWriter<byte> writer) => 0;

        public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state) { }

        public int WriteCookies(IBufferWriter<byte> writer) => 0;
    }

    /// <summary>HEAD /pets — tests MapMethod for Head.</summary>
    private readonly struct TestHeadRequest : IApiRequest<TestHeadRequest>
    {
        public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets"u8;

        public static OperationMethod Method => OperationMethod.Head;

        public static bool HasPathParameters => false;

        public static bool HasQueryParameters => false;

        public static bool HasHeaderParameters => false;

        public static bool HasCookieParameters => false;

        public void WriteResolvedPath(IBufferWriter<byte> writer) { }

        public int WriteQueryString(IBufferWriter<byte> writer) => 0;

        public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state) { }

        public int WriteCookies(IBufferWriter<byte> writer) => 0;
    }

    /// <summary>OPTIONS /pets — tests MapMethod for Options.</summary>
    private readonly struct TestOptionsRequest : IApiRequest<TestOptionsRequest>
    {
        public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets"u8;

        public static OperationMethod Method => OperationMethod.Options;

        public static bool HasPathParameters => false;

        public static bool HasQueryParameters => false;

        public static bool HasHeaderParameters => false;

        public static bool HasCookieParameters => false;

        public void WriteResolvedPath(IBufferWriter<byte> writer) { }

        public int WriteQueryString(IBufferWriter<byte> writer) => 0;

        public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state) { }

        public int WriteCookies(IBufferWriter<byte> writer) => 0;
    }

    /// <summary>TRACE /pets — tests MapMethod for Trace.</summary>
    private readonly struct TestTraceRequest : IApiRequest<TestTraceRequest>
    {
        public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets"u8;

        public static OperationMethod Method => OperationMethod.Trace;

        public static bool HasPathParameters => false;

        public static bool HasQueryParameters => false;

        public static bool HasHeaderParameters => false;

        public static bool HasCookieParameters => false;

        public void WriteResolvedPath(IBufferWriter<byte> writer) { }

        public int WriteQueryString(IBufferWriter<byte> writer) => 0;

        public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state) { }

        public int WriteCookies(IBufferWriter<byte> writer) => 0;
    }

    /// <summary>GET /pets/{petId} with query params too.</summary>
    private readonly struct TestPathAndQueryRequest : IApiRequest<TestPathAndQueryRequest>
    {
        private readonly int petId;
        private readonly int limit;

        public TestPathAndQueryRequest(int petId, int limit)
        {
            this.petId = petId;
            this.limit = limit;
        }

        public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets/{petId}"u8;

        public static OperationMethod Method => OperationMethod.Get;

        public static bool HasPathParameters => true;

        public static bool HasQueryParameters => true;

        public static bool HasHeaderParameters => false;

        public static bool HasCookieParameters => false;

        public void WriteResolvedPath(IBufferWriter<byte> writer)
        {
            byte[] path = Encoding.UTF8.GetBytes($"/pets/{this.petId}");
            writer.Write(path);
        }

        public int WriteQueryString(IBufferWriter<byte> writer)
        {
            byte[] query = Encoding.UTF8.GetBytes($"limit={this.limit}");
            writer.Write(query);
            return query.Length;
        }

        public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state) { }

        public int WriteCookies(IBufferWriter<byte> writer) => 0;
    }

    /// <summary>GET /pets with cookie parameters.</summary>
    private readonly struct TestCookieRequest : IApiRequest<TestCookieRequest>
    {
        private readonly string cookieName;
        private readonly string cookieValue;

        public TestCookieRequest(string cookieName, string cookieValue)
        {
            this.cookieName = cookieName;
            this.cookieValue = cookieValue;
        }

        public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets"u8;

        public static OperationMethod Method => OperationMethod.Get;

        public static bool HasPathParameters => false;

        public static bool HasQueryParameters => false;

        public static bool HasHeaderParameters => false;

        public static bool HasCookieParameters => true;

        public void WriteResolvedPath(IBufferWriter<byte> writer) { }

        public int WriteQueryString(IBufferWriter<byte> writer) => 0;

        public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state) { }

        public int WriteCookies(IBufferWriter<byte> writer)
        {
            byte[] cookie = Encoding.UTF8.GetBytes($"{this.cookieName}={this.cookieValue}");
            writer.Write(cookie);
            return cookie.Length;
        }
    }

    /// <summary>GET /pets with HasCookieParameters=true but WriteCookies returns 0.</summary>
    private readonly struct TestEmptyCookieRequest : IApiRequest<TestEmptyCookieRequest>
    {
        public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets"u8;

        public static OperationMethod Method => OperationMethod.Get;

        public static bool HasPathParameters => false;

        public static bool HasQueryParameters => false;

        public static bool HasHeaderParameters => false;

        public static bool HasCookieParameters => true;

        public void WriteResolvedPath(IBufferWriter<byte> writer) { }

        public int WriteQueryString(IBufferWriter<byte> writer) => 0;

        public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state) { }

        public int WriteCookies(IBufferWriter<byte> writer) => 0;
    }

    /// <summary>Publish /events — tests the unknown-method fallback in MapMethod.</summary>
    private readonly struct TestPublishRequest : IApiRequest<TestPublishRequest>
    {
        public static ReadOnlySpan<byte> PathTemplateUtf8 => "/events"u8;

        public static OperationMethod Method => OperationMethod.Publish;

        public static bool HasPathParameters => false;

        public static bool HasQueryParameters => false;

        public static bool HasHeaderParameters => false;

        public static bool HasCookieParameters => false;

        public void WriteResolvedPath(IBufferWriter<byte> writer) { }

        public int WriteQueryString(IBufferWriter<byte> writer) => 0;

        public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state) { }

        public int WriteCookies(IBufferWriter<byte> writer) => 0;
    }

    /// <summary>
    /// Response that captures response headers via <see cref="IResponseHeaders"/>.
    /// </summary>
    private struct HeaderCapturingResponse : IApiResponse<HeaderCapturingResponse>
    {
        public int StatusCode { get; private set; }

        public bool IsSuccess => this.StatusCode >= 200 && this.StatusCode < 300;

        public string? CapturedRequestId { get; private set; }

        public string? CapturedRateLimit { get; private set; }

        public static async ValueTask<HeaderCapturingResponse> CreateAsync(
            int statusCode,
            Stream contentStream,
            string? contentType = null,
            IResponseHeaders? responseHeaders = null,
            IAsyncDisposable? owner = null,
            CancellationToken cancellationToken = default)
        {
            // Drain the stream to avoid leaks.
            using MemoryStream ms = new();
            await contentStream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);

            string? requestId = null;
            string? rateLimit = null;
            if (responseHeaders is not null)
            {
                responseHeaders.TryGetValue("X-Request-Id", out requestId);
                responseHeaders.TryGetValue("X-Rate-Limit", out rateLimit);
            }

            if (owner is not null)
            {
                await owner.DisposeAsync().ConfigureAwait(false);
            }

            return new HeaderCapturingResponse
            {
                StatusCode = statusCode,
                CapturedRequestId = requestId,
                CapturedRateLimit = rateLimit,
            };
        }

        public ValueTask DisposeAsync() => default;
    }
}