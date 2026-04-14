// <copyright file="JsonataException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Represents an error encountered during JSONata expression parsing or evaluation.
/// Error codes follow the jsonata-js reference implementation conventions.
/// </summary>
public sealed class JsonataException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonataException"/> class.
    /// </summary>
    /// <param name="code">The JSONata error code (e.g. <c>S0101</c>).</param>
    /// <param name="message">A human-readable description of the error.</param>
    /// <param name="position">The zero-based character offset in the source expression where the error occurred.</param>
    /// <param name="token">An optional token or fragment relevant to the error.</param>
    public JsonataException(string code, string message, int position, string? token = null)
        : base($"{code}: {message} at character {position}")
    {
        this.Code = code;
        this.Position = position;
        this.Token = token;
    }

    /// <summary>Gets the JSONata error code.</summary>
    public string Code { get; }

    /// <summary>Gets the zero-based character offset where the error occurred.</summary>
    public int Position { get; }

    /// <summary>Gets the token or fragment relevant to the error, if any.</summary>
    public string? Token { get; }
}