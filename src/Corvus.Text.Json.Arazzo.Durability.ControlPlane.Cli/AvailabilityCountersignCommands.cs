// <copyright file="AvailabilityCountersignCommands.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Spectre.Console;
using Spectre.Console.Cli;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

internal sealed class AvailabilityCountersignSettings : RunsSettings
{
    [CommandArgument(0, "<environment>")]
    [Description("The environment the executor is countersigned for; the environment is inside the signed tuple, so the countersignature admits nothing elsewhere.")]
    public string Environment { get; init; } = string.Empty;

    [CommandArgument(1, "[baseWorkflowId]")]
    [Description("The base workflow id. With a version, countersigns that one version; omitted, countersigns every version currently available in the environment.")]
    public string? BaseWorkflowId { get; init; }

    [CommandArgument(2, "[versionNumber]")]
    [Description("The 1-based version number; required with a base workflow id.")]
    public int? VersionNumber { get; init; }

    [CommandOption("--signing-key <PEM_FILE>")]
    [Description("The tenant's executor-signing private key (PKCS#8 PEM, P-256), whose public half the environment's runners pin (ADR 0065 phase C). Kept where the operator keeps keys; never sent.")]
    public string SigningKey { get; init; } = string.Empty;

    [CommandOption("--expect-digest <DIGEST>")]
    [Description("The assembly digest (sha256:<hex>) the operator reviewed. The command refuses to sign when the executor the control plane serves has another. Applies to a single version.")]
    public string? ExpectDigest { get; init; }

    public override Spectre.Console.ValidationResult Validate()
    {
        Spectre.Console.ValidationResult inherited = base.Validate();
        if (!inherited.Successful)
        {
            return inherited;
        }

        if (string.IsNullOrEmpty(this.SigningKey))
        {
            return Spectre.Console.ValidationResult.Error("--signing-key <pem-file> is required: the countersignature is made with the tenant's own executor-signing key.");
        }

        if ((this.BaseWorkflowId is null) != (this.VersionNumber is null))
        {
            return Spectre.Console.ValidationResult.Error("Name both the base workflow id and the version number, or neither to countersign every version available in the environment.");
        }

        if (this.ExpectDigest is not null && this.BaseWorkflowId is null)
        {
            return Spectre.Console.ValidationResult.Error("--expect-digest applies to a single version.");
        }

        return Spectre.Console.ValidationResult.Success();
    }
}

