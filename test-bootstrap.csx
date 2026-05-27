using System;
using Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.Fixtures;

// Check what container.Hostname returns
await KafkaFixture.StartAsync();
Console.WriteLine($"BootstrapServers: {KafkaFixture.BootstrapServers}");
await KafkaFixture.StopAsync();
