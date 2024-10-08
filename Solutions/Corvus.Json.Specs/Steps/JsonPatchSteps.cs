// <copyright file="JsonPatchSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Corvus.Json.Patch;
using Corvus.Json.Patch.Model;
using NUnit.Framework;
using TechTalk.SpecFlow;

namespace Steps;

/// <summary>
/// Steps for URI template validation.
/// </summary>
[Binding]
public class JsonPatchSteps
{
    private const string DocumentKey = "Document";
    private const string PatchKey = "Patch";
    private const string ResultKey = "Result";
    private const string OutputKey = "Output";
    private const string ExceptionKey = "Exception";
    private const string BuilderKey = "Builder";
    private readonly ScenarioContext scenarioContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPatchSteps"/> class.
    /// </summary>
    /// <param name="scenarioContext">The scenario context.</param>
    public JsonPatchSteps(ScenarioContext scenarioContext)
    {
        this.scenarioContext = scenarioContext;
    }

    /// <summary>
    /// Parses the <paramref name="jsonString"/> to a <see cref="JsonAny"/> and stores it in the context variable <see cref="DocumentKey"/>.
    /// </summary>
    /// <param name="jsonString">The json document to parse as a string.</param>
    [Given("the document (.*)")]
    public void GivenTheDocument(string jsonString)
    {
        this.scenarioContext.Set(JsonAny.Parse(jsonString), DocumentKey);
    }

    /// <summary>
    /// Parses the <paramref name="jsonString"/> to a <see cref="JsonAny"/> and stores it in the context variable <see cref="PatchKey"/>.
    /// </summary>
    /// <param name="jsonString">The json document to parse as a string.</param>
    [Given("the patch (.*)")]
    public void GivenThePatch(string jsonString)
    {
        this.scenarioContext.Set(JsonAny.Parse(jsonString).As<JsonPatchDocument>(), PatchKey);
    }

    [When("I deep patch the JSON value (.*) at the path (.*)")]
    public void WhenIDeepPatchTheJSONValueAtThePath(string serializedValue, string path)
    {
        try
        {
            var value = JsonAny.ParseValue(serializedValue.AsSpan());
            JsonAny document = this.scenarioContext.Get<JsonAny>(DocumentKey);
            PatchBuilder builder = document.BeginPatch().DeepAddOrReplaceObjectProperties(value, path.AsSpan());
            this.scenarioContext.Set(builder, BuilderKey);
            this.scenarioContext.Set(builder.PatchOperations, PatchKey);
            this.scenarioContext.Set(builder.Value, OutputKey);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(ex, ExceptionKey);
        }
    }

    [When("I try to set a deep property on the JSON value (.*) at the path (.*)")]
    public void WhenITryToSetADeepPropertyOnTheJSONValueAtThePath(string serializedValue, string path)
    {
        try
        {
            // We force it to an object and back so we catch the type mismatch problem seen in #204
            var value = JsonAny.ParseValue(serializedValue.AsSpan());
            JsonObject document = this.scenarioContext.Get<JsonAny>(DocumentKey).AsObject;
            if (document.TrySetDeepProperty(path.AsSpan(), value, out JsonObject result))
            {
                this.scenarioContext.Set(result.AsAny, OutputKey);
            }
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(ex, ExceptionKey);
        }
    }

