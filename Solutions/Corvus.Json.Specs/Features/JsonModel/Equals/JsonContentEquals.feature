Feature: JsonContentEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonContent
Scenario Outline: Equals for json element backed value as a content
	Given the JsonElement backed JsonContent <jsonValue>
	When I compare it to the content <value>
	Then the result should be <result>

	Examples:
		| jsonValue              | value                  | result |
		| "{ \"first\": \"1\" }" | "{ \"first\": \"1\" }" | true   |
		| "{ \"first\": \"1\" }" | "Goodbye"              | false  |
		| null                   | null                   | true   |
		| null                   | "{ \"first\": \"1\" }" | false  |

Scenario Outline: Equals for dotnet backed value as a content
	Given the dotnet backed JsonContent <jsonValue>
	When I compare it to the content <value>
	Then the result should be <result>

	Examples:
		| jsonValue              | value                  | result |
		| "{ \"first\": \"1\" }" | "{ \"first\": \"1\" }" | true   |
		| "{ \"first\": \"1\" }" | "Goodbye"              | false  |

Scenario Outline: Equals for content json element backed value as an IJsonValue
	Given the JsonElement backed JsonContent <jsonValue>
	When I compare the content to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue              | value                          | result |
		| "{ \"first\": \"1\" }" | "Hello"                        | false  |
		| "{ \"first\": \"1\" }" | "Goodbye"                      | false  |
		| "{ \"first\": \"1\" }" | 1                              | false  |
		| "{ \"first\": \"1\" }" | 1.1                            | false  |
		| "{ \"first\": \"1\" }" | [1,2,3]                        | false  |
		| "{ \"first\": \"1\" }" | { "first": "1" }               | false  |
		| "{ \"first\": \"1\" }" | true                           | false  |
		| "{ \"first\": \"1\" }" | false                          | false  |
		| "{ \"first\": \"1\" }" | "2018-11-13T20:20:39+00:00"    | false  |
		| "{ \"first\": \"1\" }" | "P3Y6M4DT12H30M5S"             | false  |
		| "{ \"first\": \"1\" }" | "2018-11-13"                   | false  |
		| "{ \"first\": \"1\" }" | "hello@endjin.com"             | false  |
		| "{ \"first\": \"1\" }" | "www.example.com"              | false  |
		| "{ \"first\": \"1\" }" | "http://foo.bar/?baz=qux#quux" | false  |
		| "{ \"first\": \"1\" }" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "{ \"first\": \"1\" }" | "{ \"first\": \"1\" }"         | true   |
		| "{ \"first\": \"1\" }" | "192.168.0.1"                  | false  |
		| "{ \"first\": \"1\" }" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for content dotnet backed value as an IJsonValue
	Given the dotnet backed JsonContent <jsonValue>
	When I compare the content to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue              | value                          | result |
		| "{ \"first\": \"1\" }" | "Hello"                        | false  |
		| "{ \"first\": \"1\" }" | "Goodbye"                      | false  |
		| "{ \"first\": \"1\" }" | 1                              | false  |
		| "{ \"first\": \"1\" }" | 1.1                            | false  |
		| "{ \"first\": \"1\" }" | [1,2,3]                        | false  |
		| "{ \"first\": \"1\" }" | { "first": "1" }               | false  |
		| "{ \"first\": \"1\" }" | true                           | false  |
		| "{ \"first\": \"1\" }" | false                          | false  |
		| "{ \"first\": \"1\" }" | "2018-11-13T20:20:39+00:00"    | false  |
		| "{ \"first\": \"1\" }" | "P3Y6M4DT12H30M5S"             | false  |
		| "{ \"first\": \"1\" }" | "2018-11-13"                   | false  |
		| "{ \"first\": \"1\" }" | "hello@endjin.com"             | false  |
		| "{ \"first\": \"1\" }" | "www.example.com"              | false  |
		| "{ \"first\": \"1\" }" | "http://foo.bar/?baz=qux#quux" | false  |
		| "{ \"first\": \"1\" }" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "{ \"first\": \"1\" }" | "{ \"first\": \"1\" }"         | true   |
		| "{ \"first\": \"1\" }" | "192.168.0.1"                  | false  |
		| "{ \"first\": \"1\" }" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for content json element backed value as an object
	Given the JsonElement backed JsonContent <jsonValue>
	When I compare the content to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue              | value                          | result |
		| "{ \"first\": \"1\" }" | "Hello"                        | false  |
		| "{ \"first\": \"1\" }" | "Goodbye"                      | false  |
		| "{ \"first\": \"1\" }" | 1                              | false  |
		| "{ \"first\": \"1\" }" | 1.1                            | false  |
		| "{ \"first\": \"1\" }" | [1,2,3]                        | false  |
		| "{ \"first\": \"1\" }" | { "first": "1" }               | false  |
		| "{ \"first\": \"1\" }" | true                           | false  |
		| "{ \"first\": \"1\" }" | false                          | false  |
		| "{ \"first\": \"1\" }" | "2018-11-13T20:20:39+00:00"    | false  |
		| "{ \"first\": \"1\" }" | "P3Y6M4DT12H30M5S"             | false  |
		| "{ \"first\": \"1\" }" | "2018-11-13"                   | false  |
		| "{ \"first\": \"1\" }" | "hello@endjin.com"             | false  |
		| "{ \"first\": \"1\" }" | "www.example.com"              | false  |
		| "{ \"first\": \"1\" }" | "http://foo.bar/?baz=qux#quux" | false  |
		| "{ \"first\": \"1\" }" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "{ \"first\": \"1\" }" | "{ \"first\": \"1\" }"         | true   |
		| "{ \"first\": \"1\" }" | "192.168.0.1"                  | false  |
		| "{ \"first\": \"1\" }" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "{ \"first\": \"1\" }" | <new object()>                 | false  |
		| "{ \"first\": \"1\" }" | null                           | false  |

