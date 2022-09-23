// <copyright file="JsonTemplateParameterProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text.Json;
using CommunityToolkit.HighPerformance;
using Corvus.UriTemplates.TemplateParameterProviders;

namespace Corvus.Json.UriTemplates;

/// <summary>
/// Implements a parameter provider over a JsonAny.
/// </summary>
internal class JsonTemplateParameterProvider : ITemplateParameterProvider<JsonAny>
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
    public static VariableProcessingState ProcessVariable(ref VariableSpecification variableSpecification, in JsonAny parameters, IBufferWriter<char> output)
    {
        if (!parameters.TryGetProperty(variableSpecification.VarName, out JsonAny value)
                || value.IsNullOrUndefined()
                || (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0)
                || (value.ValueKind == JsonValueKind.Object && !value.HasProperties()))
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

        if (value.ValueKind == JsonValueKind.Array)
        {
            if (variableSpecification.OperatorInfo.Named && !variableSpecification.Explode) //// exploding will prefix with list name
            {
                AppendName(output, variableSpecification.VarName, variableSpecification.OperatorInfo.IfEmpty, value.GetArrayLength() == 0);
            }

            AppendArray(output, variableSpecification.OperatorInfo, variableSpecification.Explode, variableSpecification.VarName, value);
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            if (variableSpecification.PrefixLength != 0)
            {
                return VariableProcessingState.Failure;
            }

            if (variableSpecification.OperatorInfo.Named && !variableSpecification.Explode) //// exploding will prefix with list name
            {
                AppendName(output, variableSpecification.VarName, variableSpecification.OperatorInfo.IfEmpty, !value.HasProperties());
            }

            AppendObject(output, variableSpecification.OperatorInfo, variableSpecification.Explode, value);
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            if (variableSpecification.OperatorInfo.Named)
            {
                AppendNameAndStringValue(output, variableSpecification.VarName, variableSpecification.OperatorInfo.IfEmpty, value, variableSpecification.PrefixLength, variableSpecification.OperatorInfo.AllowReserved);
            }
            else
            {
                AppendValue(output, value, variableSpecification.PrefixLength, variableSpecification.OperatorInfo.AllowReserved);
            }
        }
        else
        {
            if (variableSpecification.OperatorInfo.Named)
            {
                AppendName(output, variableSpecification.VarName, variableSpecification.OperatorInfo.IfEmpty, value.IsNullOrUndefined());
            }

            AppendValue(output, value, variableSpecification.PrefixLength, variableSpecification.OperatorInfo.AllowReserved);
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
    private static void AppendArray(IBufferWriter<char> output, in OperatorInfo op, bool explode, ReadOnlySpan<char> variable, in JsonAny array)
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
    private static void AppendObject(IBufferWriter<char> output, in OperatorInfo op, bool explode, in JsonAny instance)
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

        TemplateParameterProvider.Encode(output, valueString, allowReserved);
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