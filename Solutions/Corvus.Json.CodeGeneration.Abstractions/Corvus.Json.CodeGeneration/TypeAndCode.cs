// <copyright file="TypeAndCode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A result of generating code for a particular type.
/// </summary>
/// <param name="DotnetTypeName">The type implemented by the code.</param>
/// <param name="Code">The code implementing the type.</param>
/// <remarks>This typically represents several partials implementing the dotnet type.</remarks>
public record struct TypeAndCode(string DotnetTypeName, ImmutableArray<CodeAndFilename> Code)
{
}

/// <summary>
/// A specific block of code and its target filename.
/// </summary>
/// <param name="Code">The code to produce.</param>
/// <param name="Filename">The filename in which to produce the code.</param>
public record struct CodeAndFilename(string Code, string Filename)
{
}