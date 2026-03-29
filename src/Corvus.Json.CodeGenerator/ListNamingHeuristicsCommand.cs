// <copyright file="ListNamingHeuristicsCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Corvus.Json.CodeGeneration.CSharp;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Spectre.Console.Cli command to list available name heuristics.
/// </summary>
internal class ListNamingHeuristicsCommand : Command
{
    public override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[green]Available name heuristics:[/]");
        foreach ((string name, bool isOptional) in CSharpLanguageProvider.Default.GetNameHeuristicNames())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                AnsiConsole.MarkupLine("[red]Operation cancelled.[/]");
                return -1;
            }

            AnsiConsole.MarkupLineInterpolated($"[yellow]{name}[/]{(isOptional ? " (optional)" : string.Empty)}");
        }

        return 1;
    }
}