Feature: parameter-matching
	Parameter matching specs

Scenario: Match URI to template
	When I create a regex for the template "http://example.com/{p1}/{p2}"
	Then the regex should match "http://example.com/foo/bar"

Scenario: Get parameters
	When I create a regex for the template "http://example.com/{p1}/{p2}"
	Then the matches for "http://example.com/foo/bar" should be
		| group | match |
		| p1    | foo   |
		| p2    | bar   |

Scenario: Get parameters with operators
	When I create a UriTemplate for "http://example.com/{+p1}/{p2*}"
	Then the parameters for "http://example.com/foo/bar" should be
		| name | value |
		| p1   | "foo" |
		| p2   | "bar" |

Scenario: Get parameters from query string
	When I create a UriTemplate for "http://example.com/{+p1}/{p2*}{?blur}"
	Then the parameters for "http://example.com/foo/bar?blur=45" should be
		| name | value |
		| p1   | "foo" |
		| p2   | "bar" |
		| blur | 45    |

Scenario: Get parameters from multiple query string
	When I create a UriTemplate for "http://example.com/{+p1}/{p2*}{?blur,blob}"
	Then the parameters for "http://example.com/foo/bar?blur=45" should be
		| name | value |
		| p1   | "foo" |
		| p2   | "bar" |
		| blur | 45    |

Scenario: Get parameters from multiple query string with two parameter values
	When I create a UriTemplate for "http://example.com/{+p1}/{p2*}{?blur,blob}"
	Then the parameters for "http://example.com/foo/bar?blur=45&blob=23" should be
		| name | value |
		| p1   | "foo" |
		| p2   | "bar" |
		| blur | 45    |
		| blob | 23    |

Scenario: Get parameters from multiple query string with optional and mandatory parameters
	When I create a UriTemplate for "http://example.com/{+p1}/{p2*}{?blur}{&blob}"
	Then the parameters for "http://example.com/foo/bar?blur=45&blob=23" should be
		| name | value |
		| p1   | "foo" |
		| p2   | "bar" |
		| blur | 45    |
		| blob | 23    |

Scenario: Get parameters from multiple query string with optional parameters
	When I create a UriTemplate for "http://example.com/{+p1}/{p2*}{?blur,blob}"
	Then the parameters for "http://example.com/foo/bar" should be
		| name | value |
		| p1   | "foo" |
		| p2   | "bar" |

Scenario: Glimpse URL
	When I create a UriTemplate for "http://example.com/Glimpse.axd?n=glimpse_ajax&parentRequestId={parentRequestId}{&hash,callback}"
	Then the parameters for "http://example.com/Glimpse.axd?n=glimpse_ajax&parentRequestId=123232323&hash=23ADE34FAE&callback=http%3A%2F%2Fexample.com%2Fcallback" should be
		| name            | value                         |
		| parentRequestId | 123232323                     |
		| hash            | "23ADE34FAE"                  |
		| callback        | "http://example.com/callback" |

Scenario: URL with question mark as first character
	When I create a UriTemplate for "?hash={hash}"
	Then the parameters for "http://localhost:5000/glimpse/metadata?hash=123" should be
		| name | value |
		| hash | 123   |

Scenario: Level 1 decode
	When I create a UriTemplate for "/{p1}"
	Then the parameters for "/Hello%20World" should be
		| name | value         |
		| p1   | "Hello World" |

Scenario: Fragment parameter
	When I create a UriTemplate for "/foo{#p1}"
	Then the parameters for "/foo#Hello%20World!" should be
		| name | value          |
		| p1   | "Hello World!" |

Scenario: Fragment parameters
	When I create a UriTemplate for "/foo{#p1,p2}"
	Then the parameters for "/foo#Hello%20World!,blurg" should be
		| name | value          |
		| p1   | "Hello World!" |
		| p2   | "blurg"        |

Scenario: Optional path parameter
	When I create a UriTemplate for "/foo{/bar}/bob"
	Then the parameters for "/foo/yuck/bob" should be
		| name | value  |
		| bar  | "yuck" |

Scenario: Optional path parameter with multiple values
	When I create a UriTemplate for "/foo{/bar,baz}/bob"
	Then the parameters for "/foo/yuck/yob/bob" should be
		| name | value  |
		| bar  | "yuck" |
		| baz  | "yob"  |

Scenario: Optional path parameter with multiple optional values, only providing one
	When I create a UriTemplate for "/foo{/bar,baz}/bob"
	Then the parameters for "/foo/yuck/bob" should be
		| name | value  |
		| bar  | "yuck" |

Scenario: Update path parameter
	Given I create a UriTemplate for "http://example.org/{tenant}/customers"
	When I set the template parameter called "tenant" to the string "acmé"
	Then the resolved template should be one of
		| values                                 |
		| http://example.org/acm%C3%A9/customers |

Scenario: Query parameters the old way
	Given I create a UriTemplate for "http://example.org/customers?active={activeFlag}"
	When I set the template parameter called "activeFlag" to the bool true
	Then the resolved template should be one of
		| values                                   |
		| http://example.org/customers?active=true |

