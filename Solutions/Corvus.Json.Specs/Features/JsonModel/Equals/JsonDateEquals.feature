Feature: JsonDateEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonDate
Scenario Outline: Equals for json element backed value as a date
	Given the JsonElement backed JsonDate <jsonValue>
	When I compare it to the date <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue    | value        | result |
		| "2018-11-13" | "2018-11-13" | true   |
		| "Garbage"    | "2018-11-13" | false  |
		| "2018-11-13" | "Goodbye"    | false  |
		| null         | null         | true   |
		| null         | "2018-11-13" | false  |

Scenario Outline: Equals for dotnet backed value as a date
	Given the dotnet backed JsonDate <jsonValue>
	When I compare it to the date <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue    | value        | result |
		| "Garbage"    | "2018-11-13" | false  |
		| "2018-11-13" | "2018-11-13" | true   |
		| "2018-11-13" | "Goodbye"    | false  |

Scenario Outline: Equals for date json element backed value as an IJsonValue
	Given the JsonElement backed JsonDate <jsonValue>
	When I compare the date to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue    | value                          | result |
		| "2018-11-13" | "Hello"                        | false  |
		| "2018-11-13" | "Goodbye"                      | false  |
		| "2018-11-13" | 1                              | false  |
		| "2018-11-13" | 1.1                            | false  |
		| "2018-11-13" | [1,2,3]                        | false  |
		| "2018-11-13" | { "first": "1" }               | false  |
		| "2018-11-13" | true                           | false  |
		| "2018-11-13" | false                          | false  |
		| "2018-11-13" | "2018-11-13T20:20:39+00:00"    | false  |
		| "2018-11-13" | "P3Y6M4DT12H30M5S"             | false  |
		| "2018-11-13" | "2018-11-13"                   | true   |
		| "Garbage"    | "2018-11-13"                   | false  |
		| "2018-11-13" | "hello@endjin.com"             | false  |
		| "2018-11-13" | "www.example.com"              | false  |
		| "2018-11-13" | "http://foo.bar/?baz=qux#quux" | false  |
		| "2018-11-13" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "2018-11-13" | "{ \"first\": \"1\" }"         | false  |
		| "2018-11-13" | "192.168.0.1"                  | false  |
		| "2018-11-13" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for date dotnet backed value as an IJsonValue
	Given the dotnet backed JsonDate <jsonValue>
	When I compare the date to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue    | value                          | result |
		| "2018-11-13" | "Hello"                        | false  |
		| "2018-11-13" | "Goodbye"                      | false  |
		| "2018-11-13" | 1                              | false  |
		| "2018-11-13" | 1.1                            | false  |
		| "2018-11-13" | [1,2,3]                        | false  |
		| "2018-11-13" | { "first": "1" }               | false  |
		| "2018-11-13" | true                           | false  |
		| "2018-11-13" | false                          | false  |
		| "2018-11-13" | "2018-11-13T20:20:39+00:00"    | false  |
		| "2018-11-13" | "P3Y6M4DT12H30M5S"             | false  |
		| "2018-11-13" | "2018-11-13"                   | true   |
		| "Garbage"    | "2018-11-13T20:20:39+00:00"    | false  |
		| "2018-11-13" | "hello@endjin.com"             | false  |
		| "2018-11-13" | "www.example.com"              | false  |
		| "2018-11-13" | "http://foo.bar/?baz=qux#quux" | false  |
		| "2018-11-13" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "2018-11-13" | "{ \"first\": \"1\" }"         | false  |
		| "2018-11-13" | "192.168.0.1"                  | false  |
		| "2018-11-13" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for date json element backed value as an object
	Given the JsonElement backed JsonDate <jsonValue>
	When I compare the date to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue    | value                          | result |
		| "2018-11-13" | "Hello"                        | false  |
		| "2018-11-13" | "Goodbye"                      | false  |
		| "2018-11-13" | 1                              | false  |
		| "2018-11-13" | 1.1                            | false  |
		| "2018-11-13" | [1,2,3]                        | false  |
		| "2018-11-13" | { "first": "1" }               | false  |
		| "2018-11-13" | true                           | false  |
		| "2018-11-13" | false                          | false  |
		| "2018-11-13" | "2018-11-13T20:20:39+00:00"    | false  |
		| "2018-11-13" | "P3Y6M4DT12H30M5S"             | false  |
		| "2018-11-13" | "2018-11-13"                   | true   |
		| "2018-11-13" | "hello@endjin.com"             | false  |
		| "2018-11-13" | "www.example.com"              | false  |
		| "2018-11-13" | "http://foo.bar/?baz=qux#quux" | false  |
		| "2018-11-13" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "2018-11-13" | "{ \"first\": \"1\" }"         | false  |
		| "2018-11-13" | "192.168.0.1"                  | false  |
		| "2018-11-13" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "2018-11-13" | <new object()>                 | false  |
		| "2018-11-13" | null                           | false  |

Scenario Outline: Equals for date dotnet backed value as an object
	Given the dotnet backed JsonDate <jsonValue>
	When I compare the date to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue    | value                          | result |
		| "2018-11-13" | "Hello"                        | false  |
		| "2018-11-13" | "Goodbye"                      | false  |
		| "2018-11-13" | 1                              | false  |
		| "2018-11-13" | 1.1                            | false  |
		| "2018-11-13" | [1,2,3]                        | false  |
		| "2018-11-13" | { "first": "1" }               | false  |
		| "2018-11-13" | true                           | false  |
		| "2018-11-13" | false                          | false  |
		| "2018-11-13" | "2018-11-13T20:20:39+00:00"    | false  |
		| "2018-11-13" | "P3Y6M4DT12H30M5S"             | false  |
		| "2018-11-13" | "2018-11-13"                   | true   |
		| "2018-11-13" | "hello@endjin.com"             | false  |
		| "2018-11-13" | "www.example.com"              | false  |
		| "2018-11-13" | "http://foo.bar/?baz=qux#quux" | false  |
		| "2018-11-13" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "2018-11-13" | "{ \"first\": \"1\" }"         | false  |
		| "2018-11-13" | "192.168.0.1"                  | false  |
		| "2018-11-13" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "2018-11-13" | <new object()>                 | false  |
		| "2018-11-13" | null                           | false  |
		| "2018-11-13" | <null>                         | false  |
		| "2018-11-13" | <undefined>                    | false  |
		| null         | null                           | true   |
		| null         | <null>                         | true   |
		| null         | <undefined>                    | false  |
