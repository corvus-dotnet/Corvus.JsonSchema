// <copyright file="RecordedApiExchange.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// One metadata-only API exchange a <see cref="RecordingApiTransport"/> observed on a host-executed run
/// (workflow-designer design §18 slice 3e-2a): the request's HTTP method paired with its resolved pre-auth
/// path and the response status code. It carries <em>no</em> request or response body and no headers — the
/// ratified §18 body posture, where bodies are a later per-environment opt-in — so the record can leave the
/// runner without exposing any secret- or payload-bearing data.
/// </summary>
/// <param name="Method">The HTTP method of the request.</param>
/// <param name="Path">The resolved request path (path parameters substituted, query included), composed from
/// the generated request itself before the inner transport applies authentication — never a signed or
/// credential-bearing URL.</param>
/// <param name="StatusCode">The response status code the inner transport returned.</param>
public readonly record struct RecordedApiExchange(OperationMethod Method, string Path, int StatusCode);