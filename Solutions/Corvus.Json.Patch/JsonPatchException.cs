// <copyright file="JsonPatchException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Patch;
using System.Runtime.Serialization;

/// <summary>
/// An exception for JSON Patch operations.
/// </summary>
[Serializable]
public class JsonPatchException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPatchException"/> class.
    /// </summary>
    public JsonPatchException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPatchException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public JsonPatchException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPatchException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public JsonPatchException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPatchException"/> class.
    /// </summary>
    /// <param name="info">The serialization information.</param>
    /// <param name="context">The serialization context.</param>
    protected JsonPatchException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}