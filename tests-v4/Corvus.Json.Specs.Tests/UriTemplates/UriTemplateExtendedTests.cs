// <copyright file="UriTemplateExtendedTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.UriTemplates;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.UriTemplates;

[TestClass]
public class UriTemplateExtendedTests
{
    private static void AssertResolveResult(string template, string expectedResultsJson, ImmutableDictionary<string, JsonAny> variables)
    {
        JsonArray expectedResults = JsonArray.Parse(expectedResultsJson);

        if (expectedResults.GetArrayLength() == 1)
        {
            using JsonArrayEnumerator enumerator = expectedResults.EnumerateArray();
            enumerator.MoveNext();
            if (enumerator.Current.ValueKind == JsonValueKind.False)
            {
                Assert.Throws<Exception>(() =>
                {
                    var uriTemplate = new UriTemplate(template, createParameterParser: false, parameters: variables);
                    uriTemplate.Resolve();
                });
                return;
            }
        }

        var ut = new UriTemplate(template, createParameterParser: false, parameters: variables);
        string result = ut.Resolve();
        bool found = false;
        foreach (JsonAny expected in expectedResults.EnumerateArray())
        {
            if (expected.AsString == result)
            {
                found = true;
                break;
            }
        }

        Assert.IsTrue(found, $"Result '{result}' was not in expected set {expectedResultsJson}");
    }

    private static ImmutableDictionary<string, JsonAny> AdditionalExamples1Variables()
    {
        ImmutableDictionary<string, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<string, JsonAny>();
        builder.Add("id", JsonAny.Parse("\"person\""));
        builder.Add("token", JsonAny.Parse("\"12345\""));
        builder.Add("fields", JsonAny.Parse("[\"id\",\"name\",\"picture\"]"));
        builder.Add("format", JsonAny.Parse("\"json\""));
        builder.Add("q", JsonAny.Parse("\"URI Templates\""));
        builder.Add("page", JsonAny.Parse("\"5\""));
        builder.Add("lang", JsonAny.Parse("\"en\""));
        builder.Add("geocode", JsonAny.Parse("[\"37.76\",\"-122.427\"]"));
        builder.Add("first_name", JsonAny.Parse("\"John\""));
        builder.Add("last.name", JsonAny.Parse("\"Doe\""));
        builder.Add("Some%20Thing", JsonAny.Parse("\"foo\""));
        builder.Add("number", JsonAny.Parse("6"));
        builder.Add("long", JsonAny.Parse("37.76"));
        builder.Add("lat", JsonAny.Parse("-122.427"));
        builder.Add("group_id", JsonAny.Parse("\"12345\""));
        builder.Add("query", JsonAny.Parse("\"PREFIX dc: \\u003Chttp://purl.org/dc/elements/1.1/\\u003E SELECT ?book ?who WHERE { ?book dc:creator ?who }\""));
        builder.Add("uri", JsonAny.Parse("\"http://example.org/?uri=http%3A%2F%2Fexample.org%2F\""));
        builder.Add("word", JsonAny.Parse("\"dr\\u00FCcken\""));
        builder.Add("Stra%C3%9Fe", JsonAny.Parse("\"Gr\\u00FCner Weg\""));
        builder.Add("random", JsonAny.Parse("\"\\u0161\\u00F6\\u00E4\\u0178\\u0153\\u00F1\\u00EA\\u20AC\\u00A3\\u00A5\\u2021\\u00D1\\u00D2\\u00D3\\u00D4\\u00D5\\u00D6\\u00D7\\u00D8\\u00D9\\u00DA\\u00E0\\u00E1\\u00E2\\u00E3\\u00E4\\u00E5\\u00E6\\u00E7\\u00FF\""));
        builder.Add("assoc_special_chars", JsonAny.Parse("{\"\\u0161\\u00F6\\u00E4\\u0178\\u0153\\u00F1\\u00EA\\u20AC\\u00A3\\u00A5\\u2021\\u00D1\\u00D2\\u00D3\\u00D4\\u00D5\":\"\\u00D6\\u00D7\\u00D8\\u00D9\\u00DA\\u00E0\\u00E1\\u00E2\\u00E3\\u00E4\\u00E5\\u00E6\\u00E7\\u00FF\"}"));
        return builder.ToImmutable();
    }

    private static ImmutableDictionary<string, JsonAny> AdditionalExamples2Variables()
    {
        ImmutableDictionary<string, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<string, JsonAny>();
        builder.Add("id", JsonAny.Parse("[\"person\",\"albums\"]"));
        builder.Add("token", JsonAny.Parse("\"12345\""));
        builder.Add("fields", JsonAny.Parse("[\"id\",\"name\",\"picture\"]"));
        builder.Add("format", JsonAny.Parse("\"atom\""));
        builder.Add("q", JsonAny.Parse("\"URI Templates\""));
        builder.Add("page", JsonAny.Parse("\"10\""));
        builder.Add("start", JsonAny.Parse("\"5\""));
        builder.Add("lang", JsonAny.Parse("\"en\""));
        builder.Add("geocode", JsonAny.Parse("[\"37.76\",\"-122.427\"]"));
        return builder.ToImmutable();
    }

