// <copyright file="WorkflowExecutorEmitterTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo10;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

[TestClass]
public class WorkflowExecutorEmitterTests
{
    private const string Document = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adopt",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    [TestMethod]
    public void Emits_a_complete_executor_class()
    {
        string source = Emit();

        source.ShouldContain("public static class AdoptWorkflow");
        source.ShouldContain("public static async ValueTask<Acme.Pets.AdoptOutputs> ExecuteAsync(IApiTransport transport, JsonWorkspace workspace, Acme.Pets.AdoptInputs inputs, CancellationToken cancellationToken = default)");
        source.ShouldContain("context.SetInputs(inputs);");
        source.ShouldContain("ArazzoTelemetry.WorkflowsStarted.Add(1);");
        source.ShouldContain("ArazzoTelemetry.WorkflowsCompleted.Add(1);");
        source.ShouldContain("ArazzoTelemetry.WorkflowsFaulted.Add(1);");

        // Defensive: validates arguments and records the failure on the span.
        source.ShouldContain("ArgumentNullException.ThrowIfNull(transport);");
        source.ShouldContain("ArgumentNullException.ThrowIfNull(workspace);");
        source.ShouldContain("activity?.SetStatus(ActivityStatusCode.Error, ex.Message);");
    }

    [TestMethod]
    public void Emits_the_step_client_call_and_gate()
    {
        string source = Emit();

        source.ShouldContain("// ── step: getPet ──");
        source.ShouldContain("var getPetClient = new Acme.Pets.PetsClient(transport);");
        source.ShouldContain("var getPetResponse = await getPetClient.GetPetAsync(petId: petIdValue, cancellationToken: cancellationToken).ConfigureAwait(false);");
        // The body is bound as a live reference — no whole-body clone.
        source.ShouldContain("if (getPetResponse.StatusCode == 200) { getPetResponseBody = (JsonElement)getPetResponse.OkBody; }");
        source.ShouldNotContain("CloneAsBuilder(workspace).RootElement); }");
        source.ShouldContain("await getPetResponse.DisposeAsync().ConfigureAwait(false);");
        source.ShouldContain("getPet_SuccessCriterion0.Evaluate(context)");
    }

    [TestMethod]
    public void Emits_step_and_workflow_output_extraction_without_a_dictionary()
    {
        string source = Emit();

        // Step outputs are built into a local, not stored in a context dictionary. The element is
        // declared before the step's try (so later steps see it) and assigned inside it.
        source.ShouldNotContain("SetStepOutputs");
        source.ShouldContain("JsonElement getPetOutputsElement = default;");
        source.ShouldContain("getPetOutputsElement = getPetOutputs.RootElement;");
        source.ShouldContain("builder.AddProperty(\"petName\"u8, values[0]);");
        // petName = $response.body#/name is projected from the live body, copying only that value.
        source.ShouldContain("getPetResponseBody.TryResolvePointer(\"/name\"u8, out JsonElement getPetOutput0Nav)");

        // The workflow output `name` = $steps.getPet.outputs.petName resolves statically against the
        // step's outputs local — direct navigation, no dictionary lookup.
        source.ShouldContain("getPetOutputsElement.TryGetProperty(\"petName\"u8, out JsonElement workflowOutput0);");
        source.ShouldContain("builder.AddProperty(\"name\"u8, values[0]);");
        source.ShouldContain("return workflowOutputsElement;");
    }

    private const string StatusOnlyDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adopt",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "echo": "$inputs.petId" }
                }
              ],
              "outputs": { "code": "$steps.getPet.outputs.echo" }
            }
          ]
        }
        """;

    [TestMethod]
    public void Skips_the_response_body_clone_for_a_status_only_step()
    {
        // The step references only $statusCode — never $response.body — so no clone is emitted.
        string source = Emit(StatusOnlyDocument);

        source.ShouldNotContain("CloneAsBuilder");
        source.ShouldNotContain("SetResponseBody");
        source.ShouldContain("context.SetResponseStatusCode(getPetResponse.StatusCode);");
    }

    private static string Emit(string document = Document)
    {
        OperationDescriptor[] operations =
        [
            new(
                "/pets/{petId}",
                OperationMethod.Get,
                "getPet",
                "GetPet",
                "Acme.Pets.GetPetRequest",
                "Acme.Pets.GetPetResponse",
                [new RequestParameterInfo("petId", ParameterLocation.Path, "PetId", "Acme.Pets.JsonString", true, "petId")],
                false,
                [new ResponseDescriptor("200", "Acme.Pets.Pet", "OkBody")],
                "Acme.Pets.PetsClient",
                "GetPetAsync",
                null),
        ];

        var binder = new WorkflowOperationBinder([new SourceDescriptionClient("petstore", OperationResolver.Create("petstore", operations))]);

        using var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(document));
        foreach (ArazzoDocument.WorkflowObject workflow in doc.RootElement.Workflows.EnumerateArray())
        {
            return WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions("Acme.Pets.Workflows", "AdoptWorkflow", "Acme.Pets.AdoptInputs", "Acme.Pets.AdoptOutputs"));
        }

        throw new InvalidOperationException("No workflow.");
    }
}