// <copyright file="ControlPlaneSecurityModeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Holds ADR 0016 to its word at the type: the posture is named, never defaulted. V-14 of the 2026-08-07 audit found
/// that <c>default(ControlPlaneSecurityMode)</c> was <c>Open</c>, so a host that bound the mode and never set it ran
/// open in silence.
/// </summary>
[TestClass]
public sealed class ControlPlaneSecurityModeTests
{
    [TestMethod]
    public void A_posture_that_was_never_named_is_refused_at_mapping()
    {
        default(ControlPlaneSecurityMode).ShouldBe(ControlPlaneSecurityMode.None);

        // The message is the one the mode guard writes. The audit-sink and telemetry gates that follow it also refuse
        // every mode but Open, so a match on the mode's name alone would pass with the guard removed.
        Should.Throw<ArgumentException>(() => Map(default)).Message.ShouldContain("ControlPlaneSecurityMode.None is not a posture");
        Should.Throw<ArgumentException>(() => Map((ControlPlaneSecurityMode)42)).Message.ShouldContain("ControlPlaneSecurityMode.42 is not a posture");
    }

    private static void Map(ControlPlaneSecurityMode mode)
    {
        var store = new InMemoryWorkflowStateStore();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        using WebApplication app = builder.Build();
        app.MapArazzoControlPlane(
            new SecuredWorkflowManagement(store, "ops"),
            new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops", administrators: new InMemoryWorkflowAdministratorStore()),
            new InMemoryRunnerRegistry(),
            mode);
    }
}