Scenario: Query parameters the new way
	Given I create a UriTemplate for "http://example.org/customers{?active}"
	When I set the template parameter called "active" to the bool true
	Then the resolved template should be one of
		| values                                   |
		| http://example.org/customers?active=true |

Scenario: Query parameters the new way without value
	When I create a UriTemplate for "http://example.org/customers{?active}"
	Then the resolved template should be one of
		| values                       |
		| http://example.org/customers |

Scenario: Should resolve URI template with non-string parameters.
	Given I create a UriTemplate for "http://example.org/location{?lat,lng}"
	When I set the template parameters
		| name | value  |
		| lat  | 31.464 |
		| lng  | 74.386 |
	Then the resolved template should be one of
		| values                                            |
		| http://example.org/location?lng=74.386&lat=31.464 |
		| http://example.org/location?lat=31.464&lng=74.386 |

Scenario: Parameters from a JsonObject
	Given I create a UriTemplate for "http://example.org/{environment}/{version}/customers{?active,country}"
	When I set the template parameters from the JsonObject {"environment": "dev", "version": "v2", "active": true, "country": "CA" }
	Then the resolved template should be one of
		| values                                                     |
		| http://example.org/dev/v2/customers?active=true&country=CA |
		| http://example.org/dev/v2/customers?country=CA&active=true |

Scenario: Some parameters from a JsonObject
	Given I create a UriTemplate for "http://example.org{/environment}/{version}/customers{?active,country}"
	When I set the template parameters from the JsonObject {"version": "v2", "active": true }
	Then the resolved template should be one of
		| values                                      |
		| http://example.org/v2/customers?active=true |

Scenario: Add JSON Object to query parameter
	Given I create a UriTemplate for "http://example.org/foo{?coords*}"
	When I set the template parameter called "coords" to the JsonAny {"x": 1, "y": 2 }
	Then the resolved template should be one of
		| values                         |
		| http://example.org/foo?x=1&y=2 |
		| http://example.org/foo?y=2&x=1 |

Scenario: Apply parameters from a JsonObject to a path segment
	Given I create a UriTemplate for "http://example.org/foo/{bar}/baz"
	When I set the template parameters from the JsonObject {"bar": "yo" }
	Then the resolved template should be one of
		| values                        |
		| http://example.org/foo/yo/baz |

Scenario: Extreme encoding
	Given I create a UriTemplate for "http://example.org/sparql{?query}"
	When I set the template parameter called "query" to the string "PREFIX dc: <http://purl.org/dc/elements/1.1/> SELECT ?book ?who WHERE { ?book dc:creator ?who }"
	Then the resolved template should be one of
		| values                                                                                                                                                                                  |
		| http://example.org/sparql?query=PREFIX%20dc%3A%20%3Chttp%3A%2F%2Fpurl.org%2Fdc%2Felements%2F1.1%2F%3E%20SELECT%20%3Fbook%20%3Fwho%20WHERE%20%7B%20%3Fbook%20dc%3Acreator%20%3Fwho%20%7D |

Scenario: Apply parameters from a JsonObject with a list
	Given I create a UriTemplate for "http://example.org/customers{?ids,order}"
	When I set the template parameters from the JsonObject {"order": "up", "ids": ["21", "75", "21"] }
	Then the resolved template should be one of
		| values                                             |
		| http://example.org/customers?ids=21,75,21&order=up |
		| http://example.org/customers?order=up&ids=21,75,21 |

Scenario: Apply parameters from a JsonObject with a list of ints
	Given I create a UriTemplate for "http://example.org/customers{?ids,order}"
	When I set the template parameters from the JsonObject {"order": "up", "ids": [21, 75, 21] }
	Then the resolved template should be one of
		| values                                             |
		| http://example.org/customers?ids=21,75,21&order=up |
		| http://example.org/customers?order=up&ids=21,75,21 |

Scenario: Apply parameters from a JsonObject with a list of ints exploded
	Given I create a UriTemplate for "http://example.org/customers{?ids*,order}"
	When I set the template parameters from the JsonObject {"order": "up", "ids": [21, 75, 21] }
	Then the resolved template should be one of
		| values                                                     |
		| http://example.org/customers?ids=21&ids=75&ids=21&order=up |
		| http://example.org/customers?order=up&ids=21&ids=75&ids=21 |

Scenario: Apply folders from a JsonObject to a path
	Given I create a UriTemplate for "http://example.org/files{/folders*}{?filename}"
	When I set the template parameters from the JsonObject {"filename": "proposal.pdf", "folders": ["customer", "project"] }
	Then the resolved template should be one of
		| values                                                          |
		| http://example.org/files/customer/project?filename=proposal.pdf |

Scenario: Apply folders from a JsonObject to a path from string not URL
	Given I create a UriTemplate for "http://example.org{/folders*}{?filename}"
	When I set the template parameters from the JsonObject {"filename": "proposal.pdf", "folders": ["files", "customer", "project"] }
	Then the resolved template should be one of
		| values                                                          |
		| http://example.org/files/customer/project?filename=proposal.pdf |

