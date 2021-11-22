Feature: JsonIdnHostnameEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonIdnHostname
Scenario Outline: Equals for json element backed value as a idnHostname
	Given the JsonElement backed JsonIdnHostname <jsonValue>
	When I compare it to the idnHostname <value>
	Then the result should be <result>

	Examples:
		| jsonValue         | value             | result |
		| "www.example.com" | "www.example.com" | true   |
		| "Garbage"         | "www.example.com" | false  |
		| "www.example.com" | "Goodbye"         | false  |
		| null              | null              | true   |
		| null              | "www.example.com" | false  |

Scenario Outline: Equals for dotnet backed value as a idnHostname
	Given the dotnet backed JsonIdnHostname <jsonValue>
	When I compare it to the idnHostname <value>
	Then the result should be <result>

	Examples:
		| jsonValue         | value             | result |
		| "Garbage"         | "www.example.com" | false  |
		| "www.example.com" | "www.example.com" | true   |
		| "www.example.com" | "Goodbye"         | false  |

Scenario Outline: Equals for idnHostname json element backed value as an IJsonValue
	Given the JsonElement backed JsonIdnHostname <jsonValue>
	When I compare the idnHostname to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue         | value                          | result |
		| "www.example.com" | "Hello"                        | false  |
		| "www.example.com" | "Goodbye"                      | false  |
		| "www.example.com" | 1                              | false  |
		| "www.example.com" | 1.1                            | false  |
		| "www.example.com" | [1,2,3]                        | false  |
		| "www.example.com" | { "first": "1" }               | false  |
		| "www.example.com" | true                           | false  |
		| "www.example.com" | false                          | false  |
		| "www.example.com" | "2018-11-13T20:20:39+00:00"    | false  |
		| "www.example.com" | "2018-11-13"                   | false  |
		| "www.example.com" | "P3Y6M4DT12H30M5S"             | false  |
		| "Garbage"         | "2018-11-13"                   | false  |
		| "www.example.com" | "hello@endjin.com"             | false  |
		| "www.example.com" | "www.example.com"              | true   |
		| "www.example.com" | "http://foo.bar/?baz=qux#quux" | false  |
		| "www.example.com" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "www.example.com" | "{ \"first\": \"1\" }"         | false  |
		| "www.example.com" | "192.168.0.1"                  | false  |
		| "www.example.com" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for idnHostname dotnet backed value as an IJsonValue
	Given the dotnet backed JsonIdnHostname <jsonValue>
	When I compare the idnHostname to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue         | value                          | result |
		| "www.example.com" | "Hello"                        | false  |
		| "www.example.com" | "Goodbye"                      | false  |
		| "www.example.com" | 1                              | false  |
		| "www.example.com" | 1.1                            | false  |
		| "www.example.com" | [1,2,3]                        | false  |
		| "www.example.com" | { "first": "1" }               | false  |
		| "www.example.com" | true                           | false  |
		| "www.example.com" | false                          | false  |
		| "www.example.com" | "2018-11-13T20:20:39+00:00"    | false  |
		| "www.example.com" | "P3Y6M4DT12H30M5S"             | false  |
		| "www.example.com" | "2018-11-13"                   | false  |
		| "Garbage"         | "P3Y6M4DT12H30M5S"             | false  |
		| "www.example.com" | "hello@endjin.com"             | false  |
		| "www.example.com" | "www.example.com"              | true   |
		| "www.example.com" | "http://foo.bar/?baz=qux#quux" | false  |
		| "www.example.com" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "www.example.com" | "{ \"first\": \"1\" }"         | false  |
		| "www.example.com" | "192.168.0.1"                  | false  |
		| "www.example.com" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for idnHostname json element backed value as an object
	Given the JsonElement backed JsonIdnHostname <jsonValue>
	When I compare the idnHostname to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue         | value                          | result |
		| "www.example.com" | "Hello"                        | false  |
		| "www.example.com" | "Goodbye"                      | false  |
		| "www.example.com" | 1                              | false  |
		| "www.example.com" | 1.1                            | false  |
		| "www.example.com" | [1,2,3]                        | false  |
		| "www.example.com" | { "first": "1" }               | false  |
		| "www.example.com" | true                           | false  |
		| "www.example.com" | false                          | false  |
		| "www.example.com" | "2018-11-13T20:20:39+00:00"    | false  |
		| "www.example.com" | "P3Y6M4DT12H30M5S"             | false  |
		| "www.example.com" | "2018-11-13"                   | false  |
		| "www.example.com" | "hello@endjin.com"             | false  |
		| "www.example.com" | "www.example.com"              | true   |
		| "www.example.com" | "http://foo.bar/?baz=qux#quux" | false  |
		| "www.example.com" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "www.example.com" | "{ \"first\": \"1\" }"         | false  |
		| "www.example.com" | "192.168.0.1"                  | false  |
		| "www.example.com" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "www.example.com" | <new object()>                 | false  |
		| "www.example.com" | null                           | false  |

Scenario Outline: Equals for idnHostname dotnet backed value as an object
	Given the dotnet backed JsonIdnHostname <jsonValue>
	When I compare the idnHostname to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue         | value                          | result |
		| "www.example.com" | "Hello"                        | false  |
		| "www.example.com" | "Goodbye"                      | false  |
		| "www.example.com" | 1                              | false  |
		| "www.example.com" | 1.1                            | false  |
		| "www.example.com" | [1,2,3]                        | false  |
		| "www.example.com" | { "first": "1" }               | false  |
		| "www.example.com" | true                           | false  |
		| "www.example.com" | false                          | false  |
		| "www.example.com" | "2018-11-13T20:20:39+00:00"    | false  |
		| "www.example.com" | "2018-11-13"                   | false  |
		| "www.example.com" | "P3Y6M4DT12H30M5S"             | false  |
		| "www.example.com" | "hello@endjin.com"             | false  |
		| "www.example.com" | "www.example.com"              | true   |
		| "www.example.com" | "http://foo.bar/?baz=qux#quux" | false  |
		| "www.example.com" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "www.example.com" | "{ \"first\": \"1\" }"         | false  |
		| "www.example.com" | "192.168.0.1"                  | false  |
		| "www.example.com" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "www.example.com" | <new object()>                 | false  |
		| "www.example.com" | null                           | false  |
		| "www.example.com" | <null>                         | false  |
		| "www.example.com" | <undefined>                    | false  |
		| null              | null                           | true   |
		| null              | <null>                         | true   |
		| null              | <undefined>                    | false  |