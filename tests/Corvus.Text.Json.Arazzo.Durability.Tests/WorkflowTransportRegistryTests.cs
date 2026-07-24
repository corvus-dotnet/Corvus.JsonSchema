// <copyright file="WorkflowTransportRegistryTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.Testing;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.Arazzo.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Tests for <see cref="WorkflowTransportRegistry"/> — resolves a descriptor's transports, failing fast on a missing binding.</summary>
[TestClass]
public sealed class WorkflowTransportRegistryTests
{
    [TestMethod]
    public void Bind_resolves_the_single_api_source_and_omits_the_message_transport_when_not_needed()
    {
        var factory = new CountingFactory();
        var registry = new WorkflowTransportRegistry(
            new Dictionary<string, IApiTransportFactory> { ["petstore"] = factory },
            new Dictionary<string, IMessageTransport> { ["events"] = new InMemoryMessageTransport() });

        WorkflowTransports transports = registry.Bind(new WorkflowDescriptor("orders-v1", ["petstore"], MessageSources: []));

        transports.ApiTransports.Count.ShouldBe(1);
        transports.ApiTransports["petstore"].ShouldNotBeNull();
        transports.MessageTransports.ShouldBeEmpty();
        factory.Created.ShouldBe(1);
    }

    [TestMethod]
    public void Bind_supplies_the_shared_message_transport_when_the_workflow_needs_it()
    {
        var message = new InMemoryMessageTransport();
        var registry = new WorkflowTransportRegistry(
            new Dictionary<string, IApiTransportFactory> { ["petstore"] = new CountingFactory() },
            new Dictionary<string, IMessageTransport> { ["events"] = message });

        WorkflowTransports transports = registry.Bind(new WorkflowDescriptor("orders-v1", ["petstore"], [new MessageSourceDescriptor("events", "nats")]));

        transports.MessageTransports["events"].ShouldBeSameAs(message);
    }

    [TestMethod]
    public void A_source_with_no_configured_binding_fails_fast()
    {
        var registry = new WorkflowTransportRegistry(new Dictionary<string, IApiTransportFactory>());

        Should.Throw<WorkflowTransportBindingException>(
            () => registry.Bind(new WorkflowDescriptor("orders-v1", ["petstore"], MessageSources: [])))
            .Message.ShouldContain("petstore");
    }

    [TestMethod]
    public void Needing_an_unconfigured_message_transport_fails_fast()
    {
        var registry = new WorkflowTransportRegistry(
            new Dictionary<string, IApiTransportFactory> { ["petstore"] = new CountingFactory() });

        Should.Throw<WorkflowTransportBindingException>(
            () => registry.Bind(new WorkflowDescriptor("orders-v1", ["petstore"], [new MessageSourceDescriptor("events", "nats")])))
            .Message.ShouldContain("events");
    }

    [TestMethod]
    public void Bind_resolves_a_transport_for_every_api_source_a_multi_source_workflow_declares()
    {
        var petstore = new CountingFactory();
        var billing = new CountingFactory();
        var registry = new WorkflowTransportRegistry(
            new Dictionary<string, IApiTransportFactory>
            {
                ["petstore"] = petstore,
                ["billing"] = billing,
            });

        WorkflowTransports transports = registry.Bind(new WorkflowDescriptor("orders-v1", ["petstore", "billing"], MessageSources: []));

        transports.ApiTransports.Count.ShouldBe(2);
        transports.ApiTransports["petstore"].ShouldNotBeNull();
        transports.ApiTransports["billing"].ShouldNotBeNull();
        petstore.Created.ShouldBe(1);
        billing.Created.ShouldBe(1);
    }

    [TestMethod]
    public void A_multi_source_workflow_with_one_unconfigured_source_fails_fast()
    {
        var registry = new WorkflowTransportRegistry(
            new Dictionary<string, IApiTransportFactory> { ["petstore"] = new CountingFactory() });

        Should.Throw<WorkflowTransportBindingException>(
            () => registry.Bind(new WorkflowDescriptor("orders-v1", ["petstore", "billing"], MessageSources: [])))
            .Message.ShouldContain("billing");
    }

    [TestMethod]
    public void Validate_reports_a_missing_binding_without_constructing_a_transport()
    {
        var factory = new CountingFactory();
        var registry = new WorkflowTransportRegistry(new Dictionary<string, IApiTransportFactory> { ["petstore"] = factory });

        Should.Throw<WorkflowTransportBindingException>(
            () => registry.Validate(new WorkflowDescriptor("orders-v1", ["unconfigured"], MessageSources: [])));
        factory.Created.ShouldBe(0);
    }

    [TestMethod]
    public void AsBinder_exposes_a_working_WorkflowTransportBinder()
    {
        var registry = new WorkflowTransportRegistry(
            new Dictionary<string, IApiTransportFactory> { ["petstore"] = new CountingFactory() });

        WorkflowTransportBinder binder = registry.AsBinder();
        WorkflowTransports transports = binder(new WorkflowDescriptor("orders-v1", ["petstore"], MessageSources: []), default);

        transports.ApiTransports["petstore"].ShouldNotBeNull();
    }

    /// <summary>An <see cref="IApiTransportFactory"/> that counts how many transports it has created.</summary>
    private sealed class CountingFactory : IApiTransportFactory
    {
        public int Created { get; private set; }

        public IApiTransport CreateTransport()
        {
            this.Created++;
            return new MockApiTransport();
        }
    }
}