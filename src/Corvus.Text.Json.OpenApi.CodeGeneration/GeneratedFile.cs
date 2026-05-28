// <copyright file="GeneratedFile.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents a generated C# source file.
/// </summary>
/// <param name="FileName">The file name (e.g. <c>IPetstoreClient.cs</c>).</param>
/// <param name="Content">The full C# source text.</param>
public sealed record GeneratedFile(string FileName, string Content);