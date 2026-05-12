// <copyright file="JsonNumberComparisonTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET
using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.JsonModel.NumericComparison;

[TestClass]
public class JsonNumberComparisonTests
{
    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_false_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1"u8);
        bool result = sut < JsonNumber.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_1_1_false_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8);
        bool result = sut < JsonNumber.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_1_false_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8);
        bool result = sut < JsonNumber.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_2_1_false_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("2"u8);
        bool result = sut < JsonNumber.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_3_true_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8);
        bool result = sut < JsonNumber.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_null_null_false_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("null"u8);
        bool result = sut < JsonNumber.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_null_1_1_false_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("null"u8);
        bool result = sut < JsonNumber.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_false_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1"u8);
        bool result = sut < JsonSingle.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_1_1_false_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8);
        bool result = sut < JsonSingle.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_1_false_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8);
        bool result = sut < JsonSingle.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_2_1_false_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("2"u8);
        bool result = sut < JsonSingle.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_3_true_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8);
        bool result = sut < JsonSingle.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_null_null_false_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("null"u8);
        bool result = sut < JsonSingle.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_null_1_1_false_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("null"u8);
        bool result = sut < JsonSingle.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_false_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1"u8);
        bool result = sut < JsonHalf.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_1_1_false_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8);
        bool result = sut < JsonHalf.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_1_false_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8);
        bool result = sut < JsonHalf.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_2_1_false_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("2"u8);
        bool result = sut < JsonHalf.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_3_true_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8);
        bool result = sut < JsonHalf.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_null_null_false_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("null"u8);
        bool result = sut < JsonHalf.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_null_1_1_false_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("null"u8);
        bool result = sut < JsonHalf.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_false_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1"u8);
        bool result = sut < JsonDouble.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_1_1_false_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8);
        bool result = sut < JsonDouble.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_1_false_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8);
        bool result = sut < JsonDouble.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_2_1_false_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("2"u8);
        bool result = sut < JsonDouble.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_3_true_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8);
        bool result = sut < JsonDouble.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_null_null_false_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("null"u8);
        bool result = sut < JsonDouble.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_null_1_1_false_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("null"u8);
        bool result = sut < JsonDouble.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_false_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1"u8);
        bool result = sut < JsonDecimal.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_1_1_false_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8);
        bool result = sut < JsonDecimal.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_1_false_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8);
        bool result = sut < JsonDecimal.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_2_1_false_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("2"u8);
        bool result = sut < JsonDecimal.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_1_1_3_true_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8);
        bool result = sut < JsonDecimal.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_null_null_false_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("null"u8);
        bool result = sut < JsonDecimal.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_json_element_backed_value_as_a_number_null_1_1_false_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("null"u8);
        bool result = sut < JsonDecimal.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_1_false_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < JsonNumber.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_1_1_1_false_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut < JsonNumber.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_1_1_false_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut < JsonNumber.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_3_true_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < JsonNumber.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_1_false_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < JsonSingle.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_1_1_1_false_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut < JsonSingle.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_1_1_false_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut < JsonSingle.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_3_true_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < JsonSingle.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_1_false_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < JsonHalf.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_1_1_1_false_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut < JsonHalf.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_1_1_false_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut < JsonHalf.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_3_true_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < JsonHalf.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_1_false_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < JsonDouble.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_1_1_1_false_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut < JsonDouble.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_1_1_false_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut < JsonDouble.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_3_true_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < JsonDouble.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_1_false_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < JsonDecimal.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_1_1_1_false_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut < JsonDecimal.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_1_1_false_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut < JsonDecimal.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_for_dotnet_backed_value_as_a_number_1_3_true_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut < JsonDecimal.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_true_JsonNum()
    {
        JsonNumber sut = JsonNumber.ParseValue("1"u8);
        bool result = sut <= JsonNumber.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_1_true_Jso()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8);
        bool result = sut <= JsonNumber.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_false_Json()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8);
        bool result = sut <= JsonNumber.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_2_1_false_JsonNu()
    {
        JsonNumber sut = JsonNumber.ParseValue("2"u8);
        bool result = sut <= JsonNumber.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_3_true_JsonN()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8);
        bool result = sut <= JsonNumber.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_null_null_false_()
    {
        JsonNumber sut = JsonNumber.ParseValue("null"u8);
        bool result = sut <= JsonNumber.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_null_1_1_false_J()
    {
        JsonNumber sut = JsonNumber.ParseValue("null"u8);
        bool result = sut <= JsonNumber.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_true_JsonHal()
    {
        JsonHalf sut = JsonHalf.ParseValue("1"u8);
        bool result = sut <= JsonHalf.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_1_true_Jso_2()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8);
        bool result = sut <= JsonHalf.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_false_Json_2()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8);
        bool result = sut <= JsonHalf.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_2_1_false_JsonHa()
    {
        JsonHalf sut = JsonHalf.ParseValue("2"u8);
        bool result = sut <= JsonHalf.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_3_true_JsonH()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8);
        bool result = sut <= JsonHalf.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_null_null_false__2()
    {
        JsonHalf sut = JsonHalf.ParseValue("null"u8);
        bool result = sut <= JsonHalf.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_null_1_1_false_J_2()
    {
        JsonHalf sut = JsonHalf.ParseValue("null"u8);
        bool result = sut <= JsonHalf.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_true_JsonSin()
    {
        JsonSingle sut = JsonSingle.ParseValue("1"u8);
        bool result = sut <= JsonSingle.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_1_true_Jso_3()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8);
        bool result = sut <= JsonSingle.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_false_Json_3()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8);
        bool result = sut <= JsonSingle.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_2_1_false_JsonSi()
    {
        JsonSingle sut = JsonSingle.ParseValue("2"u8);
        bool result = sut <= JsonSingle.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_3_true_JsonS()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8);
        bool result = sut <= JsonSingle.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_null_null_false__3()
    {
        JsonSingle sut = JsonSingle.ParseValue("null"u8);
        bool result = sut <= JsonSingle.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_null_1_1_false_J_3()
    {
        JsonSingle sut = JsonSingle.ParseValue("null"u8);
        bool result = sut <= JsonSingle.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_true_JsonDou()
    {
        JsonDouble sut = JsonDouble.ParseValue("1"u8);
        bool result = sut <= JsonDouble.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_1_true_Jso_4()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8);
        bool result = sut <= JsonDouble.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_false_Json_4()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8);
        bool result = sut <= JsonDouble.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_2_1_false_JsonDo()
    {
        JsonDouble sut = JsonDouble.ParseValue("2"u8);
        bool result = sut <= JsonDouble.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_3_true_JsonD()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8);
        bool result = sut <= JsonDouble.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_null_null_false__4()
    {
        JsonDouble sut = JsonDouble.ParseValue("null"u8);
        bool result = sut <= JsonDouble.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_null_1_1_false_J_4()
    {
        JsonDouble sut = JsonDouble.ParseValue("null"u8);
        bool result = sut <= JsonDouble.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_true_JsonDec()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1"u8);
        bool result = sut <= JsonDecimal.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_1_true_Jso_5()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8);
        bool result = sut <= JsonDecimal.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_false_Json_5()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8);
        bool result = sut <= JsonDecimal.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_2_1_false_JsonDe()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("2"u8);
        bool result = sut <= JsonDecimal.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_3_true_JsonD_2()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8);
        bool result = sut <= JsonDecimal.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_null_null_false__5()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("null"u8);
        bool result = sut <= JsonDecimal.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_json_element_backed_value_as_a_number_null_1_1_false_J_5()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("null"u8);
        bool result = sut <= JsonDecimal.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_1_true_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonNumber.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_1_1_1_true_JsonNumbe()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonNumber.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_1_1_false_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonNumber.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_3_true_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonNumber.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_1_true_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonHalf.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_1_1_1_true_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonHalf.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_1_1_false_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonHalf.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_3_true_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonHalf.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_1_true_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonSingle.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_1_1_1_true_JsonSingl()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonSingle.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_1_1_false_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonSingle.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_3_true_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonSingle.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_1_true_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonDouble.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_1_1_1_true_JsonDoubl()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonDouble.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_1_1_false_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonDouble.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_3_true_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonDouble.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_1_true_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonDecimal.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_1_1_1_true_JsonDecim()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonDecimal.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_1_1_false_JsonDecima()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonDecimal.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Less_than_or_equal_to_for_dotnet_backed_value_as_a_number_1_3_true_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut <= JsonDecimal.Parse("3");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_false_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1"u8);
        bool result = sut > JsonNumber.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_1_1_false_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8);
        bool result = sut > JsonNumber.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_1_true_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8);
        bool result = sut > JsonNumber.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_2_1_true_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("2"u8);
        bool result = sut > JsonNumber.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_3_false_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8);
        bool result = sut > JsonNumber.Parse("3");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_null_null_false_JsonNumbe()
    {
        JsonNumber sut = JsonNumber.ParseValue("null"u8);
        bool result = sut > JsonNumber.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_null_1_1_false_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("null"u8);
        bool result = sut > JsonNumber.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_false_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1"u8);
        bool result = sut > JsonSingle.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_1_1_false_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8);
        bool result = sut > JsonSingle.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_1_true_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8);
        bool result = sut > JsonSingle.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_2_1_true_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("2"u8);
        bool result = sut > JsonSingle.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_3_false_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8);
        bool result = sut > JsonSingle.Parse("3");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_null_null_false_JsonSingl()
    {
        JsonSingle sut = JsonSingle.ParseValue("null"u8);
        bool result = sut > JsonSingle.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_null_1_1_false_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("null"u8);
        bool result = sut > JsonSingle.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_false_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1"u8);
        bool result = sut > JsonHalf.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_1_1_false_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8);
        bool result = sut > JsonHalf.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_1_true_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8);
        bool result = sut > JsonHalf.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_2_1_true_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("2"u8);
        bool result = sut > JsonHalf.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_3_false_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8);
        bool result = sut > JsonHalf.Parse("3");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_null_null_false_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("null"u8);
        bool result = sut > JsonHalf.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_null_1_1_false_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("null"u8);
        bool result = sut > JsonHalf.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_false_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1"u8);
        bool result = sut > JsonDouble.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_1_1_false_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8);
        bool result = sut > JsonDouble.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_1_true_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8);
        bool result = sut > JsonDouble.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_2_1_true_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("2"u8);
        bool result = sut > JsonDouble.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_3_false_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8);
        bool result = sut > JsonDouble.Parse("3");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_null_null_false_JsonDoubl()
    {
        JsonDouble sut = JsonDouble.ParseValue("null"u8);
        bool result = sut > JsonDouble.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_null_1_1_false_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("null"u8);
        bool result = sut > JsonDouble.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_false_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1"u8);
        bool result = sut > JsonDecimal.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_1_1_false_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8);
        bool result = sut > JsonDecimal.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_1_true_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8);
        bool result = sut > JsonDecimal.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_2_1_true_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("2"u8);
        bool result = sut > JsonDecimal.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_1_1_3_false_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8);
        bool result = sut > JsonDecimal.Parse("3");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_null_null_false_JsonDecim()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("null"u8);
        bool result = sut > JsonDecimal.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_json_element_backed_value_as_a_number_null_1_1_false_JsonDecima()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("null"u8);
        bool result = sut > JsonDecimal.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_1_false_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > JsonNumber.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_1_1_1_false_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut > JsonNumber.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_1_1_true_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut > JsonNumber.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_3_false_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > JsonNumber.Parse("3");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_1_false_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > JsonHalf.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_1_1_1_false_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut > JsonHalf.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_1_1_true_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut > JsonHalf.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_3_false_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > JsonHalf.Parse("3");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_1_false_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > JsonSingle.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_1_1_1_false_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut > JsonSingle.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_1_1_true_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut > JsonSingle.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_3_false_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > JsonSingle.Parse("3");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_1_false_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > JsonDouble.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_1_1_1_false_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut > JsonDouble.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_1_1_true_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut > JsonDouble.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_3_false_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > JsonDouble.Parse("3");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_1_false_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > JsonDecimal.Parse("1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_1_1_1_false_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut > JsonDecimal.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_1_1_true_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut > JsonDecimal.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_for_dotnet_backed_value_as_a_number_1_3_false_JsonDecimal()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut > JsonDecimal.Parse("3");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_true_Json()
    {
        JsonNumber sut = JsonNumber.ParseValue("1"u8);
        bool result = sut >= JsonNumber.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_1_true_()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8);
        bool result = sut >= JsonNumber.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_true_Js()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8);
        bool result = sut >= JsonNumber.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_2_1_true_Json()
    {
        JsonNumber sut = JsonNumber.ParseValue("2"u8);
        bool result = sut >= JsonNumber.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_3_false_J()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8);
        bool result = sut >= JsonNumber.Parse("3");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_null_null_fal()
    {
        JsonNumber sut = JsonNumber.ParseValue("null"u8);
        bool result = sut >= JsonNumber.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_null_1_1_fals()
    {
        JsonNumber sut = JsonNumber.ParseValue("null"u8);
        bool result = sut >= JsonNumber.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_true_Json_2()
    {
        JsonHalf sut = JsonHalf.ParseValue("1"u8);
        bool result = sut >= JsonHalf.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_1_true__2()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8);
        bool result = sut >= JsonHalf.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_true_Js_2()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8);
        bool result = sut >= JsonHalf.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_2_1_true_Json_2()
    {
        JsonHalf sut = JsonHalf.ParseValue("2"u8);
        bool result = sut >= JsonHalf.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_3_false_J_2()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8);
        bool result = sut >= JsonHalf.Parse("3");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_null_null_fal_2()
    {
        JsonHalf sut = JsonHalf.ParseValue("null"u8);
        bool result = sut >= JsonHalf.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_null_1_1_fals_2()
    {
        JsonHalf sut = JsonHalf.ParseValue("null"u8);
        bool result = sut >= JsonHalf.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_true_Json_3()
    {
        JsonSingle sut = JsonSingle.ParseValue("1"u8);
        bool result = sut >= JsonSingle.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_1_true__3()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8);
        bool result = sut >= JsonSingle.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_true_Js_3()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8);
        bool result = sut >= JsonSingle.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_2_1_true_Json_3()
    {
        JsonSingle sut = JsonSingle.ParseValue("2"u8);
        bool result = sut >= JsonSingle.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_3_false_J_3()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8);
        bool result = sut >= JsonSingle.Parse("3");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_null_null_fal_3()
    {
        JsonSingle sut = JsonSingle.ParseValue("null"u8);
        bool result = sut >= JsonSingle.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_null_1_1_fals_3()
    {
        JsonSingle sut = JsonSingle.ParseValue("null"u8);
        bool result = sut >= JsonSingle.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_true_Json_4()
    {
        JsonDouble sut = JsonDouble.ParseValue("1"u8);
        bool result = sut >= JsonDouble.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_1_true__4()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8);
        bool result = sut >= JsonDouble.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_true_Js_4()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8);
        bool result = sut >= JsonDouble.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_2_1_true_Json_4()
    {
        JsonDouble sut = JsonDouble.ParseValue("2"u8);
        bool result = sut >= JsonDouble.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_3_false_J_4()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8);
        bool result = sut >= JsonDouble.Parse("3");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_null_null_fal_4()
    {
        JsonDouble sut = JsonDouble.ParseValue("null"u8);
        bool result = sut >= JsonDouble.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_null_1_1_fals_4()
    {
        JsonDouble sut = JsonDouble.ParseValue("null"u8);
        bool result = sut >= JsonDouble.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_true_Json_5()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1"u8);
        bool result = sut >= JsonDecimal.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_1_true__5()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8);
        bool result = sut >= JsonDecimal.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_1_true_Js_5()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8);
        bool result = sut >= JsonDecimal.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_2_1_true_Json_5()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("2"u8);
        bool result = sut >= JsonDecimal.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_1_1_3_false_J_5()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8);
        bool result = sut >= JsonDecimal.Parse("3");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_null_null_fal_5()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("null"u8);
        bool result = sut >= JsonDecimal.Null;
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_equal_to_for_json_element_backed_value_as_a_number_null_1_1_fals_5()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("null"u8);
        bool result = sut >= JsonDecimal.Parse("1.1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_1_true_JsonNumber()
    {
        JsonNumber sut = JsonNumber.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonNumber.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_1_1_1_true_JsonNu()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonNumber.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_1_1_true_JsonNumb()
    {
        JsonNumber sut = JsonNumber.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonNumber.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_3_false_JsonNumbe()
    {
        JsonNumber sut = JsonNumber.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonNumber.Parse("3");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_1_true_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonHalf.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_1_1_1_true_JsonHa()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonHalf.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_1_1_true_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonHalf.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_3_false_JsonHalf()
    {
        JsonHalf sut = JsonHalf.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonHalf.Parse("3");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_1_true_JsonSingle()
    {
        JsonSingle sut = JsonSingle.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonSingle.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_1_1_1_true_JsonSi()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonSingle.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_1_1_true_JsonSing()
    {
        JsonSingle sut = JsonSingle.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonSingle.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_3_false_JsonSingl()
    {
        JsonSingle sut = JsonSingle.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonSingle.Parse("3");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_1_true_JsonDouble()
    {
        JsonDouble sut = JsonDouble.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonDouble.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_1_1_1_true_JsonDo()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonDouble.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_1_1_true_JsonDoub()
    {
        JsonDouble sut = JsonDouble.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonDouble.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_3_false_JsonDoubl()
    {
        JsonDouble sut = JsonDouble.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonDouble.Parse("3");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_1_true_JsonDecima()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonDecimal.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_1_1_1_true_JsonDe()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonDecimal.Parse("1.1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_1_1_true_JsonDeci()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1.1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonDecimal.Parse("1");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Greater_than_or_euqal_to_for_dotnet_backed_value_as_a_number_1_3_false_JsonDecim()
    {
        JsonDecimal sut = JsonDecimal.ParseValue("1"u8).AsDotnetBackedValue();
        bool result = sut >= JsonDecimal.Parse("3");
        Assert.IsFalse(result);
    }
}
#endif