using System;
using System.Collections.Generic;
using Corvus.UriTemplates.TavisApi;
using Xunit;

namespace UriTemplateTests
{
    public class UsageTests
    {       
        [Fact]
        public void ShouldAllowUriTemplateWithPathSegmentParameter()
        {
            var template = new UriTemplate("http://example.org/foo/{bar}/baz");
            template.SetParameter("bar", "yo");
            var uriString = template.Resolve();
            Assert.Equal("http://example.org/foo/yo/baz", uriString);
        }


        [Fact]
        public void ShouldAllowUriTemplateWithMultiplePathSegmentParameter()
        {
            var template = new UriTemplate("http://example.org/foo/{bar}/baz/{blar}");
            template.SetParameter("bar", "yo");
            template.SetParameter("blar", "yuck");
            var uriString = template.Resolve();
            Assert.Equal("http://example.org/foo/yo/baz/yuck", uriString);
        }

        [Fact]
        public void ShouldResolveUriTemplateWithNonStringParameter()
        {
            var template = new UriTemplate("http://example.org/foo/{bar}/baz{?lat,lng}");

            double lat = 31.464, lng = 74.386;

            template.SetParameter("bar", "yo");
            template.SetParameter("lat", lat);
            template.SetParameter("lng", lng);
            
            var uriString = template.Resolve();
            Assert.Equal("http://example.org/foo/yo/baz?lat=31.464&lng=74.386", uriString);
        }


        [Fact]
        public void ShouldResolveMatrixParameter()
        {
            var template = new UriTemplate("http://example.org/foo{;lat,lng}");

            double lat = 31.464, lng = 74.386;

            template.SetParameter("lat", lat);
            template.SetParameter("lng", lng);

            var uriString = template.Resolve();
            Assert.Equal("http://example.org/foo;lat=31.464;lng=74.386", uriString);
        }


        [Fact]
        public void ShouldAllowUriTemplateWithQueryParamsButNoValues()
        {
            var template = new UriTemplate("http://example.org/foo{?bar,baz}");
            //template.SetParameter("bar", "yo");
            //template.SetParameter("blar", "yuck");
            var uriString = template.Resolve();
            Assert.Equal("http://example.org/foo", uriString);
        }

        [Fact]
        public void ShouldAllowPartialUriTemplateWithQueryParamsButNoValues()
        {
            var template = new UriTemplate("http://example.org/foo{?bar,baz}", resolvePartially: true);
            //template.SetParameter("bar", "yo");
            //template.SetParameter("blar", "yuck");
            var uriString = template.Resolve();
            Assert.Equal("http://example.org/foo{?bar,baz}", uriString);
        }

        [Fact]
        public void ShouldAllowUriTemplateToRemoveParameter()
        {
            var template = new UriTemplate("http://example.org/foo{?bar,baz}");
            template.SetParameter("bar", "yo");
            template.SetParameter("baz", "yuck");
            template.ClearParameter("bar");

            var uriString = template.Resolve();
            Assert.Equal("http://example.org/foo?baz=yuck", uriString);
        }
        
        
        [Fact]
        public void ShouldAllowUriTemplateWithQueryParamsWithOneValue()
        {
            var template = new UriTemplate("http://example.org/foo{?bar,baz}");
            template.SetParameter("baz", "yo");

            var uriString = template.Resolve();
            Assert.Equal("http://example.org/foo?baz=yo", uriString);
        }


        [Fact]
        public void LabelExpansionWithDotPrefixAndEmptyKeys()
        {
            var template = new UriTemplate("X{.empty_keys}");
            template.SetParameter("empty_keys", new Dictionary<string, string>());
            var uriString = template.Resolve();
            Assert.Equal("X", uriString);
        }

        [Fact]
        public void QueryParametersFromDictionary()
        {
            var template = new UriTemplate("http://example.org/customers{?query*}");
            template.SetParameter("query", new Dictionary<string, string>()
            {
                {"active","true"}, 
                {"Country","Brazil"}
            });
            var uriString = template.Resolve();
            Assert.Equal("http://example.org/customers?active=true&Country=Brazil", uriString);
        }

        [Fact]
        public void ShouldAllowListAndSingleValueInQueryParam()
        {
            var template = new UriTemplate("http://example.org{/id*}{?fields,token}");
            template.SetParameter("id", new List<string>() { "person", "albums" });
            template.SetParameter("fields", new List<string>() { "id", "name", "picture" });
            template.SetParameter("token", "12345");
            var uriString = template.Resolve();
            Assert.Equal("http://example.org/person/albums?fields=id,name,picture&token=12345", uriString);
        }


