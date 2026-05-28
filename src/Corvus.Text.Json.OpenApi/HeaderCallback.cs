// <copyright file="HeaderCallback.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// A callback invoked by <see cref="IApiRequest{TSelf}.WriteHeaders{TState}"/>
/// for each header to add to the request.
/// </summary>
/// <typeparam name="TState">The type of the state passed through to the callback.</typeparam>
/// <param name="name">The header name as a UTF-8 span.</param>
/// <param name="value">The header value as a UTF-8 span.</param>
/// <param name="state">The opaque state.</param>
public delegate void HeaderCallback<TState>(
    ReadOnlySpan<byte> name,
    ReadOnlySpan<byte> value,
    TState state);