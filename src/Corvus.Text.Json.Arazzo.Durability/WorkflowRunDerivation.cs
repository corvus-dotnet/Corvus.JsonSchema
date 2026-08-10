// <copyright file="WorkflowRunDerivation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Derives deterministic run ids under the deployment's run-derivation key (ADR 0065 §9): a keyed MAC, so an
/// adversary who knows every public input still cannot compute a victim's run id offline and pre-create it, and
/// the emitted id is always inside the run-id grammar (exactly 32 lowercase hex characters).
/// </summary>
/// <remarks>
/// <para>
/// Each derivation is HMAC-SHA256 over a domain label and its inputs, every field length-framed (ADR 0065 §5's
/// framing discipline: without framing, adjacent fields can be re-split into a colliding tuple). The id is the
/// first 16 bytes of the MAC as lowercase hex — 128 bits, matching the native mint's entropy. The domain labels
/// keep the id spaces apart: an idempotent start and a schedule address can never collide by construction.
/// </para>
/// <para>
/// There is exactly one instance per deployment, shared by every surface that derives or re-derives these ids
/// (<see cref="SecuredWorkflowManagement"/> and the schedules surface); two surfaces holding different keys would
/// derive divergent ids for the same logical start and say so nowhere, which is why the instance travels through
/// <see cref="ISecuredWorkflowManagement.RunDerivation"/> rather than through separate configuration.
/// </para>
/// </remarks>
public sealed class WorkflowRunDerivation
{
    /// <summary>The minimum run-derivation-key length: 256 bits, matching the HMAC-SHA256 output, so a full-strength key is required.</summary>
    public const int MinimumKeyBytes = 32;

    private static readonly byte[] IdempotentStartDomain = "idempotent-start"u8.ToArray();
    private static readonly byte[] ScheduleAddressDomain = "schedule-address"u8.ToArray();

    private readonly ReadOnlyMemory<byte> key;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowRunDerivation"/> class.
    /// </summary>
    /// <param name="key">The deployment's run-derivation key: at least <see cref="MinimumKeyBytes"/> bytes of
    /// high-entropy key material, shared by every component of the deployment that derives run ids.</param>
    public WorkflowRunDerivation(ReadOnlyMemory<byte> key)
    {
        if (key.Length < MinimumKeyBytes)
        {
            throw ThrowHelper.GetRunDerivationKeyTooShortException(MinimumKeyBytes, nameof(key));
        }

        this.key = key;
    }

    /// <summary>
    /// Derives the run id for an idempotent start: the keyed MAC over
    /// (<paramref name="ownerGroup"/>, <paramref name="environment"/>, <paramref name="workflowId"/>,
    /// <paramref name="idempotencyKey"/>), so the same business key names different runs in different
    /// environments and under different owner groups.
    /// </summary>
    /// <param name="ownerGroup">The owner group of the environment the run is pinned to, or <see langword="null"/>
    /// for a deployment without tenancy governance (which then derives consistently with no group).</param>
    /// <param name="environment">The deployment environment the run is pinned to.</param>
    /// <param name="workflowId">The versioned workflow id the run executes.</param>
    /// <param name="idempotencyKey">The caller's stable key identifying the logical start.</param>
    /// <returns>The derived run id, inside the run-id grammar.</returns>
    public WorkflowRunId IdempotentStart(string? ownerGroup, string environment, string workflowId, string idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);
        return this.Derive(IdempotentStartDomain, ownerGroup ?? string.Empty, environment, workflowId, idempotencyKey);
    }

    /// <summary>
    /// Derives the address of a schedule's scheduler run (#896) from its <paramref name="scheduleId"/> alone,
    /// matching the schedules surface's contract that a schedule id is globally unique across the deployment. The
    /// keyed MAC still makes the address impossible to pre-compute without the key, and the domain label keeps it
    /// out of the idempotent-start id space; re-creating a schedule id in a different environment collides on the
    /// run and is refused rather than silently adopting the other environment's schedule.
    /// </summary>
    /// <param name="scheduleId">The schedule's stable, globally-unique identity.</param>
    /// <returns>The derived run id, inside the run-id grammar.</returns>
    public WorkflowRunId ScheduleAddress(string scheduleId)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);
        return this.Derive(ScheduleAddressDomain, scheduleId);
    }

    // MAC over the domain label and the fields, each length-framed (4-byte little-endian length, then the UTF-8
    // bytes). The material is assembled in pooled scratch sized by GetMaxByteCount; the one allocation is the
    // 32-character id string, which is the WorkflowRunId seam's product. Fixed-arity overloads rather than params,
    // so a derivation on the schedule fire loop allocates no argument array.
    private WorkflowRunId Derive(byte[] domain, string field)
    {
        int maxCount = sizeof(int) + domain.Length + sizeof(int) + Encoding.UTF8.GetMaxByteCount(field.Length);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxCount);
        try
        {
            int written = Frame(buffer, 0, domain);
            written = Frame(buffer, written, field);
            return this.Emit(buffer.AsSpan(0, written));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private WorkflowRunId Derive(byte[] domain, string field1, string field2, string field3, string field4)
    {
        int maxCount = sizeof(int) + domain.Length
            + sizeof(int) + Encoding.UTF8.GetMaxByteCount(field1.Length)
            + sizeof(int) + Encoding.UTF8.GetMaxByteCount(field2.Length)
            + sizeof(int) + Encoding.UTF8.GetMaxByteCount(field3.Length)
            + sizeof(int) + Encoding.UTF8.GetMaxByteCount(field4.Length);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxCount);
        try
        {
            int written = Frame(buffer, 0, domain);
            written = Frame(buffer, written, field1);
            written = Frame(buffer, written, field2);
            written = Frame(buffer, written, field3);
            written = Frame(buffer, written, field4);
            return this.Emit(buffer.AsSpan(0, written));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private WorkflowRunId Emit(ReadOnlySpan<byte> material)
    {
        Span<byte> mac = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(this.key.Span, material, mac);
        return new WorkflowRunId(Convert.ToHexStringLower(mac[..16]));
    }

    private static int Frame(byte[] buffer, int offset, byte[] value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), value.Length);
        value.CopyTo(buffer.AsSpan(offset + sizeof(int)));
        return offset + sizeof(int) + value.Length;
    }

    private static int Frame(byte[] buffer, int offset, string value)
    {
        int length = Encoding.UTF8.GetBytes(value, buffer.AsSpan(offset + sizeof(int)));
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), length);
        return offset + sizeof(int) + length;
    }
}