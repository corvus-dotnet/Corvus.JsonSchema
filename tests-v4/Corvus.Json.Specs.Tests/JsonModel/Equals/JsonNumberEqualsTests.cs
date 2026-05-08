// <copyright file="JsonNumberEqualsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.JsonModel.Equals;

/// <summary>
/// Tests for JsonNumberEquals.
/// </summary>
[TestClass]
public class JsonNumberEqualsTests
{
    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1__1_true_JsonNumber()
    {
        var sut = JsonNumber.ParseValue("1".AsSpan());
        var other = JsonNumber.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1_1__1_1_true_JsonNumber()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        var other = JsonNumber.Parse("1.1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1_1__1_false_JsonNumber()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        var other = JsonNumber.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1_1__3_false_JsonNumber()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        var other = JsonNumber.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number_null_null_true_JsonNumber()
    {
        var sut = JsonNumber.ParseValue("null".AsSpan());
        var other = JsonNumber.ParseValue("null".AsSpan());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number_null__1_1_false_JsonNumber()
    {
        var sut = JsonNumber.ParseValue("null".AsSpan());
        var other = JsonNumber.Parse("1.1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1__1_true_JsonDouble()
    {
        var sut = JsonDouble.ParseValue("1".AsSpan());
        var other = JsonDouble.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1_1__1_1_true_JsonDouble()
    {
        var sut = JsonDouble.ParseValue("1.1".AsSpan());
        var other = JsonDouble.Parse("1.1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1_1__1_false_JsonDouble()
    {
        var sut = JsonDouble.ParseValue("1.1".AsSpan());
        var other = JsonDouble.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1_1__3_false_JsonDouble()
    {
        var sut = JsonDouble.ParseValue("1.1".AsSpan());
        var other = JsonDouble.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number_null_null_true_JsonDouble()
    {
        var sut = JsonDouble.ParseValue("null".AsSpan());
        var other = JsonDouble.ParseValue("null".AsSpan());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number_null__1_1_false_JsonDouble()
    {
        var sut = JsonDouble.ParseValue("null".AsSpan());
        var other = JsonDouble.Parse("1.1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1__1_true_JsonSingle()
    {
        var sut = JsonSingle.ParseValue("1".AsSpan());
        var other = JsonSingle.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1_1__1_1_true_JsonSingle()
    {
        var sut = JsonSingle.ParseValue("1.1".AsSpan());
        var other = JsonSingle.Parse("1.1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1_1__1_false_JsonSingle()
    {
        var sut = JsonSingle.ParseValue("1.1".AsSpan());
        var other = JsonSingle.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1_1__3_false_JsonSingle()
    {
        var sut = JsonSingle.ParseValue("1.1".AsSpan());
        var other = JsonSingle.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number_null_null_true_JsonSingle()
    {
        var sut = JsonSingle.ParseValue("null".AsSpan());
        var other = JsonSingle.ParseValue("null".AsSpan());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number_null__1_1_false_JsonSingle()
    {
        var sut = JsonSingle.ParseValue("null".AsSpan());
        var other = JsonSingle.Parse("1.1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1__1_true_JsonDecimal()
    {
        var sut = JsonDecimal.ParseValue("1".AsSpan());
        var other = JsonDecimal.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1_1__1_1_true_JsonDecimal()
    {
        var sut = JsonDecimal.ParseValue("1.1".AsSpan());
        var other = JsonDecimal.Parse("1.1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1_1__1_false_JsonDecimal()
    {
        var sut = JsonDecimal.ParseValue("1.1".AsSpan());
        var other = JsonDecimal.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1_1__3_false_JsonDecimal()
    {
        var sut = JsonDecimal.ParseValue("1.1".AsSpan());
        var other = JsonDecimal.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number_null_null_true_JsonDecimal()
    {
        var sut = JsonDecimal.ParseValue("null".AsSpan());
        var other = JsonDecimal.ParseValue("null".AsSpan());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number_null__1_1_false_JsonDecimal()
    {
        var sut = JsonDecimal.ParseValue("null".AsSpan());
        var other = JsonDecimal.Parse("1.1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1__1_true_JsonHalf()
    {
        var sut = JsonHalf.ParseValue("1".AsSpan());
        var other = JsonHalf.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1_1__1_1_true_JsonHalf()
    {
        var sut = JsonHalf.ParseValue("1.1".AsSpan());
        var other = JsonHalf.Parse("1.1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1_1__1_false_JsonHalf()
    {
        var sut = JsonHalf.ParseValue("1.1".AsSpan());
        var other = JsonHalf.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number__1_1__3_false_JsonHalf()
    {
        var sut = JsonHalf.ParseValue("1.1".AsSpan());
        var other = JsonHalf.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number_null_null_true_JsonHalf()
    {
        var sut = JsonHalf.ParseValue("null".AsSpan());
        var other = JsonHalf.ParseValue("null".AsSpan());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_number_null__1_1_false_JsonHalf()
    {
        var sut = JsonHalf.ParseValue("null".AsSpan());
        var other = JsonHalf.Parse("1.1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1__1_true_JsonNumber()
    {
        var sut = JsonNumber.Parse("1").AsDotnetBackedValue();
        var other = JsonNumber.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1_1__1_1_true_JsonNumber()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        var other = JsonNumber.Parse("1.1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1_1__1_false_JsonNumber()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        var other = JsonNumber.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1__3_false_JsonNumber()
    {
        var sut = JsonNumber.Parse("1").AsDotnetBackedValue();
        var other = JsonNumber.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1__1_true_JsonDouble()
    {
        var sut = JsonDouble.Parse("1").AsDotnetBackedValue();
        var other = JsonDouble.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1_1__1_1_true_JsonDouble()
    {
        var sut = JsonDouble.Parse("1.1").AsDotnetBackedValue();
        var other = JsonDouble.Parse("1.1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1_1__1_false_JsonDouble()
    {
        var sut = JsonDouble.Parse("1.1").AsDotnetBackedValue();
        var other = JsonDouble.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1__3_false_JsonDouble()
    {
        var sut = JsonDouble.Parse("1").AsDotnetBackedValue();
        var other = JsonDouble.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1__1_true_JsonSingle()
    {
        var sut = JsonSingle.Parse("1").AsDotnetBackedValue();
        var other = JsonSingle.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1_1__1_1_true_JsonSingle()
    {
        var sut = JsonSingle.Parse("1.1").AsDotnetBackedValue();
        var other = JsonSingle.Parse("1.1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1_1__1_false_JsonSingle()
    {
        var sut = JsonSingle.Parse("1.1").AsDotnetBackedValue();
        var other = JsonSingle.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1__3_false_JsonSingle()
    {
        var sut = JsonSingle.Parse("1").AsDotnetBackedValue();
        var other = JsonSingle.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1__1_true_JsonDecimal()
    {
        var sut = JsonDecimal.Parse("1").AsDotnetBackedValue();
        var other = JsonDecimal.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1_1__1_1_true_JsonDecimal()
    {
        var sut = JsonDecimal.Parse("1.1").AsDotnetBackedValue();
        var other = JsonDecimal.Parse("1.1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1_1__1_false_JsonDecimal()
    {
        var sut = JsonDecimal.Parse("1.1").AsDotnetBackedValue();
        var other = JsonDecimal.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1__3_false_JsonDecimal()
    {
        var sut = JsonDecimal.Parse("1").AsDotnetBackedValue();
        var other = JsonDecimal.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1__1_true_JsonHalf()
    {
        var sut = JsonHalf.Parse("1").AsDotnetBackedValue();
        var other = JsonHalf.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1_1__1_1_true_JsonHalf()
    {
        var sut = JsonHalf.Parse("1.1").AsDotnetBackedValue();
        var other = JsonHalf.Parse("1.1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1_1__1_false_JsonHalf()
    {
        var sut = JsonHalf.Parse("1.1").AsDotnetBackedValue();
        var other = JsonHalf.Parse("1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number__1_1__3_false_JsonHalf()
    {
        var sut = JsonHalf.Parse("1.1").AsDotnetBackedValue();
        var other = JsonHalf.Parse("3");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number_null_null_true_JsonHalf()
    {
        var sut = JsonHalf.Null;
        var other = JsonHalf.ParseValue("null".AsSpan());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_number_null__1_1_false_JsonHalf()
    {
        var sut = JsonHalf.Null;
        var other = JsonHalf.Parse("1.1");
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_IJsonValue__1_1_Hello_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_IJsonValue__1_1_Goodbye_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_IJsonValue__1_1__1_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_IJsonValue__1_1__1_1_true()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_IJsonValue__1_1__1_2_3_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_IJsonValue__1_1_first_1_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_IJsonValue__1_1_true_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_IJsonValue__1_1_false_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_IJsonValue__1_1__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_IJsonValue__1_1__2018_11_13_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_IJsonValue__1_1_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_IJsonValue__1_1__2018_11_13_false_2()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_IJsonValue__1_1_hello_endjin_com_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_IJsonValue__1_1_www_example_com_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_IJsonValue__1_1_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_IJsonValue__1_1_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_IJsonValue__1_1_first_1_false_2()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_IJsonValue__1_1__192_168_0_1_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_IJsonValue__1_1__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_IJsonValue__1_1_Hello_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_IJsonValue__1_1_Goodbye_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_IJsonValue__1_1__1_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_IJsonValue__1_1__1_1_true()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_IJsonValue__1_1__1_2_3_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_IJsonValue__1_1_first_1_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_IJsonValue__1_1_true_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_IJsonValue__1_1_false_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_IJsonValue__1_1__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_IJsonValue__1_1_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_IJsonValue__1_1__2018_11_13_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_IJsonValue__1_1_P3Y6M4DT12H30M5S_false_2()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_IJsonValue__1_1_hello_endjin_com_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_IJsonValue__1_1_www_example_com_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_IJsonValue__1_1_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_IJsonValue__1_1_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_IJsonValue__1_1_first_1_false_2()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_IJsonValue__1_1__192_168_0_1_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_IJsonValue__1_1__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1_Hello_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"Hello\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1_Goodbye_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"Goodbye\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1__1_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1__1_1_true()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("1.1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1__1_2_3_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("[1,2,3]");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1_first_1_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("{ \"first\": \"1\" }");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1_true_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1_false_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"2018-11-13T20:20:39+00:00\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"P3Y6M4DT12H30M5S\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1__2018_11_13_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"2018-11-13\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1_hello_endjin_com_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"hello@endjin.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1_www_example_com_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"www.example.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1_first_1_false_2()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1__192_168_0_1_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"192.168.0.1\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1_new_object_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = new object();
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_json_element_backed_value_as_an_object__1_1_null_false()
    {
        var sut = JsonNumber.ParseValue("1.1".AsSpan());
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1_Hello_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"Hello\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1_Goodbye_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"Goodbye\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1__1_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1__1_1_true()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("1.1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1__1_2_3_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("[1,2,3]");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1_first_1_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("{ \"first\": \"1\" }");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1_true_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1_false_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"2018-11-13T20:20:39+00:00\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1__2018_11_13_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"2018-11-13\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"P3Y6M4DT12H30M5S\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1_hello_endjin_com_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"hello@endjin.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1_www_example_com_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"www.example.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1_first_1_false_2()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1__192_168_0_1_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"192.168.0.1\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1_new_object_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = new object();
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1_null_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1_null_false_2()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = null;
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object__1_1_undefined_false()
    {
        var sut = JsonNumber.Parse("1.1").AsDotnetBackedValue();
        object? obj = default(JsonNumber);
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object_null_null_true()
    {
        var sut = JsonNumber.Null;
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object_null_null_true_2()
    {
        var sut = JsonNumber.Null;
        object? obj = null;
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_number_dotnet_backed_value_as_an_object_null_undefined_false()
    {
        var sut = JsonNumber.Null;
        object? obj = default(JsonNumber);
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }
}