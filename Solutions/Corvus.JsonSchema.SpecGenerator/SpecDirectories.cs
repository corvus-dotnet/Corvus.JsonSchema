// <copyright file="SpecDirectories.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.JsonSchema.SpecGenerator
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Directories for spec generation.
    /// </summary>
    internal struct SpecDirectories
    {
        private SpecDirectories(string testSet, string testsDirectory, string remotesDirectory, string outputDirectory)
        {
            this.TestSet = testSet;
            this.TestsDirectory = testsDirectory;
            this.RemotesDirectory = remotesDirectory;
            this.OutputDirectory = outputDirectory;
        }

        /// <summary>
        /// Gets the set of tests to which this belongs.
        /// </summary>
        public string TestSet { get; }

        /// <summary>
        /// Gets the directory contain the JSON schema specs.
        /// </summary>
        public string TestsDirectory { get; }

        /// <summary>
        /// Gets or sets  the direcotry containing the JSON schema remote files.
        /// </summary>
        public string RemotesDirectory { get; set; }

        /// <summary>
        /// Gets or sets  the output directory for the feature files.
        /// </summary>
        public string OutputDirectory { get; set; }

        /// <summary>
        /// Set up the directories from the program arguments.
        /// </summary>
        /// <param name="args">The program arguments.</param>
        /// <returns>The spec directories.</returns>
        public static SpecDirectories SetupDirectories(string[] args)
        {
            string inputDirectory = Environment.CurrentDirectory;
            if (args.Length > 0)
            {
                inputDirectory = Path.Combine(inputDirectory, args[0]);
            }

            string testSet = "draft2019-09";
            if (args.Length > 2)
            {
                testSet = args[2];
            }

            string testsDirectory = Path.Combine(inputDirectory, $"tests\\{testSet}");
            string remotesDirectory = Path.Combine(inputDirectory, "remotes");

            if (!Directory.Exists(testsDirectory))
            {
                throw new InvalidOperationException($"Unable to find the tests directory: '{testsDirectory}'");
            }

            if (!Directory.Exists(remotesDirectory))
            {
                throw new InvalidOperationException($"Unable to find the remotes directory: '{remotesDirectory}'");
            }

            string outputDirectory = Path.Combine(Environment.CurrentDirectory, testSet);
            if (args.Length > 1)
            {
                outputDirectory = Path.Combine(outputDirectory, args[1]);
            }

            try
            {
                Directory.CreateDirectory(outputDirectory);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unable to create the output directory: '{outputDirectory}'", ex);
            }

            return new SpecDirectories(testSet, testsDirectory, remotesDirectory, outputDirectory);
        }

        /// <summary>
        /// Gets the output feature file for the given input file.
        /// </summary>
        /// <param name="file">The input file.</param>
        /// <returns>The output feature file name.</returns>
        public string GetOutputFeatureFileFor(string file)
        {
            return Path.Combine(this.OutputDirectory, Path.ChangeExtension(Path.GetFileName(file), ".feature"));
        }

        /// <summary>
        /// Enumerates the input test files.
        /// </summary>
        /// <returns>An enumerable of the input test files and their corresponding output feature files.</returns>
        public IEnumerable<(string testSet, string inputFile, string outputFile)> EnumerateTests()
        {
            foreach (string inputFile in Directory.EnumerateFiles(this.TestsDirectory))
            {
                yield return (this.TestSet, inputFile, this.GetOutputFeatureFileFor(inputFile));
            }
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is SpecDirectories other &&
                   this.TestsDirectory == other.TestsDirectory &&
                   this.RemotesDirectory == other.RemotesDirectory &&
                   this.OutputDirectory == other.OutputDirectory;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(this.TestsDirectory, this.RemotesDirectory, this.OutputDirectory);
        }
    }
}
