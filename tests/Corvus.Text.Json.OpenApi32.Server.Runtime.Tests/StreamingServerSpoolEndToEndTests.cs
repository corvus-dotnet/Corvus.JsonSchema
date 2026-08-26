// <copyright file="StreamingServerSpoolEndToEndTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using CanonTests32.StreamingServer;
using Corvus.Text.Json.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Corvus.Text.Json.OpenApi32.Server.Runtime.Tests;

/// <summary>
/// End-to-end tests for the SpoolOutOfOrder ordering policy: the same generated
/// streaming endpoints accept browser-style bodies (binary parts before text parts)
/// when registered with spool options, and spool files never outlive the request.
/// </summary>
[TestClass]
public class StreamingServerSpoolEndToEndTests
{
    private static string? spoolDir;
    private static IHost? host;
    private static HttpClient? client;

    [ClassInitialize]
    public static async Task ClassInit(TestContext context)
    {
        spoolDir = Path.Combine(Path.GetTempPath(), "corvus-spool-e2e-" + Path.GetRandomFileName());
        Directory.CreateDirectory(spoolDir);

        HostBuilder builder = new();
        builder.ConfigureWebHost(webHost =>
        {
            webHost.UseTestServer();
            webHost.ConfigureServices(services => services.AddRouting());
            webHost.Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapApiEndpoints(
                        new StreamingMockHandler(),
                        new ApiServerOptions
                        {
                            MultipartBinaryOrdering = MultipartBinaryOrdering.SpoolOutOfOrder,
                            SpoolDirectory = spoolDir,
                            SpoolMemoryThresholdBytes = 64 * 1024,
                        });
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
        if (spoolDir is not null)
        {
            Directory.Delete(spoolDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task Upload_BrowserOrder_BinaryBeforeText_Succeeds()
    {
        byte[] fileBytes = new byte[10_000];
        Random.Shared.NextBytes(fileBytes);

        // Browser form order: the file input precedes the caption field.
        using MultipartFormDataContent content = [];
        content.Add(new ByteArrayContent(fileBytes), "file", "f.bin");
        content.Add(new StringContent("spooled"), "caption");

        HttpResponseMessage response = await client!.PostAsync("/upload", content);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.AreEqual(
            """{"caption":"spooled","fileLength":10000,"thumbLength":0}""",
            await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task Upload_LargeFileOverThreshold_SpoolFileCleanedUp()
    {
        byte[] fileBytes = new byte[5 * 1024 * 1024];
        Random.Shared.NextBytes(fileBytes);

        using MultipartFormDataContent content = [];
        content.Add(new ByteArrayContent(fileBytes), "file", "big.bin");
        content.Add(new ByteArrayContent(new byte[1024]), "thumb", "t.bin");
        content.Add(new StringContent("big and early"), "caption");

        HttpResponseMessage response = await client!.PostAsync("/upload", content);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.AreEqual(
            """{"caption":"big and early","fileLength":5242880,"thumbLength":1024}""",
            await response.Content.ReadAsStringAsync());
        Assert.AreEqual(0, Directory.GetFiles(spoolDir!).Length, "spool files must not outlive the request");
    }

    [TestMethod]
    public async Task Upload_MissingRequiredFile_Returns400()
    {
        using MultipartFormDataContent content = [];
        content.Add(new StringContent("no file"), "caption");

        HttpResponseMessage response = await client!.PostAsync("/upload", content);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "required binary part");
    }
}