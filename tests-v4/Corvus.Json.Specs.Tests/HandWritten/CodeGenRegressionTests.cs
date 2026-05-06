// <copyright file="CodeGenRegressionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;
using Corvus.Json.Specs.Tests.Infrastructure;
using Drivers;
using Xunit;

namespace Corvus.Json.Specs.Tests.HandWritten;

/// <summary>
/// Tests for code generation regression scenarios that only need to verify
/// code generation succeeds without throwing.
/// </summary>
public class CodeGenRegressionTests
{
    [Fact]
    public async Task UnableToFindProperty_CodeGenDoesNotThrow()
    {
        // Repro: A large schema with nested $refs and additionalProperties
        // that previously caused "unable to find property" exceptions.
        using var driver = DriverFactory.CreateDraft4Driver();
        IReadOnlyCollection<GeneratedCodeFile> code = await driver.GenerateCodeForVirtualFile(
            """
            {
              "$comment": "https://docs.codeclimate.com/docs/advanced-configuration",
              "$schema": "http://json-schema.org/draft-04/schema#",
              "definitions": {
                "enabled": {
                  "type": "object",
                  "properties": {
                    "enabled": { "title": "Enabled", "type": "boolean", "default": true }
                  }
                },
                "config": { "title": "Config", "type": "object" },
                "threshold": { "title": "Threshold", "type": ["integer", "null"] }
              },
              "description": "Configuration file as an alternative for configuring your repository in the settings page.",
              "id": "https://json.schemastore.org/codeclimate.json",
              "properties": {
                "version": { "title": "Version", "description": "Required to adjust maintainability checks.", "type": "string", "default": "2" },
                "prepare": {
                  "title": "Prepare", "type": "array",
                  "items": { "type": "object", "properties": { "url": { "title": "URL", "type": "string", "format": "uri" }, "path": { "title": "Path", "type": "string" } } }
                },
                "checks": {
                  "title": "Checks", "type": "object",
                  "properties": {
                    "argument-count": { "title": "Argument Count", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold", "default": 4 } } } } },
                    "complex-logic": { "title": "Complex Logic", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold", "default": 4 } } } } },
                    "file-lines": { "title": "File Lines", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold", "default": 250 } } } } },
                    "method-complexity": { "title": "Method Complexity", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold", "default": 5 } } } } },
                    "method-count": { "title": "Method Count", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold", "default": 20 } } } } },
                    "method-lines": { "title": "Method Lines", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold", "default": 25 } } } } },
                    "nested-control-flow": { "title": "Nested Control Flow", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold", "default": 4 } } } } },
                    "return-statements": { "title": "Return Statements", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold", "default": 4 } } } } },
                    "similar-code": { "title": "Similar Code", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold" } } } } },
                    "identical-code": { "title": "Identical Code", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold" } } } } }
                  }
                },
                "plugins": { "title": "Plugins", "description": "To add a plugin to your analysis.", "type": "object", "additionalProperties": { "$ref": "#/definitions/enabled" } },
                "exclude_patterns": { "title": "Exclude Patterns", "type": "array", "items": { "title": "Exclude Pattern", "type": "string" } }
              },
              "title": "Code Climate Configuration",
              "type": "object"
            }
            """,
            "unableToFindProperty.json",
            "UnableToFindProperty",
            "CodeGenDoesNotThrow",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: false);

        // We just need to verify that code generation succeeds
        Assert.NotEmpty(code);
    }

    [Fact]
    public async Task DuplicateDocumentation_CodeGenSucceeds()
    {
        // Repro #440: A schema that produces duplicate documentation comments.
        // This test verifies code generation succeeds and produces the expected
        // remarks for the 'Files' property.
        using var driver = DriverFactory.CreateDraft4Driver();
        string schemaPath = Path.Combine(
            DriverFactory.RepoRoot,
            "tests-v4",
            "Corvus.Json.Specs.Tests",
            "HandWritten",
            "duplicateDocumentation.schema.json");
        string schema = File.ReadAllText(schemaPath);

        IReadOnlyCollection<GeneratedCodeFile> code = await driver.GenerateCodeForVirtualFile(
            schema,
            "duplicateDocumentation.json",
            "DuplicateDocumentation",
            "ASchemaThatProducesDuplicateDocumentation",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: false);

        Assert.NotEmpty(code);

        // Find the file containing the FilesDefinition property
        GeneratedCodeFile? targetFile = code.FirstOrDefault(
            f => f.FileName.Contains("FilesDefinition.Object"));

        Assert.NotNull(targetFile);

        // Verify it contains the expected remarks about 'files' and 'include'
        Assert.Contains(
            "If no &#39;files&#39; or &#39;include&#39; property is present",
            targetFile!.FileContent);
    }
}