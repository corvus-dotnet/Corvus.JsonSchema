// <copyright file="WorkflowOperationBinderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo10;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

[TestClass]
public class WorkflowOperationBinderTests
{
    private const string Spec = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Pets", "version": "1.0.0" },
          "paths": {
            "/pets": {
              "get": { "operationId": "listPets", "responses": { "200": { "description": "ok" } } }
            },
            "/pets/{petId}": {
              "get": { "operationId": "getPet", "responses": { "200": { "description": "ok" } } }
            }
          }
        }
        """;

    private const string Document = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "Test", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adopt",
              "steps": [
                { "stepId": "list", "operationId": "listPets" },
                { "stepId": "byPath", "operationPath": "{$sourceDescriptions.petstore.url}#/paths/~1pets~1{petId}/get" },
                { "stepId": "sub", "workflowId": "register" }
              ]
            }
          ]
        }
        """;

    private ParsedJsonDocument<ArazzoDocument>? document;

    [TestCleanup]
    public void Cleanup() => this.document?.Dispose();

    [TestMethod]
    public void Binds_operation_id_step_to_generated_types()
    {
        WorkflowOperationBinder binder = CreateBinder("Acme.Pets");
        StepBinding binding = binder.Bind(this.Step(0));

        binding.Kind.ShouldBe(StepTargetKind.OperationId);
        binding.Operation.ShouldNotBeNull();
        binding.Operation!.Value.RequestTypeName.ShouldBe("Acme.Pets.ListPetsRequest");
        binding.Operation!.Value.ResponseTypeName.ShouldBe("Acme.Pets.ListPetsResponse");
    }

    [TestMethod]
    public void Binds_operation_path_step_to_generated_types()
    {
        WorkflowOperationBinder binder = CreateBinder("Acme.Pets");
        StepBinding binding = binder.Bind(this.Step(1));

        binding.Kind.ShouldBe(StepTargetKind.OperationPath);
        binding.Operation!.Value.RequestTypeName.ShouldBe("Acme.Pets.GetPetRequest");
    }

    [TestMethod]
    public void Binds_workflow_step_to_sub_workflow_id()
    {
        WorkflowOperationBinder binder = CreateBinder("Acme.Pets");
        StepBinding binding = binder.Bind(this.Step(2));

        binding.Kind.ShouldBe(StepTargetKind.WorkflowId);
        binding.SubWorkflowId.ShouldBe("register");
        binding.Operation.ShouldBeNull();
    }

    [TestMethod]
    public void Unknown_operation_id_throws()
    {
        var binder = new WorkflowOperationBinder([]);

        Should.Throw<InvalidOperationException>(() => binder.Bind(this.Step(0)));
    }

    private static OperationResolver CreateResolver()
    {
        using var spec = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(Spec));
        return OperationResolver.Create("petstore", spec.RootElement);
    }

    private WorkflowOperationBinder CreateBinder(string clientNamespace)
        => new([new SourceDescriptionClient("petstore", CreateResolver(), clientNamespace)]);

    private ArazzoDocument.StepObject Step(int index)
    {
        // The binder reads step strings from the live document, so keep it alive for the test.
        this.document ??= ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(Document));

        foreach (ArazzoDocument.WorkflowObject workflow in this.document.RootElement.Workflows.EnumerateArray())
        {
            int i = 0;
            foreach (ArazzoDocument.StepObject step in workflow.Steps.EnumerateArray())
            {
                if (i == index)
                {
                    return step;
                }

                i++;
            }

            break;
        }

        throw new InvalidOperationException($"No step at index {index}.");
    }
}