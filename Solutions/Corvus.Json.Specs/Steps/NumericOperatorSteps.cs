// <copyright file="NumericOperatorSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using NUnit.Framework;
using TechTalk.SpecFlow;

namespace Steps;

[Binding]
public class NumericOperatorSteps
{
    private const string OperatorResultKey = "OperatorResult";

    private readonly ScenarioContext scenarioContext;

    public NumericOperatorSteps(ScenarioContext scenarioContext)
    {
        this.scenarioContext = scenarioContext;
    }

    [When(@"I apply (.*) to the (.*) with (.*)")]
    public void WhenIApplyTheAddToTheJsonNumberWith(string op, string type, string value)
    {
        switch (type)
        {
            case "JsonNumber":
                this.JsonNumberOperator(op, value);
                break;
            case "JsonInteger":
                this.JsonIntegerOperator(op, value);
                break;
            case "JsonInt128":
                this.JsonInt128Operator(op, value);
                break;
            case "JsonInt64":
                this.JsonInt64Operator(op, value);
                break;
            case "JsonInt32":
                this.JsonInt32Operator(op, value);
                break;
            case "JsonInt16":
                this.JsonInt16Operator(op, value);
                break;
            case "JsonSByte":
                this.JsonSByteOperator(op, value);
                break;
            case "JsonUInt128":
                this.JsonUInt128Operator(op, value);
                break;
            case "JsonUInt64":
                this.JsonUInt64Operator(op, value);
                break;
            case "JsonUInt32":
                this.JsonUInt32Operator(op, value);
                break;
            case "JsonUInt16":
                this.JsonUInt16Operator(op, value);
                break;
            case "JsonByte":
                this.JsonByteOperator(op, value);
                break;
            case "JsonHalf":
                this.JsonHalfOperator(op, value);
                break;
            case "JsonSingle":
                this.JsonSingleOperator(op, value);
                break;
            case "JsonDouble":
                this.JsonDoubleOperator(op, value);
                break;
            case "JsonDecimal":
                this.JsonDecimalOperator(op, value);
                break;
            default:
                throw new InvalidOperationException($"Unexpected type: {type}");
        }
    }

    [Then("the result of the operator is the (.*) (.*)")]
    public void ThenTheJSONShouldBe(string type, string result)
    {
        switch (type)
        {
            case "JsonNumber":
                Assert.AreEqual(JsonNumber.Parse(result), this.scenarioContext.Get<JsonNumber>(OperatorResultKey));
                break;
            case "JsonInteger":
                Assert.AreEqual(JsonInteger.Parse(result), this.scenarioContext.Get<JsonInteger>(OperatorResultKey));
                break;
            case "JsonInt128":
                Assert.AreEqual(JsonInt128.Parse(result), this.scenarioContext.Get<JsonInt128>(OperatorResultKey));
                break;
            case "JsonInt64":
                Assert.AreEqual(JsonInt64.Parse(result), this.scenarioContext.Get<JsonInt64>(OperatorResultKey));
                break;
            case "JsonInt32":
                Assert.AreEqual(JsonInt32.Parse(result), this.scenarioContext.Get<JsonInt32>(OperatorResultKey));
                break;
            case "JsonInt16":
                Assert.AreEqual(JsonInt16.Parse(result), this.scenarioContext.Get<JsonInt16>(OperatorResultKey));
                break;
            case "JsonSByte":
                Assert.AreEqual(JsonSByte.Parse(result), this.scenarioContext.Get<JsonSByte>(OperatorResultKey));
                break;
            case "JsonUInt128":
                Assert.AreEqual(JsonUInt128.Parse(result), this.scenarioContext.Get<JsonUInt128>(OperatorResultKey));
                break;
            case "JsonUInt64":
                Assert.AreEqual(JsonUInt64.Parse(result), this.scenarioContext.Get<JsonUInt64>(OperatorResultKey));
                break;
            case "JsonUInt32":
                Assert.AreEqual(JsonUInt32.Parse(result), this.scenarioContext.Get<JsonUInt32>(OperatorResultKey));
                break;
            case "JsonUInt16":
                Assert.AreEqual(JsonUInt16.Parse(result), this.scenarioContext.Get<JsonUInt16>(OperatorResultKey));
                break;
            case "JsonByte":
                Assert.AreEqual(JsonByte.Parse(result), this.scenarioContext.Get<JsonByte>(OperatorResultKey));
                break;
            case "JsonHalf":
                Assert.AreEqual(JsonHalf.Parse(result), this.scenarioContext.Get<JsonHalf>(OperatorResultKey));
                break;
            case "JsonSingle":
                Assert.AreEqual(JsonSingle.Parse(result), this.scenarioContext.Get<JsonSingle>(OperatorResultKey));
                break;
            case "JsonDouble":
                Assert.AreEqual(JsonDouble.Parse(result), this.scenarioContext.Get<JsonDouble>(OperatorResultKey));
                break;
            case "JsonDecimal":
                Assert.AreEqual(JsonDecimal.Parse(result), this.scenarioContext.Get<JsonDecimal>(OperatorResultKey));
                break;
            default:
                throw new InvalidOperationException($"Unexpected type: {type}");
        }
    }

