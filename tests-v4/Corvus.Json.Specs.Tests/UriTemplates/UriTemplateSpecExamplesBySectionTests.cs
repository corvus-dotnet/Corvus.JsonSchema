// <copyright file="UriTemplateSpecExamplesBySectionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.UriTemplates;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.UriTemplates;

[TestClass]
public class UriTemplateSpecExamplesBySectionTests
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

    private static ImmutableDictionary<string, JsonAny> SectionVariables()
    {
        ImmutableDictionary<string, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<string, JsonAny>();
        builder.Add("count", JsonAny.Parse("[\"one\",\"two\",\"three\"]"));
        builder.Add("dom", JsonAny.Parse("[\"example\",\"com\"]"));
        builder.Add("dub", JsonAny.Parse("\"me/too\""));
        builder.Add("hello", JsonAny.Parse("\"Hello World!\""));
        builder.Add("half", JsonAny.Parse("\"50%\""));
        builder.Add("var", JsonAny.Parse("\"value\""));
        builder.Add("who", JsonAny.Parse("\"fred\""));
        builder.Add("base", JsonAny.Parse("\"http://example.com/home/\""));
        builder.Add("path", JsonAny.Parse("\"/foo/bar\""));
        builder.Add("list", JsonAny.Parse("[\"red\",\"green\",\"blue\"]"));
        builder.Add("keys", JsonAny.Parse("{\"semi\":\";\",\"dot\":\".\",\"comma\":\",\"}"));
        builder.Add("v", JsonAny.Parse("\"6\""));
        builder.Add("x", JsonAny.Parse("\"1024\""));
        builder.Add("y", JsonAny.Parse("\"768\""));
        builder.Add("empty", JsonAny.Parse("\"\""));
        builder.Add("empty_keys", JsonAny.Parse("[]"));
        builder.Add("undef", JsonAny.Parse("null"));
        return builder.ToImmutable();
    }

    [TestMethod]
    [DataRow("{count}", """["one,two,three"]""")]
    [DataRow("{count*}", """["one,two,three"]""")]
    [DataRow("{/count}", """["/one,two,three"]""")]
    [DataRow("{/count*}", """["/one/two/three"]""")]
    [DataRow("{;count}", """[";count=one,two,three"]""")]
    [DataRow("{;count*}", """[";count=one;count=two;count=three"]""")]
    [DataRow("{?count}", """["?count=one,two,three"]""")]
    [DataRow("{?count*}", """["?count=one\u0026count=two\u0026count=three"]""")]
    [DataRow("{&count*}", """["\u0026count=one\u0026count=two\u0026count=three"]""")]
    public void Section321VariableExpansion(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, SectionVariables());
    }

    [TestMethod]
    [DataRow("{var}", """["value"]""")]
    [DataRow("{hello}", """["Hello%20World%21"]""")]
    [DataRow("{half}", """["50%25"]""")]
    [DataRow("O{empty}X", """["OX"]""")]
    [DataRow("O{undef}X", """["OX"]""")]
    [DataRow("{x,y}", """["1024,768"]""")]
    [DataRow("{x,hello,y}", """["1024,Hello%20World%21,768"]""")]
    [DataRow("?{x,empty}", """["?1024,"]""")]
    [DataRow("?{x,undef}", """["?1024"]""")]
    [DataRow("?{undef,y}", """["?768"]""")]
    [DataRow("{var:3}", """["val"]""")]
    [DataRow("{var:30}", """["value"]""")]
    [DataRow("{list}", """["red,green,blue"]""")]
    [DataRow("{list*}", """["red,green,blue"]""")]
    [DataRow("{keys}", """["comma,%2C,dot,.,semi,%3B","comma,%2C,semi,%3B,dot,.","dot,.,comma,%2C,semi,%3B","dot,.,semi,%3B,comma,%2C","semi,%3B,comma,%2C,dot,.","semi,%3B,dot,.,comma,%2C"]""")]
    [DataRow("{keys*}", """["comma=%2C,dot=.,semi=%3B","comma=%2C,semi=%3B,dot=.","dot=.,comma=%2C,semi=%3B","dot=.,semi=%3B,comma=%2C","semi=%3B,comma=%2C,dot=.","semi=%3B,dot=.,comma=%2C"]""")]
    public void Section322SimpleStringExpansion(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, SectionVariables());
    }

    [TestMethod]
    [DataRow("{+var}", """["value"]""")]
    [DataRow("{/var,empty}", """["/value/"]""")]
    [DataRow("{/var,undef}", """["/value"]""")]
    [DataRow("{+hello}", """["Hello%20World!"]""")]
    [DataRow("{+half}", """["50%25"]""")]
    [DataRow("{base}index", """["http%3A%2F%2Fexample.com%2Fhome%2Findex"]""")]
    [DataRow("{+base}index", """["http://example.com/home/index"]""")]
    [DataRow("O{+empty}X", """["OX"]""")]
    [DataRow("O{+undef}X", """["OX"]""")]
    [DataRow("{+path}/here", """["/foo/bar/here"]""")]
    [DataRow("{+path:6}/here", """["/foo/b/here"]""")]
    [DataRow("here?ref={+path}", """["here?ref=/foo/bar"]""")]
    [DataRow("up{+path}{var}/here", """["up/foo/barvalue/here"]""")]
    [DataRow("{+x,hello,y}", """["1024,Hello%20World!,768"]""")]
    [DataRow("{+path,x}/here", """["/foo/bar,1024/here"]""")]
    [DataRow("{+list}", """["red,green,blue"]""")]
    [DataRow("{+list*}", """["red,green,blue"]""")]
    [DataRow("{+keys}", """["comma,,,dot,.,semi,;","comma,,,semi,;,dot,.","dot,.,comma,,,semi,;","dot,.,semi,;,comma,,","semi,;,comma,,,dot,.","semi,;,dot,.,comma,,"]""")]
    [DataRow("{+keys*}", """["comma=,,dot=.,semi=;","comma=,,semi=;,dot=.","dot=.,comma=,,semi=;","dot=.,semi=;,comma=,","semi=;,comma=,,dot=.","semi=;,dot=.,comma=,"]""")]
    public void Section323ReservedExpansion(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, SectionVariables());
    }

    [TestMethod]
    [DataRow("{#var}", """["#value"]""")]
    [DataRow("{#hello}", """["#Hello%20World!"]""")]
    [DataRow("{#half}", """["#50%25"]""")]
    [DataRow("foo{#empty}", """["foo#"]""")]
    [DataRow("foo{#undef}", """["foo"]""")]
    [DataRow("{#x,hello,y}", """["#1024,Hello%20World!,768"]""")]
    [DataRow("{#path,x}/here", """["#/foo/bar,1024/here"]""")]
    [DataRow("{#path:6}/here", """["#/foo/b/here"]""")]
    [DataRow("{#list}", """["#red,green,blue"]""")]
    [DataRow("{#list*}", """["#red,green,blue"]""")]
    [DataRow("{#keys}", """["#comma,,,dot,.,semi,;","#comma,,,semi,;,dot,.","#dot,.,comma,,,semi,;","#dot,.,semi,;,comma,,","#semi,;,comma,,,dot,.","#semi,;,dot,.,comma,,"]""")]
    public void Section324FragmentExpansion(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, SectionVariables());
    }

    [TestMethod]
    [DataRow("{.who}", """[".fred"]""")]
    [DataRow("{.who,who}", """[".fred.fred"]""")]
    [DataRow("{.half,who}", """[".50%25.fred"]""")]
    [DataRow("www{.dom*}", """["www.example.com"]""")]
    [DataRow("X{.var}", """["X.value"]""")]
    [DataRow("X{.var:3}", """["X.val"]""")]
    [DataRow("X{.empty}", """["X."]""")]
    [DataRow("X{.undef}", """["X"]""")]
    [DataRow("X{.list}", """["X.red,green,blue"]""")]
    [DataRow("X{.list*}", """["X.red.green.blue"]""")]
    [DataRow("{#keys}", """["#comma,,,dot,.,semi,;","#comma,,,semi,;,dot,.","#dot,.,comma,,,semi,;","#dot,.,semi,;,comma,,","#semi,;,comma,,,dot,.","#semi,;,dot,.,comma,,"]""")]
    [DataRow("{#keys*}", """["#comma=,,dot=.,semi=;","#comma=,,semi=;,dot=.","#dot=.,comma=,,semi=;","#dot=.,semi=;,comma=,","#semi=;,comma=,,dot=.","#semi=;,dot=.,comma=,"]""")]
    [DataRow("X{.empty_keys}", """["X"]""")]
    [DataRow("X{.empty_keys*}", """["X"]""")]
    public void Section325LabelExpansionWithDotPrefix(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, SectionVariables());
    }

    [TestMethod]
    [DataRow("{/who}", """["/fred"]""")]
    [DataRow("{/who,who}", """["/fred/fred"]""")]
    [DataRow("{/half,who}", """["/50%25/fred"]""")]
    [DataRow("{/who,dub}", """["/fred/me%2Ftoo"]""")]
    [DataRow("{/var}", """["/value"]""")]
    [DataRow("{/var,empty}", """["/value/"]""")]
    [DataRow("{/var,undef}", """["/value"]""")]
    [DataRow("{/var,x}/here", """["/value/1024/here"]""")]
    [DataRow("{/var:1,var}", """["/v/value"]""")]
    [DataRow("{/list}", """["/red,green,blue"]""")]
    [DataRow("{/list*}", """["/red/green/blue"]""")]
    [DataRow("{/list*,path:4}", """["/red/green/blue/%2Ffoo"]""")]
    [DataRow("{/keys}", """["/comma,%2C,dot,.,semi,%3B","/comma,%2C,semi,%3B,dot,.","/dot,.,comma,%2C,semi,%3B","/dot,.,semi,%3B,comma,%2C","/semi,%3B,comma,%2C,dot,.","/semi,%3B,dot,.,comma,%2C"]""")]
    [DataRow("{/keys*}", """["/comma=%2C/dot=./semi=%3B","/comma=%2C/semi=%3B/dot=.","/dot=./comma=%2C/semi=%3B","/dot=./semi=%3B/comma=%2C","/semi=%3B/comma=%2C/dot=.","/semi=%3B/dot=./comma=%2C"]""")]
    public void Section326PathSegmentExpansion(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, SectionVariables());
    }

    [TestMethod]
    [DataRow("{;who}", """[";who=fred"]""")]
    [DataRow("{;half}", """[";half=50%25"]""")]
    [DataRow("{;empty}", """[";empty"]""")]
    [DataRow("{;hello:5}", """[";hello=Hello"]""")]
    [DataRow("{;v,empty,who}", """[";v=6;empty;who=fred"]""")]
    [DataRow("{;v,bar,who}", """[";v=6;who=fred"]""")]
    [DataRow("{;x,y}", """[";x=1024;y=768"]""")]
    [DataRow("{;x,y,empty}", """[";x=1024;y=768;empty"]""")]
    [DataRow("{;x,y,undef}", """[";x=1024;y=768"]""")]
    [DataRow("{;list}", """[";list=red,green,blue"]""")]
    [DataRow("{;list*}", """[";list=red;list=green;list=blue"]""")]
    [DataRow("{;keys}", """[";keys=comma,%2C,dot,.,semi,%3B",";keys=comma,%2C,semi,%3B,dot,.",";keys=dot,.,comma,%2C,semi,%3B",";keys=dot,.,semi,%3B,comma,%2C",";keys=semi,%3B,comma,%2C,dot,.",";keys=semi,%3B,dot,.,comma,%2C"]""")]
    [DataRow("{;keys*}", """[";comma=%2C;dot=.;semi=%3B",";comma=%2C;semi=%3B;dot=.",";dot=.;comma=%2C;semi=%3B",";dot=.;semi=%3B;comma=%2C",";semi=%3B;comma=%2C;dot=.",";semi=%3B;dot=.;comma=%2C"]""")]
    public void Section327PathStyleParameterExpansion(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, SectionVariables());
    }

    [TestMethod]
    [DataRow("{?who}", """["?who=fred"]""")]
    [DataRow("{?half}", """["?half=50%25"]""")]
    [DataRow("{?x,y}", """["?x=1024\u0026y=768"]""")]
    [DataRow("{?x,y,empty}", """["?x=1024\u0026y=768\u0026empty="]""")]
    [DataRow("{?x,y,undef}", """["?x=1024\u0026y=768"]""")]
    [DataRow("{?var:3}", """["?var=val"]""")]
    [DataRow("{?list}", """["?list=red,green,blue"]""")]
    [DataRow("{?list*}", """["?list=red\u0026list=green\u0026list=blue"]""")]
    [DataRow("{?keys}", """["?keys=comma,%2C,dot,.,semi,%3B","?keys=comma,%2C,semi,%3B,dot,.","?keys=dot,.,comma,%2C,semi,%3B","?keys=dot,.,semi,%3B,comma,%2C","?keys=semi,%3B,comma,%2C,dot,.","?keys=semi,%3B,dot,.,comma,%2C"]""")]
    [DataRow("{?keys*}", """["?comma=%2C\u0026dot=.\u0026semi=%3B","?comma=%2C\u0026semi=%3B\u0026dot=.","?dot=.\u0026comma=%2C\u0026semi=%3B","?dot=.\u0026semi=%3B\u0026comma=%2C","?semi=%3B\u0026comma=%2C\u0026dot=.","?semi=%3B\u0026dot=.\u0026comma=%2C"]""")]
    public void Section328FormStyleQueryExpansion(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, SectionVariables());
    }

    [TestMethod]
    [DataRow("{&who}", """["\u0026who=fred"]""")]
    [DataRow("{&half}", """["\u0026half=50%25"]""")]
    [DataRow("?fixed=yes{&x}", """["?fixed=yes\u0026x=1024"]""")]
    [DataRow("{&var:3}", """["\u0026var=val"]""")]
    [DataRow("{&x,y,empty}", """["\u0026x=1024\u0026y=768\u0026empty="]""")]
    [DataRow("{&x,y,undef}", """["\u0026x=1024\u0026y=768"]""")]
    [DataRow("{&list}", """["\u0026list=red,green,blue"]""")]
    [DataRow("{&list*}", """["\u0026list=red\u0026list=green\u0026list=blue"]""")]
    [DataRow("{&keys}", """["\u0026keys=comma,%2C,dot,.,semi,%3B","\u0026keys=comma,%2C,semi,%3B,dot,.","\u0026keys=dot,.,comma,%2C,semi,%3B","\u0026keys=dot,.,semi,%3B,comma,%2C","\u0026keys=semi,%3B,comma,%2C,dot,.","\u0026keys=semi,%3B,dot,.,comma,%2C"]""")]
    [DataRow("{&keys*}", """["\u0026comma=%2C\u0026dot=.\u0026semi=%3B","\u0026comma=%2C\u0026semi=%3B\u0026dot=.","\u0026dot=.\u0026comma=%2C\u0026semi=%3B","\u0026dot=.\u0026semi=%3B\u0026comma=%2C","\u0026semi=%3B\u0026comma=%2C\u0026dot=.","\u0026semi=%3B\u0026dot=.\u0026comma=%2C"]""")]
    public void Section329FormStyleQueryContinuation(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, SectionVariables());
    }
}