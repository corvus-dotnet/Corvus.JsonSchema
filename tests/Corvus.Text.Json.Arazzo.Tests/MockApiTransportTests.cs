// <copyright file="MockApiTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Diagnostics;
using System.Text;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.Polly;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using global::Polly;
using global::Polly.Retry;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

[TestClass]
public class MockApiTransportTests
{
    [TestMethod]
    public async Task Returns_scripted_response_and_records_resolved_path()
    {
        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{id}", 200, """{"id":"42"}""");

        await using FakeResponse response = await transport.SendAsync<FakeRequest, FakeResponse>(new FakeRequest("42"));

        response.StatusCode.ShouldBe(200);
        response.Body.ShouldBe("""{"id":"42"}""");
        transport.Requests.Count.ShouldBe(1);
        transport.Requests[0].Method.ShouldBe(OperationMethod.Get);
        transport.Requests[0].Path.ShouldBe("/pets/42");
    }

    [TestMethod]
    public async Task Unmatched_route_returns_default_status()
    {
        var transport = new MockApiTransport(defaultStatusCode: 404);

        await using FakeResponse response = await transport.SendAsync<FakeRequest, FakeResponse>(new FakeRequest("1"));

        response.StatusCode.ShouldBe(404);
        response.IsSuccess.ShouldBeFalse();
    }

    [TestMethod]
    public async Task Sequenced_responses_advance_then_repeat_last()
    {
        var transport = new MockApiTransport();
        transport
            .EnqueueResponse(OperationMethod.Get, "/pets/{id}", 503, """{"err":"retry"}""")
            .EnqueueResponse(OperationMethod.Get, "/pets/{id}", 200, """{"ok":true}""");

        int s1;
        string b1;
        await using (FakeResponse r1 = await transport.SendAsync<FakeRequest, FakeResponse>(new FakeRequest("1")))
        {
            s1 = r1.StatusCode;
            b1 = r1.Body;
        }

        int s2;
        await using (FakeResponse r2 = await transport.SendAsync<FakeRequest, FakeResponse>(new FakeRequest("1")))
        {
            s2 = r2.StatusCode;
        }

        int s3;
        await using (FakeResponse r3 = await transport.SendAsync<FakeRequest, FakeResponse>(new FakeRequest("1")))
        {
            s3 = r3.StatusCode;
        }

        s1.ShouldBe(503);
        b1.ShouldBe("""{"err":"retry"}""");
        s2.ShouldBe(200);
        s3.ShouldBe(200); // last response repeats
        transport.Requests.Count.ShouldBe(3);
    }

    [TestMethod]
    public async Task Instrumented_transport_creates_a_client_span_for_the_operation()
    {
        // InstrumentedApiTransport decorates any IApiTransport with a Client span per operation, named
        // {method} {route}, carrying HTTP semantic tags — the OpenAPI analogue of the AsyncAPI decorator.
        List<Activity> activities = [];
        using ActivityListener listener = CreateActivityListener(activities);

        var inner = new MockApiTransport();
        inner.SetResponse(OperationMethod.Get, "/pets/{id}", 200, """{"id":"42"}""");
        await using var transport = new InstrumentedApiTransport(inner);

        await using (FakeResponse response = await transport.SendAsync<FakeRequest, FakeResponse>(new FakeRequest("42")))
        {
            response.StatusCode.ShouldBe(200);
        }

        activities.Count.ShouldBe(1);
        Activity activity = activities[0];
        activity.OperationName.ShouldBe("GET /pets/{id}");
        activity.Kind.ShouldBe(ActivityKind.Client);
        activity.GetTagItem("http.request.method").ShouldBe("GET");
        activity.GetTagItem("url.template").ShouldBe("/pets/{id}");
        activity.GetTagItem("http.response.status_code").ShouldBe(200);
    }

    [TestMethod]
    public async Task Resilient_transport_retries_a_transient_failure()
    {
        // ResilientApiTransport wraps each operation in a Polly pipeline; a retry strategy recovers a
        // transient failure without the caller (or the workflow step) knowing.
        var mock = new MockApiTransport();
        mock.SetResponse(OperationMethod.Get, "/pets/{id}", 200, """{"id":"42"}""");
        var flaky = new FlakyTransport(mock);

        ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 2, Delay = TimeSpan.Zero })
            .Build();
        await using var transport = new ResilientApiTransport(flaky, pipeline);

        await using (FakeResponse response = await transport.SendAsync<FakeRequest, FakeResponse>(new FakeRequest("42")))
        {
            response.StatusCode.ShouldBe(200);
        }

        // The first attempt threw and the pipeline retried, so the inner transport saw two calls.
        flaky.Calls.ShouldBe(2);
    }

    private static ActivityListener CreateActivityListener(List<Activity> activities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OpenApiTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Add,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    /// <summary>Minimal generated-style request: GET /pets/{id}.</summary>
    private readonly struct FakeRequest(string id) : IApiRequest<FakeRequest>
    {
        private readonly string id = id;

        public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets/{id}"u8;

        public static OperationMethod Method => OperationMethod.Get;

        public static bool HasPathParameters => true;

        public static bool HasQueryParameters => false;

        public static bool HasHeaderParameters => false;

        public static bool HasCookieParameters => false;

        public void WriteResolvedPath(IBufferWriter<byte> writer)
        {
            writer.Write("/pets/"u8);
            int byteCount = Encoding.UTF8.GetByteCount(this.id);
            Span<byte> destination = writer.GetSpan(byteCount);
            int written = Encoding.UTF8.GetBytes(this.id, destination);
            writer.Advance(written);
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

    /// <summary>Minimal generated-style response that captures the body text.</summary>
    private readonly struct FakeResponse : IApiResponse<FakeResponse>
    {
        public int StatusCode { get; private init; }

        public string Body { get; private init; }

        public bool IsSuccess => this.StatusCode is >= 200 and < 300;

        public static async ValueTask<FakeResponse> CreateAsync(
            int statusCode,
            Stream contentStream,
            string? contentType = null,
            IResponseHeaders? responseHeaders = null,
            IAsyncDisposable? owner = null,
            IApiTransport? transport = null,
            CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(contentStream);
            string body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            if (owner is not null)
            {
                await owner.DisposeAsync().ConfigureAwait(false);
            }

            return new FakeResponse { StatusCode = statusCode, Body = body };
        }

        public void Validate(ValidationMode mode = ValidationMode.Basic)
        {
        }

        public ValueTask DisposeAsync() => default;
    }

    /// <summary>An IApiTransport that throws on its first call, then delegates — to exercise resilience retry.</summary>
    private sealed class FlakyTransport(IApiTransport inner) : IApiTransport
    {
        public int Calls { get; private set; }

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
        {
            this.Calls++;
            if (this.Calls == 1)
            {
                throw new InvalidOperationException("transient");
            }

            return inner.SendAsync<TRequest, TResponse>(in request, cancellationToken);
        }

        public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(in TRequest request, in TBody body, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TBody : struct, Corvus.Text.Json.Internal.IJsonElement<TBody>
            where TResponse : struct, IApiResponse<TResponse>
            => throw new NotSupportedException();

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Stream body, string contentType, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => throw new NotSupportedException();

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Func<Stream, CancellationToken, ValueTask> bodyWriter, string contentType, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => throw new NotSupportedException();

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}