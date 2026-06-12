// <copyright file="HttpClientApiTransportFactoryTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.OpenApi.HttpTransport.Tests;

[TestClass]
public class HttpClientApiTransportFactoryTests
{
    [TestMethod]
    public async Task Creates_a_fresh_transport_per_call_over_the_shared_client()
    {
        using var client = new HttpClient { BaseAddress = new Uri("https://example.test/") };
        var factory = new HttpClientApiTransportFactory(client);

        await using IApiTransport first = factory.CreateTransport();
        await using IApiTransport second = factory.CreateTransport();

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreNotSame(first, second);
    }

    [TestMethod]
    public async Task Disposing_a_created_transport_leaves_the_shared_client_open()
    {
        using var client = new HttpClient { BaseAddress = new Uri("https://example.test/") };
        var factory = new HttpClientApiTransportFactory(client);

        IApiTransport transport = factory.CreateTransport();
        await transport.DisposeAsync();

        // The client was not disposed by the transport (disposeClient: false), so the factory can keep using it.
        await using IApiTransport next = factory.CreateTransport();
        Assert.IsNotNull(next);
    }

    [TestMethod]
    public void A_null_client_is_rejected()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpClientApiTransportFactory(null!));
    }
}