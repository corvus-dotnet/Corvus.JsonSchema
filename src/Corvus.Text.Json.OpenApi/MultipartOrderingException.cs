// <copyright file="MultipartOrderingException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Thrown by a streaming multipart endpoint under the
/// <see cref="MultipartBinaryOrdering.RequireBinaryLast"/> policy when a non-binary
/// part arrives after a binary part. Generated endpoints map this to a 400 response.
/// </summary>
public sealed class MultipartOrderingException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MultipartOrderingException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public MultipartOrderingException(string message)
        : base(message)
    {
    }
}