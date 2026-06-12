// <copyright file="LoadedWorkflow.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.Loader;

namespace Corvus.Text.Json.Arazzo.Execution;

/// <summary>
/// A verified, loaded workflow executor: the activated <see cref="IHostedWorkflow"/> and its manifest. The
/// owning <see cref="WorkflowExecutorLoader"/> holds the collectible load context and unloads it on eviction.
/// </summary>
public sealed class LoadedWorkflow
{
    internal LoadedWorkflow(IHostedWorkflow workflow, in WorkflowExecutorManifest manifest, AssemblyLoadContext loadContext)
    {
        this.Workflow = workflow;
        this.Manifest = manifest;
        this.LoadContext = loadContext;
    }

    /// <summary>Gets the activated hosted workflow.</summary>
    public IHostedWorkflow Workflow { get; }

    /// <summary>Gets the verified executor manifest.</summary>
    public WorkflowExecutorManifest Manifest { get; }

    /// <summary>Gets the collectible load context the assembly was loaded into.</summary>
    internal AssemblyLoadContext LoadContext { get; }
}