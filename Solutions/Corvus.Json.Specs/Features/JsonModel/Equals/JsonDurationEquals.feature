Feature: JsonDurationEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonDuration
Scenario Outline: Equals for json element backed value as a duration
	Given the JsonElement backed JsonDuration <jsonValue>
	When I compare it to the duration <value>
	Then the result should be <result>

	Examples:
		| jsonValue          | value              | result |
		| "P3Y6M4DT12H30M5S" | "P3Y6M4DT12H30M5S" | true   |
		| "Garbage"          | "P3Y6M4DT12H30M5S" | false  |
		| "P3Y6M4DT12H30M5S" | "Goodbye"          | false  |
		| null               | null               | true   |
		| null               | "P3Y6M4DT12H30M5S" | false  |

Scenario Outline: Equals for dotnet backed value as a duration
	Given the dotnet backed JsonDuration <jsonValue>
	When I compare it to the duration <value>
	Then the result should be <result>

	Examples:
		| jsonValue          | value              | result |
		| "Garbage"          | "P3Y6M4DT12H30M5S" | false  |
		| "P3Y6M4DT12H30M5S" | "P3Y6M4DT12H30M5S" | true   |
		| "P3Y6M4DT12H30M5S" | "Goodbye"          | false  |

Scenario Outline: Equals for duration json element backed value as an IJsonValue
	Given the JsonElement backed JsonDuration <jsonValue>
	When I compare the duration to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue          | value                          | result |
		| "P3Y6M4DT12H30M5S" | "Hello"                        | false  |
		| "P3Y6M4DT12H30M5S" | "Goodbye"                      | false  |
		| "P3Y6M4DT12H30M5S" | 1                              | false  |
		| "P3Y6M4DT12H30M5S" | 1.1                            | false  |
		| "P3Y6M4DT12H30M5S" | [1,2,3]                        | false  |
		| "P3Y6M4DT12H30M5S" | { "first": "1" }               | false  |
		| "P3Y6M4DT12H30M5S" | true                           | false  |
		| "P3Y6M4DT12H30M5S" | false                          | false  |
		| "P3Y6M4DT12H30M5S" | "2018-11-13T20:20:39+00:00"    | false  |
		| "P3Y6M4DT12H30M5S" | "2018-11-13"                   | false  |
		| "P3Y6M4DT12H30M5S" | "P3Y6M4DT12H30M5S"             | true   |
		| "Garbage"          | "2018-11-13"                   | false  |
		| "P3Y6M4DT12H30M5S" | "hello@endjin.com"             | false  |
		| "P3Y6M4DT12H30M5S" | "www.example.com"              | false  |
		| "P3Y6M4DT12H30M5S" | "http://foo.bar/?baz=qux#quux" | false  |
		| "P3Y6M4DT12H30M5S" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "P3Y6M4DT12H30M5S" | "{ \"first\": \"1\" }"         | false  |
		| "P3Y6M4DT12H30M5S" | "192.168.0.1"                  | false  |
		| "P3Y6M4DT12H30M5S" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for duration dotnet backed value as an IJsonValue
	Given the dotnet backed JsonDuration <jsonValue>
	When I compare the duration to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue          | value                          | result |
		| "P3Y6M4DT12H30M5S" | "Hello"                        | false  |
		| "P3Y6M4DT12H30M5S" | "Goodbye"                      | false  |
		| "P3Y6M4DT12H30M5S" | 1                              | false  |
		| "P3Y6M4DT12H30M5S" | 1.1                            | false  |
		| "P3Y6M4DT12H30M5S" | [1,2,3]                        | false  |
		| "P3Y6M4DT12H30M5S" | { "first": "1" }               | false  |
		| "P3Y6M4DT12H30M5S" | true                           | false  |
		| "P3Y6M4DT12H30M5S" | false                          | false  |
		| "P3Y6M4DT12H30M5S" | "2018-11-13T20:20:39+00:00"    | false  |
		| "P3Y6M4DT12H30M5S" | "P3Y6M4DT12H30M5S"             | true   |
		| "P3Y6M4DT12H30M5S" | "2018-11-13"                   | false  |
		| "Garbage"          | "P3Y6M4DT12H30M5S"             | false  |
		| "P3Y6M4DT12H30M5S" | "hello@endjin.com"             | false  |
		| "P3Y6M4DT12H30M5S" | "www.example.com"              | false  |
		| "P3Y6M4DT12H30M5S" | "http://foo.bar/?baz=qux#quux" | false  |
		| "P3Y6M4DT12H30M5S" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "P3Y6M4DT12H30M5S" | "{ \"first\": \"1\" }"         | false  |
		| "P3Y6M4DT12H30M5S" | "192.168.0.1"                  | false  |
		| "P3Y6M4DT12H30M5S" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for duration json element backed value as an object
	Given the JsonElement backed JsonDuration <jsonValue>
	When I compare the duration to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue          | value                          | result |
		| "P3Y6M4DT12H30M5S" | "Hello"                        | false  |
		| "P3Y6M4DT12H30M5S" | "Goodbye"                      | false  |
		| "P3Y6M4DT12H30M5S" | 1                              | false  |
		| "P3Y6M4DT12H30M5S" | 1.1                            | false  |
		| "P3Y6M4DT12H30M5S" | [1,2,3]                        | false  |
		| "P3Y6M4DT12H30M5S" | { "first": "1" }               | false  |
		| "P3Y6M4DT12H30M5S" | true                           | false  |
		| "P3Y6M4DT12H30M5S" | false                          | false  |
		| "P3Y6M4DT12H30M5S" | "2018-11-13T20:20:39+00:00"    | false  |
		| "P3Y6M4DT12H30M5S" | "P3Y6M4DT12H30M5S"             | true   |
		| "P3Y6M4DT12H30M5S" | "2018-11-13"                   | false  |
		| "P3Y6M4DT12H30M5S" | "hello@endjin.com"             | false  |
		| "P3Y6M4DT12H30M5S" | "www.example.com"              | false  |
		| "P3Y6M4DT12H30M5S" | "http://foo.bar/?baz=qux#quux" | false  |
		| "P3Y6M4DT12H30M5S" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "P3Y6M4DT12H30M5S" | "{ \"first\": \"1\" }"         | false  |
		| "P3Y6M4DT12H30M5S" | "192.168.0.1"                  | false  |
		| "P3Y6M4DT12H30M5S" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "P3Y6M4DT12H30M5S" | <new object()>                 | false  |
		| "P3Y6M4DT12H30M5S" | null                           | false  |

