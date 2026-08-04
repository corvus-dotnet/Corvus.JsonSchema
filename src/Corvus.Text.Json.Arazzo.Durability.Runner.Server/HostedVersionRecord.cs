// <copyright file="HostedVersionRecord.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// A version a runner may execute, as the coordinator resolves it. The handler projects this into the generated
/// response model and adds nothing.
/// </summary>
/// <param name="BaseWorkflowId">The unversioned identity the version belongs to.</param>
/// <param name="VersionNumber">The version number within the base workflow.</param>
/// <param name="Hash">The version's content hash, which the runner verifies its loaded executor against.</param>
public readonly record struct HostedVersionRecord(string BaseWorkflowId, int VersionNumber, string Hash);