// <copyright file="OperationScope.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// Which operations of a document to prepare.
/// </summary>
public enum OperationScope
{
    /// <summary>
    /// The operations under <c>paths</c>: the API a client calls and a server implements.
    /// </summary>
    Paths,

    /// <summary>
    /// The webhook and per-operation callback operations: the API a client receives and a server calls back into.
    /// </summary>
    WebhooksAndCallbacks,
}