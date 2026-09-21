// <copyright file="AuditCommands.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Execution;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

/// <summary>Settings for <c>audit verify</c>.</summary>
internal sealed class AuditVerifySettings : CommandSettings
{
    [CommandArgument(0, "<path>")]
    [Description("A chain file, a directory of chain files (*.jsonl), or - to read one chain from standard input.")]
    public string Path { get; init; } = string.Empty;

    [CommandOption("--trust-key <KEYID=PEMFILE>")]
    [Description("One of the audit's public keys: its key id, and a PEM file holding it. Repeat for each key. The heads' signatures are checked against these.")]
    public string[] TrustKeys { get; init; } = [];

    [CommandOption("--anchor <CHAIN:SEQUENCE:HASH>")]
    [Description("An anchor held outside the sink, from an audit.head span or an Audit anchor log record. The chain it names must hold it. Repeat for each anchor.")]
    public string[] Anchors { get; init; } = [];

    [CommandOption("--no-signature-check")]
    [Description("Verify the chains' links only, with no trust key. The heads' signatures are NOT checked, so a rewritten tail is not shown.")]
    public bool NoSignatureCheck { get; init; }
}

/// <summary>
/// Verifies audit chains from their stored bytes (ADR 0069). It reads the sink's files directly and calls no server, so
/// the control plane that wrote the evidence is not in the path that checks it.
/// </summary>
internal sealed class AuditVerifyCommand : AsyncCommand<AuditVerifySettings>
{
    internal static bool TryParseAnchor(string text, out AuditHead anchor)
    {
        anchor = default;
        string[] parts = text.Split(':');
        if (parts.Length != 3
            || parts[0].Length != AuditRecord.ChainIdLength
            || parts[2].Length != AuditRecord.HashLength
            || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long sequence))
        {
            return false;
        }

