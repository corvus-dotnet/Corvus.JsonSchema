Feature: JsonPointerEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonPointer
Scenario Outline: Equals for json element backed value as a pointer
	Given the JsonElement backed JsonPointer <jsonValue>
	When I compare it to the pointer <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value    | result |
		| "/a~1b"   | "/a~1b"  | true   |
		| "/a~1b"   | "#/a~1b" | false  |
		| null      | null     | true   |
		| null      | "/a~1b"  | false  |

Scenario Outline: Equals for dotnet backed value as a pointer
	Given the dotnet backed JsonPointer <jsonValue>
	When I compare it to the pointer <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value    | result |
		| "/a~1b"   | "/a~1b"  | true   |
		| "/a~1b"   | "#/a~1b" | false  |

Scenario Outline: Equals for pointer json element backed value as an IJsonValue
	Given the JsonElement backed JsonPointer <jsonValue>
	When I compare the pointer to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value                          | result |
		| "/a~1b"   | "Hello"                        | false  |
		| "/a~1b"   | "Goodbye"                      | false  |
		| "/a~1b"   | 1                              | false  |
		| "/a~1b"   | 1.1                            | false  |
		| "/a~1b"   | [1,2,3]                        | false  |
		| "/a~1b"   | { "first": "1" }               | false  |
		| "/a~1b"   | true                           | false  |
		| "/a~1b"   | false                          | false  |
		| "/a~1b"   | "2018-11-13T20:20:39+00:00"    | false  |
		| "/a~1b"   | "2018-11-13"                   | false  |
		| "/a~1b"   | "P3Y6M4DT12H30M5S"             | false  |
		| "/a~1b"   | "2018-11-13"                   | false  |
		| "/a~1b"   | "hello@endjin.com"             | false  |
		| "/a~1b"   | "www.example.com"              | false  |
		| "/a~1b"   | "/a~1b"                        | true   |
		| "/a~1b"   | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "/a~1b"   | "{ \"first\": \"1\" }"         | false  |
		| "/a~1b"   | "192.168.0.1"                  | false  |
		| "/a~1b"   | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for pointer dotnet backed value as an IJsonValue
	Given the dotnet backed JsonPointer <jsonValue>
	When I compare the pointer to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value                          | result |
		| "/a~1b"   | "Hello"                        | false  |
		| "/a~1b"   | "Goodbye"                      | false  |
		| "/a~1b"   | 1                              | false  |
		| "/a~1b"   | 1.1                            | false  |
		| "/a~1b"   | [1,2,3]                        | false  |
		| "/a~1b"   | { "first": "1" }               | false  |
		| "/a~1b"   | true                           | false  |
		| "/a~1b"   | false                          | false  |
		| "/a~1b"   | "2018-11-13T20:20:39+00:00"    | false  |
		| "/a~1b"   | "P3Y6M4DT12H30M5S"             | false  |
		| "/a~1b"   | "2018-11-13"                   | false  |
		| "/a~1b"   | "P3Y6M4DT12H30M5S"             | false  |
		| "/a~1b"   | "hello@endjin.com"             | false  |
		| "/a~1b"   | "www.example.com"              | false  |
		| "/a~1b"   | "/a~1b"                        | true   |
		| "/a~1b"   | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "/a~1b"   | "{ \"first\": \"1\" }"         | false  |
		| "/a~1b"   | "192.168.0.1"                  | false  |
		| "/a~1b"   | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for pointer json element backed value as an object
	Given the JsonElement backed JsonPointer <jsonValue>
	When I compare the pointer to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value                          | result |
		| "/a~1b"   | "Hello"                        | false  |
		| "/a~1b"   | "Goodbye"                      | false  |
		| "/a~1b"   | 1                              | false  |
		| "/a~1b"   | 1.1                            | false  |
		| "/a~1b"   | [1,2,3]                        | false  |
		| "/a~1b"   | { "first": "1" }               | false  |
		| "/a~1b"   | true                           | false  |
		| "/a~1b"   | false                          | false  |
		| "/a~1b"   | "2018-11-13T20:20:39+00:00"    | false  |
		| "/a~1b"   | "P3Y6M4DT12H30M5S"             | false  |
		| "/a~1b"   | "2018-11-13"                   | false  |
		| "/a~1b"   | "hello@endjin.com"             | false  |
		| "/a~1b"   | "www.example.com"              | false  |
		| "/a~1b"   | "/a~1b"                        | true   |
		| "/a~1b"   | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "/a~1b"   | "{ \"first\": \"1\" }"         | false  |
		| "/a~1b"   | "192.168.0.1"                  | false  |
		| "/a~1b"   | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "/a~1b"   | <new object()>                 | false  |
		| "/a~1b"   | null                           | false  |

Scenario Outline: Equals for pointer dotnet backed value as an object
	Given the dotnet backed JsonPointer <jsonValue>
	When I compare the pointer to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value                          | result |
		| "/a~1b"   | "Hello"                        | false  |
		| "/a~1b"   | "Goodbye"                      | false  |
		| "/a~1b"   | 1                              | false  |
		| "/a~1b"   | 1.1                            | false  |
		| "/a~1b"   | [1,2,3]                        | false  |
		| "/a~1b"   | { "first": "1" }               | false  |
		| "/a~1b"   | true                           | false  |
		| "/a~1b"   | false                          | false  |
		| "/a~1b"   | "2018-11-13T20:20:39+00:00"    | false  |
		| "/a~1b"   | "2018-11-13"                   | false  |
		| "/a~1b"   | "P3Y6M4DT12H30M5S"             | false  |
		| "/a~1b"   | "hello@endjin.com"             | false  |
		| "/a~1b"   | "www.example.com"              | false  |
		| "/a~1b"   | "/a~1b"                        | true   |
		| "/a~1b"   | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "/a~1b"   | "{ \"first\": \"1\" }"         | false  |
		| "/a~1b"   | "192.168.0.1"                  | false  |
		| "/a~1b"   | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "/a~1b"   | <new object()>                 | false  |
		| "/a~1b"   | null                           | false  |
		| "/a~1b"   | <null>                         | false  |
		| "/a~1b"   | <undefined>                    | false  |
		| null      | null                           | true   |
		| null      | <null>                         | true   |
		| null      | <undefined>                    | false  |