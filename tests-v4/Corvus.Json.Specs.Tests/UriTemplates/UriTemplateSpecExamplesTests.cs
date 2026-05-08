// <copyright file="UriTemplateSpecExamplesTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.UriTemplates;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.UriTemplates;

[TestClass]
public class UriTemplateSpecExamplesTests
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

    private static ImmutableDictionary<string, JsonAny> Level1Variables()
    {
        ImmutableDictionary<string, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<string, JsonAny>();
        builder.Add("var", JsonAny.Parse("\"value\""));
        builder.Add("hello", JsonAny.Parse("\"Hello World!\""));
        return builder.ToImmutable();
    }

    private static ImmutableDictionary<string, JsonAny> Level2Variables()
    {
        ImmutableDictionary<string, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<string, JsonAny>();
        builder.Add("var", JsonAny.Parse("\"value\""));
        builder.Add("hello", JsonAny.Parse("\"Hello World!\""));
        builder.Add("path", JsonAny.Parse("\"/foo/bar\""));
        return builder.ToImmutable();
    }

    private static ImmutableDictionary<string, JsonAny> Level3Variables()
    {
        ImmutableDictionary<string, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<string, JsonAny>();
        builder.Add("var", JsonAny.Parse("\"value\""));
        builder.Add("hello", JsonAny.Parse("\"Hello World!\""));
        builder.Add("empty", JsonAny.Parse("\"\""));
        builder.Add("path", JsonAny.Parse("\"/foo/bar\""));
        builder.Add("x", JsonAny.Parse("\"1024\""));
        builder.Add("y", JsonAny.Parse("\"768\""));
        return builder.ToImmutable();
    }

    private static ImmutableDictionary<string, JsonAny> Level4Variables()
    {
        ImmutableDictionary<string, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<string, JsonAny>();
        builder.Add("var", JsonAny.Parse("\"value\""));
        builder.Add("hello", JsonAny.Parse("\"Hello World!\""));
        builder.Add("path", JsonAny.Parse("\"/foo/bar\""));
        builder.Add("list", JsonAny.Parse("[\"red\",\"green\",\"blue\"]"));
        builder.Add("keys", JsonAny.Parse("{\"semi\":\";\",\"dot\":\".\",\"comma\":\",\"}"));
        return builder.ToImmutable();
    }

    [TestMethod]
    [DataRow("{var}", """["value"]""")]
    [DataRow("{hello}", """["Hello%20World%21"]""")]
    public void Level1Examples(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, Level1Variables());
    }

    [TestMethod]
    [DataRow("{+var}", """["value"]""")]
    [DataRow("{+hello}", """["Hello%20World!"]""")]
    [DataRow("{+path}/here", """["/foo/bar/here"]""")]
    [DataRow("here?ref={+path}", """["here?ref=/foo/bar"]""")]
    public void Level2Examples(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, Level2Variables());
    }

    [TestMethod]
    [DataRow("map?{x,y}", """["map?1024,768"]""")]
    [DataRow("{x,hello,y}", """["1024,Hello%20World%21,768"]""")]
    [DataRow("{+x,hello,y}", """["1024,Hello%20World!,768"]""")]
    [DataRow("{+path,x}/here", """["/foo/bar,1024/here"]""")]
    [DataRow("{#x,hello,y}", """["#1024,Hello%20World!,768"]""")]
    [DataRow("{#path,x}/here", """["#/foo/bar,1024/here"]""")]
    [DataRow("X{.var}", """["X.value"]""")]
    [DataRow("X{.x,y}", """["X.1024.768"]""")]
    [DataRow("{/var}", """["/value"]""")]
    [DataRow("{/var,x}/here", """["/value/1024/here"]""")]
    [DataRow("{;x,y}", """[";x=1024;y=768"]""")]
    [DataRow("{;x,y,empty}", """[";x=1024;y=768;empty"]""")]
    [DataRow("{?x,y}", """["?x=1024\u0026y=768"]""")]
    [DataRow("{?x,y,empty}", """["?x=1024\u0026y=768\u0026empty="]""")]
    [DataRow("?fixed=yes{&x}", """["?fixed=yes\u0026x=1024"]""")]
    [DataRow("{&x,y,empty}", """["\u0026x=1024\u0026y=768\u0026empty="]""")]
    public void Level3Examples(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, Level3Variables());
    }

    [TestMethod]
    [DataRow("{var:3}", """["val"]""")]
    [DataRow("{var:30}", """["value"]""")]
    [DataRow("{list}", """["red,green,blue"]""")]
    [DataRow("{list*}", """["red,green,blue"]""")]
    [DataRow("{keys}", """["comma,%2C,dot,.,semi,%3B","comma,%2C,semi,%3B,dot,.","dot,.,comma,%2C,semi,%3B","dot,.,semi,%3B,comma,%2C","semi,%3B,comma,%2C,dot,.","semi,%3B,dot,.,comma,%2C"]""")]
    [DataRow("{keys*}", """["comma=%2C,dot=.,semi=%3B","comma=%2C,semi=%3B,dot=.","dot=.,comma=%2C,semi=%3B","dot=.,semi=%3B,comma=%2C","semi=%3B,comma=%2C,dot=.","semi=%3B,dot=.,comma=%2C"]""")]
    [DataRow("{+path:6}/here", """["/foo/b/here"]""")]
    [DataRow("{+list}", """["red,green,blue"]""")]
    [DataRow("{+list*}", """["red,green,blue"]""")]
    [DataRow("{+keys}", """["comma,,,dot,.,semi,;","comma,,,semi,;,dot,.","dot,.,comma,,,semi,;","dot,.,semi,;,comma,,","semi,;,comma,,,dot,.","semi,;,dot,.,comma,,"]""")]
    [DataRow("{+keys*}", """["comma=,,dot=.,semi=;","comma=,,semi=;,dot=.","dot=.,comma=,,semi=;","dot=.,semi=;,comma=,","semi=;,comma=,,dot=.","semi=;,dot=.,comma=,"]""")]
    [DataRow("{#path:6}/here", """["#/foo/b/here"]""")]
    [DataRow("{#list}", """["#red,green,blue"]""")]
    [DataRow("{#list*}", """["#red,green,blue"]""")]
    [DataRow("{#keys}", """["#comma,,,dot,.,semi,;","#comma,,,semi,;,dot,.","#dot,.,comma,,,semi,;","#dot,.,semi,;,comma,,","#semi,;,comma,,,dot,.","#semi,;,dot,.,comma,,"]""")]
    [DataRow("{#keys*}", """["#comma=,,dot=.,semi=;","#comma=,,semi=;,dot=.","#dot=.,comma=,,semi=;","#dot=.,semi=;,comma=,","#semi=;,comma=,,dot=.","#semi=;,dot=.,comma=,"]""")]
    [DataRow("X{.var:3}", """["X.val"]""")]
    [DataRow("X{.list}", """["X.red,green,blue"]""")]
    [DataRow("X{.list*}", """["X.red.green.blue"]""")]
    [DataRow("X{.keys}", """["X.comma,%2C,dot,.,semi,%3B","X.comma,%2C,semi,%3B,dot,.","X.dot,.,comma,%2C,semi,%3B","X.dot,.,semi,%3B,comma,%2C","X.semi,%3B,comma,%2C,dot,.","X.semi,%3B,dot,.,comma,%2C"]""")]
    [DataRow("{/var:1,var}", """["/v/value"]""")]
    [DataRow("{/list}", """["/red,green,blue"]""")]
    [DataRow("{/list*}", """["/red/green/blue"]""")]
    [DataRow("{/list*,path:4}", """["/red/green/blue/%2Ffoo"]""")]
    [DataRow("{/keys}", """["/comma,%2C,dot,.,semi,%3B","/comma,%2C,semi,%3B,dot,.","/dot,.,comma,%2C,semi,%3B","/dot,.,semi,%3B,comma,%2C","/semi,%3B,comma,%2C,dot,.","/semi,%3B,dot,.,comma,%2C"]""")]
    [DataRow("{/keys*}", """["/comma=%2C/dot=./semi=%3B","/comma=%2C/semi=%3B/dot=.","/dot=./comma=%2C/semi=%3B","/dot=./semi=%3B/comma=%2C","/semi=%3B/comma=%2C/dot=.","/semi=%3B/dot=./comma=%2C"]""")]
    [DataRow("{;hello:5}", """[";hello=Hello"]""")]
    [DataRow("{;list}", """[";list=red,green,blue"]""")]
    [DataRow("{;list*}", """[";list=red;list=green;list=blue"]""")]
    [DataRow("{;keys}", """[";keys=comma,%2C,dot,.,semi,%3B",";keys=comma,%2C,semi,%3B,dot,.",";keys=dot,.,comma,%2C,semi,%3B",";keys=dot,.,semi,%3B,comma,%2C",";keys=semi,%3B,comma,%2C,dot,.",";keys=semi,%3B,dot,.,comma,%2C"]""")]
    [DataRow("{;keys*}", """[";comma=%2C;dot=.;semi=%3B",";comma=%2C;semi=%3B;dot=.",";dot=.;comma=%2C;semi=%3B",";dot=.;semi=%3B;comma=%2C",";semi=%3B;comma=%2C;dot=.",";semi=%3B;dot=.;comma=%2C"]""")]
    [DataRow("{?var:3}", """["?var=val"]""")]
    [DataRow("{?list}", """["?list=red,green,blue"]""")]
    [DataRow("{?list*}", """["?list=red\u0026list=green\u0026list=blue"]""")]
    [DataRow("{?keys}", """["?keys=comma,%2C,dot,.,semi,%3B","?keys=comma,%2C,semi,%3B,dot,.","?keys=dot,.,comma,%2C,semi,%3B","?keys=dot,.,semi,%3B,comma,%2C","?keys=semi,%3B,comma,%2C,dot,.","?keys=semi,%3B,dot,.,comma,%2C"]""")]
    [DataRow("{?keys*}", """["?comma=%2C\u0026dot=.\u0026semi=%3B","?comma=%2C\u0026semi=%3B\u0026dot=.","?dot=.\u0026comma=%2C\u0026semi=%3B","?dot=.\u0026semi=%3B\u0026comma=%2C","?semi=%3B\u0026comma=%2C\u0026dot=.","?semi=%3B\u0026dot=.\u0026comma=%2C"]""")]
    [DataRow("{&var:3}", """["\u0026var=val"]""")]
    [DataRow("{&list}", """["\u0026list=red,green,blue"]""")]
    [DataRow("{&list*}", """["\u0026list=red\u0026list=green\u0026list=blue"]""")]
    [DataRow("{&keys}", """["\u0026keys=comma,%2C,dot,.,semi,%3B","\u0026keys=comma,%2C,semi,%3B,dot,.","\u0026keys=dot,.,comma,%2C,semi,%3B","\u0026keys=dot,.,semi,%3B,comma,%2C","\u0026keys=semi,%3B,comma,%2C,dot,.","\u0026keys=semi,%3B,dot,.,comma,%2C"]""")]
    [DataRow("{&keys*}", """["\u0026comma=%2C\u0026dot=.\u0026semi=%3B","\u0026comma=%2C\u0026semi=%3B\u0026dot=.","\u0026dot=.\u0026comma=%2C\u0026semi=%3B","\u0026dot=.\u0026semi=%3B\u0026comma=%2C","\u0026semi=%3B\u0026comma=%2C\u0026dot=.","\u0026semi=%3B\u0026dot=.\u0026comma=%2C"]""")]
    public void Level4Examples(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, Level4Variables());
    }
}