        // A verifier needs an anchor's place in its chain. The signature it was published with is checked where the head
        // itself is, against the trust keys.
        anchor = new AuditHead(parts[0], sequence, parts[2], string.Empty, string.Empty, string.Empty);
        return true;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, AuditVerifySettings settings, CancellationToken cancellationToken)
    {
        if (Refuse(settings) is { } refusal)
        {
            Console.Error.WriteLine(refusal);
            return 1;
        }

        List<AuditChainSource>? chains = await ReadChainsAsync(settings.Path, cancellationToken).ConfigureAwait(false);
        if (chains is null)
        {
            return 1;
        }

        TrustStoreExecutorPackageVerifier? trustStore = null;
        if (settings.TrustKeys.Length > 0)
        {
            var keys = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string key in settings.TrustKeys)
            {
                int split = key.IndexOf('=');
                string pemFile = key[(split + 1)..];
                if (!File.Exists(pemFile))
                {
                    Console.Error.WriteLine($"trust key file not found: {pemFile}");
                    return 1;
                }

                keys[key[..split]] = await File.ReadAllTextAsync(pemFile, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                trustStore = TrustStoreExecutorPackageVerifier.FromPem(keys);
            }
            catch (Exception ex) when (ex is ArgumentException or CryptographicException)
            {
                Console.Error.WriteLine($"a trust key could not be read: {ex.Message}");
                return 1;
            }
        }

        var anchors = new List<AuditHead>(settings.Anchors.Length);
        foreach (string text in settings.Anchors)
        {
            TryParseAnchor(text, out AuditHead anchor);
            anchors.Add(anchor);
        }

        AuditChainSetVerification result = await AuditChainSetVerifier.VerifyAsync(chains, trustStore, anchors, cancellationToken).ConfigureAwait(false);

        IAnsiConsole console = OperatorCommandHelpers.CreateConsole();
        foreach (AuditChainSetEntry chain in result.Chains)
        {
            AuditChainVerification v = chain.Verification;
            bool stands = chain.Standing is AuditChainStanding.Verified or AuditChainStanding.AbandonedAndContinued or AuditChainStanding.Empty;
            console.MarkupLine($"{(stands ? "[green]✓[/]" : "[red]✗[/]")} {Markup.Escape(chain.Name)}: {Describe(chain)}");
            console.MarkupLine($"    [dim]chain[/] {Markup.Escape(v.ChainId ?? "—")}  [dim]records[/] {v.RecordCount}  [dim]heads[/] {v.HeadCount}  [dim]unsigned tail[/] {v.UnsignedTailCount}");
            if (v.ContinuesChain is not null)
            {
                console.MarkupLine($"    [dim]continues[/] {Markup.Escape(v.ContinuesChain)} [dim]from[/] {Markup.Escape(v.ContinuesHash ?? "—")}");
            }
        }

        foreach (AuditHead anchor in result.UnmatchedAnchors)
        {
            console.MarkupLine($"[red]✗[/] anchor for chain {Markup.Escape(anchor.ChainId)} at sequence {anchor.Sequence}: no such chain was given. The chain was removed whole, or was not supplied.");
        }

        if (trustStore is null)
        {
            console.MarkupLine("[yellow]![/] The heads' signatures were NOT checked (--no-signature-check). The links are verified; a tail rewritten with its hashes recomputed would not be shown.");
        }

        console.MarkupLine(result.IsIntact
            ? $"[green]✓[/] {result.Chains.Count} chain(s) verified."
            : "[red]✗[/] verification failed.");
        return result.IsIntact ? 0 : 1;
    }

    // An unchecked chain must never read as verified by accident, so the command refuses to run with no trust key unless
    // it is told, in so many words, to skip the signatures.
    private static string? Refuse(AuditVerifySettings settings)
    {
        if (settings.TrustKeys.Length == 0 && !settings.NoSignatureCheck)
        {
            return "No --trust-key was given, so the heads' signatures cannot be checked and a rewritten tail would not be shown. Give the audit's public key with --trust-key <keyId>=<pemFile>, or pass --no-signature-check to verify the links only.";
        }

        if (settings.TrustKeys.Length > 0 && settings.NoSignatureCheck)
        {
            return "--trust-key and --no-signature-check contradict each other.";
        }

        foreach (string key in settings.TrustKeys)
        {
            if (key.IndexOf('=') is <= 0 || key.EndsWith('='))
            {
                return $"--trust-key '{key}' is not <keyId>=<pemFile>.";
            }
        }

        foreach (string anchor in settings.Anchors)
        {
            if (!TryParseAnchor(anchor, out _))
            {
                return $"--anchor '{anchor}' is not <chain>:<sequence>:<hash>, with a 32-hex chain id and a 64-hex hash.";
            }
        }

        return null;
    }

    private static string Describe(in AuditChainSetEntry chain)
    {
        AuditChainVerification v = chain.Verification;
        return chain.Standing switch
        {
            AuditChainStanding.Verified => "verified.",
            AuditChainStanding.AbandonedAndContinued => $"verified up to a torn last line (line {v.BreakLine}), which a later chain continues from. This is what a failed append leaves.",
            AuditChainStanding.Empty => "holds no whole record. This is what a failed first append leaves; nothing ties it to the evidence either way.",
            AuditChainStanding.PredecessorMissing => $"continues chain {v.ContinuesChain}, which was not given. That chain was removed whole, or was not supplied.",
            AuditChainStanding.ContinuationNotFound => $"continues chain {v.ContinuesChain} from a record that chain does not hold. That chain was cut short or rewritten.",
            _ => v.Break switch
            {
                AuditChainBreak.TornTail => $"line {v.BreakLine} is torn and no later chain continues from the record before it. The chain was cut short.",
                AuditChainBreak.MalformedRecord => $"line {v.BreakLine} is not an audit record.",
                AuditChainBreak.ForeignRecord => $"line {v.BreakLine} belongs to another chain.",
                AuditChainBreak.SequenceGap => $"line {v.BreakLine} is out of sequence: a record was removed, added or reordered.",
                AuditChainBreak.HashMismatch => $"line {v.BreakLine} does not link to the record before it: that record, or this one, was altered.",
                AuditChainBreak.HeadSignatureInvalid => $"the head at line {v.BreakLine} does not verify against the trust keys: it was altered or forged, or signed with a key that was not given.",
                AuditChainBreak.AnchorNotFound => "does not hold an anchor given for it. It is not the chain that was signed, or it was cut short of the anchor.",
                _ => "failed verification.",
            },
        };
    }

    private static async Task<List<AuditChainSource>?> ReadChainsAsync(string path, CancellationToken cancellationToken)
    {
        if (path == "-")
        {
            // Standard input cannot be read twice, and a chain may be: once on its own, again for an anchor or for the
            // hash a later chain names.
            var buffered = new MemoryStream();
            using (Stream stdin = Console.OpenStandardInput())
            {
                await stdin.CopyToAsync(buffered, cancellationToken).ConfigureAwait(false);
            }

            byte[] bytes = buffered.ToArray();
            return [new AuditChainSource("(standard input)", () => new MemoryStream(bytes, writable: false))];
        }

        if (File.Exists(path))
        {
            return [new AuditChainSource(System.IO.Path.GetFileName(path), () => File.OpenRead(path))];
        }

        if (Directory.Exists(path))
        {
            string[] files = Directory.GetFiles(path, "*" + FileAuditSink.ChainFileExtension);
            Array.Sort(files, StringComparer.Ordinal);
            if (files.Length == 0)
            {
                Console.Error.WriteLine($"no chain files (*{FileAuditSink.ChainFileExtension}) in: {path}");
                return null;
            }

            var chains = new List<AuditChainSource>(files.Length);
            foreach (string file in files)
            {
                chains.Add(new AuditChainSource(System.IO.Path.GetFileName(file), () => File.OpenRead(file)));
            }

            return chains;
        }

        Console.Error.WriteLine($"not found: {path}");
        return null;
    }
}