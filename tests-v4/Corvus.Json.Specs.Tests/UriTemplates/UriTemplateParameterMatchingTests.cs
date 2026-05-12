// <copyright file="UriTemplateParameterMatchingTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Corvus.Json;
using Corvus.Json.UriTemplates;
using Corvus.UriTemplates;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.UriTemplates;

[TestClass]
public class UriTemplateParameterMatchingTests
{
    [TestMethod]
    public void MatchUriToTemplate()
    {
        Regex regex = new(UriTemplateRegexBuilder.CreateMatchingRegex("http://example.com/{p1}/{p2}"));
        StringAssert.Matches("http://example.com/foo/bar", regex);
    }

    [TestMethod]
    public void GetParameters()
    {
        Regex regex = new(UriTemplateRegexBuilder.CreateMatchingRegex("http://example.com/{p1}/{p2}"));
        Match match = regex.Match("http://example.com/foo/bar");
        Assert.AreEqual("foo", match.Groups["p1"].Value);
        Assert.AreEqual("bar", match.Groups["p2"].Value);
    }

    [TestMethod]
    public void GetParametersWithOperators()
    {
        UriTemplate template = new("http://example.com/{+p1}/{p2*}");
        Assert.IsTrue(template.TryGetParameters(new Uri("http://example.com/foo/bar", UriKind.RelativeOrAbsolute), out ImmutableDictionary<string, JsonAny>? parameters));
        Assert.AreEqual(2, parameters!.Count);
        Assert.AreEqual(JsonAny.Parse("\"foo\""), parameters["p1"]);
        Assert.AreEqual(JsonAny.Parse("\"bar\""), parameters["p2"]);
    }

    [TestMethod]
    public void GetParametersFromQueryString()
    {
        UriTemplate template = new("http://example.com/{+p1}/{p2*}{?blur}");
        Assert.IsTrue(template.TryGetParameters(new Uri("http://example.com/foo/bar?blur=45", UriKind.RelativeOrAbsolute), out ImmutableDictionary<string, JsonAny>? parameters));
        Assert.AreEqual(3, parameters!.Count);
        Assert.AreEqual(JsonAny.Parse("\"foo\""), parameters["p1"]);
        Assert.AreEqual(JsonAny.Parse("\"bar\""), parameters["p2"]);
        Assert.AreEqual(JsonAny.Parse("45"), parameters["blur"]);
    }

    [TestMethod]
    public void GetParametersFromMultipleQueryString()
    {
        UriTemplate template = new("http://example.com/{+p1}/{p2*}{?blur,blob}");
        Assert.IsTrue(template.TryGetParameters(new Uri("http://example.com/foo/bar?blur=45", UriKind.RelativeOrAbsolute), out ImmutableDictionary<string, JsonAny>? parameters));
        Assert.AreEqual(3, parameters!.Count);
        Assert.AreEqual(JsonAny.Parse("\"foo\""), parameters["p1"]);
        Assert.AreEqual(JsonAny.Parse("\"bar\""), parameters["p2"]);
        Assert.AreEqual(JsonAny.Parse("45"), parameters["blur"]);
    }

    [TestMethod]
    public void GetParametersFromMultipleQueryStringWithTwoParameterValues()
    {
        UriTemplate template = new("http://example.com/{+p1}/{p2*}{?blur,blob}");
        Assert.IsTrue(template.TryGetParameters(new Uri("http://example.com/foo/bar?blur=45&blob=23", UriKind.RelativeOrAbsolute), out ImmutableDictionary<string, JsonAny>? parameters));
        Assert.AreEqual(4, parameters!.Count);
        Assert.AreEqual(JsonAny.Parse("\"foo\""), parameters["p1"]);
        Assert.AreEqual(JsonAny.Parse("\"bar\""), parameters["p2"]);
        Assert.AreEqual(JsonAny.Parse("45"), parameters["blur"]);
        Assert.AreEqual(JsonAny.Parse("23"), parameters["blob"]);
    }

    [TestMethod]
    public void GetParametersFromMultipleQueryStringWithOptionalAndMandatoryParameters()
    {
        UriTemplate template = new("http://example.com/{+p1}/{p2*}{?blur}{&blob}");
        Assert.IsTrue(template.TryGetParameters(new Uri("http://example.com/foo/bar?blur=45&blob=23", UriKind.RelativeOrAbsolute), out ImmutableDictionary<string, JsonAny>? parameters));
        Assert.AreEqual(4, parameters!.Count);
        Assert.AreEqual(JsonAny.Parse("\"foo\""), parameters["p1"]);
        Assert.AreEqual(JsonAny.Parse("\"bar\""), parameters["p2"]);
        Assert.AreEqual(JsonAny.Parse("45"), parameters["blur"]);
        Assert.AreEqual(JsonAny.Parse("23"), parameters["blob"]);
    }

