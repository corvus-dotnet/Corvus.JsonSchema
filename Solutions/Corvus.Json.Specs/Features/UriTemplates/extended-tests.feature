Feature: extended-tests

Scenario Outline: Additional Examples 1 at level 4
	Given the variables
		| name                | value                                                                                                                                                                                       |
		| id                  | "person"                                                                                                                                                                                    |
		| token               | "12345"                                                                                                                                                                                     |
		| fields              | ["id","name","picture"]                                                                                                                                                                     |
		| format              | "json"                                                                                                                                                                                      |
		| q                   | "URI Templates"                                                                                                                                                                             |
		| page                | "5"                                                                                                                                                                                         |
		| lang                | "en"                                                                                                                                                                                        |
		| geocode             | ["37.76","-122.427"]                                                                                                                                                                        |
		| first_name          | "John"                                                                                                                                                                                      |
		| last.name           | "Doe"                                                                                                                                                                                       |
		| Some%20Thing        | "foo"                                                                                                                                                                                       |
		| number              | 6                                                                                                                                                                                           |
		| long                | 37.76                                                                                                                                                                                       |
		| lat                 | -122.427                                                                                                                                                                                    |
		| group_id            | "12345"                                                                                                                                                                                     |
		| query               | "PREFIX dc: \u003Chttp://purl.org/dc/elements/1.1/\u003E SELECT ?book ?who WHERE { ?book dc:creator ?who }"                                                                                 |
		| uri                 | "http://example.org/?uri=http%3A%2F%2Fexample.org%2F"                                                                                                                                       |
		| word                | "dr\u00FCcken"                                                                                                                                                                              |
		| Stra%C3%9Fe         | "Gr\u00FCner Weg"                                                                                                                                                                           |
		| random              | "\u0161\u00F6\u00E4\u0178\u0153\u00F1\u00EA\u20AC\u00A3\u00A5\u2021\u00D1\u00D2\u00D3\u00D4\u00D5\u00D6\u00D7\u00D8\u00D9\u00DA\u00E0\u00E1\u00E2\u00E3\u00E4\u00E5\u00E6\u00E7\u00FF"      |
		| assoc_special_chars | {"\u0161\u00F6\u00E4\u0178\u0153\u00F1\u00EA\u20AC\u00A3\u00A5\u2021\u00D1\u00D2\u00D3\u00D4\u00D5":"\u00D6\u00D7\u00D8\u00D9\u00DA\u00E0\u00E1\u00E2\u00E3\u00E4\u00E5\u00E6\u00E7\u00FF"} |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template                                                  | result                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
		| {/id*}                                                    | ["/person"]                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
		| {/id*}{?fields,first_name,last.name,token}                | ["/person?fields=id,name,picture\u0026first_name=John\u0026last.name=Doe\u0026token=12345","/person?fields=id,picture,name\u0026first_name=John\u0026last.name=Doe\u0026token=12345","/person?fields=picture,name,id\u0026first_name=John\u0026last.name=Doe\u0026token=12345","/person?fields=picture,id,name\u0026first_name=John\u0026last.name=Doe\u0026token=12345","/person?fields=name,picture,id\u0026first_name=John\u0026last.name=Doe\u0026token=12345","/person?fields=name,id,picture\u0026first_name=John\u0026last.name=Doe\u0026token=12345"] |
		| /search.{format}{?q,geocode,lang,locale,page,result_type} | ["/search.json?q=URI%20Templates\u0026geocode=37.76,-122.427\u0026lang=en\u0026page=5","/search.json?q=URI%20Templates\u0026geocode=-122.427,37.76\u0026lang=en\u0026page=5"]                                                                                                                                                                                                                                                                                                                                                                                 |
		| /test{/Some%20Thing}                                      | ["/test/foo"]                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
		| /set{?number}                                             | ["/set?number=6"]                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
		| /loc{?long,lat}                                           | ["/loc?long=37.76\u0026lat=-122.427"]                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
		| /base{/group_id,first_name}/pages{/page,lang}{?format,q}  | ["/base/12345/John/pages/5/en?format=json\u0026q=URI%20Templates"]                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
		| /sparql{?query}                                           | ["/sparql?query=PREFIX%20dc%3A%20%3Chttp%3A%2F%2Fpurl.org%2Fdc%2Felements%2F1.1%2F%3E%20SELECT%20%3Fbook%20%3Fwho%20WHERE%20%7B%20%3Fbook%20dc%3Acreator%20%3Fwho%20%7D"]                                                                                                                                                                                                                                                                                                                                                                                     |
		| /go{?uri}                                                 | ["/go?uri=http%3A%2F%2Fexample.org%2F%3Furi%3Dhttp%253A%252F%252Fexample.org%252F"]                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
		| /service{?word}                                           | ["/service?word=dr%C3%BCcken"]                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
		| /lookup{?Stra%C3%9Fe}                                     | ["/lookup?Stra%C3%9Fe=Gr%C3%BCner%20Weg"]                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
		| {random}                                                  | ["%C5%A1%C3%B6%C3%A4%C5%B8%C5%93%C3%B1%C3%AA%E2%82%AC%C2%A3%C2%A5%E2%80%A1%C3%91%C3%92%C3%93%C3%94%C3%95%C3%96%C3%97%C3%98%C3%99%C3%9A%C3%A0%C3%A1%C3%A2%C3%A3%C3%A4%C3%A5%C3%A6%C3%A7%C3%BF"]                                                                                                                                                                                                                                                                                                                                                                |
		| {?assoc_special_chars*}                                   | ["?%C5%A1%C3%B6%C3%A4%C5%B8%C5%93%C3%B1%C3%AA%E2%82%AC%C2%A3%C2%A5%E2%80%A1%C3%91%C3%92%C3%93%C3%94%C3%95=%C3%96%C3%97%C3%98%C3%99%C3%9A%C3%A0%C3%A1%C3%A2%C3%A3%C3%A4%C3%A5%C3%A6%C3%A7%C3%BF"]                                                                                                                                                                                                                                                                                                                                                              |

