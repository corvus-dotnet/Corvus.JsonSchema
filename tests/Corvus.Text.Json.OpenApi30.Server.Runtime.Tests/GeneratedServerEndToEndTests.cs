// <copyright file="GeneratedServerEndToEndTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using CanonTests30.Server;
using CanonTests30.Server.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Corvus.Text.Json.OpenApi30.Server.Runtime.Tests;

[TestClass]
public class GeneratedServerEndToEndTests
{
    private static IHost? host;
    private static HttpClient? client;

    private static HttpClient Client => client ?? throw new InvalidOperationException("The test client has not been initialized.");

    [ClassInitialize]
    public static async Task ClassInit(TestContext context)
    {
        HostBuilder builder = new();
        builder.ConfigureWebHost(webHost =>
        {
            webHost.UseTestServer();
            webHost.ConfigureServices(services => { services.AddRouting(); });
            webHost.Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapApiEndpoints(
                        new MockDefaultHandler(),
                        new MockItemsHandler(),
                        new MockSearchHandler());
                });
            });
        });

        host = await builder.StartAsync();
        client = host.GetTestClient();
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        client?.Dispose();
        if (host is not null)
        {
            await host.StopAsync();
        }

        host?.Dispose();
    }

    [TestMethod]
    public async Task ListItems_WithQueryParameters_ReturnsOk()
    {
        HttpResponseMessage response = await Client.GetAsync("/items?active=true&category=tools&page=5&sort=name&verbose=false");
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """[{"id":1,"name":"Test Item","payload":{"active":true,"category":"tools","page":5,"sort":"name","verbose":false}}]""");
    }

    [TestMethod]
    public async Task ListItems_QueryParams_ArePassedToHandler()
    {
        HttpResponseMessage response = await Client.GetAsync("/items?active=true&page=5");
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """[{"id":1,"name":"Test Item","payload":{"active":true,"page":5}}]""");
    }

    [TestMethod]
    public async Task CreateItem_WithValidJsonBody_ReturnsCreated()
    {
        StringContent content = new("""{"name":"Widget","tag":"tools"}""", Encoding.UTF8, "application/json");
        HttpResponseMessage response = await Client.PostAsync("/items", content);
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, """{"id":"new-1","name":"Created"}""");
    }

    [TestMethod]
    public async Task GetItem_WithPathParameter_ReturnsOk()
    {
        HttpResponseMessage response = await Client.GetAsync("/items/item-123");
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """{"id":42,"name":"Widget-item-123"}""");
        Assert.AreEqual("100", response.Headers.GetValues("X-Rate-Limit").Single());
        Assert.AreEqual("true", response.Headers.GetValues("X-Active").Single());
        Assert.AreEqual("req-123", response.Headers.GetValues("X-Request-Id").Single());
        Assert.AreEqual("alpha,beta", response.Headers.GetValues("X-Tags").Single());
        Assert.AreEqual("10,25", response.Headers.GetValues("X-Page-Sizes").Single());
        Assert.AreEqual("True,False", response.Headers.GetValues("X-Flags").Single());
    }

    [TestMethod]
    public async Task UpdateItemForm_WithFormUrlEncodedBody_ReturnsOk()
    {
        FormUrlEncodedContent content = new(
        [
            new KeyValuePair<string, string>("name", "Widget"),
            new KeyValuePair<string, string>("tags", "blue"),
            new KeyValuePair<string, string>("tags", "green"),
        ]);

        HttpResponseMessage response = await Client.PostAsync("/items/item-42/form", content);
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """{"id":7,"name":"Widget","payload":{"name":"Widget","tags":["blue","green"]}}""");
    }

    [TestMethod]
    public async Task UploadItemData_WithMultipartBody_ReturnsCreated()
    {
        MultipartFormDataContent content = new();
        content.Add(new ByteArrayContent("file-data"u8.ToArray()), "file", "test.bin");
        content.Add(new StringContent("A test file"), "description");

        HttpResponseMessage response = await Client.PostAsync("/items/item-42/upload", content);
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, """{"id":8,"name":"Uploaded"}""");
    }

    [TestMethod]
    public async Task DownloadFile_WhenRequested_ReturnsOk()
    {
        HttpResponseMessage response = await Client.GetAsync("/download");

        // The download endpoint returns its file content (the mock returns "file-content"); the previous assertion
        // expected an empty body, which never matched the handler. Assert the real contract: status + body.
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("file-content", await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task OptionalBodyProbe_WithoutBody_ReturnsNoContent()
    {
        // Regression (optional request bodies): a body-less POST to an operation whose requestBody is required:false
        // must not be rejected by the dispatch — the empty body binds undefined and the handler runs, returning 204.
        HttpResponseMessage response = await Client.PostAsync("/optional-body-probe", null);
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    [TestMethod]
    public async Task OptionalBodyProbe_WithBody_ReturnsNoContent()
    {
        // The same operation still accepts (and parses) a body when one is supplied.
        StringContent content = new("""{"note":"hello"}""", Encoding.UTF8, "application/json");
        HttpResponseMessage response = await Client.PostAsync("/optional-body-probe", content);
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    [TestMethod]
    public async Task SearchItems_WithAllRequiredParams_ReturnsOk()
    {
        HttpResponseMessage response = await Client.SendAsync(CreateSearchRequest("/search?q=test"));
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """[{"id":1,"name":"test","payload":{"q":"test","session":"sess-123","xCorrelationId":"corr-001"}}]""");
    }

    [TestMethod]
    public async Task SearchItems_MissingRequiredQuery_ReturnsBadRequest()
    {
        HttpResponseMessage response = await Client.SendAsync(CreateSearchRequest("/search"));
        await AssertJsonResponseAsync(response, HttpStatusCode.BadRequest, """{"type":"about:blank","title":"Bad Request","status":400,"detail":"The required parameter 'q' is missing."}""");
    }

    [TestMethod]
    public async Task SearchItems_MissingRequiredCookie_ReturnsBadRequest()
    {
        HttpResponseMessage response = await Client.SendAsync(CreateSearchRequest("/search?q=test", cookieHeader: null));
        await AssertJsonResponseAsync(response, HttpStatusCode.BadRequest, """{"type":"about:blank","title":"Bad Request","status":400,"detail":"The required parameter 'session' is missing."}""");
    }

    [TestMethod]
    public async Task SearchItems_MissingRequiredHeader_ReturnsBadRequest()
    {
        HttpResponseMessage response = await Client.SendAsync(CreateSearchRequest("/search?q=test", correlationId: null));
        await AssertJsonResponseAsync(response, HttpStatusCode.BadRequest, """{"type":"about:blank","title":"Bad Request","status":400,"detail":"The required parameter 'X-Correlation-Id' is missing."}""");
    }

    [TestMethod]
    public async Task SearchItems_WithPipeDelimitedTags_ReturnsOk()
    {
        HttpResponseMessage response = await Client.SendAsync(CreateSearchRequest("/search?q=test&tags=foo%7Cbar%7Cbaz"));
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """[{"id":1,"name":"test","payload":{"q":"test","tags":["foo","bar","baz"],"session":"sess-123","xCorrelationId":"corr-001"}}]""");
    }

    [TestMethod]
    public async Task SearchItems_WithSpaceDelimitedCoords_ReturnsOk()
    {
        HttpResponseMessage response = await Client.SendAsync(CreateSearchRequest("/search?q=test&coords=1.5%202.5%203.5"));
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """[{"id":1,"name":"test","payload":{"q":"test","coords":[1.5,2.5,3.5],"session":"sess-123","xCorrelationId":"corr-001"}}]""");
    }

    [TestMethod]
    public async Task SearchItems_WithDeepObjectFilter_ReturnsOk()
    {
        HttpResponseMessage response = await Client.SendAsync(CreateSearchRequest("/search?q=test&filter%5Bstatus%5D=active&filter%5Bpriority%5D=high"));
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """[{"id":1,"name":"test","payload":{"q":"test","filter":{"status":"active","priority":"high"},"session":"sess-123","xCorrelationId":"corr-001"}}]""");
    }

    [TestMethod]
    public async Task SearchItems_WithOptionalCookiePrefs_ReturnsOk()
    {
        HttpResponseMessage response = await Client.SendAsync(CreateSearchRequest("/search?q=test", cookieHeader: "session=sess-123; prefs=compact"));
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """[{"id":1,"name":"test","payload":{"q":"test","session":"sess-123","prefs":"compact","xCorrelationId":"corr-001"}}]""");
    }

    [TestMethod]
    public async Task SearchItems_WithAllOptionalParameters_ReturnsOk()
    {
        HttpResponseMessage response = await Client.SendAsync(CreateSearchRequest(
            "/search?q=echo-me&tags=foo%7Cbar%7Cbaz&coords=1.5%202.5%203.5&filter%5Bstatus%5D=active&filter%5Bpriority%5D=high",
            cookieHeader: "session=sess-123; prefs=dark"));
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """[{"id":1,"name":"echo-me","payload":{"q":"echo-me","tags":["foo","bar","baz"],"coords":[1.5,2.5,3.5],"filter":{"status":"active","priority":"high"},"session":"sess-123","prefs":"dark","xCorrelationId":"corr-001"}}]""");
    }

    [TestMethod]
    public async Task SearchItemsAdvanced_WithJsonBody_ReturnsOk()
    {
        StringContent content = new("""{"query":"test","limit":5}""", Encoding.UTF8, "application/json");
        HttpRequestMessage request = new(HttpMethod.Post, "/search") { Content = content };
        request.Headers.Add("Cookie", "session=sess-abc");
        HttpResponseMessage response = await Client.SendAsync(request);
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """{"results":[{"id":1,"name":"Advanced Result"}]}""");
    }

    [TestMethod]
    public async Task SearchItemsAdvanced_MissingRequiredCookie_ReturnsBadRequest()
    {
        StringContent content = new("""{"query":"test","limit":5}""", Encoding.UTF8, "application/json");
        HttpResponseMessage response = await Client.PostAsync("/search", content);
        await AssertJsonResponseAsync(response, HttpStatusCode.BadRequest, """{"type":"about:blank","title":"Bad Request","status":400,"detail":"The required parameter 'session' is missing."}""");
    }

    [TestMethod]
    public async Task UploadFile_WithBinaryBody_ReturnsCreated()
    {
        ByteArrayContent content = new("binary-data"u8.ToArray());
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        HttpRequestMessage request = new(HttpMethod.Post, "/upload") { Content = content };
        request.Headers.Add("X-File-Name", "test.bin");
        HttpResponseMessage response = await Client.SendAsync(request);
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, """{"id":1,"name":"Widget"}""");
    }

    [TestMethod]
    public async Task UploadFile_WithoutRequiredHeader_ReturnsBadRequest()
    {
        ByteArrayContent content = new("binary-data"u8.ToArray());
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        HttpResponseMessage response = await Client.PostAsync("/upload", content);
        await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest, "The required parameter 'X-File-Name' is missing.");
    }

    [TestMethod]
    public async Task SubmitFeedback_WithFormUrlEncodedBody_ReturnsCreated()
    {
        FormUrlEncodedContent content = new(
        [
            new KeyValuePair<string, string>("comment", "Great product"),
            new KeyValuePair<string, string>("rating", "5"),
        ]);

        HttpResponseMessage response = await Client.PostAsync("/feedback?source=web", content);
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, """{"id":5,"name":"Great product","payload":{"comment":"Great product","rating":5}}""");
    }

    [TestMethod]
    public async Task UploadAttachment_WithMultipartBody_ReturnsCreated()
    {
        MultipartFormDataContent content = new();
        content.Add(new ByteArrayContent("attachment"u8.ToArray()), "file", "doc.pdf");
        content.Add(new StringContent("Document"), "description");

        HttpRequestMessage request = new(HttpMethod.Post, "/attachments") { Content = content };
        request.Headers.Add("X-Upload-Token", "token-123");
        HttpResponseMessage response = await Client.SendAsync(request);
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, """{"id":1,"name":"Widget"}""");
    }

    [TestMethod]
    public async Task SubmitFeedbackEncoded_WithFormUrlEncodedBody_ReturnsCreated()
    {
        FormUrlEncodedContent content = new(
        [
            new KeyValuePair<string, string>("comment", "hello world & goodbye"),
            new KeyValuePair<string, string>("tags", "red"),
            new KeyValuePair<string, string>("tags", "green"),
        ]);

        HttpRequestMessage request = new(HttpMethod.Post, "/feedback-encoded") { Content = content };
        request.Headers.Add("X-Session-Id", "sess-123");
        HttpResponseMessage response = await Client.SendAsync(request);
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, """{"id":2,"name":"hello world \u0026 goodbye","payload":{"comment":"hello world \u0026 goodbye","tags":["red","green"]}}""");
    }

    [TestMethod]
    public async Task UploadAttachmentEncoded_WithMultipartBody_ReturnsCreated()
    {
        MultipartFormDataContent content = new();
        content.Add(new ByteArrayContent("attachment"u8.ToArray()), "file", "data.csv");
        content.Add(new StringContent("batch"), "metadata");

        HttpRequestMessage request = new(HttpMethod.Post, "/attachments-encoded") { Content = content };
        request.Headers.Add("X-Batch-Id", "batch-789");
        HttpResponseMessage response = await Client.SendAsync(request);
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, """{"id":1,"name":"Widget"}""");
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_SimpleFields_DeserializedCorrectly()
    {
        FormUrlEncodedContent content = new(
        [
            new KeyValuePair<string, string>("comment", "Widget"),
            new KeyValuePair<string, string>("metadata", """{"color":"blue"}"""),
            new KeyValuePair<string, string>("rating", "1"),
        ]);

        HttpResponseMessage response = await Client.PostAsync("/feedback?source=web", content);
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, """{"id":1,"name":"Widget","payload":{"comment":"Widget","metadata":{"color":"blue"},"rating":1}}""");
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_NumericAndBooleanValues_DeserializedAsCorrectJsonTypes()
    {
        FormUrlEncodedContent content = new(
        [
            new KeyValuePair<string, string>("count", "7"),
            new KeyValuePair<string, string>("enabled", "true"),
            new KeyValuePair<string, string>("rating", "5"),
        ]);

        HttpResponseMessage response = await Client.PostAsync("/feedback?source=web", content);
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, """{"id":5,"name":"Feedback","payload":{"count":7,"enabled":true,"rating":5}}""");
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_PercentEncodedValues_DeserializedCorrectly()
    {
        FormUrlEncodedContent content = new(
        [
            new KeyValuePair<string, string>("comment", "hello world & goodbye"),
            new KeyValuePair<string, string>("note", "a,b,c"),
            new KeyValuePair<string, string>("rating", "1"),
        ]);

        HttpResponseMessage response = await Client.PostAsync("/feedback?source=web", content);
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, """{"id":1,"name":"hello world \u0026 goodbye","payload":{"comment":"hello world \u0026 goodbye","note":"a,b,c","rating":1}}""");
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_ExplodedArray_DeserializedAsJsonArray()
    {
        FormUrlEncodedContent content = new(
        [
            new KeyValuePair<string, string>("comment", "test"),
            new KeyValuePair<string, string>("tags", "red"),
            new KeyValuePair<string, string>("tags", "green"),
            new KeyValuePair<string, string>("tags", "blue"),
        ]);

        HttpRequestMessage request = new(HttpMethod.Post, "/feedback-encoded") { Content = content };
        request.Headers.Add("X-Session-Id", "sess-456");
        HttpResponseMessage response = await Client.SendAsync(request);
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, """{"id":3,"name":"test","payload":{"comment":"test","tags":["red","green","blue"]}}""");
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_EmptyValue_DeserializedAsNull()
    {
        HttpResponseMessage response = await Client.PostAsync("/feedback?source=app", CreateRawFormContent("note=&rating=3"));
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, """{"id":3,"name":"Feedback","payload":{"note":null,"rating":3}}""");
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_BooleanValues_DeserializedCorrectly()
    {
        HttpResponseMessage response = await Client.PostAsync("/feedback?source=test", CreateRawFormContent("flag=true&disabled=false&rating=1"));
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, """{"id":1,"name":"Feedback","payload":{"flag":true,"disabled":false,"rating":1}}""");
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_KeyWithoutEquals_DeserializedAsNull()
    {
        HttpResponseMessage response = await Client.PostAsync("/feedback?source=raw", CreateRawFormContent("note&rating=5"));
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, """{"id":5,"name":"Feedback","payload":{"note":null,"rating":5}}""");
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_ConsecutiveAmpersands_EmptyPairsSkipped()
    {
        HttpResponseMessage response = await Client.PostAsync("/feedback?source=test", CreateRawFormContent("comment=hello&&rating=3&"));
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, """{"id":3,"name":"hello","payload":{"comment":"hello","rating":3}}""");
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_NonNumericStartingWithDigit_DeserializedAsString()
    {
        HttpResponseMessage response = await Client.PostAsync("/feedback?source=test", CreateRawFormContent("code=3abc&rating=7"));
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, """{"id":7,"name":"Feedback","payload":{"code":"3abc","rating":7}}""");
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_PlusEncodedSpaces_DeserializedCorrectly()
    {
        HttpResponseMessage response = await Client.PostAsync("/feedback?source=test", CreateRawFormContent("comment=hello+world&rating=10"));
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, """{"id":10,"name":"hello world","payload":{"comment":"hello world","rating":10}}""");
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_LongValue_BufferGrowthHandled()
    {
        string longValue = new('x', 1200);
        FormUrlEncodedContent content = new(
        [
            new KeyValuePair<string, string>("note", longValue),
            new KeyValuePair<string, string>("rating", "1"),
        ]);

        HttpResponseMessage response = await Client.PostAsync("/feedback?source=test", content);
        string expectedBody = "{\"id\":1,\"name\":\"Feedback\",\"payload\":{\"note\":\"" + longValue + "\",\"rating\":1}}";
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, expectedBody);
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_LongKey_BufferGrowthHandled()
    {
        string longKey = new('k', 300);
        HttpResponseMessage response = await Client.PostAsync("/feedback?source=test", CreateRawFormContent($"{longKey}=hello&rating=2"));
        string expectedBody = "{\"id\":2,\"name\":\"Feedback\",\"payload\":{\"" + longKey + "\":\"hello\",\"rating\":2}}";
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, expectedBody);
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_LongValueWithPlus_UnescapeRentsBuffer()
    {
        string longValue = new string('a', 200) + "+" + new string('b', 200);
        string expectedValue = new string('a', 200) + " " + new string('b', 200);
        HttpResponseMessage response = await Client.PostAsync("/feedback?source=test", CreateRawFormContent($"note={longValue}&rating=1"));
        string expectedBody = "{\"id\":1,\"name\":\"Feedback\",\"payload\":{\"note\":\"" + expectedValue + "\",\"rating\":1}}";
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, expectedBody);
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_MultipleGrowths_SecondGrowReturnsFirstRented()
    {
        string key1 = new('a', 300);
        string key2 = new('b', 600);
        HttpResponseMessage response = await Client.PostAsync("/feedback?source=test", CreateRawFormContent($"{key1}=val1&{key2}=val2&rating=1"));
        string expectedBody = "{\"id\":1,\"name\":\"Feedback\",\"payload\":{\"" + key1 + "\":\"val1\",\"" + key2 + "\":\"val2\",\"rating\":1}}";
        await AssertJsonResponseAsync(response, HttpStatusCode.Created, expectedBody);
    }

    [TestMethod]
    public async Task GetQuirky_WithQueryParameter_ReturnsOk()
    {
        HttpResponseMessage response = await Client.GetAsync("/quirky/q-1?weirdLoc=odd");
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """{"id":1,"name":"Widget"}""");
    }

    [TestMethod]
    public async Task GetStyledQuirky_WithStyledQueryParameter_ReturnsOk()
    {
        HttpResponseMessage response = await Client.GetAsync("/quirky/s-1/styled?badQueryStyle=xyz");
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """{"id":1,"name":"Widget"}""");
    }

    [TestMethod]
    public async Task GetEmptyServers_WhenRequested_ReturnsOk()
    {
        HttpResponseMessage response = await Client.GetAsync("/empty-servers");
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """{"ok":true}""");
    }

    [TestMethod]
    public async Task HealthCheck_WithOptionalParameters_ReturnsOk()
    {
        HttpRequestMessage request = new(HttpMethod.Get, "/health?verbose=true");
        request.Headers.Add("X-Check-Token", "check-123");
        HttpResponseMessage response = await Client.SendAsync(request);
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """{"status":"ok"}""");
    }

    [TestMethod]
    public async Task GetAdvancedStyles_WithStyledParameters_ReturnsOk()
    {
        HttpResponseMessage response = await Client.GetAsync("/advanced-styles/.id1.id2?matrixTags=red&matrixTags=blue&limit=10&weight=3.5&score=99");
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """{"items":[],"total":0}""");
    }

    [TestMethod]
    public async Task GetAdvancedStyles_WithInvalidLimitQuery_ReturnsBadRequest()
    {
        HttpResponseMessage response = await Client.GetAsync("/advanced-styles/.id1?limit=1.5");
        await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest, "The parameter 'limit' failed schema validation.");
    }

    [TestMethod]
    public async Task GetAdvancedStyles_WithInvalidWeightQuery_ReturnsBadRequest()
    {
        HttpResponseMessage response = await Client.GetAsync("/advanced-styles/.id1?weight=1e50");
        await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest, "The parameter 'weight' failed schema validation.");
    }

    [TestMethod]
    public async Task GetAdvancedStyles_WithInvalidScoreQuery_ReturnsBadRequest()
    {
        HttpResponseMessage response = await Client.GetAsync("/advanced-styles/.id1?score=1e500");
        await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest, "The parameter 'score' failed schema validation.");
    }

    [TestMethod]
    public async Task GetByMatrixCodes_WithMatrixStyleSegment_ReturnsOk()
    {
        HttpResponseMessage response = await Client.GetAsync("/matrix-test/;codes=abc,def");
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """{"count":0}""");
    }

    [TestMethod]
    public async Task GetByMatrixTags_WithMatrixNoExplodeSegment_ReturnsOk()
    {
        HttpResponseMessage response = await Client.GetAsync("/matrix-no-explode/;tags=red,green,blue");
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """{"count":0}""");
    }

    [TestMethod]
    public async Task GetByLabelItems_WithLabelStyleSegment_ReturnsOk()
    {
        HttpResponseMessage response = await Client.GetAsync("/label-no-explode/.apple.banana.cherry");
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """{"count":0}""");
    }

    [TestMethod]
    public async Task GetByStyledObject_WithObjectSegment_ReturnsOk()
    {
        HttpResponseMessage response = await Client.GetAsync("/styled-object/;color=blue");
        await AssertJsonResponseAsync(response, HttpStatusCode.OK, """{}""");
    }

    // =====================================================================
    // Server-side validation coverage
    // =====================================================================
    [TestMethod]
    public async Task CreateItem_WithInvalidJsonBody_ReturnsBadRequest()
    {
        StringContent content = new("{}", Encoding.UTF8, "application/json");
        HttpResponseMessage response = await Client.PostAsync("/items", content);
        await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest, "The request body failed schema validation.");
    }

    [TestMethod]
    public async Task CreateItem_WithMalformedJsonBody_ReturnsBadRequest()
    {
        StringContent content = new("{\"name\":", Encoding.UTF8, "application/json");
        HttpResponseMessage response = await Client.PostAsync("/items", content);
        await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest, "The request body could not be parsed.");
    }

    [TestMethod]
    public async Task CreateItem_WithWrongPropertyTypes_ReturnsBadRequest()
    {
        StringContent content = new("""{"name":123,"metadata":"bad","tag":false}""", Encoding.UTF8, "application/json");
        HttpResponseMessage response = await Client.PostAsync("/items", content);
        await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest, "The request body failed schema validation.");
    }

    [TestMethod]
    public async Task SearchItemsAdvanced_WithMalformedJsonBody_ReturnsBadRequest()
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/search")
        {
            Content = new StringContent("[incomplete", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Cookie", "session=sess-abc");
        HttpResponseMessage response = await Client.SendAsync(request);
        await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest, "The request body could not be parsed.");
    }

    [TestMethod]
    public async Task SearchItemsAdvanced_WithWrongBodyType_ReturnsBadRequest()
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/search")
        {
            Content = new StringContent("""{"query":123}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Cookie", "session=sess-abc");
        HttpResponseMessage response = await Client.SendAsync(request);
        await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest, "The request body failed schema validation.");
    }

    [TestMethod]
    public async Task ListItems_WithInvalidPageQuery_ReturnsBadRequest()
    {
        HttpResponseMessage response = await Client.GetAsync("/items?page=1.5");
        await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest, "The parameter 'page' failed schema validation.");
    }

    [TestMethod]
    public async Task SearchItems_FilterWrongType_ReturnsBadRequest()
    {
        HttpResponseMessage response = await Client.SendAsync(CreateSearchRequest("/search?q=test&filter%5Bmin%5D=abc"));
        await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest, "The parameter 'filter' failed schema validation.");
    }

    [TestMethod]
    public async Task ListItems_WithInvalidResponseBody_ReturnsInternalServerError()
    {
        MockDefaultHandler.ReturnInvalidResponse = true;
        try
        {
            HttpResponseMessage response = await Client.GetAsync("/items");
            await AssertProblemDetailsAsync(response, HttpStatusCode.InternalServerError, "The response body failed schema validation.");
        }
        finally
        {
            MockDefaultHandler.ReturnInvalidResponse = false;
        }
    }

    [TestMethod]
    public async Task GetItem_WithInvalidResponseBody_ReturnsInternalServerError()
    {
        MockDefaultHandler.ReturnInvalidResponse = true;
        try
        {
            HttpResponseMessage response = await Client.GetAsync("/items/item-1");
            await AssertProblemDetailsAsync(response, HttpStatusCode.InternalServerError, "The response body failed schema validation.");
        }
        finally
        {
            MockDefaultHandler.ReturnInvalidResponse = false;
        }
    }

    [TestMethod]
    public async Task CreateItem_WithInvalidResponseBody_ReturnsInternalServerError()
    {
        MockDefaultHandler.ReturnInvalidResponse = true;
        try
        {
            StringContent content = new("""{"name":"Widget"}""", Encoding.UTF8, "application/json");
            HttpResponseMessage response = await Client.PostAsync("/items", content);
            await AssertProblemDetailsAsync(response, HttpStatusCode.InternalServerError, "The response body failed schema validation.");
        }
        finally
        {
            MockDefaultHandler.ReturnInvalidResponse = false;
        }
    }

    [TestMethod]
    public async Task UploadItemData_WithInvalidResponseBody_ReturnsInternalServerError()
        => await AssertInvalidResponseBodyAsync(async () =>
        {
            MultipartFormDataContent content = new();
            content.Add(new ByteArrayContent("file-data"u8.ToArray()), "file", "test.bin");
            content.Add(new StringContent("A test file"), "description");
            return await Client.PostAsync("/items/item-42/upload", content);
        });

    [TestMethod]
    public async Task GetQuirky_WithInvalidResponseBody_ReturnsInternalServerError()
        => await AssertInvalidResponseBodyAsync(() => Client.GetAsync("/quirky/q-1?weirdLoc=odd"));

    [TestMethod]
    public async Task GetStyledQuirky_WithInvalidResponseBody_ReturnsInternalServerError()
        => await AssertInvalidResponseBodyAsync(() => Client.GetAsync("/quirky/s-1/styled?badQueryStyle=xyz"));

    [TestMethod]
    public async Task GetEmptyServers_WithInvalidResponseBody_ReturnsInternalServerError()
        => await AssertInvalidResponseBodyAsync(() => Client.GetAsync("/empty-servers"));

    [TestMethod]
    public async Task GetAdvancedStyles_WithInvalidResponseBody_ReturnsInternalServerError()
        => await AssertInvalidResponseBodyAsync(() => Client.GetAsync("/advanced-styles/.id1?limit=10"));

    [TestMethod]
    public async Task GetByMatrixCodes_WithInvalidResponseBody_ReturnsInternalServerError()
        => await AssertInvalidResponseBodyAsync(() => Client.GetAsync("/matrix-test/;codes=abc,def"));

    [TestMethod]
    public async Task GetByMatrixTags_WithInvalidResponseBody_ReturnsInternalServerError()
        => await AssertInvalidResponseBodyAsync(() => Client.GetAsync("/matrix-no-explode/;tags=red,green"));

    [TestMethod]
    public async Task GetByLabelItems_WithInvalidResponseBody_ReturnsInternalServerError()
        => await AssertInvalidResponseBodyAsync(() => Client.GetAsync("/label-no-explode/.apple.banana"));

    [TestMethod]
    public async Task GetByStyledObject_WithInvalidResponseBody_ReturnsInternalServerError()
        => await AssertInvalidResponseBodyAsync(() => Client.GetAsync("/styled-object/;color=blue"));

    [TestMethod]
    public async Task SearchItems_WithInvalidResponseBody_ReturnsInternalServerError()
        => await AssertInvalidResponseBodyAsync(() => Client.SendAsync(CreateSearchRequest("/search?q=test&filter%5Bstatus%5D=active", cookieHeader: "session=sess-123; prefs=dark")));

    [TestMethod]
    public async Task SearchItemsAdvanced_WithInvalidResponseBody_ReturnsInternalServerError()
        => await AssertInvalidResponseBodyAsync(async () =>
        {
            HttpRequestMessage request = new(HttpMethod.Post, "/search")
            {
                Content = new StringContent("""{"query":"test","limit":5}""", Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("Cookie", "session=sess-abc");
            return await Client.SendAsync(request);
        });

    [TestMethod]
    public async Task UploadFile_WithInvalidResponseBody_ReturnsInternalServerError()
        => await AssertInvalidResponseBodyAsync(async () =>
        {
            ByteArrayContent content = new("binary-data"u8.ToArray());
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            HttpRequestMessage request = new(HttpMethod.Post, "/upload") { Content = content };
            request.Headers.Add("X-File-Name", "test.bin");
            return await Client.SendAsync(request);
        });

    [TestMethod]
    public async Task UploadAttachment_WithInvalidResponseBody_ReturnsInternalServerError()
        => await AssertInvalidResponseBodyAsync(async () =>
        {
            MultipartFormDataContent content = new();
            content.Add(new ByteArrayContent("attachment"u8.ToArray()), "file", "doc.pdf");
            content.Add(new StringContent("Document"), "description");
            return await Client.PostAsync("/attachments", content);
        });

    [TestMethod]
    public async Task UploadAttachmentEncoded_WithInvalidResponseBody_ReturnsInternalServerError()
        => await AssertInvalidResponseBodyAsync(async () =>
        {
            MultipartFormDataContent content = new();
            content.Add(new ByteArrayContent("attachment"u8.ToArray()), "file", "data.csv");
            content.Add(new StringContent("batch"), "metadata");
            return await Client.PostAsync("/attachments-encoded", content);
        });

    private static async Task AssertInvalidResponseBodyAsync(Func<Task<HttpResponseMessage>> sendRequest)
    {
        MockDefaultHandler.ReturnInvalidResponse = true;
        try
        {
            HttpResponseMessage response = await sendRequest();
            await AssertProblemDetailsAsync(response, HttpStatusCode.InternalServerError, "The response body failed schema validation.");
        }
        finally
        {
            MockDefaultHandler.ReturnInvalidResponse = false;
        }
    }

    private static HttpRequestMessage CreateSearchRequest(string requestUri, string? cookieHeader = "session=sess-123", string? correlationId = "corr-001")
    {
        HttpRequestMessage request = new(HttpMethod.Get, requestUri);
        if (cookieHeader is not null)
        {
            request.Headers.Add("Cookie", cookieHeader);
        }

        if (correlationId is not null)
        {
            request.Headers.Add("X-Correlation-Id", correlationId);
        }

        return request;
    }

    private static ByteArrayContent CreateRawFormContent(string rawBody)
    {
        ByteArrayContent content = new(Encoding.UTF8.GetBytes(rawBody));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");
        return content;
    }

    private static async Task AssertJsonResponseAsync(HttpResponseMessage response, HttpStatusCode expectedStatusCode, string expectedBody)
    {
        string body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(expectedStatusCode, response.StatusCode);
        Assert.AreEqual(expectedBody, body);
    }

    private static async Task AssertProblemDetailsAsync(HttpResponseMessage response, HttpStatusCode expectedStatusCode, string expectedDetail)
    {
        string title = expectedStatusCode == HttpStatusCode.BadRequest ? "Bad Request" : "Internal Server Error";
        await AssertJsonResponseAsync(
            response,
            expectedStatusCode,
            $$"""{"type":"about:blank","title":"{{title}}","status":{{(int)expectedStatusCode}},"detail":"{{expectedDetail}}"}""");
    }
}