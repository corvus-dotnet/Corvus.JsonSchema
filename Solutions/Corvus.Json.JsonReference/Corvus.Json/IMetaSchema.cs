// <copyright file="IMetaSchema.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// Interface for a metaschema that can be resolved to a <see cref="JsonDocument"/>.
/// </summary>
public interface IMetaSchema
{
    /// <summary>
    /// Gets the URI of the metaschema.
    /// </summary>
    string Uri { get; }

    /// <summary>
    /// Gets the <see cref="JsonDocument"/> for the metaschema.
    /// </summary>
    JsonDocument Document { get; }
}