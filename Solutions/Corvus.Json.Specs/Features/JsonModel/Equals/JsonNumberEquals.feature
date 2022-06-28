Feature: JsonNumberEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonNumber
Scenario Outline: Equals for json element backed value as a number
	Given the JsonElement backed JsonNumber <jsonValue>
	When I compare it to the number <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value | result |
		| 1         | 1     | true   |
		| 1.1       | 1.1   | true   |
		| 1.1       | 1     | false  |
		| 1.1       | 3     | false  |
		| null      | null  | true   |
		| null      | 1.1   | false  |

Scenario Outline: Equals for dotnet backed value as a number
	Given the dotnet backed JsonNumber <jsonValue>
	When I compare it to the number <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value | result |
		| 1         | 1     | true   |
		| 1.1       | 1.1   | true   |
		| 1.1       | 1     | false  |
		| 1         | 3     | false  |

Scenario Outline: Equals for number json element backed value as an IJsonValue
	Given the JsonElement backed JsonNumber <jsonValue>
	When I compare the number to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value                          | result |
		| 1.1       | "Hello"                        | false  |
		| 1.1       | "Goodbye"                      | false  |
		| 1.1       | 1                              | false  |
		| 1.1       | 1.1                            | true   |
		| 1.1       | [1,2,3]                        | false  |
		| 1.1       | { "first": "1" }               | false  |
		| 1.1       | true                           | false  |
		| 1.1       | false                          | false  |
		| 1.1       | "2018-11-13T20:20:39+00:00"    | false  |
		| 1.1       | "2018-11-13"                   | false  |
		| 1.1       | "P3Y6M4DT12H30M5S"             | false  |
		| 1.1       | "2018-11-13"                   | false  |
		| 1.1       | "hello@endjin.com"             | false  |
		| 1.1       | "www.example.com"              | false  |
		| 1.1       | "http://foo.bar/?baz=qux#quux" | false  |
		| 1.1       | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| 1.1       | "{ \"first\": \"1\" }"         | false  |
		| 1.1       | "192.168.0.1"                  | false  |
		| 1.1       | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for number dotnet backed value as an IJsonValue
	Given the dotnet backed JsonNumber <jsonValue>
	When I compare the number to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value                          | result |
		| 1.1       | "Hello"                        | false  |
		| 1.1       | "Goodbye"                      | false  |
		| 1.1       | 1                              | false  |
		| 1.1       | 1.1                            | true   |
		| 1.1       | [1,2,3]                        | false  |
		| 1.1       | { "first": "1" }               | false  |
		| 1.1       | true                           | false  |
		| 1.1       | false                          | false  |
		| 1.1       | "2018-11-13T20:20:39+00:00"    | false  |
		| 1.1       | "P3Y6M4DT12H30M5S"             | false  |
		| 1.1       | "2018-11-13"                   | false  |
		| 1.1       | "P3Y6M4DT12H30M5S"             | false  |
		| 1.1       | "hello@endjin.com"             | false  |
		| 1.1       | "www.example.com"              | false  |
		| 1.1       | "http://foo.bar/?baz=qux#quux" | false  |
		| 1.1       | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| 1.1       | "{ \"first\": \"1\" }"         | false  |
		| 1.1       | "192.168.0.1"                  | false  |
		| 1.1       | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for number json element backed value as an object
	Given the JsonElement backed JsonNumber <jsonValue>
	When I compare the number to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value                          | result |
		| 1.1       | "Hello"                        | false  |
		| 1.1       | "Goodbye"                      | false  |
		| 1.1       | 1                              | false  |
		| 1.1       | 1.1                            | true   |
		| 1.1       | [1,2,3]                        | false  |
		| 1.1       | { "first": "1" }               | false  |
		| 1.1       | true                           | false  |
		| 1.1       | false                          | false  |
		| 1.1       | "2018-11-13T20:20:39+00:00"    | false  |
		| 1.1       | "P3Y6M4DT12H30M5S"             | false  |
		| 1.1       | "2018-11-13"                   | false  |
		| 1.1       | "hello@endjin.com"             | false  |
		| 1.1       | "www.example.com"              | false  |
		| 1.1       | "http://foo.bar/?baz=qux#quux" | false  |
		| 1.1       | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| 1.1       | "{ \"first\": \"1\" }"         | false  |
		| 1.1       | "192.168.0.1"                  | false  |
		| 1.1       | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| 1.1       | <new object()>                 | false  |
		| 1.1       | null                           | false  |

Scenario Outline: Equals for number dotnet backed value as an object
	Given the dotnet backed JsonNumber <jsonValue>
	When I compare the number to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value                          | result |
		| 1.1       | "Hello"                        | false  |
		| 1.1       | "Goodbye"                      | false  |
		| 1.1       | 1                              | false  |
		| 1.1       | 1.1                            | true   |
		| 1.1       | [1,2,3]                        | false  |
		| 1.1       | { "first": "1" }               | false  |
		| 1.1       | true                           | false  |
		| 1.1       | false                          | false  |
		| 1.1       | "2018-11-13T20:20:39+00:00"    | false  |
		| 1.1       | "2018-11-13"                   | false  |
		| 1.1       | "P3Y6M4DT12H30M5S"             | false  |
		| 1.1       | "hello@endjin.com"             | false  |
		| 1.1       | "www.example.com"              | false  |
		| 1.1       | "http://foo.bar/?baz=qux#quux" | false  |
		| 1.1       | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| 1.1       | "{ \"first\": \"1\" }"         | false  |
		| 1.1       | "192.168.0.1"                  | false  |
		| 1.1       | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| 1.1       | <new object()>                 | false  |
		| 1.1       | null                           | false  |
		| 1.1       | <null>                         | false  |
		| 1.1       | <undefined>                    | false  |
		| null      | null                           | true   |
		| null      | <null>                         | true   |
		| null      | <undefined>                    | false  |