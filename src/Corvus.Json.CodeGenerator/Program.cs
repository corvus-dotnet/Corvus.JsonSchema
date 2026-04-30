// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.CodeGenerator;

// Emit deprecation warning to stderr so it doesn't interfere with piped output.
if (!Console.IsErrorRedirected)
{
    Console.Error.WriteLine("WARNING: 'generatejsonschematypes' has been renamed to 'corvusjson'.");
    Console.Error.WriteLine("Install the new tool with: dotnet tool install Corvus.Json.Cli");
    Console.Error.WriteLine();
}

CliDefaults.DefaultEngine = Engine.V4;

// Rewrite bare-args invocations to use the 'jsonschema' subcommand.
// If the first argument is not a known subcommand or an option, prepend 'jsonschema'.
string[] knownCommands = ["jsonschema", "config", "listNameHeuristics", "validateDocument", "version", "jsonlogic", "jsonata", "jmespath"];
if (args.Length > 0
    && !args[0].StartsWith('-')
    && !knownCommands.Contains(args[0], StringComparer.OrdinalIgnoreCase))
{
    args = ["jsonschema", .. args];
}

return await CliAppFactory.Create("generatejsonschematypes").RunAsync(args);