// <copyright file="SpecDirectories.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Corvus.JsonSchema.SpecGenerator;

/// <summary>
/// Directories for spec generation.
/// </summary>
internal struct SpecDirectories
{
    private SpecDirectories(
        string testsDirectory,
        string remotesDirectory,
        string outputDirectory,
        TestSelector selector)
    {
        this.TestsDirectory = testsDirectory;
        this.RemotesDirectory = remotesDirectory;
        this.OutputDirectory = outputDirectory;
        this.Selector = selector;
    }

    /// <summary>
    /// Gets the directory contain the JSON schema specs.
    /// </summary>
    public string TestsDirectory { get; }

    /// <summary>
    /// Gets or sets the direcotry containing the JSON schema remote files.
    /// </summary>
    public string RemotesDirectory { get; set; }

    /// <summary>
    /// Gets or sets the output directory for the feature files.
    /// </summary>
    public string OutputDirectory { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="TestSelector"/> determining which files to generate tests
    /// from.
    /// </summary>
    public TestSelector Selector { get; set; }

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

        TestSelector selector;
        if (args.Length > 2)
        {
            using FileStream selectorFile = File.OpenRead(args[2]);
            selector = JsonSerializer.Deserialize<TestSelector>(
                selectorFile,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { ReadCommentHandling = JsonCommentHandling.Skip })!;
        }
        else
        {
            selector = new();
        }

        string testsDirectory = Path.Combine(inputDirectory, "tests");
        string remotesDirectory = Path.Combine(inputDirectory, "remotes");

        if (!Directory.Exists(testsDirectory))
        {
            throw new InvalidOperationException($"Unable to find the tests directory: '{testsDirectory}'");
        }

        if (!Directory.Exists(remotesDirectory))
        {
            throw new InvalidOperationException($"Unable to find the remotes directory: '{remotesDirectory}'");
        }

        string outputDirectory = Path.Combine(Environment.CurrentDirectory, testsDirectory);
        if (args.Length > 1)
        {
            outputDirectory = Path.Combine(Environment.CurrentDirectory, args[1]);
        }

        try
        {
            Directory.CreateDirectory(outputDirectory);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Unable to create the output directory: '{outputDirectory}'", ex);
        }

        return new SpecDirectories(
            testsDirectory, remotesDirectory, outputDirectory, selector);
    }

    /// <summary>
    /// Enumerates the input test files.
    /// </summary>
    /// <returns>An enumerable of the input test files and their corresponding output feature files.</returns>
    public IEnumerable<TestSet> EnumerateTests()
    {
        IEnumerable<TestSet>
            EnumerateCore(string parentTestSetInputDirectory, string inputDirectory, string outputDirectory, string? parentTestSet, TestSelector selector)
        {
            string? currentTestSet = selector.TestSet ?? parentTestSet;
            string testSetInputDirectory = selector.TestSet is null
                ? parentTestSetInputDirectory
                : inputDirectory;

            var includePatterns = selector.IncludeInThisDirectory.Select(pattern => new Regex(pattern)).ToList();
            var excludePatterns = selector.ExcludeFromThisDirectory.Select(pattern => new Regex(pattern)).ToList();
            foreach (string inputFile in Directory.EnumerateFiles(inputDirectory))
            {
                string filename = Path.GetFileName(inputFile);
                if (includePatterns.Any(pattern => pattern.IsMatch(filename)) &&
                    !excludePatterns.Any(pattern => pattern.IsMatch(filename)))
                {
                    if (currentTestSet is null)
                    {
                        throw new InvalidOperationException("Test selectors should either specify a testSet, be a descendant of a selector that specifies a testSet, or match no files");
                    }

                    string inputRelativePath = Path.GetRelativePath(testSetInputDirectory, inputFile).Replace('\\', '/');
                    IReadOnlyDictionary<string, TestSelector.TestExclusion> testToIgnoreIndicesByScenarioName =
                        selector.TestExclusions.TryGetValue(inputRelativePath, out IReadOnlyDictionary<string, TestSelector.TestExclusion>? exclusions)
                            ? exclusions
                            : new Dictionary<string, TestSelector.TestExclusion>();

                    IReadOnlyDictionary<string, IReadOnlySet<int>> testToIgnoreIndicesAsSetByScenarioName = testToIgnoreIndicesByScenarioName
                        .ToDictionary(
                            kv => kv.Key,
                            kv => (IReadOnlySet<int>)new HashSet<int>(kv.Value.TestsToIgnoreIndices));
                    yield return new(
                        currentTestSet,
                        inputFile,
                        inputRelativePath,
                        Path.Combine(outputDirectory, Path.ChangeExtension(Path.GetFileName(inputFile), ".feature")),
                        testToIgnoreIndicesAsSetByScenarioName);
                }
            }

            (string Path, string Name)[] inputSubdirectories = Directory
                .EnumerateDirectories(inputDirectory)
                .Select(path => (path, Path.GetFileName(path)))
                .ToArray();
            foreach ((string directoryPattern, TestSelector subdirectorySelector) in selector.Subdirectories)
            {
                string outputSubdirectory = subdirectorySelector.OutputFolder is null
                    ? outputDirectory
                    : Path.Combine(outputDirectory, subdirectorySelector.OutputFolder);
                Regex directoryRegex = new(directoryPattern);
                bool foundAtLeastOneMatch = false;
                foreach ((string subdirectoryPath, string subdirectoryName) in inputSubdirectories)
                {
                    // Should we detect and warn when more than one pattern matches the same directory?
                    if (directoryRegex.IsMatch(subdirectoryName))
                    {
                        foundAtLeastOneMatch = true;

                        IEnumerable<TestSet> subdirectoryResults =
                            EnumerateCore(testSetInputDirectory, subdirectoryPath, outputSubdirectory, currentTestSet, subdirectorySelector);
                        foreach (TestSet result in subdirectoryResults)
                        {
                            yield return result;
                        }
                    }
                }

                if (!foundAtLeastOneMatch)
                {
                    Console.WriteLine($"Warning: found no directories in {directoryPattern} matching {directoryPattern}");
                }
            }
        }

        return EnumerateCore(this.TestsDirectory, this.TestsDirectory, this.OutputDirectory, null, this.Selector);
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

    public record TestSet(
        string TestSetName,
        string InputFile,
        string InputFileSpecFolderRelativePath,
        string OutputFile,
        IReadOnlyDictionary<string, IReadOnlySet<int>> TestsToIgnoreIndicesByScenarioName);
}