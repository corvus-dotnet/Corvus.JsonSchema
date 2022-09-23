using System;
using System.Text.RegularExpressions;
using Corvus.UriTemplates.TavisApi;
using Xunit;

namespace UriTemplateTests
{
    public class ParameterMatchingTests
    {
        

        [Fact]
        public void MatchUriToTemplate()
        {
            var uri = new Uri("http://example.com/foo/bar");

            var sTemplate = "http://example.com/{p1}/{p2}";

            var x = UriTemplate.CreateMatchingRegex(sTemplate);

            var match = Regex.IsMatch(uri.AbsoluteUri,x);
            Assert.True(match);
        }

        [Fact]
        public void GetParameters()
        {
            var uri = new Uri("http://example.com/foo/bar");

            var sTemplate = "http://example.com/{p1}/{p2}";

            var x = UriTemplate.CreateMatchingRegex(sTemplate);
            var regex = new Regex(x);

            var match = regex.Match(uri.AbsoluteUri);

            Assert.Equal("foo",match.Groups["p1"].Value);
            Assert.Equal("bar", match.Groups["p2"].Value);
        }

        [Fact]
        public void GetParametersWithOperators()
        {
            var uri = new Uri("http://example.com/foo/bar");

            var template = new UriTemplate("http://example.com/{+p1}/{p2*}");

            var parameters = template.GetParameters(uri);

            Assert.Equal(2, parameters.Count);
            Assert.Equal("foo", parameters["p1"]);
            Assert.Equal("bar", parameters["p2"]);
        }

        [Fact]
        public void GetParametersFromQueryString()
        {
            var uri = new Uri("http://example.com/foo/bar?blur=45");

            var template = new UriTemplate("http://example.com/{+p1}/{p2*}{?blur}");

            var parameters = template.GetParameters(uri);

            Assert.Equal(3, parameters.Count);

            Assert.Equal("foo", parameters["p1"]);
            Assert.Equal("bar", parameters["p2"]);
            Assert.Equal("45", parameters["blur"]);
        }

        [Fact]
        public void GetParametersFromMultipleQueryString()
        {
            var uri = new Uri("http://example.com/foo/bar?blur=45");

            var template = new UriTemplate("http://example.com/{+p1}/{p2*}{?blur,blob}");

            var parameters = template.GetParameters(uri);

            Assert.Equal(3, parameters.Count);
            Assert.Equal("foo", parameters["p1"]);
            Assert.Equal("bar", parameters["p2"]);
            Assert.Equal("45", parameters["blur"]);

        }
        [Fact]
        public void GetParametersFromMultipleQueryStringWithTwoParamValues()
        {
            var uri = new Uri("http://example.com/foo/bar?blur=45&blob=23");

            var template = new UriTemplate("http://example.com/{+p1}/{p2*}{?blur,blob}");

            var parameters = template.GetParameters(uri);

            Assert.Equal(4, parameters.Count);
            Assert.Equal("foo", parameters["p1"]);
            Assert.Equal("bar", parameters["p2"]);
            Assert.Equal("45", parameters["blur"]);
            Assert.Equal("23", parameters["blob"]);

        }

        [Fact]
        public void GetParameterFromArrayParameter()
        {
            var uri = new Uri("http://example.com?blur=45,23");

            var template = new UriTemplate("http://example.com{?blur}");

            var parameters = template.GetParameters(uri);

            Assert.Equal(1, parameters.Count);
            Assert.Equal("45,23", parameters["blur"]);

        }

        [Fact]
        public void GetParametersFromMultipleQueryStringWithOptionalAndMandatoryParameters()
        {
            var uri = new Uri("http://example.com/foo/bar?blur=45&blob=23");

            var template = new UriTemplate("http://example.com/{+p1}/{p2*}{?blur}{&blob}");

            var parameters = template.GetParameters(uri);

            Assert.Equal(4, parameters.Count);
            Assert.Equal("foo", parameters["p1"]);
            Assert.Equal("bar", parameters["p2"]);
            Assert.Equal("45", parameters["blur"]);
            Assert.Equal("23", parameters["blob"]);

        }

        [Fact]
        public void GetParametersFromMultipleQueryStringWithOptionalParameters()
        {
            var uri = new Uri("http://example.com/foo/bar");

            var template = new UriTemplate("http://example.com/{+p1}/{p2*}{?blur,blob}");

            var parameters = template.GetParameters(uri);

            Assert.Equal("foo", parameters["p1"]);
            Assert.Equal("bar", parameters["p2"]);

        }


