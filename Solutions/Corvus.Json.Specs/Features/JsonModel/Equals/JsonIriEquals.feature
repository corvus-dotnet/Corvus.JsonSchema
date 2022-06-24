Feature: JsonIriEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonIri
Scenario Outline: Equals for json element backed value as a iri
	Given the JsonElement backed JsonIri <jsonValue>
	When I compare it to the iri <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue                      | value                            | result |
		| "http://foo.bar/?baz=qux#quux" | "http://foo.bar/?baz=qux#quux"   | true   |
		| "http://foo.bar/?baz=qux#quux" | "http://jim.bob/?sue=sally#tina" | false  |
		| null                           | null                             | true   |
		| null                           | "http://foo.bar/?baz=qux#quux"   | false  |

Scenario Outline: Equals for dotnet backed value as a iri
	Given the dotnet backed JsonIri <jsonValue>
	When I compare it to the iri <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue                      | value                            | result |
		| "http://foo.bar/?baz=qux#quux" | "http://foo.bar/?baz=qux#quux"   | true   |
		| "http://foo.bar/?baz=qux#quux" | "http://jim.bob/?sue=sally#tina" | false  |

Scenario Outline: Equals for iri json element backed value as an IJsonValue
	Given the JsonElement backed JsonIri <jsonValue>
	When I compare the iri to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue                      | value                          | result |
		| "http://foo.bar/?baz=qux#quux" | "Hello"                        | false  |
		| "http://foo.bar/?baz=qux#quux" | "Goodbye"                      | false  |
		| "http://foo.bar/?baz=qux#quux" | 1                              | false  |
		| "http://foo.bar/?baz=qux#quux" | 1.1                            | false  |
		| "http://foo.bar/?baz=qux#quux" | [1,2,3]                        | false  |
		| "http://foo.bar/?baz=qux#quux" | { "first": "1" }               | false  |
		| "http://foo.bar/?baz=qux#quux" | true                           | false  |
		| "http://foo.bar/?baz=qux#quux" | false                          | false  |
		| "http://foo.bar/?baz=qux#quux" | "2018-11-13T20:20:39+00:00"    | false  |
		| "http://foo.bar/?baz=qux#quux" | "2018-11-13"                   | false  |
		| "http://foo.bar/?baz=qux#quux" | "P3Y6M4DT12H30M5S"             | false  |
		| "http://foo.bar/?baz=qux#quux" | "2018-11-13"                   | false  |
		| "http://foo.bar/?baz=qux#quux" | "hello@endjin.com"             | false  |
		| "http://foo.bar/?baz=qux#quux" | "www.example.com"              | false  |
		| "http://foo.bar/?baz=qux#quux" | "http://foo.bar/?baz=qux#quux" | true   |
		| "http://foo.bar/?baz=qux#quux" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "http://foo.bar/?baz=qux#quux" | "{ \"first\": \"1\" }"         | false  |
		| "http://foo.bar/?baz=qux#quux" | "192.168.0.1"                  | false  |
		| "http://foo.bar/?baz=qux#quux" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for iri dotnet backed value as an IJsonValue
	Given the dotnet backed JsonIri <jsonValue>
	When I compare the iri to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue                      | value                          | result |
		| "http://foo.bar/?baz=qux#quux" | "Hello"                        | false  |
		| "http://foo.bar/?baz=qux#quux" | "Goodbye"                      | false  |
		| "http://foo.bar/?baz=qux#quux" | 1                              | false  |
		| "http://foo.bar/?baz=qux#quux" | 1.1                            | false  |
		| "http://foo.bar/?baz=qux#quux" | [1,2,3]                        | false  |
		| "http://foo.bar/?baz=qux#quux" | { "first": "1" }               | false  |
		| "http://foo.bar/?baz=qux#quux" | true                           | false  |
		| "http://foo.bar/?baz=qux#quux" | false                          | false  |
		| "http://foo.bar/?baz=qux#quux" | "2018-11-13T20:20:39+00:00"    | false  |
		| "http://foo.bar/?baz=qux#quux" | "P3Y6M4DT12H30M5S"             | false  |
		| "http://foo.bar/?baz=qux#quux" | "2018-11-13"                   | false  |
		| "http://foo.bar/?baz=qux#quux" | "P3Y6M4DT12H30M5S"             | false  |
		| "http://foo.bar/?baz=qux#quux" | "hello@endjin.com"             | false  |
		| "http://foo.bar/?baz=qux#quux" | "www.example.com"              | false  |
		| "http://foo.bar/?baz=qux#quux" | "http://foo.bar/?baz=qux#quux" | true   |
		| "http://foo.bar/?baz=qux#quux" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "http://foo.bar/?baz=qux#quux" | "{ \"first\": \"1\" }"         | false  |
		| "http://foo.bar/?baz=qux#quux" | "192.168.0.1"                  | false  |
		| "http://foo.bar/?baz=qux#quux" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for iri json element backed value as an object
	Given the JsonElement backed JsonIri <jsonValue>
	When I compare the iri to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue                      | value                          | result |
		| "http://foo.bar/?baz=qux#quux" | "Hello"                        | false  |
		| "http://foo.bar/?baz=qux#quux" | "Goodbye"                      | false  |
		| "http://foo.bar/?baz=qux#quux" | 1                              | false  |
		| "http://foo.bar/?baz=qux#quux" | 1.1                            | false  |
		| "http://foo.bar/?baz=qux#quux" | [1,2,3]                        | false  |
		| "http://foo.bar/?baz=qux#quux" | { "first": "1" }               | false  |
		| "http://foo.bar/?baz=qux#quux" | true                           | false  |
		| "http://foo.bar/?baz=qux#quux" | false                          | false  |
		| "http://foo.bar/?baz=qux#quux" | "2018-11-13T20:20:39+00:00"    | false  |
		| "http://foo.bar/?baz=qux#quux" | "P3Y6M4DT12H30M5S"             | false  |
		| "http://foo.bar/?baz=qux#quux" | "2018-11-13"                   | false  |
		| "http://foo.bar/?baz=qux#quux" | "hello@endjin.com"             | false  |
		| "http://foo.bar/?baz=qux#quux" | "www.example.com"              | false  |
		| "http://foo.bar/?baz=qux#quux" | "http://foo.bar/?baz=qux#quux" | true   |
		| "http://foo.bar/?baz=qux#quux" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "http://foo.bar/?baz=qux#quux" | "{ \"first\": \"1\" }"         | false  |
		| "http://foo.bar/?baz=qux#quux" | "192.168.0.1"                  | false  |
		| "http://foo.bar/?baz=qux#quux" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "http://foo.bar/?baz=qux#quux" | <new object()>                 | false  |
		| "http://foo.bar/?baz=qux#quux" | null                           | false  |

