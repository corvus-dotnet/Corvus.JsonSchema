// <copyright file="OperationAction.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// The action type of an AsyncAPI operation.
/// </summary>
public enum OperationAction
{
    /// <summary>
    /// The application sends a message (producer/publisher).
    /// </summary>
    Send,

    /// <summary>
    /// The application receives a message (consumer/subscriber).
    /// </summary>
    Receive,
}