// <copyright file="ApiException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Thrown when an API transport returns a non-success status code and
/// the caller has requested strict success handling.
/// </summary>
public class ApiException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApiException"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="body">The response body bytes.</param>
    public ApiException(int statusCode, ReadOnlyMemory<byte> body)
        : base($"The API returned status code {statusCode}.")
    {
        this.StatusCode = statusCode;
        this.ResponseBody = body.ToArray();
    }

    /// <summary>
    /// Gets the HTTP status code.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Gets the response body as a byte array.
    /// </summary>
    public byte[] ResponseBody { get; }
}