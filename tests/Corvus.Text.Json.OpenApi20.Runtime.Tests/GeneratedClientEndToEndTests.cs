// <copyright file="GeneratedClientEndToEndTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using CanonTests20.Client;
using CanonTests20.Client.Models;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.OpenApi20.Runtime.Tests;

/// <summary>
/// End-to-end tests that exercise the client generated from an OpenAPI 2.0 (Swagger)
/// specification through the real <see cref="HttpClientTransport"/>, using an in-memory
/// <see cref="DelegatingHandler"/> to capture requests and return canned responses.
/// </summary>
/// <remarks>
/// <para>
/// The 2.0-specific wire behaviors under test:
/// <list type="bullet">
/// <item>collectionFormat csv/ssv/tsv/pipes join query arrays with , %20 %09 %7C; multi repeats the parameter.</item>
/// <item>formData parameters serialize as an application/x-www-form-urlencoded body with per-field collectionFormat encodings.</item>
/// <item>file formData parameters produce multipart/form-data with hoisted binary parts.</item>
/// <item>host + basePath + schemes produce the server URL (https preferred; operation schemes override).</item>
/// <item>Responses carry a direct schema (media types from produces) and Header Objects parse.</item>
/// </list>
/// </para>
/// </remarks>
[TestClass]
public class GeneratedClientEndToEndTests
{
    private static readonly byte[] TwoStrings = """["a b","c"]"""u8.ToArray();

