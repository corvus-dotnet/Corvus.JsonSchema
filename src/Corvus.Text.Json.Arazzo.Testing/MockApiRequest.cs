// <copyright file="MockApiRequest.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Testing;

/// <summary>
/// A request observed by <see cref="MockApiTransport"/> — the executed method and resolved path.
/// Used to assert a workflow's call path.
/// </summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The resolved path (with path parameters substituted and any query string appended).</param>
public readonly record struct MockApiRequest(OperationMethod Method, string Path);