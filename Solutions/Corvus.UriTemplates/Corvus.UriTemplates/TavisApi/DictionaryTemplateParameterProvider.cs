// <copyright file="DictionaryTemplateParameterProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.HighPerformance;
using Corvus.UriTemplates.TemplateParameterProviders;

namespace Corvus.UriTemplates.TavisApi;

/// <summary>
/// Implements a parameter provider over a JsonAny.
/// </summary>
internal class DictionaryTemplateParameterProvider : ITemplateParameterProvider<IDictionary<string, object?>>
{
    /// <summary>
    /// Process the given variable.
    /// </summary>
    /// <param name="variableSpecification">The specification for the variable.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="output">The output to which to format the parameter.</param>
    /// <returns>
    ///     <see cref="VariableProcessingState.Success"/> if the variable was successfully processed,
    ///     <see cref="VariableProcessingState.NotProcessed"/> if the parameter was not present, or
    ///     <see cref="VariableProcessingState.Failure"/> if the parmeter could not be processed because it was incompatible with the variable specification in the template.</returns>
#if NETSTANDARD2_1
    public VariableProcessingState ProcessVariable(ref VariableSpecification variableSpecification, in IDictionary<string, object?> parameters, IBufferWriter<char> output)
#else
    public static VariableProcessingState ProcessVariable(ref VariableSpecification variableSpecification, in IDictionary<string, object?> parameters, IBufferWriter<char> output)
#endif
    {
        string varName = variableSpecification.VarName.ToString();
        if (!parameters.ContainsKey(varName)
            || parameters[varName] == null
            || (TryGetList(parameters[varName], out IList? l) && l.Count == 0)
            || (TryGetDictionary(parameters[varName], out IDictionary<string, string>? d) && d.Count == 0))
        {
            return VariableProcessingState.NotProcessed;
        }

        if (variableSpecification.First)
        {
            if (variableSpecification.OperatorInfo.First != '\0')
            {
                output.Write(variableSpecification.OperatorInfo.First);
            }
        }
        else
        {
            output.Write(variableSpecification.OperatorInfo.Separator);
        }

        object? value = parameters[varName];

        if (TryGetList(value, out IList? list))
        {
            if (variableSpecification.OperatorInfo.Named && !variableSpecification.Explode) //// exploding will prefix with list name
            {
                AppendName(output, variableSpecification.VarName, variableSpecification.OperatorInfo.IfEmpty, list.Count == 0);
            }

            AppendArray(output, variableSpecification.OperatorInfo, variableSpecification.Explode, variableSpecification.VarName, list);
        }
        else if (TryGetDictionary(value, out IDictionary<string, string>? dictionary))
        {
            if (variableSpecification.PrefixLength != 0)
            {
                return VariableProcessingState.Failure;
            }

            if (variableSpecification.OperatorInfo.Named && !variableSpecification.Explode) //// exploding will prefix with list name
            {
                AppendName(output, variableSpecification.VarName, variableSpecification.OperatorInfo.IfEmpty, dictionary.Count == 0);
            }

            AppendObject(output, variableSpecification.OperatorInfo, variableSpecification.Explode, dictionary);
        }
        else if (value is string stringValue)
        {
            if (variableSpecification.OperatorInfo.Named)
            {
                AppendNameAndStringValue(output, variableSpecification.VarName, variableSpecification.OperatorInfo.IfEmpty, stringValue, variableSpecification.PrefixLength, variableSpecification.OperatorInfo.AllowReserved);
            }
            else
            {
                AppendValue(output, stringValue, variableSpecification.PrefixLength, variableSpecification.OperatorInfo.AllowReserved);
            }
        }
        else
        {
            // Fallback to string
            string? fallbackStringValue = value?.ToString();
            if (variableSpecification.OperatorInfo.Named)
            {
                AppendName(output, variableSpecification.VarName, variableSpecification.OperatorInfo.IfEmpty, string.IsNullOrEmpty(fallbackStringValue));
            }

            AppendValue(output, fallbackStringValue, variableSpecification.PrefixLength, variableSpecification.OperatorInfo.AllowReserved);
        }

        return VariableProcessingState.Success;
    }

    private static bool TryGetDictionary(object? value, [NotNullWhen(true)] out IDictionary<string, string>? dictionary)
    {
        if (value is IDictionary<string, string> result)
        {
            dictionary = result;
            return true;
        }

        dictionary = null;
        return false;
    }

    private static bool TryGetList(object? value, [NotNullWhen(true)] out IList? list)
    {
        var result = value as IList;
        if (result == null && value is IEnumerable<string> enumerable)
        {
            result = enumerable.ToList();
        }

        list = result;
        return result != null;
    }

