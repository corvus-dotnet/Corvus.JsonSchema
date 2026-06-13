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

            c.AddBranch<CommandSettings>("catalog", catalog =>
            {
                catalog.SetDescription("Work with the workflow catalog (versioned, hashed package store).");
                catalog.AddCommand<CatalogPackCommand>("pack")
                    .WithDescription("Build a package envelope from a workflow file + its sources (local, no server).");
                catalog.AddCommand<CatalogUnpackCommand>("unpack")
                    .WithDescription("Extract a package envelope's workflow + sources to a directory (local, no server).");
                catalog.AddCommand<CatalogVerifyCommand>("verify")
                    .WithDescription("Verify a package envelope locally (structure, referential completeness, hash).");
                catalog.AddCommand<CatalogAddCommand>("add")
                    .WithDescription("Upload a new version (from --package or --workflow), with owner + tags.");
                catalog.AddCommand<CatalogSearchCommand>("search")
                    .WithDescription("Search the catalog (text / base id / tags / status / owner, paged).");
                catalog.AddCommand<CatalogVersionsCommand>("versions")
                    .WithDescription("List the versions of a base workflow id.");
                catalog.AddCommand<CatalogGetCommand>("get")
                    .WithDescription("Show a version's metadata.");
                catalog.AddCommand<CatalogPackageCommand>("package")
                    .WithDescription("Download a version's whole package envelope.");
                catalog.AddCommand<CatalogWorkflowCommand>("workflow")
                    .WithDescription("Download a version's Arazzo workflow document.");
                catalog.AddCommand<CatalogSourceCommand>("source")
                    .WithDescription("Download one named source document from a version.");
                catalog.AddCommand<CatalogUpdateCommand>("update")
                    .WithDescription("Update a version's governance metadata (owner / tags / status).");
                catalog.AddCommand<CatalogObsoleteCommand>("obsolete")
                    .WithDescription("Mark a version obsolete (shorthand for update --status Obsolete).");
                catalog.AddCommand<CatalogDeleteCommand>("delete")
                    .WithDescription("Delete a single version (refused while runs reference it).");
                catalog.AddCommand<CatalogPurgeCommand>("purge")
                    .WithDescription("Bulk-reap obsolete versions with no referencing runs.");
            });

            c.AddBranch<CommandSettings>("security", security =>
            {
                security.SetDescription("Author the row-security policy: rules and claim→rule bindings (security:read / security:write).");
                security.AddBranch<CommandSettings>("rule", rule =>
                {
                    rule.SetDescription("Manage named security rules.");
                    rule.AddCommand<SecurityRuleListCommand>("list").WithDescription("List all security rules.");
                    rule.AddCommand<SecurityRuleGetCommand>("get").WithDescription("Get a security rule by name.");
                    rule.AddCommand<SecurityRuleCreateCommand>("create").WithDescription("Create a security rule.");
                    rule.AddCommand<SecurityRuleUpdateCommand>("update").WithDescription("Replace a security rule's content.");
                    rule.AddCommand<SecurityRuleDeleteCommand>("delete").WithDescription("Delete a security rule.");
                });
                security.AddBranch<CommandSettings>("binding", binding =>
                {
                    binding.SetDescription("Manage claim→rule bindings.");
                    binding.AddCommand<SecurityBindingListCommand>("list").WithDescription("List all security bindings.");
                    binding.AddCommand<SecurityBindingGetCommand>("get").WithDescription("Get a security binding by id.");
                    binding.AddCommand<SecurityBindingCreateCommand>("create").WithDescription("Create a claim→rule binding.");
                    binding.AddCommand<SecurityBindingUpdateCommand>("update").WithDescription("Replace a claim→rule binding.");
                    binding.AddCommand<SecurityBindingDeleteCommand>("delete").WithDescription("Delete a claim→rule binding.");
                });
            });
        });

        return app;
    }
}