// <copyright file="AuditChainSource.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>One stored chain to verify: a name to report it by, and how to open its bytes. It may be opened more than once.</summary>
/// <param name="Name">The name the chain is reported by (a file name, a blob name).</param>
/// <param name="Open">Opens the chain's JSON Lines bytes from the start. The caller of <see cref="AuditChainSetVerifier"/> does not own the stream.</param>
public readonly record struct AuditChainSource(string Name, Func<Stream> Open);