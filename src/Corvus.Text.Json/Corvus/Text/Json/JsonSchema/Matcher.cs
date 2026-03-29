// <copyright file="Matcher.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

/// <summary>
/// A callback for a pattern match method.
/// </summary>
/// <typeparam name="TMatch">The type that was matched.</typeparam>
/// <typeparam name="TContext">The context of the match.</typeparam>
/// <typeparam name="TResult">The result of the match operation.</typeparam>
/// <param name="match">The matched value.</param>
/// <param name="context">The context for the match operation.</param>
/// <returns>The result of processing the match.</returns>
[CLSCompliant(false)]
public delegate TResult Matcher<TMatch, TContext, TResult>(in TMatch match, in TContext context)
    where TMatch : struct, IJsonElement<TMatch>
#if NET9_0_OR_GREATER
    where TContext : allows ref struct
#endif
    ;

/// <summary>
/// A callback for a pattern match method.
/// </summary>
/// <typeparam name="TMatch">The type that was matched.</typeparam>
/// <typeparam name="TOut">The result of the match operation.</typeparam>
/// <param name="match">The matched value.</param>
/// <returns>The result of processing the match.</returns>
[CLSCompliant(false)]
public delegate TOut Matcher<TMatch, TOut>(in TMatch match)
    where TMatch : struct, IJsonElement<TMatch>;