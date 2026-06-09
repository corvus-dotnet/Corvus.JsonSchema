// <copyright file="WorkflowCheckpoint.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A checkpoint as it comes back from a store: the opaque serialized run state plus the etag it was read at.
/// </summary>
/// <remarks>
/// The bytes are the document produced by <see cref="WorkflowCheckpointSerializer"/>; the store treats them
/// as opaque. The <see cref="Etag"/> is the optimistic-concurrency token to pass back as <c>expected</c> on
/// the next save, so a resumed run advances only if no other worker has written in the meantime.
/// </remarks>
/// <param name="Utf8">The serialized checkpoint document (UTF-8 JSON).</param>
/// <param name="Etag">The etag the checkpoint was read at.</param>
public readonly record struct WorkflowCheckpoint(ReadOnlyMemory<byte> Utf8, WorkflowEtag Etag);