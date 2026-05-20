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
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
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
        HttpResponseMessage response = await client!.GetAsync("/search?q=test");
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
}