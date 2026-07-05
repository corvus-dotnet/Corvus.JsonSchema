// <copyright file="ScenariosCommands.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using System.Text.RegularExpressions;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client;
using Corvus.Text.Json.Arazzo.Generation;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Spectre.Console.Cli;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

/// <summary>Settings for the <c>scenarios run</c> command (workflow-designer design §4.5).</summary>
internal sealed class ScenariosRunSettings : RunsSettings
{
    [CommandOption("--workflow <FILE>")]
    [Description("Standalone mode: the Arazzo workflow document to run against (no control plane needed).")]
    public string? Workflow { get; init; }

    [CommandOption("--sources <DIR>")]
    [Description("Standalone mode: the directory sourceDescriptions urls resolve against (default: the workflow file's directory). http(s) urls are fetched.")]
    public string? Sources { get; init; }

    [CommandOption("--scenarios <GLOB>")]
    [Description("Standalone mode: scenario files to run (*.scenario.json), globbable (** supported) and repeatable.")]
    public string[] Scenarios { get; init; } = [];

    [CommandOption("--filter <PATTERN>")]
    [Description("Standalone mode: run only scenarios whose name matches the wildcard pattern (e.g. payment-*).")]
    public string? Filter { get; init; }

    [CommandOption("--working-copy <ID>")]
    [Description("Remote mode: run the working copy's stored scenario suite on the control plane (--server required).")]
    public string? WorkingCopy { get; init; }

    [CommandOption("--report <FORMAT=PATH>")]
    [Description("Write a report: junit=<path> or json=<path> (the suite-report shape publish embeds as evidence). Repeatable.")]
    public string[] Reports { get; init; } = [];

    [CommandOption("--github-annotations")]
    [Description("Emit ::error annotations for failed expectations and append a job-summary markdown table when GITHUB_STEP_SUMMARY is set.")]
    public bool GitHubAnnotations { get; init; }

    /// <inheritdoc/>
    public override Spectre.Console.ValidationResult Validate()
    {
        bool standalone = this.Workflow is not null;
        bool remote = this.WorkingCopy is not null;
        if (standalone == remote)
        {
            return Spectre.Console.ValidationResult.Error("Exactly one of --workflow <file> (standalone) or --working-copy <id> (remote) is required.");
        }

        if (standalone && this.Scenarios.Length == 0)
        {
            return Spectre.Console.ValidationResult.Error("--scenarios <glob> is required with --workflow.");
        }

        if (remote && (this.Scenarios.Length > 0 || this.Filter is not null || this.Sources is not null))
        {
            return Spectre.Console.ValidationResult.Error("--scenarios/--filter/--sources apply to standalone runs only; a remote run executes the working copy's stored suite.");
        }

        foreach (string report in this.Reports)
        {
            if (!report.StartsWith("junit=", StringComparison.OrdinalIgnoreCase) && !report.StartsWith("json=", StringComparison.OrdinalIgnoreCase))
            {
                return Spectre.Console.ValidationResult.Error($"Unrecognised --report '{report}': use junit=<path> or json=<path>.");
            }
        }

        // Standalone needs no server; remote inherits the base requirement.
        return standalone ? Spectre.Console.ValidationResult.Success() : base.Validate();
    }
}

