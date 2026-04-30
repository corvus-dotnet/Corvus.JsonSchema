Feature: JsonRelativePointerEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonRelativePointer
Scenario Outline: Equals for json element backed value as a relativePointer
	Given the JsonElement backed JsonRelativePointer <jsonValue>
	When I compare it to the relativePointer <value>
	Then the result should be <result>

	Examples:
		| jsonValue   | value       | result |
		| "0/foo/bar" | "0/foo/bar" | true   |
		| "0/foo/bar" | "/foo/bar"  | false  |
		| null        | null        | true   |
		| null        | "0/foo/bar" | false  |

Scenario Outline: Equals for dotnet backed value as a relativePointer
	Given the dotnet backed JsonRelativePointer <jsonValue>
	When I compare it to the relativePointer <value>
	Then the result should be <result>

	Examples:
		| jsonValue   | value       | result |
		| "0/foo/bar" | "0/foo/bar" | true   |
		| "0/foo/bar" | "/foo/bar"  | false  |

Scenario Outline: Equals for relativePointer json element backed value as an IJsonValue
	Given the JsonElement backed JsonRelativePointer <jsonValue>
	When I compare the relativePointer to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue   | value                          | result |
		| "0/foo/bar" | "Hello"                        | false  |
		| "0/foo/bar" | "Goodbye"                      | false  |
		| "0/foo/bar" | 1                              | false  |
		| "0/foo/bar" | 1.1                            | false  |
		| "0/foo/bar" | [1,2,3]                        | false  |
		| "0/foo/bar" | { "first": "1" }               | false  |
		| "0/foo/bar" | true                           | false  |
		| "0/foo/bar" | false                          | false  |
		| "0/foo/bar" | "2018-11-13T20:20:39+00:00"    | false  |
		| "0/foo/bar" | "2018-11-13"                   | false  |
		| "0/foo/bar" | "P3Y6M4DT12H30M5S"             | false  |
		| "0/foo/bar" | "2018-11-13"                   | false  |
		| "0/foo/bar" | "hello@endjin.com"             | false  |
		| "0/foo/bar" | "www.example.com"              | false  |
		| "0/foo/bar" | "0/foo/bar"                    | true   |
		| "0/foo/bar" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "0/foo/bar" | "{ \"first\": \"1\" }"         | false  |
		| "0/foo/bar" | "192.168.0.1"                  | false  |
		| "0/foo/bar" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for relativePointer dotnet backed value as an IJsonValue
	Given the dotnet backed JsonRelativePointer <jsonValue>
	When I compare the relativePointer to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue   | value                          | result |
		| "0/foo/bar" | "Hello"                        | false  |
		| "0/foo/bar" | "Goodbye"                      | false  |
		| "0/foo/bar" | 1                              | false  |
		| "0/foo/bar" | 1.1                            | false  |
		| "0/foo/bar" | [1,2,3]                        | false  |
		| "0/foo/bar" | { "first": "1" }               | false  |
		| "0/foo/bar" | true                           | false  |
		| "0/foo/bar" | false                          | false  |
		| "0/foo/bar" | "2018-11-13T20:20:39+00:00"    | false  |
		| "0/foo/bar" | "P3Y6M4DT12H30M5S"             | false  |
		| "0/foo/bar" | "2018-11-13"                   | false  |
		| "0/foo/bar" | "P3Y6M4DT12H30M5S"             | false  |
		| "0/foo/bar" | "hello@endjin.com"             | false  |
		| "0/foo/bar" | "www.example.com"              | false  |
		| "0/foo/bar" | "0/foo/bar"                    | true   |
		| "0/foo/bar" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "0/foo/bar" | "{ \"first\": \"1\" }"         | false  |
		| "0/foo/bar" | "192.168.0.1"                  | false  |
		| "0/foo/bar" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for relativePointer json element backed value as an object
	Given the JsonElement backed JsonRelativePointer <jsonValue>
	When I compare the relativePointer to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue   | value                          | result |
		| "0/foo/bar" | "Hello"                        | false  |
		| "0/foo/bar" | "Goodbye"                      | false  |
		| "0/foo/bar" | 1                              | false  |
		| "0/foo/bar" | 1.1                            | false  |
		| "0/foo/bar" | [1,2,3]                        | false  |
		| "0/foo/bar" | { "first": "1" }               | false  |
		| "0/foo/bar" | true                           | false  |
		| "0/foo/bar" | false                          | false  |
		| "0/foo/bar" | "2018-11-13T20:20:39+00:00"    | false  |
		| "0/foo/bar" | "P3Y6M4DT12H30M5S"             | false  |
		| "0/foo/bar" | "2018-11-13"                   | false  |
		| "0/foo/bar" | "hello@endjin.com"             | false  |
		| "0/foo/bar" | "www.example.com"              | false  |
		| "0/foo/bar" | "0/foo/bar"                    | true   |
		| "0/foo/bar" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "0/foo/bar" | "{ \"first\": \"1\" }"         | false  |
		| "0/foo/bar" | "192.168.0.1"                  | false  |
		| "0/foo/bar" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "0/foo/bar" | <new object()>                 | false  |
		| "0/foo/bar" | null                           | false  |

Scenario Outline: Equals for relativePointer dotnet backed value as an object
	Given the dotnet backed JsonRelativePointer <jsonValue>
	When I compare the relativePointer to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue   | value                          | result |
		| "0/foo/bar" | "Hello"                        | false  |
		| "0/foo/bar" | "Goodbye"                      | false  |
		| "0/foo/bar" | 1                              | false  |
		| "0/foo/bar" | 1.1                            | false  |
		| "0/foo/bar" | [1,2,3]                        | false  |
		| "0/foo/bar" | { "first": "1" }               | false  |
		| "0/foo/bar" | true                           | false  |
		| "0/foo/bar" | false                          | false  |
		| "0/foo/bar" | "2018-11-13T20:20:39+00:00"    | false  |
		| "0/foo/bar" | "2018-11-13"                   | false  |
		| "0/foo/bar" | "P3Y6M4DT12H30M5S"             | false  |
		| "0/foo/bar" | "hello@endjin.com"             | false  |
		| "0/foo/bar" | "www.example.com"              | false  |
		| "0/foo/bar" | "0/foo/bar"                    | true   |
		| "0/foo/bar" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "0/foo/bar" | "{ \"first\": \"1\" }"         | false  |
		| "0/foo/bar" | "192.168.0.1"                  | false  |
		| "0/foo/bar" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "0/foo/bar" | <new object()>                 | false  |
		| "0/foo/bar" | null                           | false  |
		| "0/foo/bar" | <null>                         | false  |
		| "0/foo/bar" | <undefined>                    | false  |
		| null        | null                           | true   |
		| null        | <null>                         | true   |
		| null        | <undefined>                    | false  |