        [Fact]
        public void TestGlimpseUrl()
        {
            var uri = new Uri("http://example.com/Glimpse.axd?n=glimpse_ajax&parentRequestId=123232323&hash=23ADE34FAE&callback=http%3A%2F%2Fexample.com%2Fcallback");

            var template = new UriTemplate("http://example.com/Glimpse.axd?n=glimpse_ajax&parentRequestId={parentRequestId}{&hash,callback}");

            var parameters = template.GetParameters(uri);

            Assert.Equal(3, parameters.Count);
            Assert.Equal("123232323", parameters["parentRequestId"]);
            Assert.Equal("23ADE34FAE", parameters["hash"]);
            Assert.Equal("http://example.com/callback", parameters["callback"]);

        }

        [Fact]
        public void TestUrlWithQuestionMarkAsFirstCharacter()
        {

            var parameters = new UriTemplate("?hash={hash}").GetParameters(new Uri("http://localhost:5000/glimpse/metadata?hash=123"));;

            Assert.Equal(1, parameters.Count);
            Assert.Equal("123", parameters["hash"]);

        }

        

        [Fact]
        public void TestExactParameterCount()
        {
            var uri = new Uri("http://example.com/foo?bar=10");

            var template = new UriTemplate("http://example.com/foo{?bar}");

            var parameters = template.GetParameters(uri);

            Assert.Equal(1, parameters.Count);

        }

        [Fact]
        public void SimplePerfTest()
        {
            var uri = new Uri("http://example.com/Glimpse.axd?n=glimpse_ajax&parentRequestId=123232323&hash=23ADE34FAE&callback=http%3A%2F%2Fexample.com%2Fcallback");

            var template = new UriTemplate("http://example.com/Glimpse.axd?n=glimpse_ajax&parentRequestId={parentRequestId}{&hash,callback}");

            for (int i = 0; i < 100000; i++)
            {
                var parameters = template.GetParameters(uri);
                
            }


        }


        [Fact]
        public void Level1Decode()
        {
            var uri = new Uri("/Hello%20World", UriKind.RelativeOrAbsolute);

            var template = new UriTemplate("/{p1}");

            var parameters = template.GetParameters(uri);

            Assert.Equal("Hello World", parameters["p1"]);

        }


        //[Fact]
        //public void Level2Decode()
        //{
        //    var uri = new Uri("/foo?path=Hello/World", UriKind.RelativeOrAbsolute);

        //    var template = new UriTemplate("/foo?path={+p1}");

        //    var parameters = template.GetParameters(uri);

        //    Assert.Equal("Hello/World", parameters["p1"]);

        //}

        [Fact]
        public void FragmentParam()
        {
            var uri = new Uri("/foo#Hello%20World!", UriKind.RelativeOrAbsolute);

            var template = new UriTemplate("/foo{#p1}");

            var parameters = template.GetParameters(uri);

            Assert.Equal("Hello World!", parameters["p1"]);

        }


        [Fact]
        public void FragmentParams()
        {
            var uri = new Uri("/foo#Hello%20World!,blurg", UriKind.RelativeOrAbsolute);

            var template = new UriTemplate("/foo{#p1,p2}");

            var parameters = template.GetParameters(uri);

            Assert.Equal("Hello World!", parameters["p1"]);
            Assert.Equal("blurg", parameters["p2"]);

        }

        [Fact]
        public void OptionalPathParam()
        {
            var uri = new Uri("/foo/yuck/bob", UriKind.RelativeOrAbsolute);

            var template = new UriTemplate("/foo{/bar}/bob");

            var parameters = template.GetParameters(uri);

            Assert.Equal("yuck", parameters["bar"]);

        }

        [Fact]
        public void OptionalPathParamWithMultipleValues()
        {
            var uri = new Uri("/foo/yuck/yob/bob", UriKind.RelativeOrAbsolute);

            var template = new UriTemplate("/foo{/bar,baz}/bob");

            var parameters = template.GetParameters(uri);
            Assert.Equal(2, parameters.Count); // This current fails
            Assert.Equal("yuck", parameters["bar"]);
            Assert.Equal("yob", parameters["baz"]);
        }
        

    }
}
