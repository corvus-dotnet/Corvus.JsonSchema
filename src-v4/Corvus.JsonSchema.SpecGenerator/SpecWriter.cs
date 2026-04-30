// <copyright file="SpecWriter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Corvus.JsonSchema.SpecGenerator;

/// <summary>
/// Write .feature files for specs from the json schema specs.
/// </summary>
/// <remarks>
/// https://github.com/json-schema-org/JSON-Schema-Test-Suite/tree/master/tests/draft2020-12.
/// </remarks>
internal static partial class SpecWriter
{
    /// <summary>
    /// Write the feature files for the json specs.
    /// </summary>
    /// <param name="specDirectories">The spec directories.</param>
    internal static void Write(SpecDirectories specDirectories)
    {
        foreach (SpecDirectories.TestSet testSet in specDirectories.EnumerateTests())
        {
            Console.WriteLine($"Reading: {testSet.InputFile}");
            Console.WriteLine($"Writing: {testSet.OutputFile}");
            Console.WriteLine();
            WriteFeatureFile(testSet);
        }
    }

    private static void WriteFeatureFile(SpecDirectories.TestSet testSet)
    {
        using var testDocument = JsonDocument.Parse(File.ReadAllText(testSet.InputFile));

        var builder = new StringBuilder();

        WriteFeatureHeading(testSet.TestSetName, Path.GetFileNameWithoutExtension(testSet.OutputFile), builder);
        HashSet<string> writtenScenarios = [];
        int index = 0;
        foreach (JsonElement scenarioDefinition in testDocument.RootElement.EnumerateArray())
        {
            WriteScenario(testSet, index, scenarioDefinition, builder, writtenScenarios);
            ++index;
        }

        string? outputDirectory = Path.GetDirectoryName(testSet.OutputFile);
        if (outputDirectory is string od)
        {
            Directory.CreateDirectory(od);
        }

        File.WriteAllText(testSet.OutputFile, builder.ToString());
    }

