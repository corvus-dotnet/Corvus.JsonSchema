// <copyright file="ClientWireParityTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using System.Text.Json;
using Corvus.Text.Json.OpenApi;
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
            await CaptureAsync("search-full-style-matrix", SearchFullStyleMatrix),
            await CaptureAsync("note-text-plain-body", NoteTextPlainBody),
            await CaptureAsync("form-urlencoded-body", FormUrlencodedBody),
            await CaptureAsync("upload-octet-stream-body", UploadOctetStreamBody),
            await CaptureAsync("search-cookie-param", SearchCookieParam),
            await CaptureAsync("avatar-multipart-body", AvatarMultipartBody),
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
            Assert.AreEqual(want.ContentType, actual.ContentType, $"Case '{actual.Name}' contentType mismatch.");
            Assert.AreEqual(want.BodyUtf8, actual.BodyUtf8, $"Case '{actual.Name}' bodyUtf8 mismatch.");

            // Headers are captured sorted by name ascending, so a formatted-string comparison is a
            // faithful, order-sensitive equality check with a readable failure message.
            Assert.AreEqual(
                FormatHeaders(want.Headers),
                FormatHeaders(actual.Headers),
                $"Case '{actual.Name}' headers mismatch.");
        }
    }

    private static string FormatHeaders(IReadOnlyList<KeyValuePair<string, string>> headers)
        => string.Join(", ", headers.Select(static h => $"{h.Key}={h.Value}"));

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

    private static async Task SearchFullStyleMatrix(HttpClientTransport transport)
    {
        // W4: search with EVERY query param set (session cookie left unset) — exercises the full
        // style matrix: matrix path object, spaceDelimited array, pipeDelimited array, deepObject,
        // form-explode array, form-explode object, and a simple header array.
        var request = new SearchRequest(
            GetSearchByScopeScope.ParseValue("""{"kind":"k1","region":"r1"}"""u8))
        {
            Tags = GetSearchByScopeTags.ParseValue("""["a b","c"]"""u8),
            Codes = GetSearchByScopeCodes.ParseValue("""["c1","c2"]"""u8),
            Filter = GetSearchByScopeFilter.ParseValue("""{"min":"1","max":"2"}"""u8),
            Fields = GetSearchByScopeFields.ParseValue("""["f1","f2"]"""u8),
            Opts = GetSearchByScopeOpts.ParseValue("""{"sort":"asc","dir":"up"}"""u8),
            XTags = GetSearchByScopeXTags.ParseValue("""["t1","t2"]"""u8),
        };

        await using SearchResponse response = await transport
            .SendAsync<SearchRequest, SearchResponse>(in request, CancellationToken.None);
    }

    private static async Task NoteTextPlainBody(HttpClientTransport transport)
    {
        // W5: note with a text/plain body. The generated client sends the raw bytes with
        // content type "text/plain" via the transport's stream-body overload.
        var client = new ApiStatusClient(transport);

        using var body = new MemoryStream(Encoding.UTF8.GetBytes("hello note"));

        await using NoteResponse response = await client.NoteAsync(body, CancellationToken.None);
    }

    private static async Task FormUrlencodedBody(HttpClientTransport transport)
    {
        // W6: form with an application/x-www-form-urlencoded body. The generated client serializes
        // the typed body via FormUrlEncodedSerializer; the exact encoding it produces is the reference.
        var client = new ApiStatusClient(transport);

        PostFormBody body = PostFormBody.ParseValue("""{"name":"gadget","count":3,"tags":["a","b"]}"""u8);

        await using FormResponse response = await client.FormAsync(body, CancellationToken.None);
    }

    private static async Task UploadOctetStreamBody(HttpClientTransport transport)
    {
        // W7: upload with an application/octet-stream body. The generated client sends the raw bytes
        // with content type "application/octet-stream" via the transport's stream-body overload.
        var client = new ApiStatusClient(transport);

        using var body = new MemoryStream(Encoding.ASCII.GetBytes("binary-data"));

        await using UploadResponse response = await client.UploadAsync(body, CancellationToken.None);
    }

    private static async Task SearchCookieParam(HttpClientTransport transport)
    {
        // W8: search with only the matrix scope path param and the `session` cookie param set.
        // Exercises cookie-param composition (the Cookie request header).
        var request = new SearchRequest(
            GetSearchByScopeScope.ParseValue("""{"kind":"k1","region":"r1"}"""u8))
        {
            Session = JsonString.ParseValue("\"sess-1\""u8),
        };

        await using SearchResponse response = await transport
            .SendAsync<SearchRequest, SearchResponse>(in request, CancellationToken.None);
    }

    private static async Task AvatarMultipartBody(HttpClientTransport transport)
    {
        // W9: avatar with a multipart/form-data body — two text fields (id, tags array) plus a
        // binary "file" part. The client composes the multipart body with a random boundary; the
        // capture step normalizes that boundary to a fixed placeholder for parity.
        var client = new ApiStatusClient(transport);

        PostAvatarBody body = PostAvatarBody.ParseValue("""{"id":"a1","tags":["t1","t2"]}"""u8);

        byte[] fileBytes = Encoding.ASCII.GetBytes("filedata");
        var file = new BinaryPartData(
            (stream, ct) =>
            {
                stream.Write(fileBytes);
                return default;
            },
            ContentType: "application/octet-stream",
            FileName: "f.bin");

        await using AvatarResponse response = await client.AvatarAsync(body, file, CancellationToken.None);
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

        // Op-declared headers only: X-* (any op-declared header) or Cookie (cookie params),
        // name -> first value, sorted by name ascending. This isolates spec-declared headers
        // from framework/default headers (e.g. Accept).
        KeyValuePair<string, string>[] headers = captured.Headers
            .Where(static h =>
                h.Key.StartsWith("X-", StringComparison.OrdinalIgnoreCase)
                || h.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase))
            .Select(static h => new KeyValuePair<string, string>(h.Key, h.Value.First()))
            .OrderBy(static h => h.Key, StringComparer.Ordinal)
            .ToArray();

        // Content type and body come from the captured request body (null when there is no body).
        string? contentType = harness.CapturedRequestContentType;
        byte[]? bodyBytes = harness.CapturedRequestBody;
        string? bodyUtf8 = bodyBytes is null ? null : Encoding.UTF8.GetString(bodyBytes);

        // For multipart/form-data the boundary is randomly generated, so the framing bytes are
        // only comparable modulo the boundary. Replace the actual boundary token with a fixed
        // placeholder so the multipart framing (CRLFs, Content-Disposition, part order) can be
        // asserted for parity. The TypeScript half normalizes its own boundary the same way.
        if (bodyUtf8 is not null
            && string.Equals(contentType, "multipart/form-data", StringComparison.OrdinalIgnoreCase)
            && captured.Content?.Headers.ContentType is { } fullContentType)
        {
            string? boundary = fullContentType.Parameters
                .FirstOrDefault(p => string.Equals(p.Name, "boundary", StringComparison.OrdinalIgnoreCase))
                ?.Value?
                .Trim('"');

            if (!string.IsNullOrEmpty(boundary))
            {
                bodyUtf8 = bodyUtf8.Replace(boundary, "PARITY-BOUNDARY", StringComparison.Ordinal);
            }
        }

        return new WireCase(name, method, target, headers, contentType, bodyUtf8);
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

                writer.WriteStartObject("headers");
                foreach (KeyValuePair<string, string> header in wireCase.Headers)
                {
                    writer.WriteString(header.Key, header.Value);
                }

                writer.WriteEndObject();

                if (wireCase.ContentType is null)
                {
                    writer.WriteNull("contentType");
                }
                else
                {
                    writer.WriteString("contentType", wireCase.ContentType);
                }

                if (wireCase.BodyUtf8 is null)
                {
                    writer.WriteNull("bodyUtf8");
                }
                else
                {
                    writer.WriteString("bodyUtf8", wireCase.BodyUtf8);
                }

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
            List<KeyValuePair<string, string>> headers = [];
            foreach (System.Text.Json.JsonProperty header in element.GetProperty("headers").EnumerateObject())
            {
                headers.Add(new KeyValuePair<string, string>(header.Name, header.Value.GetString()!));
            }

            System.Text.Json.JsonElement contentTypeElement = element.GetProperty("contentType");
            string? contentType = contentTypeElement.ValueKind == System.Text.Json.JsonValueKind.Null
                ? null
                : contentTypeElement.GetString();

            System.Text.Json.JsonElement bodyElement = element.GetProperty("bodyUtf8");
            string? bodyUtf8 = bodyElement.ValueKind == System.Text.Json.JsonValueKind.Null
                ? null
                : bodyElement.GetString();

            result.Add(new WireCase(
                element.GetProperty("name").GetString()!,
                element.GetProperty("method").GetString()!,
                element.GetProperty("target").GetString()!,
                [.. headers],
                contentType,
                bodyUtf8));
        }

        return [.. result];
    }

    private readonly record struct WireCase(
        string Name,
        string Method,
        string Target,
        IReadOnlyList<KeyValuePair<string, string>> Headers,
        string? ContentType,
        string? BodyUtf8);

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

        public byte[]? CapturedRequestBody => this.handler.CapturedRequestBody;

        public string? CapturedRequestContentType => this.handler.CapturedRequestContentType;

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

        public byte[]? CapturedRequestBody { get; private set; }

        public string? CapturedRequestContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            this.CapturedRequest = request;

            if (request.Content is not null)
            {
                this.CapturedRequestBody = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

                // Capture the media type only (e.g. "application/json"), not the full header value
                // which would include a transport-specific "; charset=utf-8". The media type is the
                // cross-language-stable value the TypeScript half also produces.
                this.CapturedRequestContentType = request.Content.Headers.ContentType?.MediaType;
            }

            HttpContent content = string.IsNullOrEmpty(this.responseBody)
                ? new ByteArrayContent([])
                : new StringContent(this.responseBody, Encoding.UTF8, "application/json");

            return new HttpResponseMessage(this.statusCode) { Content = content };
        }
    }
}