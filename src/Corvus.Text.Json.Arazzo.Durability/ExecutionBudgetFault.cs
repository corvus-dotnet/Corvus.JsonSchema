// <copyright file="ExecutionBudgetFault.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The error types a run faults with when it exceeds its <see cref="ExecutionBudget"/> (ADR 0068), one per limit, and
/// the one predicate that decides whether a run's recorded facts are past its budget. The runner and the control plane
/// both decide from this predicate: the runner cooperatively as it advances, the checkpoint coordinator authoritatively
/// on every save and the runner API at claim time, so a runner that ignores the budget cannot persist or resume a run
/// past it.
/// </summary>
/// <remarks>
/// A budget fault is terminal. The whole-step resilience layer treats it as non-retryable, dispatch never reclaims a
/// run carrying one, and resuming such a run is refused: a run that hit its budget has done something its author did
/// not intend, and the considered shape is a new run under a raised budget.
/// </remarks>
public static class ExecutionBudgetFault
{
    /// <summary>The run's step journal is longer than the budget's fuel, or was truncated at the journal cap.</summary>
    public const string Fuel = "budget-fuel";

    /// <summary>The run is older than the budget's wall clock, measured from its creation.</summary>
    public const string Deadline = "budget-deadline";

    /// <summary>The run tried to enter a sub-workflow deeper than the budget's depth cap.</summary>
    public const string Depth = "budget-depth";

    /// <summary>Whether an error type is one of the budget faults.</summary>
    /// <param name="errorType">The run's error type, as its index or fault record carries it.</param>
    /// <returns><see langword="true"/> for <see cref="Fuel"/>, <see cref="Deadline"/> or <see cref="Depth"/>.</returns>
    public static bool IsBudgetFault(string? errorType)
        => errorType is Fuel or Deadline or Depth;

    /// <summary>Whether an error type, as UTF-8 text, is one of the budget faults.</summary>
    /// <param name="errorTypeUtf8">The error type text.</param>
    /// <returns><see langword="true"/> for <see cref="Fuel"/>, <see cref="Deadline"/> or <see cref="Depth"/>.</returns>
    public static bool IsBudgetFault(ReadOnlySpan<byte> errorTypeUtf8)
        => errorTypeUtf8.SequenceEqual("budget-fuel"u8)
        || errorTypeUtf8.SequenceEqual("budget-deadline"u8)
        || errorTypeUtf8.SequenceEqual("budget-depth"u8);

    /// <summary>
    /// Decides whether a run's recorded facts are past its budget, and which limit it hit. Fuel is decided first: a
    /// journal longer than the fuel, or one truncated at the cap (which is over any admissible budget by definition),
    /// is a fuel fault whatever the run's age; otherwise a run older than its wall clock is a deadline fault.
    /// </summary>
    /// <param name="budget">The run's effective budget.</param>
    /// <param name="facts">The facts read from the checkpoint body.</param>
    /// <param name="createdAt">The run's creation time, from the run's frozen identity.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The fault's error type, or <see langword="null"/> when the run is within its budget.</returns>
    public static string? Find(in ExecutionBudget budget, in CheckpointBudgetFacts facts, DateTimeOffset createdAt, DateTimeOffset now)
    {
        if (facts.JournalTruncated || facts.JournalCount > budget.MaxSteps)
        {
            return Fuel;
        }

        return now - createdAt > budget.WallClock ? Deadline : null;
    }
}

/// <summary>
/// The facts a checkpoint body carries that the budget predicate reads, without materializing the run's working
/// state: the run's frozen budget, how many steps its journal records, whether that journal was truncated at the
/// cap, and whether the run already faulted on its budget.
/// </summary>
/// <param name="Budget">The run's effective budget, or <see langword="null"/> on a checkpoint written before budgets existed.</param>
/// <param name="JournalCount">The number of entries in the step journal.</param>
/// <param name="JournalTruncated">Whether the journal was truncated at the cap.</param>
/// <param name="BudgetFaulted">Whether the run's fault record already carries a budget fault.</param>
public readonly record struct CheckpointBudgetFacts(ExecutionBudget? Budget, int JournalCount, bool JournalTruncated, bool BudgetFaulted);