// <copyright file="SecurityTag.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A security label on a run or catalog version: a key/value pair (e.g. <c>tenant=acme</c>,
/// <c>team=payments</c>). Security tags are <b>distinct from the free-form user tags</b> — they are the input
/// to tag-based row authorization (design §14.2), evaluated by a rule a principal's claims resolve to. A row
/// may carry several values for the same key. Set once when the row is created (a run inherits its workflow
/// version's security tags); not user-editable filtering metadata.
/// </summary>
/// <param name="Key">The label key (e.g. <c>tenant</c>).</param>
/// <param name="Value">The label value (e.g. <c>acme</c>).</param>
public readonly record struct SecurityTag(string Key, string Value);