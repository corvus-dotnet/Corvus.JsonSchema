// <copyright file="OperatorCommandHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Spectre.Console;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

/// <summary>Shared rendering helpers for the operator-surface list commands (runners / builds / deployments).</summary>
internal static class OperatorCommandHelpers
{
    /// <summary>Formats an optional instant as a sortable UTC timestamp (or an em dash when absent) — operator views need
    /// the time of day (liveness, build/deploy latency), not just the date.</summary>
    /// <param name="value">The optional date-time value.</param>
    /// <returns>The formatted instant.</returns>
    public static string FormatInstant(Models.JsonDateTime value)
        => value.IsNotUndefined()
            ? ((NodaTime.OffsetDateTime)value).ToDateTimeOffset().UtcDateTime.ToString("u", CultureInfo.InvariantCulture)
            : "—";

    /// <summary>Formats an optional string value (or an em dash when absent).</summary>
    /// <param name="value">The optional string value.</param>
    /// <returns>The value, or an em dash.</returns>
    public static string FormatOptional(Models.JsonString value)
        => value.IsNotUndefined() ? (string)value : "—";

    /// <summary>Finishes a list command: prints the accumulated JSON envelope, or writes the table to the console.</summary>
    /// <param name="asJson">Whether <c>--output json</c> was asked for.</param>
    /// <param name="jsonItems">The accumulated raw JSON items (non-null exactly when <paramref name="asJson"/>).</param>
    /// <param name="table">The accumulated table.</param>
    /// <param name="envelopeName">The JSON envelope property name (e.g. <c>runners</c>).</param>
    /// <returns>The process exit code.</returns>
    public static int Finish(bool asJson, List<string>? jsonItems, Table table, string envelopeName)
    {
        if (asJson)
        {
            return Output.Print($"{{\"{envelopeName}\":[{string.Join(",", jsonItems!)}]}}");
        }

        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Out) });
        console.Write(table);
        return 0;
    }
}