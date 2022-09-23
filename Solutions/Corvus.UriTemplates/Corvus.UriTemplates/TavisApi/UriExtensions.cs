// <copyright file="UriExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from Tavis.UriTemplate https://github.com/tavis-software/Tavis.UriTemplates/blob/master/License.txt
// </licensing>

using System.Text.RegularExpressions;

namespace Corvus.UriTemplates.TavisApi;

/// <summary>
/// Extension methods for converting a URI into a URI template.
/// </summary>
public static partial class UriExtensions
{
    /// <summary>
    /// Make a template from a URI, by templatizing the existing query string parameters.
    /// </summary>
    /// <param name="uri">The URI for which to make a template.</param>
    /// <returns>The URI template, with templatized query string, and parameters populated from the values in the query string.</returns>
    public static UriTemplate MakeTemplate(this Uri uri)
    {
        Dictionary<string, object?> parameters = uri.GetQueryStringParameters();
        return uri.MakeTemplate(parameters);
    }

    /// <summary>
    /// Make a template from a URI and an ordered set of parameters, removing the query string and fragment,
    /// and replacing the query with the parameter names from the dictionary.
    /// </summary>
    /// <param name="uri">The base URI.</param>
    /// <param name="parameters">The parameters to apply.</param>
    /// <returns>The URI template, with templatized query string, and parameters populated from the values provided.</returns>
    public static UriTemplate MakeTemplate(this Uri uri, IDictionary<string, object?> parameters)
    {
        string target = uri.GetComponents(
            UriComponents.AbsoluteUri
            & ~UriComponents.Query
            & ~UriComponents.Fragment,
            UriFormat.Unescaped);
        var template = new UriTemplate(target + "{?" + string.Join(",", parameters.Keys.ToArray()) + "}");
        template.AddParameters(parameters);

        return template;
    }

    /// <summary>
    /// Get the query sstring parameters from the given URI.
    /// </summary>
    /// <param name="target">The target URI for which to recover the query string parameters.</param>
    /// <returns>A map of the query string parameters.</returns>
    public static Dictionary<string, object?> GetQueryStringParameters(this Uri target)
    {
        Uri uri = target;
        var parameters = new Dictionary<string, object?>();

        Regex reg = UnreservedCharacterMatcher();

        foreach (Match m in reg.Matches(uri.Query))
        {
            string key = m.Groups[1].Value.ToLowerInvariant();
            string value = m.Groups[2].Value;
            parameters.Add(key, value);
        }

        return parameters;
    }

    // Unreserved characters: http://tools.ietf.org/html/rfc3986#section-2.3
    [GeneratedRegex("([-A-Za-z0-9._~]*)=([^&]*)&?")]
    private static partial Regex UnreservedCharacterMatcher();
}