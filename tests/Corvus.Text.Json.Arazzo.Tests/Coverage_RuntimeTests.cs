// <copyright file="Coverage_RuntimeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Branch-coverage tests for the runtime support library — the scalar/typed resolution paths a
/// non-inlined <see cref="CompiledCriterion"/> uses, the <see cref="Comparand"/> operator matrix, the
/// interpolation helpers, and the step-failure exception. These exercise sources and edge cases the
/// end-to-end emitter tests do not reach (the emitter inlines most criteria, so the context-based
/// resolution paths are otherwise only lightly hit).
/// </summary>
[TestClass]
public class Coverage_RuntimeTests
{
    private static ParsedJsonDocument<JsonElement> Parse(string json)
        => ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));

    // ---- WorkflowExecutionContext scalar resolution (TryResolveString) ----

    [TestMethod]
    public void TryResolveString_resolves_url_and_method()
    {
        var context = new WorkflowExecutionContext();
        context.SetRequest("https://api.example/pets/1", "GET");

        context.TryResolveString(ArazzoExpression.Parse("$url"), out string url).ShouldBeTrue();
        url.ShouldBe("https://api.example/pets/1");

        context.TryResolveString(ArazzoExpression.Parse("$method"), out string method).ShouldBeTrue();
        method.ShouldBe("GET");
    }

    [TestMethod]
    public void TryResolveString_url_and_method_unset_return_false()
    {
        var context = new WorkflowExecutionContext();
        context.TryResolveString(ArazzoExpression.Parse("$url"), out string url).ShouldBeFalse();
        url.ShouldBe(string.Empty);
        context.TryResolveString(ArazzoExpression.Parse("$method"), out _).ShouldBeFalse();
    }

    [TestMethod]
    public void TryResolveString_resolves_status_code()
    {
        var context = new WorkflowExecutionContext();
        context.SetResponseStatusCode(404);
        context.TryResolveString(ArazzoExpression.Parse("$statusCode"), out string code).ShouldBeTrue();
        code.ShouldBe("404");
    }

    [TestMethod]
    public void TryResolveString_status_code_unset_returns_false()
    {
        var context = new WorkflowExecutionContext();
        context.TryResolveString(ArazzoExpression.Parse("$statusCode"), out string code).ShouldBeFalse();
        code.ShouldBe(string.Empty);
    }

    [TestMethod]
    public void TryResolveString_resolves_request_and_response_and_message_headers()
    {
        var context = new WorkflowExecutionContext();
        context.SetRequestHeader("X-Req", "req-value");
        context.SetRequestQueryParameter("q", "query-value");
        context.SetRequestPathParameter("petId", "42");
        context.SetResponseHeader("X-Resp", "resp-value");
        context.SetMessageHeader("X-Msg", "msg-value");

        context.TryResolveString(ArazzoExpression.Parse("$request.header.X-Req"), out string h).ShouldBeTrue();
        h.ShouldBe("req-value");
        context.TryResolveString(ArazzoExpression.Parse("$request.query.q"), out string q).ShouldBeTrue();
        q.ShouldBe("query-value");
        context.TryResolveString(ArazzoExpression.Parse("$request.path.petId"), out string p).ShouldBeTrue();
        p.ShouldBe("42");
        context.TryResolveString(ArazzoExpression.Parse("$response.header.X-Resp"), out string r).ShouldBeTrue();
        r.ShouldBe("resp-value");
        context.TryResolveString(ArazzoExpression.Parse("$message.header.X-Msg"), out string m).ShouldBeTrue();
        m.ShouldBe("msg-value");
    }

    [TestMethod]
    public void TryResolveString_missing_header_returns_false()
    {
        var context = new WorkflowExecutionContext();
        context.TryResolveString(ArazzoExpression.Parse("$request.header.Absent"), out string v).ShouldBeFalse();
        v.ShouldBe(string.Empty);
    }

    [TestMethod]
    public void TryResolveString_default_source_non_string_uses_raw_text()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "count": 7 }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(doc.RootElement);

        context.TryResolveString(ArazzoExpression.Parse("$inputs.count"), out string value).ShouldBeTrue();
        value.ShouldBe("7");
    }

    [TestMethod]
    public void TryResolveString_default_source_missing_returns_false()
    {
        var context = new WorkflowExecutionContext();
        context.TryResolveString(ArazzoExpression.Parse("$inputs.nope"), out string value).ShouldBeFalse();
        value.ShouldBe(string.Empty);
    }

    // ---- WorkflowExecutionContext UTF-8 resolution (TryResolveUtf8) ----

    [TestMethod]
    public void TryResolveUtf8_resolves_scalar_and_header_sources()
    {
        var context = new WorkflowExecutionContext();
        context.SetRequest("https://h/x", "POST");
        context.SetRequestHeader("X-Req", "rv");
        context.SetRequestQueryParameter("q", "qv");
        context.SetRequestPathParameter("id", "9");
        context.SetResponseHeader("X-Resp", "sv");
        context.SetMessageHeader("X-Msg", "mv");

        AssertUtf8(context, "$url", "https://h/x");
        AssertUtf8(context, "$method", "POST");
        AssertUtf8(context, "$request.header.X-Req", "rv");
        AssertUtf8(context, "$request.query.q", "qv");
        AssertUtf8(context, "$request.path.id", "9");
        AssertUtf8(context, "$response.header.X-Resp", "sv");
        AssertUtf8(context, "$message.header.X-Msg", "mv");
    }

    [TestMethod]
    public void TryResolveUtf8_status_code_and_missing_and_non_string_return_false()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "n": 1 }""");
        var context = new WorkflowExecutionContext();
        context.SetResponseStatusCode(200);
        context.SetInputs(doc.RootElement);

        ArazzoExpression statusCode = ArazzoExpression.Parse("$statusCode");
        context.TryResolveUtf8(statusCode, out _).ShouldBeFalse();

        ArazzoExpression missingHeader = ArazzoExpression.Parse("$request.header.Absent");
        context.TryResolveUtf8(missingHeader, out _).ShouldBeFalse();

        ArazzoExpression unsetUrl = ArazzoExpression.Parse("$url");
        context.TryResolveUtf8(unsetUrl, out _).ShouldBeFalse();

        ArazzoExpression nonString = ArazzoExpression.Parse("$inputs.n");
        context.TryResolveUtf8(nonString, out _).ShouldBeFalse();
    }

    [TestMethod]
    public void TryResolveUtf8_default_source_string_value()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "s": "hello" }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(doc.RootElement);
        AssertUtf8(context, "$inputs.s", "hello");
    }

    // ---- WorkflowExecutionContext TryGetStatusCode ----

    [TestMethod]
    public void TryGetStatusCode_returns_value_or_false()
    {
        var context = new WorkflowExecutionContext();
        context.TryGetStatusCode(ArazzoExpression.Parse("$statusCode"), out _).ShouldBeFalse();
        context.SetResponseStatusCode(201);
        context.TryGetStatusCode(ArazzoExpression.Parse("$statusCode"), out int code).ShouldBeTrue();
        code.ShouldBe(201);

        // A non-status-code expression never yields a status code.
        context.TryGetStatusCode(ArazzoExpression.Parse("$url"), out _).ShouldBeFalse();
    }

    // ---- WorkflowExecutionContext ResolveComparand ----

    [TestMethod]
    public void ResolveComparand_scalar_sources()
    {
        var context = new WorkflowExecutionContext();
        context.SetRequest("https://u", "PUT");
        context.SetResponseStatusCode(200);
        context.SetRequestHeader("h", "hv");

        context.ResolveComparand(ArazzoExpression.Parse("$url")).ValueEquals(Comparand.FromString("https://u")).ShouldBeTrue();
        context.ResolveComparand(ArazzoExpression.Parse("$method")).ValueEquals(Comparand.FromString("PUT")).ShouldBeTrue();
        context.ResolveComparand(ArazzoExpression.Parse("$statusCode")).ValueEquals(Comparand.FromNumber(200)).ShouldBeTrue();
        context.ResolveComparand(ArazzoExpression.Parse("$request.header.h")).ValueEquals(Comparand.FromString("hv")).ShouldBeTrue();
    }

    [TestMethod]
    public void ResolveComparand_unset_scalars_are_undefined()
    {
        var context = new WorkflowExecutionContext();
        context.ResolveComparand(ArazzoExpression.Parse("$url")).ValueEquals(Comparand.FromString("x")).ShouldBeFalse();
        context.ResolveComparand(ArazzoExpression.Parse("$method")).ValueEquals(Comparand.FromString("x")).ShouldBeFalse();
        context.ResolveComparand(ArazzoExpression.Parse("$statusCode")).ValueEquals(Comparand.FromNumber(1)).ShouldBeFalse();
        context.ResolveComparand(ArazzoExpression.Parse("$request.header.absent")).ValueEquals(Comparand.FromString("x")).ShouldBeFalse();
    }

    [TestMethod]
    public void ResolveComparand_navigation_on_scalar_is_undefined()
    {
        var context = new WorkflowExecutionContext();
        context.SetRequest("https://u", "GET");

        // Navigation pointers only apply to JSON-valued sources; a scalar source yields Undefined.
        Comparand result = context.ResolveComparand(ArazzoExpression.Parse("$url"), "/whatever");
        result.ValueEquals(Comparand.FromString("https://u")).ShouldBeFalse();
    }

    [TestMethod]
    public void ResolveComparand_default_source_with_navigation()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "obj": { "n": 5 } }""");
        var context = new WorkflowExecutionContext();
        context.SetInputs(doc.RootElement);

        context.ResolveComparand(ArazzoExpression.Parse("$inputs.obj"), "/n")
            .ValueEquals(Comparand.FromNumber(5)).ShouldBeTrue();

        // A navigation that does not resolve yields Undefined.
        context.ResolveComparand(ArazzoExpression.Parse("$inputs.obj"), "/missing")
            .ValueEquals(Comparand.FromNumber(5)).ShouldBeFalse();

        // An unresolved source yields Undefined.
        context.ResolveComparand(ArazzoExpression.Parse("$inputs.absent"))
            .ValueEquals(Comparand.FromNumber(5)).ShouldBeFalse();
    }

    // ---- Comparand operator matrix ----

    [TestMethod]
    public void Comparand_FromString_null_is_undefined()
    {
        Comparand.FromString(null).ValueEquals(Comparand.FromString(string.Empty)).ShouldBeFalse();
        Comparand.FromString("a").ValueEquals(Comparand.FromString("A")).ShouldBeTrue(); // case-insensitive
    }

    [TestMethod]
    public void Comparand_numeric_operators()
    {
        Comparand two = Comparand.FromNumber(2);
        Comparand three = Comparand.FromNumber(3);

        two.LessThan(three).ShouldBeTrue();
        three.LessThan(two).ShouldBeFalse();
        two.LessThanOrEqual(Comparand.FromNumber(2)).ShouldBeTrue();
        three.GreaterThan(two).ShouldBeTrue();
        three.GreaterThanOrEqual(Comparand.FromNumber(3)).ShouldBeTrue();

        // Numeric strings coerce.
        Comparand.FromUtf8String("2"u8.ToArray()).LessThan(three).ShouldBeTrue();

        // Non-numeric operands make numeric comparison false.
        Comparand notNumber = Comparand.FromUtf8String("abc"u8.ToArray());
        notNumber.LessThan(three).ShouldBeFalse();
        notNumber.GreaterThan(three).ShouldBeFalse();
        notNumber.LessThanOrEqual(three).ShouldBeFalse();
        notNumber.GreaterThanOrEqual(three).ShouldBeFalse();
    }

    [TestMethod]
    public void Comparand_FromJsonElement_maps_each_kind()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""
            { "s":"x", "n":1, "t":true, "f":false, "z":null, "o":{"a":1}, "arr":[1] }
            """);
        JsonElement root = doc.RootElement;

        root.TryGetProperty("s"u8, out JsonElement s);
        Comparand.FromJsonElement(s).ValueEquals(Comparand.FromString("x")).ShouldBeTrue();
        root.TryGetProperty("n"u8, out JsonElement n);
        Comparand.FromJsonElement(n).ValueEquals(Comparand.FromNumber(1)).ShouldBeTrue();
        root.TryGetProperty("t"u8, out JsonElement t);
        Comparand.FromJsonElement(t).ValueEquals(Comparand.FromBoolean(true)).ShouldBeTrue();
        root.TryGetProperty("f"u8, out JsonElement f);
        Comparand.FromJsonElement(f).ValueEquals(Comparand.FromBoolean(false)).ShouldBeTrue();
        root.TryGetProperty("z"u8, out JsonElement z);
        Comparand.FromJsonElement(z).ValueEquals(Comparand.Null).ShouldBeTrue();

        // Object/array compare as opaque JSON (equal to themselves, not to a scalar).
        root.TryGetProperty("o"u8, out JsonElement o);
        Comparand.FromJsonElement(o).ValueEquals(Comparand.FromJsonElement(o)).ShouldBeTrue();
        root.TryGetProperty("arr"u8, out JsonElement arr);
        Comparand.FromJsonElement(arr).ValueEquals(Comparand.FromJsonElement(o)).ShouldBeFalse();
    }

    [TestMethod]
    public void Comparand_value_not_equals_with_undefined_is_false()
    {
        Comparand.Undefined.ValueNotEquals(Comparand.FromNumber(1)).ShouldBeFalse();
        Comparand.FromNumber(1).ValueNotEquals(Comparand.FromNumber(2)).ShouldBeTrue();
    }

    [TestMethod]
    public void Comparand_long_unicode_strings_use_pooled_buffer()
    {
        // > 128 chars and non-ASCII forces the Unicode (rented-buffer) case-insensitive path.
        string left = new string('Ä', 200);
        string right = new string('ä', 200);
        Comparand.FromString(left).ValueEquals(Comparand.FromString(right)).ShouldBeTrue();
        Comparand.FromString(left).ValueEquals(Comparand.FromString(new string('ö', 200))).ShouldBeFalse();
    }

    // ---- Interpolation helpers ----

    [TestMethod]
    public void Interpolation_AppendUtf8_empty_is_noop()
    {
        var buffer = new ArrayBufferWriter<byte>();
        Interpolation.AppendUtf8(buffer, ReadOnlySpan<byte>.Empty);
        buffer.WrittenCount.ShouldBe(0);

        Interpolation.AppendUtf8(buffer, "hi"u8);
        Encoding.UTF8.GetString(buffer.WrittenSpan).ShouldBe("hi");
    }

    [TestMethod]
    public void Interpolation_AppendValue_handles_undefined_string_and_other()
    {
        using ParsedJsonDocument<JsonElement> doc = Parse("""{ "s":"hi", "n":42 }""");
        JsonElement root = doc.RootElement;

        var buffer = new ArrayBufferWriter<byte>();

        // Undefined contributes nothing.
        Interpolation.AppendValue(buffer, default);
        buffer.WrittenCount.ShouldBe(0);

        // A string is written unescaped (no quotes).
        root.TryGetProperty("s"u8, out JsonElement s);
        Interpolation.AppendValue(buffer, s);
        Encoding.UTF8.GetString(buffer.WrittenSpan).ShouldBe("hi");

        // A non-string is written as JSON.
        var numberBuffer = new ArrayBufferWriter<byte>();
        root.TryGetProperty("n"u8, out JsonElement n);
        Interpolation.AppendValue(numberBuffer, n);
        Encoding.UTF8.GetString(numberBuffer.WrittenSpan).ShouldBe("42");
    }

    // ---- WorkflowStepFailedException constructors ----

    [TestMethod]
    public void WorkflowStepFailedException_constructors()
    {
        new WorkflowStepFailedException().StepId.ShouldBeNull();
        new WorkflowStepFailedException("boom").Message.ShouldBe("boom");

        var inner = new InvalidOperationException("cause");
        var withInner = new WorkflowStepFailedException("outer", inner);
        withInner.Message.ShouldBe("outer");
        withInner.InnerException.ShouldBe(inner);

        var withStep = new WorkflowStepFailedException("stepA", "failed");
        withStep.StepId.ShouldBe("stepA");
        withStep.Message.ShouldBe("failed");
    }

    private static void AssertUtf8(WorkflowExecutionContext context, string expression, string expected)
    {
        ArazzoExpression parsed = ArazzoExpression.Parse(expression);
        context.TryResolveUtf8(parsed, out ResolvedUtf8 value).ShouldBeTrue();
        try
        {
            Encoding.UTF8.GetString(value.Span).ShouldBe(expected);
        }
        finally
        {
            value.Dispose();
        }
    }
}
