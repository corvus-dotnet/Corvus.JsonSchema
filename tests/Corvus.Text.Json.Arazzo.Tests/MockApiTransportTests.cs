// <copyright file="MockApiTransportTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
}