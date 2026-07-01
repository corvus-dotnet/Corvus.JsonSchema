// <copyright file="ClientResponseParityTests.cs" company="Endjin Limited">
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
/// Emits (or asserts against) a shared response-decomposition fixture: for a fixed set of canned
/// responses, the C# petstore-3.0 client decodes the bytes and records which status branch matched,
/// whether the active body is schema-valid, and (for the headers case) each typed header getter's
/// value as a plain JSON value.
/// </summary>
/// <remarks>
/// <para>
/// This is the response half of the C#&lt;-&gt;TypeScript byte-parity harness. It proves the C#
/// client decodes canned response bytes into the same logical values the TypeScript client will.
/// </para>
/// <para>
/// When the environment variable <c>UPDATE_PARITY_FIXTURE</c> is set to <c>1</c>, the fixture file
/// is (re)written from the decoded values. Otherwise the decoded values are asserted against the
/// committed fixture. The TypeScript half asserts against the same fixture.
/// </para>
/// </remarks>
[TestClass]
public class ClientResponseParityTests
{
    private const string SpecName = "petstore-3.0";

    [TestMethod]
    public async Task ResponseParity_AcrossAllCases()
    {
        string[] cases =
        [
            await CaptureAsync(
                name: "getPet-200-pet",
                operation: "getPet",
                status: 200,
                bodyUtf8: """{"id":"p-1","name":"Rex","tag":"dog"}""",
                headers: null,
                decode: DecodeGetPet),
            await CaptureAsync(
                name: "getStatus-default-error",
                operation: "getStatus",
                status: 503,
                bodyUtf8: """{"code":503,"message":"down"}""",
                headers: null,
                decode: DecodeGetStatus),
            await CaptureAsync(
                name: "limits-200-headers",
                operation: "limits",
                status: 200,
                bodyUtf8: """{"id":"p-1","name":"Rex"}""",
                headers: new Dictionary<string, string>
                {
                    ["X-Rate-Limit"] = "100",
                    ["X-Request-Id"] = "req-7",
                    ["X-Tags"] = "a,b,c",
                    ["X-Expires-At"] = "2026-01-02T03:04:05Z",
                    ["X-Scope"] = "kind,read,region,eu",
                },
                decode: DecodeLimits),
        ];

        string fixturePath = ResolveFixturePath();

        if (Environment.GetEnvironmentVariable("UPDATE_PARITY_FIXTURE") == "1")
        {
            WriteFixture(fixturePath, cases);
            return;
        }

        string[] expected = LoadFixtureCases(fixturePath);

        Assert.AreEqual(expected.Length, cases.Length, "Case count differs from the committed fixture.");

        for (int i = 0; i < cases.Length; i++)
        {
            // Normalize both the freshly-decoded case (built by the Corvus writer) and the loaded
            // fixture case through the SAME serializer, so the comparison is over the logical JSON
            // and never trips on incidental byte differences between the two writers.
            Assert.AreEqual(
                Canonicalize(expected[i]),
                Canonicalize(cases[i]),
                $"Response case {i} decoded value differs from the committed fixture.");
        }
    }

