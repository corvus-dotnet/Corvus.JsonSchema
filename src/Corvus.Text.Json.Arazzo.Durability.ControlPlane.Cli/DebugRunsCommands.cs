// <copyright file="DebugRunsCommands.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Spectre.Console.Cli;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client.Models;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

// §18 debug runs (workflow-designer design §18) are the durable, forward-only debug runs a designer drives
// against a working copy: start, inspect, step/resume, inject a message into a suspended message-wait, cancel,
// and delete. They live under a working copy (/workspace/workflows/{id}/debug-runs/...), so every command
// takes the working-copy id first. This CLI surface is the scripting/CI counterpart to the designer's dock.
//
// The request-body model `.Source` values are ref structs, so they are never held in a local across an await:
// each is built inline in the client-call argument (evaluated before the await suspends), exactly as the runs
// resume command does.

/// <summary>Settings for a debug-run command scoped to a working copy.</summary>
internal class DebugRunWorkingCopySettings : RunsSettings
{
    [CommandArgument(0, "<workingCopyId>")]
    [Description("The working-copy id the debug run belongs to.")]
    public string WorkingCopyId { get; init; } = string.Empty;
}

/// <summary>Settings for a debug-run command that targets a single debug run.</summary>
internal class DebugRunIdSettings : DebugRunWorkingCopySettings
{
    [CommandArgument(1, "<debugRunId>")]
    [Description("The debug-run id.")]
    public string DebugRunId { get; init; } = string.Empty;
}

internal sealed class DebugRunStartSettings : DebugRunWorkingCopySettings
{
    [CommandOption("--workflow-id <ID>")]
    [Description("The workflow within the working copy's document to run.")]
    public string? WorkflowId { get; init; }

    [CommandOption("--environment <NAME>")]
    [Description("The development-class environment to run the draft in (must allow draft runs).")]
    [DefaultValue("development")]
    public string Environment { get; init; } = "development";

    [CommandOption("--inputs-file <PATH>")]
    [Description("Path to a JSON file of the workflow's inputs.")]
    public string? InputsFile { get; init; }

    [CommandOption("--pause-after-each")]
    [Description("Pause after every step (single-step the run).")]
    public bool PauseAfterEach { get; init; }

    [CommandOption("--breakpoint <STEP_ID>")]
    [Description("Pause before this step (a breakpoint); repeatable.")]
    public string[] Breakpoints { get; init; } = [];

    /// <inheritdoc/>
    public override Spectre.Console.ValidationResult Validate()
        => string.IsNullOrEmpty(this.WorkflowId)
            ? Spectre.Console.ValidationResult.Error("--workflow-id <id> is required for start.")
            : base.Validate();
}

internal sealed class DebugRunResumeSettings : DebugRunIdSettings
{
    [CommandOption("--pause-after-each")]
    [Description("Pause after every step (single-step from here).")]
    public bool PauseAfterEach { get; init; }

    [CommandOption("--breakpoint <STEP_ID>")]
    [Description("Pause before this step (a breakpoint); repeatable.")]
    public string[] Breakpoints { get; init; } = [];

    [CommandOption("--action <ACTION>")]
    [Description("Remediation for a faulted debug run: RetryFaultedStep, Rewind, Skip or StatePatch. Omit to simply advance (step/continue).")]
    public string? Action { get; init; }

    [CommandOption("--target-cursor <N>")]
    [Description("Rewind: the cursor to rewind to (required). Skip: the cursor to resume at (default faulted + 1).")]
    public int? TargetCursor { get; init; }

    [CommandOption("--skip-outputs-file <PATH>")]
    [Description("Skip: path to a JSON file of outputs to record for the skipped step.")]
    public string? SkipOutputsFile { get; init; }

    [CommandOption("--patch-file <PATH>")]
    [Description("StatePatch: path to a file containing an RFC 6902 JSON Patch array.")]
    public string? PatchFile { get; init; }
}

internal sealed class DebugRunInjectSettings : DebugRunIdSettings
{
    [CommandOption("--channel <CHANNEL>")]
    [Description("The AsyncAPI channel the suspended run is awaiting a message on.")]
    public string? Channel { get; init; }

