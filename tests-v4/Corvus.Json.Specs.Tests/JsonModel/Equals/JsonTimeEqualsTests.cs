// <copyright file="JsonTimeEqualsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.JsonModel.Equals;

/// <summary>
/// Tests for JsonTimeEquals.
/// </summary>
[TestClass]
public class JsonTimeEqualsTests
{
    [TestMethod]
    public void Equals_for_json_element_backed_value_as_a_time__08_30_06_00_20__08_30_06_00_20_true()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        var other = JsonTime.Parse("\"08:30:06+00:20\"");
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
    public void Equals_for_json_element_backed_value_as_a_time_Garbage__08_30_06_00_20_false()
    {
        var sut = JsonTime.ParseValue("\"Garbage\"".AsSpan());
        var other = JsonTime.Parse("\"08:30:06+00:20\"");
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
    public void Equals_for_json_element_backed_value_as_a_time__08_30_06_00_20_Goodbye_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        var other = JsonTime.Parse("\"Goodbye\"");
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
    public void Equals_for_json_element_backed_value_as_a_time_null_null_true()
    {
        var sut = JsonTime.ParseValue("null".AsSpan());
        var other = JsonTime.ParseValue("null".AsSpan());
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
    public void Equals_for_json_element_backed_value_as_a_time_null__08_30_06_00_20_false()
    {
        var sut = JsonTime.ParseValue("null".AsSpan());
        var other = JsonTime.Parse("\"08:30:06+00:20\"");
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
    public void Equals_for_dotnet_backed_value_as_a_time_Garbage__08_30_06_00_20_false()
    {
        var sut = JsonTime.Parse("\"Garbage\"").AsDotnetBackedValue();
        var other = JsonTime.Parse("\"08:30:06+00:20\"");
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
    public void Equals_for_dotnet_backed_value_as_a_time__08_30_06_00_20__08_30_06_00_20_true()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        var other = JsonTime.Parse("\"08:30:06+00:20\"");
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
    public void Equals_for_dotnet_backed_value_as_a_time__08_30_06_1234567_00_20__08_30_06_1234567_00_20_true()
    {
        var sut = JsonTime.Parse("\"08:30:06.1234567+00:20\"").AsDotnetBackedValue();
        var other = JsonTime.Parse("\"08:30:06.1234567+00:20\"");
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
    public void Equals_for_dotnet_backed_value_as_a_time__08_30_06_00_20_Goodbye_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        var other = JsonTime.Parse("\"Goodbye\"");
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
    public void Equals_for_time_json_element_backed_value_as_an_IJsonValue__08_30_06_00_20_Hello_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_IJsonValue__08_30_06_00_20_Goodbye_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_IJsonValue__08_30_06_00_20__1_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_IJsonValue__08_30_06_00_20__1_1_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_IJsonValue__08_30_06_00_20__1_2_3_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_IJsonValue__08_30_06_00_20_first_1_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_IJsonValue__08_30_06_00_20_true_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_IJsonValue__08_30_06_00_20_false_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_IJsonValue__08_30_06_00_20__08_30_06_00_20T20_20_39_00_00_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"08:30:06+00:20T20:20:39+00:00\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_IJsonValue__08_30_06_00_20_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_IJsonValue__08_30_06_00_20__08_30_06_00_20_true()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"08:30:06+00:20\""));
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_IJsonValue_Garbage__08_30_06_00_20_false()
    {
        var sut = JsonTime.ParseValue("\"Garbage\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"08:30:06+00:20\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_IJsonValue__08_30_06_00_20_hello_endjin_com_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_IJsonValue__08_30_06_00_20_www_example_com_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_IJsonValue__08_30_06_00_20_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_IJsonValue__08_30_06_00_20_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_IJsonValue__08_30_06_00_20_first_1_false_2()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_IJsonValue__08_30_06_00_20__192_168_0_1_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_IJsonValue__08_30_06_00_20__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_IJsonValue__08_30_06_00_20_Hello_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Hello\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_IJsonValue__08_30_06_00_20_Goodbye_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"Goodbye\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_IJsonValue__08_30_06_00_20__1_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_IJsonValue__08_30_06_00_20__1_1_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("1.1"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_IJsonValue__08_30_06_00_20__1_2_3_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("[1,2,3]"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_IJsonValue__08_30_06_00_20_first_1_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("{ \"first\": \"1\" }"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_IJsonValue__08_30_06_00_20_true_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("true"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_IJsonValue__08_30_06_00_20_false_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("false"));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_IJsonValue__08_30_06_00_20__08_30_06_00_20T20_20_39_00_00_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"08:30:06+00:20T20:20:39+00:00\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_IJsonValue__08_30_06_00_20_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"P3Y6M4DT12H30M5S\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_IJsonValue__08_30_06_00_20__08_30_06_00_20_true()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"08:30:06+00:20\""));
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_IJsonValue_Garbage__08_30_06_00_20T20_20_39_00_00_false()
    {
        var sut = JsonTime.Parse("\"Garbage\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"08:30:06+00:20T20:20:39+00:00\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_IJsonValue__08_30_06_00_20_hello_endjin_com_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"hello@endjin.com\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_IJsonValue__08_30_06_00_20_www_example_com_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"www.example.com\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_IJsonValue__08_30_06_00_20_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_IJsonValue__08_30_06_00_20_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_IJsonValue__08_30_06_00_20_first_1_false_2()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_IJsonValue__08_30_06_00_20__192_168_0_1_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"192.168.0.1\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_IJsonValue__08_30_06_00_20__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        bool equalsResult = sut.Equals(JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\""));
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20_Hello_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"Hello\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20_Goodbye_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"Goodbye\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20__1_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = (object)JsonAny.Parse("1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20__1_1_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = (object)JsonAny.Parse("1.1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20__1_2_3_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = (object)JsonAny.Parse("[1,2,3]");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20_first_1_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = (object)JsonAny.Parse("{ \"first\": \"1\" }");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20_true_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20_false_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20__08_30_06_00_20T20_20_39_00_00_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"08:30:06+00:20T20:20:39+00:00\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"P3Y6M4DT12H30M5S\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20__08_30_06_00_20_true()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"08:30:06+00:20\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20_hello_endjin_com_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"hello@endjin.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20_www_example_com_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"www.example.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20_first_1_false_2()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20__192_168_0_1_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"192.168.0.1\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = (object)JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20_new_object_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = new object();
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_json_element_backed_value_as_an_object__08_30_06_00_20_null_false()
    {
        var sut = JsonTime.ParseValue("\"08:30:06+00:20\"".AsSpan());
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20_Hello_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"Hello\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20_Goodbye_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"Goodbye\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20__1_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20__1_1_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("1.1");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20__1_2_3_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("[1,2,3]");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20_first_1_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("{ \"first\": \"1\" }");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20_true_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("true");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20_false_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("false");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20__08_30_06_00_20T20_20_39_00_00_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"08:30:06+00:20T20:20:39+00:00\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20_P3Y6M4DT12H30M5S_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"P3Y6M4DT12H30M5S\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20__08_30_06_00_20_true()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"08:30:06+00:20\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20_hello_endjin_com_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"hello@endjin.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20_www_example_com_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"www.example.com\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20_http_foo_bar_baz_qux_quux_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"http://foo.bar/?baz=qux#quux\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20_eyAiaGVsbG8iOiAid29ybGQiIH0_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"eyAiaGVsbG8iOiAid29ybGQiIH0=\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20_first_1_false_2()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"{ \\\"first\\\": \\\"1\\\" }\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20__192_168_0_1_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"192.168.0.1\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20__0_0_0_0_0_ffff_c0a8_0001_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("\"0:0:0:0:0:ffff:c0a8:0001\"");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20_new_object_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = new object();
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20_null_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20_null_false_2()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = null;
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object__08_30_06_00_20_undefined_false()
    {
        var sut = JsonTime.Parse("\"08:30:06+00:20\"").AsDotnetBackedValue();
        object? obj = default(JsonTime);
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object_null_null_true()
    {
        var sut = JsonTime.Null;
        object? obj = (object)JsonAny.Parse("null");
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object_null_null_true_2()
    {
        var sut = JsonTime.Null;
        object? obj = null;
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsTrue(equalsResult);
    }

    [TestMethod]
    public void Equals_for_time_dotnet_backed_value_as_an_object_null_undefined_false()
    {
        var sut = JsonTime.Null;
        object? obj = default(JsonTime);
        bool equalsResult = ((object)sut).Equals(obj);
        Assert.IsFalse(equalsResult);
    }
}