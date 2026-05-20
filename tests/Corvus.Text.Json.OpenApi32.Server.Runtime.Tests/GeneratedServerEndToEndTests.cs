// <copyright file="GeneratedServerEndToEndTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using CanonTests32.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Corvus.Text.Json.OpenApi32.Server.Runtime.Tests;

/// <summary>
/// End-to-end tests that exercise the generated server stubs through an in-memory
/// ASP.NET test host.
/// </summary>
[TestClass]
public class GeneratedServerEndToEndTests
{
    private static IHost? host;
    private static HttpClient? client;

    [ClassInitialize]
    public static async Task ClassInit(TestContext context)
    {
        HostBuilder builder = new();
        builder.ConfigureWebHost(webHost =>
        {
            webHost.UseTestServer();
            webHost.ConfigureServices(services =>
            {
                services.AddRouting();
            });
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
    public async Task GetItems_ReturnsOk_WithJsonBody()
    {
        HttpResponseMessage response = await client!.GetAsync("/items");
        string body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsTrue(body.Contains("\"id\""));
    }

    [TestMethod]
    public async Task GetItems_QueryParams_ArePassedToHandler()
    {
        HttpResponseMessage response = await client!.GetAsync("/items?active=true&page=5");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task PostItems_WithBody_ReturnsCreated()
    {
        StringContent content = new("""{"name":"Widget","price":9.99}""", Encoding.UTF8, "application/json");
        HttpResponseMessage response = await client!.PostAsync("/items", content);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    [TestMethod]
    public async Task GetItem_PathParam_IsExtracted()
    {
        HttpResponseMessage response = await client!.GetAsync("/items/item-123");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(body.Contains("item-123"));
    }

    [TestMethod]
    public async Task GetItem_HeaderParam_IsExtracted()
    {
        HttpRequestMessage request = new(HttpMethod.Get, "/items/item-1");
        request.Headers.Add("X-Request-Id", "req-abc");
        HttpResponseMessage response = await client!.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task DownloadFile_ReturnsOk()
    {
        HttpResponseMessage response = await client!.GetAsync("/download");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task SearchItems_ReturnsOk()
    {
        HttpRequestMessage request = new(HttpMethod.Get, "/search?q=test");
        request.Headers.Add("X-Correlation-Id", "corr-001");
        request.Headers.Add("Cookie", "session=abc123");
        HttpResponseMessage response = await client!.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_SimpleFields_DeserializedCorrectly()
    {
        FormUrlEncodedContent content = new(
        [
            new KeyValuePair<string, string>("name", "Widget"),
            new KeyValuePair<string, string>("tags", "electronics"),
            new KeyValuePair<string, string>("metadata", """{"color":"blue"}"""),
        ]);

        HttpResponseMessage response = await client!.PostAsync("/items/item-42/form", content);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("""{"name":"Widget","tags":"electronics","metadata":{"color":"blue"}}""", body);
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_NumericAndBooleanValues_DeserializedAsCorrectJsonTypes()
    {
        FormUrlEncodedContent content = new(
        [
            new KeyValuePair<string, string>("comment", "Great product"),
            new KeyValuePair<string, string>("rating", "5"),
        ]);

        HttpResponseMessage response = await client!.PostAsync("/feedback?source=web", content);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("""{"comment":"Great product","rating":5}""", body);
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_PercentEncodedValues_DeserializedCorrectly()
    {
        // Use special characters that get percent-encoded.
        FormUrlEncodedContent content = new(
        [
            new KeyValuePair<string, string>("comment", "hello world & goodbye"),
            new KeyValuePair<string, string>("tags", "a,b,c"),
        ]);

        HttpRequestMessage request = new(HttpMethod.Post, "/feedback-encoded");
        request.Content = content;
        request.Headers.Add("X-Session-Id", "sess-123");
        HttpResponseMessage response = await client!.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();

        // "hello world & goodbye" is string, "a,b,c" is string (not numeric/bool/JSON)
        // Utf8JsonWriter escapes '&' as '\u0026' in JSON output.
        Assert.AreEqual("""{"comment":"hello world \u0026 goodbye","tags":"a,b,c"}""", body);
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_ExplodedArray_DeserializedAsJsonArray()
    {
        // When the same key appears multiple times, it should be deserialized as an array.
        FormUrlEncodedContent content = new(
        [
            new KeyValuePair<string, string>("comment", "test"),
            new KeyValuePair<string, string>("tags", "red"),
            new KeyValuePair<string, string>("tags", "green"),
            new KeyValuePair<string, string>("tags", "blue"),
        ]);

        HttpRequestMessage request = new(HttpMethod.Post, "/feedback-encoded");
        request.Content = content;
        request.Headers.Add("X-Session-Id", "sess-456");
        HttpResponseMessage response = await client!.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("""{"comment":"test","tags":["red","green","blue"]}""", body);
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_EmptyValue_DeserializedAsNull()
    {
        // When a form field has an empty value, it should deserialize as JSON null.
        FormUrlEncodedContent content = new(
        [
            new KeyValuePair<string, string>("comment", ""),
            new KeyValuePair<string, string>("rating", "3"),
        ]);

        HttpResponseMessage response = await client!.PostAsync("/feedback?source=app", content);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("""{"comment":null,"rating":3}""", body);
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_BooleanValues_DeserializedCorrectly()
    {
        FormUrlEncodedContent content = new(
        [
            new KeyValuePair<string, string>("name", "true"),
            new KeyValuePair<string, string>("tags", "false"),
            new KeyValuePair<string, string>("metadata", """{"active":true}"""),
        ]);

        HttpResponseMessage response = await client!.PostAsync("/items/item-99/form", content);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("""{"name":true,"tags":false,"metadata":{"active":true}}""", body);
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_KeyWithoutEquals_DeserializedAsNull()
    {
        // A key without '=' should be treated as key with null value.
        // We send raw bytes to bypass FormUrlEncodedContent's encoding.
        byte[] rawBody = "comment&rating=5"u8.ToArray();
        ByteArrayContent content = new(rawBody);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

        HttpResponseMessage response = await client!.PostAsync("/feedback?source=raw", content);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("""{"comment":null,"rating":5}""", body);
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_ConsecutiveAmpersands_EmptyPairsSkipped()
    {
        // Consecutive '&' characters produce empty pairs that should be skipped.
        byte[] rawBody = "comment=hello&&rating=3&"u8.ToArray();
        ByteArrayContent content = new(rawBody);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

        HttpResponseMessage response = await client!.PostAsync("/feedback?source=test", content);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("""{"comment":"hello","rating":3}""", body);
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_NonNumericStartingWithDigit_DeserializedAsString()
    {
        // A value that starts with a digit but contains non-numeric chars (e.g. "3abc")
        // should be deserialized as a string, not raw JSON.
        byte[] rawBody = "comment=3abc&rating=7"u8.ToArray();
        ByteArrayContent content = new(rawBody);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

        HttpResponseMessage response = await client!.PostAsync("/feedback?source=test", content);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("""{"comment":"3abc","rating":7}""", body);
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_PlusEncodedSpaces_DeserializedCorrectly()
    {
        // Form encoding uses '+' for spaces. The decoder should handle this.
        byte[] rawBody = "comment=hello+world&rating=10"u8.ToArray();
        ByteArrayContent content = new(rawBody);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

        HttpResponseMessage response = await client!.PostAsync("/feedback?source=test", content);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("""{"comment":"hello world","rating":10}""", body);
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_LongValue_BufferGrowthHandled()
    {
        // Send a value longer than the initial 1024-byte buffer to exercise growth paths.
        string longComment = new('x', 1200);
        FormUrlEncodedContent content = new(
        [
            new KeyValuePair<string, string>("comment", longComment),
            new KeyValuePair<string, string>("rating", "1"),
        ]);

        HttpResponseMessage response = await client!.PostAsync("/feedback?source=test", content);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();

        // Verify the long value was preserved.
        Assert.IsTrue(body.Contains(longComment));
        Assert.IsTrue(body.Contains("\"rating\":1"));
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_LongKey_BufferGrowthHandled()
    {
        // Send a key longer than the initial 256-byte key buffer to exercise growth.
        string longKey = new('k', 300);
        byte[] rawBody = System.Text.Encoding.UTF8.GetBytes($"{longKey}=hello&rating=2");
        ByteArrayContent content = new(rawBody);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

        HttpResponseMessage response = await client!.PostAsync("/feedback?source=test", content);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();

        // The response should contain the long key as a property name.
        Assert.IsTrue(body.Contains(longKey));
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_LongValueWithPlus_UnescapeRentsBuffer()
    {
        // A value > 256 bytes with '+' characters exercises the Unescape rented buffer path.
        string longValue = new string('a', 200) + "+" + new string('b', 200);
        byte[] rawBody = System.Text.Encoding.UTF8.GetBytes($"comment={longValue}&rating=1");
        ByteArrayContent content = new(rawBody);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

        HttpResponseMessage response = await client!.PostAsync("/feedback?source=test", content);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();

        // The '+' should be decoded to a space.
        string expectedComment = new string('a', 200) + " " + new string('b', 200);
        Assert.IsTrue(body.Contains(expectedComment));
    }

    [TestMethod]
    public async Task PostFormUrlEncoded_MultipleGrowths_SecondGrowReturnsFirstRented()
    {
        // To exercise the EnsureBuffer path where a previously rented buffer must be returned
        // (line 483-486), we need keys that grow twice: first key > 256 bytes (rents), then
        // a second key even longer (must return the first rental).
        string key1 = new('a', 300);
        string key2 = new('b', 600);
        byte[] rawBody = System.Text.Encoding.UTF8.GetBytes($"{key1}=val1&{key2}=val2");
        ByteArrayContent content = new(rawBody);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

        HttpResponseMessage response = await client!.PostAsync("/feedback?source=test", content);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();

        // Both keys should appear as properties.
        Assert.IsTrue(body.Contains(key1));
        Assert.IsTrue(body.Contains(key2));
    }

    // =====================================================================
    // HTTP Verb coverage
    // =====================================================================
    [TestMethod]
    public async Task Options_Items_ReturnsNoContent()
    {
        HttpRequestMessage request = new(HttpMethod.Options, "/items");
        HttpResponseMessage response = await client!.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    [TestMethod]
    public async Task Head_Health_ReturnsOk()
    {
        HttpRequestMessage request = new(HttpMethod.Head, "/health");
        HttpResponseMessage response = await client!.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task Trace_Health_ReturnsOk()
    {
        HttpRequestMessage request = new(HttpMethod.Trace, "/health");
        HttpResponseMessage response = await client!.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task Patch_Item_ReturnsOk()
    {
        StringContent content = new("""{"name":"Updated"}""", Encoding.UTF8, "application/merge-patch+json");
        HttpRequestMessage request = new(HttpMethod.Patch, "/items/item-42") { Content = content };
        HttpResponseMessage response = await client!.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task Delete_MonitoringStatus_ReturnsNoContent()
    {
        HttpRequestMessage request = new(HttpMethod.Delete, "/monitoring/status");
        HttpResponseMessage response = await client!.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    // =====================================================================
    // Custom HTTP verbs (additionalOperations)
    // =====================================================================
    [TestMethod]
    public async Task Purge_Items_ReturnsNoContent()
    {
        HttpRequestMessage request = new(new HttpMethod("PURGE"), "/items");
        HttpResponseMessage response = await client!.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    [TestMethod]
    public async Task Copy_Resource_ReturnsCreated()
    {
        HttpRequestMessage request = new(new HttpMethod("COPY"), "/resources/res-1");
        request.Headers.Add("Destination", "https://example.com/resources/res-2");
        request.Content = new StringContent("""{"overwrite":true}""", Encoding.UTF8, "application/json");
        HttpResponseMessage response = await client!.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    [TestMethod]
    public async Task Purge_Resource_ReturnsNoContent()
    {
        HttpRequestMessage request = new(new HttpMethod("PURGE"), "/resources/res-1");
        HttpResponseMessage response = await client!.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    [TestMethod]
    public async Task Move_Resource_ReturnsOk()
    {
        HttpRequestMessage request = new(new HttpMethod("MOVE"), "/resources/res-1?destination=https://example.com/new");
        HttpResponseMessage response = await client!.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    [TestCategory("failing")] // Server doesn't yet support multipart/mixed body parsing
    public async Task Batch_Resource_ReturnsOk()
    {
        // multipart/mixed body for BATCH verb
        MultipartContent multipart = new("mixed");
        multipart.Add(new StringContent("""{"action":"update"}""", Encoding.UTF8, "application/json"));
        HttpRequestMessage request = new(new HttpMethod("BATCH"), "/resources/res-1") { Content = multipart };
        HttpResponseMessage response = await client!.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // =====================================================================
    // Binary body operations
    // =====================================================================
    [TestMethod]
    public async Task UploadFile_BinaryBody_ReturnsCreated()
    {
        byte[] data = new byte[256];
        Random.Shared.NextBytes(data);
        ByteArrayContent content = new(data);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        HttpRequestMessage request = new(HttpMethod.Post, "/upload") { Content = content };
        request.Headers.Add("X-File-Name", "test.bin");
        HttpResponseMessage response = await client!.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    [TestMethod]
    public async Task UploadRawFile_BinaryBody_ReturnsCreated()
    {
        byte[] data = "raw file content"u8.ToArray();
        ByteArrayContent content = new(data);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        HttpResponseMessage response = await client!.PostAsync("/upload-raw", content);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("""{"url":"https://example.com/file.bin"}""", body);
    }

    [TestMethod]
    public async Task ExportData_ReturnsOk()
    {
        HttpResponseMessage response = await client!.GetAsync("/export");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task StreamEvents_ReturnsOk()
    {
        HttpResponseMessage response = await client!.GetAsync("/events/stream");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // =====================================================================
    // Multipart form-data operations
    // =====================================================================
    [TestMethod]
    public async Task UploadItemData_Multipart_ReturnsCreated()
    {
        MultipartFormDataContent content = new();
        content.Add(new StringContent("test.jpg"), "file", "test.jpg");
        content.Add(new StringContent("A test file"), "description");

        HttpResponseMessage response = await client!.PostAsync("/items/item-5/upload", content);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    [TestMethod]
    public async Task UploadAttachment_Multipart_ReturnsCreated()
    {
        MultipartFormDataContent content = new();
        content.Add(new StringContent("doc.pdf"), "file", "doc.pdf");

        HttpRequestMessage request = new(HttpMethod.Post, "/attachments") { Content = content };
        request.Headers.Add("X-Upload-Token", "token-123");
        HttpResponseMessage response = await client!.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    [TestMethod]
    public async Task UploadAttachmentEncoded_Multipart_ReturnsCreated()
    {
        MultipartFormDataContent content = new();
        content.Add(new StringContent("data.csv"), "file", "data.csv");

        HttpRequestMessage request = new(HttpMethod.Post, "/attachments-encoded") { Content = content };
        request.Headers.Add("X-Batch-Id", "batch-789");
        HttpResponseMessage response = await client!.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    // =====================================================================
    // Parameter styles (matrix, label, non-explode query, cookie, querystring)
    // =====================================================================
    [TestMethod]
    public async Task GetAdvancedStyles_NonExplodeQueryParams_ReturnsOk()
    {
        // Tests non-explode form-style query parameters (e.g., limit, weight, score)
        HttpResponseMessage response = await client!.GetAsync("/advanced-styles/id1,id2?matrixTags=a,b&limit=10&weight=3.5&score=99");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetByMatrixCodes_MatrixStyle_ReturnsOk()
    {
        // Matrix-style path parameter: ;codes=val1,val2
        HttpResponseMessage response = await client!.GetAsync("/matrix-test/;codes=abc,def");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetByMatrixTags_MatrixNoExplode_ReturnsOk()
    {
        // Matrix style, no explode
        HttpResponseMessage response = await client!.GetAsync("/matrix-no-explode/;tags=red,green,blue");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetByLabelItems_LabelStyle_ReturnsOk()
    {
        // Label-style path parameter: .val1.val2
        HttpResponseMessage response = await client!.GetAsync("/label-no-explode/.apple.banana.cherry");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetByStyledObject_ObjectPathParam_ReturnsOk()
    {
        // Simple-style object path parameter: key,value,key,value
        HttpResponseMessage response = await client!.GetAsync("/styled-object/name,Widget,color,blue");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetQuirky_CustomParamLocation_ReturnsOk()
    {
        HttpResponseMessage response = await client!.GetAsync("/quirky/q-1");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetStyledQuirky_BadQueryStyle_ReturnsOk()
    {
        HttpResponseMessage response = await client!.GetAsync("/quirky/s-1/styled?badQueryStyle=xyz");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task SearchWithQuerystring_QuerystringParam_ReturnsOk()
    {
        // querystring-type parameter (raw query string)
        HttpResponseMessage response = await client!.GetAsync("/search-qs?q=hello&limit=10");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("""{"results":[]}""", body);
    }

    [TestMethod]
    public async Task SearchItemsAdvanced_PostWithCookie_ReturnsOk()
    {
        StringContent content = new("""{"query":"test","filters":{}}""", Encoding.UTF8, "application/json");
        HttpRequestMessage request = new(HttpMethod.Post, "/search") { Content = content };
        request.Headers.Add("Cookie", "session=sess-abc");
        HttpResponseMessage response = await client!.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(body.Contains("Advanced Result"));
    }

    // =====================================================================
    // Simple CRUD operations
    // =====================================================================
    [TestMethod]
    public async Task GetResource_PathParam_ReturnsOk()
    {
        HttpResponseMessage response = await client!.GetAsync("/resources/res-42");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetDocument_ReturnsOk()
    {
        HttpResponseMessage response = await client!.GetAsync("/documents/550e8400-e29b-41d4-a716-446655440000");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task PutDocument_ReturnsOk()
    {
        StringContent content = new("""{"title":"Updated Doc"}""", Encoding.UTF8, "application/json");
        HttpResponseMessage response = await client!.PutAsync("/documents/550e8400-e29b-41d4-a716-446655440000", content);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetMonitoringStatus_ReturnsOk()
    {
        HttpResponseMessage response = await client!.GetAsync("/monitoring/status");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("""{"status":"ok"}""", body);
    }

    [TestMethod]
    public async Task PutMonitoringStatus_ReturnsOk()
    {
        StringContent content = new("""{"status":"maintenance"}""", Encoding.UTF8, "application/json");
        HttpResponseMessage response = await client!.PutAsync("/monitoring/status", content);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("""{"status":"updated"}""", body);
    }

    [TestMethod]
    public async Task PostMonitoringStatus_ReturnsCreated()
    {
        StringContent content = new("""{"status":"new"}""", Encoding.UTF8, "application/json");
        HttpResponseMessage response = await client!.PostAsync("/monitoring/status", content);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("""{"status":"created"}""", body);
    }

    [TestMethod]
    public async Task GetEmptyServers_ReturnsOk()
    {
        HttpResponseMessage response = await client!.GetAsync("/empty-servers");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual("{\"ok\":true}", body);
    }
}