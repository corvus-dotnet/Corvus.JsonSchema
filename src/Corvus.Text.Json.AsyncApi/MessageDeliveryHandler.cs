// <copyright file="MessageDeliveryHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Handles one strongly typed message together with its runtime delivery metadata.
/// </summary>
/// <typeparam name="TPayload">The message payload type.</typeparam>
/// <param name="payload">The deserialized message payload.</param>
/// <param name="context">The runtime delivery metadata.</param>
/// <param name="cancellationToken">A cancellation token.</param>
public delegate ValueTask MessageDeliveryHandler<TPayload>(
    TPayload payload,
    MessageDeliveryContext context,
    CancellationToken cancellationToken);

/// <summary>
/// Handles one strongly typed request together with its runtime delivery metadata.
/// </summary>
/// <typeparam name="TRequest">The request payload type.</typeparam>
/// <typeparam name="TReply">The reply payload type.</typeparam>
/// <param name="request">The deserialized request payload.</param>
/// <param name="context">The runtime delivery metadata.</param>
/// <param name="cancellationToken">A cancellation token.</param>
public delegate ValueTask<TReply> MessageDeliveryResponder<TRequest, TReply>(
    TRequest request,
    MessageDeliveryContext context,
    CancellationToken cancellationToken);