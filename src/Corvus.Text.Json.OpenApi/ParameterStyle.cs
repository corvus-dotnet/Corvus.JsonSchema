// <copyright file="ParameterStyle.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// The serialization style for an OpenAPI parameter, determining how the value
/// is formatted in the request URI or headers.
/// </summary>
/// <remarks>
/// <para>
/// See <see href="https://spec.openapis.org/oas/v3.1.0#style-values">OpenAPI 3.1 §4.8.12</see>
/// for the full specification.
/// </para>
/// </remarks>
public enum ParameterStyle : byte
{
    /// <summary>
    /// Simple comma-separated values. Default for <c>path</c> and <c>header</c> parameters.
    /// </summary>
    Simple = 0,

    /// <summary>
    /// Form-style query parameters (<c>name=value</c>). Default for <c>query</c> and
    /// <c>cookie</c> parameters.
    /// </summary>
    Form = 1,

    /// <summary>
    /// Label expansion with dot-prefix (<c>.value</c>).
    /// </summary>
    Label = 2,

    /// <summary>
    /// Matrix expansion with semicolon-prefix (<c>;name=value</c>).
    /// </summary>
    Matrix = 3,

    /// <summary>
    /// Space-separated array values in query strings.
    /// </summary>
    SpaceDelimited = 4,

    /// <summary>
    /// Pipe-separated array values in query strings.
    /// </summary>
    PipeDelimited = 5,

    /// <summary>
    /// Deep object expansion for query parameters (<c>name[key]=value</c>).
    /// Only applies to objects with <c>explode: true</c>.
    /// </summary>
    DeepObject = 6,
}