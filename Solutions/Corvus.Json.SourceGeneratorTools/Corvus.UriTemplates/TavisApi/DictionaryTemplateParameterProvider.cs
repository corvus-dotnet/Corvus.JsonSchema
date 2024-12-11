// <copyright file="DictionaryTemplateParameterProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections;
using System.Diagnostics.CodeAnalysis;

using Corvus.HighPerformance;
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
    public VariableProcessingState ProcessVariable(ref VariableSpecification variableSpecification, in IDictionary<string, object?> parameters, ref ValueStringBuilder output)
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
                output.Append(variableSpecification.OperatorInfo.First);
            }
        }
        else
        {
            output.Append(variableSpecification.OperatorInfo.Separator);
        }

        object? value = parameters[varName];

        if (TryGetList(value, out IList? list))
        {
            if (variableSpecification.OperatorInfo.Named && !variableSpecification.Explode) //// exploding will prefix with list name
            {
                AppendName(ref output, variableSpecification.VarName, variableSpecification.OperatorInfo.IfEmpty, list.Count == 0);
            }

            AppendArray(ref output, variableSpecification.OperatorInfo, variableSpecification.Explode, variableSpecification.VarName, list);
        }
        else if (TryGetDictionary(value, out IDictionary<string, string>? dictionary))
        {
            if (variableSpecification.PrefixLength != 0)
            {
                return VariableProcessingState.Failure;
            }

            if (variableSpecification.OperatorInfo.Named && !variableSpecification.Explode) //// exploding will prefix with list name
            {
                AppendName(ref output, variableSpecification.VarName, variableSpecification.OperatorInfo.IfEmpty, dictionary.Count == 0);
            }

            AppendObject(ref output, variableSpecification.OperatorInfo, variableSpecification.Explode, dictionary);
        }
        else if (value is string stringValue)
        {
            if (variableSpecification.OperatorInfo.Named)
            {
                AppendNameAndStringValue(ref output, variableSpecification.VarName, variableSpecification.OperatorInfo.IfEmpty, stringValue, variableSpecification.PrefixLength, variableSpecification.OperatorInfo.AllowReserved);
            }
            else
            {
                AppendValue(ref output, stringValue, variableSpecification.PrefixLength, variableSpecification.OperatorInfo.AllowReserved);
            }
        }
        else
        {
            // Fallback to string
            string? fallbackStringValue = value?.ToString();
            if (variableSpecification.OperatorInfo.Named)
            {
                AppendName(ref output, variableSpecification.VarName, variableSpecification.OperatorInfo.IfEmpty, string.IsNullOrEmpty(fallbackStringValue));
            }

            AppendValue(ref output, fallbackStringValue, variableSpecification.PrefixLength, variableSpecification.OperatorInfo.AllowReserved);
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
    private static void AppendArray(ref ValueStringBuilder output, in OperatorInfo op, bool explode, ReadOnlySpan<char> variable, in IList array)
    {
        bool isFirst = true;
        foreach (object item in array)
        {
            if (!isFirst)
            {
                output.Append(explode ? op.Separator : ',');
            }
            else
            {
                isFirst = false;
            }

            if (op.Named && explode)
            {
                output.Append(variable);
                output.Append('=');
            }

            AppendValue(ref output, item.ToString(), 0, op.AllowReserved);
        }
    }

    /// <summary>
    /// Append an object to the output.
    /// </summary>
    /// <param name="output">The output buffer.</param>
    /// <param name="op">The operator info.</param>
    /// <param name="explode">Whether to explode the object.</param>
    /// <param name="instance">The object instance to append.</param>
    private static void AppendObject(ref ValueStringBuilder output, in OperatorInfo op, bool explode, in IDictionary<string, string> instance)
    {
        bool isFirst = true;
        foreach (KeyValuePair<string, string> value in instance)
        {
            if (!isFirst)
            {
                if (explode)
                {
                    output.Append(op.Separator);
                }
                else
                {
                    output.Append(',');
                }
            }
            else
            {
                isFirst = false;
            }

            WriteEncodedPropertyName(value.Key.AsSpan(), ref output, op.AllowReserved, out bool decoded);

            if (explode)
            {
                output.Append('=');
            }
            else
            {
                output.Append(',');
            }

            AppendValue(ref output, value.Value, 0, op.AllowReserved);
        }
    }

    /// <summary>
    /// Encoded and write the property name to the output.
    /// </summary>
    /// <param name="name">The name to write.</param>
    /// <param name="output">The output buffer.</param>
    /// <param name="allowReserved">A value indicating whether to allow reserved characters.</param>
    /// <param name="result">Whether the value was written successfully.</param>
    /// <returns><see langword="true"/> if the value was written successfully.</returns>
    private static bool WriteEncodedPropertyName(ReadOnlySpan<char> name, ref ValueStringBuilder output, bool allowReserved, out bool result)
    {
        TemplateParameterProvider.Encode(ref output, name, allowReserved);
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
    private static void AppendName(ref ValueStringBuilder output, ReadOnlySpan<char> variable, string ifEmpty, bool valueIsEmpty)
    {
        output.Append(variable);

        if (valueIsEmpty)
        {
           output.Append(ifEmpty);
        }
        else
        {
            output.Append('=');
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
    private static void AppendNameAndStringValue(ref ValueStringBuilder output, ReadOnlySpan<char> variable, string ifEmpty, string? value, int prefixLength, bool allowReserved)
    {
        output.Append(variable);

        ReadOnlySpan<char> span = value.AsSpan();

        // Write the name separator
        if (span.Length == 0)
        {
           output.Append(ifEmpty);
        }
        else
        {
            output.Append('=');
        }

        WriteStringValue(ref output, span, prefixLength, allowReserved);
    }

    /// <summary>
    /// Appends a value to the result.
    /// </summary>
    /// <param name="output">The output buffer to which to write the value.</param>
    /// <param name="value">The value to append.</param>
    /// <param name="prefixLength">The prefix length.</param>
    /// <param name="allowReserved">Whether to allow reserved characters.</param>
    private static void AppendValue(ref ValueStringBuilder output, string? value, int prefixLength, bool allowReserved)
    {
        WriteStringValue(ref output, value.AsSpan(), prefixLength, allowReserved);
    }

    private static void WriteStringValue(ref ValueStringBuilder output, ReadOnlySpan<char> span, int prefixLength, bool allowReserved)
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

        TemplateParameterProvider.Encode(ref output, valueString, allowReserved);
    }
}