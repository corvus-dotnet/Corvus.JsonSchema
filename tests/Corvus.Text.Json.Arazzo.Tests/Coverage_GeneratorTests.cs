// <copyright file="Coverage_GeneratorTests.cs" company="Endjin Limited">
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

/// <summary>
/// Branch-coverage tests for the generator's shared helpers and binding error paths — the identifier/
/// literal emitters (<see cref="EmitText"/>), the <c>operationPath</c> pointer parser
/// (<see cref="OperationResolver"/>), and the <see cref="WorkflowOperationBinder"/> resolution failures.
/// These edge cases (escaping, malformed pointers, unresolved targets) are not reached by the
/// happy-path end-to-end emitter tests.
/// </summary>
[TestClass]
public class Coverage_GeneratorTests
{
    // ---- EmitText.Quote ----

    [TestMethod]
    public void Quote_escapes_all_control_and_special_characters()
    {
        string source = "a\\b\"c\nd\re\tf\bg\fh\0ij";
        string quoted = EmitText.Quote(source);

        quoted.ShouldBe("\"a\\\\b\\\"c\\nd\\re\\tf\\bg\\fh\\0i\\u0001j\"");
    }

    [TestMethod]
    public void Quote_plain_text_is_wrapped_unchanged()
    {
        EmitText.Quote("plain").ShouldBe("\"plain\"");
    }

    // ---- EmitText.ToCamelCase ----

    [TestMethod]
    public void ToCamelCase_handles_empty_lower_and_upper()
    {
        EmitText.ToCamelCase(string.Empty).ShouldBe(string.Empty);
        EmitText.ToCamelCase("already").ShouldBe("already");
        EmitText.ToCamelCase("Pascal").ShouldBe("pascal");
    }

    // ---- EmitText.ToPascalCase ----

    [TestMethod]
    public void ToPascalCase_word_boundaries_empty_and_leading_digit()
    {
        EmitText.ToPascalCase("adopt-pet").ShouldBe("AdoptPet");
        EmitText.ToPascalCase("already").ShouldBe("Already");
        EmitText.ToPascalCase("---").ShouldBe("_");           // no alphanumerics
        EmitText.ToPascalCase("123abc").ShouldBe("_123abc");  // leading digit is prefixed
    }

    // ---- EmitText.SanitizeIdentifier / StepOutputsElementLocal ----

    [TestMethod]
    public void SanitizeIdentifier_replaces_invalid_handles_empty_and_leading_digit()
    {
        EmitText.SanitizeIdentifier(string.Empty).ShouldBe("_");
        EmitText.SanitizeIdentifier("a-b.c").ShouldBe("a_b_c");
        EmitText.SanitizeIdentifier("9lives").ShouldBe("_9lives");
    }

    [TestMethod]
    public void StepOutputsElementLocal_sanitizes_and_camel_cases()
    {
        EmitText.StepOutputsElementLocal("my-step").ShouldBe("my_stepOutputsElement");
    }

    // ---- OperationResolver.TryResolveOperationPath failure paths ----

    [TestMethod]
    public void OperationPath_pointer_without_leading_slash_does_not_resolve()
    {
        Resolver().TryResolveOperationPath("spec#fragment", out _).ShouldBeFalse();
    }

    [TestMethod]
    public void OperationPath_pointer_not_under_paths_does_not_resolve()
    {
        Resolver().TryResolveOperationPath("#/components/x/get", out _).ShouldBeFalse();
    }

    [TestMethod]
    public void OperationPath_pointer_without_method_segment_does_not_resolve()
    {
        Resolver().TryResolveOperationPath("#/paths/onlyonetoken", out _).ShouldBeFalse();
    }

    [TestMethod]
    public void OperationPath_pointer_with_unknown_method_does_not_resolve()
    {
        Resolver().TryResolveOperationPath("#/paths/~1pets/teleport", out _).ShouldBeFalse();
    }

    // ---- WorkflowOperationBinder error paths ----

    [TestMethod]
    public void OperationPath_named_source_that_does_not_resolve_throws()
    {
        WorkflowOperationBinder binder = Binder();
        Should.Throw<InvalidOperationException>(() => binder.Bind(Step("""
            { "stepId": "x", "operationPath": "{$sourceDescriptions.petstore.url}#/paths/~1nope/get" }
            """)));
    }

    [TestMethod]
    public void OperationPath_without_source_marker_falls_back_and_throws_when_unresolved()
    {
        WorkflowOperationBinder binder = Binder();
        Should.Throw<InvalidOperationException>(() => binder.Bind(Step("""
            { "stepId": "x", "operationPath": "#/paths/~1nope/get" }
            """)));
    }

    [TestMethod]
    public void OperationPath_unknown_source_marker_falls_back_to_any_client()
    {
        WorkflowOperationBinder binder = Binder();

        // The named source 'other' is unknown, so the binder falls back to trying every client; the
        // pointer resolves against 'petstore'.
        StepBinding binding = binder.Bind(Step("""
            { "stepId": "x", "operationPath": "{$sourceDescriptions.other.url}#/paths/~1pets/get" }
            """));

        binding.Kind.ShouldBe(StepTargetKind.OperationPath);
        binding.Operation!.Value.Operation.OperationId.ShouldBe("listPets");
    }

    [TestMethod]
    public void Channel_path_with_no_channel_source_throws()
    {
        WorkflowOperationBinder binder = Binder();
        Should.Throw<InvalidOperationException>(() => binder.Bind(Step("""
            { "stepId": "x", "channelPath": "events", "action": "send" }
            """)));
    }

    [TestMethod]
    public void Step_with_no_target_binds_to_none()
    {
        WorkflowOperationBinder binder = Binder();
        StepBinding binding = binder.Bind(Step("""{ "stepId": "x" }"""));
        binding.Kind.ShouldBe(StepTargetKind.None);
    }

    private static OperationDescriptor[] Operations() =>
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
    ];

    private static OperationResolver Resolver() => OperationResolver.Create("petstore", Operations());

    private static WorkflowOperationBinder Binder()
        => new([new SourceDescriptionClient("petstore", Resolver())]);

    private static ArazzoDocument.StepObject Step(string stepJson)
    {
        // Wrap the step in a minimal document so the typed StepObject reads its strings from a live doc.
        string doc = $$"""
            {
              "arazzo": "1.1.0",
              "info": { "title": "t", "version": "1.0.0" },
              "workflows": [ { "workflowId": "w", "steps": [ {{stepJson}} ] } ]
            }
            """;
        ParsedJsonDocument<ArazzoDocument> parsed = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(doc));
        Steps.Add(parsed);
        return parsed.RootElement.Workflows.EnumerateArray().First().Steps.EnumerateArray().First();
    }

    // Keep parsed documents alive for the duration of the test class (the typed StepObject reads from them).
    private static readonly List<IDisposable> Steps = [];

    [ClassCleanup]
    public static void Cleanup()
    {
        foreach (IDisposable d in Steps)
        {
            d.Dispose();
        }

        Steps.Clear();
    }
}