/// <summary>
/// The tenant operator's countersignature over a version's executor for an environment (ADR 0065 phase C). The executor
/// and its manifest are read from the control plane, the digest is computed here from the served bytes and has to be the
/// one the manifest records, the framed tuple is signed with the operator's key, and the countersignature is recorded on
/// the environment. What is signed is what the control plane serves, never what a manifest alone says.
/// </summary>
internal sealed class AvailabilityCountersignCommand : AsyncCommand<AvailabilityCountersignSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AvailabilityCountersignSettings settings, CancellationToken cancellationToken)
    {
        using var signer = ECDsa.Create();
        try
        {
            signer.ImportFromPem(await File.ReadAllTextAsync(settings.SigningKey, cancellationToken));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or CryptographicException)
        {
            Console.Error.WriteLine($"--signing-key: '{settings.SigningKey}' is not a readable P-256 private key in PEM ({ex.Message}).");
            return 1;
        }

        if (signer.KeySize != 256)
        {
            Console.Error.WriteLine("--signing-key: the executor-signing key must be a P-256 key; the countersignature is ES256.");
            return 1;
        }

        (HttpClient http, HttpClientTransport transport, ApiCatalogClient catalog) = await settings.CreateCatalogClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            var executors = new ApiEnvironmentExecutorsClient(transport);
            List<(string BaseWorkflowId, int VersionNumber)> targets;
            if (settings.BaseWorkflowId is { } baseWorkflowId && settings.VersionNumber is { } versionNumber)
            {
                targets = [(baseWorkflowId, versionNumber)];
            }
            else
            {
                targets = await ListAvailableAsync(new ApiAvailabilityClient(transport), settings.Environment, cancellationToken);
                if (targets.Count == 0)
                {
                    Console.WriteLine($"Nothing is available in '{settings.Environment}', so nothing was countersigned.");
                    return 0;
                }
            }

            foreach ((string target, int version) in targets)
            {
                int exit = await CountersignOneAsync(catalog, executors, signer, settings, target, version, cancellationToken);
                if (exit != 0)
                {
                    return exit;
                }
            }

            return 0;
        }
    }

    private static async Task<int> CountersignOneAsync(ApiCatalogClient catalog, ApiEnvironmentExecutorsClient executors, ECDsa signer, AvailabilityCountersignSettings settings, string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        // The executor the control plane serves, digested here. The countersignature names what the operator reviewed,
        // and that is these bytes; a manifest that says otherwise names another executor and is refused.
        byte[] executor;
        await using (GetCatalogExecutorResponse served = await catalog.GetCatalogExecutorAsync(baseWorkflowId, (Models.VersionNumber.Source)versionNumber, cancellationToken))
        {
            if (served.StatusCode != 200 || !served.TryGetOkStream(out Stream? stream))
            {
                Console.Error.WriteLine($"Version {versionNumber} of '{baseWorkflowId}' has no executor to countersign (HTTP {served.StatusCode}).");
                return 1;
            }

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            executor = buffer.ToArray();
        }

        string digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(executor));
        string packageHash;
        await using (GetCatalogExecutorManifestResponse manifest = await catalog.GetCatalogExecutorManifestAsync(baseWorkflowId, (Models.VersionNumber.Source)versionNumber, cancellationToken))
        {
            if (manifest.StatusCode != 200)
            {
                Console.Error.WriteLine($"Version {versionNumber} of '{baseWorkflowId}' has no executor manifest (HTTP {manifest.StatusCode}).");
                return 1;
            }

            var root = (JsonElement)manifest.OkBody;
            if (!root.TryGetProperty("assemblyDigest"u8, out JsonElement recordedDigest) || recordedDigest.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("packageHash"u8, out JsonElement recordedHash) || recordedHash.ValueKind != JsonValueKind.String)
            {
                Console.Error.WriteLine($"The executor manifest of version {versionNumber} of '{baseWorkflowId}' records no assembly digest or package hash.");
                return 1;
            }

            if (!recordedDigest.ValueEquals(digest))
            {
                Console.Error.WriteLine($"Refused: the executor the control plane serves for version {versionNumber} of '{baseWorkflowId}' digests to {digest}, but its manifest records {recordedDigest.GetString()}. Nothing was signed.");
                return 1;
            }

            packageHash = recordedHash.GetString()!;
        }

        if (settings.ExpectDigest is { } expected && !string.Equals(expected, digest, StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Refused: the executor the control plane serves for version {versionNumber} of '{baseWorkflowId}' digests to {digest}, not the reviewed {expected}. Nothing was signed.");
            return 1;
        }

        byte[] signature = Corvus.Text.Json.Arazzo.Durability.Environments.ExecutorCountersignature.Sign(signer, settings.Environment, baseWorkflowId, versionNumber, packageHash, digest);
        await using CountersignExecutorResponse response = await executors.CountersignExecutorAsync(
            settings.Environment,
            baseWorkflowId,
            (Models.VersionNumber.Source)versionNumber,
            Models.ExecutorCountersignature.Build(
                assemblyDigest: digest,
                packageHash: packageHash,
                signature: (Models.JsonCorvusBase64String.Source)Convert.ToBase64String(signature)),
            cancellationToken);
        return response.MatchResult(
            recorded =>
            {
                Console.WriteLine($"Countersigned version {versionNumber} of '{baseWorkflowId}' for '{settings.Environment}': executor {digest}, package {packageHash}.");
                return 0;
            },
            Output.Problem,
            Output.Problem,
            Output.Problem,
            Output.Problem,
            Output.Unexpected);
    }

    private static async Task<List<(string BaseWorkflowId, int VersionNumber)>> ListAvailableAsync(ApiAvailabilityClient availability, string environment, CancellationToken cancellationToken)
    {
        var targets = new List<(string, int)>();
        string? pageToken = null;
        do
        {
            string? next = null;
            await using ListEnvironmentAvailabilityResponse response = await availability.ListEnvironmentAvailabilityAsync(environment, pageToken: pageToken is null ? default : (Models.JsonString.Source)pageToken, cancellationToken: cancellationToken);
            if (response.StatusCode != 200)
            {
                throw new InvalidOperationException($"Listing what is available in '{environment}' failed (HTTP {response.StatusCode}).");
            }

            foreach (Models.AvailabilityEntry entry in response.OkBody.Availability.EnumerateArray())
            {
                targets.Add(((string)entry.BaseWorkflowId, (int)entry.VersionNumber));
            }

            if (((JsonElement)response.OkBody.NextPageToken).ValueKind == JsonValueKind.String)
            {
                next = (string)response.OkBody.NextPageToken;
            }

            pageToken = next;
        }
        while (pageToken is not null);

        return targets;
    }
}

