// <copyright file="UriTemplateSpecExamplesBySectionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.UriTemplates;
using Xunit;

namespace Corvus.Json.Specs.Tests.UriTemplates;

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
                Assert.ThrowsAny<Exception>(() =>
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

        Assert.True(found, $"Result '{result}' was not in expected set {expectedResultsJson}");
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

    [Theory]
    [InlineData("{count}", """["one,two,three"]""")]
    [InlineData("{count*}", """["one,two,three"]""")]
    [InlineData("{/count}", """["/one,two,three"]""")]
    [InlineData("{/count*}", """["/one/two/three"]""")]
    [InlineData("{;count}", """[";count=one,two,three"]""")]
    [InlineData("{;count*}", """[";count=one;count=two;count=three"]""")]
    [InlineData("{?count}", """["?count=one,two,three"]""")]
    [InlineData("{?count*}", """["?count=one\u0026count=two\u0026count=three"]""")]
    [InlineData("{&count*}", """["\u0026count=one\u0026count=two\u0026count=three"]""")]
    public void Section321VariableExpansion(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, SectionVariables());
    }

    [Theory]
    [InlineData("{var}", """["value"]""")]
    [InlineData("{hello}", """["Hello%20World%21"]""")]
    [InlineData("{half}", """["50%25"]""")]
    [InlineData("O{empty}X", """["OX"]""")]
    [InlineData("O{undef}X", """["OX"]""")]
    [InlineData("{x,y}", """["1024,768"]""")]
    [InlineData("{x,hello,y}", """["1024,Hello%20World%21,768"]""")]
    [InlineData("?{x,empty}", """["?1024,"]""")]
    [InlineData("?{x,undef}", """["?1024"]""")]
    [InlineData("?{undef,y}", """["?768"]""")]
    [InlineData("{var:3}", """["val"]""")]
    [InlineData("{var:30}", """["value"]""")]
    [InlineData("{list}", """["red,green,blue"]""")]
    [InlineData("{list*}", """["red,green,blue"]""")]
    [InlineData("{keys}", """["comma,%2C,dot,.,semi,%3B","comma,%2C,semi,%3B,dot,.","dot,.,comma,%2C,semi,%3B","dot,.,semi,%3B,comma,%2C","semi,%3B,comma,%2C,dot,.","semi,%3B,dot,.,comma,%2C"]""")]
    [InlineData("{keys*}", """["comma=%2C,dot=.,semi=%3B","comma=%2C,semi=%3B,dot=.","dot=.,comma=%2C,semi=%3B","dot=.,semi=%3B,comma=%2C","semi=%3B,comma=%2C,dot=.","semi=%3B,dot=.,comma=%2C"]""")]
    public void Section322SimpleStringExpansion(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, SectionVariables());
    }

    [Theory]
    [InlineData("{+var}", """["value"]""")]
    [InlineData("{/var,empty}", """["/value/"]""")]
    [InlineData("{/var,undef}", """["/value"]""")]
    [InlineData("{+hello}", """["Hello%20World!"]""")]
    [InlineData("{+half}", """["50%25"]""")]
    [InlineData("{base}index", """["http%3A%2F%2Fexample.com%2Fhome%2Findex"]""")]
    [InlineData("{+base}index", """["http://example.com/home/index"]""")]
    [InlineData("O{+empty}X", """["OX"]""")]
    [InlineData("O{+undef}X", """["OX"]""")]
    [InlineData("{+path}/here", """["/foo/bar/here"]""")]
    [InlineData("{+path:6}/here", """["/foo/b/here"]""")]
    [InlineData("here?ref={+path}", """["here?ref=/foo/bar"]""")]
    [InlineData("up{+path}{var}/here", """["up/foo/barvalue/here"]""")]
    [InlineData("{+x,hello,y}", """["1024,Hello%20World!,768"]""")]
    [InlineData("{+path,x}/here", """["/foo/bar,1024/here"]""")]
    [InlineData("{+list}", """["red,green,blue"]""")]
    [InlineData("{+list*}", """["red,green,blue"]""")]
    [InlineData("{+keys}", """["comma,,,dot,.,semi,;","comma,,,semi,;,dot,.","dot,.,comma,,,semi,;","dot,.,semi,;,comma,,","semi,;,comma,,,dot,.","semi,;,dot,.,comma,,"]""")]
    [InlineData("{+keys*}", """["comma=,,dot=.,semi=;","comma=,,semi=;,dot=.","dot=.,comma=,,semi=;","dot=.,semi=;,comma=,","semi=;,comma=,,dot=.","semi=;,dot=.,comma=,"]""")]
    public void Section323ReservedExpansion(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, SectionVariables());
    }

    [Theory]
    [InlineData("{#var}", """["#value"]""")]
    [InlineData("{#hello}", """["#Hello%20World!"]""")]
    [InlineData("{#half}", """["#50%25"]""")]
    [InlineData("foo{#empty}", """["foo#"]""")]
    [InlineData("foo{#undef}", """["foo"]""")]
    [InlineData("{#x,hello,y}", """["#1024,Hello%20World!,768"]""")]
    [InlineData("{#path,x}/here", """["#/foo/bar,1024/here"]""")]
    [InlineData("{#path:6}/here", """["#/foo/b/here"]""")]
    [InlineData("{#list}", """["#red,green,blue"]""")]
    [InlineData("{#list*}", """["#red,green,blue"]""")]
    [InlineData("{#keys}", """["#comma,,,dot,.,semi,;","#comma,,,semi,;,dot,.","#dot,.,comma,,,semi,;","#dot,.,semi,;,comma,,","#semi,;,comma,,,dot,.","#semi,;,dot,.,comma,,"]""")]
    public void Section324FragmentExpansion(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, SectionVariables());
    }

    [Theory]
    [InlineData("{.who}", """[".fred"]""")]
    [InlineData("{.who,who}", """[".fred.fred"]""")]
    [InlineData("{.half,who}", """[".50%25.fred"]""")]
    [InlineData("www{.dom*}", """["www.example.com"]""")]
    [InlineData("X{.var}", """["X.value"]""")]
    [InlineData("X{.var:3}", """["X.val"]""")]
    [InlineData("X{.empty}", """["X."]""")]
    [InlineData("X{.undef}", """["X"]""")]
    [InlineData("X{.list}", """["X.red,green,blue"]""")]
    [InlineData("X{.list*}", """["X.red.green.blue"]""")]
    [InlineData("{#keys}", """["#comma,,,dot,.,semi,;","#comma,,,semi,;,dot,.","#dot,.,comma,,,semi,;","#dot,.,semi,;,comma,,","#semi,;,comma,,,dot,.","#semi,;,dot,.,comma,,"]""")]
    [InlineData("{#keys*}", """["#comma=,,dot=.,semi=;","#comma=,,semi=;,dot=.","#dot=.,comma=,,semi=;","#dot=.,semi=;,comma=,","#semi=;,comma=,,dot=.","#semi=;,dot=.,comma=,"]""")]
    [InlineData("X{.empty_keys}", """["X"]""")]
    [InlineData("X{.empty_keys*}", """["X"]""")]
    public void Section325LabelExpansionWithDotPrefix(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, SectionVariables());
    }

    [Theory]
    [InlineData("{/who}", """["/fred"]""")]
    [InlineData("{/who,who}", """["/fred/fred"]""")]
    [InlineData("{/half,who}", """["/50%25/fred"]""")]
    [InlineData("{/who,dub}", """["/fred/me%2Ftoo"]""")]
    [InlineData("{/var}", """["/value"]""")]
    [InlineData("{/var,empty}", """["/value/"]""")]
    [InlineData("{/var,undef}", """["/value"]""")]
    [InlineData("{/var,x}/here", """["/value/1024/here"]""")]
    [InlineData("{/var:1,var}", """["/v/value"]""")]
    [InlineData("{/list}", """["/red,green,blue"]""")]
    [InlineData("{/list*}", """["/red/green/blue"]""")]
    [InlineData("{/list*,path:4}", """["/red/green/blue/%2Ffoo"]""")]
    [InlineData("{/keys}", """["/comma,%2C,dot,.,semi,%3B","/comma,%2C,semi,%3B,dot,.","/dot,.,comma,%2C,semi,%3B","/dot,.,semi,%3B,comma,%2C","/semi,%3B,comma,%2C,dot,.","/semi,%3B,dot,.,comma,%2C"]""")]
    [InlineData("{/keys*}", """["/comma=%2C/dot=./semi=%3B","/comma=%2C/semi=%3B/dot=.","/dot=./comma=%2C/semi=%3B","/dot=./semi=%3B/comma=%2C","/semi=%3B/comma=%2C/dot=.","/semi=%3B/dot=./comma=%2C"]""")]
    public void Section326PathSegmentExpansion(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, SectionVariables());
    }

    [Theory]
    [InlineData("{;who}", """[";who=fred"]""")]
    [InlineData("{;half}", """[";half=50%25"]""")]
    [InlineData("{;empty}", """[";empty"]""")]
    [InlineData("{;hello:5}", """[";hello=Hello"]""")]
    [InlineData("{;v,empty,who}", """[";v=6;empty;who=fred"]""")]
    [InlineData("{;v,bar,who}", """[";v=6;who=fred"]""")]
    [InlineData("{;x,y}", """[";x=1024;y=768"]""")]
    [InlineData("{;x,y,empty}", """[";x=1024;y=768;empty"]""")]
    [InlineData("{;x,y,undef}", """[";x=1024;y=768"]""")]
    [InlineData("{;list}", """[";list=red,green,blue"]""")]
    [InlineData("{;list*}", """[";list=red;list=green;list=blue"]""")]
    [InlineData("{;keys}", """[";keys=comma,%2C,dot,.,semi,%3B",";keys=comma,%2C,semi,%3B,dot,.",";keys=dot,.,comma,%2C,semi,%3B",";keys=dot,.,semi,%3B,comma,%2C",";keys=semi,%3B,comma,%2C,dot,.",";keys=semi,%3B,dot,.,comma,%2C"]""")]
    [InlineData("{;keys*}", """[";comma=%2C;dot=.;semi=%3B",";comma=%2C;semi=%3B;dot=.",";dot=.;comma=%2C;semi=%3B",";dot=.;semi=%3B;comma=%2C",";semi=%3B;comma=%2C;dot=.",";semi=%3B;dot=.;comma=%2C"]""")]
    public void Section327PathStyleParameterExpansion(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, SectionVariables());
    }

    [Theory]
    [InlineData("{?who}", """["?who=fred"]""")]
    [InlineData("{?half}", """["?half=50%25"]""")]
    [InlineData("{?x,y}", """["?x=1024\u0026y=768"]""")]
    [InlineData("{?x,y,empty}", """["?x=1024\u0026y=768\u0026empty="]""")]
    [InlineData("{?x,y,undef}", """["?x=1024\u0026y=768"]""")]
    [InlineData("{?var:3}", """["?var=val"]""")]
    [InlineData("{?list}", """["?list=red,green,blue"]""")]
    [InlineData("{?list*}", """["?list=red\u0026list=green\u0026list=blue"]""")]
    [InlineData("{?keys}", """["?keys=comma,%2C,dot,.,semi,%3B","?keys=comma,%2C,semi,%3B,dot,.","?keys=dot,.,comma,%2C,semi,%3B","?keys=dot,.,semi,%3B,comma,%2C","?keys=semi,%3B,comma,%2C,dot,.","?keys=semi,%3B,dot,.,comma,%2C"]""")]
    [InlineData("{?keys*}", """["?comma=%2C\u0026dot=.\u0026semi=%3B","?comma=%2C\u0026semi=%3B\u0026dot=.","?dot=.\u0026comma=%2C\u0026semi=%3B","?dot=.\u0026semi=%3B\u0026comma=%2C","?semi=%3B\u0026comma=%2C\u0026dot=.","?semi=%3B\u0026dot=.\u0026comma=%2C"]""")]
    public void Section328FormStyleQueryExpansion(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, SectionVariables());
    }

    [Theory]
    [InlineData("{&who}", """["\u0026who=fred"]""")]
    [InlineData("{&half}", """["\u0026half=50%25"]""")]
    [InlineData("?fixed=yes{&x}", """["?fixed=yes\u0026x=1024"]""")]
    [InlineData("{&var:3}", """["\u0026var=val"]""")]
    [InlineData("{&x,y,empty}", """["\u0026x=1024\u0026y=768\u0026empty="]""")]
    [InlineData("{&x,y,undef}", """["\u0026x=1024\u0026y=768"]""")]
    [InlineData("{&list}", """["\u0026list=red,green,blue"]""")]
    [InlineData("{&list*}", """["\u0026list=red\u0026list=green\u0026list=blue"]""")]
    [InlineData("{&keys}", """["\u0026keys=comma,%2C,dot,.,semi,%3B","\u0026keys=comma,%2C,semi,%3B,dot,.","\u0026keys=dot,.,comma,%2C,semi,%3B","\u0026keys=dot,.,semi,%3B,comma,%2C","\u0026keys=semi,%3B,comma,%2C,dot,.","\u0026keys=semi,%3B,dot,.,comma,%2C"]""")]
    [InlineData("{&keys*}", """["\u0026comma=%2C\u0026dot=.\u0026semi=%3B","\u0026comma=%2C\u0026semi=%3B\u0026dot=.","\u0026dot=.\u0026comma=%2C\u0026semi=%3B","\u0026dot=.\u0026semi=%3B\u0026comma=%2C","\u0026semi=%3B\u0026comma=%2C\u0026dot=.","\u0026semi=%3B\u0026dot=.\u0026comma=%2C"]""")]
    public void Section329FormStyleQueryContinuation(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, SectionVariables());
    }
}