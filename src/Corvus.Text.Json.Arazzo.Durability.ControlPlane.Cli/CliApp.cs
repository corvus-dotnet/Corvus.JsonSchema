// <copyright file="CliApp.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Spectre.Console.Cli;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

/// <summary>
/// Builds the configured <c>arazzo-runs</c> command app. Exposed so tests can drive the commands in-process.
/// </summary>
public static class CliApp
{
    /// <summary>Creates the configured <see cref="CommandApp"/> with every <c>arazzo-runs</c> subcommand registered.</summary>
    /// <returns>The configured command app.</returns>
    public static CommandApp Create()
    {
        var app = new CommandApp();
        app.Configure(c =>
        {
            c.SetApplicationName("arazzo-runs");
            c.AddCommand<ListCommand>("list")
                .WithDescription("List runs (filter by status / workflow id, paged).");
            c.AddCommand<GetCommand>("get")
                .WithDescription("Show a run's management detail.");
            c.AddCommand<ResumeCommand>("resume")
                .WithDescription("Resume a faulted run (RetryFaultedStep / Rewind / Skip / StatePatch).");
            c.AddCommand<CancelCommand>("cancel")
                .WithDescription("Cancel a non-terminal run.");
            c.AddCommand<DeleteCommand>("delete")
                .WithDescription("Permanently delete a single run.");
            c.AddCommand<PurgeCommand>("purge")
                .WithDescription("Reap old terminal runs in bulk.");
            c.AddCommand<LoginCommand>("login")
                .WithDescription("Sign in interactively (browser loopback, or --use-device-code) and cache an access token.");
            c.AddCommand<LogoutCommand>("logout")
                .WithDescription("Remove the cached access token.");
        });

        return app;
    }
}