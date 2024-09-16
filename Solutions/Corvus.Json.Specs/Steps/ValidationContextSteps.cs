// <copyright file="ValidationContextSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using NUnit.Framework;
using TechTalk.SpecFlow;

namespace Steps;

/// <summary>
/// Steps for Json value types.
/// </summary>
[Binding]
public class ValidationContextSteps
{
    private const string ValidationContextKey = "ValidationContext";
    private const string ParentValidationContextKey = "ParentValidationContext";

    private readonly ScenarioContext scenarioContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationContextSteps"/> class.
    /// </summary>
    /// <param name="scenarioContext">The scenario context.</param>
    public ValidationContextSteps(ScenarioContext scenarioContext)
    {
        this.scenarioContext = scenarioContext;
    }

    /// <summary>
    /// Creates a <see cref="ValidationContext"/> with relevant initial conditions and stores it in the context key <see cref="ValidationContextKey"/>.
    /// </summary>
    /// <param name="validInvalid">The string <c>valid</c> or <c>invalid</c>.</param>
    /// <param name="withWithoutResults">The string <c>with</c> or <c>without</c> to determine whether we are <see cref="ValidationContext.UsingResults()"/>.</param>
    /// <param name="withWithoutStack">The string <c>with</c> or <c>without</c> to determine whether we are <see cref="ValidationContext.UsingStack()"/>.</param>
    /// <param name="withWithoutEvaluatedProperties">The string <c>with</c> or <c>without</c> to determine whether we are <see cref="ValidationContext.UsingEvaluatedProperties()"/>.</param>
    /// <param name="withWithoutEvaluatedItems">The string <c>with</c> or <c>without</c> to determine whether we are <see cref="ValidationContext.UsingEvaluatedItems()"/>.</param>
    [Given("a (.*) validation context (.*) results, (.*) a stack, (.*) evaluated properties, and (.*) evaluated items")]
    public void GivenAValidOrInvalidValidationContextWithOrWithoutAStackWithOrWithoutEvaluatedPropertiesAndWithOrWithoutEvaluatedItems(string validInvalid, string withWithoutResults, string withWithoutStack, string withWithoutEvaluatedProperties, string withWithoutEvaluatedItems)
    {
        ValidationContext context = validInvalid == "valid" ? ValidationContext.ValidContext : ValidationContext.InvalidContext;

        if (withWithoutResults == "with")
        {
            context = context.UsingResults();
        }

        if (withWithoutStack == "with")
        {
            context = context.UsingStack();
        }

        if (withWithoutEvaluatedProperties == "with")
        {
            context = context.UsingEvaluatedProperties();
        }

        if (withWithoutEvaluatedItems == "with")
        {
            context = context.UsingEvaluatedItems();
        }

        this.scenarioContext.Set(context, ValidationContextKey);
    }

    /// <summary>
    /// Adds a valid or invalid result to the <see cref="ValidationContext"/> stored in the context key <see cref="ValidationContextKey"/>, with an optional message, and stores the result in the context key <see cref="ValidationContextKey"/>.
    /// </summary>
    /// <param name="validOrInvalidResult">The string <c>valid</c> or <c>invalid</c>.</param>
    /// <param name="message">A message or the string <c>&lt;null&gt;</c> if no message is supplied.</param>
    [When("I add a (.*) result with the message (.*)")]
    public void WhenIAddAValidOrInvalidResultWithTheMessage(string validOrInvalidResult, string message)
    {
        ValidationContext context = this.scenarioContext.Get<ValidationContext>(ValidationContextKey);
        context = context.WithResult(validOrInvalidResult == "valid", message == "<null>" ? null : message);
        this.scenarioContext.Set(context, ValidationContextKey);
    }

    /// <summary>
    /// Gets the <see cref="ValidationContext"/> from the context key <see cref="ValidationContextKey"/> and verifies whether it is valid or invalid.
    /// </summary>
    /// <param name="validOrInvalid">The string <c>valid</c> or <c>invalid</c>.</param>
    [Then("the validationResult should be (.*)")]
    public void ThenTheValidationResultShouldBeValid(string validOrInvalid)
    {
        ValidationContext context = this.scenarioContext.Get<ValidationContext>(ValidationContextKey);
        if (validOrInvalid == "valid")
        {
            Assert.IsTrue(context.IsValid);
        }
        else
        {
            Assert.IsFalse(context.IsValid);
        }
    }

