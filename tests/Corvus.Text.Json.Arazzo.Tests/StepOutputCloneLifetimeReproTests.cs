// <copyright file="StepOutputCloneLifetimeReproTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Reproduces the durable-executor step-output lifetime the generated executor relies on: a step projects its
/// outputs by cloning the addressed response-body values into the run-scoped <see cref="JsonWorkspace"/>
/// (<c>CloneAsBuilder</c>), assembles them into an outputs object (<c>JsonElement.CreateBuilder</c>), then
/// disposes the response — and a later checkpoint serialises the staged outputs object (<c>WriteTo</c>). This
/// must survive the response's disposal. The single-step / scalar-output unit tests never exercised a checkpoint
/// over a <em>nested</em> (object/array) output, which live execution does.
/// </summary>
[TestClass]
public class StepOutputCloneLifetimeReproTests
{
    [TestMethod]
    public void A_workspace_does_not_dispose_a_managed_document_another_workspace_created_when_only_referenced()
    {
        // A workspace registers a document into its table both when it creates one and when it references one from
        // another workspace to resolve a cross-workspace value. Disposal must distinguish the two: disposing a
        // document another (still-alive) workspace created corrupts that workspace. Here B only references D, which
        // A created and still owns; after B is disposed, D must remain readable.
        using JsonWorkspace a = JsonWorkspace.Create();

        // D: a workspace-managed builder document owned by A (like a step's outputs object).
        JsonElement d;
        using (ParsedJsonDocument<JsonElement> src = ParsedJsonDocument<JsonElement>.Parse("""{ "accountId": "acc-1" }"""u8.ToArray()))
        {
            d = src.RootElement.CloneAsBuilder(a).RootElement;
        }

        // Control: D is readable while its owner A is alive.
        Serialize(d).ShouldContain("acc-1");

        // B: a separate workspace that builds a value REFERENCING D, then is disposed — exactly the shape of the
        // generated OpenAPI client (JsonWorkspace.CreateUnrented() + build a request value from a caller .Source +
        // Dispose()).
        using (JsonWorkspace b = JsonWorkspace.CreateUnrented())
        {
            Span<JsonElement> values = [d];
            JsonElement built = JsonElement.CreateBuilder(
                b,
                (ReadOnlySpan<JsonElement>)values,
                static (in ReadOnlySpan<JsonElement> v, ref JsonElement.ObjectBuilder builder) => builder.AddProperty("wrapped"u8, v[0])).RootElement;
            Serialize(built).ShouldContain("acc-1");
        }

        // The experiment: A is still alive and was never disposed. Is D — which A created — still readable?
        bool dStillReadable;
        try
        {
            _ = d.ValueKind;
            dStillReadable = true;
        }
        catch (ObjectDisposedException)
        {
            dStillReadable = false;
        }

        // A workspace must only dispose documents it created; B must not have disposed A's document.
        dStillReadable.ShouldBeTrue();
    }

    [TestMethod]
    public void Scalar_output_cloned_into_workspace_survives_response_disposal_and_serialises()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement outputsElement;

        using (ParsedJsonDocument<JsonElement> response = ParsedJsonDocument<JsonElement>.Parse("""{ "accountId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301" }"""u8.ToArray()))
        {
            JsonElement body = response.RootElement;
            JsonElement value = default;
            if (body.TryResolvePointer("/accountId"u8, out JsonElement nav))
            {
                value = nav.CloneAsBuilder(workspace).RootElement;
            }

            Span<JsonElement> values = [value];
            var outputs = JsonElement.CreateBuilder(
                workspace,
                (ReadOnlySpan<JsonElement>)values,
                static (in ReadOnlySpan<JsonElement> v, ref JsonElement.ObjectBuilder builder) =>
                {
                    builder.AddProperty("accountId"u8, v[0]);
                });
            outputsElement = outputs.RootElement;
        }

