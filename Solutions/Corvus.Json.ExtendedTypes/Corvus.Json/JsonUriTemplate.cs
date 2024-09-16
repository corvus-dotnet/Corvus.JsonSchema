// <copyright file="JsonUriTemplate.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
#if NET8_0_OR_GREATER
using System.Collections.Immutable;
using System.Text.Json;
using Corvus.Json.Internal;
using Corvus.Json.UriTemplates;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON UriTemplate.
/// </summary>
public readonly partial struct JsonUriTemplate : IJsonString<JsonUriTemplate>
{
    /// <summary>
    /// Gets the value as a <see cref="UriTemplate"/>.
    /// </summary>
    /// <param name="result">The value as a UriTemplate.</param>
    /// <param name="resolvePartially">Whether to allow partial resolution.</param>
    /// <param name="caseInsensitiveParameterNames">Whether to use case insensitive parameter names.</param>
    /// <param name="createParameterRegex">Whether to create a parameter regex (defaults to true).</param>
    /// <param name="parameters">The parameter values with which to initialize the template.</param>
    /// <returns><c>True</c> if the value could be retrieved.</returns>
    public bool TryGetUriTemplate(out UriTemplate result, bool resolvePartially = false, bool caseInsensitiveParameterNames = false, bool createParameterRegex = true, ImmutableDictionary<string, JsonAny>? parameters = null)
    {
        if ((this.backing & Backing.String) != 0)
        {
            result = new UriTemplate(this.stringBacking, resolvePartially, caseInsensitiveParameterNames, createParameterRegex, parameters);
            return true;
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            string? str = this.jsonElementBacking.GetString();
            if (str is not null)
            {
                result = new UriTemplate(str, resolvePartially, caseInsensitiveParameterNames, createParameterRegex, parameters);
                return true;
            }
        }

        result = default;
        return false;
    }
}
#endif