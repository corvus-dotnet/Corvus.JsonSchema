// <copyright file="JsonPatchSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Steps
{
    using Corvus.Json;
    using Corvus.Json.Patch;
    using NUnit.Framework;
    using TechTalk.SpecFlow;

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
        private readonly ScenarioContext scenarioContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="UriTemplateSteps"/> class.
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
            this.scenarioContext.Set(JsonAny.Parse(jsonString).As<PatchOperationArray>(), PatchKey);
        }

        /// <summary>
        /// Applies the <see cref="PatchOperationArray"/> in the context at <see cref="PatchKey"/> to the <see cref="JsonAny"/> in the context at <see cref="DocumentKey"/>
        /// and stores the results in the context in <see cref="ResultKey"/> and <see cref="OutputKey"/>.
        /// </summary>
        [When("I apply the patch to the document")]
        public void WhenIApplyThePatchToTheDocument()
        {
            bool result = this.scenarioContext.Get<JsonAny>(DocumentKey).TryApplyPatch(this.scenarioContext.Get<PatchOperationArray>(PatchKey), out JsonAny output);
            this.scenarioContext.Set(result, ResultKey);
            this.scenarioContext.Set(output, OutputKey);
        }

        [Then("the transformed document should equal (.*)")]
        public void ThenTheTransformedDocumentShouldEqual(string jsonString)
        {
            Assert.AreEqual(JsonAny.Parse(jsonString), this.scenarioContext.Get<JsonAny>(OutputKey));
        }

        [Then("the document should not be transformed.")]
        public void ThenTheDocumentShouldNotBeTransformed()
        {
            Assert.IsFalse(this.scenarioContext.Get<bool>(ResultKey));
        }
    }
}
