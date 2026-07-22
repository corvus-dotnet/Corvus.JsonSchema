// <copyright file="WorkflowExecutorSource.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.Generation;

/// <summary>
/// The generated C# source for a workflow executor, before compilation. This is the compile-time input an AOT
/// execution backend bakes into a per-version native host (ADR 0028): the in-process backend compiles it to IL at
/// catalog-add time, while an AOT build assembles these files into a host-app project and compiles them natively.
/// </summary>
/// <param name="WorkflowId">The workflow id the executor runs.</param>
/// <param name="EntryType">The fully-qualified name of the generated entry type (the <c>IHostedWorkflow</c> the host activates).</param>
/// <param name="Files">The generated C# source files (relative path plus content), the whole executor.</param>
/// <param name="Sources">The workflow's declared API and message sources (name, url, type), as recorded in the manifest.</param>
public readonly record struct WorkflowExecutorSource(
    string WorkflowId,
    string EntryType,
    IReadOnlyList<GeneratedCodeFile> Files,
    IReadOnlyList<(string Name, string Url, string Type)> Sources);

#endif