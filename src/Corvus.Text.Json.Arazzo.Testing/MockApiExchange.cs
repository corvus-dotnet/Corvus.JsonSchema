// <copyright file="MockApiExchange.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Testing;

/// <summary>
/// One request/response exchange observed by <see cref="MockApiTransport"/>: the request's method and
/// resolved path paired with the scripted response that served it. The simulation trace's exchange
/// records come straight from these.
/// </summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The resolved request path (parameters substituted, query included).</param>
/// <param name="StatusCode">The scripted response status.</param>
/// <param name="ResponseBody">The scripted response body bytes (empty for a bodiless response).</param>
/// <param name="ContentType">The scripted response content type.</param>
/// <param name="RequestBody">The request body AS SENT (expressions resolved), when the call carried a
/// typed JSON body — the debugger's "what did this step actually send". Empty otherwise.</param>
public readonly record struct MockApiExchange(
    OperationMethod Method,
    string Path,
    int StatusCode,
    ReadOnlyMemory<byte> ResponseBody,
    string ContentType,
    ReadOnlyMemory<byte> RequestBody = default);