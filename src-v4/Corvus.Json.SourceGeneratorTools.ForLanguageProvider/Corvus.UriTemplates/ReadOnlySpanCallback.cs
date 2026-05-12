// <copyright file="ReadOnlySpanCallback.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.UriTemplates;

/// <summary>
/// A callback for receiving data from a <see cref="ReadOnlySpan{T}"/>.
/// </summary>
/// <typeparam name="TItem">The span item type.</typeparam>
/// <typeparam name="TState">The type of the state to pass.</typeparam>
/// <typeparam name="TResult">The return type.</typeparam>
/// <param name="state">An argument providing whatever state the callback requires.</param>
/// <param name="span">The span.</param>
/// <returns>Whatever information the callback wishes to return.</returns>
public delegate TResult ReadOnlySpanCallback<TItem, TState, TResult>(TState state, ReadOnlySpan<TItem> span);