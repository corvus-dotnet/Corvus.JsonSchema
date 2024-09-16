// <copyright file="Matcher.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// A callback for a pattern match method.
/// </summary>
/// <typeparam name="TMatch">The type that was matched.</typeparam>
/// <typeparam name="TContext">The context of the match.</typeparam>
/// <typeparam name="TOut">The result of the match operation.</typeparam>
/// <param name="match">The matched value.</param>
/// <param name="context">The context for the match operation.</param>
/// <returns>The result of processing the match.</returns>
public delegate TOut Matcher<TMatch, TContext, TOut>(in TMatch match, in TContext context)
    where TMatch : struct, IJsonValue<TMatch>;

/// <summary>
/// A callback for a pattern match method.
/// </summary>
/// <typeparam name="TMatch">The type that was matched.</typeparam>
/// <typeparam name="TOut">The result of the match operation.</typeparam>
/// <param name="match">The matched value.</param>
/// <returns>The result of processing the match.</returns>
public delegate TOut Matcher<TMatch, TOut>(in TMatch match)
    where TMatch : struct, IJsonValue<TMatch>;