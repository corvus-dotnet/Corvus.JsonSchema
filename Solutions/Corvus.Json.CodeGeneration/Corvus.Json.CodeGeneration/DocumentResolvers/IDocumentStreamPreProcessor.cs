// <copyright file="IDocumentStreamPreProcessor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.DocumentResolvers;

/// <summary>
/// Pre-process a document stream.
/// </summary>
public interface IDocumentStreamPreProcessor
{
    /// <summary>
    /// Pre-processes the input stream to produce the output stream.
    /// </summary>
    /// <param name="input">The input document stream.</param>
    /// <returns>The output stream.</returns>
    Stream Process(Stream input);
}