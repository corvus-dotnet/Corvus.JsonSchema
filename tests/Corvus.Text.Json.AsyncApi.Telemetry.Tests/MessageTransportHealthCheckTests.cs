// <copyright file="MessageTransportHealthCheckTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.AsyncApi.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Corvus.Text.Json.AsyncApi.Telemetry.Tests;

[TestClass]
public class MessageTransportHealthCheckTests
{
    [TestMethod]
    public async Task CheckHealthAsync_WhenConnectedAndPingSucceeds_ReturnsHealthy()
    {
        MockHealthCheckableTransport transport = new(isConnected: true, pingResult: true);
        MessageTransportHealthCheck healthCheck = new(transport);

        HealthCheckResult result = await healthCheck.CheckHealthAsync(CreateContext(), default);

        Assert.AreEqual(HealthStatus.Healthy, result.Status);
        Assert.IsTrue(result.Description!.Contains("responsive"));
        Assert.AreEqual("mock-system", result.Data["messaging.system"]);
        Assert.AreEqual(true, result.Data["connected"]);
    }

    [TestMethod]
    public async Task CheckHealthAsync_WhenDisconnected_ReturnsDegraded()
    {
        MockHealthCheckableTransport transport = new(isConnected: false, pingResult: false);
        MessageTransportHealthCheck healthCheck = new(transport);

        HealthCheckResult result = await healthCheck.CheckHealthAsync(CreateContext(), default);

        Assert.AreEqual(HealthStatus.Degraded, result.Status);
        Assert.IsTrue(result.Description!.Contains("disconnected"));
        Assert.AreEqual(false, result.Data["connected"]);
    }

    [TestMethod]
    public async Task CheckHealthAsync_WhenConnectedButPingFails_ReturnsUnhealthy()
    {
        MockHealthCheckableTransport transport = new(isConnected: true, pingResult: false);
        MessageTransportHealthCheck healthCheck = new(transport);

        HealthCheckResult result = await healthCheck.CheckHealthAsync(CreateContext(), default);

        Assert.AreEqual(HealthStatus.Unhealthy, result.Status);
        Assert.IsTrue(result.Description!.Contains("ping failed"));
    }

    [TestMethod]
    public async Task CheckHealthAsync_WhenPingThrowsOperationCanceled_ReturnsUnhealthy()
    {
        MockHealthCheckableTransport transport = new(
            isConnected: true,
            pingResult: false,
            pingException: new OperationCanceledException());
        MessageTransportHealthCheck healthCheck = new(transport);

        HealthCheckResult result = await healthCheck.CheckHealthAsync(CreateContext(), default);

        Assert.AreEqual(HealthStatus.Unhealthy, result.Status);
        Assert.IsTrue(result.Description!.Contains("timed out"));
    }

    [TestMethod]
    public async Task CheckHealthAsync_WhenPingThrowsException_ReturnsUnhealthyWithException()
    {
        InvalidOperationException ex = new("connection reset");
        MockHealthCheckableTransport transport = new(
            isConnected: true,
            pingResult: false,
            pingException: ex);
        MessageTransportHealthCheck healthCheck = new(transport);

        HealthCheckResult result = await healthCheck.CheckHealthAsync(CreateContext(), default);

        Assert.AreEqual(HealthStatus.Unhealthy, result.Status);
        Assert.AreSame(ex, result.Exception);
        Assert.AreEqual("System.InvalidOperationException", result.Data["error.type"]);
    }

    private static HealthCheckContext CreateContext()
    {
        return new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                "test-transport",
                _ => new MessageTransportHealthCheck(new MockHealthCheckableTransport(true, true)),
                null,
                null),
        };
    }

    private sealed class MockHealthCheckableTransport : IHealthCheckableTransport
    {
        private readonly bool pingResult;
        private readonly Exception? pingException;

        public MockHealthCheckableTransport(bool isConnected, bool pingResult, Exception? pingException = null)
        {
            this.IsConnected = isConnected;
            this.pingResult = pingResult;
            this.pingException = pingException;
        }

        public bool IsConnected { get; }

        public string MessagingSystem => "mock-system";

        public ValueTask<bool> PingAsync(CancellationToken cancellationToken = default)
        {
            if (this.pingException is not null)
            {
                throw this.pingException;
            }

            return ValueTask.FromResult(this.pingResult);
        }
    }
}