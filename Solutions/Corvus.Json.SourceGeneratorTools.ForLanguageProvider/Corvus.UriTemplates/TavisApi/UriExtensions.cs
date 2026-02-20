// <copyright file="UriExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from Tavis.UriTemplate https://github.com/tavis-software/Tavis.UriTemplates/blob/master/License.txt
// </licensing>

using System.Buffers;

namespace Corvus.UriTemplates.TavisApi;

/// <summary>
/// A delegate for recieving query string parameters.
/// </summary>
/// <typeparam name="TState">The type of the state for the callback.</typeparam>
/// <param name="name">The name of the parameter.</param>
/// <param name="value">The value of the parameter.</param>
/// <param name="state">The state for the callback.</param>
public delegate void QueryStringParameterCallback<TState>(ReadOnlySpan<char> name, ReadOnlySpan<char> value, ref TState state);

/// <summary>
/// Extension methods for converting a URI into a URI template.
/// </summary>
public static partial class UriExtensions
{
    private enum State
    {
        LookingForName,
        LookingForValue,
    }

    /// <summary>
    /// Make a template from a URI, by templatizing the existing query string parameters.
    /// </summary>
    /// <param name="uri">The URI for which to make a template.</param>
    /// <returns>The URI template, with templatized query string, and parameters populated from the values in the query string.</returns>
    public static UriTemplate MakeTemplate(this Uri uri)
    {
        Dictionary<string, object> parameters = uri.GetQueryStringParameters();
        return uri.MakeTemplate((IDictionary<string, object?>)parameters);
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
    /// Get the query string parameters from the given URI.
    /// </summary>
    /// <param name="target">The target URI for which to recover the query string parameters.</param>
    /// <returns>A map of the query string parameters.</returns>
    public static Dictionary<string, object> GetQueryStringParameters(this Uri target)
    {
        Dictionary<string, object> parameters = new();

        GetQueryStringParameters(target, AccumulateResults, ref parameters);

        return parameters;

        static void AccumulateResults(ReadOnlySpan<char> name, ReadOnlySpan<char> value, ref Dictionary<string, object> state)
        {
            state.Add(name.ToString(), value.ToString());
        }
    }

    /// <summary>
    /// Get the query string parameters from the given URI.
    /// </summary>
    /// <typeparam name="TState">The type of the state for the callback.</typeparam>
    /// <param name="target">The target URI for which to recover the query string parameters.</param>
    /// <param name="callback">The callback to receieve the query parameters.</param>
    /// <param name="state">The state for the callback.</param>
    public static void GetQueryStringParameters<TState>(this Uri target, QueryStringParameterCallback<TState> callback, ref TState state)
    {
#if NET8_0_OR_GREATER
        MatchQueryParameters(target.Query, callback, ref state);
#else
        MatchQueryParameters(target.Query.AsSpan(), callback, ref state);
#endif
    }

    private static bool MatchQueryParameters<TState>(ReadOnlySpan<char> query, QueryStringParameterCallback<TState> callback, ref TState state)
    {
        // Skip the initial '?'
        int currentIndex = 1;
        State currentState = State.LookingForName;
        int nameSegmentStart = 1;
        int nameSegmentEnd = 1;
        int valueSegmentStart = -1;

        while (currentIndex < query.Length)
        {
            switch (currentState)
            {
                case State.LookingForName:
                    if (query[currentIndex] == '=')
                    {
                        nameSegmentEnd = currentIndex;
                        valueSegmentStart = currentIndex + 1;
                        currentState = State.LookingForValue;

                        // That's an empty name
                        if (nameSegmentStart >= nameSegmentEnd)
                        {
                            return false;
                        }
                    }
                    else if (!IsPermittedValueCharacter(query[currentIndex]))
                    {
                        return false;
                    }

                    break;

                case State.LookingForValue:
                    if (query[currentIndex] == '&')
                    {
                        ReadOnlySpan<char> name = query[nameSegmentStart..nameSegmentEnd];
                        ReadOnlySpan<char> value = valueSegmentStart == currentIndex ? ReadOnlySpan<char>.Empty : query[valueSegmentStart..currentIndex];

                        ExecuteCallback(callback, name, value, ref state);

                        currentState = State.LookingForName;
                        nameSegmentStart = currentIndex + 1;
                    }

                    break;
            }

            ++currentIndex;
        }

        if (currentState == State.LookingForValue)
        {
            ExecuteCallback(callback, query[nameSegmentStart..nameSegmentEnd], query[valueSegmentStart..currentIndex], ref state);
        }

        return true;

        static void ExecuteCallback(QueryStringParameterCallback<TState> callback, ReadOnlySpan<char> name, ReadOnlySpan<char> value, ref TState state)
        {
            char[]? pooledArray = null;

            Span<char> lowerName = name.Length <= 256 ?
                stackalloc char[256] :
                (pooledArray = ArrayPool<char>.Shared.Rent(name.Length));

            try
            {
                name.ToLowerInvariant(lowerName);
                callback(lowerName[..name.Length], value, ref state);
            }
            finally
            {
                if (pooledArray is not null)
                {
                    ArrayPool<char>.Shared.Return(pooledArray, true);
                }
            }
        }
    }

    private static bool IsPermittedValueCharacter(char v)
    {
        return
            v == '-' ||
            (v >= 'A' && v <= 'Z') ||
            (v >= 'a' && v <= 'z') ||
            (v >= '0' && v <= '9') ||
            v == '.' ||
            v == '_' ||
            v == '~';
    }
}