Scenario Outline: Equals for duration dotnet backed value as an object
	Given the dotnet backed JsonDuration <jsonValue>
	When I compare the duration to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue          | value                          | result |
		| "P3Y6M4DT12H30M5S" | "Hello"                        | false  |
		| "P3Y6M4DT12H30M5S" | "Goodbye"                      | false  |
		| "P3Y6M4DT12H30M5S" | 1                              | false  |
		| "P3Y6M4DT12H30M5S" | 1.1                            | false  |
		| "P3Y6M4DT12H30M5S" | [1,2,3]                        | false  |
		| "P3Y6M4DT12H30M5S" | { "first": "1" }               | false  |
		| "P3Y6M4DT12H30M5S" | true                           | false  |
		| "P3Y6M4DT12H30M5S" | false                          | false  |
		| "P3Y6M4DT12H30M5S" | "2018-11-13T20:20:39+00:00"    | false  |
		| "P3Y6M4DT12H30M5S" | "2018-11-13"                   | false  |
		| "P3Y6M4DT12H30M5S" | "P3Y6M4DT12H30M5S"             | true   |
		| "P3Y6M4DT12H30M5S" | "hello@endjin.com"             | false  |
		| "P3Y6M4DT12H30M5S" | "www.example.com"              | false  |
		| "P3Y6M4DT12H30M5S" | "http://foo.bar/?baz=qux#quux" | false  |
		| "P3Y6M4DT12H30M5S" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "P3Y6M4DT12H30M5S" | "{ \"first\": \"1\" }"         | false  |
		| "P3Y6M4DT12H30M5S" | "192.168.0.1"                  | false  |
		| "P3Y6M4DT12H30M5S" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "P3Y6M4DT12H30M5S" | <new object()>                 | false  |
		| "P3Y6M4DT12H30M5S" | null                           | false  |
		| "P3Y6M4DT12H30M5S" | <null>                         | false  |
		| "P3Y6M4DT12H30M5S" | <undefined>                    | false  |
		| null               | null                           | true   |
		| null               | <null>                         | true   |
		| null               | <undefined>                    | false  |