// <copyright file="ApiTransportTimeoutException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Thrown when a request does not complete within the timeout a transport was configured with.
/// </summary>
/// <remarks>
/// <para>
/// The timeout spans the whole exchange: sending the request, receiving the response headers
/// and producing the response from its body. It is raised only when the transport's own clock
/// ran out. A caller's cancellation still surfaces as an <see cref="OperationCanceledException"/>,
/// and a transport with no timeout configured never raises it.
/// </para>
/// </remarks>
public sealed class ApiTransportTimeoutException : TimeoutException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApiTransportTimeoutException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="timeout">The configured timeout.</param>
    /// <param name="innerException">The cancellation that the elapsed timeout raised.</param>
    public ApiTransportTimeoutException(string message, TimeSpan timeout, Exception? innerException)
        : base(message, innerException)
    {
        this.Timeout = timeout;
    }

    /// <summary>
    /// Gets the configured timeout that the request exceeded.
    /// </summary>
    public TimeSpan Timeout { get; }
}