// <copyright file="IServerlessInvokeAuthenticator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Authenticates the runner's invocation of a deployed serverless function (ADR 0059 decision 4). The function advances
/// whatever run the invocation names, against whatever checkpoint surface it names, with the function's own source
/// credentials, so an invocation the platform cannot attribute to the runner must never reach it. Each function platform
/// has its own implementation: a Signature Version 4 signature for an <c>AWS_IAM</c> Lambda Function URL, a function key
/// for an Azure Functions trigger, and optionally a Microsoft Entra token on top of that key.
/// </summary>
/// <remarks>
/// The seam is required on the invoking backend and has no anonymous implementation.
/// <see cref="LoopbackServerlessInvokeAuthenticator"/> is the one that adds no credential, and it refuses any function URL
/// that is not on the loopback interface.
/// </remarks>
public interface IServerlessInvokeAuthenticator
{
    /// <summary>
    /// Adds the platform's credential to an invocation before it is sent. The request already carries its final URL,
    /// method and content.
    /// </summary>
    /// <param name="request">The invocation request to authenticate.</param>
    /// <param name="body">The exact bytes of the request content, for a scheme that signs the payload.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the request carries its credential.</returns>
    ValueTask AuthenticateAsync(HttpRequestMessage request, ReadOnlyMemory<byte> body, CancellationToken cancellationToken);
}