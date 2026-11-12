// <copyright file="SuiteCompatibility.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace TestUtilities.JsonSchemaTestSuite;

/// <summary>
/// Evaluates the <c>compatibility</c> property of a JSON-Schema-Test-Suite test case.
/// </summary>
/// <remarks>
/// <para>
/// The value is a comma-separated list of constraints, ordered from the smallest release to the
/// largest. A bare number is a minimum, <c>&lt;=</c> a maximum and <c>=</c> an exact release.
/// Releases are numbered 3, 4, 6, 7, 2019 and 2020 (the draft number, or the year).
/// </para>
/// <para>
/// This follows the algorithm in the suite's <c>validation/README.md</c>: each constraint sets the
/// answer, and evaluation stops at the first constraint whose release is at or above the release
/// under test.
/// </para>
/// </remarks>
public static class SuiteCompatibility
{
    /// <summary>
    /// Determines whether a test case applies to the given release.
    /// </summary>
    /// <param name="compatibility">The test case's <c>compatibility</c> value, or <see langword="null"/> if it has none.</param>
    /// <param name="release">The release under test (3, 4, 6, 7, 2019 or 2020).</param>
    /// <returns><see langword="true"/> if the test case applies to the release.</returns>
    public static bool IsCompatible(string? compatibility, int release)
    {
        if (compatibility is null)
        {
            return true;
        }

        bool isValid = true;

        foreach (string rawConstraint in compatibility.Split(','))
        {
            string constraint = rawConstraint.Trim();
            int version;

            if (constraint.StartsWith("<=", StringComparison.Ordinal))
            {
                version = int.Parse(constraint.Substring(2), System.Globalization.CultureInfo.InvariantCulture);
                isValid = release <= version;
            }
            else if (constraint.StartsWith("=", StringComparison.Ordinal))
            {
                version = int.Parse(constraint.Substring(1), System.Globalization.CultureInfo.InvariantCulture);
                isValid = release == version;
            }
            else
            {
                version = int.Parse(constraint, System.Globalization.CultureInfo.InvariantCulture);
                isValid = release >= version;
            }

            if (release <= version)
            {
                break;
            }
        }

        return isValid;
    }
}