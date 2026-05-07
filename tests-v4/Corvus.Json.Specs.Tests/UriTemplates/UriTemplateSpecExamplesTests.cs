// <copyright file="UriTemplateSpecExamplesTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.UriTemplates;
using Xunit;

namespace Corvus.Json.Specs.Tests.UriTemplates;

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

    [Theory]
    [InlineData("{var}", """["value"]""")]
    [InlineData("{hello}", """["Hello%20World%21"]""")]
    public void Level1Examples(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, Level1Variables());
    }

    [Theory]
    [InlineData("{+var}", """["value"]""")]
    [InlineData("{+hello}", """["Hello%20World!"]""")]
    [InlineData("{+path}/here", """["/foo/bar/here"]""")]
    [InlineData("here?ref={+path}", """["here?ref=/foo/bar"]""")]
    public void Level2Examples(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, Level2Variables());
    }

    [Theory]
    [InlineData("map?{x,y}", """["map?1024,768"]""")]
    [InlineData("{x,hello,y}", """["1024,Hello%20World%21,768"]""")]
    [InlineData("{+x,hello,y}", """["1024,Hello%20World!,768"]""")]
    [InlineData("{+path,x}/here", """["/foo/bar,1024/here"]""")]
    [InlineData("{#x,hello,y}", """["#1024,Hello%20World!,768"]""")]
    [InlineData("{#path,x}/here", """["#/foo/bar,1024/here"]""")]
    [InlineData("X{.var}", """["X.value"]""")]
    [InlineData("X{.x,y}", """["X.1024.768"]""")]
    [InlineData("{/var}", """["/value"]""")]
    [InlineData("{/var,x}/here", """["/value/1024/here"]""")]
    [InlineData("{;x,y}", """[";x=1024;y=768"]""")]
    [InlineData("{;x,y,empty}", """[";x=1024;y=768;empty"]""")]
    [InlineData("{?x,y}", """["?x=1024\u0026y=768"]""")]
    [InlineData("{?x,y,empty}", """["?x=1024\u0026y=768\u0026empty="]""")]
    [InlineData("?fixed=yes{&x}", """["?fixed=yes\u0026x=1024"]""")]
    [InlineData("{&x,y,empty}", """["\u0026x=1024\u0026y=768\u0026empty="]""")]
    public void Level3Examples(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, Level3Variables());
    }

    [Theory]
    [InlineData("{var:3}", """["val"]""")]
    [InlineData("{var:30}", """["value"]""")]
    [InlineData("{list}", """["red,green,blue"]""")]
    [InlineData("{list*}", """["red,green,blue"]""")]
    [InlineData("{keys}", """["comma,%2C,dot,.,semi,%3B","comma,%2C,semi,%3B,dot,.","dot,.,comma,%2C,semi,%3B","dot,.,semi,%3B,comma,%2C","semi,%3B,comma,%2C,dot,.","semi,%3B,dot,.,comma,%2C"]""")]
    [InlineData("{keys*}", """["comma=%2C,dot=.,semi=%3B","comma=%2C,semi=%3B,dot=.","dot=.,comma=%2C,semi=%3B","dot=.,semi=%3B,comma=%2C","semi=%3B,comma=%2C,dot=.","semi=%3B,dot=.,comma=%2C"]""")]
    [InlineData("{+path:6}/here", """["/foo/b/here"]""")]
    [InlineData("{+list}", """["red,green,blue"]""")]
    [InlineData("{+list*}", """["red,green,blue"]""")]
    [InlineData("{+keys}", """["comma,,,dot,.,semi,;","comma,,,semi,;,dot,.","dot,.,comma,,,semi,;","dot,.,semi,;,comma,,","semi,;,comma,,,dot,.","semi,;,dot,.,comma,,"]""")]
    [InlineData("{+keys*}", """["comma=,,dot=.,semi=;","comma=,,semi=;,dot=.","dot=.,comma=,,semi=;","dot=.,semi=;,comma=,","semi=;,comma=,,dot=.","semi=;,dot=.,comma=,"]""")]
    [InlineData("{#path:6}/here", """["#/foo/b/here"]""")]
    [InlineData("{#list}", """["#red,green,blue"]""")]
    [InlineData("{#list*}", """["#red,green,blue"]""")]
    [InlineData("{#keys}", """["#comma,,,dot,.,semi,;","#comma,,,semi,;,dot,.","#dot,.,comma,,,semi,;","#dot,.,semi,;,comma,,","#semi,;,comma,,,dot,.","#semi,;,dot,.,comma,,"]""")]
    [InlineData("{#keys*}", """["#comma=,,dot=.,semi=;","#comma=,,semi=;,dot=.","#dot=.,comma=,,semi=;","#dot=.,semi=;,comma=,","#semi=;,comma=,,dot=.","#semi=;,dot=.,comma=,"]""")]
    [InlineData("X{.var:3}", """["X.val"]""")]
    [InlineData("X{.list}", """["X.red,green,blue"]""")]
    [InlineData("X{.list*}", """["X.red.green.blue"]""")]
    [InlineData("X{.keys}", """["X.comma,%2C,dot,.,semi,%3B","X.comma,%2C,semi,%3B,dot,.","X.dot,.,comma,%2C,semi,%3B","X.dot,.,semi,%3B,comma,%2C","X.semi,%3B,comma,%2C,dot,.","X.semi,%3B,dot,.,comma,%2C"]""")]
    [InlineData("{/var:1,var}", """["/v/value"]""")]
    [InlineData("{/list}", """["/red,green,blue"]""")]
    [InlineData("{/list*}", """["/red/green/blue"]""")]
    [InlineData("{/list*,path:4}", """["/red/green/blue/%2Ffoo"]""")]
    [InlineData("{/keys}", """["/comma,%2C,dot,.,semi,%3B","/comma,%2C,semi,%3B,dot,.","/dot,.,comma,%2C,semi,%3B","/dot,.,semi,%3B,comma,%2C","/semi,%3B,comma,%2C,dot,.","/semi,%3B,dot,.,comma,%2C"]""")]
    [InlineData("{/keys*}", """["/comma=%2C/dot=./semi=%3B","/comma=%2C/semi=%3B/dot=.","/dot=./comma=%2C/semi=%3B","/dot=./semi=%3B/comma=%2C","/semi=%3B/comma=%2C/dot=.","/semi=%3B/dot=./comma=%2C"]""")]
    [InlineData("{;hello:5}", """[";hello=Hello"]""")]
    [InlineData("{;list}", """[";list=red,green,blue"]""")]
    [InlineData("{;list*}", """[";list=red;list=green;list=blue"]""")]
    [InlineData("{;keys}", """[";keys=comma,%2C,dot,.,semi,%3B",";keys=comma,%2C,semi,%3B,dot,.",";keys=dot,.,comma,%2C,semi,%3B",";keys=dot,.,semi,%3B,comma,%2C",";keys=semi,%3B,comma,%2C,dot,.",";keys=semi,%3B,dot,.,comma,%2C"]""")]
    [InlineData("{;keys*}", """[";comma=%2C;dot=.;semi=%3B",";comma=%2C;semi=%3B;dot=.",";dot=.;comma=%2C;semi=%3B",";dot=.;semi=%3B;comma=%2C",";semi=%3B;comma=%2C;dot=.",";semi=%3B;dot=.;comma=%2C"]""")]
    [InlineData("{?var:3}", """["?var=val"]""")]
    [InlineData("{?list}", """["?list=red,green,blue"]""")]
    [InlineData("{?list*}", """["?list=red\u0026list=green\u0026list=blue"]""")]
    [InlineData("{?keys}", """["?keys=comma,%2C,dot,.,semi,%3B","?keys=comma,%2C,semi,%3B,dot,.","?keys=dot,.,comma,%2C,semi,%3B","?keys=dot,.,semi,%3B,comma,%2C","?keys=semi,%3B,comma,%2C,dot,.","?keys=semi,%3B,dot,.,comma,%2C"]""")]
    [InlineData("{?keys*}", """["?comma=%2C\u0026dot=.\u0026semi=%3B","?comma=%2C\u0026semi=%3B\u0026dot=.","?dot=.\u0026comma=%2C\u0026semi=%3B","?dot=.\u0026semi=%3B\u0026comma=%2C","?semi=%3B\u0026comma=%2C\u0026dot=.","?semi=%3B\u0026dot=.\u0026comma=%2C"]""")]
    [InlineData("{&var:3}", """["\u0026var=val"]""")]
    [InlineData("{&list}", """["\u0026list=red,green,blue"]""")]
    [InlineData("{&list*}", """["\u0026list=red\u0026list=green\u0026list=blue"]""")]
    [InlineData("{&keys}", """["\u0026keys=comma,%2C,dot,.,semi,%3B","\u0026keys=comma,%2C,semi,%3B,dot,.","\u0026keys=dot,.,comma,%2C,semi,%3B","\u0026keys=dot,.,semi,%3B,comma,%2C","\u0026keys=semi,%3B,comma,%2C,dot,.","\u0026keys=semi,%3B,dot,.,comma,%2C"]""")]
    [InlineData("{&keys*}", """["\u0026comma=%2C\u0026dot=.\u0026semi=%3B","\u0026comma=%2C\u0026semi=%3B\u0026dot=.","\u0026dot=.\u0026comma=%2C\u0026semi=%3B","\u0026dot=.\u0026semi=%3B\u0026comma=%2C","\u0026semi=%3B\u0026comma=%2C\u0026dot=.","\u0026semi=%3B\u0026dot=.\u0026comma=%2C"]""")]
    public void Level4Examples(string template, string expectedResultsJson)
    {
        AssertResolveResult(template, expectedResultsJson, Level4Variables());
    }
}