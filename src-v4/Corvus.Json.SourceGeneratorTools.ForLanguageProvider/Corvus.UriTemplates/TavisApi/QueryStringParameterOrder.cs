// <copyright file="QueryStringParameterOrder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from Tavis.UriTemplate https://github.com/tavis-software/Tavis.UriTemplates/blob/master/License.txt
// </licensing>

namespace Corvus.UriTemplates.TavisApi;

/// <summary>
/// Determines the parameter ordering in query string parameters.
/// </summary>
public enum QueryStringParameterOrder
{
    /// <summary>
    /// Strict ordering as per the template.
    /// </summary>
    Strict,

    /// <summary>
    /// Arbitrary ordering.
    /// </summary>
    Any,
}