    [CommandOption("--payload-file <PATH>")]
    [Description("Path to a JSON file with the message payload to deliver.")]
    public string? PayloadFile { get; init; }

    [CommandOption("--correlation-id <ID>")]
    [Description("Correlation id to match (optional; omit to match any awaiting run on the channel).")]
    public string? CorrelationId { get; init; }

    /// <inheritdoc/>
    public override Spectre.Console.ValidationResult Validate()
    {
        if (string.IsNullOrEmpty(this.Channel))
        {
            return Spectre.Console.ValidationResult.Error("--channel <channel> is required for inject-message.");
        }

        if (string.IsNullOrEmpty(this.PayloadFile))
        {
            return Spectre.Console.ValidationResult.Error("--payload-file <path> is required for inject-message.");
        }

        return base.Validate();
    }
}

/// <summary>Helpers shared by the debug-run commands: the pause configuration. Returns a ref-struct
/// <c>Source</c>, so callers must consume it inline (never hold it in a local across an await).</summary>
internal static class DebugRunCommandHelpers
{
    /// <summary>Builds the pause configuration (breakpoints + step-through) from the flags, or <c>default</c>
    /// (no pause — run to completion or the next existing breakpoint) when neither is set.</summary>
    public static Models.DebugRunPause.Source BuildPause(bool afterEach, string[] breakpoints)
    {
        if (!afterEach && breakpoints.Length == 0)
        {
            return default;
        }

        Models.JsonBoolean.Source afterEachStep = afterEach ? (Models.JsonBoolean.Source)true : default;
        Models.DebugRunPause.JsonStringArray.Source beforeSteps = default;
        if (breakpoints.Length > 0)
        {
            string[] steps = breakpoints;
            beforeSteps = new Models.DebugRunPause.JsonStringArray.Source((ref Models.DebugRunPause.JsonStringArray.Builder arrayBuilder) =>
            {
                foreach (string step in steps)
                {
                    arrayBuilder.AddItem(step);
                }
            });
        }

        return Models.DebugRunPause.Build(afterEachStep: afterEachStep, beforeSteps: beforeSteps);
    }
}

internal sealed class DebugRunStartCommand : AsyncCommand<DebugRunStartSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, DebugRunStartSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiDebugRunsClient client) = await settings.CreateDebugRunsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            // The parsed inputs document (if any) must stay alive while the request body is built and sent, so build
            // the body and call the client inside its `using`; the pause and inputs are built inline (ref structs).
            if (settings.InputsFile is { } inputsFile)
            {
                if (!File.Exists(inputsFile))
                {
                    Console.Error.WriteLine($"--inputs-file not found: {inputsFile}");
                    return 1;
                }

                using ParsedJsonDocument<JsonElement> inputsDoc = ParsedJsonDocument<JsonElement>.Parse(File.ReadAllBytes(inputsFile));
                await using StartDebugRunResponse withInputs = await client.StartDebugRunAsync(
                    settings.WorkingCopyId,
                    Models.DebugRunStart.Build(
                        environment: settings.Environment,
                        workflowId: settings.WorkflowId!,
                        inputs: (Models.DebugRunStart.TheWorkflowSInputsValidatedAgainstItsOwnInputsSchema)inputsDoc.RootElement,
                        pause: DebugRunCommandHelpers.BuildPause(settings.PauseAfterEach, settings.Breakpoints)),
                    cancellationToken);
                return RenderStart(withInputs);
            }

            await using StartDebugRunResponse response = await client.StartDebugRunAsync(
                settings.WorkingCopyId,
                Models.DebugRunStart.Build(
                    environment: settings.Environment,
                    workflowId: settings.WorkflowId!,
                    pause: DebugRunCommandHelpers.BuildPause(settings.PauseAfterEach, settings.Breakpoints)),
                cancellationToken);
            return RenderStart(response);
        }
    }

    private static int RenderStart(StartDebugRunResponse response) => response.MatchResult(
        run => Output.Print(run.ToString()),
        Output.Problem,
        Output.Problem,
        Output.Problem,
        Output.Problem,
        Output.Unexpected);
}

