// <copyright file="ServerlessTarget.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Aot;

/// <summary>
/// The serverless platform a host-app is assembled and compiled for. The platform decides the entry shim, the compile
/// form-factor, and the published-artifact shape (ADR 0055 per-target amendment, ADR 0061): a runtime-less target
/// compiles to a self-contained Native-AOT binary, a runtime-present target to a framework-dependent ReadyToRun app.
/// </summary>
public enum ServerlessTarget
{
    /// <summary>
    /// AWS Lambda on the <c>provided.al2023</c> custom runtime, which has no .NET runtime — so the host-app is a
    /// self-contained Native-AOT <c>bootstrap</c> binary that runs the Lambda custom-runtime loop.
    /// </summary>
    AwsLambda,

    /// <summary>
    /// Azure Functions on the <c>dotnet-isolated</c> worker, which provides the .NET runtime — so the host-app is a
    /// framework-dependent ReadyToRun isolated-worker app whose HTTP-trigger <c>[Function]</c> feeds invocations to the
    /// vendor-neutral invocation handler.
    /// </summary>
    AzureFunctions,

    /// <summary>
    /// A Hyperlight micro-VM under a Unikraft guest kernel (the micro-guest backend, ADR 0063), which has no .NET
    /// runtime — so the host-app is a fully static Native-AOT <c>guest</c> binary (a <c>linux-musl</c> target) that the
    /// warm sidecar snapshots once and restores per advance: each restore fetches its invocation from the sidecar,
    /// advances the run, posts the outcome, and exits the VM.
    /// </summary>
    MicroGuest,
}