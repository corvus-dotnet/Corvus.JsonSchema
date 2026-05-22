// <copyright file="ServerInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// Describes a server from the AsyncAPI specification.
/// </summary>
/// <param name="Name">The server key name from the specification.</param>
/// <param name="Host">The server host (e.g., "broker.example.com:9092").</param>
/// <param name="Protocol">The protocol (e.g., "kafka", "amqp", "mqtt").</param>
/// <param name="Pathname">The optional pathname (e.g., "/vhost").</param>
/// <param name="Variables">The server URL variables with default values.</param>
/// <param name="SecuritySchemes">The security scheme names required by this server.</param>
public readonly record struct ServerInfo(
    string Name,
    string Host,
    string Protocol,
    string? Pathname,
    IReadOnlyList<ServerVariable> Variables,
    IReadOnlyList<string> SecuritySchemes);

/// <summary>
/// A server URL variable with optional enum constraint and default value.
/// </summary>
/// <param name="Name">The variable name (used in URL template as {name}).</param>
/// <param name="DefaultValue">The default value for the variable.</param>
/// <param name="EnumValues">Allowed values, or empty if unconstrained.</param>
/// <param name="Description">Optional description of the variable.</param>
public readonly record struct ServerVariable(
    string Name,
    string? DefaultValue,
    IReadOnlyList<string> EnumValues,
    string? Description);