    [TestMethod]
    public void GetParametersFromMultipleQueryStringWithOptionalParameters()
    {
        UriTemplate template = new("http://example.com/{+p1}/{p2*}{?blur,blob}");
        Assert.IsTrue(template.TryGetParameters(new Uri("http://example.com/foo/bar", UriKind.RelativeOrAbsolute), out ImmutableDictionary<string, JsonAny>? parameters));
        Assert.AreEqual(2, parameters!.Count);
        Assert.AreEqual(JsonAny.Parse("\"foo\""), parameters["p1"]);
        Assert.AreEqual(JsonAny.Parse("\"bar\""), parameters["p2"]);
    }

    [TestMethod]
    public void GlimpseUrl()
    {
        UriTemplate template = new("http://example.com/Glimpse.axd?n=glimpse_ajax&parentRequestId={parentRequestId}{&hash,callback}");
        Assert.IsTrue(template.TryGetParameters(new Uri("http://example.com/Glimpse.axd?n=glimpse_ajax&parentRequestId=123232323&hash=23ADE34FAE&callback=http%3A%2F%2Fexample.com%2Fcallback", UriKind.RelativeOrAbsolute), out ImmutableDictionary<string, JsonAny>? parameters));
        Assert.AreEqual(3, parameters!.Count);
        Assert.AreEqual(JsonAny.Parse("123232323"), parameters["parentRequestId"]);
        Assert.AreEqual(JsonAny.Parse("\"23ADE34FAE\""), parameters["hash"]);
        Assert.AreEqual(JsonAny.Parse("\"http://example.com/callback\""), parameters["callback"]);
    }

    [TestMethod]
    public void UrlWithQuestionMarkAsFirstCharacter()
    {
        UriTemplate template = new("?hash={hash}");
        Assert.IsTrue(template.TryGetParameters(new Uri("http://localhost:5000/glimpse/metadata?hash=123", UriKind.RelativeOrAbsolute), out ImmutableDictionary<string, JsonAny>? parameters));
        Assert.AreEqual(1, (parameters!).Count());
        Assert.AreEqual(JsonAny.Parse("123"), parameters["hash"]);
    }

    [TestMethod]
    public void Level1Decode()
    {
        UriTemplate template = new("/{p1}");
        Assert.IsTrue(template.TryGetParameters(new Uri("/Hello%20World", UriKind.RelativeOrAbsolute), out ImmutableDictionary<string, JsonAny>? parameters));
        Assert.AreEqual(1, (parameters!).Count());
        Assert.AreEqual(JsonAny.Parse("\"Hello World\""), parameters["p1"]);
    }

    [TestMethod]
    public void FragmentParameter()
    {
        UriTemplate template = new("/foo{#p1}");
        Assert.IsTrue(template.TryGetParameters(new Uri("/foo#Hello%20World!", UriKind.RelativeOrAbsolute), out ImmutableDictionary<string, JsonAny>? parameters));
        Assert.AreEqual(1, (parameters!).Count());
        Assert.AreEqual(JsonAny.Parse("\"Hello World!\""), parameters["p1"]);
    }

    [TestMethod]
    public void FragmentParameters()
    {
        UriTemplate template = new("/foo{#p1,p2}");
        Assert.IsTrue(template.TryGetParameters(new Uri("/foo#Hello%20World!,blurg", UriKind.RelativeOrAbsolute), out ImmutableDictionary<string, JsonAny>? parameters));
        Assert.AreEqual(2, parameters!.Count);
        Assert.AreEqual(JsonAny.Parse("\"Hello World!\""), parameters["p1"]);
        Assert.AreEqual(JsonAny.Parse("\"blurg\""), parameters["p2"]);
    }

    [TestMethod]
    public void OptionalPathParameter()
    {
        UriTemplate template = new("/foo{/bar}/bob");
        Assert.IsTrue(template.TryGetParameters(new Uri("/foo/yuck/bob", UriKind.RelativeOrAbsolute), out ImmutableDictionary<string, JsonAny>? parameters));
        Assert.AreEqual(1, (parameters!).Count());
        Assert.AreEqual(JsonAny.Parse("\"yuck\""), parameters["bar"]);
    }

