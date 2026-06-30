// <copyright file="TypeScriptApiEmitterOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.TypeScript.CodeGeneration;

/// <summary>
/// Options for the <see cref="TypeScriptApiEmitter"/>.
/// </summary>
/// <param name="ClientRuntimeModuleSpecifier">
/// The module specifier the generated client imports the byte-native transport runtime from. The
/// production default is the published package <c>@endjin/corvus-json-client-runtime</c>; a relative
/// specifier (e.g. <c>../../../packages/corvus-json-client-runtime/dist/index.js</c>) lets a recipe
/// resolve the runtime from the working tree without an install step.
/// </param>
/// <param name="ModelsModuleSpecifier">
/// The module specifier the generated client imports the generated models from. The generated TS
/// models live under <c>&lt;outputPath&gt;/models</c>; this defaults to <c>./models/generated.js</c>
/// (the single-file model output). The named model exports (resolved via <c>Ts_FinalName</c>) are
/// imported from here.
/// </param>
public readonly record struct TypeScriptApiEmitterOptions(
    string ClientRuntimeModuleSpecifier = "@endjin/corvus-json-client-runtime",
    string ModelsModuleSpecifier = "./models/generated.js");