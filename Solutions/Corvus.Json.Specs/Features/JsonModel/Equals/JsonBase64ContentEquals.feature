Feature: JsonBase64ContentEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonBase64Content
Scenario Outline: Equals for json element backed value as a base64content
	Given the JsonElement backed JsonBase64Content <jsonValue>
	When I compare it to the base64content <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue                      | value                          | result |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  |
		| null                           | null                           | true   |
		| null                           | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |

Scenario Outline: Equals for dotnet backed value as a base64content
	Given the dotnet backed JsonBase64Content <jsonValue>
	When I compare it to the base64content <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue                      | value                          | result |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  |

Scenario Outline: Equals for base64content json element backed value as an IJsonValue
	Given the JsonElement backed JsonBase64Content <jsonValue>
	When I compare the base64content to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue                      | value                          | result |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \"first\": \"1\" }"         | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for base64content dotnet backed value as an IJsonValue
	Given the dotnet backed JsonBase64Content <jsonValue>
	When I compare the base64content to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue                      | value                          | result |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \"first\": \"1\" }"         | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for base64content json element backed value as an object
	Given the JsonElement backed JsonBase64Content <jsonValue>
	When I compare the base64content to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue                      | value                          | result |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \"first\": \"1\" }"         | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <new object()>                 | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | null                           | false  |

Scenario Outline: Equals for base64content dotnet backed value as an object
	Given the dotnet backed JsonBase64Content <jsonValue>
	When I compare the base64content to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue                      | value                          | result |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \"first\": \"1\" }"         | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <new object()>                 | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | null                           | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <null>                         | false  |
		| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <undefined>                    | false  |
		| null                           | null                           | true   |
		| null                           | <null>                         | true   |
		| null                           | <undefined>                    | false  |