    [TestMethod]
    public void OptionalPathParameterWithMultipleValues()
    {
        UriTemplate template = new("/foo{/bar,baz}/bob");
        Assert.IsTrue(template.TryGetParameters(new Uri("/foo/yuck/yob/bob", UriKind.RelativeOrAbsolute), out ImmutableDictionary<string, JsonAny>? parameters));
        Assert.AreEqual(2, parameters!.Count);
        Assert.AreEqual(JsonAny.Parse("\"yuck\""), parameters["bar"]);
        Assert.AreEqual(JsonAny.Parse("\"yob\""), parameters["baz"]);
    }

    [TestMethod]
    public void OptionalPathParameterWithMultipleOptionalValuesOnlyProvidingOne()
    {
        UriTemplate template = new("/foo{/bar,baz}/bob");
        Assert.IsTrue(template.TryGetParameters(new Uri("/foo/yuck/bob", UriKind.RelativeOrAbsolute), out ImmutableDictionary<string, JsonAny>? parameters));
        Assert.AreEqual(1, (parameters!).Count());
        Assert.AreEqual(JsonAny.Parse("\"yuck\""), parameters["bar"]);
    }

    [TestMethod]
    public void UpdatePathParameter()
    {
        UriTemplate template = new("http://example.org/{tenant}/customers");
        template = template.SetParameter("tenant", "acm\u00E9");
        string resolved = template.Resolve();

        string[] expected = ["http://example.org/acm%C3%A9/customers"];
        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void QueryParametersTheOldWay()
    {
        UriTemplate template = new("http://example.org/customers?active={activeFlag}");
        template = template.SetParameter("activeFlag", true);
        string resolved = template.Resolve();

        string[] expected = ["http://example.org/customers?active=true"];
        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void QueryParametersTheNewWay()
    {
        UriTemplate template = new("http://example.org/customers{?active}");
        template = template.SetParameter("active", true);
        string resolved = template.Resolve();

        string[] expected = ["http://example.org/customers?active=true"];
        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void QueryParametersTheNewWayWithoutValue()
    {
        UriTemplate template = new("http://example.org/customers{?active}");
        string resolved = template.Resolve();

        string[] expected = ["http://example.org/customers"];
        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void ShouldResolveUriTemplateWithNonStringParameters()
    {
        UriTemplate template = new("http://example.org/location{?lat,lng}");
        template = template.SetParameters(
            [
                ("lat", JsonAny.Parse("31.464")),
                ("lng", JsonAny.Parse("74.386")),
            ]);
        string resolved = template.Resolve();

        string[] expected =
        [
            "http://example.org/location?lng=74.386&lat=31.464",
            "http://example.org/location?lat=31.464&lng=74.386",
        ];

        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void ParametersFromJsonObject()
    {
        UriTemplate template = new("http://example.org/{environment}/{version}/customers{?active,country}");
        template = template.SetParameters(JsonObject.Parse("{\"environment\": \"dev\", \"version\": \"v2\", \"active\": true, \"country\": \"CA\" }"));
        string resolved = template.Resolve();

        string[] expected =
        [
            "http://example.org/dev/v2/customers?active=true&country=CA",
            "http://example.org/dev/v2/customers?country=CA&active=true",
        ];

        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void SomeParametersFromJsonObject()
    {
        UriTemplate template = new("http://example.org{/environment}/{version}/customers{?active,country}");
        template = template.SetParameters(JsonObject.Parse("{\"version\": \"v2\", \"active\": true }"));
        string resolved = template.Resolve();

        string[] expected = ["http://example.org/v2/customers?active=true"];
        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void AddJsonObjectToQueryParameter()
    {
        UriTemplate template = new("http://example.org/foo{?coords*}");
        template = template.SetParameter("coords", JsonAny.Parse("{\"x\": 1, \"y\": 2 }"));
        string resolved = template.Resolve();

        string[] expected =
        [
            "http://example.org/foo?x=1&y=2",
            "http://example.org/foo?y=2&x=1",
        ];

        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void ApplyParametersFromJsonObjectToPathSegment()
    {
        UriTemplate template = new("http://example.org/foo/{bar}/baz");
        template = template.SetParameters(JsonObject.Parse("{\"bar\": \"yo\" }"));
        string resolved = template.Resolve();

        string[] expected = ["http://example.org/foo/yo/baz"];
        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void ExtremeEncoding()
    {
        UriTemplate template = new("http://example.org/sparql{?query}");
        template = template.SetParameter("query", "PREFIX dc: <http://purl.org/dc/elements/1.1/> SELECT ?book ?who WHERE { ?book dc:creator ?who }");
        string resolved = template.Resolve();

        string[] expected = ["http://example.org/sparql?query=PREFIX%20dc%3A%20%3Chttp%3A%2F%2Fpurl.org%2Fdc%2Felements%2F1.1%2F%3E%20SELECT%20%3Fbook%20%3Fwho%20WHERE%20%7B%20%3Fbook%20dc%3Acreator%20%3Fwho%20%7D"];
        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void ApplyParametersFromJsonObjectWithList()
    {
        UriTemplate template = new("http://example.org/customers{?ids,order}");
        template = template.SetParameters(JsonObject.Parse("{\"order\": \"up\", \"ids\": [\"21\", \"75\", \"21\"] }"));
        string resolved = template.Resolve();

        string[] expected =
        [
            "http://example.org/customers?ids=21,75,21&order=up",
            "http://example.org/customers?order=up&ids=21,75,21",
        ];

        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void ApplyParametersFromJsonObjectWithListOfInts()
    {
        UriTemplate template = new("http://example.org/customers{?ids,order}");
        template = template.SetParameters(JsonObject.Parse("{\"order\": \"up\", \"ids\": [21, 75, 21] }"));
        string resolved = template.Resolve();

        string[] expected =
        [
            "http://example.org/customers?ids=21,75,21&order=up",
            "http://example.org/customers?order=up&ids=21,75,21",
        ];

        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void ApplyParametersFromJsonObjectWithListOfIntsExploded()
    {
        UriTemplate template = new("http://example.org/customers{?ids*,order}");
        template = template.SetParameters(JsonObject.Parse("{\"order\": \"up\", \"ids\": [21, 75, 21] }"));
        string resolved = template.Resolve();

        string[] expected =
        [
            "http://example.org/customers?ids=21&ids=75&ids=21&order=up",
            "http://example.org/customers?order=up&ids=21&ids=75&ids=21",
        ];

        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void ApplyFoldersFromJsonObjectToPath()
    {
        UriTemplate template = new("http://example.org/files{/folders*}{?filename}");
        template = template.SetParameters(JsonObject.Parse("{\"filename\": \"proposal.pdf\", \"folders\": [\"customer\", \"project\"] }"));
        string resolved = template.Resolve();

        string[] expected = ["http://example.org/files/customer/project?filename=proposal.pdf"];
        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void ApplyFoldersFromJsonObjectToPathFromStringNotUrl()
    {
        UriTemplate template = new("http://example.org{/folders*}{?filename}");
        template = template.SetParameters(JsonObject.Parse("{\"filename\": \"proposal.pdf\", \"folders\": [\"files\", \"customer\", \"project\"] }"));
        string resolved = template.Resolve();

        string[] expected = ["http://example.org/files/customer/project?filename=proposal.pdf"];
        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void ParametersFromJsonObjectFromInvalidUrl()
    {
        UriTemplate template = new("http://{environment}.example.org/{version}/customers{?active,country}");
        template = template.SetParameters(JsonObject.Parse("{\"environment\": \"dev\", \"version\": \"v2\", \"active\": true, \"country\": \"CA\" }"));
        string resolved = template.Resolve();

        string[] expected =
        [
            "http://dev.example.org/v2/customers?active=true&country=CA",
            "http://example.org/dev/v2/customers?country=CA&active=true",
        ];

        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void ReplaceBaseAddress()
    {
        UriTemplate template = new("{+baseUrl}api/customer/{id}");
        template = template.SetParameters(JsonObject.Parse("{\"baseUrl\": \"http://example.org/\", \"id\": \"22\" }"));
        string resolved = template.Resolve();

        string[] expected = ["http://example.org/api/customer/22"];
        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void ReplaceBaseAddressButNotId()
    {
        UriTemplate template = new("{+baseUrl}api/customer/{id}", resolvePartially: true);
        template = template.SetParameters(JsonObject.Parse("{\"baseUrl\": \"http://example.org/\" }"));
        string resolved = template.Resolve();

        string[] expected = ["http://example.org/api/customer/{id}"];
        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void PartiallyApplyParametersFromJsonObjectFromInvalidUrl()
    {
        UriTemplate template = new("http://{environment}.example.org/{version}/customers{?active,country}", resolvePartially: true);
        template = template.SetParameters(JsonObject.Parse("{\"environment\": \"dev\", \"version\": \"v2\"}"));
        string resolved = template.Resolve();

        string[] expected =
        [
            "http://dev.example.org/v2/customers{?active,country}",
            "http://dev.example.org/v2/customers{?active}{&country}",
            "http://dev.example.org/v2/customers{?country}{&active}",
        ];

        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void PartiallyApplyParametersFromJsonObjectToPathFromStringNotUrl()
    {
        UriTemplate template = new("http://example.org{/folders*}{?filename}", resolvePartially: true);
        template = template.SetParameters(JsonObject.Parse("{\"filename\": \"proposal.pdf\" }"));
        string resolved = template.Resolve();

        string[] expected = ["http://example.org{/folders*}?filename=proposal.pdf"];
        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void AddMultipleParametersToLink()
    {
        UriTemplate template = new("http://localhost/api/{dataset}/customer{?foo,bar,baz}");
        template = template.SetParameters(
            [
                ("foo", JsonAny.Parse("\"bar\"")),
                ("baz", JsonAny.Parse("99")),
                ("dataset", JsonAny.Parse("\"bob\"")),
            ]);
        string resolved = template.Resolve();

        string[] expected =
        [
            "http://localhost/api/bob/customer?foo=bar&baz=99",
            "http://localhost/api/bob/customer?baz=99&foo=bar",
        ];

        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void SetTemplateParametersForInt()
    {
        UriTemplate template = new("http://example.org/location{?value}");
        template = template.SetParameter("value", 3);
        string resolved = template.Resolve();

        string[] expected = ["http://example.org/location?value=3"];
        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void SetTemplateParametersForDouble()
    {
        UriTemplate template = new("http://example.org/location{?value}");
        template = template.SetParameter("value", 3.3);
        string resolved = template.Resolve();

        string[] expected = ["http://example.org/location?value=3.3"];
        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void SetTemplateParametersForBoolean()
    {
        UriTemplate template = new("http://example.org/location{?value}");
        template = template.SetParameter("value", true);
        string resolved = template.Resolve();

        string[] expected = ["http://example.org/location?value=true"];
        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void SetTemplateParametersForString()
    {
        UriTemplate template = new("http://example.org/location{?value}");
        template = template.SetParameter("value", "SomeString");
        string resolved = template.Resolve();

        string[] expected = ["http://example.org/location?value=SomeString"];
        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void SetTemplateParametersForFloat()
    {
        UriTemplate template = new("http://example.org/location{?value}");
        template = template.SetParameter("value", 3.3f);
        string resolved = template.Resolve();

        string[] expected =
        [
            "http://example.org/location?value=3.29999995231628",
            "http://example.org/location?value=3.299999952316284",
        ];

        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void SetTemplateParametersForLong()
    {
        UriTemplate template = new("http://example.org/location{?value}");
        template = template.SetParameter("value", 333L);
        string resolved = template.Resolve();

        string[] expected = ["http://example.org/location?value=333"];
        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void SetTemplateParametersForDictionaryOfStrings()
    {
        UriTemplate template = new("http://example.org/location{?value*}");
        template = template.SetParameter("value", new Dictionary<string, string>
        {
            ["foo"] = "bar",
            ["bar"] = "baz",
            ["baz"] = "bob",
        });
        string resolved = template.Resolve();

        string[] expected =
        [
            "http://example.org/location?foo=bar&bar=baz&baz=bob",
            "http://example.org/location?foo=bar&baz=bob&bar=baz",
            "http://example.org/location?baz=bob&bar=baz&foo=bar",
            "http://example.org/location?baz=bob&foo=bar&bar=baz",
            "http://example.org/location?bar=baz&baz=bob&foo=bar",
            "http://example.org/location?bar=baz&foo=bar&baz=bob",
        ];

        CollectionAssert.Contains(expected, resolved);
    }

    [TestMethod]
    public void SetTemplateParametersForArrayOfStrings()
    {
        UriTemplate template = new("http://example.org/location{?value*}");
        template = template.SetParameter("value", new[] { "bar", "baz", "bob" }.AsEnumerable());
        string resolved = template.Resolve();

        string[] expected =
        [
            "http://example.org/location?value=bar&value=baz&value=bob",
            "http://example.org/location?value=bar&value=bob&value=baz",
            "http://example.org/location?value=baz&value=bob&value=bar",
            "http://example.org/location?value=baz&value=bar&value=bob",
            "http://example.org/location?value=bob&value=baz&value=bar",
            "http://example.org/location?value=bob&value=bar&value=baz",
        ];

        CollectionAssert.Contains(expected, resolved);
    }
}