    /// <summary>
    /// Parses the <paramref name="jsonString"/> and uses it to create a <see cref="PatchBuilder"/> for those operations. The result
    /// is stored in the context key <see cref="BuilderKey"/>. If the build fails, the exception is stored in the <see cref="ExceptionKey"/>.
    /// </summary>
    /// <param name="jsonString">The json string containing the patch.</param>
    [When("I build the patch (.*)")]
    public void WhenIBuildThePatch(string jsonString)
    {
        try
        {
            var patchOperationArray = JsonPatchDocument.Parse(jsonString);
            this.scenarioContext.Set(patchOperationArray, PatchKey);

            JsonAny document = this.scenarioContext.Get<JsonAny>(DocumentKey);
            PatchBuilder builder = document.BeginPatch();

            foreach (JsonPatchDocument.PatchOperation operation in patchOperationArray.EnumerateArray())
            {
                string op = (string)operation.Op;
                switch (op)
                {
                    case "add":
                        JsonPatchDocument.AddOperation add = operation.AsAddOperation;

                        if (!add.IsValid())
                        {
                            throw new JsonPatchException("Invalid add operation.");
                        }

                        builder = builder.Add(add.Value, operation.Path);
                        break;
                    case "copy":
                        JsonPatchDocument.CopyOperation copy = operation.AsCopyOperation;

                        if (!copy.IsValid())
                        {
                            throw new JsonPatchException("Invalid copy operation.");
                        }

                        builder = builder.Copy(copy.From, operation.Path);
                        break;
                    case "move":
                        JsonPatchDocument.MoveOperation move = operation.AsMoveOperation;

                        if (!move.IsValid())
                        {
                            throw new JsonPatchException("Invalid move operation.");
                        }

                        builder = builder.Move(move.From, operation.Path);
                        break;
                    case "remove":
                        builder = builder.Remove(operation.Path);
                        break;
                    case "replace":
                        JsonPatchDocument.ReplaceOperation replace = operation.AsReplaceOperation;

                        if (!replace.IsValid())
                        {
                            throw new JsonPatchException("Invalid replace operation.");
                        }

                        builder = builder.Replace(operation.Path, replace.Value);
                        break;
                    case "test":
                        JsonPatchDocument.TestOperation test = operation.AsTestOperation;

                        if (!test.IsValid())
                        {
                            throw new JsonPatchException("Invalid test operation.");
                        }

                        builder = builder.Test(operation.Path, test.Value);
                        break;
                    default:
                        throw new JsonPatchException("Unrecognized operation.");
                }
            }

            this.scenarioContext.Set(builder, BuilderKey);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(ex, ExceptionKey);
        }
    }

    /// <summary>
    /// Applies the <see cref="JsonPatchDocument"/> in the context at <see cref="PatchKey"/> to the <see cref="JsonAny"/> in the context at <see cref="DocumentKey"/>
    /// and stores the results in the context in <see cref="ResultKey"/> and <see cref="OutputKey"/>.
    /// </summary>
    [When("I apply the patch to the document")]
    public void WhenIApplyThePatchToTheDocument()
    {
        JsonPatchDocument patch = this.scenarioContext.Get<JsonPatchDocument>(PatchKey);
        JsonAny doc = this.scenarioContext.Get<JsonAny>(DocumentKey);

        if (!patch.IsValid())
        {
            this.scenarioContext.Set(false, ResultKey);
            this.scenarioContext.Set(doc, OutputKey);
            return;
        }

        bool result = doc.TryApplyPatch(patch, out JsonAny output);
        this.scenarioContext.Set(result, ResultKey);
        this.scenarioContext.Set(output, OutputKey);
    }

    /// <summary>
    /// Gets the result from the <see cref="BuilderKey"/>, then compares the resulting value with the <see cref="JsonAny"/> represented by the <paramref name="jsonString"/>.
    /// </summary>
    /// <param name="jsonString">The expected value.</param>
    [Then("the patch result should equal (.*)")]
    public void ThenThePatchResultShouldEqual(string jsonString)
    {
        Assert.IsTrue(this.scenarioContext.ContainsKey(BuilderKey), "No result was set.");
        PatchBuilder builder = this.scenarioContext.Get<PatchBuilder>(BuilderKey);
        Assert.AreEqual(JsonAny.Parse(jsonString), builder.Value);
    }

    /// <summary>
    /// Compare the transformed document from the <see cref="OutputKey"/> with the <see cref="JsonAny"/> represented
    /// by the <paramref name="jsonString"/>.
    /// </summary>
    /// <param name="jsonString">The document with which to compare the output.</param>
    [Then("the transformed document should equal (.*)")]
    public void ThenTheTransformedDocumentShouldEqual(string jsonString)
    {
        Assert.AreEqual(JsonAny.Parse(jsonString), this.scenarioContext.Get<JsonAny>(OutputKey));
    }

    /// <summary>
    /// Validate the <see cref="ResultKey"/> indicates that the document was not transformed.
    /// </summary>
    [Then("the document should not be transformed.")]
    public void ThenTheDocumentShouldNotBeTransformed()
    {
        Assert.IsFalse(this.scenarioContext.Get<bool>(ResultKey));
    }

    /// <summary>
    /// Validate the <see cref="ResultKey"/> indicates that the document was not transformed.
    /// </summary>
    [Then("a patch exception should be thrown")]
    public void APatchExceptionShouldBeThrown()
    {
        Assert.IsInstanceOf<JsonPatchException>(this.scenarioContext.Get<Exception>(ExceptionKey));
    }
}