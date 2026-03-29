// <copyright file="Utf8JsonWriter.WriteValues.Float.cs" company="Endjin Limited">
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
using System.Globalization;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

public sealed partial class Utf8JsonWriter
{
    /// <summary>
    /// Writes the <see cref="float"/> value (as a JSON number) as an element of a JSON array.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if this would result in invalid JSON being written (while validation is enabled).
    /// </exception>
    /// <remarks>
    /// Writes the <see cref="float"/> using the default <see cref="StandardFormat"/> on .NET Core 3 or higher
    /// and 'G9' on any other framework.
    /// </remarks>
    public void WriteNumberValue(float value)
    {
        JsonWriterHelper.ValidateSingle(value);

        if (!_options.SkipValidation)
        {
            ValidateWritingValue();
        }

        if (_options.Indented)
        {
            WriteNumberValueIndented(value);
        }
        else
        {
            WriteNumberValueMinimized(value);
        }

        SetFlagToAddListSeparatorBeforeNextItem();
        _tokenType = JsonTokenType.Number;
    }

    private void WriteNumberValueMinimized(float value)
    {
        const int maxRequired = JsonConstants.MaximumFormatSingleLength + 1; // Optionally, 1 list separator

        if (_memory.Length - BytesPending < maxRequired)
        {
            Grow(maxRequired);
        }

        Span<byte> output = _memory.Span;

        if (_currentDepth < 0)
        {
            output[BytesPending++] = JsonConstants.ListSeparator;
        }

        bool result = TryFormatSingle(value, output.Slice(BytesPending), out int bytesWritten);
        Debug.Assert(result);
        BytesPending += bytesWritten;
    }

    private void WriteNumberValueIndented(float value)
    {
        int indent = Indentation;
        Debug.Assert(indent <= _indentLength * _options.MaxDepth);

        int maxRequired = indent + JsonConstants.MaximumFormatSingleLength + 1 + _newLineLength; // Optionally, 1 list separator and 1-2 bytes for new line

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

        bool result = TryFormatSingle(value, output.Slice(BytesPending), out int bytesWritten);
        Debug.Assert(result);
        BytesPending += bytesWritten;
    }

    private static bool TryFormatSingle(float value, Span<byte> destination, out int bytesWritten)
    {
        // Frameworks that are not .NET Core 3.0 or higher do not produce roundtrippable strings by
        // default. Further, the Utf8Formatter on older frameworks does not support taking a precision
        // specifier for 'G' nor does it represent other formats such as 'R'. As such, we duplicate
        // the .NET Core 3.0 logic of forwarding to the UTF16 formatter and transcoding it back to UTF8,
        // with some additional changes to remove dependencies on Span APIs which don't exist downlevel.
#if NET
        return Utf8Formatter.TryFormat(value, destination, out bytesWritten);
#else
        string utf16Text = value.ToString(JsonConstants.SingleFormatString, CultureInfo.InvariantCulture);

        // Copy the value to the destination, if it's large enough.
        if (utf16Text.Length > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(utf16Text);

            if (bytes.Length > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            bytes.CopyTo(destination);
            bytesWritten = bytes.Length;

            return true;
        }
        catch
        {
            bytesWritten = 0;
            return false;
        }
#endif
    }
}