internal sealed class DebugRunGetCommand : AsyncCommand<DebugRunIdSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, DebugRunIdSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiDebugRunsClient client) = await settings.CreateDebugRunsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using GetDebugRunResponse response = await client.GetDebugRunAsync(settings.WorkingCopyId, settings.DebugRunId, cancellationToken);
            return response.MatchResult(
                run => Output.Print(run.ToString()),
                Output.Problem,
                Output.Unexpected);
        }
    }
}

internal sealed class DebugRunResumeCommand : AsyncCommand<DebugRunResumeSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, DebugRunResumeSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiDebugRunsClient client) = await settings.CreateDebugRunsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            // With no --action the debug run simply advances (step/continue) under the pause config. Otherwise the
            // action is the same ResumeRequest union the runs view uses. Every ref-struct Source (pause, action) is
            // built inline in the client-call argument; the file-valued modes do so inside the parsed document's scope.
            switch (settings.Action)
            {
                case null:
                case "":
                {
                    await using ResumeDebugRunResponse response = await client.ResumeDebugRunAsync(
                        settings.WorkingCopyId,
                        settings.DebugRunId,
                        Models.DebugRunResume.Build(pause: DebugRunCommandHelpers.BuildPause(settings.PauseAfterEach, settings.Breakpoints)),
                        cancellationToken);
                    return RenderResume(response);
                }

                case "RetryFaultedStep":
                {
                    await using ResumeDebugRunResponse response = await client.ResumeDebugRunAsync(
                        settings.WorkingCopyId,
                        settings.DebugRunId,
                        Models.DebugRunResume.Build(
                            action: Models.ResumeRequest.Build(static (ref Models.RetryFaultedStepResume.Builder b) => b.Create()),
                            pause: DebugRunCommandHelpers.BuildPause(settings.PauseAfterEach, settings.Breakpoints)),
                        cancellationToken);
                    return RenderResume(response);
                }

                case "Rewind":
                {
                    if (settings.TargetCursor is not { } rewindCursor)
                    {
                        Console.Error.WriteLine("--target-cursor <n> is required for --action Rewind.");
                        return 1;
                    }

                    await using ResumeDebugRunResponse response = await client.ResumeDebugRunAsync(
                        settings.WorkingCopyId,
                        settings.DebugRunId,
                        Models.DebugRunResume.Build(
                            action: Models.RewindResume.Build(rewindCursor),
                            pause: DebugRunCommandHelpers.BuildPause(settings.PauseAfterEach, settings.Breakpoints)),
                        cancellationToken);
                    return RenderResume(response);
                }

                case "Skip":
                    return await ResumeSkipAsync(client, settings, cancellationToken);

                case "StatePatch":
                    return await ResumeStatePatchAsync(client, settings, cancellationToken);

                default:
                    Console.Error.WriteLine($"Unknown --action '{settings.Action}'. Use RetryFaultedStep, Rewind, Skip or StatePatch (or omit to step/continue).");
                    return 1;
            }
        }
    }

    private static async Task<int> ResumeSkipAsync(ApiDebugRunsClient client, DebugRunResumeSettings settings, CancellationToken cancellationToken)
    {
        if (settings.SkipOutputsFile is { } outputsFile)
        {
            if (!File.Exists(outputsFile))
            {
                Console.Error.WriteLine($"--skip-outputs-file not found: {outputsFile}");
                return 1;
            }

            using ParsedJsonDocument<JsonElement> outputs = ParsedJsonDocument<JsonElement>.Parse(File.ReadAllBytes(outputsFile));
            await using ResumeDebugRunResponse response = await client.ResumeDebugRunAsync(
                settings.WorkingCopyId,
                settings.DebugRunId,
                Models.DebugRunResume.Build(
                    action: Models.SkipResume.Build(
                        skipOutputs: outputs.RootElement,
                        targetCursor: settings.TargetCursor is { } c ? (Models.JsonInt32.Source)c : default),
                    pause: DebugRunCommandHelpers.BuildPause(settings.PauseAfterEach, settings.Breakpoints)),
                cancellationToken);
            return RenderResume(response);
        }

        await using ResumeDebugRunResponse noOutputs = await client.ResumeDebugRunAsync(
            settings.WorkingCopyId,
            settings.DebugRunId,
            Models.DebugRunResume.Build(
                action: Models.SkipResume.Build(targetCursor: settings.TargetCursor is { } sc ? (Models.JsonInt32.Source)sc : default),
                pause: DebugRunCommandHelpers.BuildPause(settings.PauseAfterEach, settings.Breakpoints)),
            cancellationToken);
        return RenderResume(noOutputs);
    }

    private static async Task<int> ResumeStatePatchAsync(ApiDebugRunsClient client, DebugRunResumeSettings settings, CancellationToken cancellationToken)
    {
        if (settings.PatchFile is not { } patchFile)
        {
            Console.Error.WriteLine("--patch-file <path> is required for --action StatePatch.");
            return 1;
        }

        if (!File.Exists(patchFile))
        {
            Console.Error.WriteLine($"--patch-file not found: {patchFile}");
            return 1;
        }

        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(File.ReadAllBytes(patchFile));
        await using ResumeDebugRunResponse response = await client.ResumeDebugRunAsync(
            settings.WorkingCopyId,
            settings.DebugRunId,
            Models.DebugRunResume.Build(
                action: Models.StatePatchResume.Build((Models.StatePatchResume.JsonObjectArray)document.RootElement),
                pause: DebugRunCommandHelpers.BuildPause(settings.PauseAfterEach, settings.Breakpoints)),
            cancellationToken);
        return RenderResume(response);
    }

    private static int RenderResume(ResumeDebugRunResponse response) => response.MatchResult(
        run => Output.Print(run.ToString()),
        Output.Problem,
        Output.Problem,
        Output.Unexpected);
}

