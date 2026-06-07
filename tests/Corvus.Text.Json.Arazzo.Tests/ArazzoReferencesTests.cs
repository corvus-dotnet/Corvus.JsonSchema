// <copyright file="ArazzoReferencesTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo10;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

[TestClass]
public class ArazzoReferencesTests
{
    private const string Document = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "Test", "version": "1.0.0" },
          "sourceDescriptions": [
            { "name": "petstore", "url": "./petstore.yaml", "type": "openapi" }
          ],
          "workflows": [
            {
              "workflowId": "adopt",
              "steps": [
                { "stepId": "list", "operationId": "listPets" },
                { "stepId": "get", "operationId": "getPet" },
                { "stepId": "again", "operationId": "listPets" },
                { "stepId": "byPath", "operationPath": "{$sourceDescriptions.petstore.url}#/paths/~1pets/get" },
                { "stepId": "sub", "workflowId": "register" }
              ]
            },
            {
              "workflowId": "register",
              "steps": [
                { "stepId": "create", "operationId": "createPet" }
              ]
            }
          ]
        }
        """;

    [TestMethod]
    public void Collect_gathers_steps_in_document_order()
    {
        ArazzoReferences references = Collect();

        references.Steps.Count.ShouldBe(6);
        references.Steps[0].WorkflowId.ShouldBe("adopt");
        references.Steps[0].StepId.ShouldBe("list");
        references.Steps[0].Kind.ShouldBe(StepTargetKind.OperationId);
        references.Steps[0].Target.ShouldBe("listPets");

        references.Steps[3].StepId.ShouldBe("byPath");
        references.Steps[3].Kind.ShouldBe(StepTargetKind.OperationPath);

        references.Steps[4].StepId.ShouldBe("sub");
        references.Steps[4].Kind.ShouldBe(StepTargetKind.WorkflowId);
        references.Steps[4].Target.ShouldBe("register");

        references.Steps[5].WorkflowId.ShouldBe("register");
        references.Steps[5].StepId.ShouldBe("create");
        references.Steps[5].Kind.ShouldBe(StepTargetKind.OperationId);
        references.Steps[5].Target.ShouldBe("createPet");
    }

    [TestMethod]
    public void Collect_deduplicates_operation_ids_in_first_seen_order()
    {
        ArazzoReferences references = Collect();

        references.OperationIds.ShouldBe(["listPets", "getPet", "createPet"]);
    }

    [TestMethod]
    public void Collect_gathers_operation_paths()
    {
        ArazzoReferences references = Collect();

        references.OperationPaths.Count.ShouldBe(1);
        references.OperationPaths[0].ShouldContain("#/paths/~1pets/get");
    }

    [TestMethod]
    public void Collect_gathers_sub_workflow_ids()
    {
        ArazzoReferences references = Collect();

        references.WorkflowIds.ShouldBe(["register"]);
    }

    private static ArazzoReferences Collect()
    {
        using var parsed = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(Document));
        return ArazzoReferences.Collect(parsed.RootElement);
    }
}