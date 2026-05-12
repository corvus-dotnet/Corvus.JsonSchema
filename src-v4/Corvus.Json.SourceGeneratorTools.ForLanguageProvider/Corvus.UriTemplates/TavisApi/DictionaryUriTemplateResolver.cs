// <copyright file="DictionaryUriTemplateResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;

using Corvus.HighPerformance;

namespace Corvus.UriTemplates.TavisApi;

/// <summary>
/// A wrapper around <see cref="UriTemplateResolver{TParameterProvider, TParameterPayload}"/>
/// for a <see cref="DictionaryTemplateParameterProvider"/>.
/// </summary>
internal static class DictionaryUriTemplateResolver
{
    private static readonly Dictionary<string, object?> EmptyDictionary = new();
    private static readonly DictionaryTemplateParameterProvider ParameterProvider = new();

    /// <summary>
    /// Resolve the template into an output result.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to the callback.</typeparam>
    /// <param name="template">The template to resolve.</param>
    /// <param name="resolvePartially">If <see langword="true"/> then partially resolve the result.</param>
    /// <param name="parameters">The parameters to apply to the template.</param>
    /// <param name="parameterNameCallback">An optional callback which is provided each parameter name as they are discovered.</param>
    /// <param name="callback">The callback which is provided with the resolved template.</param>
    /// <param name="state">The state passed to the callback(s).</param>
    /// <returns><see langword="true"/> if the URI matched the template, and the parameters were resolved successfully.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryResolveResult<TState>(ReadOnlySpan<char> template, bool resolvePartially, in IDictionary<string, object?> parameters, ParameterNameCallback<TState> parameterNameCallback, ResolvedUriTemplateCallback<TState> callback, ref TState state)
    {
        return UriTemplateResolver<DictionaryTemplateParameterProvider, IDictionary<string, object?>>.TryResolveResult(ParameterProvider, template, resolvePartially, parameters, callback, parameterNameCallback, ref state);
    }

    /// <summary>
    /// Resolve the template into an output result.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to the callback.</typeparam>
    /// <param name="template">The template to resolve.</param>
    /// <param name="resolvePartially">If <see langword="true"/> then partially resolve the result.</param>
    /// <param name="parameters">The parameters to apply to the template.</param>
    /// <param name="callback">The callback which is provided with the resolved template.</param>
    /// <param name="state">The state passed to the callback(s).</param>
    /// <returns><see langword="true"/> if the URI matched the template, and the parameters were resolved successfully.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryResolveResult<TState>(ReadOnlySpan<char> template, bool resolvePartially, in IDictionary<string, object?> parameters, ResolvedUriTemplateCallback<TState> callback, ref TState state)
    {
        return UriTemplateResolver<DictionaryTemplateParameterProvider, IDictionary<string, object?>>.TryResolveResult(ParameterProvider, template, resolvePartially, parameters, callback, null, ref state);
    }

    /// <summary>
    /// Resolve the template into an output result.
    /// </summary>
    /// <param name="template">The template to resolve.</param>
    /// <param name="output">The output buffer into which to resolve the template.</param>
    /// <param name="resolvePartially">If <see langword="true"/> then partially resolve the result.</param>
    /// <param name="parameters">The parameters to apply to the template.</param>
    /// <returns><see langword="true"/> if the URI matched the template, and the parameters were resolved successfully.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryResolveResult(ReadOnlySpan<char> template, ref ValueStringBuilder output, bool resolvePartially, in IDictionary<string, object?> parameters)
    {
        object? nullState = default;
        return UriTemplateResolver<DictionaryTemplateParameterProvider, IDictionary<string, object?>>.TryResolveResult(ParameterProvider, template, ref output, resolvePartially, parameters, null, ref nullState);
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
        return UriTemplateResolver<DictionaryTemplateParameterProvider, IDictionary<string, object?>>.TryResolveResult(ParameterProvider, template, true, EmptyDictionary, Nop, callback, ref state);

#pragma warning disable RCS1163 // Unused parameter.
        static void Nop(ReadOnlySpan<char> value, ref TState state)
        {
#pragma warning restore RCS1163 // Unused parameter.
        }
    }
}