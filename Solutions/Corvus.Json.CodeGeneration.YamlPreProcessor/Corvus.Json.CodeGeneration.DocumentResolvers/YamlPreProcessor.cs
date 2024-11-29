// <copyright file="YamlPreProcessor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using YamlDotNet.Serialization;

namespace Corvus.Json.CodeGeneration.DocumentResolvers;

/// <summary>
/// Pre-process documents to convert YAML to JSON.
/// </summary>
public class YamlPreProcessor : IDocumentStreamPreProcessor
{
    /// <inheritdoc/>
    public Stream Process(Stream input)
    {
        try
        {
            IDeserializer deserializer = new DeserializerBuilder().Build();
            object? yamlObject = deserializer.Deserialize(new StreamReader(input));

            MemoryStream jsonStream = new();
            StreamWriter writer = new(jsonStream);
            ISerializer serializer = new SerializerBuilder()
               .JsonCompatible()
               .Build();

            serializer.Serialize(writer, yamlObject);
            writer.Flush();
            jsonStream.Position = 0;
            return jsonStream;
        }
        catch (Exception)
        {
            // We could not process the stream.
            return input;
        }
    }
}