// <copyright file="UriTemplateResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using System.Text.Json;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using Corvus.Json.Internal;
using Microsoft.Extensions.ObjectPool;

namespace Corvus.Json.UriTemplates;

/// <summary>
/// Resolves a UriTemplate by (optionally, partially) applying parameters to the template, to create a URI (if fully resolved), or a partially resolved URI template.
/// </summary>
public static class UriTemplateResolver
{
    private const string UriReservedSymbols = ":/?#[]@!$&'()*+,;=";
    private const string UriUnreservedSymbols = "-._~";
    private static readonly char[] HexDigits = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
    private static readonly ObjectPool<ArrayPoolBufferWriter<char>> ArrayPoolWriterPool =
     new DefaultObjectPoolProvider().Create<ArrayPoolBufferWriter<char>>();

    private static readonly OpInfo OpInfoZero = new(@default: true, first: '\0', separator: ',', named: false, ifEmpty: string.Empty, allowReserved: false);
    private static readonly OpInfo OpInfoPlus = new(@default: false, first: '\0', separator: ',', named: false, ifEmpty: string.Empty, allowReserved: true);
    private static readonly OpInfo OpInfoDot = new(@default: false, first: '.', separator: '.', named: false, ifEmpty: string.Empty, allowReserved: false);
    private static readonly OpInfo OpInfoSlash = new(@default: false, first: '/', separator: '/', named: false, ifEmpty: string.Empty, allowReserved: false);
    private static readonly OpInfo OpInfoSemicolon = new(@default: false, first: ';', separator: ';', named: true, ifEmpty: string.Empty, allowReserved: false);
    private static readonly OpInfo OpInfoQuery = new(@default: false, first: '?', separator: '&', named: true, ifEmpty: "=", allowReserved: false);
    private static readonly OpInfo OpInfoAmpersand = new(@default: false, first: '&', separator: '&', named: true, ifEmpty: "=", allowReserved: false);
    private static readonly OpInfo OpInfoHash = new(@default: false, first: '#', separator: ',', named: false, ifEmpty: string.Empty, allowReserved: true);

    /// <summary>
    /// A delegate for a callback providing parameter names as they are discovered.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    public delegate void ParameterNameCallback(ReadOnlySpan<char> name);

    /// <summary>
    /// A delegate for a callback providing a resolved template.
    /// </summary>
    /// <param name="resolvedTemplate">The resolved template.</param>
    public delegate void ResolvedUriTemplateCallback(ReadOnlySpan<char> resolvedTemplate);

    private enum States
    {
        CopyingLiterals,
        ParsingExpression,
    }

    private enum VariableProcessingState
    {
        Success,
        NotProcessed,
        Failure,
    }

    /// <summary>
    /// Resolve the template into an output result.
    /// </summary>
    /// <param name="template">The template to resolve.</param>
    /// <param name="resolvePartially">If <see langword="true"/> then partially resolve the result.</param>
    /// <param name="parameters">The parameters to apply to the template.</param>
    /// <param name="callback">The callback which is provided with the resolved template.</param>
    /// <param name="parameterNameCallback">An optional callback which is provided each parameter name as they are discovered.</param>
    /// <returns><see langword="true"/> if the URI matched the template, and the parameters were resolved successfully.</returns>
    public static bool TryResolveResult(ReadOnlySpan<char> template, bool resolvePartially, in JsonAny parameters, ResolvedUriTemplateCallback callback, ParameterNameCallback? parameterNameCallback = null)
    {
        ArrayPoolBufferWriter<char> abw = ArrayPoolWriterPool.Get();
        try
        {
            if (TryResolveResult(template, abw, resolvePartially, parameters, parameterNameCallback))
            {
                callback(abw.WrittenSpan);
                return true;
            }

            return false;
        }
        finally
        {
            ArrayPoolWriterPool.Return(abw);
        }
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
    public static bool TryResolveResult(ReadOnlySpan<char> template, IBufferWriter<char> output, bool resolvePartially, in JsonAny parameters, ParameterNameCallback? parameterNameCallback = null)
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
                            output.Write(template[expressionStart..expressionEnd]);
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
                        if (!ProcessExpression(template[expressionStart..expressionEnd], output, resolvePartially, parameters, parameterNameCallback))
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
            output.Write(template[expressionStart..expressionEnd]);
        }

