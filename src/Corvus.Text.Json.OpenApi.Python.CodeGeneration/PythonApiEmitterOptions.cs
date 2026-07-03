// <copyright file="PythonApiEmitterOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.Python.CodeGeneration;

/// <summary>
/// Options controlling the Python OpenAPI client emitter.
/// </summary>
/// <param name="ClientRuntimeModuleSpecifier">
/// The import path for the byte-native client transport runtime. Defaults to the published package
/// name <c>corvus_json_client_runtime</c>. A relative dotted path lets a recipe resolve the runtime
/// from the working tree.
/// </param>
/// <param name="ModelsPackage">
/// The dotted import path for the generated model package, relative to the generated client. Defaults
/// to <c>.models</c>.
/// </param>
public readonly record struct PythonApiEmitterOptions(
    string ClientRuntimeModuleSpecifier = "corvus_json_client_runtime",
    string ModelsPackage = ".models");