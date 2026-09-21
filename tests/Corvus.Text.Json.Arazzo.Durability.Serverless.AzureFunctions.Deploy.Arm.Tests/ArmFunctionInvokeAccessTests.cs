// <copyright file="ArmFunctionInvokeAccessTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure.ResourceManager.AppService.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy.Arm.Tests;

/// <summary>
/// Proves the posture check the deployer makes when Entra is layered on the invoke key: an app is deployed to only when
/// its authentication settings require Entra authentication for the configured audience on every path. There is no ARM
/// emulator, so the reading of the settings from a real app is proven by the live Azure gate.
/// </summary>
[TestClass]
public sealed class ArmFunctionInvokeAccessTests
{
    private const string Audience = "api://arazzo-functions";

    private static readonly string[] Accepted = [Audience];

    [TestMethod]
    public void An_app_that_requires_entra_for_the_audience_is_accepted()
    {
        Refusal().ShouldBeNull();
        Refusal(action: UnauthenticatedClientActionV2.Return403).ShouldBeNull();

        // A trailing slash on either side is the same audience, and the provider's enabled flag defaults to on.
        ArmFunctionInvokeAccess.EntraPostureRefusal(true, true, UnauthenticatedClientActionV2.Return401, null, null, ["API://arazzo-functions/"], Audience).ShouldBeNull();
    }

    [TestMethod]
    public void An_app_whose_authentication_is_off_or_optional_is_refused()
    {
        Refusal(platformEnabled: false).ShouldNotBeNull();
        Refusal(platformEnabled: null).ShouldNotBeNull();
        Refusal(required: false).ShouldNotBeNull();
        Refusal(required: null).ShouldNotBeNull();
    }

    [TestMethod]
    public void An_app_that_lets_an_unauthenticated_request_through_is_refused()
    {
        Refusal(action: UnauthenticatedClientActionV2.AllowAnonymous).ShouldNotBeNull();
        Refusal(action: UnauthenticatedClientActionV2.RedirectToLoginPage).ShouldNotBeNull();
        Refusal(action: null).ShouldNotBeNull();
    }

    [TestMethod]
    public void An_app_that_exempts_any_path_is_refused()
        => Refusal(excluded: ["/api/invoke"]).ShouldNotBeNull();

    [TestMethod]
    public void An_app_whose_entra_provider_is_disabled_or_takes_another_audience_is_refused()
    {
        Refusal(entraEnabled: false).ShouldNotBeNull();
        Refusal(audiences: ["api://something-else"]).ShouldNotBeNull();
        Refusal(audiences: []).ShouldNotBeNull();
        ArmFunctionInvokeAccess.EntraPostureRefusal(true, true, UnauthenticatedClientActionV2.Return401, null, true, null, Audience).ShouldNotBeNull();
    }

    private static string? Refusal(
        bool? platformEnabled = true,
        bool? required = true,
        UnauthenticatedClientActionV2? action = UnauthenticatedClientActionV2.Return401,
        string[]? excluded = null,
        bool? entraEnabled = true,
        string[]? audiences = null)
        => ArmFunctionInvokeAccess.EntraPostureRefusal(platformEnabled, required, action, excluded, entraEnabled, audiences ?? Accepted, Audience);
}