        [Fact]
        public void ShouldHandleUriEncoding()
        {
            var template = new UriTemplate("http://example.org/sparql{?query}");
            template.SetParameter("query", "PREFIX dc: <http://purl.org/dc/elements/1.1/> SELECT ?book ?who WHERE { ?book dc:creator ?who }");
            var uriString = template.Resolve();
            Assert.Equal("http://example.org/sparql?query=PREFIX%20dc%3A%20%3Chttp%3A%2F%2Fpurl.org%2Fdc%2Felements%2F1.1%2F%3E%20SELECT%20%3Fbook%20%3Fwho%20WHERE%20%7B%20%3Fbook%20dc%3Acreator%20%3Fwho%20%7D", uriString);
        }

        [Fact]
        public void ShouldHandleEncodingAParametersThatIsAUriWithAUriAsAParameter()
        {
            var template = new UriTemplate("http://example.org/go{?uri}");
            template.SetParameter("uri", "http://example.org/?uri=http%3A%2F%2Fexample.org%2F");
            var uriString = template.Resolve();
            Assert.Equal("http://example.org/go?uri=http%3A%2F%2Fexample.org%2F%3Furi%3Dhttp%253A%252F%252Fexample.org%252F", uriString);
        }


        [Fact]
        public void ShouldThrowWhenExpressionIsNotClosed()
        {
            var result = string.Empty;
            try
            {
                var template = new UriTemplate("http://example.org/foo/{bar/baz/");
                var uriString = template.Resolve();
            }
            catch (ArgumentException ex)
            {

                result = ex.Message;
            }

            Assert.Equal("Malformed template.", result);

        }

        [Fact]
        public void ShouldThrowWhenTemplateExpressionIsEmpty()
        {
            var result = string.Empty;
            try
            {
                var template = new UriTemplate("http://example.org/foo/{}/baz/");
                var uriString = template.Resolve();
            }
            catch (ArgumentException ex)
            {

                result = ex.Message;
            }

            Assert.Equal("Malformed template.", result);

        }

        [Fact]
        public void Query_param_with_exploded_array()
        {
            UriTemplate template = new UriTemplate("/foo/{foo}/baz{?haz*}");
            template.SetParameter("foo", "1234");
            template.SetParameter("haz", new string[] { "foo","bar" });

            string uri = template.Resolve();

            Assert.Equal("/foo/1234/baz?haz=foo&haz=bar", uri);
        }

        [Fact]
        public void Query_param_with_list_array()
        {
            UriTemplate template = new UriTemplate("/foo/{foo}/baz{?haz}");
            template.SetParameter("foo", "1234");
            template.SetParameter("haz", new string[] { "foo", "bar" });

            string uri = template.Resolve();

            Assert.Equal("/foo/1234/baz?haz=foo,bar", uri);
        }

        [Fact]
        public void Query_param_with_empty_array()
        {
            UriTemplate template = new UriTemplate("/foo/{foo}/baz{?haz*}");
            template.SetParameter("foo", "1234");
            template.SetParameter("haz", new string[] {});

            string uri = template.Resolve();

            Assert.Equal("/foo/1234/baz",uri);
        }

        [Fact]
        public void ResolveOptionalAndRequiredQueryParameters()
        {
            UriTemplate template = new UriTemplate("https://api.github.com/search/code?q={query}{&page,per_page,sort,order}");
            template.SetParameter("query", "1234");
            template.SetParameter("per_page", "19");
            var result = template.Resolve();

            Assert.Equal("https://api.github.com/search/code?q=1234&per_page=19", result);
        }


        [Fact]
        public void ReservedCharacterExpansion()
        {
            UriTemplate template = new UriTemplate("https://foo.com/{?format}");
            template.SetParameter("format", "application/vnd.foo+xml");
            
            var result = template.Resolve();

            Assert.Equal("https://foo.com/?format=application%2Fvnd.foo%2Bxml",result);
            
        }

        [Fact(Skip = "Unit tests should not require internet access!!")] 
        public void PreserveReservedCharacterExpansion()
        {
            UriTemplate template = new UriTemplate("https://foo.com/?format={+format}");
            template.SetParameter("format", "application/vnd.foo+xml");

            var result = template.Resolve();

            Assert.Equal("https://foo.com/?format=application/vnd.foo+xml", result);
            
        }