    private static string Canonicalize(string json)
    {
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);
        using var buffer = new MemoryStream();
        var writerOptions = new System.Text.Json.JsonWriterOptions
        {
            Indented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer, writerOptions))
        {
            doc.RootElement.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    // ── Per-operation decoders ───────────────────────────────────────────
    // Each writes a "decoded" JSON object: { "matched", "valid", ["headerValues"] }.
    // The writer is the Corvus byte-native writer, so generated model WriteTo(Utf8JsonWriter)
    // calls bind directly to it (that overload takes Corvus.Text.Json.Utf8JsonWriter).
    private static async Task DecodeGetPet(HttpClientTransport transport, Corvus.Text.Json.Utf8JsonWriter writer)
    {
        await using GetPetResponse response = await transport
            .SendAsync<GetPetRequest, GetPetResponse>(
                new GetPetRequest(JsonString.ParseValue("\"p-1\""u8)),
                CancellationToken.None);

        writer.WriteStartObject();
        if (response.TryGetOk(out Pet body))
        {
            writer.WriteString("matched", "ok");
            writer.WriteBoolean("valid", body.EvaluateSchema());
        }
        else
        {
            writer.WriteString("matched", "default");
            writer.WriteBoolean("valid", false);
        }

        writer.WriteEndObject();
    }

    private static async Task DecodeGetStatus(HttpClientTransport transport, Corvus.Text.Json.Utf8JsonWriter writer)
    {
        await using GetStatusResponse response = await transport
            .SendAsync<GetStatusRequest, GetStatusResponse>(
                default(GetStatusRequest),
                CancellationToken.None);

        writer.WriteStartObject();
        if (response.TryGetOk(out ServiceStatus okBody))
        {
            writer.WriteString("matched", "ok");
            writer.WriteBoolean("valid", okBody.EvaluateSchema());
        }
        else if (response.TryGetDefault(out Error defaultBody))
        {
            writer.WriteString("matched", "default");
            writer.WriteBoolean("valid", defaultBody.EvaluateSchema());
        }
        else
        {
            writer.WriteString("matched", "unmatched");
            writer.WriteBoolean("valid", false);
        }

        writer.WriteEndObject();
    }

    private static async Task DecodeLimits(HttpClientTransport transport, Corvus.Text.Json.Utf8JsonWriter writer)
    {
        await using LimitsResponse response = await transport
            .SendAsync<LimitsRequest, LimitsResponse>(
                default(LimitsRequest),
                CancellationToken.None);

        writer.WriteStartObject();
        if (response.TryGetOk(out Pet body))
        {
            writer.WriteString("matched", "ok");
            writer.WriteBoolean("valid", body.EvaluateSchema());
        }
        else
        {
            writer.WriteString("matched", "default");
            writer.WriteBoolean("valid", false);
        }

        // Each generated typed header getter, keyed by its camelCase stem (no "Header" suffix),
        // emitted as a plain JSON value via the model's WriteTo (C# is the reference).
        writer.WriteStartObject("headerValues");
        writer.WritePropertyName("xRateLimit");
        response.XRateLimitHeader.WriteTo(writer);
        writer.WritePropertyName("xRequestId");
        response.XRequestIdHeader.WriteTo(writer);
        writer.WritePropertyName("xTags");
        response.XTagsHeader.WriteTo(writer);
        writer.WritePropertyName("xExpiresAt");
        response.XExpiresAtHeader.WriteTo(writer);
        writer.WritePropertyName("xScope");
        response.XScopeHeader.WriteTo(writer);
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private static async Task<string> CaptureAsync(
        string name,
        string operation,
        int status,
        string bodyUtf8,
        Dictionary<string, string>? headers,
        Func<HttpClientTransport, Corvus.Text.Json.Utf8JsonWriter, Task> decode)
    {
        using var harness = new TestHarness((HttpStatusCode)status, bodyUtf8, headers);

        // Emit the whole case object into a canonical (non-indented) JSON string so both the emit
        // path and the assert path produce byte-identical values for the same decoded result. The
        // Corvus byte-native writer is used so the generated model WriteTo(...) calls in the
        // decoders bind to it directly (C# is the reference for each header value's JSON form).
        using var buffer = new MemoryStream();
        var writerOptions = new Corvus.Text.Json.JsonWriterOptions
        {
            Indented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        var writer = new Corvus.Text.Json.Utf8JsonWriter(buffer, writerOptions);
        try
        {
            writer.WriteStartObject();
            writer.WriteString("name", name);
            writer.WriteString("operation", operation);
            writer.WriteNumber("status", status);
            writer.WriteString("bodyUtf8", bodyUtf8);

            writer.WriteStartObject("headers");
            if (headers is not null)
            {
                foreach (KeyValuePair<string, string> header in headers.OrderBy(static h => h.Key, StringComparer.Ordinal))
                {
                    writer.WriteString(header.Key, header.Value);
                }
            }

            writer.WriteEndObject();

            writer.WritePropertyName("decoded");
            await decode(harness.Transport, writer);

            writer.WriteEndObject();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string ResolveFixturePath()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs", "typescript", "openapi-examples")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("Could not locate the docs/typescript/openapi-examples directory from the test output location.");
        }

        return Path.Combine(dir.FullName, "docs", "typescript", "openapi-examples", "parity", "response-fixture.json");
    }

    private static void WriteFixture(string fixturePath, string[] caseJson)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)!);

        using var buffer = new MemoryStream();
        var writerOptions = new System.Text.Json.JsonWriterOptions
        {
            Indented = true,
            IndentSize = 2,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer, writerOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("spec", SpecName);
            writer.WriteStartArray("cases");
            foreach (string json in caseJson)
            {
                using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);
                doc.RootElement.WriteTo(writer);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        buffer.WriteByte((byte)'\n');
        File.WriteAllBytes(fixturePath, buffer.ToArray());
    }

    private static string[] LoadFixtureCases(string fixturePath)
    {
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"Response fixture not found at '{fixturePath}'. Run once with UPDATE_PARITY_FIXTURE=1 to emit it.");

        byte[] json = File.ReadAllBytes(fixturePath);
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
        System.Text.Json.JsonElement root = document.RootElement;

        Assert.AreEqual(SpecName, root.GetProperty("spec").GetString(), "Fixture spec name mismatch.");

        List<string> result = [];
        foreach (System.Text.Json.JsonElement element in root.GetProperty("cases").EnumerateArray())
        {
            // Re-serialize each fixture case canonically (non-indented) so it can be compared
            // byte-for-byte against the freshly decoded, canonically-serialized case.
            using var buffer = new MemoryStream();
            var writerOptions = new System.Text.Json.JsonWriterOptions
            {
                Indented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
            using (var writer = new System.Text.Json.Utf8JsonWriter(buffer, writerOptions))
            {
                element.WriteTo(writer);
            }

            result.Add(Encoding.UTF8.GetString(buffer.ToArray()));
        }

        return [.. result];
    }

    /// <summary>
    /// Encapsulates a mock HTTP handler, HttpClient, and HttpClientTransport for testing.
    /// The handler captures the outgoing request and returns a canned status, body, and headers.
    /// </summary>
    private sealed class TestHarness : IDisposable
    {
        private readonly MockHandler handler;
        private readonly HttpClient client;

        public TestHarness(
            HttpStatusCode statusCode,
            string responseBody,
            Dictionary<string, string>? responseHeaders)
        {
            this.handler = new MockHandler(statusCode, responseBody, responseHeaders);
            this.client = new HttpClient(this.handler)
            {
                BaseAddress = new Uri("http://localhost"),
            };
            this.Transport = new HttpClientTransport(this.client);
        }

        public HttpClientTransport Transport { get; }

        public void Dispose()
        {
            this.Transport.DisposeAsync().AsTask().GetAwaiter().GetResult();
            this.client.Dispose();
            this.handler.Dispose();
        }
    }

    /// <summary>
    /// A <see cref="DelegatingHandler"/> that returns a preconfigured response with the given
    /// status code, body, and response headers.
    /// </summary>
    private sealed class MockHandler : DelegatingHandler
    {
        private readonly HttpStatusCode statusCode;
        private readonly string responseBody;
        private readonly Dictionary<string, string>? responseHeaders;

        public MockHandler(
            HttpStatusCode statusCode,
            string responseBody,
            Dictionary<string, string>? responseHeaders)
        {
            this.statusCode = statusCode;
            this.responseBody = responseBody;
            this.responseHeaders = responseHeaders;
            this.InnerHandler = new HttpClientHandler();
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            }

            HttpContent content = string.IsNullOrEmpty(this.responseBody)
                ? new ByteArrayContent([])
                : new StringContent(this.responseBody, Encoding.UTF8, "application/json");

            var response = new HttpResponseMessage(this.statusCode) { Content = content };

            if (this.responseHeaders is not null)
            {
                foreach (KeyValuePair<string, string> header in this.responseHeaders)
                {
                    response.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            return response;
        }
    }
}