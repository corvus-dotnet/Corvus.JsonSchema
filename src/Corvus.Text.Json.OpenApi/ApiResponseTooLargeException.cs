// <copyright file="ApiResponseTooLargeException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Thrown when a response body exceeds the maximum size a transport was configured to accept.
/// </summary>
/// <remarks>
/// <para>
/// Raised by a transport that was given a response size bound, either ahead of the read when
/// the response declares a larger <c>Content-Length</c>, or during the read when the body
/// grows past the bound. A transport with no bound configured never raises it.
/// </para>
/// </remarks>
public sealed class ApiResponseTooLargeException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApiResponseTooLargeException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="maxResponseLength">The configured maximum response length in bytes.</param>
    public ApiResponseTooLargeException(string message, long maxResponseLength)
        : base(message)
    {
        this.MaxResponseLength = maxResponseLength;
    }

    /// <summary>
    /// Gets the configured maximum response length, in bytes, that the response body exceeded.
    /// </summary>
    public long MaxResponseLength { get; }
}