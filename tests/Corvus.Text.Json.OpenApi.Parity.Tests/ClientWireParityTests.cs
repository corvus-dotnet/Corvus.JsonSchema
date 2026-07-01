// <copyright file="ClientWireParityTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using System.Text.Json;
using Corvus.Text.Json.OpenApi.HttpTransport;
using PetstoreParity.Client;
using PetstoreParity.Client.Models;

namespace Corvus.Text.Json.OpenApi.Parity.Tests;

/// <summary>
/// Emits (or asserts against) a shared wire fixture of <c>method + target (path+query)</c>
/// for a fixed set of request cases driven through the generated petstore-3.0 client.
/// </summary>
/// <remarks>
/// <para>
/// This is the C# half of a C#&lt;-&gt;TypeScript byte-parity harness. Each case constructs a
/// generated request, sends it through a capturing <see cref="HttpClientTransport"/>, and records
/// the outgoing HTTP method and request target (path + query string).
/// </para>
/// <para>
/// When the environment variable <c>UPDATE_PARITY_FIXTURE</c> is set to <c>1</c>, the fixture file
/// is (re)written from the captured wire values. Otherwise the captured values are asserted against
/// the committed fixture. The TypeScript half asserts against the same fixture.
/// </para>
/// </remarks>
[TestClass]
public class ClientWireParityTests
{
    private const string SpecName = "petstore-3.0";

    private const string CannedResponseBody = """{"id":"p-1","name":"Rex","tag":"dog"}""";

    [TestMethod]
    public async Task WireParity_AcrossAllCases()
    {
        WireCase[] cases =
        [
            await CaptureAsync("getPet-reserved-path", GetPetReservedPath),
            await CaptureAsync("updatePet-compound-query-boolean", UpdatePetCompoundQueryBoolean),
            await CaptureAsync("search-spacedelimited-deepobject", SearchSpaceDelimitedDeepObject),
        ];

        string fixturePath = ResolveFixturePath();

        if (Environment.GetEnvironmentVariable("UPDATE_PARITY_FIXTURE") == "1")
        {
            WriteFixture(fixturePath, cases);
            return;
        }

        WireCase[] expected = LoadFixture(fixturePath);

        Assert.AreEqual(expected.Length, cases.Length, "Case count differs from the committed fixture.");

        for (int i = 0; i < cases.Length; i++)
        {
            WireCase actual = cases[i];
            WireCase want = expected[i];

            Assert.AreEqual(want.Name, actual.Name, $"Case {i} name mismatch.");
            Assert.AreEqual(want.Method, actual.Method, $"Case '{actual.Name}' method mismatch.");
            Assert.AreEqual(want.Target, actual.Target, $"Case '{actual.Name}' target mismatch.");
        }
    }

    private static async Task GetPetReservedPath(HttpClientTransport transport)
    {
        // C1: getPet with petId = "a/b c" (reserved characters in a path parameter).
        var request = new GetPetRequest(JsonString.ParseValue("\"a/b c\""u8));

        await using GetPetResponse response = await transport
            .SendAsync<GetPetRequest, GetPetResponse>(in request, CancellationToken.None);
    }

    private static async Task UpdatePetCompoundQueryBoolean(HttpClientTransport transport)
    {
        // C2: updatePet with a path param, a compound (array) query param, a boolean query param,
        // a required header, and a JSON request body.
        var request = new UpdatePetRequest(
            JsonString.ParseValue("\"p 1\""u8),
            JsonString.ParseValue("\"req-1\""u8))
        {
            Tags = PostPetsByPetIdTags.ParseValue("""["a b","a/b"]"""u8),
            Verbose = JsonBoolean.ParseValue("true"u8),
        };

        PetUpdate body = PetUpdate.ParseValue("""{"name":"Rex","tag":"dog"}"""u8);

        await using UpdatePetResponse response = await transport
            .SendAsync<UpdatePetRequest, PetUpdate, UpdatePetResponse>(in request, in body, CancellationToken.None);
    }

    private static async Task SearchSpaceDelimitedDeepObject(HttpClientTransport transport)
    {
        // C3: search with a matrix-style object path param, a spaceDelimited array query param,
        // and a deepObject query param. All other optional params are left unset.
        var request = new SearchRequest(
            GetSearchByScopeScope.ParseValue("""{"kind":"k 1","region":"r/2"}"""u8))
        {
            Tags = GetSearchByScopeTags.ParseValue("""["a b","c/d"]"""u8),
            Filter = GetSearchByScopeFilter.ParseValue("""{"min":"1","max":"2"}"""u8),
        };

        await using SearchResponse response = await transport
            .SendAsync<SearchRequest, SearchResponse>(in request, CancellationToken.None);
    }

