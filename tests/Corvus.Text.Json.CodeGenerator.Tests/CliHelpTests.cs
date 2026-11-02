// <copyright file="CliHelpTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// The help of every command renders. Spectre.Console reads square brackets in a description as markup, so a
/// description with text such as <c>[Flags]</c> made <c>corvusjson jsonschema --help</c> fail with
/// "Could not find color or style 'Flags'".
/// </summary>
[TestClass]
public class CliHelpTests
{
    [TestMethod]
    public async Task TheHelpOfEveryCommandRenders()
    {
        ProcessResult root = await CodeGeneratorRunner.RunAsync("--help");
        Assert.AreEqual(0, root.ExitCode, root.StandardOutput + root.StandardError);

        // Spectre.Console colours its output where it detects a terminal or a CI host (GitHub Actions among them), so
        // the escape sequences are removed before the lines are read.
        string[] commands =
        [
            .. Regex.Replace(root.StandardOutput, "\u001b\\[[0-9;]*m", string.Empty)
                .Split('\n')
                .SkipWhile(l => !l.TrimStart().StartsWith("COMMANDS:", StringComparison.Ordinal))
                .Skip(1)
                .Where(l => l.Length > 4 && l.StartsWith("    ", StringComparison.Ordinal) && char.IsLetter(l[4])) // a command's own line, not a wrapped description
                .Select(l => l.Trim().Split(' ', 2)[0])
                .Distinct(StringComparer.Ordinal)
        ];

        Assert.IsTrue(commands.Contains("jsonschema"), string.Join(", ", commands));

        foreach (string command in commands)
        {
            ProcessResult help = await CodeGeneratorRunner.RunAsync($"{command} --help");
            Assert.AreEqual(0, help.ExitCode, $"{command} --help: {help.StandardOutput}{help.StandardError}");
            Assert.IsFalse((help.StandardOutput + help.StandardError).Contains("Error:", StringComparison.Ordinal), $"{command} --help: {help.StandardOutput}{help.StandardError}");
        }
    }
}