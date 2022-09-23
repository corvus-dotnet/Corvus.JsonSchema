// <copyright file="DictionaryUriTemplateResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Corvus.UriTemplates.TavisApi;

/// <summary>
/// A wrapper around <see cref="UriTemplateResolver{TParameterProvider, TParameterPayload}"/>
/// for a <see cref="DictionaryTemplateParameterProvider"/>.
/// </summary>
public static class DictionaryUriTemplateResolver
{
    /// <summary>
    /// Resolve the template into an output result.
    /// </summary>
    /// <param name="template">The template to resolve.</param>
    /// <param name="resolvePartially">If <see langword="true"/> then partially resolve the result.</param>
    /// <param name="parameters">The parameters to apply to the template.</param>
    /// <param name="callback">The callback which is provided with the resolved template.</param>
    /// <param name="parameterNameCallback">An optional callback which is provided each parameter name as they are discovered.</param>
    /// <returns><see langword="true"/> if the URI matched the template, and the parameters were resolved successfully.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryResolveResult(ReadOnlySpan<char> template, bool resolvePartially, in IDictionary<string, object?> parameters, ResolvedUriTemplateCallback callback, ParameterNameCallback? parameterNameCallback = null)
    {
        return UriTemplateResolver<DictionaryTemplateParameterProvider, IDictionary<string, object?>>.TryResolveResult(template, resolvePartially, parameters, callback, parameterNameCallback);
    }

    /// <summary>
    /// Resolve the template into an output result.
    /// </summary>
    /// <param name="template">The template to resolve.</param>
    /// <param name="output">The output buffer into which to resolve the template.</param>
    /// <param name="resolvePartially">If <see langword="true"/> then partially resolve the result.</param>
    /// <param name="parameters">The parameters to apply to the template.</param>
    /// <param name="parameterNameCallback">An optional callback which is provided each parameter name as they are discovered.</param>
    /// <returns><see langword="true"/> if the URI matched the template, and the parameters were resolved successfully.</returns>
    public static bool TryResolveResult(ReadOnlySpan<char> template, IBufferWriter<char> output, bool resolvePartially, in IDictionary<string, object?> parameters, ParameterNameCallback? parameterNameCallback = null)
    {
        return UriTemplateResolver<DictionaryTemplateParameterProvider, IDictionary<string, object?>>.TryResolveResult(template, output, resolvePartially, parameters, parameterNameCallback);
    }
}