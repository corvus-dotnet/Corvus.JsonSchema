Feature: JsonStringEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonString
Scenario Outline: Equals for json element backed value as a string
	Given the JsonElement backed JsonString <jsonValue>
	When I compare it to the string <value>
	Then the result should be <result>

	Examples:
		| jsonValue | value     | result |
		| "Hello"   | "Hello"   | true   |
		| "Hello"   | "Goodbye" | false  |
		| null      | null      | true   |
		| null      | "Goodbye" | false  |

Scenario Outline: Equals for dotnet backed value as a string
	Given the dotnet backed JsonString <jsonValue>
	When I compare it to the string <value>
	Then the result should be <result>

	Examples:
		| jsonValue | value     | result |
		| "Hello"   | "Hello"   | true   |
		| "Hello"   | "Goodbye" | false  |

Scenario Outline: Equals for string json element backed value as an IJsonValue
	Given the JsonElement backed JsonString <jsonValue>
	When I compare the string to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue | value                          | result |
		| "Hello"   | "Hello"                        | true   |
		| "Hello"   | "Goodbye"                      | false  |
		| "Hello"   | 1                              | false  |
		| "Hello"   | 1.1                            | false  |
		| "Hello"   | [1,2,3]                        | false  |
		| "Hello"   | { "first": "1" }               | false  |
		| "Hello"   | true                           | false  |
		| "Hello"   | false                          | false  |
		| "Hello"   | "2018-11-13T20:20:39+00:00"    | false  |
		| "Hello"   | "P3Y6M4DT12H30M5S"             | false  |
		| "Hello"   | "2018-11-13"                   | false  |
		| "Hello"   | "hello@endjin.com"             | false  |
		| "Hello"   | "www.example.com"              | false  |
		| "Hello"   | "http://foo.bar/?baz=qux#quux" | false  |
		| "Hello"   | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "Hello"   | "{ \"first\": \"1\" }"         | false  |
		| "Hello"   | "192.168.0.1"                  | false  |
		| "Hello"   | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for string dotnet backed value as an IJsonValue
	Given the dotnet backed JsonString <jsonValue>
	When I compare the string to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue | value                          | result |
		| "Hello"   | "Hello"                        | true   |
		| "Hello"   | "Goodbye"                      | false  |
		| "Hello"   | 1                              | false  |
		| "Hello"   | 1.1                            | false  |
		| "Hello"   | [1,2,3]                        | false  |
		| "Hello"   | { "first": "1" }               | false  |
		| "Hello"   | true                           | false  |
		| "Hello"   | false                          | false  |
		| "Hello"   | "2018-11-13T20:20:39+00:00"    | false  |
		| "Hello"   | "P3Y6M4DT12H30M5S"             | false  |
		| "Hello"   | "2018-11-13"                   | false  |
		| "Hello"   | "hello@endjin.com"             | false  |
		| "Hello"   | "www.example.com"              | false  |
		| "Hello"   | "http://foo.bar/?baz=qux#quux" | false  |
		| "Hello"   | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "Hello"   | "{ \"first\": \"1\" }"         | false  |
		| "Hello"   | "192.168.0.1"                  | false  |
		| "Hello"   | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for string json element backed value as an object
	Given the JsonElement backed JsonString <jsonValue>
	When I compare the string to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue | value                          | result |
		| "Hello"   | "Hello"                        | true   |
		| "Hello"   | "Goodbye"                      | false  |
		| "Hello"   | 1                              | false  |
		| "Hello"   | 1.1                            | false  |
		| "Hello"   | [1,2,3]                        | false  |
		| "Hello"   | { "first": "1" }               | false  |
		| "Hello"   | true                           | false  |
		| "Hello"   | false                          | false  |
		| "Hello"   | "2018-11-13T20:20:39+00:00"    | false  |
		| "Hello"   | "P3Y6M4DT12H30M5S"             | false  |
		| "Hello"   | "2018-11-13"                   | false  |
		| "Hello"   | "hello@endjin.com"             | false  |
		| "Hello"   | "www.example.com"              | false  |
		| "Hello"   | "http://foo.bar/?baz=qux#quux" | false  |
		| "Hello"   | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "Hello"   | "{ \"first\": \"1\" }"         | false  |
		| "Hello"   | "192.168.0.1"                  | false  |
		| "Hello"   | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "Hello"   | <new object()>                 | false  |
		| "Hello"   | null                           | false  |

Scenario Outline: Equals for string dotnet backed value as an object
	Given the dotnet backed JsonString <jsonValue>
	When I compare the string to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue | value                          | result |
		| "Hello"   | "Hello"                        | true   |
		| "Hello"   | "Goodbye"                      | false  |
		| "Hello"   | 1                              | false  |
		| "Hello"   | 1.1                            | false  |
		| "Hello"   | [1,2,3]                        | false  |
		| "Hello"   | { "first": "1" }               | false  |
		| "Hello"   | true                           | false  |
		| "Hello"   | false                          | false  |
		| "Hello"   | "2018-11-13T20:20:39+00:00"    | false  |
		| "Hello"   | "P3Y6M4DT12H30M5S"             | false  |
		| "Hello"   | "2018-11-13"                   | false  |
		| "Hello"   | "hello@endjin.com"             | false  |
		| "Hello"   | "www.example.com"              | false  |
		| "Hello"   | "http://foo.bar/?baz=qux#quux" | false  |
		| "Hello"   | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "Hello"   | "{ \"first\": \"1\" }"         | false  |
		| "Hello"   | "192.168.0.1"                  | false  |
		| "Hello"   | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "Hello"   | <new object()>                 | false  |
		| "Hello"   | null                           | false  |
		| "Hello"   | <null>                         | false  |
		| "Hello"   | <undefined>                    | false  |
		| null      | null                           | true   |
		| null      | <null>                         | true   |
		| null      | <undefined>                    | false  |