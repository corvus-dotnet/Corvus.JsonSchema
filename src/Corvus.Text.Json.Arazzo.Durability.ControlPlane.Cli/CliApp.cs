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

            c.AddBranch<CommandSettings>("credentials", credentials =>
            {
                credentials.SetDescription("Manage source credential bindings — references and non-secret metadata only, never secret material (credentials:read / credentials:write).");
                credentials.AddCommand<CredentialListCommand>("list").WithDescription("List bindings as a status-first table (--status / --source filter; --output json).");
                credentials.AddCommand<CredentialGetCommand>("get").WithDescription("Show one binding's references and lifecycle metadata.");
                credentials.AddCommand<CredentialCreateCommand>("create").WithDescription("Create a binding from secret references (--ref name=scheme://...), auth kind, config, grants.");
                credentials.AddCommand<CredentialUpdateCommand>("update").WithDescription("Change a binding (merge): re-point a --ref to rotate, adjust --expires-at / --config; unspecified fields are preserved.");
                credentials.AddCommand<CredentialDeleteCommand>("delete").WithDescription("Delete a binding.");
            });

            c.AddBranch<CommandSettings>("environments", environments =>
            {
                environments.SetDescription("Manage governed, reach-scoped deployment environments and their administrators (environments:read / environments:write).");
                environments.AddCommand<EnvironmentListCommand>("list").WithDescription("List environments the caller's reach admits (--output json).");
                environments.AddCommand<EnvironmentGetCommand>("get").WithDescription("Show one environment.");
                environments.AddCommand<EnvironmentCreateCommand>("create").WithDescription("Create an environment (--display-name, --description, --manage key=value); grants the creator administration.");
                environments.AddCommand<EnvironmentUpdateCommand>("update").WithDescription("Change display name / description (merge; current-administrator only).");
                environments.AddCommand<EnvironmentDeleteCommand>("delete").WithDescription("Delete an environment (current-administrator only).");
                environments.AddBranch<CommandSettings>("administrators", administrators =>
                {
                    administrators.SetDescription("Govern who administers the environment — manage it and approve promotions into it (§7.7).");
                    administrators.AddCommand<EnvironmentAdminListCommand>("list").WithDescription("List the environment's administrators (named as deployment-mapped grants).");
                    administrators.AddCommand<EnvironmentAdminAddCommand>("add").WithDescription("Add an administrator identity (dimension value).");
                    administrators.AddCommand<EnvironmentAdminRemoveCommand>("remove").WithDescription("Remove an administrator by its identity digest (the set may not be left empty).");
                    administrators.AddCommand<EnvironmentAdminTransferCommand>("transfer").WithDescription("Replace the whole administrator set (--admin dimension=value, repeatable).");
                });
            });

            c.AddBranch<CommandSettings>("sources", sources =>
            {
                sources.SetDescription("Manage first-class, reach-scoped sources — the OpenAPI/AsyncAPI documents a workflow references by name (sources:read / sources:write).");
                sources.AddCommand<SourceListCommand>("list").WithDescription("List sources the caller's reach admits (--output json); the list omits each document.");
                sources.AddCommand<SourceGetCommand>("get").WithDescription("Show one source, including its registered document.");
                sources.AddCommand<SourceRegisterCommand>("register").WithDescription("Register a source (--type openapi|asyncapi, --document <file>, --display-name, --description, --manage key=value).");
                sources.AddCommand<SourceUpdateCommand>("update").WithDescription("Change display name / description (merge), or rotate the document with --document <file>.");
                sources.AddCommand<SourceDeleteCommand>("delete").WithDescription("Delete a source registration (its credentials are managed separately).");
            });

            c.AddBranch<CommandSettings>("availability", availability =>
            {
                availability.SetDescription("Make workflow versions available in environments — \"promotion\" (availability:read / availability:write); governed by the target environment's administrators and readiness-gated (§7.8).");
                availability.AddCommand<AvailabilityMakeCommand>("make").WithDescription("Make a version available in an environment (baseWorkflowId versionNumber environment); requires environment administration + readiness.");
                availability.AddCommand<AvailabilityWithdrawCommand>("withdraw").WithDescription("Withdraw a version's availability in an environment (baseWorkflowId versionNumber environment).");
                availability.AddCommand<AvailabilityListEnvironmentsCommand>("environments").WithDescription("List the environments a version is available in (baseWorkflowId versionNumber; --output json).");
                availability.AddCommand<AvailabilityListVersionsCommand>("versions").WithDescription("List the workflow versions available in an environment (environment; --output json).");
            });

            c.AddBranch<CommandSettings>("administrators", administrators =>
            {
                administrators.SetDescription("Manage a workflow's administrator set — the identities entitled to publish versions and govern administration (administrators:read / administrators:write).");
                administrators.AddCommand<AdministratorListCommand>("list").WithDescription("List a base id's administrators (named as deployment-mapped grants).");
                administrators.AddCommand<AdministratorAddCommand>("add").WithDescription("Add an administrator identity (dimension value).");
                administrators.AddCommand<AdministratorRemoveCommand>("remove").WithDescription("Remove an administrator by its identity digest from `list` (the set may not be left empty).");
                administrators.AddCommand<AdministratorTransferCommand>("transfer").WithDescription("Replace the whole administrator set (--admin dimension=value, repeatable).");
            });

            c.AddBranch<CommandSettings>("access-requests", accessRequests =>
            {
                accessRequests.SetDescription("Request elevated capability on a workflow, and — as a §15 administrator — decide requests (§16.5): submit, the approver queue, approve / approve-as-eligible / deny / withdraw / revoke.");
                accessRequests.AddCommand<AccessRequestSubmitCommand>("submit").WithDescription("Submit a request for capability on a workflow (--scope, repeatable; --reason; --duration-seconds).");
                accessRequests.AddCommand<AccessRequestListCommand>("list").WithDescription("List your own requests, or a workflow's approver queue (--workflow); --status filter; --output json.");
                accessRequests.AddCommand<AccessRequestGetCommand>("get").WithDescription("Show one access request.");
                accessRequests.AddCommand<AccessRequestApproveCommand>("approve").WithDescription("Approve a pending request, writing the time-boxed grant (administrator only).");
                accessRequests.AddCommand<AccessRequestApproveAsEligibleCommand>("approve-as-eligible").WithDescription("Approve a request as durable eligibility for JIT self-elevation (--window-seconds); administrator only.");
                accessRequests.AddCommand<AccessRequestDenyCommand>("deny").WithDescription("Deny a pending request (administrator only).");
                accessRequests.AddCommand<AccessRequestWithdrawCommand>("withdraw").WithDescription("Withdraw your own pending request.");
                accessRequests.AddCommand<AccessRequestRevokeCommand>("revoke").WithDescription("Revoke an approved grant or eligibility assignment early (administrator only).");
            });

            c.AddBranch<CommandSettings>("availability-requests", availabilityRequests =>
            {
                availabilityRequests.SetDescription("Request that a workflow version be made available in an environment, and — as an environment administrator — decide requests (§7.8): submit, the approver inbox, approve / deny / withdraw.");
                availabilityRequests.AddCommand<AvailabilityRequestSubmitCommand>("submit").WithDescription("Request that a version be made available in an environment (<baseWorkflowId> <versionNumber> <environment>; --reason).");
                availabilityRequests.AddCommand<AvailabilityRequestListCommand>("list").WithDescription("List your own requests, an environment's queue (--environment), or the approver inbox (--inbox); --status filter; --output json.");
                availabilityRequests.AddCommand<AvailabilityRequestGetCommand>("get").WithDescription("Show one availability request.");
                availabilityRequests.AddCommand<AvailabilityRequestApproveCommand>("approve").WithDescription("Approve a pending request, making the version available (environment administrator only; readiness-gated).");
                availabilityRequests.AddCommand<AvailabilityRequestDenyCommand>("deny").WithDescription("Deny a pending request (environment administrator only).");
                availabilityRequests.AddCommand<AvailabilityRequestWithdrawCommand>("withdraw").WithDescription("Withdraw your own pending request.");
            });
        });

        return app;
    }
}