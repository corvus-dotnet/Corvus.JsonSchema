// <copyright file="AzureServiceBusFixture.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Testcontainers.ServiceBus;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.Fixtures;

/// <summary>
/// Manages an Azure Service Bus emulator container for integration tests.
/// </summary>
internal static class AzureServiceBusFixture
{
    private static ServiceBusContainer? s_container;
    private static string? s_configFilePath;

    /// <summary>
    /// Gets the connection string for the running Azure Service Bus emulator.
    /// </summary>
    public static string ConnectionString => s_container?.GetConnectionString()
        ?? throw new InvalidOperationException("Azure Service Bus container not started.");

    /// <summary>
    /// Starts the Azure Service Bus emulator container.
    /// </summary>
    /// <returns>A task that completes when the container is ready.</returns>
    public static async Task StartAsync()
    {
        // Generate emulator config with session-enabled queues for request-reply testing
        const string emulatorConfig = """
            {
              "UserConfig": {
                "Namespaces": [
                  {
                    "Name": "sbemulatorns",
                    "Queues": [
                      {
                        "Name": "test-queue",
                        "Properties": {
                          "RequiresSession": false
                        }
                      },
                      {
                        "Name": "test-reply-queue",
                        "Properties": {
                          "RequiresSession": true
                        }
                      },
                      {
                        "Name": "test-deadletter-source",
                        "Properties": {
                          "RequiresSession": false
                        }
                      }
                    ],
                    "Topics": [
                      {
                        "Name": "test-topic",
                        "Subscriptions": [
                          {
                            "Name": "test-subscription"
                          }
                        ]
                      }
                    ]
                  }
                ],
                "Logging": {
                  "Type": "File"
                }
              }
            }
            """;

        // Write config to temp file
        s_configFilePath = Path.GetTempFileName();
        File.WriteAllText(s_configFilePath, emulatorConfig);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
        {
            File.SetUnixFileMode(
                s_configFilePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }

        // Build and start container (auto-provisions MSSQL 2022 dependency)
        // Accepting EULA: https://github.com/Azure/azure-service-bus-emulator-installer/blob/main/EMULATOR_EULA.txt
        s_container = new ServiceBusBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
            .WithBindMount(s_configFilePath, "/ServiceBus_Emulator/ConfigFiles/Config.json")
            .WithAcceptLicenseAgreement(true)
            .Build();

        await s_container.StartAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Stops and disposes the Azure Service Bus emulator container.
    /// </summary>
    /// <returns>A task that completes when the container is disposed.</returns>
    public static async Task StopAsync()
    {
        if (s_container is not null)
        {
            await s_container.DisposeAsync().ConfigureAwait(false);
            s_container = null;
        }

        if (s_configFilePath is not null && File.Exists(s_configFilePath))
        {
            File.Delete(s_configFilePath);
            s_configFilePath = null;
        }
    }
}