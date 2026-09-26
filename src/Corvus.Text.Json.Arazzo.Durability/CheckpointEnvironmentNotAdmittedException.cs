// <copyright file="CheckpointEnvironmentNotAdmittedException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A checkpoint for an environment the runner's allowlist does not admit (ADR 0065 decision 10): the sealing store
/// neither loads nor saves it. A binding the control plane wrote for an environment the tenant did not name is what
/// this refuses, before a byte of the row is trusted or a byte of plaintext is written.
/// </summary>
public sealed class CheckpointEnvironmentNotAdmittedException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="CheckpointEnvironmentNotAdmittedException"/> class.</summary>
    /// <param name="address">The run.</param>
    /// <param name="message">The message.</param>
    public CheckpointEnvironmentNotAdmittedException(in WorkflowRunAddress address, string message)
        : base(message)
    {
        this.Address = address;
    }

    /// <summary>Gets the run.</summary>
    public WorkflowRunAddress Address { get; }
}