// <copyright file="RequiredBinaryPartMissingException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Thrown when a streaming multipart endpoint's handler opens a required binary part
/// that is not present in the body. Generated endpoints map this to a 400 response.
/// </summary>
public sealed class RequiredBinaryPartMissingException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequiredBinaryPartMissingException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="partName">The missing part's name.</param>
    public RequiredBinaryPartMissingException(string message, string partName)
        : base(message)
    {
        this.PartName = partName;
    }

    /// <summary>
    /// Gets the missing part's name.
    /// </summary>
    public string PartName { get; }
}