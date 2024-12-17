// <copyright file="UriTemplate.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from Tavis.UriTemplate https://github.com/tavis-software/Tavis.UriTemplates/blob/master/License.txt
// </licensing>

namespace Corvus.UriTemplates.TavisApi;

/// <summary>
/// A UriTemplate conforming (broadly!) to the Tavis API.
/// </summary>
public class UriTemplate
{
    private static readonly Uri ComponentBaseUri = new("https://localhost.com", UriKind.Absolute);
    private readonly object lockObject = new();
    private readonly string template;
    private readonly Dictionary<string, object?> parameters;
    private readonly bool resolvePartially;
    private IUriTemplateParser? parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="UriTemplate"/> class.
    /// </summary>
    /// <param name="template">The URI template.</param>
    /// <param name="resolvePartially">Indicates whether to allow partial resolution.</param>
    /// <param name="caseInsensitiveParameterNames">Indicates whether to use case insensitive parameter names.</param>
    public UriTemplate(string template, bool resolvePartially = false, bool caseInsensitiveParameterNames = false)
    {
        this.resolvePartially = resolvePartially;
        this.template = template;
        this.parameters = caseInsensitiveParameterNames
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>();
    }

    /// <summary>
    /// Create a matching regular expression for the uri template.
    /// </summary>
    /// <param name="uriTemplate">The uri template for which to get the regular expression.</param>
    /// <returns>The matching regular expression.</returns>
    public static string CreateMatchingRegex(string uriTemplate)
    {
        return UriTemplateRegexBuilder.CreateMatchingRegex(uriTemplate);
    }

    /// <summary>
    /// Create a matching regular expression for the uri template.
    /// </summary>
    /// <param name="uriTemplate">The uri template for which to get the regular expression.</param>
    /// <returns>The matching regular expression.</returns>
    public static string CreateMatchingRegex2(string uriTemplate)
    {
        return UriTemplateRegexBuilder.CreateMatchingRegex(uriTemplate);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.template;
    }

    /// <summary>
    /// Set a parameter.
    /// </summary>
    /// <param name="name">The name of the parameter to set.</param>
    /// <param name="value">The value of the parameter.</param>
    public void SetParameter(string name, object? value)
    {
        this.parameters[name] = value;
    }

    /// <summary>
    /// Clears the parameter with the given name.
    /// </summary>
    /// <param name="name">The name of the parameter to clear.</param>
    public void ClearParameter(string name)
    {
        this.parameters.Remove(name);
    }

    /// <summary>
    /// Set a parameter.
    /// </summary>
    /// <param name="name">The name of the parameter to set.</param>
    /// <param name="value">The value of the parameter.</param>
    public void SetParameter(string name, string? value)
    {
        this.parameters[name] = value;
    }

    /// <summary>
    /// Set a parameter.
    /// </summary>
    /// <param name="name">The name of the parameter to set.</param>
    /// <param name="value">The value of the parameter.</param>
    public void SetParameter(string name, IEnumerable<string>? value)
    {
        this.parameters[name] = value;
    }

    /// <summary>
    /// Set a parameter.
    /// </summary>
    /// <param name="name">The name of the parameter to set.</param>
    /// <param name="value">The value of the parameter.</param>
    public void SetParameter(string name, IDictionary<string, string>? value)
    {
        this.parameters[name] = value;
    }

    /// <summary>
    /// Get the names of the parameters in the template.
    /// </summary>
    /// <returns>The parameters in the template.</returns>
    public IEnumerable<string> GetParameterNames()
    {
        List<string> builder = new();

        DictionaryUriTemplateResolver.TryGetParameterNames(this.template.AsSpan(), AccumulateParameterNames, ref builder);
        return builder;

        static void AccumulateParameterNames(ReadOnlySpan<char> name, ref List<string> state)
        {
            state.Add(name.ToString());
        }
    }

    /// <summary>
    /// Applies the parameters to the template and returns the result.
    /// </summary>
    /// <returns>The resulting URI or partially-resolved template.</returns>
    public string Resolve()
    {
        string result = string.Empty;
        if (!DictionaryUriTemplateResolver.TryResolveResult(this.template.AsSpan(), this.resolvePartially, this.parameters, ResolveToString, ref result))
        {
            throw new ArgumentException("Malformed template.");
        }

        return result;

        static void ResolveToString(ReadOnlySpan<char> name, ref string state)
        {
            state = name.ToString();
        }
    }

