using System;
using Corvus.UriTemplates.TavisApi;
using Xunit;


namespace UriTemplateTests
{
    public class UriTemplateTableTests
    {
        [Theory,
        InlineData("/","root"),
        InlineData("/baz/fod/burg",""),
        InlineData("/baz/kit", "kit"),
        InlineData("/baz/fod", "baz"),
        InlineData("/baz/fod/blob", "blob"),
        InlineData("/glah/flid/blob", "goo")]
        public void FindPathTemplates(string url, string key)
        {
            var table = new UriTemplateTable();  // Shorter paths and literal path segments should be added to the table first.
            table.Add("root", new UriTemplate("/"));
            table.Add("foo", new UriTemplate("/foo/{bar}"));
            table.Add("kit", new UriTemplate("/baz/kit"));
            table.Add("baz", new UriTemplate("/baz/{bar}"));
            table.Add("blob", new UriTemplate("/baz/{bar}/blob"));
            table.Add("goo", new UriTemplate("/{goo}/{bar}/blob"));


            var result = table.Match(new Uri(url, UriKind.RelativeOrAbsolute));

            if (string.IsNullOrEmpty(key))
            {
                Assert.Null(result);
            }
            else
            {
                Assert.Equal(key, result.Key);
            }

            Assert.NotNull(table["goo"]);
            Assert.Null(table["goo1"]);
        }

        [Theory,
     InlineData("/games", "games"),
     InlineData("/games/monopoly/Setup/23", "gamessetup"),
     InlineData("/games/monopoly/Resources/foo/23", "resource"),
     InlineData("/games/monopoly/22/Chat/33", "chat"),     
     InlineData("/games/monopoly/22/State/33", "state"),     
    ]
        public void FindTemplatesInGamesApi(string url, string key)
        {
            var table = new UriTemplateTable();
            table.Add("games", new UriTemplate("/games"));
            table.Add("gamessetup", new UriTemplate("/games/{gametitle}/Setup/{gamesid}"));
            table.Add("resource", new UriTemplate("/games/{gametitle}/Resources/{resourcetype}/{resourceid}"));
            table.Add("chat", new UriTemplate("/games/{gametitle}/{gameid}/Chat/{chatid}"));
            table.Add("state", new UriTemplate("/games/{gametitle}/{gameid}/State/{stateid}"));


            var result = table.Match(new Uri(url, UriKind.RelativeOrAbsolute));

            if (string.IsNullOrEmpty(key))
            {
                Assert.Null(result);
            }
            else
            {
                Assert.Equal(key, result.Key);
            }
        }


                [Theory,
     InlineData("/foo?x=1&y=2", "fooxy3"),
     InlineData("/foo?x=1", "fooxy2"),
     InlineData("/foo?x=a,b,c,d", "fooxy2"),
     InlineData("/foo?y=2", "fooxy"),

     InlineData("/foo", "fooxy"),
    ]
        public void FindTemplatesWithQueryStrings(string url, string key)
        {
            var table = new UriTemplateTable();   // More restrictive templates should have priority over less restrictive ones
            table.Add("fooxy3", new UriTemplate("/foo?x={x}&y={y}"));
            table.Add("fooxy2", new UriTemplate("/foo?x={x}{&y}"));
            table.Add("fooxy4", new UriTemplate("/foo?x={x}{&z}"));
            table.Add("fooxy", new UriTemplate("/foo{?x,y}"));
            table.Add("foo", new UriTemplate("/foo"));
 

            var result = table.Match(new Uri(url, UriKind.RelativeOrAbsolute));

            if (string.IsNullOrEmpty(key))
            {
                Assert.Null(result);
            }
            else
            {
                Assert.Equal(key, result.Key);
            }
        }

        [Fact]
        public void FindTemplatesWithArrayQueryParameters()
        {
            var table = new UriTemplateTable();   // More restrictive templates should have priority over less restrictive ones
            table.Add("fooxy3", new UriTemplate("/foo?x={x}&y={y}"));
            table.Add("fooxy2", new UriTemplate("/foo?x={x}{&y}"));
            table.Add("fooxy4", new UriTemplate("/foo?x={x}{&z}"));
            table.Add("fooxy", new UriTemplate("/foo{?x,y}"));
            table.Add("foo", new UriTemplate("/foo"));


            var result = table.Match(new Uri("/foo?x=a,b,c,d", UriKind.RelativeOrAbsolute));

            Assert.Equal("fooxy2", result.Key);
            
        }


        [Fact]
        public void MatchTemplateWithDifferentOrderOfQueryStringParameters()
        {
            var table = new UriTemplateTable();   // More restrictive templates should have priority over less restrictive ones
            table.Add("fooxy3", new UriTemplate("/foo?x={x}&y={y}"));

            var result = table.Match(new Uri("/foo?y=a&x=b", UriKind.RelativeOrAbsolute), QueryStringParameterOrder.Any);

            Assert.Equal("fooxy3", result.Key);
        }

    }

}



