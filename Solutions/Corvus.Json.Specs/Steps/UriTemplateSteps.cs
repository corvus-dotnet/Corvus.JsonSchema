// <copyright file="UriTemplateSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Json;
using Corvus.Json.UriTemplates;
using NUnit.Framework;
using TechTalk.SpecFlow;

namespace Steps;

/// <summary>
/// Steps for URI template validation.
/// </summary>
[Binding]
public class UriTemplateSteps
{
    private const string TemplateKey = "Template";
    private const string TargetUriKey = "TargetUri";
    private const string ParametersKey = "Parameters";
    private const string RegexKey = "Regex";
    private const string ExceptionKey = "Exception";
    private const string VariablesKey = "Variables";
    private const string ResultKey = "Result";
    private readonly ScenarioContext scenarioContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UriTemplateSteps"/> class.
    /// </summary>
    /// <param name="scenarioContext">The scenario context.</param>
    public UriTemplateSteps(ScenarioContext scenarioContext)
    {
        this.scenarioContext = scenarioContext;
    }

    /// <summary>
    /// Set a parameter on the URI template stored in the context variable <c>Template</c>, and writes it back to the context.
    /// </summary>
    /// <param name="name">The parmaeter name.</param>
    /// <param name="value">The new value.</param>
    [When(@"I set the template parameter called ""(.*)"" to the integer ""(.*)""")]
    public void WhenISetTheTemplateParameterCalledToTheInteger(string name, int value)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<UriTemplate>(TemplateKey).SetParameter(name, value), TemplateKey);
    }

    /// <summary>
    /// Set a parameter on the URI template stored in the context variable <c>Template</c>, and writes it back to the context.
    /// </summary>
    /// <param name="name">The parmaeter name.</param>
    /// <param name="value">The new value.</param>
    [When(@"I set the template parameter called ""(.*)"" to the double ""(.*)""")]
    public void WhenISetTheTemplateParameterCalledToTheDouble(string name, double value)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<UriTemplate>(TemplateKey).SetParameter(name, value), TemplateKey);
    }

    /// <summary>
    /// Set a parameter on the URI template stored in the context variable <c>Template</c>, and writes it back to the context.
    /// </summary>
    /// <param name="name">The parmaeter name.</param>
    /// <param name="value">The new value.</param>
    [When(@"I set the template parameter called ""(.*)"" to the bool ""(.*)""")]
    public void WhenISetTheTemplateParameterCalledToTheBool(string name, bool value)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<UriTemplate>(TemplateKey).SetParameter(name, value), TemplateKey);
    }

    /// <summary>
    /// Set a parameter on the URI template stored in the context variable <c>Template</c>, and writes it back to the context.
    /// </summary>
    /// <param name="name">The parmaeter name.</param>
    /// <param name="value">The new value.</param>
    [When(@"I set the template parameter called ""(.*)"" to the string ""(.*)""")]
    public void WhenISetTheTemplateParameterCalledToTheString(string name, string value)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<UriTemplate>(TemplateKey).SetParameter(name, value), TemplateKey);
    }

    /// <summary>
    /// Set a parameter on the URI template stored in the context variable <c>Template</c>, and writes it back to the context.
    /// </summary>
    /// <param name="name">The parmaeter name.</param>
    /// <param name="table">The new value.</param>
    [When(@"I set the template parameter called ""(.*)"" to the dictionary of strings")]
    public void WhenISetTheTemplateParameterCalledToTheDictionaryOfStrings(string name, Table table)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<UriTemplate>(TemplateKey).SetParameter(name, table.Rows.ToDictionary(r => r["key"], r => r["value"])), TemplateKey);
    }

    /// <summary>
    /// Set a parameter on the URI template stored in the context variable <c>Template</c>, and writes it back to the context.
    /// </summary>
    /// <param name="name">The parmaeter name.</param>
    /// <param name="table">The new value.</param>
    [When(@"I set the template parameter called ""(.*)"" to the enumerable of strings")]
    public void WhenISetTheTemplateParameterCalledToTheArrayOfStrings(string name, Table table)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<UriTemplate>(TemplateKey).SetParameter(name, table.Rows.Select(r => r["value"])), TemplateKey);
    }

    /// <summary>
    /// Set a parameter on the URI template stored in the context variable <c>Template</c>, and writes it back to the context.
    /// </summary>
    /// <param name="name">The parmaeter name.</param>
    /// <param name="value">The new value.</param>
    [When(@"I set the template parameter called ""(.*)"" to the float ""(.*)""")]
    public void WhenISetTheTemplateParameterCalledToTheFloat(string name, float value)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<UriTemplate>(TemplateKey).SetParameter(name, value), TemplateKey);
    }

    /// <summary>
    /// Set a parameter on the URI template stored in the context variable <c>Template</c>, and writes it back to the context.
    /// </summary>
    /// <param name="name">The parmaeter name.</param>
    /// <param name="value">The new value.</param>
    [When(@"I set the template parameter called ""(.*)"" to the long ""(.*)""")]
    public void WhenISetTheTemplateParameterCalledToTheLong(string name, long value)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<UriTemplate>(TemplateKey).SetParameter(name, value), TemplateKey);
    }

    /// <summary>
    /// Set the template parameters from a JsonObject on the UriTemplate in the context variable called <c>Template</c> and store it back to the context.
    /// </summary>
    /// <param name="json">The json string for the JSON object.</param>
    [When("I set the template parameters from the JsonObject '(.*)'")]
    public void WhenISetTheTemplateParametersFromTheJsonObject(string json)
    {
        JsonObject jsonObject = JsonAny.Parse(json);
        this.scenarioContext.Set(this.scenarioContext.Get<UriTemplate>(TemplateKey).SetParameters(jsonObject), TemplateKey);
    }

    /// <summary>
    /// Make a template for the URI in the context variable <c>TargetUri</c> using the given parameters and stores it in the context variable <c>Template</c>.
    /// </summary>
    /// <param name="table">The parameters to apply.</param>
    [When("I make a template for the target uri with the parameters")]
    public void WhenIMakeATemplateForTheTargetUriWithTheParameters(Table table)
    {
        ImmutableDictionary<string, JsonAny>.Builder parameters = ImmutableDictionary.CreateBuilder<string, JsonAny>();
        foreach (TableRow row in table.Rows)
        {
            parameters.Add(row["name"], JsonAny.Parse(row["value"]));
        }

        this.scenarioContext.Set(this.scenarioContext.Get<Uri>(TargetUriKey).MakeTemplate(parameters.ToImmutable()), TemplateKey);
    }

    /// <summary>
    /// Make a template for the URI in the context variable <c>TargetUri</c> using the given parameters and stores it in the context variable <c>Template</c>.
    /// </summary>
    /// <param name="table">The parameters to apply.</param>
    [When("I make a template for the target uri with the parameters as params")]
    public void WhenIMakeATemplateForTheTargetUriWithTheParametersAsParams(Table table)
    {
        var parameters = new (string, JsonAny)[table.Rows.Count];
        int index = 0;
        foreach (TableRow row in table.Rows)
        {
            parameters[index] = (row["name"], JsonAny.Parse(row["value"]));
            index++;
        }

        this.scenarioContext.Set(this.scenarioContext.Get<Uri>(TargetUriKey).MakeTemplate(parameters), TemplateKey);
    }

    /// <summary>
    /// Set the template parameters from the table on the UriTemplate in the context variable called <c>Template</c> and store it back to the context.
    /// </summary>
    /// <param name="table">The table of parameters to set.</param>
    [When("I set the template parameters")]
    public void WhenISetTheTemplateParameters(Table table)
    {
        var parameters = new (string, JsonAny)[table.RowCount];
        int index = 0;
        foreach (TableRow row in table.Rows)
        {
            parameters[index] = (row["name"], JsonAny.Parse(row["value"]));
            ++index;
        }

        this.scenarioContext.Set(this.scenarioContext.Get<UriTemplate>(TemplateKey).SetParameters(parameters), TemplateKey);
    }

    /// <summary>
    /// Sets the target uri into a context variable called <c>TargetUri</c>.
    /// </summary>
    /// <param name="targetUri">The target uri.</param>
    [Given(@"the target uri ""(.*)""")]
    public void GivenTheTargetUri(string targetUri)
    {
        this.scenarioContext.Set(new Uri(targetUri, UriKind.RelativeOrAbsolute), TargetUriKey);
    }

    /// <summary>
    /// Sets the query string parameters for the target URI in the context variable <c>TargetUri</c> into a context variable called <c>Parameters</c>.
    /// </summary>
    [When("I get the query string parameters for the target uri")]
    public void WhenIGetTheQueryStringParametersForTheTargetUri()
    {
        this.scenarioContext.Set(this.scenarioContext.Get<Uri>(TargetUriKey).GetQueryStringParameters(), ParametersKey);
    }

    /// <summary>
    /// Sets the named parameter in the context variable called <c>Parameters</c> to the given value.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value of the parameter, in JSON format.</param>
    [When(@"I set the parameter called ""(.*)"" to ""(.*)""")]
    public void WhenISetTheParameterCalledTo(string name, string value)
    {
        ImmutableDictionary<string, JsonAny> parameters = this.scenarioContext.Get<ImmutableDictionary<string, JsonAny>>(ParametersKey);
        this.scenarioContext.Set(parameters.SetItem(name, JsonAny.Parse(value)), ParametersKey);
    }

    /// <summary>
    /// Makes a URI template for the target uri in the context variable <c>TargetUri</c> using the parameters in the context variable <c>Parameters</c> and sets it into a context variable called <c>Template</c>.
    /// </summary>
    [When("I make a template for the target uri from the parameters")]
    public void WhenIMakeATemplateForTheTargetUriFromTheParameters()
    {
        this.scenarioContext.Set(
            this.scenarioContext.Get<Uri>(TargetUriKey).MakeTemplate(this.scenarioContext.Get<ImmutableDictionary<string, JsonAny>>(ParametersKey)),
            TemplateKey);
    }

    /// <summary>
    /// Matches the resolved template in the context variable <c>Template</c> with the expected value.
    /// </summary>
    /// <param name="table">The table of possible values to match.</param>
    [Then("the resolved template should be one of")]
    public void ThenTheResovledTemplateShouldBeOneOf(Table table)
    {
        string resolved = this.scenarioContext.Get<UriTemplate>(TemplateKey).Resolve();
        foreach (TableRow row in table.Rows)
        {
            if (row[0] == resolved)
            {
                return;
            }
        }

        Assert.Fail($"Saw {resolved} but expected one of\r\n{table.ToString()}");
    }

    /// <summary>
    /// Makes a template for the target URI in the context variable <c>TargetUri</c> and stores it in the context variable <c>Template</c>.
    /// </summary>
    [Given("I make a template for the target uri")]
    public void GivenIMakeATemplateForTheTargetUri()
    {
        this.scenarioContext.Set(this.scenarioContext.Get<Uri>(TargetUriKey).MakeTemplate(), TemplateKey);
    }

    /// <summary>
    /// Set a parameter on the URI template stored in the context variable <c>Template</c>, and writes it back to the context.
    /// </summary>
    /// <param name="name">The parmaeter name.</param>
    /// <param name="value">The new value, in JSON form.</param>
    [When(@"I set the template parameter called ""(.*)"" to ""(.*)""")]
    public void WhenISetTheTemplateParameterCalledTo(string name, string value)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<UriTemplate>(TemplateKey).SetParameter(name, JsonAny.Parse(value)), TemplateKey);
    }

    /// <summary>
    /// Removes a parameter from the URI template stored in the context variable <c>Template</c>, and writes it back to the context.
    /// </summary>
    /// <param name="name">The parmaeter name.</param>
    [When(@"I clear the template parameter called ""(.*)""")]
    public void WhenIClearTheTemplateParameterCalled(string name)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<UriTemplate>(TemplateKey).ClearParameter(name), TemplateKey);
    }

    /// <summary>
    /// Creates a regex for the given template expression and stores it in the context variable "Regex".
    /// </summary>
    /// <param name="regex">The regex expression.</param>
    [When(@"I create a regex for the template ""(.*)""")]
    public void GivenICreateARegexForTheTemplate(string regex)
    {
        this.scenarioContext.Set(new Regex(UriTemplate.CreateMatchingRegex(regex)), RegexKey);
    }

    /// <summary>
    /// Matches the regex stored in the context variable "Regex" with the given URI.
    /// </summary>
    /// <param name="uri">The URI to match.</param>
    [Then(@"the regex should match ""(.*)""")]
    public void ThenTheRegexShouldMatch(string uri)
    {
        Assert.IsTrue(this.scenarioContext.Get<Regex>(RegexKey).IsMatch(uri));
    }

    /// <summary>
    /// Matches the regex stored in the context variable "Regex" with the given URI and validates the matches.
    /// </summary>
    /// <param name="uri">The uri to match.</param>
    /// <param name="table">The table of matches.</param>
    [Then(@"the matches for ""(.*)"" should be")]
    public void ThenTheMatchesForShouldBe(string uri, Table table)
    {
        Match match = this.scenarioContext.Get<Regex>(RegexKey).Match(uri);
        foreach (TableRow row in table.Rows)
        {
            Assert.AreEqual(row["match"], match.Groups[row["group"]].Value);
        }
    }

    /// <summary>
    /// Creates a URI template for the given template expression and stores it in the context variable "Template".
    /// </summary>
    /// <param name="uriTemplate">The template expression.</param>
    [Given(@"I create a UriTemplate for ""(.*)""")]
    [When(@"I create a UriTemplate for ""(.*)""")]
    public void GivenICreateAUriTemplateFor(string uriTemplate)
    {
        this.scenarioContext.Set(new UriTemplate(uriTemplate), TemplateKey);
    }

    /// <summary>
    /// Creates a URI template with partial resoltion for the given template expression and stores it in the context variable <c>Template</c>.
    /// </summary>
    /// <param name="uriTemplate">The template expression.</param>
    [Given(@"I create a UriTemplate for ""(.*)"" with partial resolution")]
    public void GivenICreateAUriTemplateForWithPartialResolution(string uriTemplate)
    {
        this.scenarioContext.Set(new UriTemplate(uriTemplate, resolvePartially: true), TemplateKey);
    }

    /// <summary>
    /// Validates the parameters for the URI template stored in the context variable "Template" when resolved against the given URI.
    /// </summary>
    /// <param name="uri">The uri from which to extract the parameters.</param>
    /// <param name="parameters">The parameters to match.</param>
    [Then(@"the parameters for ""(.*)"" should be")]
    public void ThenTheParametersForShouldBe(string uri, Table parameters)
    {
        if (this.scenarioContext.Get<UriTemplate>(TemplateKey).TryGetParameters(new Uri(uri, UriKind.RelativeOrAbsolute), out ImmutableDictionary<string, JsonAny>? actual))
        {
            Assert.AreEqual(parameters.RowCount, actual.Count);
            foreach (TableRow row in parameters.Rows)
            {
                Assert.AreEqual(JsonAny.Parse(row["value"]), actual[row["name"]]);
            }
        }
        else
        {
            Assert.Fail("Failed to get parameters.");
        }
    }

    /// <summary>
    /// Set the level for the scenario in the context variable "Level".
    /// </summary>
    /// <param name="level">The level to set.</param>
    [Given("the Level is (.*)")]
    public void GivenTheLevelIs(int level)
    {
        this.scenarioContext.Set(level, "Level");
    }

    /// <summary>
    /// Build the variables into an <see cref="ImmutableDictionary{TKey, TValue}"/> of <see cref="string"/> to <see cref="JsonAny"/> and set the context variable "Variables".
    /// </summary>
    /// <param name="table">The table of variables.</param>
    [Given("the variables")]
    public void GivenTheVariables(Table table)
    {
        ImmutableDictionary<string, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<string, JsonAny>();

        foreach (TableRow row in table.Rows)
        {
            builder.Add(row["name"], JsonAny.Parse(row["value"]));
        }

        this.scenarioContext.Set(builder.ToImmutable(), VariablesKey);
    }

    /// <summary>
    /// Apply the variables to the template and store the result in the context variable "Result".
    /// </summary>
    /// <param name="template">The URI template.</param>
    [When("I apply the variables to the template (.*)")]
    public void WhenIApplyTheVariablesToTheTemplateVar(string template)
    {
        try
        {
            var uriTemplate = new UriTemplate(template, parameters: this.scenarioContext.Get<ImmutableDictionary<string, JsonAny>>(VariablesKey));
            this.scenarioContext.Set(uriTemplate.Resolve(), ResultKey);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(ex, ExceptionKey);
        }
    }

    /// <summary>
    /// Ensure that the result is one of the expected options.
    /// </summary>
    /// <param name="result">The result array.</param>
    [Then("the result should be one of (.*)")]
    public void ThenTheResultShouldBeOneOf(string result)
    {
        JsonArray array = JsonAny.Parse(result);

        // This is the weird way it expects an exception in the test schema
        if (array.GetArrayLength() == 1)
        {
            using JsonArrayEnumerator enumerator = array.EnumerateArray();
            enumerator.MoveNext();
            if (enumerator.Current.ValueKind == JsonValueKind.False)
            {
                Assert.IsTrue(this.scenarioContext.ContainsKey(ExceptionKey));
                return;
            }
        }

        Assert.False(this.scenarioContext.ContainsKey(ExceptionKey));
        string actual = this.scenarioContext.Get<string>(ResultKey);
        foreach (string expected in array.EnumerateArray())
        {
            if (expected == actual)
            {
                return;
            }
        }

        Assert.Fail($"{result} did not contain {actual}");
    }
}