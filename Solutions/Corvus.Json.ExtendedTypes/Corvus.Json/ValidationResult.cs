// <copyright file="ValidationResult.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;

namespace Corvus.Json;

/// <summary>
/// A validation result.
/// </summary>
public readonly struct ValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> struct.
    /// </summary>
    /// <param name="valid">A value indicating whether this is a valid result.</param>
    /// <param name="message">The error message.</param>
    /// <param name="location">The location of the result.</param>
    public ValidationResult(bool valid, string? message = null, (JsonReference ValidationLocation, JsonReference SchemaLocation, JsonReference DocumentLocation)? location = null)
    {
        this.Valid = valid;
        this.Message = message;
        this.Location = location;
    }

    /// <summary>
    /// Gets a value indicating whether the item was valid.
    /// </summary>
    public bool Valid
    {
        get;
    }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets the location.
    /// </summary>
    public (JsonReference ValidationLocation, JsonReference SchemaLocation, JsonReference DocumentLocation)? Location { get; }

    /// <inheritdoc/>
    public override string ToString()
    {
        if (this.Location is (JsonReference, JsonReference, JsonReference) location)
        {
            return $"{location.DocumentLocation} {(this.Valid ? "Valid" : "Invalid")} {(this.Message is string m ? m : string.Empty)}{(this.Message is not null ? " " : string.Empty)}[{location.ValidationLocation}, {location.SchemaLocation}]";
        }

        return this.Valid ? "Valid" : "Invalid";
    }
}