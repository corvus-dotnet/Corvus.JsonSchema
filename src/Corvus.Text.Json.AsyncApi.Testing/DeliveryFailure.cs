// <copyright file="DeliveryFailure.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.Testing;

/// <summary>
/// Represents a handler failure from a loopback delivery on the <see cref="InMemoryMessageTransport"/>
/// (a publish delivered to a subscription on the same transport whose handler threw). As on a real
/// broker, these never surface to the publish call; assert on
/// <see cref="InMemoryMessageTransport.DeliveryFailures"/> instead.
/// </summary>
/// <param name="Channel">The channel the message was delivered on.</param>
/// <param name="Exception">The handler's failure.</param>
public sealed record DeliveryFailure(string Channel, Exception Exception);