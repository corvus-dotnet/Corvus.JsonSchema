// <copyright file="UriTemplateResolver{TParameterProvider,TParameterPayload}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.HighPerformance;
using Corvus.UriTemplates.TemplateParameterProviders;

namespace Corvus.UriTemplates;

/// <summary>
/// A delegate for a callback providing parameter names as they are discovered.
/// </summary>
/// <typeparam name="TState">The type of the state.</typeparam>
/// <param name="name">The parameter name.</param>
/// <param name="state">The state passed by the caller.</param>
public delegate void ParameterNameCallback<TState>(ReadOnlySpan<char> name, ref TState state);

/// <summary>
/// A delegate for a callback providing a resolved template.
/// </summary>
/// <typeparam name="TState">The type of the state.</typeparam>
/// <param name="resolvedTemplate">The resolved template.</param>
/// <param name="state">The state passed by the caller.</param>
public delegate void ResolvedUriTemplateCallback<TState>(ReadOnlySpan<char> resolvedTemplate, ref TState state);

/// <summary>
/// Resolves a UriTemplate by (optionally, partially) applying parameters to the template, to create a URI (if fully resolved), or a partially resolved URI template.
/// </summary>
/// <typeparam name="TParameterProvider">The type of the template parameter provider.</typeparam>
/// <typeparam name="TParameterPayload">The type of the parameter payload.</typeparam>
public static class UriTemplateResolver<TParameterProvider, TParameterPayload>
    where TParameterProvider : ITemplateParameterProvider<TParameterPayload>
{
    private static readonly OperatorInfo OpInfoZero = new(@default: true, first: '\0', separator: ',', named: false, ifEmpty: string.Empty, allowReserved: false);
    private static readonly OperatorInfo OpInfoPlus = new(@default: false, first: '\0', separator: ',', named: false, ifEmpty: string.Empty, allowReserved: true);
    private static readonly OperatorInfo OpInfoDot = new(@default: false, first: '.', separator: '.', named: false, ifEmpty: string.Empty, allowReserved: false);
    private static readonly OperatorInfo OpInfoSlash = new(@default: false, first: '/', separator: '/', named: false, ifEmpty: string.Empty, allowReserved: false);
    private static readonly OperatorInfo OpInfoSemicolon = new(@default: false, first: ';', separator: ';', named: true, ifEmpty: string.Empty, allowReserved: false);
    private static readonly OperatorInfo OpInfoQuery = new(@default: false, first: '?', separator: '&', named: true, ifEmpty: "=", allowReserved: false);
    private static readonly OperatorInfo OpInfoAmpersand = new(@default: false, first: '&', separator: '&', named: true, ifEmpty: "=", allowReserved: false);
    private static readonly OperatorInfo OpInfoHash = new(@default: false, first: '#', separator: ',', named: false, ifEmpty: string.Empty, allowReserved: true);

    private enum States
    {
        CopyingLiterals,
        ParsingExpression,
    }

    /// <summary>
    /// Resolve the template into an output result.
    /// </summary>
    /// <typeparam name="TState">The type of the state to pass to the callback.</typeparam>
    /// <param name="parameterProvider">An instance of the parameter provider.</param>
    /// <param name="template">The template to resolve.</param>
    /// <param name="resolvePartially">If <see langword="true"/> then partially resolve the result.</param>
    /// <param name="parameters">The parameters to apply to the template.</param>
    /// <param name="callback">The callback which is provided with the resolved template.</param>
    /// <param name="parameterNameCallback">An optional callback which is provided each parameter name as they are discovered.</param>
    /// <param name="state">The state to pass to the callback.</param>
    /// <returns><see langword="true"/> if the URI matched the template, and the parameters were resolved successfully.</returns>
    public static bool TryResolveResult<TState>(TParameterProvider parameterProvider, ReadOnlySpan<char> template, bool resolvePartially, in TParameterPayload parameters, ResolvedUriTemplateCallback<TState> callback, ParameterNameCallback<TState>? parameterNameCallback, ref TState state)
    {
        ValueStringBuilder builder = new(template.Length * 4);

        try
        {
            if (TryResolveResult(parameterProvider, template, ref builder, resolvePartially, parameters, parameterNameCallback, ref state))
            {
                callback(builder.AsSpan(), ref state);
                return true;
            }

            return false;
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>
    /// Resolve the template into an output result.
    /// </summary>
    /// <typeparam name="TState">The type of the callback state.</typeparam>
    /// <param name="parameterProvider">The parameter provider.</param>
    /// <param name="template">The template to resolve.</param>
    /// <param name="output">The output buffer into which to resolve the template.</param>
    /// <param name="resolvePartially">If <see langword="true"/> then partially resolve the result.</param>
    /// <param name="parameters">The parameters to apply to the template.</param>
    /// <param name="parameterNameCallback">An optional callback which is provided each parameter name as they are discovered.</param>
    /// <param name="state">The callback state.</param>
    /// <returns><see langword="true"/> if the URI matched the template, and the parameters were resolved successfully.</returns>
    public static bool TryResolveResult<TState>(TParameterProvider parameterProvider, ReadOnlySpan<char> template, ref ValueStringBuilder output, bool resolvePartially, in TParameterPayload parameters, ParameterNameCallback<TState>? parameterNameCallback, ref TState state)
    {
        States currentState = States.CopyingLiterals;
        int expressionStart = -1;
        int expressionEnd = -1;
        int index = 0;

        foreach (char character in template)
        {
            switch (currentState)
            {
                case States.CopyingLiterals:
                    if (character == '{')
                    {
                        if (expressionStart != -1)
                        {
                            output.Append(template[expressionStart..expressionEnd]);
                        }

                        currentState = States.ParsingExpression;
                        expressionStart = index + 1;
                        expressionEnd = index + 1;
                    }
                    else if (character == '}')
                    {
                        return false;
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
                        if (!ProcessExpression(parameterProvider, template[expressionStart..expressionEnd], ref output, resolvePartially, parameters, parameterNameCallback, ref state))
                        {
                            return false;
                        }

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
            return false;
        }

        if (expressionStart != -1)
        {
            output.Append(template[expressionStart..expressionEnd]);
        }

        return true;
    }

    private static OperatorInfo GetOperator(char operatorIndicator)
    {
        return operatorIndicator switch
        {
            '+' => OpInfoPlus,
            ';' => OpInfoSemicolon,
            '/' => OpInfoSlash,
            '#' => OpInfoHash,
            '&' => OpInfoAmpersand,
            '?' => OpInfoQuery,
            '.' => OpInfoDot,
            _ => OpInfoZero,
        };
    }

    private static bool IsVarNameChar(char c)
    {
        return (c >= 'A' && c <= 'z') ////     Alpha
                || (c >= '0' && c <= '9') //// Digit
                || c == '_'
                || c == '%'
                || c == '.';
    }

    private static bool ProcessExpression<TState>(TParameterProvider parameterProvider, ReadOnlySpan<char> currentExpression, ref ValueStringBuilder output, bool resolvePartially, in TParameterPayload parameters, ParameterNameCallback<TState>? parameterNameCallback, ref TState state)
    {
        if (currentExpression.Length == 0)
        {
            return false;
        }

        OperatorInfo op = GetOperator(currentExpression[0]);

        int firstChar = op.Default ? 0 : 1;
        bool multivariableExpression = false;
        int varNameStart = -1;
        int varNameEnd = -1;
        VariableProcessingState success = VariableProcessingState.Success;

        var varSpec = new VariableSpecification(op, ReadOnlySpan<char>.Empty);
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
                        return false;
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

                    if (prefixStart == i)
                    {
                        // This is a malformed template
                        return false;
                    }

#if NET8_0_OR_GREATER
                    varSpec.PrefixLength = int.Parse(currentExpression[prefixStart..i]);
#else
                    varSpec.PrefixLength = ParseInt(currentExpression[prefixStart..i]);
#endif
                    i--;
                    break;

                case ',':
                    varSpec.VarName = currentExpression[varNameStart..varNameEnd];
                    multivariableExpression = true;

                    success = ProcessVariable(parameterProvider, ref varSpec, ref output, multivariableExpression, resolvePartially, parameters, parameterNameCallback, ref state);
                    bool isFirst = varSpec.First;

                    // Reset for new variable
                    varSpec = new VariableSpecification(op, ReadOnlySpan<char>.Empty);
                    varNameStart = -1;
                    varNameEnd = -1;
                    if ((success == VariableProcessingState.Success) || !isFirst || resolvePartially)
                    {
                        varSpec.First = false;
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
                        return false;
                    }

                    break;
            }
        }

        if (varNameStart != -1)
        {
            varSpec.VarName = currentExpression[varNameStart..varNameEnd];
        }

        VariableProcessingState outerSuccess = ProcessVariable(parameterProvider, ref varSpec, ref output, multivariableExpression, resolvePartially, parameters, parameterNameCallback, ref state);

        return outerSuccess != VariableProcessingState.Failure;

#if !NET8_0_OR_GREATER
        static int ParseInt(ReadOnlySpan<char> span)
        {
            int result = 0;
            foreach (char c in span)
            {
                result = (result * 10) + (c - '0');
            }

            return result;
        }
#endif
    }

    private static VariableProcessingState ProcessVariable<TState>(TParameterProvider parameterProvider, ref VariableSpecification varSpec, ref ValueStringBuilder output, bool multiVariableExpression, bool resolvePartially, in TParameterPayload parameters, ParameterNameCallback<TState>? parameterNameCallback, ref TState state)
    {
        if (parameterNameCallback is ParameterNameCallback<TState> callback)
        {
            callback(varSpec.VarName, ref state);
        }

        VariableProcessingState result = parameterProvider.ProcessVariable(ref varSpec, parameters, ref output);

        if (result == VariableProcessingState.NotProcessed)
        {
            if (resolvePartially)
            {
                if (multiVariableExpression)
                {
                    if (varSpec.First)
                    {
                        output.Append("{");
                    }
                    else
                    {
                        output.Append("{&");
                    }

                    varSpec.CopyTo(ref output);

                    output.Append('}');
                }
                else
                {
                    output.Append('{');
                    varSpec.CopyTo(ref output);
                    output.Append('}');
                }
            }

            return VariableProcessingState.NotProcessed;
        }

        return result;
    }
}