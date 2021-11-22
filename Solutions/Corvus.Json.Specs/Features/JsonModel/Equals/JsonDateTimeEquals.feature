Feature: JsonDateTimeEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonDateTime
Scenario Outline: Equals for json element backed value as a dateTime
	Given the JsonElement backed JsonDateTime <jsonValue>
	When I compare it to the dateTime <value>
	Then the result should be <result>

	Examples:
		| jsonValue                   | value                       | result |
		| "2018-11-13T20:20:39+00:00" | "2018-11-13T20:20:39+00:00" | true   |
		| "Garbage"                   | "2018-11-13T20:20:39+00:00" | false  |
		| "2018-11-13T20:20:39+00:00" | "Goodbye"                   | false  |
		| null                        | null                        | true   |
		| null                        | "2018-11-13T20:20:39+00:00" | false  |

Scenario Outline: Equals for dotnet backed value as a dateTime
	Given the dotnet backed JsonDateTime <jsonValue>
	When I compare it to the dateTime <value>
	Then the result should be <result>

	Examples:
		| jsonValue                   | value                       | result |
		| "Garbage"                   | "2018-11-13T20:20:39+00:00" | false  |
		| "2018-11-13T20:20:39+00:00" | "2018-11-13T20:20:39+00:00" | true   |
		| "2018-11-13T20:20:39+00:00" | "Goodbye"                   | false  |

Scenario Outline: Equals for dateTime json element backed value as an IJsonValue
	Given the JsonElement backed JsonDateTime <jsonValue>
	When I compare the dateTime to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue                   | value                          | result |
		| "2018-11-13T20:20:39+00:00" | "Hello"                        | false  |
		| "2018-11-13T20:20:39+00:00" | "Goodbye"                      | false  |
		| "2018-11-13T20:20:39+00:00" | 1                              | false  |
		| "2018-11-13T20:20:39+00:00" | 1.1                            | false  |
		| "2018-11-13T20:20:39+00:00" | [1,2,3]                        | false  |
		| "2018-11-13T20:20:39+00:00" | { "first": "1" }               | false  |
		| "2018-11-13T20:20:39+00:00" | true                           | false  |
		| "2018-11-13T20:20:39+00:00" | false                          | false  |
		| "2018-11-13T20:20:39+00:00" | "2018-11-13T20:20:39+00:00"    | true   |
		| "2018-11-13T20:20:39+00:00" | "P3Y6M4DT12H30M5S"             | false  |
		| "2018-11-13T20:20:39+00:00" | "2018-11-13"                   | false  |
		| "Garbage"                   | "2018-11-13"                   | false  |
		| "2018-11-13T20:20:39+00:00" | "hello@endjin.com"             | false  |
		| "2018-11-13T20:20:39+00:00" | "www.example.com"              | false  |
		| "2018-11-13T20:20:39+00:00" | "http://foo.bar/?baz=qux#quux" | false  |
		| "2018-11-13T20:20:39+00:00" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "2018-11-13T20:20:39+00:00" | "{ \"first\": \"1\" }"         | false  |
		| "2018-11-13T20:20:39+00:00" | "192.168.0.1"                  | false  |
		| "2018-11-13T20:20:39+00:00" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for dateTime dotnet backed value as an IJsonValue
	Given the dotnet backed JsonDateTime <jsonValue>
	When I compare the dateTime to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue                   | value                          | result |
		| "2018-11-13T20:20:39+00:00" | "Hello"                        | false  |
		| "2018-11-13T20:20:39+00:00" | "Goodbye"                      | false  |
		| "2018-11-13T20:20:39+00:00" | 1                              | false  |
		| "2018-11-13T20:20:39+00:00" | 1.1                            | false  |
		| "2018-11-13T20:20:39+00:00" | [1,2,3]                        | false  |
		| "2018-11-13T20:20:39+00:00" | { "first": "1" }               | false  |
		| "2018-11-13T20:20:39+00:00" | true                           | false  |
		| "2018-11-13T20:20:39+00:00" | false                          | false  |
		| "2018-11-13T20:20:39+00:00" | "2018-11-13T20:20:39+00:00"    | true   |
		| "2018-11-13T20:20:39+00:00" | "P3Y6M4DT12H30M5S"             | false  |
		| "2018-11-13T20:20:39+00:00" | "2018-11-13"                   | false  |
		| "Garbage"                   | "2018-11-13"                   | false  |
		| "2018-11-13T20:20:39+00:00" | "hello@endjin.com"             | false  |
		| "2018-11-13T20:20:39+00:00" | "www.example.com"              | false  |
		| "2018-11-13T20:20:39+00:00" | "http://foo.bar/?baz=qux#quux" | false  |
		| "2018-11-13T20:20:39+00:00" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "2018-11-13T20:20:39+00:00" | "{ \"first\": \"1\" }"         | false  |
		| "2018-11-13T20:20:39+00:00" | "192.168.0.1"                  | false  |
		| "2018-11-13T20:20:39+00:00" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for dateTime json element backed value as an object
	Given the JsonElement backed JsonDateTime <jsonValue>
	When I compare the dateTime to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue                   | value                          | result |
		| "2018-11-13T20:20:39+00:00" | "Hello"                        | false  |
		| "2018-11-13T20:20:39+00:00" | "Goodbye"                      | false  |
		| "2018-11-13T20:20:39+00:00" | 1                              | false  |
		| "2018-11-13T20:20:39+00:00" | 1.1                            | false  |
		| "2018-11-13T20:20:39+00:00" | [1,2,3]                        | false  |
		| "2018-11-13T20:20:39+00:00" | { "first": "1" }               | false  |
		| "2018-11-13T20:20:39+00:00" | true                           | false  |
		| "2018-11-13T20:20:39+00:00" | false                          | false  |
		| "2018-11-13T20:20:39+00:00" | "2018-11-13T20:20:39+00:00"    | true   |
		| "2018-11-13T20:20:39+00:00" | "P3Y6M4DT12H30M5S"             | false  |
		| "2018-11-13T20:20:39+00:00" | "2018-11-13"                   | false  |
		| "2018-11-13T20:20:39+00:00" | "hello@endjin.com"             | false  |
		| "2018-11-13T20:20:39+00:00" | "www.example.com"              | false  |
		| "2018-11-13T20:20:39+00:00" | "http://foo.bar/?baz=qux#quux" | false  |
		| "2018-11-13T20:20:39+00:00" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "2018-11-13T20:20:39+00:00" | "{ \"first\": \"1\" }"         | false  |
		| "2018-11-13T20:20:39+00:00" | "192.168.0.1"                  | false  |
		| "2018-11-13T20:20:39+00:00" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "2018-11-13T20:20:39+00:00" | <new object()>                 | false  |
		| "2018-11-13T20:20:39+00:00" | null                           | false  |

