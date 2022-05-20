// <copyright file="SpecWriter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.JsonSchema.SpecGenerator
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// Write .feature files for specs from the json schema specs.
    /// </summary>
    /// <remarks>
    /// https://github.com/json-schema-org/JSON-Schema-Test-Suite/tree/master/tests/draft2020-12.
    /// </remarks>
    internal class SpecWriter
    {
        /// <summary>
        /// Write the feature files for the json specs.
        /// </summary>
        /// <param name="specDirectories">The spec directories.</param>
        internal static void Write(SpecDirectories specDirectories)
        {
            foreach (SpecDirectories.TestSet testSet in specDirectories.EnumerateTests())
            {
                Console.WriteLine($"Reading: {testSet.inputFile}");
                Console.WriteLine($"Writing: {testSet.outputFile}");
                Console.WriteLine();
                WriteFeatureFile(testSet);
            }
        }

        private static void WriteFeatureFile(SpecDirectories.TestSet testSet)
        {
            using var testDocument = JsonDocument.Parse(File.ReadAllText(testSet.inputFile));

            var builder = new StringBuilder();

            WriteFeatureHeading(testSet.testSet, Path.GetFileNameWithoutExtension(testSet.inputFile), builder);

            int index = 0;
            foreach (JsonElement scenarioDefinition in testDocument.RootElement.EnumerateArray())
            {
                WriteScenario(testSet, index, scenarioDefinition, builder);
                ++index;
            }

            File.WriteAllText(testSet.outputFile, builder.ToString());
        }

        private static void WriteScenario(SpecDirectories.TestSet testSet, int scenarioIndex, JsonElement scenarioDefinition, StringBuilder builder)
        {
            string inputSchemaReference = $"#/{scenarioIndex}/schema";

            string? scenarioTitle = scenarioDefinition.GetProperty("description").GetString();
            scenarioTitle = NormalizeTitleForDeduplication(scenarioTitle!);

            builder.AppendLine();
            builder.AppendLine($"Scenario Outline: {scenarioTitle}");
            builder.AppendLine("/* Schema: ");
            builder.AppendLine(scenarioDefinition.GetProperty("schema").ToString());
            builder.AppendLine("*/");
            builder.AppendLine($"    Given the input JSON file \"{testSet.inputFileSpecFolderRelativePath}\"");
            builder.AppendLine($"    And the schema at \"{inputSchemaReference}\"");
            builder.AppendLine("    And the input data at \"<inputDataReference>\"");
            builder.AppendLine("    And I generate a type for the schema");
            builder.AppendLine("    And I construct an instance of the schema type from the data");
            builder.AppendLine("    When I validate the instance");
            builder.AppendLine("    Then the result will be <valid>");
            builder.AppendLine();
            builder.AppendLine("    Examples:");
            builder.AppendLine("        | inputDataReference   | valid | description                                                                      |");

            if (!testSet.testsToIgnoreIndicesByScenarioName.TryGetValue(scenarioTitle, out IReadOnlySet<int>? testsToIgnoreIndices))
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
            scenarioTitle = scenarioTitle
                .Replace("[", "array[")
                .Replace("<=", " less than or equal ")
                .Replace("<", " less than ")
                .Replace(">=", " greater than or equal ")
                .Replace(">", " greater than ")
                .Replace("==", " equals ")
                .Replace("=", " equals ");
            return scenarioTitle;
        }

        private static void WriteFeatureHeading(string testSet, string featureName, StringBuilder builder)
        {
            builder.AppendLine($"@{testSet}");
            builder.AppendLine();
            builder.AppendLine($"Feature: {featureName} {testSet}");
            builder.AppendLine("    In order to use json-schema");
            builder.AppendLine("    As a developer");
            builder.AppendLine($"    I want to support {featureName} in {testSet}");
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

            string? description = test.GetProperty("description").GetString();

            if (description is null)
            {
                throw new Exception("Expected a 'description' property with a string value.");
            }

            description = description.PadRight(80);

            builder.AppendLine($"        {(omit ? '#' : '|')} {inputDataReference} | {valid} | {description} |");
        }
    }
}