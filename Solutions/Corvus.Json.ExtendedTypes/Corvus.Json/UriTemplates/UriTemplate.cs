// <copyright file="UriTemplate.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// Derived from Tavis.UriTemplate https://github.com/tavis-software/Tavis.UriTemplates/blob/master/License.txt

using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Json.Internal;

namespace Corvus.Json.UriTemplates;

/// <summary>
/// A UriTemplate conforming to http://tools.ietf.org/html/rfc6570.
/// </summary>
public readonly struct UriTemplate
{
    private const string Varname = "[a-zA-Z0-9_]*";
    private const string Op = "(?<op>[+#./;?&]?)";
    private const string Var = "(?<var>(?:(?<lvar>" + Varname + ")[*]?,?)*)";
    private const string Varspec = "(?<varspec>{" + Op + Var + "})";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    private static readonly Regex FindParam = new(Varspec, RegexOptions.Compiled, DefaultTimeout);
    private static readonly Regex TemplateConversion = new(@"([^{]|^)\?", RegexOptions.Compiled, DefaultTimeout);

    private readonly string template;
    private readonly ImmutableDictionary<string, JsonAny> parameters;
    private readonly bool resolvePartially;
    private readonly Regex? parameterRegex;

    /// <summary>
    /// Initializes a new instance of the <see cref="UriTemplate"/> struct.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="resolvePartially">Whether to partially resolve the template.</param>
    /// <param name="caseInsensitiveParameterNames">Whether to use case insensitive parameter names.</param>
    /// <param name="createParameterRegex">Whether to pre-create the parameter extraction regex.</param>
    /// <param name="parameters">The parameters to use.</param>
    /// <remarks>
    /// <para>
    /// If you know this URI template is to be used purely for URI creation, not parameter extraction,
    /// then you should set <paramref name="createParameterRegex"/> to <c>false</c>.
    /// You will avoid creating and compiling a regular expression for parameter extraction.
    /// </para>
    /// </remarks>
    public UriTemplate(string template, bool resolvePartially = false, bool caseInsensitiveParameterNames = false, bool createParameterRegex = true, ImmutableDictionary<string, JsonAny>? parameters = null)
    {
        this.resolvePartially = resolvePartially;
        this.template = template;
        if (parameters is ImmutableDictionary<string, JsonAny> p)
        {
            this.parameters = caseInsensitiveParameterNames
                    ? p.WithComparers(StringComparer.OrdinalIgnoreCase)
                    : p.WithComparers(StringComparer.Ordinal);
        }
        else
        {
            this.parameters = caseInsensitiveParameterNames
                ? ImmutableDictionary.Create<string, JsonAny>(StringComparer.OrdinalIgnoreCase)
                : ImmutableDictionary<string, JsonAny>.Empty;
        }

        if (createParameterRegex)
        {
            this.parameterRegex = new Regex(CreateMatchingRegex(template), RegexOptions.Compiled, DefaultTimeout);
        }
        else
        {
            this.parameterRegex = null;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UriTemplate"/> struct.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="resolvePartially">Whether to partially resolve the template.</param>
    /// <param name="parameters">The parameters dictionary.</param>
    /// <param name="parameterRegex">The parameter regular expression.</param>
    internal UriTemplate(string template, bool resolvePartially, ImmutableDictionary<string, JsonAny> parameters, Regex? parameterRegex)
    {
        this.resolvePartially = resolvePartially;
        this.template = template;
        this.parameters = parameters;
        this.parameterRegex = parameterRegex;
    }

    private enum States
    {
        CopyingLiterals,
        ParsingExpression,
    }

    /// <summary>
    /// Creates a regular expression matching the given URI template.
    /// </summary>
    /// <param name="uriTemplate">The uri template.</param>
    /// <returns>The regular expression string matching the URI template.</returns>
    public static string CreateMatchingRegex(string uriTemplate)
    {
        string template = TemplateConversion.Replace(uriTemplate, @"$+\?");
        string regex = FindParam.Replace(template, Match);
        return regex + "$";

        static string Match(Match m)
        {
            CaptureCollection captures = m.Groups["lvar"].Captures;
            string[] paramNames = ArrayPool<string>.Shared.Rent(captures.Count);
            try
            {
                int written = 0;
                foreach (Capture capture in captures)
                {
                    if (!string.IsNullOrEmpty(capture.Value))
                    {
                        paramNames[written++] = capture.Value;
                    }
                }

                ReadOnlySpan<string> paramNamesSpan = paramNames.AsSpan()[0..written];

                string op = m.Groups["op"].Value;
                return op switch
                {
                    "?" => GetQueryExpression(paramNamesSpan, prefix: "?"),
                    "&" => GetQueryExpression(paramNamesSpan, prefix: "&"),
                    "#" => GetExpression(paramNamesSpan, prefix: "#"),
                    "/" => GetExpression(paramNamesSpan, prefix: "/"),
                    "+" => GetExpression(paramNamesSpan),
                    _ => GetExpression(paramNamesSpan),
                };
            }
            finally
            {
                ArrayPool<string>.Shared.Return(paramNames);
            }
        }
    }

    /// <summary>
    /// Gets the parameters from the given URI.
    /// </summary>
    /// <param name="uri">The URI from which to get the parameters.</param>
    /// <param name="parameters">The parameters decomposed from the Uri.</param>
    /// <returns>True if the parameters were successfully decomposed, otherwise false.</returns>
    public bool TryGetParameters(Uri uri, [NotNullWhen(true)] out ImmutableDictionary<string, JsonAny>? parameters)
    {
        return this.TryGetParameters(uri.OriginalString, out parameters);
    }

    /// <summary>
    /// Gets the parameters from the given URI.
    /// </summary>
    /// <param name="uri">The URI from which to get the parameters.</param>
    /// <param name="parameters">The parameters decomposed from the Uri.</param>
    /// <returns>True if the parameters were successfully decomposed, otherwise false.</returns>
    public bool TryGetParameters(string uri, [NotNullWhen(true)] out ImmutableDictionary<string, JsonAny>? parameters)
    {
        Regex regex = this.parameterRegex ?? new Regex(CreateMatchingRegex(this.template), RegexOptions.None, DefaultTimeout);
        Match match = regex.Match(uri);
        if (match.Success)
        {
            ImmutableDictionary<string, JsonAny>.Builder result = ImmutableDictionary.CreateBuilder<string, JsonAny>();

            for (int x = 1; x < match.Groups.Count; x++)
            {
                if (match.Groups[x].Success)
                {
                    string paramName = regex.GroupNameFromNumber(x);
                    if (!string.IsNullOrEmpty(paramName))
                    {
                        result.Add(paramName, JsonAny.ParseUriValue(Uri.UnescapeDataString(match.Groups[x].Value)));
                    }
                }
            }

            parameters = result.ToImmutable();
            return true;
        }

        parameters = null;
        return false;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.template;
    }

    /// <summary>
    /// Sets multiple parameters on the URI template.
    /// </summary>
    /// <typeparam name="T">The type of the object to use to set parameters.</typeparam>
    /// <param name="parameters">The parameters to set.</param>
    /// <param name="options">The (optional) serialization options.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    /// <remarks>This serializes the object, and treats each property on the resulting <see cref="JsonObject"/> as a named parameter value.</remarks>
    public UriTemplate SetParameters<T>(T parameters, JsonWriterOptions options = default)
    {
        return this.SetParameters(JsonAny.From(parameters, options));
    }

    /// <summary>
    /// Sets multiple parameters on the URI template.
    /// </summary>
    /// <param name="parameters">The parameters to set.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    /// <remarks>This treats each property on the <see cref="JsonObject"/> as a named parameter value.</remarks>
    public UriTemplate SetParameters(in JsonAny parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException($"The parameters must be {JsonValueKind.Object}, but were {parameters.ValueKind}", nameof(parameters));
        }

        return this.SetParameters(parameters.AsObject);
    }

    /// <summary>
    /// Sets multiple parameters on the URI template.
    /// </summary>
    /// <param name="parameters">The parameters to set.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    /// <remarks>This treats each property on the <see cref="JsonObject"/> as a named parameter value.</remarks>
    public UriTemplate SetParameters(in JsonObject parameters)
    {
        ImmutableDictionary<string, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<string, JsonAny>(this.parameters.KeyComparer);
        builder.AddRange(this.parameters);
        foreach (JsonObjectProperty property in parameters.EnumerateObject())
        {
            string name = property.Name;
            if (builder.ContainsKey(name))
            {
                builder.Remove(name);
            }

            builder.Add(name, property.Value);
        }

        return new UriTemplate(this.template, this.resolvePartially, builder.ToImmutable(), this.parameterRegex);
    }

    /// <summary>
    /// Sets multiple parameters on the URI template.
    /// </summary>
    /// <param name="parameters">The parameters to set.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    public UriTemplate SetParameters(params (string, JsonAny)[] parameters)
    {
        ImmutableDictionary<string, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<string, JsonAny>(this.parameters.KeyComparer);
        builder.AddRange(this.parameters);
        foreach ((string name, JsonAny value) in parameters)
        {
            if (builder.ContainsKey(name))
            {
                builder.Remove(name);
            }

            builder.Add(name, value);
        }

        return new UriTemplate(this.template, this.resolvePartially, builder.ToImmutable(), this.parameterRegex);
    }

    /// <summary>
    /// Sets the named parameter to the given value.
    /// </summary>
    /// <typeparam name="T">The type of the value to set.</typeparam>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    public UriTemplate SetParameter<T>(string name, T value)
        where T : struct, IJsonValue
    {
        return new UriTemplate(this.template, this.resolvePartially, this.parameters.SetItem(name, value.AsAny), this.parameterRegex);
    }

    /// <summary>
    /// Removes the given parameter from the template.
    /// </summary>
    /// <param name="name">The name of the parameter to remove.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    public UriTemplate ClearParameter(string name)
    {
        return new UriTemplate(this.template, this.resolvePartially, this.parameters.Remove(name), this.parameterRegex);
    }

    /// <summary>
    /// Sets the named parameter to the given value.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    public UriTemplate SetParameter(string name, string value)
    {
        return new UriTemplate(this.template, this.resolvePartially, this.parameters.SetItem(name, value), this.parameterRegex);
    }

    /// <summary>
    /// Sets the named parameter to the given value.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    public UriTemplate SetParameter(string name, double value)
    {
        return new UriTemplate(this.template, this.resolvePartially, this.parameters.SetItem(name, value), this.parameterRegex);
    }

    /// <summary>
    /// Sets the named parameter to the given value.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    public UriTemplate SetParameter(string name, int value)
    {
        return new UriTemplate(this.template, this.resolvePartially, this.parameters.SetItem(name, value), this.parameterRegex);
    }

    /// <summary>
    /// Sets the named parameter to the given value.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    public UriTemplate SetParameter(string name, long value)
    {
        return new UriTemplate(this.template, this.resolvePartially, this.parameters.SetItem(name, value), this.parameterRegex);
    }

    /// <summary>
    /// Sets the named parameter to the given value.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    public UriTemplate SetParameter(string name, bool value)
    {
        return new UriTemplate(this.template, this.resolvePartially, this.parameters.SetItem(name, value), this.parameterRegex);
    }

    /// <summary>
    /// Sets the named parameter to the given value.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    public UriTemplate SetParameter(string name, IEnumerable<string> value)
    {
        return new UriTemplate(this.template, this.resolvePartially, this.parameters.SetItem(name, JsonAny.FromRange(value)), this.parameterRegex);
    }

    /// <summary>
    /// Sets the named parameter to the given value.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    public UriTemplate SetParameter(string name, IDictionary<string, string> value)
    {
        return new UriTemplate(this.template, this.resolvePartially, this.parameters.SetItem(name, JsonAny.From(value)), this.parameterRegex);
    }

    /// <summary>
    /// Gets the parameter names in the template.
    /// </summary>
    /// <returns>An enumerator for the parameter names.</returns>
    public ImmutableArray<string> GetParameterNames()
    {
        ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>();

        ArrayBufferWriter<char> output = new();
        var properties = this.parameters.ToImmutableDictionary(k => (JsonPropertyName)k.Key, v => v.Value);
        UriTemplateResolver.TryResolveResult(this.template.AsSpan(), output, this.resolvePartially, JsonAny.FromProperties(properties), AccumulateParameterNames);
        return builder.ToImmutable();

        void AccumulateParameterNames(ReadOnlySpan<char> name)
        {
            builder.Add(name.ToString());
        }
    }

    /// <summary>
    /// Resolve the template.
    /// </summary>
    /// <returns>The resolved template.</returns>
    public string Resolve()
    {
        ArrayBufferWriter<char> output = new();
        var properties = this.parameters.ToImmutableDictionary(k => (JsonPropertyName)k.Key, v => v.Value);
        if (!UriTemplateResolver.TryResolveResult(this.template.AsSpan(), output, this.resolvePartially, JsonAny.FromProperties(properties)))
        {
            throw new ArgumentException("Malformed template.");
        }

        return output.WrittenSpan.ToString();
    }

    private static string GetQueryExpression(ReadOnlySpan<string> paramNames, string prefix)
    {
        StringBuilder sb = StringBuilderPool.Shared.Get();

        try
        {
            foreach (string paramname in paramNames)
            {
                sb.Append('\\');
                sb.Append(prefix);
                sb.Append('?');
                if (prefix == "?")
                {
                    prefix = "&";
                }

                sb.Append("(?:");
                sb.Append(paramname);
                sb.Append('=');

                sb.Append("(?<");
                sb.Append(paramname);
                sb.Append('>');
                sb.Append("[^/?&]+");
                sb.Append(')');
                sb.Append(")?");
            }

            return sb.ToString();
        }
        finally
        {
            StringBuilderPool.Shared.Return(sb);
        }
    }

    private static string GetExpression(ReadOnlySpan<string> paramNames, string? prefix = null)
    {
        StringBuilder sb = StringBuilderPool.Shared.Get();

        try
        {
            string paramDelim = prefix switch
            {
                "#" => "[^,]+",
                "/" => "[^/?]+",
                "?" or "&" => "[^&#]+",
                ";" => "[^;/?#]+",
                "." => "[^./?#]+",
                _ => "[^/?&]+",
            };

            foreach (string paramname in paramNames)
            {
                if (string.IsNullOrEmpty(paramname))
                {
                    continue;
                }

                if (prefix != null)
                {
                    sb.Append('\\');
                    sb.Append(prefix);
                    sb.Append('?');
                    if (prefix == "#")
                    {
                        prefix = ",";
                    }
                }

                sb.Append("(?<");
                sb.Append(paramname);
                sb.Append('>');
                sb.Append(paramDelim); // Param Value
                sb.Append(")?");
            }

            return sb.ToString();
        }
        finally
        {
            StringBuilderPool.Shared.Return(sb);
        }
    }
}