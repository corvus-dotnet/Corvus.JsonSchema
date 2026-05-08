// <copyright file="JsonRelativePointerEqualsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.JsonModel.Equals;

/// <summary>
/// Tests for JsonRelativePointerEquals.
/// </summary>
[TestClass]
public class JsonRelativePointerEqualsTests
{
    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_relativePointer__0_foo_bar__0_foo_bar_true()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        var other = JsonRelativePointer.Parse("\"0/foo/bar\"");
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_relativePointer__0_foo_bar_foo_bar_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        var other = JsonRelativePointer.Parse("\"/foo/bar\"");
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_relativePointer_null_null_true()
    {
        var sut = JsonRelativePointer.ParseValue("null".AsSpan());
        var other = JsonRelativePointer.ParseValue("null".AsSpan());
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
    public void Equals_for_json_element_backed_value_as_a_relativePointer_null__0_foo_bar_false()
    {
        var sut = JsonRelativePointer.ParseValue("null".AsSpan());
        var other = JsonRelativePointer.Parse("\"0/foo/bar\"");
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_relativePointer__0_foo_bar__0_foo_bar_true()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        var other = JsonRelativePointer.Parse("\"0/foo/bar\"");
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        Assert.IsTrue(equalsResult);
        Assert.IsTrue(equalityResult);
        Assert.IsFalse(inequalityResult);
        Assert.IsTrue(hashCodeResult);
        Assert.IsTrue(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_dotnet_backed_value_as_a_relativePointer__0_foo_bar_foo_bar_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        var other = JsonRelativePointer.Parse("\"/foo/bar\"");
        bool equalsResult = sut.Equals(other);
        bool equalityResult = sut == other;
        bool inequalityResult = sut != other;
        bool hashCodeResult = sut.GetHashCode() == other.GetHashCode();
        bool equalsOtherBackedResult = sut.Equals(other.AsDotnetBackedValue());
        Assert.IsFalse(equalsResult);
        Assert.IsFalse(equalityResult);
        Assert.IsTrue(inequalityResult);
        Assert.IsFalse(hashCodeResult);
        Assert.IsFalse(equalsOtherBackedResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_IJsonValue__0_foo_bar_Hello_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_IJsonValue__0_foo_bar_Goodbye_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_IJsonValue__0_foo_bar__1_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_IJsonValue__0_foo_bar__1_1_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_IJsonValue__0_foo_bar__1_2_3_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_IJsonValue__0_foo_bar_first_1_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_IJsonValue__0_foo_bar_true_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_IJsonValue__0_foo_bar_false_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_IJsonValue__0_foo_bar__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_IJsonValue__0_foo_bar__2018_11_13_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_IJsonValue__0_foo_bar_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_IJsonValue__0_foo_bar__2018_11_13_false_2()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_IJsonValue__0_foo_bar_hello_endjin_com_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_IJsonValue__0_foo_bar_www_example_com_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_IJsonValue__0_foo_bar__0_foo_bar_true()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0/foo/bar\""));
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_IJsonValue__0_foo_bar_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_IJsonValue__0_foo_bar_first_1_false_2()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_IJsonValue__0_foo_bar__192_168_0_1_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_IJsonValue__0_foo_bar__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_IJsonValue__0_foo_bar_Hello_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_IJsonValue__0_foo_bar_Goodbye_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_IJsonValue__0_foo_bar__1_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_IJsonValue__0_foo_bar__1_1_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_IJsonValue__0_foo_bar__1_2_3_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_IJsonValue__0_foo_bar_first_1_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_IJsonValue__0_foo_bar_true_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_IJsonValue__0_foo_bar_false_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_IJsonValue__0_foo_bar__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_IJsonValue__0_foo_bar_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_IJsonValue__0_foo_bar__2018_11_13_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_IJsonValue__0_foo_bar_P3Y6M4DT12H30M5S_false_2()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_IJsonValue__0_foo_bar_hello_endjin_com_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_IJsonValue__0_foo_bar_www_example_com_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_IJsonValue__0_foo_bar__0_foo_bar_true()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0/foo/bar\""));
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_IJsonValue__0_foo_bar_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_IJsonValue__0_foo_bar_first_1_false_2()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_IJsonValue__0_foo_bar__192_168_0_1_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_IJsonValue__0_foo_bar__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar_Hello_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"Hello\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar_Goodbye_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"Goodbye\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar__1_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = (object)JsonAny.Parse("1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar__1_1_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = (object)JsonAny.Parse("1.1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar__1_2_3_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = (object)JsonAny.Parse("[1,2,3]");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar_first_1_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = (object)JsonAny.Parse("{ \"first\": \"1\" }");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar_true_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar_false_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"2018-11-13T20:20:39+00:00\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"P3Y6M4DT12H30M5S\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar__2018_11_13_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"2018-11-13\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar_hello_endjin_com_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"hello@endjin.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar_www_example_com_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"www.example.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar__0_foo_bar_true()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"0/foo/bar\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar_first_1_false_2()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar__192_168_0_1_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"192.168.0.1\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar_new_object_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = new object();
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_json_element_backed_value_as_an_object__0_foo_bar_null_false()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar_Hello_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"Hello\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar_Goodbye_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"Goodbye\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar__1_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar__1_1_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("1.1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar__1_2_3_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("[1,2,3]");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar_first_1_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("{ \"first\": \"1\" }");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar_true_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar_false_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"2018-11-13T20:20:39+00:00\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar__2018_11_13_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"2018-11-13\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"P3Y6M4DT12H30M5S\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar_hello_endjin_com_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"hello@endjin.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar_www_example_com_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"www.example.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar__0_foo_bar_true()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"0/foo/bar\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar_first_1_false_2()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar__192_168_0_1_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"192.168.0.1\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar_new_object_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = new object();
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar_null_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar_null_false_2()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = null;
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object__0_foo_bar_undefined_false()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        object? obj = default(JsonRelativePointer);
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object_null_null_true()
    {
        var sut = JsonRelativePointer.Null;
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object_null_null_true_2()
    {
        var sut = JsonRelativePointer.Null;
        object? obj = null;
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_relativePointer_dotnet_backed_value_as_an_object_null_undefined_false()
    {
        var sut = JsonRelativePointer.Null;
        object? obj = default(JsonRelativePointer);
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }
}