/// <summary>
/// Runs a scenario suite (workflow-designer design §4.5) — the CI story. Standalone (default) hosts
/// the deterministic simulator in-process: the workflow document and its source documents compile
/// locally and every matched scenario runs against the mock transport and virtual clock, exactly as
/// the designer does interactively. Remote runs a working copy's stored suite on the control plane.
/// Exit codes: 0 all passed; 1 a scenario failed; 2 the suite could not run.
/// </summary>
internal sealed class ScenariosRunCommand : AsyncCommand<ScenariosRunSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, ScenariosRunSettings settings, CancellationToken cancellationToken)
    {
        byte[] suiteBytes;
        if (settings.WorkingCopy is { } workingCopy)
        {
            (HttpClient http, HttpClientTransport transport, ApiWorkspaceClient client) = await settings.CreateWorkspaceClientAsync(cancellationToken);
            using (http)
            await using (transport)
            {
                await using RunAllScenariosResponse response = await client.RunAllScenariosAsync((Models.JsonString.Source)workingCopy, cancellationToken);
                if (!response.TryGetOk(out Models.ScenarioSuiteReport report))
                {
                    return response.MatchResult(_ => 0, Output.Problem, Output.Problem, Output.Unexpected);
                }

                // Copy the suite out before the response's buffers go back to the pool.
                suiteBytes = PersistedJson.ToArray((JsonElement)report, static (Utf8JsonWriter writer, in JsonElement r) => r.WriteTo(writer));
            }
        }
        else
        {
            (int? error, suiteBytes) = await RunStandaloneAsync(settings, cancellationToken);
            if (error is { } exit)
            {
                return exit;
            }
        }

        using var suite = ParsedJsonDocument<JsonElement>.Parse(suiteBytes);
        WriteReports(settings, suiteBytes, suite.RootElement);
        return Summarize(settings, suite.RootElement);
    }

    // Resolves, compiles, and runs the matched scenario files in-process, producing the suite-report
    // bytes (the same shape the server's run-all returns and publish embeds as evidence). Returns an
    // exit code instead when the suite could not run at all.
    private static async Task<(int? Error, byte[] SuiteBytes)> RunStandaloneAsync(ScenariosRunSettings settings, CancellationToken cancellationToken)
    {
        string workflowPath = Path.GetFullPath(settings.Workflow!);
        if (!File.Exists(workflowPath))
        {
            Console.Error.WriteLine($"Workflow document not found: {workflowPath}");
            return (2, []);
        }

        byte[] workflowBytes = await File.ReadAllBytesAsync(workflowPath, cancellationToken);
        string sourcesBase = settings.Sources is { } dir ? Path.GetFullPath(dir) : Path.GetDirectoryName(workflowPath)!;

        // Resolve the document's sourceDescriptions against local files (or fetch http(s) urls), and
        // collect the (source, operationId) → route map scenario mocks address operations by.
        var sourceBytes = new List<KeyValuePair<string, byte[]>>();
        var routes = new Dictionary<(string Source, string OperationId), (string Method, string Path)>();
        using (var workflowDoc = ParsedJsonDocument<JsonElement>.Parse(workflowBytes))
        {
            if (workflowDoc.RootElement.TryGetProperty("sourceDescriptions"u8, out JsonElement descriptions) && descriptions.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement description in descriptions.EnumerateArray())
                {
                    if (!(description.TryGetProperty("name"u8, out JsonElement n) && n.GetString() is { Length: > 0 } name)
                        || !(description.TryGetProperty("url"u8, out JsonElement u) && u.GetString() is { Length: > 0 } url))
                    {
                        continue;
                    }

                    byte[] bytes;
                    if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        using var http = new HttpClient();
                        bytes = await http.GetByteArrayAsync(url, cancellationToken);
                    }
                    else
                    {
                        string path = Path.GetFullPath(Path.Combine(sourcesBase, url.Replace('/', Path.DirectorySeparatorChar)));
                        if (!File.Exists(path))
                        {
                            Console.Error.WriteLine($"Source document '{name}' not found: {path} (resolve with --sources <dir>).");
                            return (2, []);
                        }

                        bytes = await File.ReadAllBytesAsync(path, cancellationToken);
                    }

                    sourceBytes.Add(new(name, bytes));
                    using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse(bytes);
                    ScenarioSuite.CollectRoutes(name, sourceDoc.RootElement, routes);
                }
            }
        }

        List<string> files = MatchScenarioFiles(settings.Scenarios);
        if (files.Count == 0)
        {
            Console.Error.WriteLine("No scenario files matched --scenarios.");
            return (2, []);
        }

        Regex? filter = settings.Filter is { } pattern
            ? new Regex($"^{Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".")}$", RegexOptions.CultureInvariant)
            : null;

        // Everything the designer does interactively, headless: compile once (content-addressed cache
        // inside the simulator), then replay every matched scenario deterministically.
        var runs = new List<(string Name, SimulationResult Result, List<ScenarioSuite.Verdict> Verdicts)>();
        try
        {
            using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
            foreach (string file in files)
            {
                byte[] scenarioBytes = await File.ReadAllBytesAsync(file, cancellationToken);
                using var scenarioDoc = ParsedJsonDocument<JsonElement>.Parse(scenarioBytes);
                string name = scenarioDoc.RootElement.TryGetProperty("name"u8, out JsonElement scenarioName) && scenarioName.GetString() is { Length: > 0 } declared
                    ? declared
                    : Path.GetFileName(file).Replace(".scenario.json", string.Empty, StringComparison.OrdinalIgnoreCase);
                if (filter?.IsMatch(name) == false)
                {
                    continue;
                }

                SimulationResult result = await simulator.SimulateAsync(
                    workflowBytes, sourceBytes, ScenarioSuite.BuildScenario(scenarioDoc.RootElement, routes), cancellationToken: cancellationToken);
                if (result.Outcome == SimulationOutcome.NotExecutable)
                {
                    result.Dispose();
                    Console.Error.WriteLine($"The workflow document does not compile (running '{name}'). Validate it before running scenarios.");
                    return (2, []);
                }

                runs.Add((name, result, ScenarioSuite.Evaluate(scenarioDoc.RootElement, result)));
            }

            if (runs.Count == 0)
            {
                Console.Error.WriteLine($"No matched scenario is named like --filter '{settings.Filter}'.");
                return (2, []);
            }

            byte[] suiteBytes = PersistedJson.ToArray(
                runs,
                static (Utf8JsonWriter writer, in List<(string Name, SimulationResult Result, List<ScenarioSuite.Verdict> Verdicts)> all) =>
                {
                    int passed = 0;
                    foreach ((string _, SimulationResult _, List<ScenarioSuite.Verdict> verdicts) in all)
                    {
                        bool ok = true;
                        foreach (ScenarioSuite.Verdict v in verdicts)
                        {
                            ok &= v.Passed;
                        }

                        if (ok)
                        {
                            passed++;
                        }
                    }

                    writer.WriteStartObject();
                    writer.WriteNumber("total"u8, all.Count);
                    writer.WriteNumber("passed"u8, passed);
                    writer.WriteNumber("failed"u8, all.Count - passed);
                    writer.WriteStartArray("results"u8);
                    foreach ((string name, SimulationResult result, List<ScenarioSuite.Verdict> verdicts) in all)
                    {
                        ScenarioSuite.WriteRunResult(writer, name, result, verdicts);
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                });
            return (null, suiteBytes);
        }
        finally
        {
            foreach ((string _, SimulationResult result, List<ScenarioSuite.Verdict> _) in runs)
            {
                result.Dispose();
            }
        }
    }

    // Expands the --scenarios patterns (** via FileSystemGlobbing; a literal existing path passes
    // straight through), deduplicated and ordinal-sorted so runs and reports are deterministic.
    // Rooted and relative patterns both work: the glob runs from the pattern's longest wildcard-free
    // directory prefix (relative prefixes resolve against the current directory).
    private static List<string> MatchScenarioFiles(string[] patterns)
    {
        var files = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string pattern in patterns)
        {
            if (File.Exists(pattern))
            {
                files.Add(Path.GetFullPath(pattern));
                continue;
            }

            int wildcard = pattern.IndexOfAny(['*', '?']);
            if (wildcard < 0)
            {
                continue; // A literal path that does not exist matches nothing.
            }

            int lastSeparator = pattern.LastIndexOfAny(['/', '\\'], wildcard);
            string root = Path.GetFullPath(lastSeparator >= 0 ? pattern[..lastSeparator] : ".");
            string glob = lastSeparator >= 0 ? pattern[(lastSeparator + 1)..] : pattern;
            if (!Directory.Exists(root))
            {
                continue;
            }

            var matcher = new Matcher(StringComparison.Ordinal);
            matcher.AddInclude(glob);
            foreach (FilePatternMatch match in matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(root))).Files)
            {
                files.Add(Path.GetFullPath(Path.Combine(root, match.Path)));
            }
        }

        return [.. files];
    }

    private static void WriteReports(ScenariosRunSettings settings, byte[] suiteBytes, in JsonElement suite)
    {
        foreach (string report in settings.Reports)
        {
            int eq = report.IndexOf('=', StringComparison.Ordinal);
            string format = report[..eq];
            string path = Path.GetFullPath(report[(eq + 1)..]);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllBytes(path, suiteBytes);
            }
            else
            {
                File.WriteAllText(path, JUnitReport(suite));
            }
        }
    }

    // The JUnit XML shape CI systems ingest: one testcase per scenario; a failed scenario carries one
    // failure element listing its failed expectations.
    private static string JUnitReport(in JsonElement suite)
    {
        int total = suite.GetProperty("total"u8).GetInt32();
        int failed = suite.GetProperty("failed"u8).GetInt32();
        var xml = new System.Text.StringBuilder();
        var settings = new System.Xml.XmlWriterSettings { Indent = true, OmitXmlDeclaration = false };
        using (System.Xml.XmlWriter writer = System.Xml.XmlWriter.Create(xml, settings))
        {
            writer.WriteStartElement("testsuites");
            writer.WriteAttributeString("tests", total.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteAttributeString("failures", failed.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteStartElement("testsuite");
            writer.WriteAttributeString("name", "arazzo-scenarios");
            writer.WriteAttributeString("tests", total.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteAttributeString("failures", failed.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (JsonElement result in suite.GetProperty("results"u8).EnumerateArray())
            {
                writer.WriteStartElement("testcase");
                writer.WriteAttributeString("name", result.GetProperty("scenario"u8).GetString());
                if (!result.GetProperty("passed"u8).GetBoolean())
                {
                    writer.WriteStartElement("failure");
                    writer.WriteAttributeString("message", $"outcome: {result.GetProperty("outcome"u8).GetString()}");
                    var details = new System.Text.StringBuilder();
                    foreach (JsonElement expectation in result.GetProperty("expectations"u8).EnumerateArray())
                    {
                        if (!expectation.GetProperty("passed"u8).GetBoolean())
                        {
                            details.Append(expectation.GetProperty("kind"u8).GetString())
                                .Append(": ")
                                .AppendLine(expectation.TryGetProperty("detail"u8, out JsonElement detail) ? detail.GetString() : string.Empty);
                        }
                    }

                    writer.WriteString(details.ToString());
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        return xml.ToString();
    }

    // Console verdicts (and optional GitHub annotations / job summary) from the suite report; the CI
    // exit code — 0 when everything passed, 1 when any scenario failed.
    private static int Summarize(ScenariosRunSettings settings, in JsonElement suite)
    {
        int total = suite.GetProperty("total"u8).GetInt32();
        int passed = suite.GetProperty("passed"u8).GetInt32();
        int failed = suite.GetProperty("failed"u8).GetInt32();
        var summaryRows = settings.GitHubAnnotations ? new System.Text.StringBuilder() : null;

        foreach (JsonElement result in suite.GetProperty("results"u8).EnumerateArray())
        {
            string name = result.GetProperty("scenario"u8).GetString()!;
            string outcome = result.GetProperty("outcome"u8).GetString()!;
            bool ok = result.GetProperty("passed"u8).GetBoolean();
            Console.WriteLine($"{(ok ? "✓" : "✗")} {name} ({outcome})");
            summaryRows?.AppendLine($"| {(ok ? "✅" : "❌")} | {name} | {outcome} |");
            if (!ok)
            {
                foreach (JsonElement expectation in result.GetProperty("expectations"u8).EnumerateArray())
                {
                    if (!expectation.GetProperty("passed"u8).GetBoolean())
                    {
                        string kind = expectation.GetProperty("kind"u8).GetString()!;
                        string detail = expectation.TryGetProperty("detail"u8, out JsonElement d) ? d.GetString() ?? string.Empty : string.Empty;
                        Console.WriteLine($"    {kind}: {detail}");
                        if (settings.GitHubAnnotations)
                        {
                            Console.WriteLine($"::error title=Scenario '{name}'::{kind}: {detail}");
                        }
                    }
                }
            }
        }

        Console.WriteLine($"{passed}/{total} scenarios passed{(failed > 0 ? $", {failed} failed" : string.Empty)}.");

        if (summaryRows is not null && Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY") is { Length: > 0 } summaryPath)
        {
            File.AppendAllText(
                summaryPath,
                $"### Arazzo scenarios — {passed}/{total} passed\n\n|  | Scenario | Outcome |\n| --- | --- | --- |\n{summaryRows}\n");
        }

        return failed > 0 ? 1 : 0;
    }
}