    private static ImmutableDictionary<string, JsonAny> AdditionalExamples3Variables()
    {
        ImmutableDictionary<string, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<string, JsonAny>();
        builder.Add("empty_list", JsonAny.Parse("[]"));
        builder.Add("empty_assoc", JsonAny.Parse("{}"));
        return builder.ToImmutable();
    }

    private static ImmutableDictionary<string, JsonAny> AdditionalExamples4Variables()
    {
        ImmutableDictionary<string, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<string, JsonAny>();
        builder.Add("42", JsonAny.Parse("\"The Answer to the Ultimate Question of Life, the Universe, and Everything\""));
        builder.Add("1337", JsonAny.Parse("[\"leet\",\"as\",\"it\",\"can\",\"be\"]"));
        builder.Add("german", JsonAny.Parse("{\"11\":\"elf\",\"12\":\"zw\\u00F6lf\"}"));
        return builder.ToImmutable();
    }

    private static ImmutableDictionary<string, JsonAny> AdditionalExamples5Variables()
    {
        ImmutableDictionary<string, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<string, JsonAny>();
        builder.Add("id", JsonAny.Parse("\"admin\""));
        builder.Add("token", JsonAny.Parse("\"12345\""));
        builder.Add("tab", JsonAny.Parse("\"overview\""));
        builder.Add("keys", JsonAny.Parse("{\"key1\": \"val1\",\"key2\": \"val2\" }"));
        return builder.ToImmutable();
    }

    private static ImmutableDictionary<string, JsonAny> AdditionalExamples6Variables()
    {
        ImmutableDictionary<string, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<string, JsonAny>();
        builder.Add("id", JsonAny.Parse("\"admin%2F\""));
        builder.Add("not_pct", JsonAny.Parse("\"%foo\""));
        builder.Add("list", JsonAny.Parse("[\"red%25\", \"%2Fgreen\", \"blue \"]"));
        builder.Add("keys", JsonAny.Parse("{\"key1\": \"val1%2F\",\"key2\": \"val2%2F\" }"));
        return builder.ToImmutable();
    }