internal sealed class DebugRunInjectCommand : AsyncCommand<DebugRunInjectSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, DebugRunInjectSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiDebugRunsClient client) = await settings.CreateDebugRunsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            if (!File.Exists(settings.PayloadFile!))
            {
                Console.Error.WriteLine($"--payload-file not found: {settings.PayloadFile}");
                return 1;
            }

            using ParsedJsonDocument<JsonElement> payload = ParsedJsonDocument<JsonElement>.Parse(File.ReadAllBytes(settings.PayloadFile!));
            await using InjectDebugRunMessageResponse response = await client.InjectDebugRunMessageAsync(
                settings.WorkingCopyId,
                settings.DebugRunId,
                Models.DebugRunMessageInjection.Build(
                    channel: settings.Channel!,
                    payload: payload.RootElement,
                    correlationId: settings.CorrelationId is { } cid ? (Models.JsonString.Source)cid : default),
                cancellationToken);
            return response.MatchResult(
                run => Output.Print(run.ToString()),
                Output.Problem,
                Output.Problem,
                Output.Unexpected);
        }
    }
}

internal sealed class DebugRunCancelCommand : AsyncCommand<DebugRunIdSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, DebugRunIdSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiDebugRunsClient client) = await settings.CreateDebugRunsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using CancelDebugRunResponse response = await client.CancelDebugRunAsync(settings.WorkingCopyId, settings.DebugRunId, cancellationToken);
            return response.MatchResult(
                run => Output.Print(run.ToString()),
                Output.Problem,
                Output.Unexpected);
        }
    }
}

internal sealed class DebugRunDeleteCommand : AsyncCommand<DebugRunIdSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, DebugRunIdSettings settings, CancellationToken cancellationToken)
    {
        (HttpClient http, HttpClientTransport transport, ApiDebugRunsClient client) = await settings.CreateDebugRunsClientAsync(cancellationToken);
        using (http)
        await using (transport)
        {
            await using DeleteDebugRunResponse response = await client.DeleteDebugRunAsync(settings.WorkingCopyId, settings.DebugRunId, cancellationToken);
            if (response.StatusCode == 204)
            {
                Console.WriteLine($"Deleted debug run '{settings.DebugRunId}'.");
                return 0;
            }

            return response.MatchResult(
                Output.Problem,
                Output.Unexpected);
        }
    }
}