// <copyright file="AzureFunctionsInvokeAuthorization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy;

/// <summary>
/// How the runner's invocation of a deployed Function App is authorized (ADR 0059 decision 4). The baked trigger is
/// always at the <c>Function</c> authorization level, so every invocation presents a function key. Microsoft Entra
/// authentication is an optional second layer on top of that key, never a replacement for it, so the function is not
/// anonymous even if the app's authentication settings are later switched off.
/// </summary>
/// <remarks>
/// The key is a secret in the runner's own secret store, named by reference. The deployer sets it on the app as the
/// host-level function key <see cref="KeyName"/>, and <see cref="FunctionKeyServerlessInvokeAuthenticator"/> presents
/// the same secret on each invocation. It never reaches the control plane or its store.
/// </remarks>
public sealed record AzureFunctionsInvokeAuthorization
{
    /// <summary>The name of the host-level function key the deployer sets and the invoker presents.</summary>
    public const string KeyName = "arazzo-invoke";

    /// <summary>Gets the reference to the invoke key in the runner's secret store.</summary>
    public required SecretRef InvokeKey { get; init; }

    /// <summary>
    /// Gets the Microsoft Entra audience (the Function App's application id URI or client id) the runner's token is
    /// issued for, or <see langword="null"/> when the key is the only layer. When set, the deployer refuses to deploy to
    /// an app whose authentication settings do not require Entra authentication for this audience.
    /// </summary>
    public string? EntraAudience { get; init; }
}