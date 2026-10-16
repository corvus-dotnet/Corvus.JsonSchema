// <copyright file="ClientEmitOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The options a client emitter needs beyond the prepared model.
/// </summary>
/// <param name="Namespace">
/// The container the generated client lives in, in the target language's terms: a C# namespace, a Go or Java
/// package, a Python or Julia module, a Rust crate.
/// </param>
/// <param name="ClientName">The prefix for generated client type names, or <see langword="null"/> to derive one from the API title.</param>
public sealed record ClientEmitOptions(string Namespace, string? ClientName);