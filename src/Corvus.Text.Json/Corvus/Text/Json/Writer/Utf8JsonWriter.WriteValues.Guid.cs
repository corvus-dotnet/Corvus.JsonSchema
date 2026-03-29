// <copyright file="Utf8JsonWriter.WriteValues.Guid.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

public sealed partial class Utf8JsonWriter
{
    /// <summary>
    /// Writes the <see cref="Guid"/> value (as a JSON string) as an element of a JSON array.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if this would result in invalid JSON being written (while validation is enabled).
    /// </exception>
    /// <remarks>
    /// Writes the <see cref="Guid"/> using the default <see cref="StandardFormat"/> (that is, 'D'), as the form: nnnnnnnn-nnnn-nnnn-nnnn-nnnnnnnnnnnn.
    /// </remarks>
    public void WriteStringValue(Guid value)
    {
        if (!_options.SkipValidation)
        {
            ValidateWritingValue();
        }

        if (_options.Indented)
        {
            WriteStringValueIndented(value);
        }
        else
        {
            WriteStringValueMinimized(value);
        }

        SetFlagToAddListSeparatorBeforeNextItem();
        _tokenType = JsonTokenType.String;
    }

    private void WriteStringValueIndented(Guid value)
    {
        int indent = Indentation;
        Debug.Assert(indent <= _indentLength * _options.MaxDepth);

        // 2 quotes, and optionally, 1 list separator and 1-2 bytes for new line
        int maxRequired = indent + JsonConstants.MaximumFormatGuidLength + 3 + _newLineLength;

        if (_memory.Length - BytesPending < maxRequired)
        {
            Grow(maxRequired);
        }

        Span<byte> output = _memory.Span;

        if (_currentDepth < 0)
        {
            output[BytesPending++] = JsonConstants.ListSeparator;
        }

        if (_tokenType != JsonTokenType.PropertyName)
        {
            if (_tokenType != JsonTokenType.None)
            {
                WriteNewLine(output);
            }

            WriteIndentation(output.Slice(BytesPending), indent);
            BytesPending += indent;
        }

        output[BytesPending++] = JsonConstants.Quote;

        bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out int bytesWritten);
        Debug.Assert(result);
        BytesPending += bytesWritten;

        output[BytesPending++] = JsonConstants.Quote;
    }

    private void WriteStringValueMinimized(Guid value)
    {
        const int maxRequired = JsonConstants.MaximumFormatGuidLength + 3; // 2 quotes, and optionally, 1 list separator

        if (_memory.Length - BytesPending < maxRequired)
        {
            Grow(maxRequired);
        }

        Span<byte> output = _memory.Span;

        if (_currentDepth < 0)
        {
            output[BytesPending++] = JsonConstants.ListSeparator;
        }

        output[BytesPending++] = JsonConstants.Quote;

        bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out int bytesWritten);
        Debug.Assert(result);
        BytesPending += bytesWritten;

        output[BytesPending++] = JsonConstants.Quote;
    }
}