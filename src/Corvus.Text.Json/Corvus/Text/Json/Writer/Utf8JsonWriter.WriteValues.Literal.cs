// <copyright file="Utf8JsonWriter.WriteValues.Literal.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

public sealed partial class Utf8JsonWriter
{
    /// <summary>
    /// Writes the <see cref="bool"/> value (as a JSON literal "true" or "false") as an element of a JSON array.
    /// </summary>
    /// <param name="value">The value write.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if this would result in invalid JSON being written (while validation is enabled).
    /// </exception>
    public void WriteBooleanValue(bool value)
    {
        if (value)
        {
            WriteLiteralByOptions(JsonConstants.TrueValue);
            _tokenType = JsonTokenType.True;
        }
        else
        {
            WriteLiteralByOptions(JsonConstants.FalseValue);
            _tokenType = JsonTokenType.False;
        }

        SetFlagToAddListSeparatorBeforeNextItem();
    }

    /// <summary>
    /// Writes the JSON literal "null" as an element of a JSON array.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if this would result in invalid JSON being written (while validation is enabled).
    /// </exception>
    public void WriteNullValue()
    {
        WriteLiteralByOptions(JsonConstants.NullValue);

        SetFlagToAddListSeparatorBeforeNextItem();
        _tokenType = JsonTokenType.Null;
    }

    private void WriteLiteralByOptions(ReadOnlySpan<byte> utf8Value)
    {
        if (!_options.SkipValidation)
        {
            ValidateWritingValue();
        }

        if (_options.Indented)
        {
            WriteLiteralIndented(utf8Value);
        }
        else
        {
            WriteLiteralMinimized(utf8Value);
        }
    }

    private void WriteLiteralIndented(ReadOnlySpan<byte> utf8Value)
    {
        int indent = Indentation;
        Debug.Assert(indent <= _indentLength * _options.MaxDepth);
        Debug.Assert(utf8Value.Length <= 5);

        int maxRequired = indent + utf8Value.Length + 1 + _newLineLength; // Optionally, 1 list separator and 1-2 bytes for new line

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

        utf8Value.CopyTo(output.Slice(BytesPending));
        BytesPending += utf8Value.Length;
    }

    private void WriteLiteralMinimized(ReadOnlySpan<byte> utf8Value)
    {
        Debug.Assert(utf8Value.Length <= 5);

        int maxRequired = utf8Value.Length + 1; // Optionally, 1 list separator

        if (_memory.Length - BytesPending < maxRequired)
        {
            Grow(maxRequired);
        }

        Span<byte> output = _memory.Span;

        if (_currentDepth < 0)
        {
            output[BytesPending++] = JsonConstants.ListSeparator;
        }

        utf8Value.CopyTo(output.Slice(BytesPending));
        BytesPending += utf8Value.Length;
    }
}