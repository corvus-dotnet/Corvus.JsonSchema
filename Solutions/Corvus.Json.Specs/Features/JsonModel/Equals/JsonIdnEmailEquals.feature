Feature: JsonIdnEmailEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonIdnEmail
Scenario Outline: Equals for json element backed value as a idnEmail
	Given the JsonElement backed JsonIdnEmail <jsonValue>
	When I compare it to the idnEmail <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue          | value              | result |
		| "hello@endjin.com" | "hello@endjin.com" | true   |
		| "Garbage"          | "hello@endjin.com" | false  |
		| "hello@endjin.com" | "Goodbye"          | false  |
		| null               | null               | true   |
		| null               | "hello@endjin.com" | false  |

Scenario Outline: Equals for dotnet backed value as a idnEmail
	Given the dotnet backed JsonIdnEmail <jsonValue>
	When I compare it to the idnEmail <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue          | value              | result |
		| "Garbage"          | "hello@endjin.com" | false  |
		| "hello@endjin.com" | "hello@endjin.com" | true   |
		| "hello@endjin.com" | "Goodbye"          | false  |

Scenario Outline: Equals for idnEmail json element backed value as an IJsonValue
	Given the JsonElement backed JsonIdnEmail <jsonValue>
	When I compare the idnEmail to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue          | value                          | result |
		| "hello@endjin.com" | "Hello"                        | false  |
		| "hello@endjin.com" | "Goodbye"                      | false  |
		| "hello@endjin.com" | 1                              | false  |
		| "hello@endjin.com" | 1.1                            | false  |
		| "hello@endjin.com" | [1,2,3]                        | false  |
		| "hello@endjin.com" | { "first": "1" }               | false  |
		| "hello@endjin.com" | true                           | false  |
		| "hello@endjin.com" | false                          | false  |
		| "hello@endjin.com" | "2018-11-13T20:20:39+00:00"    | false  |
		| "hello@endjin.com" | "2018-11-13"                   | false  |
		| "hello@endjin.com" | "P3Y6M4DT12H30M5S"             | false  |
		| "Garbage"          | "2018-11-13"                   | false  |
		| "hello@endjin.com" | "hello@endjin.com"             | true   |
		| "hello@endjin.com" | "www.example.com"              | false  |
		| "hello@endjin.com" | "http://foo.bar/?baz=qux#quux" | false  |
		| "hello@endjin.com" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "hello@endjin.com" | "{ \"first\": \"1\" }"         | false  |
		| "hello@endjin.com" | "192.168.0.1"                  | false  |
		| "hello@endjin.com" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for idnEmail dotnet backed value as an IJsonValue
	Given the dotnet backed JsonIdnEmail <jsonValue>
	When I compare the idnEmail to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue          | value                          | result |
		| "hello@endjin.com" | "Hello"                        | false  |
		| "hello@endjin.com" | "Goodbye"                      | false  |
		| "hello@endjin.com" | 1                              | false  |
		| "hello@endjin.com" | 1.1                            | false  |
		| "hello@endjin.com" | [1,2,3]                        | false  |
		| "hello@endjin.com" | { "first": "1" }               | false  |
		| "hello@endjin.com" | true                           | false  |
		| "hello@endjin.com" | false                          | false  |
		| "hello@endjin.com" | "2018-11-13T20:20:39+00:00"    | false  |
		| "hello@endjin.com" | "P3Y6M4DT12H30M5S"             | false  |
		| "hello@endjin.com" | "2018-11-13"                   | false  |
		| "Garbage"          | "P3Y6M4DT12H30M5S"             | false  |
		| "hello@endjin.com" | "hello@endjin.com"             | true   |
		| "hello@endjin.com" | "http://foo.bar/?baz=qux#quux" | false  |
		| "hello@endjin.com" | "www.example.com"              | false  |
		| "hello@endjin.com" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "hello@endjin.com" | "{ \"first\": \"1\" }"         | false  |
		| "hello@endjin.com" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for idnEmail json element backed value as an object
	Given the JsonElement backed JsonIdnEmail <jsonValue>
	When I compare the idnEmail to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue          | value                          | result |
		| "hello@endjin.com" | "Hello"                        | false  |
		| "hello@endjin.com" | "Goodbye"                      | false  |
		| "hello@endjin.com" | 1                              | false  |
		| "hello@endjin.com" | 1.1                            | false  |
		| "hello@endjin.com" | [1,2,3]                        | false  |
		| "hello@endjin.com" | { "first": "1" }               | false  |
		| "hello@endjin.com" | true                           | false  |
		| "hello@endjin.com" | false                          | false  |
		| "hello@endjin.com" | "2018-11-13T20:20:39+00:00"    | false  |
		| "hello@endjin.com" | "P3Y6M4DT12H30M5S"             | false  |
		| "hello@endjin.com" | "2018-11-13"                   | false  |
		| "hello@endjin.com" | "hello@endjin.com"             | true   |
		| "hello@endjin.com" | "www.example.com"              | false  |
		| "hello@endjin.com" | "http://foo.bar/?baz=qux#quux" | false  |
		| "hello@endjin.com" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "hello@endjin.com" | "{ \"first\": \"1\" }"         | false  |
		| "hello@endjin.com" | "192.168.0.1"                  | false  |
		| "hello@endjin.com" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "hello@endjin.com" | <new object()>                 | false  |
		| "hello@endjin.com" | null                           | false  |

Scenario Outline: Equals for idnEmail dotnet backed value as an object
	Given the dotnet backed JsonIdnEmail <jsonValue>
	When I compare the idnEmail to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue          | value                          | result |
		| "hello@endjin.com" | "Hello"                        | false  |
		| "hello@endjin.com" | "Goodbye"                      | false  |
		| "hello@endjin.com" | 1                              | false  |
		| "hello@endjin.com" | 1.1                            | false  |
		| "hello@endjin.com" | [1,2,3]                        | false  |
		| "hello@endjin.com" | { "first": "1" }               | false  |
		| "hello@endjin.com" | true                           | false  |
		| "hello@endjin.com" | false                          | false  |
		| "hello@endjin.com" | "2018-11-13T20:20:39+00:00"    | false  |
		| "hello@endjin.com" | "2018-11-13"                   | false  |
		| "hello@endjin.com" | "P3Y6M4DT12H30M5S"             | false  |
		| "hello@endjin.com" | "hello@endjin.com"             | true   |
		| "hello@endjin.com" | "www.example.com"              | false  |
		| "hello@endjin.com" | "http://foo.bar/?baz=qux#quux" | false  |
		| "hello@endjin.com" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "hello@endjin.com" | "{ \"first\": \"1\" }"         | false  |
		| "hello@endjin.com" | "192.168.0.1"                  | false  |
		| "hello@endjin.com" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "hello@endjin.com" | <new object()>                 | false  |
		| "hello@endjin.com" | null                           | false  |
		| "hello@endjin.com" | <null>                         | false  |
		| "hello@endjin.com" | <undefined>                    | false  |
		| null               | null                           | true   |
		| null               | <null>                         | true   |
		| null               | <undefined>                    | false  |