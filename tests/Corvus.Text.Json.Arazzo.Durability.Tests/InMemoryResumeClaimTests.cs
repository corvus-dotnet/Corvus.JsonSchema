// <copyright file="InMemoryResumeClaimTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Runs the §18 resume-claimable dispatch contract (<see cref="ResumeClaimConformance"/>) against
/// <see cref="InMemoryWorkflowStateStore"/> — the reference implementation of <see cref="IWorkflowDispatchIndex"/>.
/// </summary>
[TestClass]
public sealed class InMemoryResumeClaimTests
{
    [TestMethod]
    public Task Surfaces_only_the_resume_requested_runs()
        => ResumeClaimConformance.Surfaces_only_the_resume_requested_runs(new InMemoryWorkflowStateStore());

    [TestMethod]
    public Task Respects_hosted_and_environment_filters()
        => ResumeClaimConformance.Respects_hosted_and_environment_filters(new InMemoryWorkflowStateStore());
}