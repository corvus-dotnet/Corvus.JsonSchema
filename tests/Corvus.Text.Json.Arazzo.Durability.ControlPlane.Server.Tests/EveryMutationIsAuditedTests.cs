// <copyright file="EveryMutationIsAuditedTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Holds the control plane to ADR 0038's "every governed action is audited". It reads the OpenAPI contract for every
/// operation that changes state, finds that operation's handler in the source, and requires an audit call in it.
/// V-29 of the 2026-08-07 audit found 22 of 82 mutating operations recording nothing, publishing a catalog version from
/// the designer among them, and no test noticed. A new mutating operation now fails here until it either audits or is
/// put on the exemption list below with its reason.
/// </summary>
/// <remarks>
/// This is a check over the source, and deliberately so: driving all of these operations to their success paths would
/// take a fixture apiece, and the defect it guards against is an omission, which is a property of the source. It finds
/// the repository from this file's own compile-time path.
/// </remarks>
[TestClass]
public sealed partial class EveryMutationIsAuditedTests
{
    // Operations that are POSTs and change nothing: they compute over what the caller sent, hold no state afterwards and
    // disclose nothing the caller did not supply. Each is here by decision, with its reason.
    private static readonly Dictionary<string, string> ComputeOnly = new(StringComparer.Ordinal)
    {
        ["validateWorkspaceWorkflow"] = "Validates the working copy the caller names and stores nothing.",
        ["validateCatalogValue"] = "Validates a value against a catalogued version's schema and stores nothing.",
        ["simulateWorkingCopy"] = "Runs the simulator over a working copy and stores nothing.",
        ["simulateCatalogVersion"] = "Runs the simulator over a catalogued version and stores nothing.",
        ["runScenario"] = "Runs one scenario in the simulator and stores nothing.",
        ["runAllScenarios"] = "Runs a working copy's scenarios in the simulator and stores nothing.",
    };

    // GETs that change state. OAuth makes its callback a GET, and on success the control plane takes custody of a
    // user's token, so these are held to the rule although the verb would let them past.
    private static readonly string[] StateChangingGets = ["completeGitHubAuth", "completeProviderAuth"];

    // Operations whose record is written by something they hand the work to, named here so that the hand-off is checked
    // at both ends: the handler must call it, and it must audit.
    private static readonly Dictionary<string, string> AuditedByDelegation = new(StringComparer.Ordinal)
    {
        ["startCatalogWorkflowRun"] = "AdmitAndStartAsync",
        ["rerunRun"] = "AdmitAndStartAsync",
    };

    [TestMethod]
    public void Every_operation_that_changes_state_records_an_audit_entry()
    {
        string root = RepositoryRoot();
        IReadOnlyList<string> operations = MutatingOperations(root);
        string[] handlers = Directory.GetFiles(Path.Combine(root, "src", "Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server"), "*Handler.cs");
        string[] sources = [.. handlers.Select(static f => File.ReadAllText(f).ReplaceLineEndings("\n"))];

        // Sanity: the contract was read and the handlers found, so an empty result cannot pass for a clean one.
        operations.Count.ShouldBeGreaterThan(70);
        sources.Length.ShouldBeGreaterThan(10);

        var unaudited = new List<string>();
        foreach (string operationId in operations)
        {
            if (ComputeOnly.ContainsKey(operationId))
            {
                continue;
            }

            string? body = MethodBody(sources, HandlerName(operationId));
            if (body is null)
            {
                unaudited.Add($"{operationId}: no handler method {HandlerName(operationId)} was found");
                continue;
            }

            if (DirectAuditCall().IsMatch(body))
            {
                continue;
            }

            if (AuditedByDelegation.TryGetValue(operationId, out string? delegateName)
                && body.Contains(delegateName + "(", StringComparison.Ordinal)
                && MethodBody(sources, delegateName) is { } delegateBody
                && DirectAuditCall().IsMatch(delegateBody))
            {
                continue;
            }

            unaudited.Add($"{operationId}: {HandlerName(operationId)} makes no audit call");
        }

        unaudited.ShouldBeEmpty("every operation that changes state records an audit entry (ADR 0038), or is on the compute-only list with its reason");
    }

    [TestMethod]
    public void The_exemption_lists_name_only_operations_the_contract_still_has()
    {
        // A list that outlives its operation is a hole waiting for a new operation of the same name.
        string root = RepositoryRoot();
        HashSet<string> all = AllOperations(root);
        foreach (string operationId in ComputeOnly.Keys.Concat(StateChangingGets).Concat(AuditedByDelegation.Keys))
        {
            all.ShouldContain(operationId);
        }

        foreach (string reason in ComputeOnly.Values)
        {
            reason.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [GeneratedRegex(@"this\.auditor\.(MutationAsync|RefusalAsync)\(")]
    private static partial Regex DirectAuditCall();

    private static string HandlerName(string operationId) => "Handle" + char.ToUpperInvariant(operationId[0]) + operationId[1..] + "Async";

    // The text of a method, from its declaration to the next member at class level. Members are indented four spaces in
    // this codebase, so the next line that starts a member or its documentation ends the method.
    private static string? MethodBody(string[] sources, string methodName)
    {
        var declaration = new Regex(@"^    (?:public|private|internal) (?:async )?ValueTask<[^\n(]+> " + Regex.Escape(methodName) + @"\(", RegexOptions.Multiline);
        var nextMember = new Regex(@"^    (?:public|private|internal|protected|///) ", RegexOptions.Multiline);
        foreach (string source in sources)
        {
            Match start = declaration.Match(source);
            if (!start.Success)
            {
                continue;
            }

            int from = start.Index + start.Length;
            Match end = nextMember.Match(source, from);
            return end.Success ? source[start.Index..end.Index] : source[start.Index..];
        }

        return null;
    }

    private static IReadOnlyList<string> MutatingOperations(string root)
    {
        var operations = new List<string>();
        using Stj.JsonDocument contract = Stj.JsonDocument.Parse(File.ReadAllBytes(ContractPath(root)));
        foreach (Stj.JsonProperty path in contract.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (Stj.JsonProperty verb in path.Value.EnumerateObject())
            {
                if (verb.Name is "post" or "put" or "patch" or "delete" && verb.Value.TryGetProperty("operationId", out Stj.JsonElement id))
                {
                    operations.Add(id.GetString()!);
                }
            }
        }

        operations.AddRange(StateChangingGets);
        return operations;
    }

    private static HashSet<string> AllOperations(string root)
    {
        var operations = new HashSet<string>(StringComparer.Ordinal);
        using Stj.JsonDocument contract = Stj.JsonDocument.Parse(File.ReadAllBytes(ContractPath(root)));
        foreach (Stj.JsonProperty path in contract.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (Stj.JsonProperty verb in path.Value.EnumerateObject())
            {
                if (verb.Value.ValueKind == Stj.JsonValueKind.Object && verb.Value.TryGetProperty("operationId", out Stj.JsonElement id))
                {
                    operations.Add(id.GetString()!);
                }
            }
        }

        return operations;
    }

    private static string ContractPath(string root) => Path.Combine(root, "docs", "arazzo", "reference", "arazzo-control-plane.openapi.json");

    private static string RepositoryRoot([CallerFilePath] string thisFile = "")
    {
        // tests/<project>/<this file>
        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));
        File.Exists(ContractPath(root)).ShouldBeTrue($"the control-plane contract was not found under '{root}'.");
        return root;
    }
}