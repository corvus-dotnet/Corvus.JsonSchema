// <copyright file="SpecWriter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;

namespace Corvus.JsonSchema.SpecGenerator;

/// <summary>
/// Write .feature files for specs from the json schema specs.
/// </summary>
/// <remarks>
/// https://github.com/json-schema-org/JSON-Schema-Test-Suite/tree/master/tests/draft2020-12.
/// </remarks>
internal static class SpecWriter
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

        WriteFeatureHeading(testSet.TestSetName, Path.GetFileNameWithoutExtension(testSet.InputFile), builder);
        HashSet<string> writtenScenarios = [];
        int index = 0;
        foreach (JsonElement scenarioDefinition in testDocument.RootElement.EnumerateArray())
        {
            WriteScenario(testSet, index, scenarioDefinition, builder, writtenScenarios);
            ++index;
        }

        File.WriteAllText(testSet.OutputFile, builder.ToString());
    }

    private static void WriteScenario(SpecDirectories.TestSet testSet, int scenarioIndex, JsonElement scenarioDefinition, StringBuilder builder, HashSet<string> writtenScenarios)
    {
        string inputSchemaReference = $"#/{scenarioIndex}/schema";

        string scenarioTitleBase = scenarioDefinition.GetProperty("description").GetString()!;
        scenarioTitleBase = NormalizeTitleForDeduplication(scenarioTitleBase!);
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
        builder.AppendLine("    And I generate a type for the schema");
        builder.AppendLine("    And I construct an instance of the schema type from the data");
        builder.AppendLine("    When I validate the instance");
        builder.AppendLine("    Then the result will be <valid>");
        builder.AppendLine();
        builder.AppendLine("    Examples:");
        builder.AppendLine("        | inputDataReference   | valid | description                                                                      |");

        if (!testSet.TestsToIgnoreIndicesByScenarioName.TryGetValue(scenarioTitle, out IReadOnlySet<int>? testsToIgnoreIndices))
        {
            testsToIgnoreIndices = new HashSet<int>();
        }

        int testIndex = 0;
        foreach (JsonElement test in scenarioDefinition.GetProperty("tests").EnumerateArray())
        {
            WriteExample(scenarioIndex, testIndex, test, builder, testsToIgnoreIndices.Contains(testIndex));
            ++testIndex;
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
        description = description.PadRight(80);

        builder.Append("        ").Append(omit ? '#' : '|').Append(' ').Append(inputDataReference).Append(" | ").Append(valid).Append(" | ").Append(description).AppendLine(" |");
    }
}