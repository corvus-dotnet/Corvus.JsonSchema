// <copyright file="GeneratorConfig.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGenerator;

/// <summary>
/// JSON Schema for a configuration driver file for the corvus codegenerator.
/// </summary>
/// <remarks>
/// The implementation is generated at build time by the V4 source generator from
/// <c>generator-config.json</c>. See the build-time generation configuration in
/// <c>Corvus.Json.Cli.Core.csproj</c>.
/// </remarks>
[Corvus.Json.JsonSchemaTypeGenerator("./generator-config.json")]
public readonly partial struct GeneratorConfig
{
}