        [Fact]
        public void ShouldSupportUnicodeCharacters()
        {
            UriTemplate template = new UriTemplate("/lookup{?Stra%C3%9Fe}");
            template.SetParameter("Stra%C3%9Fe", "Grüner Weg");

            var result = template.Resolve();
            

            Assert.Equal("/lookup?Stra%C3%9Fe=Gr%C3%BCner%20Weg", result);

        }
        // "assoc_special_chars"  : { "šöäŸœñê€£¥‡ÑÒÓÔÕ" : "Ö×ØÙÚàáâãäåæçÿ" }
        [Fact]
        public void ShouldSupportUnicodeCharacters2()
        {
            UriTemplate template = new UriTemplate("{?assoc_special_chars*}");
            var dict = new Dictionary<string, string>
            {
                {"šöäŸœñê€£¥‡ÑÒÓÔÕ", "Ö×ØÙÚàáâãäåæçÿ"}
            };
            template.SetParameter("assoc_special_chars", dict);

            var result = template.Resolve();


            Assert.Equal("?%C5%A1%C3%B6%C3%A4%C5%B8%C5%93%C3%B1%C3%AA%E2%82%AC%C2%A3%C2%A5%E2%80%A1%C3%91%C3%92%C3%93%C3%94%C3%95=%C3%96%C3%97%C3%98%C3%99%C3%9A%C3%A0%C3%A1%C3%A2%C3%A3%C3%A4%C3%A5%C3%A6%C3%A7%C3%BF", result);

        }

        [Fact]
        public void Remove_a_query_parameters2()
        {

            var target = new Uri("http://example.org/customer?format=xml&id=23");

            var template = target.MakeTemplate();
            template.ClearParameter("format");


            Assert.Equal("http://example.org/customer?id=23", template.Resolve());
        }


        [Fact]
        public void EncodingTest1()
        {

            var url = new UriTemplate("/1/search/auto/{folder}{?query}")
                .AddParameter("folder","My Documents")
                .AddParameter("query", "draft 2013")
                .Resolve();

            Assert.Equal("/1/search/auto/My%20Documents?query=draft%202013", url);
        }

        [Fact]
        public void EncodingTest2()
        {

            // Parameter values get encoded but hyphen doesn't need to be encoded because it
            // is an "unreserved" character according to RFC 3986
            var url = new UriTemplate("{/greeting}")
                .AddParameter("greeting", "hello-world")
                .Resolve();

            Assert.Equal("/hello-world", url);

            // A slash does need to be encoded
            var url2 = new UriTemplate("{/greeting}")
                .AddParameter("greeting", "hello/world")
                .Resolve();

            Assert.Equal("/hello%2Fworld", url2);

            // If you truly want to make multiple path segments then do this
            var url3 = new UriTemplate("{/greeting*}")
                .AddParameter("greeting", new List<string> {"hello","world"})
                .Resolve();

            Assert.Equal("/hello/world", url3);

        }

        //  /docs/salary.csv?columns=1,2
        //  /docs/salary.csv?column=1&column=2

        //   /emails?from[name]=Don&from[date]=1998-03-24&to[name]=Norm

        // : /log?a=b&c=4
        [Fact]
        public void EncodingTest3()
        {

            // There are different ways that lists can be included in query params
            // Just as a comma delimited list
            var url = new UriTemplate("/docs/salary.csv{?columns}")
                .AddParameter("columns", new List<int> {1,2})
                .Resolve();

            Assert.Equal("/docs/salary.csv?columns=1,2", url);

            // or as a multiple parameter instances
            var url2 = new UriTemplate("/docs/salary.csv{?columns*}")
                .AddParameter("columns", new List<int> { 1, 2 })
                .Resolve();

            Assert.Equal("/docs/salary.csv?columns=1&columns=2", url2);
        }

        [Fact]
        public void EncodingTest4()
        {
            var url = new UriTemplate("/emails{?params*}")
                .AddParameter("params", new Dictionary<string,string>
                {
                    {"from[name]","Don"},
                    {"from[date]","1998-03-24"},
                    {"to[name]","Norm"}
                })
                .Resolve();

            Assert.Equal("/emails?from[name]=Don&from[date]=1998-03-24&to[name]=Norm", url);
        }

        [Fact]
        public void EncodingTest5()
        {
            ///log?a=b&c=4
            var url = new UriTemplate("/log?a={a}&c={c}")
                .AddParameter("a", "b")
                .AddParameter("c", "4")
                .Resolve();

            Assert.Equal("/log?a=b&c=4", url);
        }


        [Fact]
        public void InvalidSpace()
        {
            Exception ex = null;
            try
            {
                var url = new UriTemplate("/feeds/events{? fromId").Resolve();
            } catch (Exception e)
            {
                ex = e;
            }
            Assert.NotNull(ex);
        }

        
    }
}