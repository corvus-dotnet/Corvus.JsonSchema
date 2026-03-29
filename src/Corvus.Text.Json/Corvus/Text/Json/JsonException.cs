// <copyright file="JsonException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json;

/// <summary>
/// Represents errors that occur during JSON parsing, reading, or writing operations.
/// This exception is thrown when invalid JSON text is encountered, when the defined maximum depth is exceeded,
/// or when the JSON text is not compatible with the type of a property on an object.
/// </summary>
public class JsonException : Exception
{
    // Allow the message to mutate to avoid re-throwing and losing the StackTrace to an inner exception.
    internal string? _message;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonException"/> class with a specified error message, path, line number, byte position, and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The context specific error message.</param>
    /// <param name="path">The path where the invalid JSON was encountered.</param>
    /// <param name="lineNumber">The line number at which the invalid JSON was encountered (starting at 0) when deserializing.</param>
    /// <param name="bytePositionInLine">The byte count within the current line where the invalid JSON was encountered (starting at 0).</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    /// <remarks>
    /// Note that the <paramref name="bytePositionInLine"/> counts the number of bytes (i.e. UTF-8 code units) and not characters or scalars.
    /// </remarks>
    public JsonException(string? message, string? path, long? lineNumber, long? bytePositionInLine, Exception? innerException)
        : base(message, innerException)
    {
        _message = message;
        LineNumber = lineNumber;
        BytePositionInLine = bytePositionInLine;
        Path = path;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonException"/> class with a specified error message, path, line number, and byte position.
    /// </summary>
    /// <param name="message">The context specific error message.</param>
    /// <param name="path">The path where the invalid JSON was encountered.</param>
    /// <param name="lineNumber">The line number at which the invalid JSON was encountered (starting at 0) when deserializing.</param>
    /// <param name="bytePositionInLine">The byte count within the current line where the invalid JSON was encountered (starting at 0).</param>
    /// <remarks>
    /// Note that the <paramref name="bytePositionInLine"/> counts the number of bytes (i.e. UTF-8 code units) and not characters or scalars.
    /// </remarks>
    public JsonException(string? message, string? path, long? lineNumber, long? bytePositionInLine)
        : base(message)
    {
        _message = message;
        LineNumber = lineNumber;
        BytePositionInLine = bytePositionInLine;
        Path = path;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The context specific error message.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public JsonException(string? message, Exception? innerException)
        : base(message, innerException)
    {
        _message = message;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The context specific error message.</param>
    public JsonException(string? message)
        : base(message)
    {
        _message = message;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonException"/> class.
    /// </summary>
    public JsonException()
        : base()
    {
    }

    /// <summary>
    /// Gets the number of bytes read within the current line before the exception (starting at 0).
    /// </summary>
    public long? BytePositionInLine { get; internal set; }

    /// <summary>
    /// Gets the number of lines read so far before the exception (starting at 0).
    /// </summary>
    public long? LineNumber { get; internal set; }

    /// <summary>
    /// Gets a message that describes the current exception.
    /// </summary>
    public override string Message
    {
        get
        {
            return _message ?? base.Message;
        }
    }

    /// <summary>
    /// Gets the path within the JSON where the exception was encountered.
    /// </summary>
    public string? Path { get; internal set; }

    /// <summary>
    /// Specifies that 'try' logic should append Path information to the exception message.
    /// </summary>
    internal bool AppendPathInformation { get; set; }

    internal void SetMessage(string? message)
    {
        _message = message;
    }
}