Scenario Outline: Equals for dateTime dotnet backed value as an object
	Given the dotnet backed JsonDateTime <jsonValue>
	When I compare the dateTime to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue                   | value                          | result |
		| "2018-11-13T20:20:39+00:00" | "Hello"                        | false  |
		| "2018-11-13T20:20:39+00:00" | "Goodbye"                      | false  |
		| "2018-11-13T20:20:39+00:00" | 1                              | false  |
		| "2018-11-13T20:20:39+00:00" | 1.1                            | false  |
		| "2018-11-13T20:20:39+00:00" | [1,2,3]                        | false  |
		| "2018-11-13T20:20:39+00:00" | { "first": "1" }               | false  |
		| "2018-11-13T20:20:39+00:00" | true                           | false  |
		| "2018-11-13T20:20:39+00:00" | false                          | false  |
		| "2018-11-13T20:20:39+00:00" | "2018-11-13T20:20:39+00:00"    | true   |
		| "2018-11-13T20:20:39+00:00" | "P3Y6M4DT12H30M5S"             | false  |
		| "2018-11-13T20:20:39+00:00" | "2018-11-13"                   | false  |
		| "2018-11-13T20:20:39+00:00" | "hello@endjin.com"             | false  |
		| "2018-11-13T20:20:39+00:00" | "www.example.com"              | false  |
		| "2018-11-13T20:20:39+00:00" | "http://foo.bar/?baz=qux#quux" | false  |
		| "2018-11-13T20:20:39+00:00" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "2018-11-13T20:20:39+00:00" | "{ \"first\": \"1\" }"         | false  |
		| "2018-11-13T20:20:39+00:00" | "192.168.0.1"                  | false  |
		| "2018-11-13T20:20:39+00:00" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "2018-11-13T20:20:39+00:00" | <new object()>                 | false  |
		| "2018-11-13T20:20:39+00:00" | null                           | false  |
		| "2018-11-13T20:20:39+00:00" | <null>                         | false  |
		| "2018-11-13T20:20:39+00:00" | <undefined>                    | false  |
		| null                        | null                           | true   |
		| null                        | <null>                         | true   |
		| null                        | <undefined>                    | false  |