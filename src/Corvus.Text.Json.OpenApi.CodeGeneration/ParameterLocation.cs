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
}