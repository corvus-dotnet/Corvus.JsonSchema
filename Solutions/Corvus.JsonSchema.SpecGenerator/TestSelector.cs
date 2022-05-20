// <copyright file="TestSelector.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.JsonSchema.SpecGenerator
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Deserialized contents of JSONC selector file that determines which tests from which
    /// subdirectories will be included for generation, and how those break down into test sets.
    /// </summary>
    public class TestSelector
    {
        /// <summary>
        /// Gets or sets the patterns that determine which files to consider for inclusion in the
        /// directory this instance represents. If any of the patterns match, the file is
        /// considered. This is processed before <see cref="ExcludeFromThisDirectory"/>.
        /// </summary>
        public IReadOnlyList<string> IncludeInThisDirectory { get; set; } = new[] { @".*\.json" };

        /// <summary>
        /// Gets or sets the patterns that determine which of the files matched by the
        /// <see cref="IncludeInThisDirectory"/> should not be processed.
        /// </summary>
        public IReadOnlyList<string> ExcludeFromThisDirectory { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets a dictionary in which each key is the relative path to a JSON Schema Test
        /// Suite test file, and the value is a list of numbers indicating the indices of tests
        /// within that test file that should be excluded.
        /// </summary>
        /// <remarks>
        /// We use this to when we only want to exclude some of the tests in a file.
        /// </remarks>
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, TestExclusion>> TestExclusions { get; set; } = new Dictionary<string, IReadOnlyDictionary<string, TestExclusion>>();

        /// <summary>
        /// Gets or sets a dictionary in which each key is a pattern identifying one or more
        /// subdirectories that should be processed, and the value is a <see cref="TestSelector"/>
        /// determining the settings to use when processing the directories matching the key.
        /// </summary>
        public IReadOnlyDictionary<string, TestSelector> Subdirectories { get; set; } = new Dictionary<string, TestSelector>();

        /// <summary>
        /// Gets or sets the name of the test set. If null, any files included for this instance
        /// will be considered part of the same test set as the parent instance.
        /// </summary>
        /// <remarks>
        /// The root <see cref="TestSelector"/> typically won't set this if there are multiple test
        /// sets. It is an error for any <see cref="TestSelector"/> to match any test files if
        /// neither it nor any of its ancestors has a non-null <see cref="TestSet"/>.
        /// </remarks>
        public string? TestSet { get; set; }

        /// <summary>
        /// Gets or sets the name of the output folder to use when generating files for this
        /// instance. If null, any files generated for this instance will be put in the same
        /// directory as for the parent instance.
        /// </summary>
        /// <remarks>
        /// The root <see cref="TestSelector"/> typically won't set this if there are multiple test
        /// sets. It is an error for any <see cref="TestSelector"/> to match any test files if
        /// neither it nor any of its ancestors has a non-null <see cref="OutputFolder"/>.
        /// </remarks>
        public string? OutputFolder { get; set; }

        /// <summary>
        /// Details for a test exclusion.
        /// </summary>
        /// <param name="TestsToIgnoreIndices">
        /// Indices of tests to ignore in a JSON Schema Test Suite input file, grouped by scenario name.
        /// </param>
#pragma warning disable SA1313 // Parameter names should begin with lower-case letter - StyleCop doesn't understand
        public record TestExclusion(IReadOnlyList<int> TestsToIgnoreIndices);
#pragma warning restore SA1313 // Parameter names should begin with lower-case letter
    }
}