Scenario: Parameters from a JsonObject from invalid URL
	Given I create a UriTemplate for "http://{environment}.example.org/{version}/customers{?active,country}"
	When I set the template parameters from the JsonObject {"environment": "dev", "version": "v2", "active": true, "country": "CA" }
	Then the resolved template should be one of
		| values                                                     |
		| http://dev.example.org/v2/customers?active=true&country=CA |
		| http://example.org/dev/v2/customers?country=CA&active=true |

Scenario: Replace base address
	Given I create a UriTemplate for "{+baseUrl}api/customer/{id}"
	When I set the template parameters from the JsonObject {"baseUrl": "http://example.org/", "id": "22" }
	Then the resolved template should be one of
		| values                             |
		| http://example.org/api/customer/22 |

Scenario: Replace base address but not ID
	Given I create a UriTemplate for "{+baseUrl}api/customer/{id}" with partial resolution
	When I set the template parameters from the JsonObject {"baseUrl": "http://example.org/" }
	Then the resolved template should be one of
		| values                               |
		| http://example.org/api/customer/{id} |

Scenario: Partially apply parameters from a JSON Object from Invalid URL
	Given I create a UriTemplate for "http://{environment}.example.org/{version}/customers{?active,country}" with partial resolution
	When I set the template parameters from the JsonObject {"environment": "dev", "version": "v2"}
	Then the resolved template should be one of
		| values                                               |
		| http://dev.example.org/v2/customers{?active,country} |

Scenario: Partially apply parameters from a JSON Object to a path from string not url
	Given I create a UriTemplate for "http://example.org{/folders*}{?filename}" with partial resolution
	When I set the template parameters from the JsonObject {"filename": "proposal.pdf" }
	Then the resolved template should be one of
		| values                                              |
		| http://example.org{/folders*}?filename=proposal.pdf |

Scenario: Add multiple parameters to link
	Given I create a UriTemplate for "http://localhost/api/{dataset}/customer{?foo,bar,baz}"
	When I set the template parameters
		| name    | value |
		| foo     | "bar" |
		| baz     | 99    |
		| dataset | "bob" |
	Then the resolved template should be one of
		| values                                           |
		| http://localhost/api/bob/customer?foo=bar&baz=99 |
		| http://localhost/api/bob/customer?baz=99&foo=bar |

Scenario: Set template parameters for an int
	Given I create a UriTemplate for "http://example.org/location{?value}"
	When I set the template parameter called "value" to the integer 3
	Then the resolved template should be one of
		| values                              |
		| http://example.org/location?value=3 |

Scenario: Set template parameters for a double
	Given I create a UriTemplate for "http://example.org/location{?value}"
	When I set the template parameter called "value" to the double 3.3
	Then the resolved template should be one of
		| values                                |
		| http://example.org/location?value=3.3 |

Scenario: Set template parameters for a boolean
	Given I create a UriTemplate for "http://example.org/location{?value}"
	When I set the template parameter called "value" to the bool true
	Then the resolved template should be one of
		| values                                 |
		| http://example.org/location?value=true |

Scenario: Set template parameters for a string
	Given I create a UriTemplate for "http://example.org/location{?value}"
	When I set the template parameter called "value" to the string "SomeString"
	Then the resolved template should be one of
		| values                                       |
		| http://example.org/location?value=SomeString |

Scenario: Set template parameters for a float
	Given I create a UriTemplate for "http://example.org/location{?value}"
	When I set the template parameter called "value" to the float 3.3
	Then the resolved template should be one of
		| values                                              |
		| http://example.org/location?value=3.299999952316284 |

Scenario: Set template parameters for a long
	Given I create a UriTemplate for "http://example.org/location{?value}"
	When I set the template parameter called "value" to the long 333
	Then the resolved template should be one of
		| values                                |
		| http://example.org/location?value=333 |

Scenario: Set template parameters for a dictionary of strings
	Given I create a UriTemplate for "http://example.org/location{?value*}"
	When I set the template parameter called "value" to the dictionary of strings
		| key | value |
		| foo | bar   |
		| bar | baz   |
		| baz | bob   |
	Then the resolved template should be one of
		| values                                              |
		| http://example.org/location?foo=bar&bar=baz&baz=bob |
		| http://example.org/location?foo=bar&baz=bob&bar=baz |
		| http://example.org/location?baz=bob&bar=baz&foo=bar |
		| http://example.org/location?baz=bob&foo=bar&bar=baz |
		| http://example.org/location?bar=baz&baz=bob&foo=bar |
		| http://example.org/location?bar=baz&foo=bar&baz=bob |

Scenario: Set template parameters for an array of strings
	Given I create a UriTemplate for "http://example.org/location{?value*}"
	When I set the template parameter called "value" to the enumerable of strings
		| value |
		| bar   |
		| baz   |
		| bob   |
	Then the resolved template should be one of
		| values                                                    |
		| http://example.org/location?value=bar&value=baz&value=bob |
		| http://example.org/location?value=bar&value=bob&value=baz |
		| http://example.org/location?value=baz&value=bob&value=bar |
		| http://example.org/location?value=baz&value=bar&value=bob |
		| http://example.org/location?value=bob&value=baz&value=bar |
		| http://example.org/location?value=bob&value=bar&value=baz |