// <copyright file="FormatAssertionMode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Determines how a JSON Schema <c>format</c> assertion is emitted by the code
/// generation pipeline for a particular format.
/// </summary>
/// <remarks>
/// The mode is resolved at code-generation time, not at runtime. Each mode emits
/// different static code, so the default <see cref="Assert"/> path carries no
/// additional runtime branches.
/// </remarks>
public enum FormatAssertionMode
{
    /// <summary>
    /// The format is validated and a non-conformant value fails validation. This is
    /// the standard behaviour.
    /// </summary>
    Assert,

    /// <summary>
    /// The format keyword becomes annotation-only: no validation is performed.
    /// </summary>
    Disable,

    /// <summary>
    /// The format is validated, but validation always succeeds. A non-conformant
    /// value produces a <c>WARNING</c> annotation rather than a failure.
    /// </summary>
    Warning,
}