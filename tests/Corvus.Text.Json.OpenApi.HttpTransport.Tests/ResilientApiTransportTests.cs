// <copyright file="ResilientApiTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.Polly;
using global::Polly;
using global::Polly.Retry;

namespace Corvus.Text.Json.OpenApi.HttpTransport.Tests;

/// <summary>
/// Tests for <see cref="ResilientApiTransport"/> request body handling across retried
/// attempts: seekable stream bodies rewind, non-seekable bodies retry only while
/// unconsumed, and write callbacks run once per attempt.
/// </summary>
[TestClass]
public class ResilientApiTransportTests
{
    private static ResiliencePipeline RetryPipeline(int maxRetries = 2) => new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = maxRetries,
            Delay = TimeSpan.Zero,
            ShouldHandle = new PredicateBuilder().Handle<SimulatedTransportException>(),
        })
        .Build();

    [TestMethod]
    public async Task SeekableBody_RetriedAttempt_ResendsFullBody()
    {
        byte[] payload = [1, 2, 3, 4, 5, 6, 7, 8];
        using MemoryStream body = new(payload);
        FlakyTransport inner = new(failuresBeforeSuccess: 1, consumeBodyBeforeFailing: true);
        await using ResilientApiTransport transport = new(inner, RetryPipeline());

        TestRequest request = default;
        TestResponse response = await transport.SendAsync<TestRequest, TestResponse>(in request, body, "application/octet-stream");

        Assert.AreEqual(200, response.StatusCode);
        Assert.AreEqual(2, inner.StreamAttemptBodies.Count);
        CollectionAssert.AreEqual(payload, inner.StreamAttemptBodies[1], "the retried attempt must re-send the full body");
    }

    [TestMethod]
    public async Task SeekableBody_NonZeroEntryPosition_RewindsToEntryPosition()
    {
        byte[] full = [9, 9, 9, 1, 2, 3];
        using MemoryStream body = new(full);
        body.Position = 3;
        FlakyTransport inner = new(failuresBeforeSuccess: 1, consumeBodyBeforeFailing: true);
        await using ResilientApiTransport transport = new(inner, RetryPipeline());

        TestRequest request = default;
        _ = await transport.SendAsync<TestRequest, TestResponse>(in request, body, "application/octet-stream");

        Assert.AreEqual(2, inner.StreamAttemptBodies.Count);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, inner.StreamAttemptBodies[0]);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, inner.StreamAttemptBodies[1], "the rewind must return to the entry position, not zero");
    }

    [TestMethod]
    public async Task NonSeekableBody_ConsumedBeforeFailure_RetryThrowsDescriptively()
    {
        byte[] payload = [1, 2, 3, 4];
        using NonSeekableStream body = new(payload);
        FlakyTransport inner = new(failuresBeforeSuccess: 1, consumeBodyBeforeFailing: true);
        await using ResilientApiTransport transport = new(inner, RetryPipeline());

        TestRequest request = default;
        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await transport.SendAsync<TestRequest, TestResponse>(in request, body, "application/octet-stream"));

        StringAssert.Contains(ex.Message, "not seekable");
        Assert.AreEqual(1, inner.StreamAttemptBodies.Count, "the consumed body must never be re-sent");
    }

    [TestMethod]
    public async Task NonSeekableBody_FailureBeforeConsumption_RetriesSuccessfully()
    {
        byte[] payload = [5, 6, 7, 8];
        using NonSeekableStream body = new(payload);
        FlakyTransport inner = new(failuresBeforeSuccess: 1, consumeBodyBeforeFailing: false);
        await using ResilientApiTransport transport = new(inner, RetryPipeline());

        TestRequest request = default;
        TestResponse response = await transport.SendAsync<TestRequest, TestResponse>(in request, body, "application/octet-stream");

        Assert.AreEqual(200, response.StatusCode);
        Assert.AreEqual(1, inner.StreamAttemptBodies.Count, "only the successful attempt read the body");
        CollectionAssert.AreEqual(payload, inner.StreamAttemptBodies[0], "an unconsumed non-seekable body must retry with its full content");
    }

    [TestMethod]
    public async Task BodyWriterCallback_InvokedOncePerAttempt()
    {
        byte[] payload = [10, 20, 30];
        int invocations = 0;
        FlakyTransport inner = new(failuresBeforeSuccess: 1, consumeBodyBeforeFailing: true);
        await using ResilientApiTransport transport = new(inner, RetryPipeline());

        TestRequest request = default;
        TestResponse response = await transport.SendAsync<TestRequest, TestResponse>(
            in request,
            async (stream, ct) =>
            {
                invocations++;
                await stream.WriteAsync(payload, ct);
            },
            "application/octet-stream");

        Assert.AreEqual(200, response.StatusCode);
        Assert.AreEqual(2, invocations, "the write callback must run once per attempt");
        Assert.AreEqual(2, inner.WriterAttemptBodies.Count);
        CollectionAssert.AreEqual(payload, inner.WriterAttemptBodies[1], "the retried attempt must carry the full body");
    }

    /// <summary>
    /// The failure a retry strategy is configured to handle.
    /// </summary>
    private sealed class SimulatedTransportException : Exception;

    /// <summary>
    /// An inner transport that fails a configurable number of attempts, optionally
    /// consuming the body first (as a real connection does when it drops mid-send),
    /// and records the bytes each attempt observed.
    /// </summary>
    private sealed class FlakyTransport : IApiTransport
    {
        private readonly bool consumeBodyBeforeFailing;
        private int remainingFailures;

        public FlakyTransport(int failuresBeforeSuccess, bool consumeBodyBeforeFailing)
        {
            this.remainingFailures = failuresBeforeSuccess;
            this.consumeBodyBeforeFailing = consumeBodyBeforeFailing;
        }

        public List<byte[]> StreamAttemptBodies { get; } = [];

        public List<byte[]> WriterAttemptBodies { get; } = [];

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => throw new NotSupportedException();

        public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(in TRequest request, in TBody body, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TBody : struct, IJsonElement<TBody>
            where TResponse : struct, IApiResponse<TResponse>
            => throw new NotSupportedException();

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Stream body, string contentType, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => this.SendStreamCoreAsync<TResponse>(body, cancellationToken);

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Func<Stream, CancellationToken, ValueTask> bodyWriter, string contentType, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => this.SendWriterCoreAsync<TResponse>(bodyWriter, cancellationToken);

        private async ValueTask<TResponse> SendStreamCoreAsync<TResponse>(Stream body, CancellationToken cancellationToken)
            where TResponse : struct, IApiResponse<TResponse>
        {
            if (this.remainingFailures > 0 && !this.consumeBodyBeforeFailing)
            {
                this.remainingFailures--;
                throw new SimulatedTransportException();
            }

            using MemoryStream ms = new();
            await body.CopyToAsync(ms, cancellationToken);
            this.StreamAttemptBodies.Add(ms.ToArray());

            if (this.remainingFailures > 0)
            {
                this.remainingFailures--;
                throw new SimulatedTransportException();
            }

            return await TResponse.CreateAsync(200, Stream.Null, null, null, null, this, cancellationToken);
        }

        private async ValueTask<TResponse> SendWriterCoreAsync<TResponse>(Func<Stream, CancellationToken, ValueTask> bodyWriter, CancellationToken cancellationToken)
            where TResponse : struct, IApiResponse<TResponse>
        {
            using MemoryStream ms = new();
            await bodyWriter(ms, cancellationToken);
            this.WriterAttemptBodies.Add(ms.ToArray());

            if (this.remainingFailures > 0)
            {
                this.remainingFailures--;
                throw new SimulatedTransportException();
            }

            return await TResponse.CreateAsync(200, Stream.Null, null, null, null, this, cancellationToken);
        }

        public ValueTask DisposeAsync() => default;
    }

    /// <summary>
    /// A forward-only view over a byte payload.
    /// </summary>
    private sealed class NonSeekableStream : Stream
    {
        private readonly MemoryStream inner;

        public NonSeekableStream(byte[] payload)
        {
            this.inner = new MemoryStream(payload);
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => this.inner.Read(buffer, offset, count);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>POST /upload with no parameters.</summary>
    private readonly struct TestRequest : IApiRequest<TestRequest>
    {
        public static ReadOnlySpan<byte> PathTemplateUtf8 => "/upload"u8;

        public static OperationMethod Method => OperationMethod.Post;

        public static bool HasPathParameters => false;

        public static bool HasQueryParameters => false;

        public static bool HasHeaderParameters => false;

        public static bool HasCookieParameters => false;

        public void WriteResolvedPath(IBufferWriter<byte> writer)
        {
        }

        public int WriteQueryString(IBufferWriter<byte> writer) => 0;

        public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state)
        {
        }

        public int WriteCookies(IBufferWriter<byte> writer) => 0;

        public void Validate(ValidationMode mode = ValidationMode.Basic)
        {
        }
    }

    private struct TestResponse : IApiResponse<TestResponse>
    {
        public int StatusCode { get; private set; }

        public bool IsSuccess => this.StatusCode >= 200 && this.StatusCode < 300;

        public static ValueTask<TestResponse> CreateAsync(
            int statusCode,
            Stream contentStream,
            string? contentType = null,
            IResponseHeaders? responseHeaders = null,
            IAsyncDisposable? owner = null,
            IApiTransport? transport = null,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new TestResponse { StatusCode = statusCode });

        public ValueTask DisposeAsync() => default;

        public void Validate(ValidationMode mode = ValidationMode.Basic)
        {
        }
    }
}