internal sealed class AvailabilityCountersignaturesSettings : RunsSettings
{
    [CommandArgument(0, "<environment>")]
    [Description("The environment.")]
    public string Environment { get; init; } = string.Empty;

    [CommandOption("--output <FORMAT>")]
    [Description("Output format: table (default) or json.")]
    [DefaultValue("table")]
    public string Output { get; init; } = "table";
}

internal sealed class AvailabilityCountersignaturesCommand : AsyncCommand<AvailabilityCountersignaturesSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AvailabilityCountersignaturesSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiCatalogClient _) = await settings.CreateCatalogClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            var executors = new ApiEnvironmentExecutorsClient(transport);
            await using ListExecutorCountersignaturesResponse response = await executors.ListExecutorCountersignaturesAsync(settings.Environment, cancellationToken: cancellationToken);
            if (response.StatusCode != 200)
            {
                return response.MatchResult(_ => 0, Output.Problem, Output.Problem, Output.Unexpected);
            }

            if (settings.Output.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(response.OkBody.ToString());
                return 0;
            }

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Workflow");
            table.AddColumn("Version");
            table.AddColumn("Assembly digest");
            table.AddColumn("Package hash");
            table.AddColumn("Signed by");
            table.AddColumn("Signed at");
            foreach (Models.ExecutorCountersignatureView countersignature in response.OkBody.Countersignatures.EnumerateArray())
            {
                table.AddRow(
                    Markup.Escape((string)countersignature.BaseWorkflowId),
                    ((int)countersignature.VersionNumber).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Markup.Escape((string)countersignature.AssemblyDigest),
                    Markup.Escape((string)countersignature.PackageHash),
                    Markup.Escape((string)countersignature.SignedBy),
                    Markup.Escape((string)countersignature.SignedAt));
            }

            AnsiConsole.Write(table);
            return 0;
        }
    }
}

internal sealed class AvailabilityWithdrawCountersignatureSettings : RunsSettings
{
    [CommandArgument(0, "<environment>")]
    [Description("The environment.")]
    public string Environment { get; init; } = string.Empty;

    [CommandArgument(1, "<baseWorkflowId>")]
    [Description("The base workflow id.")]
    public string BaseWorkflowId { get; init; } = string.Empty;

    [CommandArgument(2, "<versionNumber>")]
    [Description("The 1-based version number.")]
    public int VersionNumber { get; init; }
}

internal sealed class AvailabilityWithdrawCountersignatureCommand : AsyncCommand<AvailabilityWithdrawCountersignatureSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, AvailabilityWithdrawCountersignatureSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiCatalogClient _) = await settings.CreateCatalogClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            var executors = new ApiEnvironmentExecutorsClient(transport);
            await using WithdrawExecutorCountersignatureResponse response = await executors.WithdrawExecutorCountersignatureAsync(settings.Environment, settings.BaseWorkflowId, (Models.VersionNumber.Source)settings.VersionNumber, cancellationToken);
            if (response.StatusCode == 204)
            {
                Console.WriteLine($"Withdrew the countersignature of version {settings.VersionNumber} of '{settings.BaseWorkflowId}' for '{settings.Environment}'.");
                return 0;
            }

            return response.MatchResult(Output.Problem, Output.Problem, Output.Problem, Output.Unexpected);
        }
    }
}