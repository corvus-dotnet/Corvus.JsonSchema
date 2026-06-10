// <copyright file="WorkflowOperationBinderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo11;
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
    private readonly List<ParsedJsonDocument<ArazzoDocument>> extraDocuments = [];

    [TestCleanup]
    public void Cleanup()
    {
        this.document?.Dispose();
        foreach (ParsedJsonDocument<ArazzoDocument> doc in this.extraDocuments)
        {
            doc.Dispose();
        }
    }

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

    [TestMethod]
    public void Source_qualified_operation_id_resolves_against_the_named_source()
    {
        WorkflowOperationBinder binder = CreateMultiSourceBinder();
        StepBinding binding = binder.Bind(this.OperationIdStep("$sourceDescriptions.billing.chargeCard"));

        binding.Kind.ShouldBe(StepTargetKind.OperationId);
        binding.Operation!.Value.SourceName.ShouldBe("billing");
        binding.Operation!.Value.Operation.RequestTypeName.ShouldBe("Acme.Billing.ChargeCardRequest");
    }

    [TestMethod]
    public void Source_qualified_operation_id_disambiguates_an_id_defined_by_two_sources()
    {
        WorkflowOperationBinder binder = CreateMultiSourceBinder();

        // Both sources define listPets; the runtime expression selects billing's.
        StepBinding binding = binder.Bind(this.OperationIdStep("$sourceDescriptions.billing.listPets"));

        binding.Operation!.Value.SourceName.ShouldBe("billing");
        binding.Operation!.Value.Operation.RequestTypeName.ShouldBe("Acme.Billing.ListPetsRequest");
    }

    [TestMethod]
    public void Plain_operation_id_defined_by_two_sources_throws_an_ambiguity_error()
    {
        WorkflowOperationBinder binder = CreateMultiSourceBinder();

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(
            () => binder.Bind(this.OperationIdStep("listPets")));
        ex.Message.ShouldContain("more than one source description");
    }

    [TestMethod]
    public void Plain_operation_id_unique_to_one_source_binds_across_multiple_sources()
    {
        WorkflowOperationBinder binder = CreateMultiSourceBinder();
        StepBinding binding = binder.Bind(this.OperationIdStep("chargeCard"));

        binding.Operation!.Value.SourceName.ShouldBe("billing");
    }

    [TestMethod]
    public void Source_qualified_operation_id_with_unknown_source_throws()
    {
        WorkflowOperationBinder binder = CreateMultiSourceBinder();

        Should.Throw<InvalidOperationException>(
            () => binder.Bind(this.OperationIdStep("$sourceDescriptions.missing.listPets")));
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
                false,
                [],
                "Acme.Pets.PetsClient",
                "ListPetsAsync",
                null),
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

        var resolver = OperationResolver.Create("petstore", operations);
        return new WorkflowOperationBinder([new SourceDescriptionClient("petstore", resolver)]);
    }

    private static WorkflowOperationBinder CreateMultiSourceBinder()
    {
        OperationDescriptor[] petstore =
        [
            new(
                "/pets", OperationMethod.Get, "listPets", "ListPets",
                "Acme.Pets.ListPetsRequest", "Acme.Pets.ListPetsResponse", [], false, [],
                "Acme.Pets.PetsClient", "ListPetsAsync", null),
        ];

        OperationDescriptor[] billing =
        [
            // A deliberately duplicated operationId (listPets) plus one unique to this source.
            new(
                "/pets", OperationMethod.Get, "listPets", "ListPets",
                "Acme.Billing.ListPetsRequest", "Acme.Billing.ListPetsResponse", [], false, [],
                "Acme.Billing.BillingClient", "ListPetsAsync", null),
            new(
                "/charges", OperationMethod.Post, "chargeCard", "ChargeCard",
                "Acme.Billing.ChargeCardRequest", "Acme.Billing.ChargeCardResponse", [], false, [],
                "Acme.Billing.BillingClient", "ChargeCardAsync", null),
        ];

        return new WorkflowOperationBinder(
        [
            new SourceDescriptionClient("petstore", OperationResolver.Create("petstore", petstore)),
            new SourceDescriptionClient("billing", OperationResolver.Create("billing", billing)),
        ]);
    }

    private ArazzoDocument.StepObject OperationIdStep(string operationId)
    {
        // The binder reads the operationId from the live document, so keep it alive for the test.
        string json = $$"""
            {
              "arazzo": "1.0.1",
              "info": { "title": "Test", "version": "1.0.0" },
              "sourceDescriptions": [
                { "name": "petstore", "url": "./petstore.yaml", "type": "openapi" },
                { "name": "billing", "url": "./billing.yaml", "type": "openapi" }
              ],
              "workflows": [
                { "workflowId": "w", "steps": [ { "stepId": "s", "operationId": "{{operationId}}" } ] }
              ]
            }
            """;

        var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(json));
        this.extraDocuments.Add(doc);
        foreach (ArazzoDocument.WorkflowObject workflow in doc.RootElement.Workflows.EnumerateArray())
        {
            foreach (ArazzoDocument.StepObject step in workflow.Steps.EnumerateArray())
            {
                return step;
            }
        }

        throw new InvalidOperationException("No step.");
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