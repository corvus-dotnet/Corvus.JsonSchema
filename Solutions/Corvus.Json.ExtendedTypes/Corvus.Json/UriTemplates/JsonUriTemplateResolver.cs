﻿// <copyright file="JsonUriTemplateResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using Corvus.HighPerformance;
using Corvus.UriTemplates;

namespace Corvus.Json.UriTemplates;

/// <summary>
/// A wrapper around <see cref="UriTemplateResolver{TParameterProvider, TParameterPayload}"/>
/// for a <see cref="JsonTemplateParameterProvider{TPayload}"/>.
/// </summary>
public static class JsonUriTemplateResolver
{
    /// <summary>
    /// Resolve the template into an output result.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to the callback.</typeparam>
    /// <typeparam name="T">The type of the Json parameters.</typeparam>
    /// <param name="template">The template to resolve.</param>
    /// <param name="resolvePartially">If <see langword="true"/> then partially resolve the result.</param>
    /// <param name="parameters">The parameters to apply to the template.</param>
    /// <param name="parameterNameCallback">An optional callback which is provided each parameter name as they are discovered.</param>
    /// <param name="callback">The callback which is provided with the resolved template.</param>
    /// <param name="state">The state passed to the callback(s).</param>
    /// <returns><see langword="true"/> if the URI matched the template, and the parameters were resolved successfully.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryResolveResult<TState, T>(ReadOnlySpan<char> template, bool resolvePartially, in T parameters, ParameterNameCallback<TState>? parameterNameCallback, ResolvedUriTemplateCallback<TState> callback, ref TState state)
        where T : struct, IJsonObject<T>
    {
        return UriTemplateResolver<JsonTemplateParameterProvider<T>, T>.TryResolveResult(JsonTemplateParameterProvider<T>.Instance, template, resolvePartially, parameters, callback, parameterNameCallback, ref state);
    }

    /// <summary>
    /// Resolve the template into an output result.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to the callback.</typeparam>
    /// <typeparam name="T">The type of the Json parameters.</typeparam>
    /// <param name="template">The template to resolve.</param>
    /// <param name="resolvePartially">If <see langword="true"/> then partially resolve the result.</param>
    /// <param name="parameters">The parameters to apply to the template.</param>
    /// <param name="callback">The callback which is provided with the resolved template.</param>
    /// <param name="state">The state passed to the callback(s).</param>
    /// <returns><see langword="true"/> if the URI matched the template, and the parameters were resolved successfully.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryResolveResult<TState, T>(ReadOnlySpan<char> template, bool resolvePartially, in T parameters, ResolvedUriTemplateCallback<TState> callback, ref TState state)
        where T : struct, IJsonObject<T>
    {
        return UriTemplateResolver<JsonTemplateParameterProvider<T>, T>.TryResolveResult(JsonTemplateParameterProvider<T>.Instance, template, resolvePartially, parameters, callback, null, ref state);
    }

    /// <summary>
    /// Resolve the template into an output result.
    /// </summary>
    /// <typeparam name="T">The type of the Json parameters.</typeparam>
    /// <param name="template">The template to resolve.</param>
    /// <param name="output">The output buffer into which to resolve the template.</param>
    /// <param name="resolvePartially">If <see langword="true"/> then partially resolve the result.</param>
    /// <param name="parameters">The parameters to apply to the template.</param>
    /// <returns><see langword="true"/> if the URI matched the template, and the parameters were resolved successfully.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryResolveResult<T>(ReadOnlySpan<char> template, ref ValueStringBuilder output, bool resolvePartially, in T parameters)
        where T : struct, IJsonObject<T>
    {
        object? nullState = default;
        return UriTemplateResolver<JsonTemplateParameterProvider<T>, T>.TryResolveResult(JsonTemplateParameterProvider<T>.Instance, template, ref output, resolvePartially, parameters, null, ref nullState);
    }

    /// <summary>
    /// Get the parameter names from the template.
    /// </summary>
    /// <typeparam name="TState">The type of the state for the callback.</typeparam>
    /// <param name="template">The template for the callback.</param>
    /// <param name="callback">The callback provided with the parameter names.</param>
    /// <param name="state">The state for the callback.</param>
    /// <returns><see langword="true"/> if the URI matched the template, and the parameters were resolved successfully.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetParameterNames<TState>(ReadOnlySpan<char> template, ParameterNameCallback<TState> callback, ref TState state)
    {
        return UriTemplateResolver<JsonTemplateParameterProvider<JsonObject>, JsonObject>.TryResolveResult(JsonTemplateParameterProvider<JsonObject>.Instance, template, true, JsonObject.Null, Nop, callback, ref state);

        static void Nop(ReadOnlySpan<char> value, ref TState state)
        {
        }
    }
}