    [TestMethod]
    [DataRow("{/id*}", """["/person"]""")]
    [DataRow("{/id*}{?fields,first_name,last.name,token}", """["/person?fields=id,name,picture\u0026first_name=John\u0026last.name=Doe\u0026token=12345","/person?fields=id,picture,name\u0026first_name=John\u0026last.name=Doe\u0026token=12345","/person?fields=picture,name,id\u0026first_name=John\u0026last.name=Doe\u0026token=12345","/person?fields=picture,id,name\u0026first_name=John\u0026last.name=Doe\u0026token=12345","/person?fields=name,picture,id\u0026first_name=John\u0026last.name=Doe\u0026token=12345","/person?fields=name,id,picture\u0026first_name=John\u0026last.name=Doe\u0026token=12345"]""")]
    [DataRow("/search.{format}{?q,geocode,lang,locale,page,result_type}", """["/search.json?q=URI%20Templates\u0026geocode=37.76,-122.427\u0026lang=en\u0026page=5","/search.json?q=URI%20Templates\u0026geocode=-122.427,37.76\u0026lang=en\u0026page=5"]""")]
    [DataRow("/test{/Some%20Thing}", """["/test/foo"]""")]
    [DataRow("/set{?number}", """["/set?number=6"]""")]
    [DataRow("/loc{?long,lat}", """["/loc?long=37.76\u0026lat=-122.427"]""")]
    [DataRow("/base{/group_id,first_name}/pages{/page,lang}{?format,q}", """["/base/12345/John/pages/5/en?format=json\u0026q=URI%20Templates"]""")]
    [DataRow("/sparql{?query}", """["/sparql?query=PREFIX%20dc%3A%20%3Chttp%3A%2F%2Fpurl.org%2Fdc%2Felements%2F1.1%2F%3E%20SELECT%20%3Fbook%20%3Fwho%20WHERE%20%7B%20%3Fbook%20dc%3Acreator%20%3Fwho%20%7D"]""")]
    [DataRow("/go{?uri}", """["/go?uri=http%3A%2F%2Fexample.org%2F%3Furi%3Dhttp%253A%252F%252Fexample.org%252F"]""")]
    [DataRow("/service{?word}", """["/service?word=dr%C3%BCcken"]""")]
    [DataRow("/lookup{?Stra%C3%9Fe}", """["/lookup?Stra%C3%9Fe=Gr%C3%BCner%20Weg"]""")]
    [DataRow("{random}", """["%C5%A1%C3%B6%C3%A4%C5%B8%C5%93%C3%B1%C3%AA%E2%82%AC%C2%A3%C2%A5%E2%80%A1%C3%91%C3%92%C3%93%C3%94%C3%95%C3%96%C3%97%C3%98%C3%99%C3%9A%C3%A0%C3%A1%C3%A2%C3%A3%C3%A4%C3%A5%C3%A6%C3%A7%C3%BF"]""")]
    [DataRow("{?assoc_special_chars*}", """["?%C5%A1%C3%B6%C3%A4%C5%B8%C5%93%C3%B1%C3%AA%E2%82%AC%C2%A3%C2%A5%E2%80%A1%C3%91%C3%92%C3%93%C3%94%C3%95=%C3%96%C3%97%C3%98%C3%99%C3%9A%C3%A0%C3%A1%C3%A2%C3%A3%C3%A4%C3%A5%C3%A6%C3%A7%C3%BF"]""")]
    public void AdditionalExamples1(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, AdditionalExamples1Variables());
    }

    [TestMethod]
    [DataRow("{/id*}", """["/person/albums","/albums/person"]""")]
    [DataRow("{/id*}{?fields,token}", """["/person/albums?fields=id,name,picture\u0026token=12345","/person/albums?fields=id,picture,name\u0026token=12345","/person/albums?fields=picture,name,id\u0026token=12345","/person/albums?fields=picture,id,name\u0026token=12345","/person/albums?fields=name,picture,id\u0026token=12345","/person/albums?fields=name,id,picture\u0026token=12345","/albums/person?fields=id,name,picture\u0026token=12345","/albums/person?fields=id,picture,name\u0026token=12345","/albums/person?fields=picture,name,id\u0026token=12345","/albums/person?fields=picture,id,name\u0026token=12345","/albums/person?fields=name,picture,id\u0026token=12345","/albums/person?fields=name,id,picture\u0026token=12345"]""")]
    public void AdditionalExamples2(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, AdditionalExamples2Variables());
    }

    [TestMethod]
    [DataRow("{/empty_list}", """[""]""")]
    [DataRow("{/empty_list*}", """[""]""")]
    [DataRow("{?empty_list}", """[""]""")]
    [DataRow("{?empty_list*}", """[""]""")]
    [DataRow("{?empty_assoc}", """[""]""")]
    [DataRow("{?empty_assoc*}", """[""]""")]
    public void AdditionalExamples3EmptyVariables(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, AdditionalExamples3Variables());
    }

    [TestMethod]
    [DataRow("{42}", """["The%20Answer%20to%20the%20Ultimate%20Question%20of%20Life%2C%20the%20Universe%2C%20and%20Everything"]""")]
    [DataRow("{?42}", """["?42=The%20Answer%20to%20the%20Ultimate%20Question%20of%20Life%2C%20the%20Universe%2C%20and%20Everything"]""")]
    [DataRow("{1337}", """["leet,as,it,can,be"]""")]
    [DataRow("{?1337*}", """["?1337=leet\u00261337=as\u00261337=it\u00261337=can\u00261337=be"]""")]
    [DataRow("{?german*}", """["?11=elf\u002612=zw%C3%B6lf","?12=zw%C3%B6lf\u002611=elf"]""")]
    public void AdditionalExamples4NumericKeys(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, AdditionalExamples4Variables());
    }

    [TestMethod]
    [DataRow("{?id,token,keys*}", """["?id=admin&token=12345&key1=val1&key2=val2","?id=admin&token=12345&key2=val2&key1=val1"]""")]
    [DataRow("{/id}{?token,keys*}", """["/admin?token=12345&key1=val1&key2=val2", "/admin?token=12345&key2=val2&key1=val1"]""")]
    [DataRow("{?id,token}{&keys*}", """["?id=admin&token=12345&key1=val1&key2=val2","?id=admin&token=12345&key2=val2&key1=val1"]""")]
    [DataRow("/user{/id}{?token,tab}{&keys*}", """["/user/admin?token=12345&tab=overview&key1=val1&key2=val2", "/user/admin?token=12345&tab=overview&key2=val2&key1=val1"]""")]
    public void AdditionalExamples5ExplodeCombinations(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, AdditionalExamples5Variables());
    }

    [TestMethod]
    [DataRow("{+id}", """["admin%2F"]""")]
    [DataRow("{#id}", """["#admin%2F"]""")]
    [DataRow("{id}", """["admin%252F"]""")]
    [DataRow("{+not_pct}", """["%25foo"]""")]
    [DataRow("{#not_pct}", """["#%25foo"]""")]
    [DataRow("{not_pct}", """["%25foo"]""")]
    [DataRow("{+list}", """["red%25,%2Fgreen,blue%20"]""")]
    [DataRow("{#list}", """["#red%25,%2Fgreen,blue%20"]""")]
    [DataRow("{list}", """["red%2525,%252Fgreen,blue%20"]""")]
    [DataRow("{+keys}", """["key1,val1%2F,key2,val2%2F"]""")]
    [DataRow("{#keys}", """["#key1,val1%2F,key2,val2%2F"]""")]
    [DataRow("{keys}", """["key1,val1%252F,key2,val2%252F"]""")]
    public void AdditionalExamples6ReservedExpansion(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, AdditionalExamples6Variables());
    }
}