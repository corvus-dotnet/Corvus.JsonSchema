// <copyright file="WorkflowExecutionContextTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

[TestClass]
public class WorkflowExecutionContextTests
{
    [TestMethod]
    public void Resolve_input_by_name()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "username": "alice" }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(doc.RootElement);

        bool resolved = context.TryResolveValue(ArazzoExpression.Parse("$inputs.username"), out JsonElement value);

        resolved.ShouldBeTrue();
        value.GetString().ShouldBe("alice");
    }

    [TestMethod]
    public void Resolve_input_with_json_pointer()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "user": { "profile": { "email": "a@b.com" } } }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(doc.RootElement);

        bool resolved = context.TryResolveValue(ArazzoExpression.Parse("$inputs.user#/profile/email"), out JsonElement value);

        resolved.ShouldBeTrue();
        value.GetString().ShouldBe("a@b.com");
    }

    [TestMethod]
    public void Resolve_missing_input_returns_false()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "username": "alice" }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(doc.RootElement);

        context.TryResolveValue(ArazzoExpression.Parse("$inputs.missing"), out _).ShouldBeFalse();
    }

    [TestMethod]
    public void Resolve_step_output()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "token": "xyz" }""");
        var context = new WorkflowExecutionContext();
        context.SetStepOutputs("loginStep", doc.RootElement);

        bool resolved = context.TryResolveValue(ArazzoExpression.Parse("$steps.loginStep.outputs.token"), out JsonElement value);

        resolved.ShouldBeTrue();
        value.GetString().ShouldBe("xyz");
    }

    [TestMethod]
    public void Resolve_step_output_for_unknown_step_returns_false()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "token": "xyz" }""");
        var context = new WorkflowExecutionContext();
        context.SetStepOutputs("loginStep", doc.RootElement);

        context.TryResolveValue(ArazzoExpression.Parse("$steps.otherStep.outputs.token"), out _).ShouldBeFalse();
    }

    [TestMethod]
    public void Resolve_response_body_whole_value()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "accessToken": "T" }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        bool resolved = context.TryResolveValue(ArazzoExpression.Parse("$response.body"), out JsonElement value);

        resolved.ShouldBeTrue();
        value.TryGetProperty("accessToken", out JsonElement token).ShouldBeTrue();
        token.GetString().ShouldBe("T");
    }

    [TestMethod]
    public void Resolve_response_body_with_pointer_into_array()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "data": { "items": [10, 20, 30] } }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        bool resolved = context.TryResolveValue(ArazzoExpression.Parse("$response.body#/data/items/1"), out JsonElement value);

        resolved.ShouldBeTrue();
        value.GetInt32().ShouldBe(20);
    }

    [TestMethod]
    public void Resolve_request_body_with_pointer()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "user": { "id": 42 } }""");
        var context = new WorkflowExecutionContext();
        context.SetRequestBody(doc.RootElement);

        bool resolved = context.TryResolveValue(ArazzoExpression.Parse("$request.body#/user/id"), out JsonElement value);

        resolved.ShouldBeTrue();
        value.GetInt32().ShouldBe(42);
    }

    [TestMethod]
    public void Resolve_message_payload_with_pointer()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "orderId": "o-1" }""");
        var context = new WorkflowExecutionContext();
        context.SetMessagePayload(doc.RootElement);

        bool resolved = context.TryResolveValue(ArazzoExpression.Parse("$message.payload#/orderId"), out JsonElement value);

        resolved.ShouldBeTrue();
        value.GetString().ShouldBe("o-1");
    }

    [TestMethod]
    public void Resolve_response_body_when_not_set_returns_false()
    {
        var context = new WorkflowExecutionContext();

        context.TryResolveValue(ArazzoExpression.Parse("$response.body#/x"), out _).ShouldBeFalse();
    }

    [TestMethod]
    public void Resolve_pointer_to_missing_member_returns_false()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "a": 1 }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        context.TryResolveValue(ArazzoExpression.Parse("$response.body#/missing"), out _).ShouldBeFalse();
    }

    [TestMethod]
    public void Resolve_scalar_source_returns_false()
    {
        var context = new WorkflowExecutionContext();

        // $url is a scalar source not handled by the JSON-valued resolver.
        context.TryResolveValue(ArazzoExpression.Parse("$url"), out _).ShouldBeFalse();
    }

    private static ParsedJsonDocument<JsonElement> Parse(string json)
        => ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));
}