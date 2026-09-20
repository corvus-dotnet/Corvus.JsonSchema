// <copyright file="AuditHead.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A signed head as it is published outside the sink (ADR 0069): an anchor. Whoever holds it can show that a chain
/// offered later is not the chain that was signed, because the record at <see cref="Sequence"/> of the chain
/// <see cref="ChainId"/> must carry <see cref="PreviousHash"/> and this signature. The fields are strings because an
/// anchor's destinations are a span's tags and a log record.
/// </summary>
/// <param name="ChainId">The chain's id.</param>
/// <param name="Sequence">The head record's sequence.</param>
/// <param name="PreviousHash">The head record's previous-hash: the hash of the chain's last record before the head.</param>
/// <param name="Algorithm">The signature algorithm.</param>
/// <param name="KeyId">The id of the audit key that signed.</param>
/// <param name="Signature">The signature, base64.</param>
public readonly record struct AuditHead(string ChainId, long Sequence, string PreviousHash, string Algorithm, string KeyId, string Signature);