    /// <summary>
    /// Gets the parameters from the given URI.
    /// </summary>
    /// <param name="uri">The URI from which to get the parameters.</param>
    /// <param name="order">Whether to apply strict or relaxed query parameter ordering.</param>
    /// <returns>The parameters decomposed from the Uri.</returns>
    public IDictionary<string, object>? GetParameters(Uri uri, QueryStringParameterOrder order = QueryStringParameterOrder.Strict)
    {
        switch (order)
        {
            case QueryStringParameterOrder.Strict:
                {
                    IUriTemplateParser? parser = this.parser;

                    if (parser == null)
                    {
                        parser = UriTemplateParserFactory.CreateParser(this.template);
                        lock (this.lockObject)
                        {
                            this.parser = parser;
                        }
                    }

                    var parameters = new Dictionary<string, object>();

                    if (parser.ParseUri(uri.OriginalString.AsSpan(), AddResults, ref parameters))
                    {
                        return parameters;
                    }
                    else
                    {
                        return null;
                    }

                    static void AddResults(bool reset, ReadOnlySpan<char> name, ReadOnlySpan<char> value, ref Dictionary<string, object> results)
                    {
                        if (reset)
                        {
                            results.Clear();
                        }
                        else
                        {
                            // Note we are making no attempt to make this low-allocation
                            results.Add(name.ToString(), Uri.UnescapeDataString(value.ToString()));
                        }
                    }
                }

            case QueryStringParameterOrder.Any:
                {
                    if (!uri.IsAbsoluteUri)
                    {
                        uri = new Uri(ComponentBaseUri, uri);
                    }

                    string uriString = uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path | UriComponents.Fragment, UriFormat.UriEscaped);
                    var uriWithoutQuery = new Uri(uriString, UriKind.Absolute);

                    IDictionary<string, object> pathParameters = this.GetParameters(uriWithoutQuery) ?? new Dictionary<string, object>(this.parameters.Comparer);

                    HashSet<string> parameterNames = this.GetParameterNamesHashSet();

                    (HashSet<string> ParameterNames, IDictionary<string, object> PathParameters) parameterState = (parameterNames, pathParameters);

                    uri.GetQueryStringParameters(MatchParameterNames, ref parameterState);

                    return pathParameters.Count == 0 ? null : pathParameters;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(order), order, null);
        }
    }

    /// <summary>
    /// Matches the given URI against the template.
    /// </summary>
    /// <param name="uri">The URI to match.</param>
    /// <param name="order">Whether query string ordering is strict.</param>
    /// <returns><see langword="true"/> if the uri matches the template.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The parameter order was not understood.</exception>
    internal bool IsMatch(Uri uri, QueryStringParameterOrder order = QueryStringParameterOrder.Strict)
    {
        switch (order)
        {
            case QueryStringParameterOrder.Strict:
                {
                    IUriTemplateParser? parser = this.parser;

                    if (parser == null)
                    {
                        parser = UriTemplateParserFactory.CreateParser(this.template);
                        lock (this.lockObject)
                        {
                            this.parser = parser;
                        }
                    }

                    if (parser.IsMatch(uri.OriginalString.AsSpan()))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

            case QueryStringParameterOrder.Any:
                {
                    if (!uri.IsAbsoluteUri)
                    {
                        uri = new Uri(ComponentBaseUri, uri);
                    }

                    IDictionary<string, object> pathParameters = new Dictionary<string, object>(this.parameters.Comparer);

                    HashSet<string> parameterNames = this.GetParameterNamesHashSet();

                    (HashSet<string> ParameterNames, IDictionary<string, object> PathParameters) parameterState = (parameterNames, pathParameters);

                    uri.GetQueryStringParameters(MatchParameterNames, ref parameterState);

                    return pathParameters.Count != 0;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(order), order, null);
        }
    }

    /// <summary>
    /// Get the names of the parameters in the template.
    /// </summary>
    /// <returns>The parameters in the template.</returns>
    internal HashSet<string> GetParameterNamesHashSet()
    {
        HashSet<string> builder = new();

        DictionaryUriTemplateResolver.TryGetParameterNames(this.template.AsSpan(), AccumulateParameterNames, ref builder);
        return builder;

        static void AccumulateParameterNames(ReadOnlySpan<char> name, ref HashSet<string> state)
        {
            state.Add(name.ToString());
        }
    }

    private static void MatchParameterNames(ReadOnlySpan<char> name, ReadOnlySpan<char> value, ref (HashSet<string> ParameterNames, IDictionary<string, object> PathParameters) state)
    {
        string name1 = name.ToString();
        if (state.ParameterNames.Contains(name1))
        {
            state.PathParameters.Add(name1, value.ToString());
        }
    }
}