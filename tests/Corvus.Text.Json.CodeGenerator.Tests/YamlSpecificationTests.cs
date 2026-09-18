// <copyright file="YamlSpecificationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Every OpenAPI and AsyncAPI command reads a YAML specification, when <c>--yaml</c> says so or when the file's
/// extension is <c>.yaml</c> or <c>.yml</c> (issue #967).
/// </summary>
[TestClass]
public class YamlSpecificationTests
{
    private const string OpenApiYaml =
        """
        openapi: 3.1.0
        info:
          title: Events
          version: 1.0.0
        paths:
          /subscriptions:
            post:
              operationId: subscribe
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Subscription'
              responses:
                '201':
                  description: Created
              callbacks:
                onEvent:
                  '{$request.body#/callbackUrl}':
                    post:
                      operationId: onEvent
                      requestBody:
                        content:
                          application/json:
                            schema:
                              $ref: '#/components/schemas/Event'
                      responses:
                        '200':
                          description: OK
        components:
          schemas:
            Subscription:
              type: object
              required:
                - callbackUrl
              properties:
                callbackUrl:
                  type: string
                  format: uri
            Event:
              type: object
              properties:
                id:
                  type: string
        """;

    private const string AsyncApiYaml =
        """
        asyncapi: 3.0.0
        info:
          title: Lights
          version: 1.0.0
        channels:
          lights:
            address: lights
            messages:
              turnOn:
                payload:
                  type: object
                  properties:
                    level:
                      type: integer
        operations:
          sendTurnOn:
            action: send
            channel:
              $ref: '#/channels/lights'
        """;

    [TestMethod]
    [DataRow("openapi-server")]
    [DataRow("openapi-callback-server")]
    [DataRow("openapi-callback-client")]
    public async Task OpenApiCommand_YamlSpecification_GeneratesTheSameCodeAsJson(string command)
    {
        string directory = Directory.CreateTempSubdirectory("yaml-spec-").FullName;
        try
        {
            // --yaml on a file named .json, against the same specification as JSON under the same name.
            string jsonNamed = Path.Combine(directory, "api.json");
            await File.WriteAllTextAsync(jsonNamed, OpenApiYaml);
            byte[] json = ConvertYamlToJson(OpenApiYaml);
            await RunAsync(command, jsonNamed, Path.Combine(directory, "explicit-yaml"), "--yaml");
            await File.WriteAllBytesAsync(jsonNamed, json);
            await RunAsync(command, jsonNamed, Path.Combine(directory, "json"));
            AssertSameFiles(Path.Combine(directory, "json"), Path.Combine(directory, "explicit-yaml"));

            // The .yaml extension without --yaml, against --yaml on the same file.
            string yamlNamed = Path.Combine(directory, "api.yaml");
            await File.WriteAllTextAsync(yamlNamed, OpenApiYaml);
            await RunAsync(command, yamlNamed, Path.Combine(directory, "detected-yaml"));
            await RunAsync(command, yamlNamed, Path.Combine(directory, "explicit-yaml-named-yaml"), "--yaml");
            AssertSameFiles(Path.Combine(directory, "explicit-yaml-named-yaml"), Path.Combine(directory, "detected-yaml"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    [DataRow("openapi-show", "api.yaml")]
    [DataRow("asyncapi-show", "asyncapi.yml")]
    public async Task ShowCommand_YamlSpecification_IsRead(string command, string fileName)
    {
        string directory = Directory.CreateTempSubdirectory("yaml-spec-").FullName;
        try
        {
            string specFile = Path.Combine(directory, fileName);
            await File.WriteAllTextAsync(specFile, command.StartsWith("openapi", StringComparison.Ordinal) ? OpenApiYaml : AsyncApiYaml);
            int exitCode = await CliAppFactory.Create("corvusjson").RunAsync([command, specFile]);
            Assert.AreEqual(0, exitCode, $"corvusjson {command} {specFile}");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static byte[] ConvertYamlToJson(string yaml)
    {
        Corvus.Json.CodeGeneration.DocumentResolvers.YamlPreProcessor preProcessor = new();
        using MemoryStream input = new(System.Text.Encoding.UTF8.GetBytes(yaml));
        using Stream processed = preProcessor.Process(input);
        using MemoryStream output = new();
        processed.CopyTo(output);
        return output.ToArray();
    }

    private static async Task RunAsync(string command, string specFile, string outputPath, params string[] options)
    {
        string[] arguments = [command, specFile, "--rootNamespace", "YamlSpecification.Tests", "--outputPath", outputPath, .. options];
        int exitCode = await CliAppFactory.Create("corvusjson").RunAsync(arguments);
        Assert.AreEqual(0, exitCode, $"corvusjson {string.Join(' ', arguments)}");
    }

    private static void AssertSameFiles(string expectedDirectory, string actualDirectory)
    {
        static Dictionary<string, byte[]> Load(string directory) =>
            Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".lock", StringComparison.Ordinal))
                .ToDictionary(f => Path.GetRelativePath(directory, f), File.ReadAllBytes, StringComparer.Ordinal);

        Dictionary<string, byte[]> expected = Load(expectedDirectory);
        Dictionary<string, byte[]> actual = Load(actualDirectory);
        CollectionAssert.AreEquivalent(expected.Keys.ToArray(), actual.Keys.ToArray(), "generated file names");
        Assert.IsTrue(expected.Count > 0, "Expected generated files.");
        string[] different = expected.Keys.Where(k => !expected[k].AsSpan().SequenceEqual(actual[k])).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Assert.AreEqual(0, different.Length, $"Files that differ: {string.Join(", ", different)}");
    }
}