// <copyright file="StubHttpMessageHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Directories.Conformance;

/// <summary>
/// A test <see cref="HttpMessageHandler"/> that answers each request from a supplied responder, with no network — the
/// shared mock-HTTP seam for the SaaS <c>IPrincipalDirectory</c> adapters (SCIM, Entra/Graph, Okta, Google), each of which
/// accepts an injected <see cref="HttpClient"/>. The responder inspects the request (method, URI, query, headers) and
/// returns the provider-shaped JSON, so the shared <see cref="PrincipalDirectoryConformance"/> runs against a real adapter
/// driving real request-building and response-parsing — only the wire is faked.
/// </summary>
/// <param name="responder">Builds the response for a request.</param>
public sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(responder(request));

    /// <summary>Builds a JSON response with the given body and status.</summary>
    /// <param name="json">The response body.</param>
    /// <param name="contentType">The media type (e.g. <c>application/scim+json</c>, <c>application/json</c>).</param>
    /// <param name="status">The status code (default <see cref="HttpStatusCode.OK"/>).</param>
    /// <returns>The response message.</returns>
    public static HttpResponseMessage Json(string json, string contentType = "application/json", HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, contentType) };
}