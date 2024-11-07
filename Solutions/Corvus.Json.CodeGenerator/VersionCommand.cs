using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Corvus.Json.CodeGenerator;

/// <summary>
/// Spectre.Console.Cli command to display the product version.
/// </summary>
internal class VersionCommand : Command
{
    public override int Execute(CommandContext context)
    {
        AnsiConsole.MarkupLineInterpolated($"[green]Version:[/] {(Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString()) ?? "Not available."}");

        return 1;
    }
}