Scenario Outline: Additional Examples 2 at level 4
	Given the variables
		| name    | value                   |
		| id      | ["person","albums"]     |
		| token   | "12345"                 |
		| fields  | ["id","name","picture"] |
		| format  | "atom"                  |
		| q       | "URI Templates"         |
		| page    | "10"                    |
		| start   | "5"                     |
		| lang    | "en"                    |
		| geocode | ["37.76","-122.427"]    |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template              | result                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
		| {/id*}                | ["/person/albums","/albums/person"]                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
		| {/id*}{?fields,token} | ["/person/albums?fields=id,name,picture\u0026token=12345","/person/albums?fields=id,picture,name\u0026token=12345","/person/albums?fields=picture,name,id\u0026token=12345","/person/albums?fields=picture,id,name\u0026token=12345","/person/albums?fields=name,picture,id\u0026token=12345","/person/albums?fields=name,id,picture\u0026token=12345","/albums/person?fields=id,name,picture\u0026token=12345","/albums/person?fields=id,picture,name\u0026token=12345","/albums/person?fields=picture,name,id\u0026token=12345","/albums/person?fields=picture,id,name\u0026token=12345","/albums/person?fields=name,picture,id\u0026token=12345","/albums/person?fields=name,id,picture\u0026token=12345"] |

Scenario Outline: Additional Examples 3: Empty Variables at level 0
	Given the variables
		| name        | value |
		| empty_list  | []    |
		| empty_assoc | {}    |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template        | result |
		| {/empty_list}   | [""]   |
		| {/empty_list*}  | [""]   |
		| {?empty_list}   | [""]   |
		| {?empty_list*}  | [""]   |
		| {?empty_assoc}  | [""]   |
		| {?empty_assoc*} | [""]   |

Scenario Outline: Additional Examples 4: Numeric Keys at level 0
	Given the variables
		| name   | value                                                                       |
		| 42     | "The Answer to the Ultimate Question of Life, the Universe, and Everything" |
		| 1337   | ["leet","as","it","can","be"]                                               |
		| german | {"11":"elf","12":"zw\u00F6lf"}                                              |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template   | result                                                                                                      |
		| {42}       | ["The%20Answer%20to%20the%20Ultimate%20Question%20of%20Life%2C%20the%20Universe%2C%20and%20Everything"]     |
		| {?42}      | ["?42=The%20Answer%20to%20the%20Ultimate%20Question%20of%20Life%2C%20the%20Universe%2C%20and%20Everything"] |
		| {1337}     | ["leet,as,it,can,be"]                                                                                       |
		| {?1337*}   | ["?1337=leet\u00261337=as\u00261337=it\u00261337=can\u00261337=be"]                                         |
		| {?german*} | ["?11=elf\u002612=zw%C3%B6lf","?12=zw%C3%B6lf\u002611=elf"]                                                 |

Scenario Outline: Additional Examples 5: Explode Combinations
	Given the variables
		| name   | value                                                                       |
		| id     | "admin" |
		| token   | "12345"|
		| tab | "overview" |
		| keys | {"key1": "val1","key2": "val2" }                                              |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template   | result                                                                                                      |
		| {?id,token,keys*}       | ["?id=admin&token=12345&key1=val1&key2=val2","?id=admin&token=12345&key2=val2&key1=val1"]     |
		| {/id}{?token,keys*}      | ["/admin?token=12345&key1=val1&key2=val2", "/admin?token=12345&key2=val2&key1=val1"] |
		| {?id,token}{&keys*}     | ["?id=admin&token=12345&key1=val1&key2=val2","?id=admin&token=12345&key2=val2&key1=val1"]                                                                                       |
		| /user{/id}{?token,tab}{&keys*}   | ["/user/admin?token=12345&tab=overview&key1=val1&key2=val2", "/user/admin?token=12345&tab=overview&key2=val2&key1=val1"]                                         |

Scenario Outline: Additional Examples 6: Reserved Expansion
	Given the variables
		| name    | value									|
		| id      | "admin%2F"								|
		| not_pct | "%foo"									|
		| list    | ["red%25", "%2Fgreen", "blue "]			|
		| keys    | {"key1": "val1%2F","key2": "val2%2F" }  |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template   | result                                                                                                      |
		| {+id} |["admin%2F"] |
		| {#id} |["#admin%2F"] |
		| {id} |["admin%252F"] |
		| {+not_pct} |["%25foo"] |
		| {#not_pct} |["#%25foo"] |
		| {not_pct} |["%25foo"] |
		| {+list} |["red%25,%2Fgreen,blue%20"] |
		| {#list} |["#red%25,%2Fgreen,blue%20"] |
		| {list} |["red%2525,%252Fgreen,blue%20"] |
		| {+keys} |["key1,val1%2F,key2,val2%2F"] |
		| {#keys} |["#key1,val1%2F,key2,val2%2F"] |
		| {keys} |["key1,val1%252F,key2,val2%252F"] |