// <copyright file="RequestBodyTooLargeException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Thrown when a buffered request body exceeds the configured maximum size.
/// </summary>
/// <remarks>
/// <para>
/// Raised by the body deserializers that buffer the whole request body
/// (<see cref="MultipartFormDataSerializer"/>, <see cref="MultipartMixedSerializer"/>
/// and <see cref="FormUrlEncodedSerializer"/>) when the body grows past the caller's
/// configured maximum. Generated server endpoints map this exception to a
/// 413 Payload Too Large response.
/// </para>
/// </remarks>
public sealed class RequestBodyTooLargeException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequestBodyTooLargeException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="maxBodyLength">The configured maximum body length in bytes.</param>
    public RequestBodyTooLargeException(string message, long maxBodyLength)
        : base(message)
    {
        this.MaxBodyLength = maxBodyLength;
    }

    /// <summary>
    /// Gets the configured maximum body length, in bytes, that the request body exceeded.
    /// </summary>
    public long MaxBodyLength { get; }
}