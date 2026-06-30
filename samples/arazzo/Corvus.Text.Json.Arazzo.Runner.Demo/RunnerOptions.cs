// <copyright file="RunnerOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Runner.Demo;

/// <summary>
/// The runner process's identity and capacity, advertised in its <c>RunnerRegistration</c> and used as the
/// lease owner when it claims runs.
/// </summary>
/// <param name="RunnerId">The stable identity of this runner instance (the dispatch/resume lease owner).</param>
/// <param name="Environment">The single deployment environment this runner serves (design §5.5). The runner is
/// dispatchable only for runs targeting it, resolves this environment's sources and credentials, and inherits its
/// reach (§14.2). A host serving several environments runs one runner process per environment.</param>
/// <param name="MaxConcurrency">The maximum number of runs the runner will execute concurrently (advertised to the control plane).</param>
public sealed record RunnerOptions(string RunnerId, string Environment, int MaxConcurrency = 4);