    private static async Task<WireCase> CaptureAsync(
        string name,
        Func<HttpClientTransport, Task> send)
    {
        using var harness = new TestHarness(HttpStatusCode.OK, CannedResponseBody);

        await send(harness.Transport);

        HttpRequestMessage captured = harness.CapturedRequest
            ?? throw new InvalidOperationException($"No request was captured for case '{name}'.");

        string method = captured.Method.Method;
        string target = captured.RequestUri!.PathAndQuery;

        return new WireCase(name, method, target);
    }

    private static string ResolveFixturePath()
    {
        // Walk up from the test assembly location to the repository root (the directory that holds
        // the docs/ folder), then resolve the shared fixture path.
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs", "typescript", "openapi-examples")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("Could not locate the docs/typescript/openapi-examples directory from the test output location.");
        }

        return Path.Combine(dir.FullName, "docs", "typescript", "openapi-examples", "parity", "wire-fixture.json");
    }

    private static void WriteFixture(string fixturePath, WireCase[] cases)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)!);

        using var buffer = new MemoryStream();
        var writerOptions = new JsonWriterOptions
        {
            Indented = true,
            IndentSize = 2,

            // Emit '&' and other characters literally rather than HTML-escaped (&), so the
            // fixture reads cleanly and matches the raw wire target verbatim.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        using (var writer = new Utf8JsonWriter(buffer, writerOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("spec", SpecName);
            writer.WriteStartArray("cases");
            foreach (WireCase wireCase in cases)
            {
                writer.WriteStartObject();
                writer.WriteString("name", wireCase.Name);
                writer.WriteString("method", wireCase.Method);
                writer.WriteString("target", wireCase.Target);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        buffer.WriteByte((byte)'\n');
        File.WriteAllBytes(fixturePath, buffer.ToArray());
    }

    private static WireCase[] LoadFixture(string fixturePath)
    {
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"Wire fixture not found at '{fixturePath}'. Run once with UPDATE_PARITY_FIXTURE=1 to emit it.");

        byte[] json = File.ReadAllBytes(fixturePath);
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
        System.Text.Json.JsonElement root = document.RootElement;

        Assert.AreEqual(SpecName, root.GetProperty("spec").GetString(), "Fixture spec name mismatch.");

        List<WireCase> result = [];
        foreach (System.Text.Json.JsonElement element in root.GetProperty("cases").EnumerateArray())
        {
            result.Add(new WireCase(
                element.GetProperty("name").GetString()!,
                element.GetProperty("method").GetString()!,
                element.GetProperty("target").GetString()!));
        }

        return [.. result];
    }

    private readonly record struct WireCase(string Name, string Method, string Target);

    /// <summary>
    /// Encapsulates a mock HTTP handler, HttpClient, and HttpClientTransport for testing.
    /// The handler captures the outgoing request and returns a canned response.
    /// </summary>
    private sealed class TestHarness : IDisposable
    {
        private readonly MockHandler handler;
        private readonly HttpClient client;

        public TestHarness(HttpStatusCode statusCode, string responseBody)
        {
            this.handler = new MockHandler(statusCode, responseBody);
            this.client = new HttpClient(this.handler)
            {
                BaseAddress = new Uri("http://localhost"),
            };
            this.Transport = new HttpClientTransport(this.client);
        }

        public HttpClientTransport Transport { get; }

        public HttpRequestMessage? CapturedRequest => this.handler.CapturedRequest;

        public void Dispose()
        {
            this.Transport.DisposeAsync().AsTask().GetAwaiter().GetResult();
            this.client.Dispose();
            this.handler.Dispose();
        }
    }

    /// <summary>
    /// A <see cref="DelegatingHandler"/> that captures the request and returns
    /// a preconfigured response with the given status code and body.
    /// </summary>
    private sealed class MockHandler : DelegatingHandler
    {
        private readonly HttpStatusCode statusCode;
        private readonly string responseBody;

        public MockHandler(HttpStatusCode statusCode, string responseBody)
        {
            this.statusCode = statusCode;
            this.responseBody = responseBody;
            this.InnerHandler = new HttpClientHandler();
        }

        public HttpRequestMessage? CapturedRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            this.CapturedRequest = request;

            if (request.Content is not null)
            {
                await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            }

            HttpContent content = string.IsNullOrEmpty(this.responseBody)
                ? new ByteArrayContent([])
                : new StringContent(this.responseBody, Encoding.UTF8, "application/json");

            return new HttpResponseMessage(this.statusCode) { Content = content };
        }
    }
}