    private void JsonNumberOperator(string op, string value)
    {
        JsonNumber sut = this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest);
        switch (op)
        {
            case "add":
                this.scenarioContext.Set(sut + JsonNumber.ParseValue(value), OperatorResultKey);
                break;
            case "sub":
                this.scenarioContext.Set(sut - JsonNumber.ParseValue(value), OperatorResultKey);
                break;
            case "mul":
                this.scenarioContext.Set(sut * JsonNumber.ParseValue(value), OperatorResultKey);
                break;
            case "div":
                this.scenarioContext.Set(sut / JsonNumber.ParseValue(value), OperatorResultKey);
                break;
            default:
                throw new InvalidOperationException($"Unexpected operator: {op}");
        }
    }

    private void JsonIntegerOperator(string op, string value)
    {
        JsonInteger sut = this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest);
        switch (op)
        {
            case "add":
                this.scenarioContext.Set(sut + JsonInteger.ParseValue(value), OperatorResultKey);
                break;
            case "sub":
                this.scenarioContext.Set(sut - JsonInteger.ParseValue(value), OperatorResultKey);
                break;
            case "mul":
                this.scenarioContext.Set(sut * JsonInteger.ParseValue(value), OperatorResultKey);
                break;
            case "div":
                this.scenarioContext.Set(sut / JsonInteger.ParseValue(value), OperatorResultKey);
                break;
            default:
                throw new InvalidOperationException($"Unexpected operator: {op}");
        }
    }

    private void JsonInt128Operator(string op, string value)
    {
        JsonInt128 sut = this.scenarioContext.Get<JsonInt128>(JsonValueSteps.SubjectUnderTest);
        switch (op)
        {
            case "add":
                this.scenarioContext.Set(sut + JsonInt128.ParseValue(value), OperatorResultKey);
                break;
            case "sub":
                this.scenarioContext.Set(sut - JsonInt128.ParseValue(value), OperatorResultKey);
                break;
            case "mul":
                this.scenarioContext.Set(sut * JsonInt128.ParseValue(value), OperatorResultKey);
                break;
            case "div":
                this.scenarioContext.Set(sut / JsonInt128.ParseValue(value), OperatorResultKey);
                break;
            default:
                throw new InvalidOperationException($"Unexpected operator: {op}");
        }
    }

    private void JsonInt64Operator(string op, string value)
    {
        JsonInt64 sut = this.scenarioContext.Get<JsonInt64>(JsonValueSteps.SubjectUnderTest);
        switch (op)
        {
            case "add":
                this.scenarioContext.Set(sut + JsonInt64.ParseValue(value), OperatorResultKey);
                break;
            case "sub":
                this.scenarioContext.Set(sut - JsonInt64.ParseValue(value), OperatorResultKey);
                break;
            case "mul":
                this.scenarioContext.Set(sut * JsonInt64.ParseValue(value), OperatorResultKey);
                break;
            case "div":
                this.scenarioContext.Set(sut / JsonInt64.ParseValue(value), OperatorResultKey);
                break;
            default:
                throw new InvalidOperationException($"Unexpected operator: {op}");
        }
    }

    private void JsonInt32Operator(string op, string value)
    {
        JsonInt32 sut = this.scenarioContext.Get<JsonInt32>(JsonValueSteps.SubjectUnderTest);
        switch (op)
        {
            case "add":
                this.scenarioContext.Set(sut + JsonInt32.ParseValue(value), OperatorResultKey);
                break;
            case "sub":
                this.scenarioContext.Set(sut - JsonInt32.ParseValue(value), OperatorResultKey);
                break;
            case "mul":
                this.scenarioContext.Set(sut * JsonInt32.ParseValue(value), OperatorResultKey);
                break;
            case "div":
                this.scenarioContext.Set(sut / JsonInt32.ParseValue(value), OperatorResultKey);
                break;
            default:
                throw new InvalidOperationException($"Unexpected operator: {op}");
        }
    }

    private void JsonInt16Operator(string op, string value)
    {
        JsonInt16 sut = this.scenarioContext.Get<JsonInt16>(JsonValueSteps.SubjectUnderTest);
        switch (op)
        {
            case "add":
                this.scenarioContext.Set(sut + JsonInt16.ParseValue(value), OperatorResultKey);
                break;
            case "sub":
                this.scenarioContext.Set(sut - JsonInt16.ParseValue(value), OperatorResultKey);
                break;
            case "mul":
                this.scenarioContext.Set(sut * JsonInt16.ParseValue(value), OperatorResultKey);
                break;
            case "div":
                this.scenarioContext.Set(sut / JsonInt16.ParseValue(value), OperatorResultKey);
                break;
            default:
                throw new InvalidOperationException($"Unexpected operator: {op}");
        }
    }

    private void JsonSByteOperator(string op, string value)
    {
        JsonSByte sut = this.scenarioContext.Get<JsonSByte>(JsonValueSteps.SubjectUnderTest);
        switch (op)
        {
            case "add":
                this.scenarioContext.Set(sut + JsonSByte.ParseValue(value), OperatorResultKey);
                break;
            case "sub":
                this.scenarioContext.Set(sut - JsonSByte.ParseValue(value), OperatorResultKey);
                break;
            case "mul":
                this.scenarioContext.Set(sut * JsonSByte.ParseValue(value), OperatorResultKey);
                break;
            case "div":
                this.scenarioContext.Set(sut / JsonSByte.ParseValue(value), OperatorResultKey);
                break;
            default:
                throw new InvalidOperationException($"Unexpected operator: {op}");
        }
    }

    private void JsonUInt128Operator(string op, string value)
    {
        JsonUInt128 sut = this.scenarioContext.Get<JsonUInt128>(JsonValueSteps.SubjectUnderTest);
        switch (op)
        {
            case "add":
                this.scenarioContext.Set(sut + JsonUInt128.ParseValue(value), OperatorResultKey);
                break;
            case "sub":
                this.scenarioContext.Set(sut - JsonUInt128.ParseValue(value), OperatorResultKey);
                break;
            case "mul":
                this.scenarioContext.Set(sut * JsonUInt128.ParseValue(value), OperatorResultKey);
                break;
            case "div":
                this.scenarioContext.Set(sut / JsonUInt128.ParseValue(value), OperatorResultKey);
                break;
            default:
                throw new InvalidOperationException($"Unexpected operator: {op}");
        }
    }

    private void JsonUInt64Operator(string op, string value)
    {
        JsonUInt64 sut = this.scenarioContext.Get<JsonUInt64>(JsonValueSteps.SubjectUnderTest);
        switch (op)
        {
            case "add":
                this.scenarioContext.Set(sut + JsonUInt64.ParseValue(value), OperatorResultKey);
                break;
            case "sub":
                this.scenarioContext.Set(sut - JsonUInt64.ParseValue(value), OperatorResultKey);
                break;
            case "mul":
                this.scenarioContext.Set(sut * JsonUInt64.ParseValue(value), OperatorResultKey);
                break;
            case "div":
                this.scenarioContext.Set(sut / JsonUInt64.ParseValue(value), OperatorResultKey);
                break;
            default:
                throw new InvalidOperationException($"Unexpected operator: {op}");
        }
    }

    private void JsonUInt32Operator(string op, string value)
    {
        JsonUInt32 sut = this.scenarioContext.Get<JsonUInt32>(JsonValueSteps.SubjectUnderTest);
        switch (op)
        {
            case "add":
                this.scenarioContext.Set(sut + JsonUInt32.ParseValue(value), OperatorResultKey);
                break;
            case "sub":
                this.scenarioContext.Set(sut - JsonUInt32.ParseValue(value), OperatorResultKey);
                break;
            case "mul":
                this.scenarioContext.Set(sut * JsonUInt32.ParseValue(value), OperatorResultKey);
                break;
            case "div":
                this.scenarioContext.Set(sut / JsonUInt32.ParseValue(value), OperatorResultKey);
                break;
            default:
                throw new InvalidOperationException($"Unexpected operator: {op}");
        }
    }

    private void JsonUInt16Operator(string op, string value)
    {
        JsonUInt16 sut = this.scenarioContext.Get<JsonUInt16>(JsonValueSteps.SubjectUnderTest);
        switch (op)
        {
            case "add":
                this.scenarioContext.Set(sut + JsonUInt16.ParseValue(value), OperatorResultKey);
                break;
            case "sub":
                this.scenarioContext.Set(sut - JsonUInt16.ParseValue(value), OperatorResultKey);
                break;
            case "mul":
                this.scenarioContext.Set(sut * JsonUInt16.ParseValue(value), OperatorResultKey);
                break;
            case "div":
                this.scenarioContext.Set(sut / JsonUInt16.ParseValue(value), OperatorResultKey);
                break;
            default:
                throw new InvalidOperationException($"Unexpected operator: {op}");
        }
    }

    private void JsonByteOperator(string op, string value)
    {
        JsonByte sut = this.scenarioContext.Get<JsonByte>(JsonValueSteps.SubjectUnderTest);
        switch (op)
        {
            case "add":
                this.scenarioContext.Set(sut + JsonByte.ParseValue(value), OperatorResultKey);
                break;
            case "sub":
                this.scenarioContext.Set(sut - JsonByte.ParseValue(value), OperatorResultKey);
                break;
            case "mul":
                this.scenarioContext.Set(sut * JsonByte.ParseValue(value), OperatorResultKey);
                break;
            case "div":
                this.scenarioContext.Set(sut / JsonByte.ParseValue(value), OperatorResultKey);
                break;
            default:
                throw new InvalidOperationException($"Unexpected operator: {op}");
        }
    }

    private void JsonHalfOperator(string op, string value)
    {
        JsonHalf sut = this.scenarioContext.Get<JsonHalf>(JsonValueSteps.SubjectUnderTest);
        switch (op)
        {
            case "add":
                this.scenarioContext.Set(sut + JsonHalf.ParseValue(value), OperatorResultKey);
                break;
            case "sub":
                this.scenarioContext.Set(sut - JsonHalf.ParseValue(value), OperatorResultKey);
                break;
            case "mul":
                this.scenarioContext.Set(sut * JsonHalf.ParseValue(value), OperatorResultKey);
                break;
            case "div":
                this.scenarioContext.Set(sut / JsonHalf.ParseValue(value), OperatorResultKey);
                break;
            default:
                throw new InvalidOperationException($"Unexpected operator: {op}");
        }
    }

    private void JsonSingleOperator(string op, string value)
    {
        JsonSingle sut = this.scenarioContext.Get<JsonSingle>(JsonValueSteps.SubjectUnderTest);
        switch (op)
        {
            case "add":
                this.scenarioContext.Set(sut + JsonSingle.ParseValue(value), OperatorResultKey);
                break;
            case "sub":
                this.scenarioContext.Set(sut - JsonSingle.ParseValue(value), OperatorResultKey);
                break;
            case "mul":
                this.scenarioContext.Set(sut * JsonSingle.ParseValue(value), OperatorResultKey);
                break;
            case "div":
                this.scenarioContext.Set(sut / JsonSingle.ParseValue(value), OperatorResultKey);
                break;
            default:
                throw new InvalidOperationException($"Unexpected operator: {op}");
        }
    }

    private void JsonDoubleOperator(string op, string value)
    {
        JsonDouble sut = this.scenarioContext.Get<JsonDouble>(JsonValueSteps.SubjectUnderTest);
        switch (op)
        {
            case "add":
                this.scenarioContext.Set(sut + JsonDouble.ParseValue(value), OperatorResultKey);
                break;
            case "sub":
                this.scenarioContext.Set(sut - JsonDouble.ParseValue(value), OperatorResultKey);
                break;
            case "mul":
                this.scenarioContext.Set(sut * JsonDouble.ParseValue(value), OperatorResultKey);
                break;
            case "div":
                this.scenarioContext.Set(sut / JsonDouble.ParseValue(value), OperatorResultKey);
                break;
            default:
                throw new InvalidOperationException($"Unexpected operator: {op}");
        }
    }

    private void JsonDecimalOperator(string op, string value)
    {
        JsonDecimal sut = this.scenarioContext.Get<JsonDecimal>(JsonValueSteps.SubjectUnderTest);
        switch (op)
        {
            case "add":
                this.scenarioContext.Set(sut + JsonDecimal.ParseValue(value), OperatorResultKey);
                break;
            case "sub":
                this.scenarioContext.Set(sut - JsonDecimal.ParseValue(value), OperatorResultKey);
                break;
            case "mul":
                this.scenarioContext.Set(sut * JsonDecimal.ParseValue(value), OperatorResultKey);
                break;
            case "div":
                this.scenarioContext.Set(sut / JsonDecimal.ParseValue(value), OperatorResultKey);
                break;
            default:
                throw new InvalidOperationException($"Unexpected operator: {op}");
        }
    }
}