    /// <summary>
    /// Append an array to the result.
    /// </summary>
    /// <param name="output">The output buffer.</param>
    /// <param name="op">The operator info.</param>
    /// <param name="explode">Whether to explode the array.</param>
    /// <param name="variable">The variable name.</param>
    /// <param name="array">The array to add.</param>
    private static void AppendArray(IBufferWriter<char> output, in OperatorInfo op, bool explode, ReadOnlySpan<char> variable, in IList array)
    {
        bool isFirst = true;
        foreach (object item in array)
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

            AppendValue(output, item.ToString(), 0, op.AllowReserved);
        }
    }

    /// <summary>
    /// Append an object to the output.
    /// </summary>
    /// <param name="output">The output buffer.</param>
    /// <param name="op">The operator info.</param>
    /// <param name="explode">Whether to explode the object.</param>
    /// <param name="instance">The object instance to append.</param>
    private static void AppendObject(IBufferWriter<char> output, in OperatorInfo op, bool explode, in IDictionary<string, string> instance)
    {
        bool isFirst = true;
        foreach (KeyValuePair<string, string> value in instance)
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

            WriteEncodedPropertyName(value.Key.AsSpan(), new WriteEncodedPropertyNameState(output, op.AllowReserved), out bool decoded);

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
        TemplateParameterProvider.Encode(state.Output, name, state.AllowReserved);
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
    private static void AppendNameAndStringValue(IBufferWriter<char> output, ReadOnlySpan<char> variable, string ifEmpty, string? value, int prefixLength, bool allowReserved)
    {
        output.Write(variable);
        ProcessString(value.AsSpan(), new AppendNameAndValueState(output, ifEmpty, prefixLength, allowReserved), out bool _);
    }

    /// <summary>
    /// Appends a value to the result.
    /// </summary>
    /// <param name="output">The output buffer to which to write the value.</param>
    /// <param name="value">The value to append.</param>
    /// <param name="prefixLength">The prefix length.</param>
    /// <param name="allowReserved">Whether to allow reserved characters.</param>
    private static void AppendValue(IBufferWriter<char> output, string? value, int prefixLength, bool allowReserved)
    {
        ProcessString(value.AsSpan(), new AppendValueState(output, prefixLength, allowReserved), out bool _);
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

        TemplateParameterProvider.Encode(output, valueString, allowReserved);
    }

    private readonly struct AppendValueState
    {
        public AppendValueState(IBufferWriter<char> output, int prefixLength, bool allowReserved)
        {
            this.Output = output;
            this.PrefixLength = prefixLength;
            this.AllowReserved = allowReserved;
        }

        public IBufferWriter<char> Output { get; }

        public int PrefixLength { get; }

        public bool AllowReserved { get; }

        public static implicit operator (IBufferWriter<char> Output, int PrefixLength, bool AllowReserved)(AppendValueState value)
        {
            return (value.Output, value.PrefixLength, value.AllowReserved);
        }

        public static implicit operator AppendValueState((IBufferWriter<char> Output, int PrefixLength, bool AllowReserved) value)
        {
            return new AppendValueState(value.Output, value.PrefixLength, value.AllowReserved);
        }
    }

    private readonly struct AppendNameAndValueState
    {
        public AppendNameAndValueState(IBufferWriter<char> output, string ifEmpty, int prefixLength, bool allowReserved)
        {
            this.Output = output;
            this.IfEmpty = ifEmpty;
            this.PrefixLength = prefixLength;
            this.AllowReserved = allowReserved;
        }

        public IBufferWriter<char> Output { get; }

        public string IfEmpty { get; }

        public int PrefixLength { get; }

        public bool AllowReserved { get; }

        public static implicit operator (IBufferWriter<char> Output, string IfEmpty, int PrefixLength, bool AllowReserved)(AppendNameAndValueState value)
        {
            return (value.Output, value.IfEmpty, value.PrefixLength, value.AllowReserved);
        }

        public static implicit operator AppendNameAndValueState((IBufferWriter<char> Output, string IfEmpty, int PrefixLength, bool AllowReserved) value)
        {
            return new AppendNameAndValueState(value.Output, value.IfEmpty, value.PrefixLength, value.AllowReserved);
        }
    }

    private readonly struct WriteEncodedPropertyNameState
    {
        public WriteEncodedPropertyNameState(IBufferWriter<char> output, bool allowReserved)
        {
            this.Output = output;
            this.AllowReserved = allowReserved;
        }

        public IBufferWriter<char> Output { get; }

        public bool AllowReserved { get; }

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