        return true;
    }

    private static OpInfo GetOperator(char operatorIndicator)
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

    private static bool ProcessExpression(ReadOnlySpan<char> currentExpression, IBufferWriter<char> output, bool resolvePartially, in JsonAny parameters, ParameterNameCallback? parameterNameCallback)
    {
        if (currentExpression.Length == 0)
        {
            return false;
        }

        OpInfo op = GetOperator(currentExpression[0]);

        int firstChar = op.Default ? 0 : 1;
        bool multivariableExpression = false;
        int varNameStart = -1;
        int varNameEnd = -1;

        var varSpec = new VariableSpec(op, ReadOnlySpan<char>.Empty);
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

                    varSpec.PrefixLength = int.Parse(currentExpression[prefixStart..i]);
                    i--;
                    break;

                case ',':
                    varSpec.VarName = currentExpression[varNameStart..varNameEnd];
                    multivariableExpression = true;
                    VariableProcessingState success = ProcessVariable(ref varSpec, output, multivariableExpression, resolvePartially, parameters, parameterNameCallback);
                    bool isFirst = varSpec.First;

                    // Reset for new variable
                    varSpec = new VariableSpec(op, ReadOnlySpan<char>.Empty);
                    varNameStart = -1;
                    varNameEnd = -1;
                    if ((success == VariableProcessingState.Success) || !isFirst || resolvePartially)
                    {
                        varSpec.First = false;
                    }

                    if ((success == VariableProcessingState.NotProcessed) && resolvePartially)
                    {
                        output.Write(',');
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

        VariableProcessingState outerSuccess = ProcessVariable(ref varSpec, output, multivariableExpression, resolvePartially, parameters, parameterNameCallback);

        if (outerSuccess == VariableProcessingState.Failure)
        {
            return false;
        }

        if (multivariableExpression && resolvePartially)
        {
            output.Write('}');
        }

        return true;
    }

    private static VariableProcessingState ProcessVariable(ref VariableSpec varSpec, IBufferWriter<char> output, bool multiVariableExpression, bool resolvePartially, in JsonAny parameters, ParameterNameCallback? parameterNameCallback)
    {
        if (parameterNameCallback is ParameterNameCallback callback)
        {
            callback(varSpec.VarName);
        }

        if (!parameters.TryGetProperty(varSpec.VarName, out JsonAny value)
                || value.IsNullOrUndefined()
                || (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0)
                || (value.ValueKind == JsonValueKind.Object && !value.HasProperties()))
        {
            if (resolvePartially)
            {
                if (multiVariableExpression)
                {
                    if (varSpec.First)
                    {
                        output.Write('{');
                    }

                    varSpec.CopyTo(output);
                }
                else
                {
                    output.Write('{');
                    varSpec.CopyTo(output);
                    output.Write('}');
                }
            }

            return VariableProcessingState.NotProcessed;
        }

        if (varSpec.First)
        {
            if (varSpec.OperatorInfo.First != '\0')
            {
                output.Write(varSpec.OperatorInfo.First);
            }
        }
        else
        {
            output.Write(varSpec.OperatorInfo.Separator);
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            if (varSpec.OperatorInfo.Named && !varSpec.Explode) //// exploding will prefix with list name
            {
                AppendName(output, varSpec.VarName, varSpec.OperatorInfo.IfEmpty, value.GetArrayLength() == 0);
            }

            AppendArray(output, varSpec.OperatorInfo, varSpec.Explode, varSpec.VarName, value);
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            if (varSpec.PrefixLength != 0)
            {
                return VariableProcessingState.Failure;
            }

            if (varSpec.OperatorInfo.Named && !varSpec.Explode) //// exploding will prefix with list name
            {
                AppendName(output, varSpec.VarName, varSpec.OperatorInfo.IfEmpty, !value.HasProperties());
            }

            AppendObject(output, varSpec.OperatorInfo, varSpec.Explode, value);
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            if (varSpec.OperatorInfo.Named)
            {
                AppendNameAndStringValue(output, varSpec.VarName, varSpec.OperatorInfo.IfEmpty, value, varSpec.PrefixLength, varSpec.OperatorInfo.AllowReserved);
            }
            else
            {
                AppendValue(output, value, varSpec.PrefixLength, varSpec.OperatorInfo.AllowReserved);
            }
        }
        else
        {
            if (varSpec.OperatorInfo.Named)
            {
                AppendName(output, varSpec.VarName, varSpec.OperatorInfo.IfEmpty, value.IsNullOrUndefined());
            }

            AppendValue(output, value, varSpec.PrefixLength, varSpec.OperatorInfo.AllowReserved);
        }

        return VariableProcessingState.Success;
    }

    /// <summary>
    /// Append an array to the result.
    /// </summary>
    /// <param name="output">The output buffer.</param>
    /// <param name="op">The operator info.</param>
    /// <param name="explode">Whether to explode the array.</param>
    /// <param name="variable">The variable name.</param>
    /// <param name="array">The array to add.</param>
    private static void AppendArray(IBufferWriter<char> output, in OpInfo op, bool explode, ReadOnlySpan<char> variable, in JsonAny array)
    {
        bool isFirst = true;
        foreach (JsonAny item in array.EnumerateArray())
        {
            if (!isFirst)
            {
                output.Write(explode ? op.Separator : ',');
            }
            else
            {
                isFirst = false;
            }

            if (op.Named && explode)
            {
                output.Write(variable);
                output.Write('=');
            }

            AppendValue(output, item, 0, op.AllowReserved);
        }
    }

    /// <summary>
    /// Append an object to the output.
    /// </summary>
    /// <param name="output">The output buffer.</param>
    /// <param name="op">The operator info.</param>
    /// <param name="explode">Whether to explode the object.</param>
    /// <param name="instance">The object instance to append.</param>
    private static void AppendObject(IBufferWriter<char> output, in OpInfo op, bool explode, in JsonAny instance)
    {
        bool isFirst = true;
        foreach (JsonObjectProperty value in instance.EnumerateObject())
        {
            if (!isFirst)
            {
                if (explode)
                {
                    output.Write(op.Separator);
                }
                else
                {
                    output.Write(',');
                }
            }
            else
            {
                isFirst = false;
            }

            value.TryGetName(WriteEncodedPropertyName, new WriteEncodedPropertyNameState(output, op.AllowReserved), out bool decoded);

            if (explode)
            {
                output.Write('=');
            }
            else
            {
                output.Write(',');
            }

            AppendValue(output, value.Value, 0, op.AllowReserved);
        }
    }

    /// <summary>
    /// Encoded and write the property name to the output.
    /// </summary>
    /// <param name="name">The name to write.</param>
    /// <param name="state">The state for the writer.</param>
    /// <param name="result">Whether the value was written successfully.</param>
    /// <returns><see langword="true"/> if the value was written successfully.</returns>
    private static bool WriteEncodedPropertyName(ReadOnlySpan<char> name, in WriteEncodedPropertyNameState state, out bool result)
    {
        Encode(state.Output, name, state.AllowReserved);
        result = true;
        return true;
    }

    /// <summary>
    /// Append a variable to the result.
    /// </summary>
    /// <param name="output">The output buffer to which the URI template is written.</param>
    /// <param name="variable">The variable name.</param>
    /// <param name="ifEmpty">The string to apply if the value is empty.</param>
    /// <param name="valueIsEmpty">True if the value is empty.</param>
    private static void AppendName(IBufferWriter<char> output, ReadOnlySpan<char> variable, string ifEmpty, bool valueIsEmpty)
    {
        output.Write(variable);

        if (valueIsEmpty)
        {
            output.Write(ifEmpty);
        }
        else
        {
            output.Write('=');
        }
    }

    /// <summary>
    /// Appends a value to the result.
    /// </summary>
    /// <param name="output">The output buffer to which to write the value.</param>
    /// <param name="variable">The variable name.</param>
    /// <param name="ifEmpty">The string to add if the value is empty.</param>
    /// <param name="value">The value to append.</param>
    /// <param name="prefixLength">The prefix length.</param>
    /// <param name="allowReserved">Whether to allow reserved characters.</param>
    private static void AppendNameAndStringValue(IBufferWriter<char> output, ReadOnlySpan<char> variable, string ifEmpty, JsonAny value, int prefixLength, bool allowReserved)
    {
        output.Write(variable);

        if (value.HasJsonElementBacking)
        {
            value.AsJsonElement.TryGetValue(ProcessString, new AppendNameAndValueState(output, ifEmpty, prefixLength, allowReserved), out bool _);
        }
        else
        {
            ProcessString(value.AsSpan(), new AppendNameAndValueState(output, ifEmpty, prefixLength, allowReserved), out bool _);
        }
    }

    /// <summary>
    /// Appends a value to the result.
    /// </summary>
    /// <param name="output">The output buffer to which to write the value.</param>
    /// <param name="value">The value to append.</param>
    /// <param name="prefixLength">The prefix length.</param>
    /// <param name="allowReserved">Whether to allow reserved characters.</param>
    private static void AppendValue(IBufferWriter<char> output, JsonAny value, int prefixLength, bool allowReserved)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            if (value.HasJsonElementBacking)
            {
                value.AsJsonElement.TryGetValue(ProcessString, new AppendValueState(output, prefixLength, allowReserved), out bool _);
            }
            else
            {
                ProcessString(value.AsSpan(), new AppendValueState(output, prefixLength, allowReserved), out bool _);
            }
        }
        else if (value.ValueKind == JsonValueKind.True)
        {
            output.Write("true");
        }
        else if (value.ValueKind == JsonValueKind.False)
        {
            output.Write("false");
        }
        else if (value.ValueKind == JsonValueKind.Null)
        {
            output.Write("null");
        }
        else if (value.ValueKind == JsonValueKind.Number)
        {
            double valueNumber = (double)value;

            // The maximum number of digits in a double precision number is 1074; we allocate a little above this
            Span<char> buffer = stackalloc char[1100];
            valueNumber.TryFormat(buffer, out int written);
            output.Write(buffer[..written]);
        }
    }

    private static bool ProcessString(ReadOnlySpan<char> span, in AppendValueState state, out bool encoded)
    {
        WriteStringValue(state.Output, span, state.PrefixLength, state.AllowReserved);
        encoded = true;
        return true;
    }

    private static bool ProcessString(ReadOnlySpan<char> span, in AppendNameAndValueState state, out bool encoded)
    {
        // Write the name separator
        if (span.Length == 0)
        {
            state.Output.Write(state.IfEmpty);
        }
        else
        {
            state.Output.Write('=');
        }

        WriteStringValue(state.Output, span, state.PrefixLength, state.AllowReserved);
        encoded = true;
        return true;
    }

    private static void WriteStringValue(IBufferWriter<char> output, ReadOnlySpan<char> span, int prefixLength, bool allowReserved)
    {
        // Write the value
        ReadOnlySpan<char> valueString = span;

        if (prefixLength != 0)
        {
            if (prefixLength < valueString.Length)
            {
                valueString = valueString[..prefixLength];
            }
        }

        Encode(output, valueString, allowReserved);
    }

    private static void Encode(IBufferWriter<char> output, ReadOnlySpan<char> p, bool allowReserved)
    {
        foreach (char c in p)
        {
            if ((c >= 'A' && c <= 'z') ////                                     Alpha
                || (c >= '0' && c <= '9') ////                                  Digit
                || UriUnreservedSymbols.IndexOf(c) != -1 ////                   Unreserved symbols  - These should never be percent encoded
                || (allowReserved && UriReservedSymbols.IndexOf(c) != -1)) //// Reserved symbols - should be included if requested (+)
            {
                output.Write(c);
            }
            else
            {
                WriteHexDigits(output, c);
            }
        }

        static void WriteHexDigits(IBufferWriter<char> output, char c)
        {
            Span<char> source = stackalloc char[1];
            source[0] = c;
            Span<byte> bytes = stackalloc byte[Encoding.UTF8.GetMaxByteCount(1)];
            int encoded = Encoding.UTF8.GetBytes(source, bytes);
            foreach (byte abyte in bytes[..encoded])
            {
                output.Write('%');
                output.Write(HexDigits[(abyte & 240) >> 4]);
                output.Write(HexDigits[abyte & 15]);
            }
        }
    }

    /// <summary>
    /// Gets the operator info for a variable specification.
    /// </summary>
    private readonly struct OpInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpInfo"/> struct.
        /// </summary>
        /// <param name="default">The defualt value.</param>
        /// <param name="first">If this is the first parameter.</param>
        /// <param name="separator">The separator.</param>
        /// <param name="named">If this is a named parameter.</param>
        /// <param name="ifEmpty">The value to use if empty.</param>
        /// <param name="allowReserved">Whether to allow reserved characters.</param>
        public OpInfo(bool @default, char first, char separator, bool named, string ifEmpty, bool allowReserved)
        {
            this.Default = @default;
            this.First = first;
            this.Separator = separator;
            this.Named = named;
            this.IfEmpty = ifEmpty;
            this.AllowReserved = allowReserved;
        }

        /// <summary>
        /// Gets a value indicating whether this is default.
        /// </summary>
        public bool Default { get; }

        /// <summary>
        /// Gets the first element.
        /// </summary>
        public char First { get; }

        /// <summary>
        /// Gets the separator.
        /// </summary>
        public char Separator { get; }

        /// <summary>
        /// Gets a value indicating whether this is named.
        /// </summary>
        public bool Named { get; }

        /// <summary>
        /// Gets the string to use if empty.
        /// </summary>
        public string IfEmpty { get; }

        /// <summary>
        /// Gets a value indicating whether this allows reserved symbols.
        /// </summary>
        public bool AllowReserved { get; }
    }

    /// <summary>
    /// A variable specification.
    /// </summary>
    private ref struct VariableSpec
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VariableSpec"/> struct.
        /// </summary>
        /// <param name="operatorInfo">The operator info.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="explode">Whether to explode the variable.</param>
        /// <param name="prefixLength">The prefix length.</param>
        /// <param name="first">Wether this is the first variable in the template.</param>
        public VariableSpec(OpInfo operatorInfo, ReadOnlySpan<char> variableName, bool explode = false, int prefixLength = 0, bool first = true)
        {
            this.OperatorInfo = operatorInfo;
            this.Explode = explode;
            this.PrefixLength = prefixLength;
            this.First = first;
            this.VarName = variableName;
        }

        /// <summary>
        /// Gets the operation info.
        /// </summary>
        public OpInfo OperatorInfo { get; }

        /// <summary>
        /// Gets or sets the variable name.
        /// </summary>
        public ReadOnlySpan<char> VarName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this variable is exploded.
        /// </summary>
        public bool Explode { get; set; }

        /// <summary>
        /// Gets or sets the prefix length for the variable.
        /// </summary>
        public int PrefixLength { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is the first variable in the template.
        /// </summary>
        public bool First { get; set; }

        /// <summary>
        /// Copy the result to the output span.
        /// </summary>
        /// <param name="output">The span to which to copy the result.</param>
        public void CopyTo(IBufferWriter<char> output)
        {
            int written = 0;
            if (this.First)
            {
                if (this.OperatorInfo.First != '\0')
                {
                    output.Write(this.OperatorInfo.First);
                }
            }

            output.Write(this.VarName);
            written += this.VarName.Length;

            if (this.Explode)
            {
                output.Write('*');
            }

            if (this.PrefixLength > 0)
            {
                output.Write(':');

                Span<char> buffer = stackalloc char[256];
                this.PrefixLength.TryFormat(buffer, out int charsWritten);
                output.Write(buffer[..charsWritten]);
            }
        }

        /// <summary>
        /// Gets the variable as a string.
        /// </summary>
        /// <returns>The variable specification as a string.</returns>
        public override string ToString()
        {
            StringBuilder builder = StringBuilderPool.Shared.Get();
            if (this.First)
            {
                builder.Append(this.OperatorInfo.First);
            }

            builder.Append(this.VarName);
            if (this.Explode)
            {
                builder.Append('*');
            }

            if (this.PrefixLength > 0)
            {
                builder.Append(':');
                builder.Append(this.PrefixLength);
            }

            string result = builder.ToString();
            StringBuilderPool.Shared.Return(builder);
            return result;
        }
    }

    private readonly record struct AppendValueState(IBufferWriter<char> Output, int PrefixLength, bool AllowReserved)
    {
        public static implicit operator (IBufferWriter<char> Output, int PrefixLength, bool AllowReserved)(AppendValueState value)
        {
            return (value.Output, value.PrefixLength, value.AllowReserved);
        }

        public static implicit operator AppendValueState((IBufferWriter<char> Output, int PrefixLength, bool AllowReserved) value)
        {
            return new AppendValueState(value.Output, value.PrefixLength, value.AllowReserved);
        }
    }

    private readonly record struct AppendNameAndValueState(IBufferWriter<char> Output, string IfEmpty, int PrefixLength, bool AllowReserved)
    {
        public static implicit operator (IBufferWriter<char> Output, string IfEmpty, int PrefixLength, bool AllowReserved)(AppendNameAndValueState value)
        {
            return (value.Output, value.IfEmpty, value.PrefixLength, value.AllowReserved);
        }

        public static implicit operator AppendNameAndValueState((IBufferWriter<char> Output, string IfEmpty, int PrefixLength, bool AllowReserved) value)
        {
            return new AppendNameAndValueState(value.Output, value.IfEmpty, value.PrefixLength, value.AllowReserved);
        }
    }

    private readonly record struct WriteEncodedPropertyNameState(IBufferWriter<char> Output, bool AllowReserved)
    {
        public static implicit operator (IBufferWriter<char> Output, bool AllowReserved)(WriteEncodedPropertyNameState value)
        {
            return (value.Output, value.AllowReserved);
        }

        public static implicit operator WriteEncodedPropertyNameState((IBufferWriter<char> Output, bool AllowReserved) value)
        {
            return new WriteEncodedPropertyNameState(value.Output, value.AllowReserved);
        }
    }
}