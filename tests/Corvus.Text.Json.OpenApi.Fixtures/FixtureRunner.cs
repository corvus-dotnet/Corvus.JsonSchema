// <copyright file="FixtureRunner.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net.Http.Headers;
using System.Text;
using Corvus.Text.Json.Canonicalization;

namespace Corvus.Text.Json.OpenApi.Fixtures;

/// <summary>
/// Runs one case of a fixture file against a generated client through its fixture adapter.
/// </summary>
public static class FixtureRunner
{
    /// <summary>
    /// The generated adapter's entry point.
    /// </summary>
    /// <param name="operationId">The operation to invoke.</param>
    /// <param name="input">The case's <c>input</c>, or an undefined element when it has none.</param>
    /// <param name="transport">The transport to send through.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome as JSON.</returns>
    public delegate ValueTask<string> Invoke(string operationId, JsonElement input, IApiTransport transport, CancellationToken cancellationToken);

    /// <summary>
    /// Runs a case.
    /// </summary>
    /// <param name="fixture">The root of the fixture file.</param>
    /// <param name="caseIndex">The index of the case in <c>cases</c>.</param>
    /// <param name="invoke">The generated adapter.</param>
    /// <param name="language">The language under test, for <c>skip</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the case has passed.</returns>
    /// <exception cref="FixtureSkippedException">The case is skipped for the language.</exception>
    /// <exception cref="FixtureAssertionException">An expectation was not met.</exception>
    public static async Task RunAsync(JsonElement fixture, int caseIndex, Invoke invoke, string language = "csharp", CancellationToken cancellationToken = default)
    {
        string operationId = fixture.GetProperty("operationId").GetString()!;
        JsonElement testCase = fixture.GetProperty("cases")[caseIndex];

        if (testCase.TryGetProperty("skip", out JsonElement skip) && skip.ValueKind == JsonValueKind.Object && skip.TryGetProperty(language, out JsonElement reason))
        {
            throw new FixtureSkippedException(reason.GetString() ?? "skipped");
        }

        JsonElement input = testCase.TryGetProperty("input", out JsonElement inputElement) ? inputElement : default;
        using FixtureTransport transport = FixtureTransport.Create(testCase.GetProperty("response"));

        string resultJson = await invoke(operationId, input, transport.Transport, cancellationToken).ConfigureAwait(false);
        using ParsedJsonDocument<JsonElement> resultDocument = ParsedJsonDocument<JsonElement>.Parse(resultJson);
        JsonElement result = resultDocument.RootElement;

        if (result.TryGetProperty("kind", out JsonElement kind) && kind.GetString() == "unsupported")
        {
            throw new FixtureSkippedException("adapter: " + result.GetProperty("reason").GetString());
        }

        if (testCase.TryGetProperty("expectRequest", out JsonElement expectRequest))
        {
            AssertRequest(expectRequest, transport);
        }

        AssertOutcome(testCase.GetProperty("expect"), result);
    }

    private static void AssertRequest(JsonElement expected, FixtureTransport transport)
    {
        HttpRequestMessage request = transport.CapturedRequest ?? throw new FixtureAssertionException("the client sent no request");

        if (expected.TryGetProperty("method", out JsonElement method))
        {
            Equal("request method", method.GetString()!, request.Method.Method);
        }

        string pathAndQuery = SentPathAndQuery(request.RequestUri!);
        if (expected.TryGetProperty("path", out JsonElement path))
        {
            Equal("request path", path.GetString()!, pathAndQuery);
        }

        if (expected.TryGetProperty("pathAnyOf", out JsonElement paths))
        {
            bool any = false;
            foreach (JsonElement candidate in paths.EnumerateArray())
            {
                any |= candidate.GetString() == pathAndQuery;
            }

            if (!any)
            {
                throw new FixtureAssertionException($"request path '{pathAndQuery}' is none of the expected alternatives");
            }
        }

        if (expected.TryGetProperty("headers", out JsonElement headers))
        {
            foreach (JsonProperty<JsonElement> header in headers.EnumerateObject())
            {
                string? actual = HeaderValue(request, header.Name);
                if (actual is null)
                {
                    throw new FixtureAssertionException($"request header '{header.Name}' is absent; expected '{header.Value.GetString()}'");
                }

                Equal($"request header '{header.Name}'", header.Value.GetString()!, actual);
            }
        }

        if (expected.TryGetProperty("absentHeaders", out JsonElement absent))
        {
            foreach (JsonElement name in absent.EnumerateArray())
            {
                if (HeaderValue(request, name.GetString()!) is not null)
                {
                    throw new FixtureAssertionException($"request header '{name.GetString()}' is present but must be absent");
                }
            }
        }

        if (expected.TryGetProperty("contentType", out JsonElement contentType))
        {
            Equal("request content type", contentType.GetString()!, request.Content?.Headers.ContentType?.MediaType ?? "(none)");
        }

        if (expected.TryGetProperty("noBody", out JsonElement noBody) && noBody.ValueKind == JsonValueKind.True && transport.CapturedBody is { Length: > 0 })
        {
            throw new FixtureAssertionException("the client sent a body but none was expected");
        }

        if (expected.TryGetProperty("body", out JsonElement body))
        {
            byte[] captured = transport.CapturedBody ?? throw new FixtureAssertionException("the client sent no body");
            using ParsedJsonDocument<JsonElement> capturedDocument = ParseJson(captured, "request body");
            Equal("request body (canonical JSON)", Canonical(body), Canonical(capturedDocument.RootElement));
        }

        if (expected.TryGetProperty("bodyText", out JsonElement bodyText))
        {
            Equal("request body text", bodyText.GetString()!, Encoding.UTF8.GetString(transport.CapturedBody ?? []));
        }

        if (expected.TryGetProperty("bodyForm", out JsonElement bodyForm))
        {
            Equal("request form body", bodyForm.GetString()!, Encoding.UTF8.GetString(transport.CapturedBody ?? []));
        }

        if (expected.TryGetProperty("bodyBase64", out JsonElement bodyBase64))
        {
            Equal("request body bytes", bodyBase64.GetString()!, Convert.ToBase64String(transport.CapturedBody ?? []));
        }
    }