Scenario Outline: Equals for iri dotnet backed value as an object
	Given the dotnet backed JsonIri <jsonValue>
	When I compare the iri to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue                      | value                          | result |
		| "http://foo.bar/?baz=qux#quux" | "Hello"                        | false  |
		| "http://foo.bar/?baz=qux#quux" | "Goodbye"                      | false  |
		| "http://foo.bar/?baz=qux#quux" | 1                              | false  |
		| "http://foo.bar/?baz=qux#quux" | 1.1                            | false  |
		| "http://foo.bar/?baz=qux#quux" | [1,2,3]                        | false  |
		| "http://foo.bar/?baz=qux#quux" | { "first": "1" }               | false  |
		| "http://foo.bar/?baz=qux#quux" | true                           | false  |
		| "http://foo.bar/?baz=qux#quux" | false                          | false  |
		| "http://foo.bar/?baz=qux#quux" | "2018-11-13T20:20:39+00:00"    | false  |
		| "http://foo.bar/?baz=qux#quux" | "2018-11-13"                   | false  |
		| "http://foo.bar/?baz=qux#quux" | "P3Y6M4DT12H30M5S"             | false  |
		| "http://foo.bar/?baz=qux#quux" | "hello@endjin.com"             | false  |
		| "http://foo.bar/?baz=qux#quux" | "www.example.com"              | false  |
		| "http://foo.bar/?baz=qux#quux" | "http://foo.bar/?baz=qux#quux" | true   |
		| "http://foo.bar/?baz=qux#quux" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "http://foo.bar/?baz=qux#quux" | "{ \"first\": \"1\" }"         | false  |
		| "http://foo.bar/?baz=qux#quux" | "192.168.0.1"                  | false  |
		| "http://foo.bar/?baz=qux#quux" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "http://foo.bar/?baz=qux#quux" | <new object()>                 | false  |
		| "http://foo.bar/?baz=qux#quux" | null                           | false  |
		| "http://foo.bar/?baz=qux#quux" | <null>                         | false  |
		| "http://foo.bar/?baz=qux#quux" | <undefined>                    | false  |
		| null                           | null                           | true   |
		| null                           | <null>                         | true   |
		| null                           | <undefined>                    | false  |