    /// <summary>
    /// Gets the <see cref="ValidationContext"/> stored in the context key <see cref="ValidationContextKey"/>, and uses <see cref="ValidationContext.WithLocalProperty(int)"/> to apply
    /// the propertiess in the given index array.
    /// </summary>
    /// <param name="propertyIndexArray">A string containing the comma separated list of property indices.</param>
    [Given(@"I evaluate the properties at \[(.*)]")]
    [When(@"I evaluate the properties at \[(.*)]")]
    public void WhenIEvaluateThePropertiesAt(string propertyIndexArray)
    {
        ValidationContext context = this.scenarioContext.Get<ValidationContext>(ValidationContextKey);

#if NET8_0_OR_GREATER
        foreach (string propertyIndex in propertyIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
#else
        foreach (string propertyIndex in propertyIndexArray.Split(','))
#endif
        {
            context = context.WithLocalProperty(int.Parse(propertyIndex));
        }

        this.scenarioContext.Set(context, ValidationContextKey);
    }

    /// <summary>
    /// Validates that for the  <see cref="ValidationContext"/> stored in the context key <see cref="ValidationContextKey"/>, <see cref="ValidationContext.HasEvaluatedLocalProperty(int)"/> evaluates to false for the given indices in the array.
    /// </summary>
    /// <param name="propertyIndexArray">A string containing the comma separated list of property indices.</param>
    [Then(@"the properties at \[(.*)] should not be evaluated")]
    [Then(@"the properties at \[(.*)] should not be evaluated locally")]
    public void ThenThePropertiesAtShouldNotBeEvaluated(string propertyIndexArray)
    {
        if (propertyIndexArray == "<none>")
        {
            return;
        }

        ValidationContext context = this.scenarioContext.Get<ValidationContext>(ValidationContextKey);

#if NET8_0_OR_GREATER
        foreach (string propertyIndex in propertyIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
#else
        foreach (string propertyIndex in propertyIndexArray.Split(','))
#endif
        {
            Assert.IsFalse(context.HasEvaluatedLocalProperty(int.Parse(propertyIndex)));
        }
    }

    /// <summary>
    /// Validates that for the  <see cref="ValidationContext"/> stored in the context key <see cref="ValidationContextKey"/>, <see cref="ValidationContext.HasEvaluatedLocalProperty(int)"/> evaluates to false for the given indices in the array.
    /// </summary>
    /// <param name="propertyIndexArray">A string containing the comma separated list of property indices.</param>
    [Then(@"the properties at \[(.*)] should be evaluated")]
    [Then(@"the properties at \[(.*)] should be evaluated locally")]
    public void ThenThePropertiesAtShouldBeEvaluated(string propertyIndexArray)
    {
        if (propertyIndexArray == "<none>")
        {
            return;
        }

        ValidationContext context = this.scenarioContext.Get<ValidationContext>(ValidationContextKey);

#if NET8_0_OR_GREATER
        foreach (string propertyIndex in propertyIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
#else
        foreach (string propertyIndex in propertyIndexArray.Split(','))
#endif
        {
            Assert.IsTrue(context.HasEvaluatedLocalProperty(int.Parse(propertyIndex)));
        }
    }

    /// <summary>
    /// Validates that for the  <see cref="ValidationContext"/> stored in the context key <see cref="ValidationContextKey"/>, <see cref="ValidationContext.HasEvaluatedLocalProperty(int)"/> evaluates to false for the given indices in the array.
    /// </summary>
    /// <param name="propertyIndexArray">A string containing the comma separated list of property indices.</param>
    [Then(@"the properties at \[(.*)] should not be evaluated locally or applied")]
    public void ThenThePropertiesAtShouldNotBeEvaluatedLocallyOrApplied(string propertyIndexArray)
    {
        if (propertyIndexArray == "<none>")
        {
            return;
        }

        ValidationContext context = this.scenarioContext.Get<ValidationContext>(ValidationContextKey);

#if NET8_0_OR_GREATER
        foreach (string propertyIndex in propertyIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
#else
        foreach (string propertyIndex in propertyIndexArray.Split(','))
#endif
        {
            Assert.IsFalse(context.HasEvaluatedLocalOrAppliedProperty(int.Parse(propertyIndex)));
        }
    }

    /// <summary>
    /// Validates that for the  <see cref="ValidationContext"/> stored in the context key <see cref="ValidationContextKey"/>, <see cref="ValidationContext.HasEvaluatedLocalProperty(int)"/> evaluates to false for the given indices in the array.
    /// </summary>
    /// <param name="propertyIndexArray">A string containing the comma separated list of property indices.</param>
    [Then(@"the properties at \[(.*)] should be evaluated locally or applied")]
    public void ThenThePropertiesAtShouldBeEvaluatedLocallyOrApplied(string propertyIndexArray)
    {
        if (propertyIndexArray == "<none>")
        {
            return;
        }

        ValidationContext context = this.scenarioContext.Get<ValidationContext>(ValidationContextKey);

#if NET8_0_OR_GREATER
        foreach (string propertyIndex in propertyIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
#else
        foreach (string propertyIndex in propertyIndexArray.Split(','))
#endif
        {
            Assert.IsTrue(context.HasEvaluatedLocalOrAppliedProperty(int.Parse(propertyIndex)));
        }
    }

    /// <summary>
    /// Gets the <see cref="ValidationContext"/> stored in the context key <see cref="ValidationContextKey"/>, and uses <see cref="ValidationContext.WithLocalItemIndex(int)"/> to apply
    /// the itemss in the given index array.
    /// </summary>
    /// <param name="itemIndexArray">A string containing the comma separated list of item indices.</param>
    [Given(@"I evaluate the items at \[(.*)]")]
    [When(@"I evaluate the items at \[(.*)]")]
    public void WhenIEvaluateTheItemsAt(string itemIndexArray)
    {
        ValidationContext context = this.scenarioContext.Get<ValidationContext>(ValidationContextKey);

#if NET8_0_OR_GREATER
        foreach (string itemIndex in itemIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
#else
        foreach (string itemIndex in itemIndexArray.Split(','))
#endif
        {
            context = context.WithLocalItemIndex(int.Parse(itemIndex));
        }

        this.scenarioContext.Set(context, ValidationContextKey);
    }

    /// <summary>
    /// Validates that for the  <see cref="ValidationContext"/> stored in the context key <see cref="ValidationContextKey"/>, <see cref="ValidationContext.HasEvaluatedLocalItemIndex(int)"/> evaluates to false for the given indices in the array.
    /// </summary>
    /// <param name="itemIndexArray">A string containing the comma separated list of item indices.</param>
    [Then(@"the items at \[(.*)] should not be evaluated")]
    [Then(@"the items at \[(.*)] should not be evaluated locally")]
    public void ThenTheItemsAtShouldNotBeEvaluated(string itemIndexArray)
    {
        if (itemIndexArray == "<none>")
        {
            return;
        }

        ValidationContext context = this.scenarioContext.Get<ValidationContext>(ValidationContextKey);

#if NET8_0_OR_GREATER
        foreach (string itemIndex in itemIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
#else
        foreach (string itemIndex in itemIndexArray.Split(','))
#endif
        {
            Assert.IsFalse(context.HasEvaluatedLocalItemIndex(int.Parse(itemIndex)));
        }
    }

    /// <summary>
    /// Validates that for the  <see cref="ValidationContext"/> stored in the context key <see cref="ValidationContextKey"/>, <see cref="ValidationContext.HasEvaluatedLocalItemIndex(int)"/> evaluates to false for the given indices in the array.
    /// </summary>
    /// <param name="itemIndexArray">A string containing the comma separated list of item indices.</param>
    [Then(@"the items at \[(.*)] should be evaluated")]
    [Then(@"the items at \[(.*)] should be evaluated locally")]
    public void ThenTheItemsAtShouldBeEvaluated(string itemIndexArray)
    {
        if (itemIndexArray == "<none>")
        {
            return;
        }

        ValidationContext context = this.scenarioContext.Get<ValidationContext>(ValidationContextKey);

#if NET8_0_OR_GREATER
        foreach (string itemIndex in itemIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
#else
        foreach (string itemIndex in itemIndexArray.Split(','))
#endif
        {
            Assert.IsTrue(context.HasEvaluatedLocalItemIndex(int.Parse(itemIndex)));
        }
    }

    /// <summary>
    /// Validates that for the  <see cref="ValidationContext"/> stored in the context key <see cref="ValidationContextKey"/>, <see cref="ValidationContext.HasEvaluatedLocalItemIndex(int)"/> evaluates to false for the given indices in the array.
    /// </summary>
    /// <param name="itemIndexArray">A string containing the comma separated list of item indices.</param>
    [Then(@"the items at \[(.*)] should not be evaluated locally or applied")]
    public void ThenTheItemsAtShouldNotBeEvaluatedLocallyOrApplied(string itemIndexArray)
    {
        if (itemIndexArray == "<none>")
        {
            return;
        }

        ValidationContext context = this.scenarioContext.Get<ValidationContext>(ValidationContextKey);

#if NET8_0_OR_GREATER
        foreach (string itemIndex in itemIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
#else
        foreach (string itemIndex in itemIndexArray.Split(','))
#endif
        {
            Assert.IsFalse(context.HasEvaluatedLocalOrAppliedItemIndex(int.Parse(itemIndex)));
        }
    }

    /// <summary>
    /// Validates that for the  <see cref="ValidationContext"/> stored in the context key <see cref="ValidationContextKey"/>, <see cref="ValidationContext.HasEvaluatedLocalItemIndex(int)"/> evaluates to false for the given indices in the array.
    /// </summary>
    /// <param name="itemIndexArray">A string containing the comma separated list of item indices.</param>
    [Then(@"the items at \[(.*)] should be evaluated locally or applied")]
    public void ThenTheItemsAtShouldBeEvaluatedLocallyOrApplied(string itemIndexArray)
    {
        if (itemIndexArray == "<none>")
        {
            return;
        }

        ValidationContext context = this.scenarioContext.Get<ValidationContext>(ValidationContextKey);

#if NET8_0_OR_GREATER
        foreach (string itemIndex in itemIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
#else
        foreach (string itemIndex in itemIndexArray.Split(','))
#endif
        {
            Assert.IsTrue(context.HasEvaluatedLocalOrAppliedItemIndex(int.Parse(itemIndex)));
        }
    }

    /// <summary>
    /// Creates a child <see cref="ValidationContext"/> from that stored in the key <see cref="ValidationContextKey"/>, store the result in the <see cref="ValidationContextKey"/> and stores the original context in <see cref="ParentValidationContextKey"/>.
    /// </summary>
    [When("I create a child context")]
    public void WhenICreateAChildContext()
    {
        ValidationContext parentContext = this.scenarioContext.Get<ValidationContext>(ValidationContextKey);
        ValidationContext childContext = parentContext.CreateChildContext();
        this.scenarioContext.Set(parentContext, ParentValidationContextKey);
        this.scenarioContext.Set(childContext, ValidationContextKey);
    }

    /// <summary>
    /// Get the child <see cref="ValidationContext"/> from the key <see cref="ValidationContextKey"/>, merges it into the context from the <see cref="ParentValidationContextKey"/> and stores the result in the <see cref="ValidationContextKey"/>.
    /// </summary>
    [When("I merge the child context including results")]
    public void WhenIMergeTheChildContextIncludingResults()
    {
        ValidationContext parentContext = this.scenarioContext.Get<ValidationContext>(ParentValidationContextKey);
        ValidationContext childContext = this.scenarioContext.Get<ValidationContext>(ValidationContextKey);
        this.scenarioContext.Set(parentContext.MergeChildContext(childContext, true), ValidationContextKey);
    }

    /// <summary>
    /// Get the child <see cref="ValidationContext"/> from the key <see cref="ValidationContextKey"/>, merges it into the context from the <see cref="ParentValidationContextKey"/> and stores the result in the <see cref="ValidationContextKey"/>.
    /// </summary>
    [When("I merge the child context")]
    public void WhenIMergeTheChildContext()
    {
        ValidationContext parentContext = this.scenarioContext.Get<ValidationContext>(ParentValidationContextKey);
        ValidationContext childContext = this.scenarioContext.Get<ValidationContext>(ValidationContextKey);
        this.scenarioContext.Set(parentContext.MergeChildContext(childContext, false), ValidationContextKey);
    }
}