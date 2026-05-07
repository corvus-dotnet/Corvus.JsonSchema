// <copyright file="UriTemplateNegativeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using Corvus.Json;
using Corvus.Json.UriTemplates;
using Xunit;

namespace Corvus.Json.Specs.Tests.UriTemplates;

public class UriTemplateNegativeTests
{
    private static ImmutableDictionary<string, JsonAny> BuildVariables()
    {
        ImmutableDictionary<string, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<string, JsonAny>();
        builder.Add("id", JsonAny.Parse("\"thing\""));
        builder.Add("var", JsonAny.Parse("\"value\""));
        builder.Add("hello", JsonAny.Parse("\"Hello World!\""));
        builder.Add("with space", JsonAny.Parse("\"fail\""));
        builder.Add("leading_space", JsonAny.Parse("\"Hi!\""));
        builder.Add("trailing_space", JsonAny.Parse("\"Bye!\""));
        builder.Add("empty", JsonAny.Parse("\"\""));
        builder.Add("path", JsonAny.Parse("\"/foo/bar\""));
        builder.Add("x", JsonAny.Parse("\"1024\""));
        builder.Add("y", JsonAny.Parse("\"768\""));
        builder.Add("list", JsonAny.Parse("[\"red\",\"green\",\"blue\"]"));
        builder.Add("keys", JsonAny.Parse("{\"semi\":\";\",\"dot\":\".\",\"comma\":\",\"}"));
        builder.Add("example", JsonAny.Parse("\"red\""));
        builder.Add("searchTerms", JsonAny.Parse("\"uri templates\""));
        builder.Add("~thing", JsonAny.Parse("\"some-user\""));
        builder.Add("default-graph-uri", JsonAny.Parse("[\"http://www.example/book/\",\"http://www.example/papers/\"]"));
        builder.Add("query", JsonAny.Parse("\"PREFIX dc: \\u003Chttp://purl.org/dc/elements/1.1/\\u003E SELECT ?book ?who WHERE { ?book dc:creator ?who }\""));
        return builder.ToImmutable();
    }

    [Theory]
    [InlineData("{/id*")]
    [InlineData("/id*}")]
    [InlineData("{/?id}")]
    [InlineData("{var:prefix}")]
    [InlineData("{hello:2*}")]
    [InlineData("{??hello}")]
    [InlineData("{!hello}")]
    [InlineData("{with space}")]
    [InlineData("{ leading_space}")]
    [InlineData("{trailing_space }")]
    [InlineData("{=path}")]
    [InlineData("{$var}")]
    [InlineData("{|var*}")]
    [InlineData("{*keys?}")]
    [InlineData("{?empty=default,var}")]
    [InlineData("{var}{-prefix|/-/|var}")]
    [InlineData("?q={searchTerms}&amp;c={example:color?}")]
    [InlineData("x{?empty|foo=none}")]
    [InlineData("/h{#hello+}")]
    [InlineData("/h#{hello+}")]
    [InlineData("{keys:1}")]
    [InlineData("{+keys:1}")]
    [InlineData("{;keys:1*}")]
    [InlineData("?{-join|&|var,list}")]
    [InlineData("/people/{~thing}")]
    [InlineData("/{default-graph-uri}")]
    [InlineData("/sparql{?query,default-graph-uri}")]
    [InlineData("/sparql{?query){&default-graph-uri*}")]
    [InlineData("/resolution{?x, y}")]
    public void FailureTests(string template)
    {
        ImmutableDictionary<string, JsonAny> variables = BuildVariables();

        Assert.ThrowsAny<Exception>(() =>
        {
            var uriTemplate = new UriTemplate(template, createParameterParser: false, parameters: variables);
            uriTemplate.Resolve();
        });
    }
}