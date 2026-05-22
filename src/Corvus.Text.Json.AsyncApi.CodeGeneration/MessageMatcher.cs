// <copyright file="MessageMatcher.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// A callback for a message match method.
/// </summary>
/// <typeparam name="TPayload">The type of the message payload.</typeparam>
/// <typeparam name="TResult">The result of the match operation.</typeparam>
/// <param name="payload">The message payload.</param>
/// <returns>The result of processing the match.</returns>
public delegate TResult MessageMatcher<TPayload, TResult>(TPayload payload);

/// <summary>
/// A callback for a message match method with context.
/// </summary>
/// <typeparam name="TPayload">The type of the message payload.</typeparam>
/// <typeparam name="TContext">The context of the match.</typeparam>
/// <typeparam name="TResult">The result of the match operation.</typeparam>
/// <param name="payload">The message payload.</param>
/// <param name="context">The context for the match operation.</param>
/// <returns>The result of processing the match.</returns>
public delegate TResult MessageMatcher<TPayload, TContext, TResult>(TPayload payload, in TContext context)
    where TContext : allows ref struct;