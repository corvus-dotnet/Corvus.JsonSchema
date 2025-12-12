// <copyright file="NamespaceMappingSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Frozen;
using Corvus.Json;
using Corvus.Json.CodeGeneration.CSharp;
using NUnit.Framework;
using TechTalk.SpecFlow;
using TechTalk.SpecFlow.Assist;

namespace Corvus.Specs.Steps;

/// <summary>
/// Step definitions for namespace mapping tests.
/// </summary>
[Binding]
public sealed class NamespaceMappingSteps
{
    private readonly ScenarioContext scenarioContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="NamespaceMappingSteps"/> class.
    /// </summary>
    /// <param name="scenarioContext">The scenario context.</param>
    public NamespaceMappingSteps(ScenarioContext scenarioContext)
    {
        this.scenarioContext = scenarioContext;
    }

    /// <summary>
    /// Sets up a namespace map with a single entry.
    /// </summary>
    /// <param name="baseUri">The base URI.</param>
    /// <param name="ns">The namespace.</param>
    [Given(@"I have a namespace map with ""(.*)"" mapped to ""(.*)""")]
    public void GivenIHaveANamespaceMapWith(string baseUri, string ns)
    {
        var namespaceMap = new Dictionary<string, string>
        {
            { baseUri, ns },
        }.ToFrozenDictionary();

        this.scenarioContext.Add("NamespaceMap", namespaceMap);
    }

    /// <summary>
    /// Sets up a namespace map with multiple entries from a table.
    /// </summary>
    /// <param name="table">The table of base URI to namespace mappings.</param>
    [Given(@"I have a namespace map with the following entries")]
    public void GivenIHaveANamespaceMapWithTheFollowingEntries(Table table)
    {
        var entries = table.CreateSet<NamespaceEntry>();
        var namespaceMap = entries.ToFrozenDictionary(e => e.BaseUri, e => e.Namespace);

        this.scenarioContext.Add("NamespaceMap", namespaceMap);
    }

    /// <summary>
    /// Tries to get the namespace for a given schema URI.
    /// </summary>
    /// <param name="schemaUri">The schema URI.</param>
    [When(@"I try to get the namespace for ""(.*)""")]
    public void WhenITryToGetTheNamespaceFor(string schemaUri)
    {
        var namespaceMap = this.scenarioContext.Get<FrozenDictionary<string, string>>("NamespaceMap");
        var jsonReference = new JsonReference(schemaUri);

        bool found = CSharpLanguageProvider.Options.TryGetNamespace(jsonReference, namespaceMap, out string? ns);

        this.scenarioContext.Add("Found", found);
        this.scenarioContext.Add("Namespace", ns);
    }

    /// <summary>
    /// Tries to get the namespace for a relative URI.
    /// </summary>
    /// <param name="relativeUri">The relative URI.</param>
    [When(@"I try to get the namespace for a relative URI ""(.*)""")]
    public void WhenITryToGetTheNamespaceForARelativeUri(string relativeUri)
    {
        var namespaceMap = this.scenarioContext.Get<FrozenDictionary<string, string>>("NamespaceMap");
        var jsonReference = new JsonReference(relativeUri);

        bool found = CSharpLanguageProvider.Options.TryGetNamespace(jsonReference, namespaceMap, out string? ns);

        this.scenarioContext.Add("Found", found);
        this.scenarioContext.Add("Namespace", ns);
    }

    /// <summary>
    /// Verifies the namespace result.
    /// </summary>
    /// <param name="expectedNamespace">The expected namespace.</param>
    [Then(@"the namespace should be ""(.*)""")]
    public void ThenTheNamespaceShouldBe(string expectedNamespace)
    {
        string? actualNamespace = this.scenarioContext.Get<string?>("Namespace");

        // When no namespace is expected (empty string in feature file), we expect null from the method
        if (string.IsNullOrEmpty(expectedNamespace))
        {
            Assert.IsNull(actualNamespace);
        }
        else
        {
            Assert.AreEqual(expectedNamespace, actualNamespace);
        }
    }

    /// <summary>
    /// Verifies whether the namespace was found.
    /// </summary>
    /// <param name="expected">Whether the namespace should be found.</param>
    [Then(@"the namespace lookup result should be (true|false)")]
    public void ThenTheNamespaceLookupResultShouldBe(bool expected)
    {
        bool actual = this.scenarioContext.Get<bool>("Found");
        Assert.AreEqual(expected, actual);
    }

    private sealed class NamespaceEntry
    {
        public string BaseUri { get; set; } = string.Empty;

        public string Namespace { get; set; } = string.Empty;
    }
}