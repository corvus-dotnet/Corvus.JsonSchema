using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Corvus.Json.CodeGeneration.CSharp;
using Microsoft.CSharp;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Corvus.Json.CodeGenerator;

/// <summary>
/// Spectre.Console.Cli command to list available name heuristics.
/// </summary>
internal class ListNamingHeuristicsCommand : Command
{
    public override int Execute(CommandContext context)
    {
        AnsiConsole.MarkupLine("[green]Available name heuristics:[/]");
        foreach((string name, bool isOptional) in CSharpLanguageProvider.Default.GetNameHeuristicNames())
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{name}[/]{(isOptional ? " (optional)" : string.Empty)}");
        }

        return 1;
    }
}
