// <copyright file="VersionCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Diagnostics;
using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Spectre.Console.Cli command to display the product version.
/// </summary>
internal class VersionCommand : Command
{
    public override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        string? assemblyLocation = Assembly.GetEntryAssembly()?.Location;
        string? version = null;
        string? build = null;
        if (assemblyLocation is string)
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