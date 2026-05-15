// <copyright file="ResponseMatcher.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// A callback for a response match method.
/// </summary>
/// <typeparam name="TBody">The type of the response body.</typeparam>
/// <typeparam name="TResult">The result of the match operation.</typeparam>
/// <param name="body">The response body.</param>
/// <returns>The result of processing the match.</returns>
public delegate TResult ResponseMatcher<TBody, TResult>(TBody body);

/// <summary>
/// A callback for a response match method with context.
/// </summary>
/// <typeparam name="TBody">The type of the response body.</typeparam>
/// <typeparam name="TContext">The context of the match.</typeparam>
/// <typeparam name="TResult">The result of the match operation.</typeparam>
/// <param name="body">The response body.</param>
/// <param name="context">The context for the match operation.</param>
/// <returns>The result of processing the match.</returns>
public delegate TResult ResponseMatcher<TBody, TContext, TResult>(TBody body, in TContext context)
    where TContext : allows ref struct;