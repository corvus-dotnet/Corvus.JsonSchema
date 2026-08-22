// <copyright file="GeneratedServerEndToEndTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using CanonTests20.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Corvus.Text.Json.OpenApi20.Server.Runtime.Tests;

/// <summary>
/// End-to-end tests for server stubs generated from an OpenAPI 2.0 (Swagger)
/// specification, exercised through a TestServer.
/// </summary>
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
                        new MockWidgetsHandler(),
                        new MockUploadsHandler(),
                        new MockDefaultHandler());
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
    public async Task ListWidgets_TsvQueryParameterSplitsOnTab()
    {
        MockWidgetsHandler.CapturedTsvTagsCount = 0;

        HttpResponseMessage response = await Client.GetAsync("/widgets?tsvTags=alpha%09beta%09gamma");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(3, MockWidgetsHandler.CapturedTsvTagsCount);
    }

    [TestMethod]
    public async Task PostLegacy_UrlEncodedFormBodyWithPipesParses()
    {
        MockDefaultHandler.CapturedFlagsCount = 0;

        StringContent content = new("flags=a%7Cb%7Cc", Encoding.UTF8, "application/x-www-form-urlencoded");
        HttpResponseMessage response = await Client.PostAsync("/legacy", content);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(3, MockDefaultHandler.CapturedFlagsCount);
    }

    [TestMethod]
    public async Task UploadBundle_MultipartWithBinaryAndFieldsParses()
    {
        MockUploadsHandler.CapturedNotes = null;
        MockUploadsHandler.CapturedArchive = null;

        using MultipartFormDataContent content = [];
        content.Add(new ByteArrayContent([0x01, 0x02, 0x03]), "archive", "bundle.bin");
        content.Add(new StringContent("hello world"), "notes");

        HttpResponseMessage response = await Client.PostAsync("/uploads", content);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.AreEqual("hello world", MockUploadsHandler.CapturedNotes, $"parsed body: {MockUploadsHandler.CapturedBodyJson}");
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03 }, MockUploadsHandler.CapturedArchive, "the file part's bytes must reach the handler");
    }

    [TestMethod]
    public async Task ListWidgets_TenantPatternViolationIsRejected()
    {
        // The path-level tenant query parameter declares pattern ^[a-z]+$.
        HttpResponseMessage response = await Client.GetAsync("/widgets?tenant=UPPER");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task CreateWidget_JsonBodyRoundTrips()
    {
        StringContent content = new("""{"id":"w9","size":5}""", Encoding.UTF8, "application/json");
        HttpResponseMessage response = await Client.PostAsync("/widgets", content);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(body, "\"id\":\"w9\"");
    }
}