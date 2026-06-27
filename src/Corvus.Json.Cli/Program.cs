// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.CodeGenerator;

// In-process codegen worker for the TypeScript Bowtie harness: long-running, registry-aware codegen over
// stdin/stdout NDJSON (no per-schema process spawn). Bypasses the banner and command parsing.
if (args is ["codegen-worker"])
{
    return await GenerationDriverTypeScript.RunBowtieCodegenLoopAsync();
}

if (!Console.IsOutputRedirected)
{
    Banner.Write();
}

CliDefaults.DefaultEngine = Engine.V5;

return await CliAppFactory.Create("corvusjson").RunAsync(args);