    private static void AssertOutcome(JsonElement expected, JsonElement result)
    {
        string expectedKind = expected.GetProperty("kind").GetString()!;
        string actualKind = result.GetProperty("kind").GetString()!;
        if (expectedKind != actualKind)
        {
            string detail = result.TryGetProperty("error", out JsonElement error) ? " (" + error.GetString() + ")" : string.Empty;
            throw new FixtureAssertionException($"outcome kind: expected '{expectedKind}', got '{actualKind}'{detail}");
        }

        if (expected.TryGetProperty("status", out JsonElement status))
        {
            Equal("status", status.GetInt32().ToString(), result.GetProperty("status").GetInt32().ToString());
        }

        if (expected.TryGetProperty("case", out JsonElement caseName))
        {
            Equal("matched case", caseName.GetString()!, result.TryGetProperty("case", out JsonElement actualCase) ? actualCase.GetString()! : "(none)");
        }
        else if (expectedKind == "ok" && result.TryGetProperty("case", out JsonElement unexpectedCase))
        {
            throw new FixtureAssertionException($"no response case was expected to match, but '{unexpectedCase.GetString()}' did");
        }

        if (expected.TryGetProperty("body", out JsonElement body))
        {
            if (!result.TryGetProperty("body", out JsonElement actualBody))
            {
                throw new FixtureAssertionException("the outcome has no body");
            }

            Equal("body (canonical JSON)", Canonical(body), Canonical(actualBody));
        }

        if (expected.TryGetProperty("bodyText", out JsonElement bodyText))
        {
            Equal("body text", bodyText.GetString()!, result.TryGetProperty("bodyText", out JsonElement actualText) ? actualText.GetString()! : "(none)");
        }

        if (expected.TryGetProperty("headers", out JsonElement headers))
        {
            result.TryGetProperty("headers", out JsonElement actualHeaders);
            foreach (JsonProperty<JsonElement> header in headers.EnumerateObject())
            {
                string headerName = header.Name.ToLowerInvariant();
                bool present = actualHeaders.ValueKind == JsonValueKind.Object && actualHeaders.TryGetProperty(headerName, out JsonElement actual) && actual.ValueKind != JsonValueKind.Null;
                if (header.Value.ValueKind == JsonValueKind.Null)
                {
                    if (present)
                    {
                        throw new FixtureAssertionException($"response header '{header.Name}' was expected absent");
                    }

                    continue;
                }

                if (!present)
                {
                    throw new FixtureAssertionException($"response header '{header.Name}' is absent");
                }

                Equal($"response header '{header.Name}'", Canonical(header.Value), Canonical(actualHeaders.GetProperty(headerName)));
            }
        }

        if (expectedKind == "error")
        {
            string message = result.TryGetProperty("error", out JsonElement actualError) ? actualError.GetString()! : string.Empty;
            if (!expected.TryGetProperty("error", out JsonElement expectedError) || !expectedError.TryGetProperty("contains", out JsonElement contains))
            {
                throw new FixtureAssertionException($"an error case must give error.contains; the client reported '{message}'");
            }

            if (!message.Contains(contains.GetString()!, StringComparison.Ordinal))
            {
                throw new FixtureAssertionException($"error message '{message}' does not contain '{contains.GetString()}'");
            }
        }
    }

    /// <summary>
    /// The path and query exactly as the client wrote them. <see cref="Uri.PathAndQuery"/> is the
    /// normalised form (it escapes characters such as '|' and ' ', decodes escaped unreserved
    /// characters and collapses dot segments), which would let an over- or under-encoding client
    /// pass; the original string is what went into the request.
    /// </summary>
    private static string SentPathAndQuery(Uri requestUri)
    {
        string original = requestUri.OriginalString;
        if (!requestUri.IsAbsoluteUri)
        {
            return original;
        }

        int schemeEnd = original.IndexOf("://", StringComparison.Ordinal);
        int pathStart = schemeEnd < 0 ? -1 : original.IndexOf('/', schemeEnd + 3);
        return pathStart < 0 ? "/" : original[pathStart..];
    }

    private static ParsedJsonDocument<JsonElement> ParseJson(byte[] bytes, string what)
    {
        try
        {
            return ParsedJsonDocument<JsonElement>.Parse(bytes);
        }
        catch (JsonException ex)
        {
            throw new FixtureAssertionException($"{what} is not JSON ({ex.Message}): '{Encoding.UTF8.GetString(bytes)}'");
        }
    }

    private static string? HeaderValue(HttpRequestMessage request, string name)
    {
        if (request.Headers.TryGetValues(name, out IEnumerable<string>? values))
        {
            return string.Join(", ", values);
        }

        if (request.Content is { } content && content.Headers.TryGetValues(name, out IEnumerable<string>? contentValues))
        {
            return string.Join(", ", contentValues);
        }

        return null;
    }

    private static string Canonical(JsonElement element) => Encoding.UTF8.GetString(JsonCanonicalizer.Canonicalize(in element));

    private static void Equal(string what, string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new FixtureAssertionException($"{what}: expected '{expected}', got '{actual}'");
        }
    }
}