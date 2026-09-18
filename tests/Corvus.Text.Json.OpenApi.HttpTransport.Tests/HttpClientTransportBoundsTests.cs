// <copyright file="HttpClientTransportBoundsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Net;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.OpenApi.HttpTransport.Tests;

/// <summary>
/// The opt-in request timeout and response size bound on <see cref="HttpClientTransport"/>.
/// </summary>
[TestClass]
public class HttpClientTransportBoundsTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(150);

    [TestMethod]
    [Timeout(20_000)]
    public async Task Timeout_HeadersNeverArrive_ThrowsTransportTimeout()
    {
        using HttpClient client = CreateClient((_, ct) => NeverAsync(ct));
        await using HttpClientTransport transport = new(client, ShortTimeout, maxResponseLength: null);

        ApiTransportTimeoutException ex = await Assert.ThrowsExactlyAsync<ApiTransportTimeoutException>(
            async () => await transport.SendAsync<GetRequest, DrainingResponse>(default));

        Assert.AreEqual(ShortTimeout, ex.Timeout);
        Assert.IsInstanceOfType<OperationCanceledException>(ex.InnerException);
    }

    [TestMethod]
    [Timeout(20_000)]
    public async Task Timeout_BodyNeverCompletes_ThrowsTransportTimeout()
    {
        // The headers arrive at once, which is where HttpClient.Timeout stops applying. The body then stalls.
        using HttpClient client = CreateClient((_, _) => Task.FromResult(Respond(new StreamContent(new StallingStream(prefix: 8)))));
        await using HttpClientTransport transport = new(client, ShortTimeout, maxResponseLength: null);

        await Assert.ThrowsExactlyAsync<ApiTransportTimeoutException>(
            async () => await transport.SendAsync<GetRequest, DrainingResponse>(default));
    }

    [TestMethod]
    public async Task Timeout_CallerCancels_StaysACancellation()
    {
        using HttpClient client = CreateClient((_, ct) => NeverAsync(ct));
        await using HttpClientTransport transport = new(client, TimeSpan.FromMinutes(5), maxResponseLength: null);
        using CancellationTokenSource caller = new(ShortTimeout);

        Exception ex = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await transport.SendAsync<GetRequest, DrainingResponse>(default, caller.Token));

        Assert.IsNotInstanceOfType<ApiTransportTimeoutException>(ex);
    }

    [TestMethod]
    public async Task Timeout_FastResponse_Succeeds()
    {
        using HttpClient client = CreateClient((_, _) => Task.FromResult(Respond(new ByteArrayContent(new byte[32]))));
        await using HttpClientTransport transport = new(client, TimeSpan.FromMinutes(5), maxResponseLength: null);

        DrainingResponse response = await transport.SendAsync<GetRequest, DrainingResponse>(default);

        Assert.AreEqual(32, response.Length);
    }

    [TestMethod]
    public async Task MaxResponseLength_DeclaredLengthOverTheBound_RefusedBeforeAnyRead()
    {
        CountingStream body = new(length: 1024);
        StreamContent content = new(body);
        content.Headers.ContentLength = 1024;
        using HttpClient client = CreateClient((_, _) => Task.FromResult(Respond(content)));
        await using HttpClientTransport transport = new(client, requestTimeout: null, maxResponseLength: 100);

        ApiResponseTooLargeException ex = await Assert.ThrowsExactlyAsync<ApiResponseTooLargeException>(
            async () => await transport.SendAsync<GetRequest, DrainingResponse>(default));

        Assert.AreEqual(100, ex.MaxResponseLength);
        Assert.AreEqual(0, body.BytesServed);
    }

    [TestMethod]
    public async Task MaxResponseLength_ChunkedBodyOverTheBound_ThrowsDuringTheRead()
    {
        // No Content-Length, as on a chunked response: only counting the read can catch it.
        CountingStream body = new(length: 1_000_000);
        using HttpClient client = CreateClient((_, _) => Task.FromResult(Respond(new LengthlessContent(body))));
        await using HttpClientTransport transport = new(client, requestTimeout: null, maxResponseLength: 4096);

        await Assert.ThrowsExactlyAsync<ApiResponseTooLargeException>(
            async () => await transport.SendAsync<GetRequest, DrainingResponse>(default));

        Assert.IsTrue(body.BytesServed < 1_000_000, "The read should stop near the bound, not drain the body.");
    }

    [TestMethod]
    public async Task MaxResponseLength_DeclaredLengthUnderstatesTheBody_ThrowsDuringTheRead()
    {
        StreamContent content = new(new CountingStream(length: 1024));
        content.Headers.ContentLength = 10;
        using HttpClient client = CreateClient((_, _) => Task.FromResult(Respond(content)));
        await using HttpClientTransport transport = new(client, requestTimeout: null, maxResponseLength: 100);

        await Assert.ThrowsExactlyAsync<ApiResponseTooLargeException>(
            async () => await transport.SendAsync<GetRequest, DrainingResponse>(default));
    }

    [TestMethod]
    public async Task MaxResponseLength_BodyExactlyAtTheBound_Succeeds()
    {
        using HttpClient client = CreateClient((_, _) => Task.FromResult(Respond(new LengthlessContent(new CountingStream(length: 100)))));
        await using HttpClientTransport transport = new(client, requestTimeout: null, maxResponseLength: 100);

        DrainingResponse response = await transport.SendAsync<GetRequest, DrainingResponse>(default);

        Assert.AreEqual(100, response.Length);
    }

    [TestMethod]
    public async Task NoBounds_LargeBody_IsUntouched()
    {
        using HttpClient client = CreateClient((_, _) => Task.FromResult(Respond(new LengthlessContent(new CountingStream(length: 100_000)))));
        await using HttpClientTransport transport = new(client, requestTimeout: null, maxResponseLength: null);

        DrainingResponse response = await transport.SendAsync<GetRequest, DrainingResponse>(default);

        Assert.AreEqual(100_000, response.Length);
    }

    [TestMethod]
    public void Constructor_NonPositiveBounds_AreRejected()
    {
        using HttpClient client = new();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HttpClientTransport(client, TimeSpan.Zero, null));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HttpClientTransport(client, TimeSpan.FromSeconds(-1), null));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HttpClientTransport(client, null, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HttpClientTransport(client, null, -1));
    }

    [TestMethod]
    public async Task Factory_BoundedTransport_CarriesTheBounds()
    {
        using HttpClient client = CreateClient((_, _) => Task.FromResult(Respond(new LengthlessContent(new CountingStream(length: 1024)))));
        HttpClientApiTransportFactory factory = new(client);
        await using IApiTransport transport = factory.CreateTransport(requestTimeout: null, maxResponseLength: 100);

        await Assert.ThrowsExactlyAsync<ApiResponseTooLargeException>(
            async () => await transport.SendAsync<GetRequest, DrainingResponse>(default));
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        => new(new DelegateHandler(respond)) { BaseAddress = new Uri("http://localhost") };

    private static HttpResponseMessage Respond(HttpContent content) => new(HttpStatusCode.OK) { Content = content };

    private static async Task<HttpResponseMessage> NeverAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        throw new InvalidOperationException("unreachable");
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request, cancellationToken);
    }

    /// <summary>Content with no computable length, so no Content-Length header: the shape of a chunked response.</summary>
    private sealed class LengthlessContent(Stream body) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => body.CopyToAsync(stream);

        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult(body);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    /// <summary>Serves <c>length</c> zero bytes, a little at a time, and counts what it has served.</summary>
    private sealed class CountingStream(long length) : Stream
    {
        public long BytesServed { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = (int)Math.Min(Math.Min(count, 512), length - this.BytesServed);
            Array.Clear(buffer, offset, n);
            this.BytesServed += n;
            return n;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Serves a few bytes and then never completes another read until cancelled.</summary>
    private sealed class StallingStream(int prefix) : Stream
    {
        private int served;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("async only");

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (this.served < prefix)
            {
                int n = Math.Min(buffer.Length, prefix - this.served);
                buffer.Span[..n].Clear();
                this.served += n;
                return n;
            }

            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Drains the body with the token it was given, as a generated response does.</summary>
    private struct DrainingResponse : IApiResponse<DrainingResponse>
    {
        public int StatusCode { get; private set; }

        public bool IsSuccess => true;

        public long Length { get; private set; }

        public static async ValueTask<DrainingResponse> CreateAsync(
            int statusCode,
            Stream contentStream,
            string? contentType = null,
            IResponseHeaders? responseHeaders = null,
            IAsyncDisposable? owner = null,
            IApiTransport? transport = null,
            CancellationToken cancellationToken = default)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
            try
            {
                long total = 0;
                int read;
                while ((read = await contentStream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    total += read;
                }

                if (owner is not null)
                {
                    await owner.DisposeAsync().ConfigureAwait(false);
                }

                return new DrainingResponse { StatusCode = statusCode, Length = total };
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public ValueTask DisposeAsync() => default;

        public void Validate(ValidationMode mode = ValidationMode.Basic) { }
    }

    private readonly struct GetRequest : IApiRequest<GetRequest>
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

        public void Validate(ValidationMode mode = ValidationMode.Basic) { }
    }
}