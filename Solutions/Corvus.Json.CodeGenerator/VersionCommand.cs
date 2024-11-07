using System.Diagnostics;
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
        string? assemblyLocation = Assembly.GetEntryAssembly()?.Location;
        string? version = null;
        string? build = null;

        if (assemblyLocation is string al)
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(assemblyLocation);
            if (versionInfo is not null)
            {
                string? pv = versionInfo.ProductVersion;
                if (pv is not null)
                {
                    int index = pv.IndexOf('+');
                    if (index >= 0)
                    {
                        version = pv.Substring(0, index);
                        build = pv.Substring(index + 1);
                    }
                }
            }
        }

        AnsiConsole.MarkupLineInterpolated($"[green]Version:[/] {version ?? "Not available."} [green]Build[/]: {build ?? "Not available."}");

        return 1;
    }
}
