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
}