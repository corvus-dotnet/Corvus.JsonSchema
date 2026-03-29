// <copyright file="Utf8JsonWriter.WriteValues.DateTimeOffset.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Diagnostics;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

public sealed partial class Utf8JsonWriter
{
    /// <summary>
    /// Writes the <see cref="DateTimeOffset"/> value (as a JSON string) as an element of a JSON array.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if this would result in invalid JSON being written (while validation is enabled).
    /// </exception>
    /// <remarks>
    /// Writes the <see cref="DateTimeOffset"/> using the round-trippable ('O') <see cref="StandardFormat"/>, for example: 2017-06-12T05:30:45.7680000-07:00.
    /// </remarks>
    public void WriteStringValue(DateTimeOffset value)
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

    private void WriteStringValueIndented(DateTimeOffset value)
    {
        int indent = Indentation;
        Debug.Assert(indent <= _indentLength * _options.MaxDepth);

        // 2 quotes, and optionally, 1 list separator and 1-2 bytes for new line
        int maxRequired = indent + JsonConstants.MaximumFormatDateTimeOffsetLength + 3 + _newLineLength;

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

        JsonWriterHelper.WriteDateTimeOffsetTrimmed(output.Slice(BytesPending), value, out int bytesWritten);
        BytesPending += bytesWritten;

        output[BytesPending++] = JsonConstants.Quote;
    }

    private void WriteStringValueMinimized(DateTimeOffset value)
    {
        const int maxRequired = JsonConstants.MaximumFormatDateTimeOffsetLength + 3; // 2 quotes, and optionally, 1 list separator

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

        JsonWriterHelper.WriteDateTimeOffsetTrimmed(output.Slice(BytesPending), value, out int bytesWritten);
        BytesPending += bytesWritten;

        output[BytesPending++] = JsonConstants.Quote;
    }
}