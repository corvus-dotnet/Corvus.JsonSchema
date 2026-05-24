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
/// <param name="SecuritySchemes">The security scheme information for this server.</param>
/// <param name="BindingsJson">The raw JSON of the server bindings, or <c>null</c> if no bindings are present.</param>
public readonly record struct ServerInfo(
    string Name,
    string Host,
    string Protocol,
    string? Pathname,
    IReadOnlyList<ServerVariable> Variables,
    IReadOnlyList<SecuritySchemeInfo> SecuritySchemes,
    string? BindingsJson);

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

/// <summary>
/// Describes a security scheme referenced by a server.
/// </summary>
/// <param name="Name">The security scheme name (key in components/securitySchemes).</param>
/// <param name="Type">The scheme type string from the specification (e.g., "userPassword", "apiKey", "oauth2").</param>
public readonly record struct SecuritySchemeInfo(
    string Name,
    string Type);