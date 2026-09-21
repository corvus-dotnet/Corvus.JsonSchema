// <copyright file="AuditVerifyCliTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Tests;

/// <summary>
/// <c>arazzo-runs audit verify</c> (ADR 0069) over chain files the real writer and file sink produced: it reads the
/// sink's bytes directly, and what it accepts and refuses is what an operator with the files and the public key sees.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class AuditVerifyCliTests
{
    private static readonly AuditEntry Approve = new("access-request.approve", "alice", "acme", "access-request", "req-1", "granted", "production");

    [TestMethod]
    public async Task A_directory_of_signed_chains_verifies_against_the_audit_key_and_an_anchor()
    {
        using var workspace = new TempAudit();
        List<AuditHead> anchors = await workspace.WriteChainAsync(records: 5, recordsPerHead: 2);
        AuditHead last = anchors[^1];

        (int exit, string output, _) = await RunAsync("audit", "verify", workspace.Directory, "--trust-key", "audit-1=" + workspace.PublicKeyFile, "--anchor", $"{last.ChainId}:{last.Sequence}:{last.PreviousHash}");

        exit.ShouldBe(0, output);
        output.ShouldContain("verified.");
        output.ShouldContain("records 8");
        output.ShouldContain("heads 3");
        output.ShouldContain("unsigned tail 0");
    }

    [TestMethod]
    public async Task With_no_trust_key_the_command_refuses_unless_told_to_skip_the_signatures()
    {
        using var workspace = new TempAudit();
        await workspace.WriteChainAsync(records: 2, recordsPerHead: 2);

        (int refusedExit, string refusedOut, string refusedErr) = await RunAsync("audit", "verify", workspace.Directory);
        refusedExit.ShouldNotBe(0);
        (refusedOut + refusedErr).ShouldContain("--trust-key");

        (int exit, string output, _) = await RunAsync("audit", "verify", workspace.Directory, "--no-signature-check");
        exit.ShouldBe(0, output);
        output.ShouldContain("NOT checked");
    }

    [TestMethod]
    public async Task An_altered_record_a_forged_head_and_a_wrong_anchor_each_fail()
    {
        using var workspace = new TempAudit();
        await workspace.WriteChainAsync(records: 2, recordsPerHead: 2);
        string file = Directory.GetFiles(workspace.Directory, "*.jsonl").ShouldHaveSingleItem();
        string original = await File.ReadAllTextAsync(file);
        string trust = "audit-1=" + workspace.PublicKeyFile;

        await File.WriteAllTextAsync(file, original.Replace("\"granted\"", "\"denied\""));
        (int alteredExit, string alteredOut, _) = await RunAsync("audit", "verify", file, "--trust-key", trust);
        alteredExit.ShouldBe(1);
        alteredOut.ShouldContain("does not link");
        await File.WriteAllTextAsync(file, original);

        // The same chain checked against a key that did not sign it is a forged head as far as the verifier can tell.
        using ECDsa stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string strangerFile = Path.Combine(workspace.Directory, "stranger.pub.pem");
        await File.WriteAllTextAsync(strangerFile, stranger.ExportSubjectPublicKeyInfoPem());
        (int forgedExit, string forgedOut, _) = await RunAsync("audit", "verify", file, "--trust-key", "audit-1=" + strangerFile);
        forgedExit.ShouldBe(1);
        forgedOut.ShouldContain("does not verify against the trust keys");

        string chainId = Path.GetFileNameWithoutExtension(file);
        (int anchorExit, string anchorOut, _) = await RunAsync("audit", "verify", file, "--trust-key", trust, "--anchor", $"{chainId}:2:{new string('a', 64)}");
        anchorExit.ShouldBe(1);
        anchorOut.ShouldContain("does not hold an anchor");

        (int goneExit, string goneOut, _) = await RunAsync("audit", "verify", file, "--trust-key", trust, "--anchor", $"{new string('b', 32)}:2:{new string('a', 64)}");
        goneExit.ShouldBe(1);
        goneOut.ShouldContain("no such chain was given");
    }

    [TestMethod]
    public async Task A_path_that_is_not_there_and_a_malformed_option_are_refused()
    {
        (int missingExit, _, string missingErr) = await RunAsync("audit", "verify", Path.Combine(Path.GetTempPath(), "no-such-audit-" + Guid.NewGuid().ToString("N")), "--no-signature-check");
        missingExit.ShouldBe(1);
        missingErr.ShouldContain("not found");

        using var workspace = new TempAudit();
        await workspace.WriteChainAsync(records: 1, recordsPerHead: 1);
        (int anchorExit, string anchorOut, string anchorErr) = await RunAsync("audit", "verify", workspace.Directory, "--no-signature-check", "--anchor", "not-an-anchor");
        anchorExit.ShouldNotBe(0);
        (anchorOut + anchorErr).ShouldContain("<chain>:<sequence>:<hash>");
    }

    private static async Task<(int Exit, string Stdout, string Stderr)> RunAsync(params string[] args)
    {
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();
        TextWriter previousOut = Console.Out;
        TextWriter previousError = Console.Error;
        Console.SetOut(outWriter);
        Console.SetError(errWriter);
        try
        {
            int exit = await CliApp.Create().RunAsync(args);
            return (exit, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
    }

    private sealed class TempAudit : IDisposable
    {
        private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        public TempAudit()
        {
            this.Directory = Path.Combine(Path.GetTempPath(), "arazzo-audit-cli-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(this.Directory);
            this.PublicKeyFile = Path.Combine(this.Directory, "audit-1.pub.pem");
            File.WriteAllText(this.PublicKeyFile, this.key.ExportSubjectPublicKeyInfoPem());
        }

        public string Directory { get; }

        public string PublicKeyFile { get; }

        public async Task<List<AuditHead>> WriteChainAsync(int records, int recordsPerHead)
        {
            var anchors = new List<AuditHead>();
            await using var writer = new AuditChainWriter(new FileAuditSink(this.Directory), headSigner: new EcdsaExecutorPackageSigner(this.key, "audit-1"), headOptions: new AuditHeadOptions(recordsPerHead, TimeSpan.FromHours(1)), onHeadSigned: anchors.Add);
            for (int i = 0; i < records; i++)
            {
                await writer.AppendAsync(Approve, default);
            }

            await writer.DisposeAsync();
            return anchors;
        }

        public void Dispose()
        {
            this.key.Dispose();
            System.IO.Directory.Delete(this.Directory, recursive: true);
        }
    }
}