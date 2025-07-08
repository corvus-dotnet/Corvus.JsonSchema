// <copyright file="MetaSchema.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text.Json;

namespace Corvus.Json.JsonSchema.Draft4;

/// <summary>
/// MetaSchema for JsonSchema Draft 4.
/// </summary>
public sealed class MetaSchema : IMetaSchema
{
    /// <summary>
    /// Gets the default instance of the Draft 4 <see cref="MetaSchema"/>.
    /// </summary>
    public static MetaSchema Instance { get; } = new();

    /// <inheritdoc />
    public string Uri => "https://json-schema.org/draft-04/schema";

    /// <inheritdoc />
    public JsonDocument Document => JsonDocument.Parse(ReadResource("./metaschema/draft4/schema.json"));

    private static string ReadResource(string name)
    {
        using Stream? resourceStream = typeof(MetaSchema).Assembly.GetManifestResourceStream(name);
        Debug.Assert(resourceStream is not null, $"The manifest resource stream {name} does not exist.");
        using var reader = new StreamReader(resourceStream);
        return reader.ReadToEnd();
    }
}