// <copyright file="RunnerApiCheckpointStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json.Arazzo.Durability.Runner.Client.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client;

/// <summary>
/// The <see cref="IWorkflowCheckpointStore"/> a claimed run loads and saves through: every read and write goes to the
/// runner API under the lease the client already holds, so the run itself is unaware it is not talking to a database.
/// </summary>
internal sealed class RunnerApiCheckpointStore : IWorkflowCheckpointStore
{
    private readonly ArazzoRunnerClient owner;

    internal RunnerApiCheckpointStore(ArazzoRunnerClient owner) => this.owner = owner;

    /// <inheritdoc/>
    public async ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        string token = this.owner.RequireLease(id);
        await using LoadCheckpointResponse response = await this.owner.CheckpointsClient
            .LoadCheckpointAsync(id.Value, token, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == 409)
        {
            this.owner.Forget(id);
            throw new RunnerLeaseLostException(id);
        }

        if (response.StatusCode == 404)
        {
            // The lease is current and the row is absent. A fresh run legitimately has none; a runner that believed it
            // held state for this run treats it as a fault, which is its judgement to make rather than this store's.
            return null;
        }

        if (response.StatusCode != 200 || !response.TryGetOkStream(out Stream? body))
        {
            throw ArazzoRunnerClient.Refused($"load the checkpoint for run '{id.Value}'", response.StatusCode);
        }

        using var buffer = new MemoryStream();
        await body.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        // The etag the run threads is not the predicate on this path — the sequence inside the document is (ADR 0065
        // decision 6) — so it carries the persisted sequence rather than a store etag, and nothing reads it back.
        long persisted = SequenceHeader(response);
        return new WorkflowCheckpoint(buffer.ToArray(), new WorkflowEtag(persisted.ToString(CultureInfo.InvariantCulture)));
    }

    /// <inheritdoc/>
    // Not async: the interface takes the index by `in`, which an async method may not. The synchronous part reads
    // the sequence and then hands off to the core, which is where the await lives.
    public ValueTask<WorkflowEtag> SaveAsync(WorkflowRunId id, ReadOnlyMemory<byte> checkpointUtf8, in WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
    {
        string token = this.owner.RequireLease(id);

        // The sequence comes from the document the run authored — one series, one author — and is read by a forward-only
        // scan that stops at the property rather than a parse of the whole checkpoint. The index travels nowhere: the
        // server re-projects it from these same bytes, so sending it would be sending a second copy of what it already
        // has, and one the server could not trust anyway.
        WorkflowCheckpointSerializer.TryReadSequence(checkpointUtf8, out long sequence);
        return this.SaveCoreAsync(id, checkpointUtf8, token, sequence, cancellationToken);
    }

    private static long SequenceHeader(LoadCheckpointResponse response)
        => response.XArazzoCheckpointSeqHeader.IsNotUndefined() ? (long)response.XArazzoCheckpointSeqHeader : 0;

    private async ValueTask<WorkflowEtag> SaveCoreAsync(WorkflowRunId id, ReadOnlyMemory<byte> checkpointUtf8, string token, long sequence, CancellationToken cancellationToken)
    {
        using var body = new ReadOnlyMemoryStream(checkpointUtf8);
        await using SaveCheckpointResponse response = await this.owner.CheckpointsClient
            .SaveCheckpointAsync(id.Value, token, sequence, body, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == 409)
        {
            // Both refusals land here and are told apart by the problem type, because both mean the same thing about
            // the write — nothing was stored — and different things about what the runner should do next.
            CheckpointWriteProblem problem = response.ConflictBody;
            if (problem.IsNotUndefined() && problem.Type.IsNotUndefined() && problem.Type.ValueEquals(RunnerProblemTypes.LeaseLost))
            {
                this.owner.Forget(id);
                throw new RunnerLeaseLostException(id);
            }

            long accepted = problem.IsNotUndefined() && problem.AcceptedSequence.IsNotUndefined()
                ? (long)problem.AcceptedSequence
                : 0;
            throw new CheckpointSupersededException(id, sequence, accepted);
        }

        if (response.StatusCode != 204)
        {
            throw ArazzoRunnerClient.Refused($"save the checkpoint for run '{id.Value}'", response.StatusCode);
        }

        return new WorkflowEtag(sequence.ToString(CultureInfo.InvariantCulture));
    }

    // A read-only stream over the caller's buffer, so the body is sent without a copy. The buffer is alive for the
    // duration of the send: the caller may reuse it once SaveAsync returns, and this save is awaited before it does.
    private sealed class ReadOnlyMemoryStream(ReadOnlyMemory<byte> memory) : Stream
    {
        private int position;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => memory.Length;

        public override long Position
        {
            get => this.position;
            set => this.position = checked((int)value);
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => this.Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            int take = Math.Min(buffer.Length, memory.Length - this.position);
            if (take <= 0)
            {
                return 0;
            }

            memory.Span.Slice(this.position, take).CopyTo(buffer);
            this.position += take;
            return take;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => this.position + offset,
                SeekOrigin.End => memory.Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };

            this.position = checked((int)target);
            return this.position;
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

/// <summary>The problem types the runner API answers with, as the client compares them.</summary>
internal static class RunnerProblemTypes
{
    /// <summary>The problem type for a lease that is no longer current.</summary>
    internal static ReadOnlySpan<byte> LeaseLost => "https://corvus-oss.org/arazzo/runner/problems/lease-lost"u8;
}