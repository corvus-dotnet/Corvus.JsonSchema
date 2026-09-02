// <copyright file="RequestBodyLimitTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using CanonTests31.Server;
using Corvus.Text.Json.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Corvus.Text.Json.OpenApi31.Server.Runtime.Tests;

/// <summary>
/// End-to-end tests for the registration-time buffered request body size limit:
/// bodies over <see cref="ApiServerOptions.MaxBufferedRequestBodyLength"/> are rejected
/// with 413 on the buffered body paths (multipart and form-urlencoded), and bodies
/// within the limit are processed normally.
/// </summary>
[TestClass]
public class RequestBodyLimitTests
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
                        new MockSearchHandler(),
                        configureEndpoint: null,
                        serverOptions: new ApiServerOptions { MaxBufferedRequestBodyLength = 256 });
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
    public async Task Multipart_BodyOverLimit_Returns413()
    {
        MultipartFormDataContent content = new();
        content.Add(new ByteArrayContent(new byte[1024]), "file", "big.bin");
        content.Add(new StringContent("too big"), "description");

        HttpResponseMessage response = await Client.PostAsync("/items/item-42/upload", content);

        Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [TestMethod]
    public async Task Multipart_BodyWithinLimit_ReturnsCreated()
    {
        MultipartFormDataContent content = new();
        content.Add(new StringContent("A test file"), "description");

        HttpResponseMessage response = await Client.PostAsync("/items/item-42/upload", content);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    [TestMethod]
    public async Task FormUrlEncoded_BodyOverLimit_Returns413()
    {
        string bigValue = new('v', 1024);
        StringContent content = new($"name={bigValue}", Encoding.UTF8, "application/x-www-form-urlencoded");

        HttpResponseMessage response = await Client.PostAsync("/feedback", content);

        Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [TestMethod]
    public async Task Multipart_BodyOverLimit_WithoutContentLength_Returns413()
    {
        // Chunked transfer defeats the Content-Length fast reject, so the cap must
        // also trip while the body is being buffered.
        MultipartFormDataContent inner = new();
        inner.Add(new ByteArrayContent(new byte[1024]), "file", "big.bin");
        ChunkedContent content = new(inner);

        HttpResponseMessage response = await Client.PostAsync("/items/item-42/upload", content);

        Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    /// <summary>
    /// Wraps another <see cref="HttpContent"/>, copying its headers but suppressing
    /// Content-Length so the request is sent with chunked transfer encoding.
    /// </summary>
    private sealed class ChunkedContent : HttpContent
    {
        private readonly HttpContent inner;

        public ChunkedContent(HttpContent inner)
        {
            this.inner = inner;
            foreach (KeyValuePair<string, IEnumerable<string>> header in inner.Headers)
            {
                if (!string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    this.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
            => this.inner.CopyToAsync(stream);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}