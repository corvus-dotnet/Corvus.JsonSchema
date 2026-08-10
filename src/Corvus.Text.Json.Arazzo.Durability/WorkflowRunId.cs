// <copyright file="WorkflowRunId.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The opaque, stable identity of a single workflow run — the key under which its checkpoint is stored.
/// </summary>
/// <remarks>
/// A run id is supplied by the host (it is the host's correlation handle for the run), not generated here, so
/// callers can derive it from a business key (an order id, a saga id) and make resumption idempotent. The
/// underlying value is treated as an opaque string by every backend.
/// </remarks>
/// <param name="Value">The underlying identity string.</param>
public readonly record struct WorkflowRunId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => this.Value;

    /// <summary>Converts a string to a <see cref="WorkflowRunId"/>.</summary>
    /// <param name="value">The identity string.</param>
    public static implicit operator WorkflowRunId(string value) => new(value);

    /// <summary>
    /// Tests a candidate against the run-id grammar: exactly 32 lowercase hexadecimal characters (ADR 0065 §9).
    /// </summary>
    /// <remarks>
    /// Every ingress that accepts a run id from outside a trust boundary validates this grammar before any store
    /// touch. The contract-generated surfaces enforce it through the <c>RunId</c> schema's pattern; hand-mapped
    /// ingresses (the serverless checkpoint surface, the serverless invocation parse) call this predicate. It is
    /// deliberately not enforced by the constructor: in-process callers mint conforming ids, and the type stays an
    /// opaque wrapper over whatever the store row carries.
    /// </remarks>
    /// <param name="value">The candidate run id.</param>
    /// <returns><see langword="true"/> when the candidate is inside the grammar.</returns>
    public static bool IsWellFormed(ReadOnlySpan<char> value)
    {
        if (value.Length != 32)
        {
            return false;
        }

        foreach (char c in value)
        {
            if (!char.IsAsciiHexDigitLower(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Tests a UTF-8 candidate against the run-id grammar: exactly 32 lowercase hexadecimal characters
    /// (ADR 0065 §9).
    /// </summary>
    /// <remarks>
    /// The UTF-8 variant lets an ingress that holds the candidate as bytes (a parsed JSON value, a wire token)
    /// refuse it without materializing a string: the grammar's characters are ASCII, so byte-wise inspection is
    /// exact.
    /// </remarks>
    /// <param name="utf8Value">The candidate run id as UTF-8 bytes.</param>
    /// <returns><see langword="true"/> when the candidate is inside the grammar.</returns>
    public static bool IsWellFormedUtf8(ReadOnlySpan<byte> utf8Value)
    {
        if (utf8Value.Length != 32)
        {
            return false;
        }

        foreach (byte b in utf8Value)
        {
            if (!char.IsAsciiHexDigitLower((char)b))
            {
                return false;
            }
        }

        return true;
    }
}