    private static void WriteScenario(SpecDirectories.TestSet testSet, int scenarioIndex, JsonElement scenarioDefinition, StringBuilder builder, HashSet<string> writtenScenarios)
    {
        string inputSchemaReference = $"#/{scenarioIndex}/schema";

        string scenarioDescription = scenarioDefinition.GetProperty("description").GetString()!;

        // Use the raw scenario description for exclusion lookup (not the normalized/deduped title)
        if (!testSet.TestsToIgnoreByScenarioName.TryGetValue(scenarioDescription, out IReadOnlySet<string>? testsToIgnore))
        {
            testsToIgnore = new HashSet<string>();
        }

        // Validate that test descriptions are unique within this scenario
        HashSet<string> seenDescriptions = [];
        foreach (JsonElement test in scenarioDefinition.GetProperty("tests").EnumerateArray())
        {
            string desc = test.GetProperty("description").GetString()!;
            if (!seenDescriptions.Add(desc))
            {
                throw new InvalidOperationException(
                    $"Duplicate test description '{desc}' in scenario '{scenarioDescription}' of file '{testSet.InputFileSpecFolderRelativePath}'. " +
                    "Name-based exclusions require unique test descriptions within a scenario.");
            }
        }

        // Check if ALL tests would be excluded — if so, skip the entire scenario
        // (a Scenario Outline with no examples causes a SpecFlow build error)
        bool allExcluded = true;
        foreach (JsonElement test in scenarioDefinition.GetProperty("tests").EnumerateArray())
        {
            string testDescription = test.GetProperty("description").GetString()!;
            if (!testsToIgnore.Contains(testDescription))
            {
                allExcluded = false;
                break;
            }
        }

        if (allExcluded && testsToIgnore.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(
                $"  Skipping scenario '{scenarioDescription}' in '{testSet.InputFileSpecFolderRelativePath}' — all tests excluded.");
            Console.ResetColor();
            return;
        }

        string scenarioTitleBase = NormalizeTitleForDeduplication(scenarioDescription);
        string scenarioTitle = scenarioTitleBase;
        int dupIndex = 1;
        while (writtenScenarios.Contains(scenarioTitle))
        {
            scenarioTitle = $"{scenarioTitleBase} [{dupIndex}]";
            ++dupIndex;
        }

        writtenScenarios.Add(scenarioTitle);

        builder.AppendLine();
        builder.Append("Scenario Outline: ").AppendLine(scenarioTitle);
        builder.AppendLine("/* Schema: ");
        builder.AppendLine(scenarioDefinition.GetProperty("schema").ToString());
        builder.AppendLine("*/");
        builder.Append("    Given the input JSON file \"").Append(testSet.InputFileSpecFolderRelativePath).AppendLine("\"");
        builder.Append("    And the schema at \"").Append(inputSchemaReference).AppendLine("\"");
        builder.AppendLine("    And the input data at \"<inputDataReference>\"");

        if (testSet.AssertFormat)
        {
            builder.AppendLine("    And I assert format");
        }

        builder.AppendLine("    And I generate a type for the schema");
        builder.AppendLine("    And I construct an instance of the schema type from the data");
        builder.AppendLine("    When I validate the instance");
        builder.AppendLine("    Then the result will be <valid>");
        builder.AppendLine();
        builder.AppendLine("    Examples:");
        builder.AppendLine("        | inputDataReference   | valid | description                                                                      |");

        // Track which exclusion names were actually matched
        HashSet<string> matchedExclusions = [];

        int testIndex = 0;
        foreach (JsonElement test in scenarioDefinition.GetProperty("tests").EnumerateArray())
        {
            string testDescription = test.GetProperty("description").GetString()!;
            bool omit = testsToIgnore.Contains(testDescription);
            if (omit)
            {
                matchedExclusions.Add(testDescription);
            }

            WriteExample(scenarioIndex, testIndex, test, builder, omit);
            ++testIndex;
        }

        // Warn about stale exclusions that didn't match any test
        foreach (string exclusionName in testsToIgnore)
        {
            if (!matchedExclusions.Contains(exclusionName))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(
                    $"WARNING: Exclusion '{exclusionName}' in scenario '{scenarioDescription}' " +
                    $"of file '{testSet.InputFileSpecFolderRelativePath}' did not match any test.");
                Console.ResetColor();
            }
        }
    }

    private static string NormalizeTitleForDeduplication(string scenarioTitle)
    {
        return scenarioTitle
            .Replace("[", "array[")
            .Replace("<=", " less than or equal ")
            .Replace("<", " less than ")
            .Replace(">=", " greater than or equal ")
            .Replace(">", " greater than ")
            .Replace("==", " equals ")
            .Replace("=", " equals ");
    }

    private static void WriteFeatureHeading(string testSet, string featureName, StringBuilder builder)
    {
        builder.Append('@').AppendLine(testSet);
        builder.AppendLine();
        builder.Append("Feature: ").Append(featureName).Append(' ').AppendLine(testSet);
        builder.AppendLine("    In order to use json-schema");
        builder.AppendLine("    As a developer");
        builder.Append("    I want to support ").Append(featureName).Append(" in ").AppendLine(testSet);
    }

    private static void WriteExample(int scenarioIndex, int testIndex, JsonElement test, StringBuilder builder, bool omit)
    {
        // Write the example data as a comment
        string data = test.GetProperty("data").ToString();

        // Replace all consecutive whitespace with a single space
        data = WhitespaceReplacer().Replace(data, " ");
        builder.Append("        # ");
        builder.AppendLine(data);

        // Write the example
        string inputDataReference = $"#/{scenarioIndex:D3}/tests/{testIndex:D3}/data";

        string valid;
        if (test.GetProperty("valid").ValueKind == JsonValueKind.False)
        {
            valid = "false";
        }
        else
        {
            valid = "true ";
        }

        string description = test.GetProperty("description").GetString() ?? throw new Exception("Expected a 'description' property with a string value.");
        description = description.Replace("|", "\\|").PadRight(80);
        builder.Append("        ").Append(omit ? '#' : '|').Append(' ').Append(inputDataReference).Append(" | ").Append(valid).Append(" | ").Append(description).AppendLine(" |");
    }

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceReplacer();
}