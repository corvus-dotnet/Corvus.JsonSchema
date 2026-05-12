// <copyright file="YamlPreProcessor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Yaml;

namespace Corvus.Json.CodeGeneration.DocumentResolvers;

/// <summary>
/// Pre-process documents to convert YAML to JSON.
/// </summary>
public class YamlPreProcessor : IDocumentStreamPreProcessor
{
    /// <inheritdoc/>
    public Stream Process(Stream input)
    {
        byte[] yamlBytes;
        using (MemoryStream ms = new())
        {
            input.CopyTo(ms);
            yamlBytes = ms.ToArray();
        }

        try
        {
            string json = YamlDocument.ConvertToJsonString(yamlBytes);
            return new MemoryStream(Encoding.UTF8.GetBytes(json));
        }
        catch (Exception)
        {
            // We could not process the stream as YAML; return the
            // buffered bytes so downstream can attempt JSON parsing.
            return new MemoryStream(yamlBytes, writable: false);
        }
    }
}