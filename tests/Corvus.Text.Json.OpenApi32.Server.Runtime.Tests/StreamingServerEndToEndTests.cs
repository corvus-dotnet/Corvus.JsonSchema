// <copyright file="StreamingServerEndToEndTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using CanonTests32.StreamingServer;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Corvus.Text.Json.OpenApi32.Server.Runtime.Tests;

/// <summary>
/// End-to-end tests for server stubs generated with <c>--serverBinaryParts stream</c>:
/// binary parts reach the handler as wire-order streaming handles without buffering.
/// </summary>
[TestClass]
public class StreamingServerEndToEndTests
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
            webHost.ConfigureServices(services => services.AddRouting());
            webHost.Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapApiEndpoints(new StreamingMockHandler());
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
    public async Task Upload_BinaryLast_PartsStreamToHandlerInWireOrder()
    {
        byte[] fileBytes = new byte[50_000];
        Random.Shared.NextBytes(fileBytes);

        using MultipartFormDataContent content = [];
        content.Add(new StringContent("hello streaming"), "caption");
        content.Add(new ByteArrayContent(fileBytes), "file", "f.bin");
        content.Add(new ByteArrayContent(new byte[1024]), "thumb", "t.bin");

        HttpResponseMessage response = await client!.PostAsync("/upload", content);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.AreEqual(
            """{"caption":"hello streaming","fileLength":50000,"thumbLength":1024}""",
            await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task Upload_LargeFile_StreamsWithoutBuffering()
    {
        byte[] fileBytes = new byte[5 * 1024 * 1024];
        Random.Shared.NextBytes(fileBytes);

        using MultipartFormDataContent content = [];
        content.Add(new StringContent("big"), "caption");
        content.Add(new ByteArrayContent(fileBytes), "file", "big.bin");

        HttpResponseMessage response = await client!.PostAsync("/upload", content);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.AreEqual(
            """{"caption":"big","fileLength":5242880,"thumbLength":0}""",
            await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task Upload_MissingOptionalThumb_Succeeds()
    {
        using MultipartFormDataContent content = [];
        content.Add(new StringContent("no thumb"), "caption");
        content.Add(new ByteArrayContent([1, 2, 3]), "file", "f.bin");

        HttpResponseMessage response = await client!.PostAsync("/upload", content);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.AreEqual(
            """{"caption":"no thumb","fileLength":3,"thumbLength":0}""",
            await response.Content.ReadAsStringAsync());
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

    [TestMethod]
    public async Task MixedDoc_TailShapedBody_StreamsBinaryPart()
    {
        byte[] payload = new byte[20_000];
        Random.Shared.NextBytes(payload);

        using MultipartContent content = new("mixed");
        content.Add(System.Net.Http.Json.JsonContent.Create(new { title = "doc" }));
        ByteArrayContent binary = new(payload);
        binary.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(binary);

        HttpResponseMessage response = await client!.PostAsync("/mixed-doc", content);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.AreEqual(
            """{"title":"doc","payloadLength":20000}""",
            await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task MixedDoc_AbsentBinaryPart_BindsAsEmpty()
    {
        using MultipartContent content = new("mixed");
        content.Add(System.Net.Http.Json.JsonContent.Create(new { title = "solo" }));

        HttpResponseMessage response = await client!.PostAsync("/mixed-doc", content);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.AreEqual(
            """{"title":"solo","payloadLength":0}""",
            await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task MixedBatch_SequenceConsumesEveryItem()
    {
        using MultipartContent content = new("mixed");
        foreach (int size in (int[])[10, 20_000, 5])
        {
            ByteArrayContent item = new(new byte[size]);
            item.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            content.Add(item);
        }

        HttpResponseMessage response = await client!.PostAsync("/mixed-batch", content);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.AreEqual(
            """{"itemCount":3,"totalLength":20015}""",
            await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task Upload_TextAfterBinary_Returns400()
    {
        using MultipartFormDataContent content = [];
        content.Add(new StringContent("early"), "caption");
        content.Add(new ByteArrayContent([1, 2, 3]), "file", "f.bin");
        content.Add(new StringContent("too late"), "straggler");

        HttpResponseMessage response = await client!.PostAsync("/upload", content);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "Binary parts must come after");
    }
}

/// <summary>
/// Mock handler for the streaming spec: counts the streamed bytes of each part
/// without materializing them and echoes the counts.
/// </summary>
internal sealed class StreamingMockHandler : IApiDefaultHandler
{
    public async ValueTask<UploadDocumentResult> HandleUploadDocumentAsync(UploadDocumentParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        long fileLength = await CountAsync(parameters.File, cancellationToken);
        long thumbLength = await CountAsync(parameters.Thumb, cancellationToken);

        string caption = parameters.Body.TryGetProperty("caption"u8, out JsonElement captionEl)
            ? captionEl.GetString() ?? string.Empty
            : string.Empty;

        CanonTests32.StreamingServer.Models.PostUploadCreated body = CanonTests32.StreamingServer.Models.PostUploadCreated.ParseValue(Encoding.UTF8.GetBytes(
            $$"""{"caption":"{{caption}}","fileLength":{{fileLength}},"thumbLength":{{thumbLength}}}"""));
        return UploadDocumentResult.Created(body, workspace);
    }

    public async ValueTask<UploadMixedDocResult> HandleUploadMixedDocAsync(UploadMixedDocParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        long payloadLength = await CountAsync(parameters.Part1, cancellationToken);

        using System.Text.Json.JsonDocument meta = System.Text.Json.JsonDocument.Parse(parameters.Body.ToString());
        string title = meta.RootElement[0].GetProperty("title").GetString() ?? string.Empty;

        CanonTests32.StreamingServer.Models.PostMixedDocCreated body = CanonTests32.StreamingServer.Models.PostMixedDocCreated.ParseValue(Encoding.UTF8.GetBytes(
            $$"""{"title":"{{title}}","payloadLength":{{payloadLength}}}"""));
        return UploadMixedDocResult.Created(body, workspace);
    }

    public async ValueTask<UploadMixedBatchResult> HandleUploadMixedBatchAsync(UploadMixedBatchParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        int itemCount = 0;
        long totalLength = 0;
        while (await parameters.Items.MoveNextAsync(cancellationToken) is { } stream)
        {
            itemCount++;
            totalLength += await CountStreamAsync(stream, cancellationToken);
        }

        CanonTests32.StreamingServer.Models.PostMixedBatchCreated body = CanonTests32.StreamingServer.Models.PostMixedBatchCreated.ParseValue(Encoding.UTF8.GetBytes(
            $$"""{"itemCount":{{itemCount}},"totalLength":{{totalLength}}}"""));
        return UploadMixedBatchResult.Created(body, workspace);
    }

    private static async ValueTask<long> CountAsync(BinaryPartHandle handle, CancellationToken cancellationToken)
    {
        Stream? stream = await handle.OpenStreamAsync(cancellationToken);
        if (stream is null)
        {
            return 0;
        }

        return await CountStreamAsync(stream, cancellationToken);
    }

    private static async ValueTask<long> CountStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        long total = 0;
        byte[] scratch = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(scratch, cancellationToken)) > 0)
        {
            total += read;
        }

        return total;
    }
}