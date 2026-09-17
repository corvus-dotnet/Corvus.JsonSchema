// <copyright file="IGeneratedCodeFileSink.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Receives generated code files as each one is completed, so that a host can write or hand on a file without
/// the whole output being held at once.
/// </summary>
public interface IGeneratedCodeFileSink
{
    /// <summary>
    /// Adds a completed file.
    /// </summary>
    /// <param name="file">The file.</param>
    void Add(GeneratedCodeFile file);
}