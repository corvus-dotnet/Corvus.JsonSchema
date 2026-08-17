// <copyright file="AzureServiceBusFixture.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Testcontainers.MsSql;
using Testcontainers.ServiceBus;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests.Fixtures;

/// <summary>
/// Manages an Azure Service Bus emulator container for integration tests.
/// </summary>
internal static class AzureServiceBusFixture
{
    // The same values the ServiceBus module wires when it provisions the database itself, so
    // the emulator's SQL_SERVER/MSSQL_SA_PASSWORD configuration is unchanged.
    private const string DatabaseNetworkAlias = "database-container";
    private const string DatabasePassword = "yourStrong(!)Password";

    private static ServiceBusContainer? s_container;
    private static MsSqlContainer? s_databaseContainer;
    private static INetwork? s_network;
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

        // The emulator's MSSQL dependency and the network joining them are provisioned here
        // rather than left to the ServiceBusBuilder: the builder's implicit resources are never
        // disposed with the emulator container, so with Ryuk disabled (as it is on this Podman
        // setup) every run leaked one running MSSQL container and one network. Owning them here
        // lets StopAsync dispose all three.
        // Accepting EULA: https://github.com/Azure/azure-service-bus-emulator-installer/blob/main/EMULATOR_EULA.txt
        s_network = new NetworkBuilder().Build();

        s_databaseContainer = new MsSqlBuilder()
            .WithNetwork(s_network)
            .WithNetworkAliases(DatabaseNetworkAlias)
            .WithPassword(DatabasePassword)
            .Build();

        s_container = new ServiceBusBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
            .WithBindMount(s_configFilePath, "/ServiceBus_Emulator/ConfigFiles/Config.json")
            .WithAcceptLicenseAgreement(true)
            .WithMsSqlContainer(s_network, s_databaseContainer, DatabaseNetworkAlias, DatabasePassword)
            .Build();

        await s_container.StartAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Stops and disposes the Azure Service Bus emulator, its database container, and their network.
    /// </summary>
    /// <returns>A task that completes when the resources are disposed.</returns>
    public static async Task StopAsync()
    {
        if (s_container is not null)
        {
            await s_container.DisposeAsync().ConfigureAwait(false);
            s_container = null;
        }

        if (s_databaseContainer is not null)
        {
            await s_databaseContainer.DisposeAsync().ConfigureAwait(false);
            s_databaseContainer = null;
        }

        if (s_network is not null)
        {
            await s_network.DisposeAsync().ConfigureAwait(false);
            s_network = null;
        }

        if (s_configFilePath is not null && File.Exists(s_configFilePath))
        {
            File.Delete(s_configFilePath);
            s_configFilePath = null;
        }
    }
}