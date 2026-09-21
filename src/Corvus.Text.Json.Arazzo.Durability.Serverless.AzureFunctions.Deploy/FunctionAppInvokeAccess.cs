// <copyright file="FunctionAppInvokeAccess.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy;

/// <summary>
/// The invoke access a <see cref="IFunctionAppConfigurator"/> establishes on the Function App before it points the app
/// at a package: the resolved value of the <see cref="AzureFunctionsInvokeAuthorization.KeyName"/> function key, and the
/// Entra audience the app must require when Entra is layered on top.
/// </summary>
/// <param name="InvokeKey">The resolved invoke key value. It is a string because that is what the management API takes.</param>
/// <param name="EntraAudience">The Entra audience the app must require, or <see langword="null"/> when the key is the only layer.</param>
public readonly record struct FunctionAppInvokeAccess(string InvokeKey, string? EntraAudience);