Scenario Outline: Equals for content dotnet backed value as an object
	Given the dotnet backed JsonContent <jsonValue>
	When I compare the content to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue              | value                          | result |
		| "{ \"first\": \"1\" }" | "Hello"                        | false  |
		| "{ \"first\": \"1\" }" | "Goodbye"                      | false  |
		| "{ \"first\": \"1\" }" | 1                              | false  |
		| "{ \"first\": \"1\" }" | 1.1                            | false  |
		| "{ \"first\": \"1\" }" | [1,2,3]                        | false  |
		| "{ \"first\": \"1\" }" | { "first": "1" }               | false  |
		| "{ \"first\": \"1\" }" | true                           | false  |
		| "{ \"first\": \"1\" }" | false                          | false  |
		| "{ \"first\": \"1\" }" | "2018-11-13T20:20:39+00:00"    | false  |
		| "{ \"first\": \"1\" }" | "P3Y6M4DT12H30M5S"             | false  |
		| "{ \"first\": \"1\" }" | "2018-11-13"                   | false  |
		| "{ \"first\": \"1\" }" | "hello@endjin.com"             | false  |
		| "{ \"first\": \"1\" }" | "www.example.com"              | false  |
		| "{ \"first\": \"1\" }" | "http://foo.bar/?baz=qux#quux" | false  |
		| "{ \"first\": \"1\" }" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "{ \"first\": \"1\" }" | "{ \"first\": \"1\" }"         | true   |
		| "{ \"first\": \"1\" }" | "192.168.0.1"                  | false  |
		| "{ \"first\": \"1\" }" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "{ \"first\": \"1\" }" | <new object()>                 | false  |
		| "{ \"first\": \"1\" }" | null                           | false  |
		| "{ \"first\": \"1\" }" | <null>                         | false  |
		| "{ \"first\": \"1\" }" | <undefined>                    | false  |
		| null                   | null                           | true   |
		| null                   | <null>                         | true   |
		| null                   | <undefined>                    | false  |