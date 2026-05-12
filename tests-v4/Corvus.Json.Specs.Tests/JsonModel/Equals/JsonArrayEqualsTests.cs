// <copyright file="JsonArrayEqualsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.JsonModel.Equals;

/// <summary>
/// Tests for JsonArrayEquals.
/// </summary>
[TestClass]
public class JsonArrayEqualsTests
{
    [TestMethod]
    public void Equals_for_json_element_backed_value_as_an_array__1_2_3__1_2_3_true()
    {
        var sut = JsonArray.ParseValue("[1,\"2\",3]".AsSpan());
        var other = JsonArray.Parse("[1,\"2\",3]");
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
    public void Equals_for_json_element_backed_value_as_an_array__1_2__1_2_3_false()
    {
        var sut = JsonArray.ParseValue("[1,\"2\"]".AsSpan());
        var other = JsonArray.Parse("[1,\"2\",3]");
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
    public void Equals_for_json_element_backed_value_as_an_array__1_2_3__1_2_false()
    {
        var sut = JsonArray.ParseValue("[1,\"2\",3]".AsSpan());
        var other = JsonArray.Parse("[1,\"2\"]");
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
    public void Equals_for_json_element_backed_value_as_an_array__1_2_3__3_2_1_false()
    {
        var sut = JsonArray.ParseValue("[1,\"2\",3]".AsSpan());
        var other = JsonArray.Parse("[3,\"2\",1]");
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
    public void Equals_for_json_element_backed_value_as_an_array__1_2_3__1_2_3_false()
    {
        var sut = JsonArray.ParseValue("[1,\"2\",3]".AsSpan());
        var other = JsonArray.Parse("[1,2,3]");
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
    public void Equals_for_json_element_backed_value_as_an_array___true()
    {
        var sut = JsonArray.ParseValue("[]".AsSpan());
        var other = JsonArray.Parse("[]");
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
    public void Equals_for_json_element_backed_value_as_an_array___3_2_1_false()
    {
        var sut = JsonArray.ParseValue("[]".AsSpan());
        var other = JsonArray.Parse("[3,2,1]");
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
    public void Equals_for_json_element_backed_value_as_an_array_null_null_true()
    {
        var sut = JsonArray.ParseValue("null".AsSpan());
        var other = JsonArray.ParseValue("null".AsSpan());
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
    public void Equals_for_json_element_backed_value_as_an_array_null__1_2_3_false()
    {
        var sut = JsonArray.ParseValue("null".AsSpan());
        var other = JsonArray.Parse("[1,2,3]");
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
    public void Equals_for_dotnet_backed_value_as_an_array__1_2_3__1_2_3_true()
    {
        var sut = JsonArray.Parse("[1,\"2\",3]").AsDotnetBackedValue();
        var other = JsonArray.Parse("[1,\"2\",3]");
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
    public void Equals_for_dotnet_backed_value_as_an_array__1_2__1_2_3_false()
    {
        var sut = JsonArray.Parse("[1,\"2\"]").AsDotnetBackedValue();
        var other = JsonArray.Parse("[1,\"2\",3]");
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
    public void Equals_for_dotnet_backed_value_as_an_array__1_2_3__1_2_false()
    {
        var sut = JsonArray.Parse("[1,\"2\",3]").AsDotnetBackedValue();
        var other = JsonArray.Parse("[1,\"2\"]");
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
    public void Equals_for_dotnet_backed_value_as_an_array__1_2_3__3_2_1_false()
    {
        var sut = JsonArray.Parse("[1,\"2\",3]").AsDotnetBackedValue();
        var other = JsonArray.Parse("[3,\"2\",1]");
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
    public void Equals_for_dotnet_backed_value_as_an_array__1_2_3__1_2_3_false()
    {
        var sut = JsonArray.Parse("[1,\"2\",3]").AsDotnetBackedValue();
        var other = JsonArray.Parse("[1,2,3]");
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
    public void Equals_for_dotnet_backed_value_as_an_array___true()
    {
        var sut = JsonArray.Parse("[]").AsDotnetBackedValue();
        var other = JsonArray.Parse("[]");
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
    public void Equals_for_dotnet_backed_value_as_an_array___3_2_1_false()
    {
        var sut = JsonArray.Parse("[]").AsDotnetBackedValue();
        var other = JsonArray.Parse("[3,2,1]");
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
    public void Equals_for_array_json_element_backed_value_as_an_IJsonValue__1_2_3_Hello_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_IJsonValue__1_2_3__1_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_IJsonValue__1_2_3__1_1_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_IJsonValue__1_2_3__1_2_3_true()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_IJsonValue__1_2_3_first_1_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_IJsonValue__1_2_3_true_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_IJsonValue__1_2_3_false_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_IJsonValue__1_2_3__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_IJsonValue__1_2_3_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_IJsonValue__1_2_3__2018_11_13_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_IJsonValue__1_2_3_hello_endjin_com_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_IJsonValue__1_2_3_www_example_com_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_IJsonValue__1_2_3_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_IJsonValue__1_2_3_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_IJsonValue__1_2_3_first_1_false_2()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_IJsonValue__1_2_3__192_168_0_1_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_IJsonValue__1_2_3__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_IJsonValue__1_2_3_Hello_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_IJsonValue__1_2_3__1_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_IJsonValue__1_2_3__1_1_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_IJsonValue__1_2_3__1_2_3_true()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_IJsonValue__1_2_3_first_1_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_IJsonValue__1_2_3_true_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_IJsonValue__1_2_3_false_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_IJsonValue__1_2_3__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13T20:20:39+00:00\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_IJsonValue__1_2_3_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_IJsonValue__1_2_3__2018_11_13_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"2018-11-13\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_IJsonValue__1_2_3_hello_endjin_com_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_IJsonValue__1_2_3_www_example_com_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_IJsonValue__1_2_3_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_IJsonValue__1_2_3_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_IJsonValue__1_2_3_first_1_false_2()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_IJsonValue__1_2_3__192_168_0_1_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_IJsonValue__1_2_3__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_object__1_2_3_Hello_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        object? obj = (object)JsonAny.Parse("\"Hello\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_object__1_2_3__1_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        object? obj = (object)JsonAny.Parse("1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_object__1_2_3__1_1_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        object? obj = (object)JsonAny.Parse("1.1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_object__1_2_3__1_2_3_true()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        object? obj = (object)JsonAny.Parse("[1,2,3]");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_object__1_2_3_first_1_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        object? obj = (object)JsonAny.Parse("{ \"first\": \"1\" }");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_object__1_2_3_true_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_object__1_2_3_false_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_object__1_2_3__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        object? obj = (object)JsonAny.Parse("\"2018-11-13T20:20:39+00:00\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_object__1_2_3_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        object? obj = (object)JsonAny.Parse("\"P3Y6M4DT12H30M5S\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_object__1_2_3__2018_11_13_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        object? obj = (object)JsonAny.Parse("\"2018-11-13\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_object__1_2_3_hello_endjin_com_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        object? obj = (object)JsonAny.Parse("\"hello@endjin.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_object__1_2_3_www_example_com_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        object? obj = (object)JsonAny.Parse("\"www.example.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_object__1_2_3_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        object? obj = (object)JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_object__1_2_3_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        object? obj = (object)JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_object__1_2_3_first_1_false_2()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        object? obj = (object)JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_object__1_2_3__192_168_0_1_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        object? obj = (object)JsonAny.Parse("\"192.168.0.1\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_json_element_backed_value_as_an_object__1_2_3__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonArray.ParseValue("[1,2,3]".AsSpan());
        object? obj = (object)JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3_Hello_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"Hello\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3__1_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3__1_1_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("1.1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3__1_2_3_true()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("[1,2,3]");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3_first_1_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("{ \"first\": \"1\" }");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3_true_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3_false_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3__2018_11_13T20_20_39_00_00_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"2018-11-13T20:20:39+00:00\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"P3Y6M4DT12H30M5S\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3__2018_11_13_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"2018-11-13\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3_hello_endjin_com_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"hello@endjin.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3_www_example_com_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"www.example.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3_first_1_false_2()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3__192_168_0_1_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"192.168.0.1\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3_new_object_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = new object();
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3_null_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3_null_false_2()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = null;
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object__1_2_3_undefined_false()
    {
        var sut = JsonArray.Parse("[1,2,3]").AsDotnetBackedValue();
        object? obj = default(JsonArray);
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object_null_null_true()
    {
        var sut = JsonArray.Null;
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object_null_null_true_2()
    {
        var sut = JsonArray.Null;
        object? obj = null;
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_array_dotnet_backed_value_as_an_object_null_undefined_false()
    {
        var sut = JsonArray.Null;
        object? obj = default(JsonArray);
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }
}