// <copyright file="CheckpointIntegrityTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The unified MAC of ADR 0065 decision 4, as built: one HMAC over the header, key id, runner region and the digest
/// of the payload, in the row's MAC region, verified by the party that holds the key. Each tamper case below is a
/// distinct region the MAC has to bind; the control-plane region is deliberately outside it (decision 7).
/// </summary>
[TestClass]
public sealed class CheckpointIntegrityTests
{
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)(i * 7)).ToArray();
    private static readonly byte[] OtherKey = Enumerable.Range(0, 32).Select(i => (byte)(i * 11)).ToArray();
    private static readonly DateTimeOffset CreatedAt = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [TestMethod]
    public void A_sealed_row_verifies_and_names_its_generation()
    {
        byte[] row = Row(controlPlane: new ControlPlaneRecord(Budget: ExecutionBudget.Default).ToUtf8());
        CheckpointIntegrity.KeyIdOf(row).ShouldBeNull("a clear row names no generation");

        byte[] sealedRow = CheckpointIntegrity.Seal(row, "k1", Key);

        CheckpointIntegrity.KeyIdOf(sealedRow).ShouldBe("k1");
        CheckpointIntegrity.Verify(sealedRow, Key).ShouldBeTrue();
        CheckpointRowLayout layout = CheckpointRow.Parse(sealedRow);
        sealedRow[layout.Mac].Length.ShouldBe(CheckpointIntegrity.MacLength);
        sealedRow[layout.RunnerRegion].ShouldBe(row[CheckpointRow.Parse(row).RunnerRegion], "sealing changes only the key id and MAC regions");
        sealedRow[layout.ControlPlaneRegion].ShouldBe(row[CheckpointRow.Parse(row).ControlPlaneRegion]);
        WorkflowCheckpointSerializer.TryProject(sealedRow, out CheckpointProjection projection).ShouldBeTrue("a sealed row is still a row");
        projection.Sequence.ShouldBe(3);
    }

    [TestMethod]
    public void A_submission_seals_the_same_as_the_row_it_came_from()
    {
        // The runner seals what it submits (decision 7), and the server joins the region back on. The MAC has to
        // survive that join unchanged, which is what keeps the control-plane region outside its coverage.
        byte[] region = new ControlPlaneRecord(Cancellation: new ControlPlaneCancellation(CreatedAt)).ToUtf8();
        byte[] row = Row(controlPlane: []);
        byte[] sealedSubmission = CheckpointIntegrity.Seal(CheckpointRow.SubmittedBytes(row).Span, "k1", Key);

        CheckpointRow.TryParseSubmitted(sealedSubmission, out _).ShouldBeTrue("sealing a submission yields a submission");
        byte[] joined = CheckpointRow.Join(sealedSubmission, region);

        CheckpointIntegrity.Verify(joined, Key).ShouldBeTrue("the control-plane region is outside the MAC");
        CheckpointIntegrity.Verify(sealedSubmission, Key).ShouldBeTrue();
        CheckpointRow.SubmittedBytes(joined).ToArray().ShouldBe(sealedSubmission);
    }

    [TestMethod]
    public void A_clear_row_does_not_verify()
    {
        CheckpointIntegrity.Verify(Row(), Key).ShouldBeFalse("no MAC is not a valid MAC");
    }

    [TestMethod]
    public void The_wrong_key_does_not_verify()
    {
        byte[] sealedRow = CheckpointIntegrity.Seal(Row(), "k1", Key);
        CheckpointIntegrity.Verify(sealedRow, OtherKey).ShouldBeFalse();
    }

    [TestMethod]
    public void A_changed_runner_region_does_not_verify()
    {
        // The attack SEQ-1 names: the status or sequence in the envelope rewritten by a party that holds the store.
        byte[] sealedRow = CheckpointIntegrity.Seal(Row(), "k1", Key);
        CheckpointRowLayout layout = CheckpointRow.Parse(sealedRow);
        string runner = Encoding.UTF8.GetString(sealedRow[layout.RunnerRegion]).Replace("\"sequence\":3", "\"sequence\":4", StringComparison.Ordinal);
        byte[] tampered = Reframe(sealedRow, runnerRegion: Encoding.UTF8.GetBytes(runner));

        WorkflowCheckpointSerializer.TryProject(tampered, out CheckpointProjection projection).ShouldBeTrue();
        projection.Sequence.ShouldBe(4, "the tamper itself has to be well formed for the test to mean anything");
        CheckpointIntegrity.Verify(tampered, Key).ShouldBeFalse();
    }

    [TestMethod]
    public void A_changed_payload_does_not_verify()
    {
        byte[] sealedRow = CheckpointIntegrity.Seal(Row(), "k1", Key);
        byte[] tampered = Reframe(sealedRow, payload: "{\"correlationTokens\":{},\"stepOutputs\":{\"x\":1}}"u8.ToArray());

        CheckpointIntegrity.Verify(tampered, Key).ShouldBeFalse();
    }

    [TestMethod]
    public void A_repointed_key_id_does_not_verify()
    {
        // The key id is inside the coverage: a row cannot be re-pointed at another generation, which would otherwise
        // let a rolled generation's row be presented as a current one.
        byte[] sealedRow = CheckpointIntegrity.Seal(Row(), "k1", Key);
        byte[] tampered = Reframe(sealedRow, keyId: "k2"u8.ToArray());

        CheckpointIntegrity.KeyIdOf(tampered).ShouldBe("k2");
        CheckpointIntegrity.Verify(tampered, Key).ShouldBeFalse();
    }

    [TestMethod]
    public void A_changed_algorithm_selector_does_not_verify()
    {
        // The header is inside the coverage (decision 6): flipping the selector from clear to encrypted, or back,
        // is caught by the MAC rather than by whatever the selector then makes of the payload.
        byte[] sealedRow = CheckpointIntegrity.Seal(Row(), "k1", Key);
        byte[] tampered = (byte[])sealedRow.Clone();
        tampered[1] = (byte)CheckpointAlgorithm.Aes256Gcm;

        CheckpointRow.TryParse(tampered, out _).ShouldBeTrue("the tampered row still parses");
        CheckpointIntegrity.Verify(tampered, Key).ShouldBeFalse();
    }

    [TestMethod]
    public void A_changed_control_plane_region_still_verifies()
    {
        // Decision 7: the region is the control plane's to write after the runner has sealed, so it is outside the MAC.
        byte[] sealedRow = CheckpointIntegrity.Seal(Row(controlPlane: []), "k1", Key);
        byte[] rewritten = CheckpointRow.WithControlPlaneRegion(sealedRow, new ControlPlaneRecord(Cancellation: new ControlPlaneCancellation(CreatedAt)).ToUtf8());

        CheckpointIntegrity.Verify(rewritten, Key).ShouldBeTrue();
    }

    [TestMethod]
    public void A_key_id_without_a_mac_is_not_a_row()
    {
        // The clear-row rule: key id and MAC come together or not at all. Half a seal is malformed, not "clear".
        byte[] halfSealed = CheckpointRow.WithIntegrity(Row(), "k1"u8, ReadOnlySpan<byte>.Empty);
        CheckpointRow.TryParse(halfSealed, out _).ShouldBeFalse();
        byte[] macOnly = CheckpointRow.WithIntegrity(Row(), ReadOnlySpan<byte>.Empty, new byte[CheckpointIntegrity.MacLength]);
        CheckpointRow.TryParse(macOnly, out _).ShouldBeFalse();
        Should.Throw<FormatException>(() => CheckpointIntegrity.Verify(halfSealed, Key));
    }

    [TestMethod]
    public void The_message_is_length_framed()
    {
        // Two rows whose (keyId, runnerRegion) concatenations coincide must not share a MAC. The pair is a genuine
        // collision absent framing, asserted first so the test proves the framing rather than any two inputs differing.
        string.Concat("k1", "{\"a\":1}").ShouldBe(string.Concat("k1{", "\"a\":1}"));
        byte[] a = CheckpointIntegrity.Seal(Reframe(Row(), runnerRegion: "{\"a\":1}"u8.ToArray()), "k1", Key);
        byte[] b = CheckpointIntegrity.Seal(Reframe(Row(), runnerRegion: "\"a\":1}"u8.ToArray()), "k1{", Key);

        a[CheckpointRow.Parse(a).Mac].ShouldNotBe(b[CheckpointRow.Parse(b).Mac]);
    }

    private static byte[] Row(long sequence = 3, byte[]? controlPlane = null)
    {
        using var retryCounters = PooledUtf8Map<int>.Rent(0);
        using var stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        return WorkflowCheckpointSerializer.Serialize(
            new CheckpointEnvelope(
                new WorkflowRunId("run-1"),
                "production",
                "petWorkflow",
                WorkflowRunStatus.Running,
                0,
                sequence,
                Epoch: 2,
                CreatedAt,
                CreatedAt,
                CorrelationId: null,
                RerunOf: null,
                default,
                default,
                [],
                false,
                null,
                null),
            retryCounters,
            new Dictionary<string, byte[]>(StringComparer.Ordinal),
            inputs: default,
            stepOutputs,
            outputs: default,
            controlPlane ?? []);
    }

    // Re-frames a row around replacement regions, keeping every other region byte for byte.
    private static byte[] Reframe(byte[] row, byte[]? keyId = null, byte[]? runnerRegion = null, byte[]? payload = null)
    {
        CheckpointRowLayout layout = CheckpointRow.Parse(row);
        byte[] clear = CheckpointRow.WriteClear(runnerRegion ?? row[layout.RunnerRegion], payload ?? row[layout.Payload], row[layout.ControlPlaneRegion]);
        clear[1] = row[1];
        return CheckpointRow.WithIntegrity(clear, keyId ?? row[layout.KeyId], row[layout.Mac]);
    }
}