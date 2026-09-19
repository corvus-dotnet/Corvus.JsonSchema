// <copyright file="CheckpointRemediation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// What a control-plane remediation changes on a run's checkpoint. Everything it does not name is carried from the
/// stored document unchanged (<see cref="WorkflowCheckpointSerializer.RewriteForRemediation"/>).
/// </summary>
/// <param name="Cursor">The cursor the run re-enters at. A remediation that does not move it passes the stored one.</param>
/// <param name="UpdatedAt">When the remediation was applied.</param>
/// <param name="ReplacesContext">Whether the run's context (<paramref name="Inputs"/> and
/// <paramref name="StepOutputs"/>) is replaced: a skip that supplies the skipped step's outputs, or a state patch.</param>
/// <param name="Inputs">The replacement inputs, when <paramref name="ReplacesContext"/>. Undefined omits them.</param>
/// <param name="StepOutputs">The replacement step outputs, when <paramref name="ReplacesContext"/>.</param>
/// <param name="Budget">The execution budget to freeze into the run in place of the stored one (ADR 0068, a
/// re-budget), or <see langword="null"/> to keep the stored budget, which is every other remediation.</param>
internal readonly record struct CheckpointRemediation(
    int Cursor,
    DateTimeOffset UpdatedAt,
    bool ReplacesContext = false,
    JsonElement Inputs = default,
    PooledUtf8Map<JsonElement>? StepOutputs = null,
    ExecutionBudget? Budget = null);