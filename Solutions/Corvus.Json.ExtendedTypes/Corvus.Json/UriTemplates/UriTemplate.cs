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
    private static readonly Regex FindParam = new(Varspec, RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex TemplateConversion = new(@"([^{]|^)\?", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static readonly Dictionary<char, OperatorInfo> Operators = new()
        {
            { '\0', new OperatorInfo(@default: true, first: string.Empty, separator: ',', named: false, ifEmpty: string.Empty, allowReserved: false) },
            { '+', new OperatorInfo(@default: false, first: string.Empty, separator: ',', named: false, ifEmpty: string.Empty, allowReserved: true) },
            { '.', new OperatorInfo(@default: false, first: ".", separator: '.', named: false, ifEmpty: string.Empty, allowReserved: false) },
            { '/', new OperatorInfo(@default: false, first: "/", separator: '/', named: false, ifEmpty: string.Empty, allowReserved: false) },
            { ';', new OperatorInfo(@default: false, first: ";", separator: ';', named: true, ifEmpty: string.Empty, allowReserved: false) },
            { '?', new OperatorInfo(@default: false, first: "?", separator: '&', named: true, ifEmpty: "=", allowReserved: false) },
            { '&', new OperatorInfo(@default: false, first: "&", separator: '&', named: true, ifEmpty: "=", allowReserved: false) },
            { '#', new OperatorInfo(@default: false, first: "#", separator: ',', named: false, ifEmpty: string.Empty, allowReserved: true) },
        };

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
            this.parameterRegex = new Regex(CreateMatchingRegex(template), RegexOptions.Compiled, TimeSpan.FromSeconds(1));
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
        Regex regex = this.parameterRegex ?? new Regex(CreateMatchingRegex(this.template));
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
        var result = ResultBuilder.Get();
        try
        {
            this.ResolveResult(ref result);
            return result.ParameterNames;
        }
        finally
        {
            ResultBuilder.Return(ref result);
        }
    }

    /// <summary>
    /// Resolve the template.
    /// </summary>
    /// <returns>The resolved template.</returns>
    public string Resolve()
    {
        var result = ResultBuilder.Get();
        try
        {
            this.ResolveResult(ref result);
            return result.ToString();
        }
        finally
        {
            ResultBuilder.Return(ref result);
        }
    }

    private static bool IsVarNameChar(char c)
    {
        return (c >= 'A' && c <= 'z') ////     Alpha
                || (c >= '0' && c <= '9') //// Digit
                || c == '_'
                || c == '%'
                || c == '.';
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

    private static OperatorInfo GetOperator(char operatorIndicator)
    {
        OperatorInfo op = operatorIndicator switch
        {
            '+' or ';' or '/' or '#' or '&' or '?' or '.' => Operators[operatorIndicator],
            _ => Operators['\0'],
        };
        return op;
    }

    private void ResolveResult(ref ResultBuilder result)
    {
        States currentState = States.CopyingLiterals;
        int expressionStart = -1;
        int expressionEnd = -1;
        int index = 0;
        ReadOnlySpan<char> templateSpan = this.template.AsSpan();

        foreach (char character in templateSpan)
        {
            switch (currentState)
            {
                case States.CopyingLiterals:
                    if (character == '{')
                    {
                        if (expressionStart != -1)
                        {
                            result.Append(templateSpan[expressionStart..expressionEnd]);
                        }

                        currentState = States.ParsingExpression;
                        expressionStart = index + 1;
                        expressionEnd = index + 1;
                    }
                    else if (character == '}')
                    {
                        throw new ArgumentException("Malformed template, unexpected } : " + result.ToString());
                    }
                    else
                    {
                        if (expressionStart == -1)
                        {
                            expressionStart = index;
                        }

                        expressionEnd = index + 1;
                    }

                    break;
                case States.ParsingExpression:
                    System.Diagnostics.Debug.Assert(expressionStart != -1, "The current expression must be set before parsing the expression.");

                    if (character == '}')
                    {
                        this.ProcessExpression(templateSpan[expressionStart..expressionEnd], ref result);

                        expressionStart = -1;
                        expressionEnd = -1;
                        currentState = States.CopyingLiterals;
                    }
                    else
                    {
                        expressionEnd = index + 1;
                    }

                    break;
            }

            index++;
        }

        if (currentState == States.ParsingExpression)
        {
            System.Diagnostics.Debug.Assert(expressionStart != -1, "The current expression must be set before parsing the expression.");

            result.Append("{");
            result.Append(templateSpan[expressionStart..expressionEnd]);

            throw new ArgumentException("Malformed template, missing } : " + result.ToString());
        }
        else
        {
            if (expressionStart != -1)
            {
                result.Append(templateSpan[expressionStart..expressionEnd]);
            }
        }

        if (result.ErrorDetected)
        {
            throw new ArgumentException("Malformed template : " + result.ToString());
        }
    }

    private void ProcessExpression(ReadOnlySpan<char> currentExpression, ref ResultBuilder result)
    {
        if (currentExpression.Length == 0)
        {
            result.ErrorDetected = true;
            result.Append("{}");
            return;
        }

        OperatorInfo op = GetOperator(currentExpression[0]);

        int firstChar = op.Default ? 0 : 1;
        bool multivariableExpression = false;
        int varNameStart = -1;
        int varNameEnd = -1;

        var varSpec = new VarSpec(op, ReadOnlySpan<char>.Empty);
        for (int i = firstChar; i < currentExpression.Length; i++)
        {
            char currentChar = currentExpression[i];
            switch (currentChar)
            {
                case '*':
                    if (varSpec.PrefixLength == 0)
                    {
                        varSpec.Explode = true;
                    }
                    else
                    {
                        result.ErrorDetected = true;
                    }

                    break;

                case ':': // Parse Prefix Modifier
                    currentChar = currentExpression[++i];
                    int prefixStart = i;
                    while (currentChar >= '0' && currentChar <= '9' && i < currentExpression.Length)
                    {
                        i++;
                        if (i < currentExpression.Length)
                        {
                            currentChar = currentExpression[i];
                        }
                    }

                    varSpec.PrefixLength = int.Parse(currentExpression[prefixStart..i]);
                    i--;
                    break;

                case ',':
                    varSpec.VarName = currentExpression[varNameStart..varNameEnd];
                    multivariableExpression = true;
                    bool success = this.ProcessVariable(ref varSpec, ref result, multivariableExpression);
                    bool isFirst = varSpec.First;

                    // Reset for new variable
                    varSpec = new VarSpec(op, ReadOnlySpan<char>.Empty);
                    varNameStart = -1;
                    varNameEnd = -1;
                    if (success || !isFirst || this.resolvePartially)
                    {
                        varSpec.First = false;
                    }

                    if (!success && this.resolvePartially)
                    {
                        result.Append(",");
                    }

                    break;

                default:
                    if (IsVarNameChar(currentChar))
                    {
                        if (varNameStart == -1)
                        {
                            varNameStart = i;
                        }

                        varNameEnd = i + 1;
                    }
                    else
                    {
                        result.ErrorDetected = true;
                    }

                    break;
            }
        }

        if (varNameStart != -1)
        {
            varSpec.VarName = currentExpression[varNameStart..varNameEnd];
        }

        this.ProcessVariable(ref varSpec, ref result, multivariableExpression);
        if (multivariableExpression && this.resolvePartially)
        {
            result.Append("}");
        }
    }

    private bool ProcessVariable(ref VarSpec varSpec, ref ResultBuilder result, bool multiVariableExpression = false)
    {
        string varname = varSpec.VarName.ToString();

        result.AddParameterName(varname);

        if (!this.parameters.ContainsKey(varname)
                || this.parameters[varname].IsNullOrUndefined()
                || (this.parameters[varname].ValueKind == JsonValueKind.Array && this.parameters[varname].GetArrayLength() == 0)
                || (this.parameters[varname].ValueKind == JsonValueKind.Object && !this.parameters[varname].HasProperties()))
        {
            if (this.resolvePartially)
            {
                if (multiVariableExpression)
                {
                    if (varSpec.First)
                    {
                        result.Append("{");
                    }

                    result.Append(varSpec.ToString());
                }
                else
                {
                    result.Append("{");
                    result.Append(varSpec.ToString());
                    result.Append("}");
                }

                return false;
            }

            return false;
        }

        if (varSpec.First)
        {
            result.Append(varSpec.OperatorInfo.First);
        }
        else
        {
            result.Append(varSpec.OperatorInfo.Separator);
        }

        JsonAny value = this.parameters[varname];

        JsonValueKind valueKind = value.ValueKind;
        if (valueKind == JsonValueKind.Array)
        {
            if (varSpec.OperatorInfo.Named && !varSpec.Explode) //// exploding will prefix with list name
            {
                result.AppendName(varname, varSpec.OperatorInfo, value.GetArrayLength() == 0);
            }

            result.AppendArray(varSpec.OperatorInfo, varSpec.Explode, varname, value);
        }
        else if (valueKind == JsonValueKind.Object)
        {
            if (varSpec.PrefixLength != 0)
            {
                result.ErrorDetected = true;
                return false;
            }

            if (varSpec.OperatorInfo.Named && !varSpec.Explode) //// exploding will prefix with list name
            {
                result.AppendName(varname, varSpec.OperatorInfo, !value.HasProperties());
            }

            result.AppendObject(varSpec.OperatorInfo, varSpec.Explode, value);
        }
        else if (valueKind == JsonValueKind.String)
        {
            if (varSpec.OperatorInfo.Named)
            {
                result.AppendName(varname, varSpec.OperatorInfo, value.IsNullOrUndefined() || string.IsNullOrEmpty(value));
            }

            result.AppendValue(value, varSpec.PrefixLength, varSpec.OperatorInfo.AllowReserved);
        }
        else
        {
            if (varSpec.OperatorInfo.Named)
            {
                result.AppendName(varname, varSpec.OperatorInfo, value.IsNullOrUndefined());
            }

            result.AppendValue(value, varSpec.PrefixLength, varSpec.OperatorInfo.AllowReserved);
        }

        return true;
    }
}