// <copyright file="WorkflowInterpolationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

[TestClass]
public class WorkflowInterpolationTests
{
    [TestMethod]
    public void Interpolate_literal_only()
    {
        var context = new WorkflowExecutionContext();

        context.TryInterpolate("Bearer abc", out string result).ShouldBeTrue();
        result.ShouldBe("Bearer abc");
    }

    [TestMethod]
    public void Interpolate_embedded_input_string()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "host": "example.com" }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(doc.RootElement);

        context.TryInterpolate("https://{$inputs.host}/api", out string result).ShouldBeTrue();
        result.ShouldBe("https://example.com/api");
    }

    [TestMethod]
    public void Interpolate_embedded_step_output_token()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "token": "xyz" }""");
        var context = new WorkflowExecutionContext();
        context.SetStepOutputs("login", doc.RootElement);

        context.TryInterpolate("Authorization: Bearer {$steps.login.outputs.token}", out string result).ShouldBeTrue();
        result.ShouldBe("Authorization: Bearer xyz");
    }

    [TestMethod]
    public void Interpolate_embedded_status_code()
    {
        var context = new WorkflowExecutionContext();
        context.SetResponseStatusCode(200);

        context.TryInterpolate("code={$statusCode}", out string result).ShouldBeTrue();
        result.ShouldBe("code=200");
    }

    [TestMethod]
    public void Interpolate_embedded_response_header()
    {
        var context = new WorkflowExecutionContext();
        context.SetResponseHeader("X-Request-Id", "req-7");

        context.TryInterpolate("id={$response.header.X-Request-Id}", out string result).ShouldBeTrue();
        result.ShouldBe("id=req-7");
    }

    [TestMethod]
    public void Interpolate_embedded_number_is_unquoted()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "count": 5 }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        context.TryInterpolate("n={$response.body#/count}", out string result).ShouldBeTrue();
        result.ShouldBe("n=5");
    }

    [TestMethod]
    public void Interpolate_embedded_object_is_json()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "obj": { "a": 1 } }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseBody(doc.RootElement);

        context.TryInterpolate("{$response.body#/obj}", out string result).ShouldBeTrue();
        result.ShouldBe("""{"a":1}""");
    }

    [TestMethod]
    public void Interpolate_multiple_expressions()
    {
        using ParsedJsonDocument<JsonElement> inputs = Parse("""{ "host": "h" }""");
        using ParsedJsonDocument<JsonElement> stepOutputs = Parse("""{ "id": "42" }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(inputs.RootElement);
        context.SetStepOutputs("create", stepOutputs.RootElement);

        context.TryInterpolate("https://{$inputs.host}/api/{$steps.create.outputs.id}", out string result).ShouldBeTrue();
        result.ShouldBe("https://h/api/42");
    }

    [TestMethod]
    public void Interpolate_unresolved_expression_returns_false()
    {
        var context = new WorkflowExecutionContext();

        context.TryInterpolate("Bearer {$steps.login.outputs.token}", out string result).ShouldBeFalse();
        result.ShouldBe(string.Empty);
    }

    [TestMethod]
    public void Interpolate_unmatched_open_brace_is_literal()
    {
        var context = new WorkflowExecutionContext();

        context.TryInterpolate("a { b", out string result).ShouldBeTrue();
        result.ShouldBe("a { b");
    }

    [TestMethod]
    public void Interpolate_compiled_template()
    {
        using ParsedJsonDocument<JsonElement> inputs = Parse("""{ "host": "h" }""");
        using ParsedJsonDocument<JsonElement> stepOutputs = Parse("""{ "id": "42" }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(inputs.RootElement);
        context.SetStepOutputs("create", stepOutputs.RootElement);

        CompiledInterpolationTemplate template = CompiledInterpolationTemplate.Compile("https://{$inputs.host}/api/{$steps.create.outputs.id}");
        var output = new ArrayBufferWriter<byte>();
        context.TryInterpolate(template, output).ShouldBeTrue();

        Encoding.UTF8.GetString(output.WrittenSpan).ShouldBe("https://h/api/42");
    }

    [TestMethod]
    public void Interpolate_compiled_template_unresolved_returns_false()
    {
        var context = new WorkflowExecutionContext();
        CompiledInterpolationTemplate template = CompiledInterpolationTemplate.Compile("Bearer {$steps.login.outputs.token}");
        var output = new ArrayBufferWriter<byte>();

        context.TryInterpolate(template, output).ShouldBeFalse();
    }

    [TestMethod]
    public void Interpolate_to_buffer_writer()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "host": "example.com" }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(doc.RootElement);

        var output = new ArrayBufferWriter<byte>();
        context.TryInterpolate("https://{$inputs.host}/api", output).ShouldBeTrue();

        Encoding.UTF8.GetString(output.WrittenSpan).ShouldBe("https://example.com/api");
    }

    private static ParsedJsonDocument<JsonElement> Parse(string json)
        => ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));
}