// <copyright file="UriTemplate.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// Derived from Tavis.UriTemplate https://github.com/tavis-software/Tavis.UriTemplates/blob/master/License.txt

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Corvus.HighPerformance;
using Corvus.UriTemplates;

namespace Corvus.Json.UriTemplates;

/// <summary>
/// Implements a URI template conforming to http://tools.ietf.org/html/rfc6570, built over <see cref="JsonAny"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is modelled on the Tavis.UriTemplate API, and offers mechanisms for parsing parameters from a URI according to a URI template
/// and also resolving a template to a URI based on a provided set of parameters.
/// </para>
/// <para>
/// Note that this is not a low-allocation type. In particular, processing a URI template to create the template extraction
/// parser is an expensive operation. If you do not need this functionality for a particular instance, you should ensure that you
/// set the <c>createParameterParser</c> constructor parameter to <see langword="false"/>.
/// </para>
/// <para>
/// For low-allocation scenarios you should use the low-level <see cref="UriTemplateParserFactory"/> to create and cache an <see cref="IUriTemplateParser"/> instance if you are doing parameter extraction.
/// or use a <see cref="UriTemplateResolver{TParameterProvider, TParameterPayload}"/> if you are resolving a template from parameters.
/// </para>
/// <para>
/// </para>
/// </remarks>
public readonly struct UriTemplate
{
    private readonly string template;
    private readonly ImmutableDictionary<string, JsonAny> parameters;
    private readonly bool resolvePartially;
    private readonly IUriTemplateParser? parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="UriTemplate"/> struct.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="resolvePartially">Whether to partially resolve the template.</param>
    /// <param name="caseInsensitiveParameterNames">Whether to use case insensitive parameter names.</param>
    /// <param name="createParameterParser">Whether to pre-create the parameter extraction regex.</param>
    /// <param name="parameters">The parameters to use.</param>
    /// <remarks>
    /// <para>
    /// If you know this URI template is to be used purely for URI creation, not parameter extraction,
    /// then you should set <paramref name="createParameterParser"/> to <c>false</c>.
    /// You will avoid creating and compiling a regular expression for parameter extraction.
    /// </para>
    /// </remarks>
    public UriTemplate(string template, bool resolvePartially = false, bool caseInsensitiveParameterNames = false, bool createParameterParser = true, ImmutableDictionary<string, JsonAny>? parameters = null)
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

        if (createParameterParser)
        {
            this.parser = UriTemplateParserFactory.CreateParser(template);
        }
        else
        {
            this.parser = null;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UriTemplate"/> struct.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="resolvePartially">Whether to partially resolve the template.</param>
    /// <param name="parameters">The parameters dictionary.</param>
    /// <param name="parameterRegex">The parameter regular expression.</param>
    internal UriTemplate(string template, bool resolvePartially, ImmutableDictionary<string, JsonAny> parameters, IUriTemplateParser? parameterRegex)
    {
        this.resolvePartially = resolvePartially;
        this.template = template;
        this.parameters = parameters;
        this.parser = parameterRegex;
    }

    private enum States
    {
        CopyingLiterals,
        ParsingExpression,
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
        IUriTemplateParser parser = this.parser ?? UriTemplateParserFactory.CreateParser(this.template);

        ImmutableDictionary<string, JsonAny>.Builder result = ImmutableDictionary.CreateBuilder<string, JsonAny>();

        if (parser.ParseUri(uri.AsSpan(), AddResults, ref result))
        {
            parameters = result.ToImmutable();
            return true;
        }

        static void AddResults(bool reset, ReadOnlySpan<char> name, ReadOnlySpan<char> value, ref ImmutableDictionary<string, JsonAny>.Builder builder)
        {
            if (reset)
            {
                builder.Clear();
            }
            else
            {
                // Note we are making no attempt to make this low-allocation
                builder.Add(name.ToString(), UriExtensions.ParseUriValue(Uri.UnescapeDataString(value.ToString())));
            }
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
        return this.SetParameters(JsonAny.CreateFromSerializedInstance(parameters, options));
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
            string name = property.Name.GetString();
            builder.Remove(name);
            builder.Add(name, property.Value);
        }

        return new UriTemplate(this.template, this.resolvePartially, builder.ToImmutable(), this.parser);
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
            builder.Remove(name);
            builder.Add(name, value);
        }

        return new UriTemplate(this.template, this.resolvePartially, builder.ToImmutable(), this.parser);
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
        return new UriTemplate(this.template, this.resolvePartially, this.parameters.SetItem(name, value.AsAny), this.parser);
    }

    /// <summary>
    /// Removes the given parameter from the template.
    /// </summary>
    /// <param name="name">The name of the parameter to remove.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    public UriTemplate ClearParameter(string name)
    {
        return new UriTemplate(this.template, this.resolvePartially, this.parameters.Remove(name), this.parser);
    }

    /// <summary>
    /// Sets the named parameter to the given value.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    public UriTemplate SetParameter(string name, string value)
    {
        return new UriTemplate(this.template, this.resolvePartially, this.parameters.SetItem(name, new(value)), this.parser);
    }

    /// <summary>
    /// Sets the named parameter to the given value.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    public UriTemplate SetParameter(string name, double value)
    {
        return new UriTemplate(this.template, this.resolvePartially, this.parameters.SetItem(name, new(new BinaryJsonNumber(value))), this.parser);
    }

    /// <summary>
    /// Sets the named parameter to the given value.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    public UriTemplate SetParameter(string name, int value)
    {
        return new UriTemplate(this.template, this.resolvePartially, this.parameters.SetItem(name, new(new BinaryJsonNumber(value))), this.parser);
    }

    /// <summary>
    /// Sets the named parameter to the given value.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    public UriTemplate SetParameter(string name, long value)
    {
        return new UriTemplate(this.template, this.resolvePartially, this.parameters.SetItem(name, new(new BinaryJsonNumber(value))), this.parser);
    }

    /// <summary>
    /// Sets the named parameter to the given value.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    public UriTemplate SetParameter(string name, bool value)
    {
        return new UriTemplate(this.template, this.resolvePartially, this.parameters.SetItem(name, new(value)), this.parser);
    }

    /// <summary>
    /// Sets the named parameter to the given value.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    public UriTemplate SetParameter(string name, IEnumerable<string> value)
    {
        return new UriTemplate(this.template, this.resolvePartially, this.parameters.SetItem(name, JsonArray.FromRange(value)), this.parser);
    }

    /// <summary>
    /// Sets the named parameter to the given value.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>An instance of the template with the updated parameters.</returns>
    public UriTemplate SetParameter(string name, IDictionary<string, string> value)
    {
        return new UriTemplate(this.template, this.resolvePartially, this.parameters.SetItem(name, JsonAny.CreateFromSerializedInstance(value)), this.parser);
    }

    /// <summary>
    /// Gets the parameter names in the template.
    /// </summary>
    /// <returns>An enumerator for the parameter names.</returns>
    public ImmutableArray<string> GetParameterNames()
    {
        ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>();
        JsonUriTemplateResolver.TryGetParameterNames(this.template.AsSpan(), AccumulateParameterNames, ref builder);
        return builder.ToImmutable();

        static void AccumulateParameterNames(ReadOnlySpan<char> name, ref ImmutableArray<string>.Builder state)
        {
            state.Add(name.ToString());
        }
    }

    /// <summary>
    /// Resolve the template.
    /// </summary>
    /// <returns>The resolved template.</returns>
    public string Resolve()
    {
        // Use a fixed 4k, stack allocated buffer as our initial guess at a buffer size
        // ValueStringBuilder will grow it with a rented buffer if necessary, but this is
        // super-fast for the "general" case. It is also not a recursive operation, so we
        // are not going to overflow the buffer.
        Span<char> buffer = stackalloc char[4096];
        ValueStringBuilder output = new(buffer);
        var properties = this.parameters.ToImmutableDictionary(k => new JsonPropertyName(k.Key), v => v.Value);
        if (!JsonUriTemplateResolver.TryResolveResult(this.template.AsSpan(), ref output, this.resolvePartially, JsonObject.FromProperties(properties)))
        {
            throw new ArgumentException("Malformed template.");
        }

        return output.ToString();
    }
}