// <copyright file="WorkflowOperationBinderTests.cs" company="Endjin Limited">
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
public class WorkflowOperationBinderTests
{
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
    public void Binds_operation_id_step_to_resolved_operation()
    {
        WorkflowOperationBinder binder = CreateBinder();
        StepBinding binding = binder.Bind(this.Step(0));

        binding.Kind.ShouldBe(StepTargetKind.OperationId);
        binding.Operation.ShouldNotBeNull();
        binding.Operation!.Value.Operation.RequestTypeName.ShouldBe("Acme.Pets.ListPetsRequest");
        binding.Operation!.Value.Operation.ResponseTypeName.ShouldBe("Acme.Pets.ListPetsResponse");
    }

    [TestMethod]
    public void Binds_operation_path_step_to_resolved_operation()
    {
        WorkflowOperationBinder binder = CreateBinder();
        StepBinding binding = binder.Bind(this.Step(1));

        binding.Kind.ShouldBe(StepTargetKind.OperationPath);
        binding.Operation!.Value.Operation.RequestTypeName.ShouldBe("Acme.Pets.GetPetRequest");
        binding.Operation!.Value.Operation.RequestParameters[0].PropertyName.ShouldBe("PetId");
    }

    [TestMethod]
    public void Binds_workflow_step_to_sub_workflow_id()
    {
        WorkflowOperationBinder binder = CreateBinder();
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

    private static WorkflowOperationBinder CreateBinder()
    {
        OperationDescriptor[] operations =
        [
            new(
                "/pets",
                OperationMethod.Get,
                "listPets",
                "ListPets",
                "Acme.Pets.ListPetsRequest",
                "Acme.Pets.ListPetsResponse",
                [],
                false),
            new(
                "/pets/{petId}",
                OperationMethod.Get,
                "getPet",
                "GetPet",
                "Acme.Pets.GetPetRequest",
                "Acme.Pets.GetPetResponse",
                [new RequestParameterInfo("petId", ParameterLocation.Path, "PetId", "Acme.Pets.JsonString", true)],
                false),
        ];

        var resolver = OperationResolver.Create("petstore", operations);
        return new WorkflowOperationBinder([new SourceDescriptionClient("petstore", resolver)]);
    }

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