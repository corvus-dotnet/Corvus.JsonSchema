// <copyright file="FormatAssertionModeParser.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Helpers for parsing per-format <see cref="FormatAssertionMode"/> configuration from the
/// textual forms used by the CLI (<c>date-time=disable,time=warning</c>) and the MSBuild
/// source-generator property (<c>date-time=disable;time=warning</c>).
/// </summary>
public static class FormatAssertionModeParser
{
    /// <summary>
    /// Tries to parse a single mode token (<c>assert</c>, <c>disable</c> or <c>warning</c>,
    /// case-insensitive).
    /// </summary>
    /// <param name="value">The value to parse.</param>
    /// <param name="mode">The parsed mode.</param>
    /// <returns><see langword="true"/> if the value was a recognized mode.</returns>
    public static bool TryParseMode(string? value, out FormatAssertionMode mode)
    {
        if (value is not null)
        {
            if (string.Equals(value, "assert", StringComparison.OrdinalIgnoreCase))
            {
                mode = FormatAssertionMode.Assert;
                return true;
            }

            if (string.Equals(value, "disable", StringComparison.OrdinalIgnoreCase))
            {
                mode = FormatAssertionMode.Disable;
                return true;
            }

            if (string.Equals(value, "warning", StringComparison.OrdinalIgnoreCase))
            {
                mode = FormatAssertionMode.Warning;
                return true;
            }
        }

        mode = FormatAssertionMode.Assert;
        return false;
    }

    /// <summary>
    /// Parses a per-format mode specification consisting of <c>format=mode</c> pairs separated
    /// by any of the given <paramref name="separators"/> (e.g. <c>date-time=disable,time=warning</c>).
    /// </summary>
    /// <param name="specification">The specification to parse. May be <see langword="null"/> or empty.</param>
    /// <param name="separators">The pair separators (e.g. <c>,</c> for the CLI, <c>;</c> for MSBuild).</param>
    /// <returns>A dictionary of format name to <see cref="FormatAssertionMode"/>. Empty if there were no pairs.</returns>
    /// <exception cref="FormatException">A pair was malformed or specified an unrecognized mode.</exception>
    public static IReadOnlyDictionary<string, FormatAssertionMode> ParseSpecification(string? specification, params char[] separators)
    {
        Dictionary<string, FormatAssertionMode> result = new(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(specification))
        {
            return result;
        }

        foreach (string rawPair in specification!.Split(separators, StringSplitOptions.RemoveEmptyEntries))
        {
            string pair = rawPair.Trim();
            if (pair.Length == 0)
            {
                continue;
            }

            int equalsIndex = pair.IndexOf('=');
            if (equalsIndex < 0)
            {
                // A bare mode (no '=') sets the wildcard "*" default applied to every format — e.g.
                // 'disable' to generate annotation-only output across all formats and drafts.
                if (TryParseMode(pair, out FormatAssertionMode bareMode))
                {
                    result["*"] = bareMode;
                    continue;
                }

                throw new FormatException($"Invalid format mode entry '{pair}'. Expected '<format>=<assert|disable|warning>', or a bare '<assert|disable|warning>' to set the default for all formats.");
            }

            if (equalsIndex == 0 || equalsIndex == pair.Length - 1)
            {
                throw new FormatException($"Invalid format mode entry '{pair}'. Expected '<format>=<assert|disable|warning>'.");
            }

            string format = pair.Substring(0, equalsIndex).Trim();
            string modeText = pair.Substring(equalsIndex + 1).Trim();

            if (format.Length == 0 || !TryParseMode(modeText, out FormatAssertionMode mode))
            {
                throw new FormatException($"Invalid format mode entry '{pair}'. Expected '<format>=<assert|disable|warning>'.");
            }

            result[format] = mode;
        }

        return result;
    }
}