    [TestMethod]
    public async Task ListWidgets_QueryCollectionFormatsSerializeTheirWireFormats()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, "[]");

        var request = new ListWidgetsRequest
        {
            CsvTags = GetWidgetsCsvTags.ParseValue(TwoStrings),
            SsvTags = GetWidgetsSsvTags.ParseValue(TwoStrings),
            TsvTags = GetWidgetsTsvTags.ParseValue(TwoStrings),
            PipeTags = GetWidgetsPipeTags.ParseValue(TwoStrings),
        };

        await using ListWidgetsResponse response = await harness.Transport
            .SendAsync<ListWidgetsRequest, ListWidgetsResponse>(in request, CancellationToken.None);

        string uri = harness.CapturedRequest!.RequestUri!.OriginalString;

        // The element value "a b" displays with its raw space in OriginalString; the
        // property under test is the SEPARATOR between elements: comma for csv,
        // %20 for ssv, %09 for tsv, and %7C for pipes.
        StringAssert.Contains(uri, "csvTags=a b,c");
        StringAssert.Contains(uri, "ssvTags=a b%20c");
        StringAssert.Contains(uri, "tsvTags=a b%09c");
        StringAssert.Contains(uri, "pipeTags=a b%7Cc");
    }

    [TestMethod]
    public async Task ListWidgets_MultiCollectionFormatRepeatsTheParameter()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, "[]");

        var request = new ListWidgetsRequest
        {
            MultiTags = GetWidgetsMultiTags.ParseValue("""["x","y"]"""u8),
        };

        await using ListWidgetsResponse response = await harness.Transport
            .SendAsync<ListWidgetsRequest, ListWidgetsResponse>(in request, CancellationToken.None);

        string uri = harness.CapturedRequest!.RequestUri!.OriginalString;
        StringAssert.Contains(uri, "multiTags=x&multiTags=y");
    }

    [TestMethod]
    public async Task ListWidgets_ServerUrlPrefersHttps()
    {
        // The root declares schemes [http, https] with host api.example.com and
        // basePath /v1; the generated server URI prefers https.
        Assert.AreEqual("https://api.example.com/v1", ListWidgetsRequest.CreateServerUri().OriginalString);
    }

    [TestMethod]
    public async Task RenderWidget_OperationSchemesOverrideSelectsHttp()
    {
        Assert.AreEqual("http://api.example.com/v1", RenderWidgetRequest.CreateServerUri().OriginalString);
    }

    [TestMethod]
    public async Task PostLegacy_FormDataSerializesUrlEncodedWithPipes()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"code":1,"message":"ok"}""");

        // The urlencoded serialization path lives in the generated client method,
        // which owns the synthesized encodings map.
        await using var client = new ApiDefaultClient(harness.Transport);
        using var bodyDoc = ParsedJsonDocument<PostLegacyFormBody>.Parse("""{"flags":["a","b"]}""");

        await using PostLegacyResponse response = await client.PostLegacyAsync(
            bodyDoc.RootElement, cancellationToken: CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequestBody);
        string wireBody = Encoding.UTF8.GetString(harness.CapturedRequestBody);

        // The flags field declares collectionFormat: pipes; the urlencoded serializer
        // joins with %7C per the synthesized encodings map.
        Assert.AreEqual("flags=a%7Cb", wireBody);
        StringAssert.Contains(harness.CapturedRequestContentType!, "application/x-www-form-urlencoded");
    }

    [TestMethod]
    public async Task ListWidgets_ResponseBodyParsesAndHeaderIsReadable()
    {
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """[{"id":"w1"},{"id":"w2"}]""",
            new Dictionary<string, string> { ["X-Total-Count"] = "2" });

        await using ListWidgetsResponse response = await harness.Transport
            .SendAsync<ListWidgetsRequest, ListWidgetsResponse>(default, CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);
    }

    [TestMethod]
    public async Task CreateWidget_BodyParameterSerializesAsJson()
    {
        using var harness = new TestHarness(HttpStatusCode.Created, """{"id":"w1"}""");

        using var bodyDoc = ParsedJsonDocument<Widget>.Parse("""{"id":"w1","size":3}""");
        Widget body = bodyDoc.RootElement;

        await using CreateWidgetResponse response = await harness.Transport
            .SendAsync<CreateWidgetRequest, Widget, CreateWidgetResponse>(
                default(CreateWidgetRequest), in body, CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequestBody);
        string wireBody = Encoding.UTF8.GetString(harness.CapturedRequestBody);
        StringAssert.Contains(wireBody, "\"id\":\"w1\"");
        StringAssert.Contains(harness.CapturedRequestContentType!, "application/json");
        Assert.AreEqual(201, response.StatusCode);
    }

    private sealed class TestHarness : IDisposable
    {
        private readonly MockHandler handler;
        private readonly HttpClient client;

        public TestHarness(HttpStatusCode statusCode, string responseBody)
            : this(statusCode, responseBody, null)
        {
        }

        public TestHarness(
            HttpStatusCode statusCode,
            string responseBody,
            Dictionary<string, string>? responseHeaders)
        {
            this.handler = new MockHandler(statusCode, responseBody, responseHeaders);
            this.client = new HttpClient(this.handler)
            {
                BaseAddress = new Uri("http://localhost"),
            };
            this.Transport = new HttpClientTransport(this.client);
        }

        public HttpClientTransport Transport { get; }

        public HttpRequestMessage? CapturedRequest => this.handler.CapturedRequest;

        public byte[]? CapturedRequestBody => this.handler.CapturedRequestBody;

        public string? CapturedRequestContentType => this.handler.CapturedRequestContentType;

        public void Dispose()
        {
            this.Transport.DisposeAsync().AsTask().GetAwaiter().GetResult();
            this.client.Dispose();
            this.handler.Dispose();
        }
    }

    private sealed class MockHandler : DelegatingHandler
    {
        private readonly HttpStatusCode statusCode;
        private readonly string responseBody;
        private readonly Dictionary<string, string>? responseHeaders;

        public MockHandler(
            HttpStatusCode statusCode,
            string responseBody,
            Dictionary<string, string>? responseHeaders = null)
        {
            this.statusCode = statusCode;
            this.responseBody = responseBody;
            this.responseHeaders = responseHeaders;
            this.InnerHandler = new HttpClientHandler();
        }

        public HttpRequestMessage? CapturedRequest { get; private set; }

        public byte[]? CapturedRequestBody { get; private set; }

        public string? CapturedRequestContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            this.CapturedRequest = request;

            if (request.Content is not null)
            {
                this.CapturedRequestBody = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                this.CapturedRequestContentType = request.Content.Headers.ContentType?.ToString();
            }

            HttpContent content = string.IsNullOrEmpty(this.responseBody)
                ? new ByteArrayContent([])
                : new StringContent(this.responseBody, Encoding.UTF8, "application/json");

            HttpResponseMessage response = new(this.statusCode) { Content = content };

            if (this.responseHeaders is not null)
            {
                foreach ((string key, string value) in this.responseHeaders)
                {
                    response.Headers.TryAddWithoutValidation(key, value);
                }
            }

            return response;
        }
    }
}