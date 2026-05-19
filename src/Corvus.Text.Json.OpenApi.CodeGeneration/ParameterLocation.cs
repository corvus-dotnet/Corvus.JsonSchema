// <copyright file="ParameterLocation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The location of an API parameter.
/// </summary>
public enum ParameterLocation
{
    /// <summary>
    /// A path parameter (e.g. <c>/pets/{petId}</c>).
    /// </summary>
    Path,

    /// <summary>
    /// A query string parameter.
    /// </summary>
    Query,

    /// <summary>
    /// An HTTP header parameter.
    /// </summary>
    Header,

    /// <summary>
    /// A cookie parameter.
    /// </summary>
    Cookie,

    /// <summary>
    /// A querystring parameter (OAS 3.2+). Represents the entire query string
    /// serialized via a content media type rather than individual named parameters.
    /// </summary>
    Querystring,
}