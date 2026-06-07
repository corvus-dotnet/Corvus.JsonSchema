// <copyright file="ControlFlowTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

[TestClass]
public class ControlFlowTests
{
    [TestMethod]
    public void Empty_criteria_set_always_matches()
    {
        var context = new WorkflowExecutionContext();
        context.SetResponseStatusCode(200);

        CriteriaSet.Empty.AllMatch(context).ShouldBeTrue();
        new CriteriaSet(null).AllMatch(context).ShouldBeTrue();
    }

    [TestMethod]
    public void Criteria_set_is_and()
    {
        var context = new WorkflowExecutionContext();
        context.SetResponseStatusCode(200);

        var pass = new CriteriaSet([
            CompiledCriterion.Compile(CriterionType.Simple, "$statusCode == 200"),
            CompiledCriterion.Compile(CriterionType.Simple, "$statusCode != 500"),
        ]);
        var oneFails = new CriteriaSet([
            CompiledCriterion.Compile(CriterionType.Simple, "$statusCode == 200"),
            CompiledCriterion.Compile(CriterionType.Simple, "$statusCode == 500"),
        ]);

        pass.AllMatch(context).ShouldBeTrue();
        oneFails.AllMatch(context).ShouldBeFalse();
    }

    [TestMethod]
    public void Action_matches_when_its_criteria_match()
    {
        var context = new WorkflowExecutionContext();
        context.SetResponseStatusCode(201);

        var action = new CompiledAction(
            "created",
            WorkflowActionType.Goto,
            new CriteriaSet([CompiledCriterion.Compile(CriterionType.Simple, "$statusCode == 201")]),
            stepId: "next");

        action.Matches(context).ShouldBeTrue();
        action.Type.ShouldBe(WorkflowActionType.Goto);
        action.StepId.ShouldBe("next");
    }

    [TestMethod]
    public void Select_first_match_returns_first_satisfied_action_in_order()
    {
        var context = new WorkflowExecutionContext();
        context.SetResponseStatusCode(429);

        CompiledAction[] failureActions =
        [
            new("on500", WorkflowActionType.End, new CriteriaSet([Simple("$statusCode == 500")])),
            new("on429", WorkflowActionType.Retry, new CriteriaSet([Simple("$statusCode == 429")]), retryAfter: 2, retryLimit: 3),
            new("catchAll", WorkflowActionType.End, CriteriaSet.Empty),
        ];

        CompiledAction? selected = CompiledAction.SelectFirstMatch(failureActions, context);

        selected.ShouldNotBeNull();
        selected!.Name.ShouldBe("on429");
        selected.Type.ShouldBe(WorkflowActionType.Retry);
        selected.RetryAfter.ShouldBe(2);
        selected.RetryLimit.ShouldBe(3);
    }

    [TestMethod]
    public void Select_first_match_falls_through_to_unconditional_action()
    {
        var context = new WorkflowExecutionContext();
        context.SetResponseStatusCode(200);

        CompiledAction[] actions =
        [
            new("on500", WorkflowActionType.End, new CriteriaSet([Simple("$statusCode == 500")])),
            new("default", WorkflowActionType.End, CriteriaSet.Empty),
        ];

        CompiledAction.SelectFirstMatch(actions, context)!.Name.ShouldBe("default");
    }

    [TestMethod]
    public void Select_first_match_returns_null_when_none_match()
    {
        var context = new WorkflowExecutionContext();
        context.SetResponseStatusCode(200);

        CompiledAction[] actions = [new("on500", WorkflowActionType.End, new CriteriaSet([Simple("$statusCode == 500")]))];

        CompiledAction.SelectFirstMatch(actions, context).ShouldBeNull();
    }

    private static CompiledCriterion Simple(string condition)
        => CompiledCriterion.Compile(CriterionType.Simple, condition);
}