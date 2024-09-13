// <copyright file="JsonTemplateParameterProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.HighPerformance;
using Corvus.UriTemplates.TemplateParameterProviders;

namespace Corvus.Json.UriTemplates;

/// <summary>
/// Implements a parameter provider over a JsonAny.
/// </summary>
/// <typeparam name="TPayload">The type of the payload.</typeparam>
internal class JsonTemplateParameterProvider<TPayload> : ITemplateParameterProvider<TPayload>
    where TPayload : struct, IJsonObject<TPayload>
{
    /// <summary>
    /// Gets the instance of the <see cref="JsonTemplateParameterProvider{TPayload}"/>.
    /// </summary>
    internal static JsonTemplateParameterProvider<TPayload> Instance { get; } = new();

    /// <summary>
    /// Process the given variable.
    /// </summary>
    /// <param name="variableSpecification">The specification for the variable.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="output">The output to which to format the parameter.</param>
    /// <returns>
    ///     <see cref="VariableProcessingState.Success"/> if the variable was successfully processed,
    ///     <see cref="VariableProcessingState.NotProcessed"/> if the parameter was not present, or
    ///     <see cref="VariableProcessingState.Failure"/> if the parameter could not be processed because it was incompatible with the variable specification in the template.</returns>
    public VariableProcessingState ProcessVariable(ref VariableSpecification variableSpecification, in TPayload parameters, ref ValueStringBuilder output)
    {
        if (!parameters.TryGetProperty(variableSpecification.VarName, out JsonAny value)
                || value.IsNullOrUndefined()
                || (value.ValueKind == JsonValueKind.Array && value.AsArray.GetArrayLength() == 0)
                || (value.ValueKind == JsonValueKind.Object && !value.AsObject.HasProperties()))
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

        if (value.ValueKind == JsonValueKind.Array)
        {
            if (variableSpecification.OperatorInfo.Named && !variableSpecification.Explode) //// exploding will prefix with list name
            {
                AppendName(ref output, variableSpecification.VarName, variableSpecification.OperatorInfo.IfEmpty, false);
            }

            AppendArray(ref output, variableSpecification.OperatorInfo, variableSpecification.Explode, variableSpecification.VarName, value.AsArray);
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            if (variableSpecification.PrefixLength != 0)
            {
                return VariableProcessingState.Failure;
            }

            if (variableSpecification.OperatorInfo.Named && !variableSpecification.Explode) //// exploding will prefix with list name
            {
                AppendName(ref output, variableSpecification.VarName, variableSpecification.OperatorInfo.IfEmpty, false);
            }

            AppendObject(ref output, variableSpecification.OperatorInfo, variableSpecification.Explode, value.AsObject);
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            if (variableSpecification.OperatorInfo.Named)
            {
                AppendNameAndStringValue(ref output, variableSpecification.VarName, variableSpecification.OperatorInfo.IfEmpty, value.AsString, variableSpecification.PrefixLength, variableSpecification.OperatorInfo.AllowReserved);
            }
            else
            {
                AppendValue(ref output, value, variableSpecification.PrefixLength, variableSpecification.OperatorInfo.AllowReserved);
            }
        }
        else
        {
            if (variableSpecification.OperatorInfo.Named)
            {
                AppendName(ref output, variableSpecification.VarName, variableSpecification.OperatorInfo.IfEmpty, false);
            }

            AppendValue(ref output, value, variableSpecification.PrefixLength, variableSpecification.OperatorInfo.AllowReserved);
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
    private static void AppendArray(ref ValueStringBuilder output, in OperatorInfo op, bool explode, ReadOnlySpan<char> variable, in JsonArray array)
    {
        bool isFirst = true;
        foreach (JsonAny item in array.EnumerateArray())
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

            AppendValue(ref output, item, 0, op.AllowReserved);
        }
    }

    /// <summary>
    /// Append an object to the output.
    /// </summary>
    /// <param name="output">The output buffer.</param>
    /// <param name="op">The operator info.</param>
    /// <param name="explode">Whether to explode the object.</param>
    /// <param name="instance">The object instance to append.</param>
    private static void AppendObject(ref ValueStringBuilder output, in OperatorInfo op, bool explode, in JsonObject instance)
    {
        bool isFirst = true;
        foreach (JsonObjectProperty value in instance.EnumerateObject())
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

            value.TryGetName(ProcessString, new AppendValueState(0, op.AllowReserved), out ProcessingResult result);
            output.Append(result.Span);
            result.Dispose();

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
    private static void AppendNameAndStringValue(ref ValueStringBuilder output, ReadOnlySpan<char> variable, string ifEmpty, JsonString value, int prefixLength, bool allowReserved)
    {
        output.Append(variable);
        value.TryGetValue(ProcessString, new AppendNameAndValueState(ifEmpty, prefixLength, allowReserved), out ProcessingResult result);
        output.Append(result.Span);
        result.Dispose();
    }

    /// <summary>
    /// Appends a value to the result.
    /// </summary>
    /// <param name="output">The output buffer to which to write the value.</param>
    /// <param name="value">The value to append.</param>
    /// <param name="prefixLength">The prefix length.</param>
    /// <param name="allowReserved">Whether to allow reserved characters.</param>
    private static void AppendValue(ref ValueStringBuilder output, JsonAny value, int prefixLength, bool allowReserved)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                value.AsString.TryGetValue(ProcessString, new AppendValueState(prefixLength, allowReserved), out ProcessingResult result);
                output.Append(result.Span);
                result.Dispose();
                break;
            case JsonValueKind.True:
                output.Append("true");
                break;
            case JsonValueKind.False:
                output.Append("false");
                break;
            case JsonValueKind.Null:
                output.Append("null");
                break;
            case JsonValueKind.Number:
                {
                    double valueNumber = (double)value.AsNumber;

#if NET8_0_OR_GREATER
                    // The maximum number of digits in a double precision number is 1074; we allocate a little above this
                    Span<char> buffer = stackalloc char[1100];
                    valueNumber.TryFormat(buffer, out int written);
                    output.Append(buffer[..written]);
#else
                    output.Append(valueNumber.ToString());
#endif
                    break;
                }
        }
    }

    private static bool ProcessString(ReadOnlySpan<char> span, in AppendValueState state, out ProcessingResult result)
    {
        ValueStringBuilder output = new(span.Length * 2);
        WriteStringValue(ref output, span, state.PrefixLength, state.AllowReserved);
        (char[]? Buffer, int Length) vsbOutput = output.GetRentedBufferAndLengthAndDispose();
        result = new(vsbOutput.Buffer, vsbOutput.Length);
        return true;
    }

    private static bool ProcessString(ReadOnlySpan<char> span, in AppendNameAndValueState state, out ProcessingResult result)
    {
        ValueStringBuilder output;

        // Write the name separator
        if (span.Length == 0)
        {
            output = new(state.IfEmpty.Length * 4);
            output.Append(state.IfEmpty);
        }
        else
        {
            output = new(span.Length * 4);
            output.Append('=');
        }

        WriteStringValue(ref output, span, state.PrefixLength, state.AllowReserved);

        (char[]? Buffer, int Length) vsbOutput = output.GetRentedBufferAndLengthAndDispose();
        result = new(vsbOutput.Buffer, vsbOutput.Length);
        return true;
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

    private readonly struct AppendValueState
    {
        public AppendValueState(int prefixLength, bool allowReserved)
        {
            this.PrefixLength = prefixLength;
            this.AllowReserved = allowReserved;
        }

        public int PrefixLength { get; }

        public bool AllowReserved { get; }
    }

    private readonly struct AppendNameAndValueState
    {
        public AppendNameAndValueState(string ifEmpty, int prefixLength, bool allowReserved)
        {
            this.IfEmpty = ifEmpty;
            this.PrefixLength = prefixLength;
            this.AllowReserved = allowReserved;
        }

        public string IfEmpty { get; }

        public int PrefixLength { get; }

        public bool AllowReserved { get; }
    }

    private readonly struct WriteEncodedPropertyNameState
    {
        public WriteEncodedPropertyNameState(bool allowReserved)
        {
            this.AllowReserved = allowReserved;
        }

        public bool AllowReserved { get; }
    }

    private readonly struct ProcessingResult
    {
        private readonly char[]? rentedResult;
        private readonly int written;

        public ProcessingResult(char[]? rentedResult, int written)
        {
            this.rentedResult = rentedResult;
            this.written = written;
        }

        public ReadOnlySpan<char> Span => this.rentedResult.AsSpan(0, this.written);

        public void Dispose()
        {
            ValueStringBuilder.ReturnRentedBuffer(this.rentedResult);
        }
    }
}