        // The response document is now disposed (as the executor disposes it before the checkpoint).
        Serialize(outputsElement).ShouldContain("3f2504e0");
    }

    [TestMethod]
    public void Nested_outputs_cloned_into_workspace_survive_response_disposal_and_serialise()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement outputsElement;

        using (ParsedJsonDocument<JsonElement> response = ParsedJsonDocument<JsonElement>.Parse(
            """
            {
              "score": 0.92,
              "method": "document",
              "applicant": { "fullName": "Ada Lovelace", "country": "GB" },
              "evidence": { "kind": "document", "documentType": "passport" },
              "flags": []
            }
            """u8.ToArray()))
        {
            JsonElement body = response.RootElement;
            JsonElement o0 = Project(body, "/score"u8, workspace);
            JsonElement o1 = Project(body, "/method"u8, workspace);
            JsonElement o2 = Project(body, "/applicant"u8, workspace);
            JsonElement o3 = Project(body, "/evidence"u8, workspace);
            JsonElement o4 = Project(body, "/flags"u8, workspace);

            Span<JsonElement> values = [o0, o1, o2, o3, o4];
            var outputs = JsonElement.CreateBuilder(
                workspace,
                (ReadOnlySpan<JsonElement>)values,
                static (in ReadOnlySpan<JsonElement> v, ref JsonElement.ObjectBuilder builder) =>
                {
                    builder.AddProperty("score"u8, v[0]);
                    builder.AddProperty("method"u8, v[1]);
                    builder.AddProperty("applicant"u8, v[2]);
                    builder.AddProperty("evidence"u8, v[3]);
                    builder.AddProperty("flags"u8, v[4]);
                });
            outputsElement = outputs.RootElement;
        }

        // The response document is now disposed; serialising the staged outputs (as the checkpoint does) must
        // not touch the disposed response.
        string serialized = Serialize(outputsElement);
        serialized.ShouldContain("Ada Lovelace");
        serialized.ShouldContain("passport");
    }

    [TestMethod]
    public void Two_nested_outputs_objects_in_one_workspace_both_survive_a_second_clone_round()
    {
        // Mirrors a two-step run: the first step's outputs object must remain serialisable after the second
        // step clones its own (nested) outputs into the same workspace and disposes its response.
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement firstOutputs;
        JsonElement secondOutputs;

        using (ParsedJsonDocument<JsonElement> firstResponse = ParsedJsonDocument<JsonElement>.Parse("""{ "accountId": "acc-1" }"""u8.ToArray()))
        {
            Span<JsonElement> values = [Project(firstResponse.RootElement, "/accountId"u8, workspace)];
            firstOutputs = JsonElement.CreateBuilder(
                workspace,
                (ReadOnlySpan<JsonElement>)values,
                static (in ReadOnlySpan<JsonElement> v, ref JsonElement.ObjectBuilder builder) => builder.AddProperty("accountId"u8, v[0])).RootElement;
        }

        // First step's outputs serialise fine here (its own checkpoint).
        Serialize(firstOutputs).ShouldContain("acc-1");

        using (ParsedJsonDocument<JsonElement> secondResponse = ParsedJsonDocument<JsonElement>.Parse(
            """{ "applicant": { "fullName": "Ada Lovelace" }, "flags": [] }"""u8.ToArray()))
        {
            JsonElement a = Project(secondResponse.RootElement, "/applicant"u8, workspace);
            JsonElement f = Project(secondResponse.RootElement, "/flags"u8, workspace);
            Span<JsonElement> values = [a, f];
            secondOutputs = JsonElement.CreateBuilder(
                workspace,
                (ReadOnlySpan<JsonElement>)values,
                static (in ReadOnlySpan<JsonElement> v, ref JsonElement.ObjectBuilder builder) =>
                {
                    builder.AddProperty("applicant"u8, v[0]);
                    builder.AddProperty("flags"u8, v[1]);
                }).RootElement;
        }

        // The second step's checkpoint serialises BOTH step outputs (this is where live execution faulted).
        Serialize(firstOutputs).ShouldContain("acc-1");
        Serialize(secondOutputs).ShouldContain("Ada Lovelace");
    }

    [TestMethod]
    public async Task Nested_outputs_from_an_async_stream_parsed_response_survive_disposal()
    {
        // Faithful to the HTTP transport: the response body is parsed from a stream via ParseAsync, then the
        // step's outputs are cloned into the run workspace, the response document is disposed, and a later
        // checkpoint serialises the staged outputs.
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement outputsElement;

        byte[] payload = """
            { "score": 0.92, "applicant": { "fullName": "Ada Lovelace" }, "evidence": { "kind": "document", "documentType": "passport" }, "flags": [] }
            """u8.ToArray();
        using (var stream = new System.IO.MemoryStream(payload))
        using (ParsedJsonDocument<JsonElement> response = await ParsedJsonDocument<JsonElement>.ParseAsync(stream, default))
        {
            JsonElement body = response.RootElement;
            JsonElement o0 = Project(body, "/score"u8, workspace);
            JsonElement o1 = Project(body, "/applicant"u8, workspace);
            JsonElement o2 = Project(body, "/evidence"u8, workspace);
            JsonElement o3 = Project(body, "/flags"u8, workspace);
            Span<JsonElement> values = [o0, o1, o2, o3];
            outputsElement = JsonElement.CreateBuilder(
                workspace,
                (ReadOnlySpan<JsonElement>)values,
                static (in ReadOnlySpan<JsonElement> v, ref JsonElement.ObjectBuilder builder) =>
                {
                    builder.AddProperty("score"u8, v[0]);
                    builder.AddProperty("applicant"u8, v[1]);
                    builder.AddProperty("evidence"u8, v[2]);
                    builder.AddProperty("flags"u8, v[3]);
                }).RootElement;
        }

        string serialized = Serialize(outputsElement);
        serialized.ShouldContain("Ada Lovelace");
        serialized.ShouldContain("passport");
    }

    private static JsonElement Project(in JsonElement body, ReadOnlySpan<byte> pointer, JsonWorkspace workspace)
    {
        if (body.TryResolvePointer(pointer, out JsonElement nav))
        {
            return nav.CloneAsBuilder(workspace).RootElement;
        }

        return default;
    }

    // Serialises the element exactly as the checkpoint does (a full traversal of its backing document), so a
    // dangling reference into a disposed document surfaces here as it does in WorkflowCheckpointSerializer.
    private static string Serialize(in JsonElement element) => element.GetRawText();
}
