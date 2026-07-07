// <copyright file="DraftRun.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The audited capture record of a §18 draft run: which working copy's document the run executes, at which etag,
/// who started it, and where — persisted in the <see cref="IDraftRunStore"/> alongside the captured package,
/// keyed by the run id. Every backend stores it as its JSON document verbatim, exactly as the runner registry
/// stores a <see cref="RunnerRegistration"/>.
/// </summary>
[JsonSchemaTypeGenerator("../Schemas/DraftRun.json")]
public readonly partial struct DraftRun
{
    /// <summary>Gets the run id as a string.</summary>
    public string RunIdValue => (string)this.RunId;

    /// <summary>Gets the working copy id as a string.</summary>
    public string WorkingCopyIdValue => (string)this.WorkingCopyId;

    /// <summary>Gets the content hash of the captured document + sources as a string — the runner's compile-cache
    /// key and the executor manifest's integrity binding.</summary>
    public string ContentHashValue => (string)this.ContentHash;

    /// <summary>Parses a <see cref="DraftRun"/> from its persisted JSON document as a detached value (one owned copy).</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The draft-run record.</returns>
    public static DraftRun FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);
}