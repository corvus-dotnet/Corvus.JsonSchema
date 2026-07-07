// <copyright file="SqliteResumeClaimTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite.Tests;

/// <summary>
/// Runs the §18 resume-claimable dispatch contract (<see cref="ResumeClaimConformance"/>) against
/// <see cref="SqliteWorkflowStateStore"/>, each test over its own isolated in-memory SQLite database.
/// </summary>
[TestClass]
public sealed class SqliteResumeClaimTests
{
    [TestMethod]
    public async Task Surfaces_only_the_resume_requested_runs()
    {
        await using SqliteWorkflowStateStore store = await SqliteWorkflowStateStore.ConnectAsync("Data Source=:memory:");
        await ResumeClaimConformance.Surfaces_only_the_resume_requested_runs(store);
    }

    [TestMethod]
    public async Task Respects_hosted_and_environment_filters()
    {
        await using SqliteWorkflowStateStore store = await SqliteWorkflowStateStore.ConnectAsync("Data Source=:memory:");
        await ResumeClaimConformance.Respects_hosted_and_environment_filters(store);
    }
}