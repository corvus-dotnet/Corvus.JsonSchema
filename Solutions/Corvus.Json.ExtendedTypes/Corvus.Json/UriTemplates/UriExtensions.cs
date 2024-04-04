// <copyright file="UriExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// Derived from Tavis.UriTemplate https://github.com/tavis-software/Tavis.UriTemplates/blob/master/License.txt

using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Corvus.Json.Internal;

namespace Corvus.Json.UriTemplates;

/// <summary>
/// Uri extensions for URI templates.
/// </summary>
public static class UriExtensions
{
    private static readonly Regex UnreservedCharacters = new("([-A-Za-z0-9._~]*)=([^&]*)&?", RegexOptions.Compiled, TimeSpan.FromSeconds(1));       //// Unreserved characters: http://tools.ietf.org/html/rfc3986#section-2.3

    /// <summary>
    /// Make a template from a URI and its query string parameters.
    /// </summary>
    /// <param name="uri">The uri from which to make a template.</param>
    /// <returns>The UriTemplate built from the URI and its query string parameters.</returns>
    public static UriTemplate MakeTemplate(this Uri uri)
    {
        ImmutableDictionary<string, JsonAny> parameters = uri.GetQueryStringParameters();
        return uri.MakeTemplate(parameters);
    }

    /// <summary>
    /// Make a template from a URI and a given set of parameters to use as a query string.
    /// </summary>
    /// <param name="uri">The uri from which to make a template.</param>
    /// <param name="parameters">The parameters to apply in a query string.</param>
    /// <returns>The URI template with the corresponding query string parameters.</returns>
    /// <remarks>It is expected the parameters for the query string have already been exploded if appropriate.</remarks>
    public static UriTemplate MakeTemplate(this Uri uri, params (string Key, JsonAny Value)[] parameters)
    {
        return MakeTemplate(uri, parameters.ToImmutableDictionary(p => p.Key, p => p.Value));
    }

    /// <summary>
    /// Make a template from a URI and a given set of parameters to use as a query string.
    /// </summary>
    /// <param name="uri">The uri from which to make a template.</param>
    /// <param name="parameters">The parameters to apply in a query string.</param>
    /// <returns>The URI template with the corresponding query string parameters.</returns>
    /// <remarks>It is expected the parameters for the query string have already been exploded if appropriate.</remarks>
    public static UriTemplate MakeTemplate(this Uri uri, ImmutableDictionary<string, JsonAny> parameters)
    {
        string target = uri.GetComponents(
            UriComponents.AbsoluteUri
            & ~UriComponents.Query
            & ~UriComponents.Fragment,
            UriFormat.Unescaped);

        StringBuilder sb = StringBuilderPool.Shared.Get();
        try
        {
            sb.Append(target);
            sb.Append("{?");
            bool first = true;

            foreach (string name in parameters.Keys)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    sb.Append(',');
                }

                sb.Append(name);
            }

            sb.Append('}');

            var template = new UriTemplate(sb.ToString(), false, parameters, null);

            return template;
        }
        finally
        {
            StringBuilderPool.Shared.Return(sb);
        }
    }

    /// <summary>
    /// Gets the query string parameters from the given URI.
    /// </summary>
    /// <param name="target">The target URI.</param>
    /// <returns>A dictionary of query string parameters.</returns>
    public static ImmutableDictionary<string, JsonAny> GetQueryStringParameters(this Uri target)
    {
        Uri uri = target;
        ImmutableDictionary<string, JsonAny>.Builder? parameters = ImmutableDictionary.CreateBuilder<string, JsonAny>();

        foreach (Match m in UnreservedCharacters.Matches(uri.Query).Cast<Match>())
        {
            string key = m.Groups[1].Value.ToLowerInvariant();
            string value = m.Groups[2].Value;
            parameters.Add(key, ParseUriValue(value));
        }

        return parameters.ToImmutable();
    }

    /// <summary>
    /// Parses a naked value from a URI string.
    /// </summary>
    /// <param name="value">The value to parse.</param>
    /// <returns>A <see cref="JsonAny"/> instance representing the value.</returns>
    /// <remarks>Note that this only applies to <c>null</c>, <c>bool</c>, <c>number</c> and <c>string</c> types.</remarks>
    internal static JsonAny ParseUriValue(string value)
    {
        if (value == "null")
        {
            return JsonAny.Null;
        }

        if (bool.TryParse(value, out bool boolResult))
        {
            return new(boolResult);
        }

        if (double.TryParse(value, out double doubleResult))
        {
            return new(new BinaryJsonNumber(doubleResult));
        }

        if (decimal.TryParse(value, out decimal decimalResult))
        {
            return new(new BinaryJsonNumber(decimalResult));
        }

        return new(value);
    }
}