// <copyright file="JsonValueConverter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Internal;

/// <summary>
/// Configuration for the custom <see cref="JsonValueConverter{T}"/>.
/// </summary>
public static class JsonValueConverter
{
    /// <summary>
    /// Gets or sets a value indicating whether serialization is enabled for <see cref="IJsonValue{T}"/> instances.
    /// </summary>
    public static bool EnableInefficientDeserializationSupport { get; set; }
}