Feature: JsonRegexEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonRegex
Scenario Outline: Equals for json element backed value as a regex
	Given the JsonElement backed JsonRegex <jsonValue>
	When I compare it to the regex <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue       | value           | result |
		| "([abc])+\\s+$" | "([abc])+\\s+$" | true   |
		| "([abc])+\\s+$" | "([abc]+\\s+$"  | false  |
		| null            | null            | true   |
		| null            | "([abc])+\\s+$" | false  |

Scenario Outline: Equals for dotnet backed value as a regex
	Given the dotnet backed JsonRegex <jsonValue>
	When I compare it to the regex <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue       | value           | result |
		| "([abc])+\\s+$" | "([abc])+\\s+$" | true   |
		| "([abc])+\\s+$" | "([abc]+\\s+$"  | false  |

Scenario Outline: Equals for regex json element backed value as an IJsonValue
	Given the JsonElement backed JsonRegex <jsonValue>
	When I compare the regex to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue       | value                          | result |
		| "([abc])+\\s+$" | "Hello"                        | false  |
		| "([abc])+\\s+$" | "Goodbye"                      | false  |
		| "([abc])+\\s+$" | 1                              | false  |
		| "([abc])+\\s+$" | 1.1                            | false  |
		| "([abc])+\\s+$" | [1,2,3]                        | false  |
		| "([abc])+\\s+$" | { "first": "1" }               | false  |
		| "([abc])+\\s+$" | true                           | false  |
		| "([abc])+\\s+$" | false                          | false  |
		| "([abc])+\\s+$" | "2018-11-13T20:20:39+00:00"    | false  |
		| "([abc])+\\s+$" | "2018-11-13"                   | false  |
		| "([abc])+\\s+$" | "P3Y6M4DT12H30M5S"             | false  |
		| "([abc])+\\s+$" | "2018-11-13"                   | false  |
		| "([abc])+\\s+$" | "hello@endjin.com"             | false  |
		| "([abc])+\\s+$" | "www.example.com"              | false  |
		| "([abc])+\\s+$" | "([abc])+\\s+$"                | true   |
		| "([abc])+\\s+$" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "([abc])+\\s+$" | "{ \"first\": \"1\" }"         | false  |
		| "([abc])+\\s+$" | "192.168.0.1"                  | false  |
		| "([abc])+\\s+$" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for regex dotnet backed value as an IJsonValue
	Given the dotnet backed JsonRegex <jsonValue>
	When I compare the regex to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue       | value                          | result |
		| "([abc])+\\s+$" | "Hello"                        | false  |
		| "([abc])+\\s+$" | "Goodbye"                      | false  |
		| "([abc])+\\s+$" | 1                              | false  |
		| "([abc])+\\s+$" | 1.1                            | false  |
		| "([abc])+\\s+$" | [1,2,3]                        | false  |
		| "([abc])+\\s+$" | { "first": "1" }               | false  |
		| "([abc])+\\s+$" | true                           | false  |
		| "([abc])+\\s+$" | false                          | false  |
		| "([abc])+\\s+$" | "2018-11-13T20:20:39+00:00"    | false  |
		| "([abc])+\\s+$" | "P3Y6M4DT12H30M5S"             | false  |
		| "([abc])+\\s+$" | "2018-11-13"                   | false  |
		| "([abc])+\\s+$" | "P3Y6M4DT12H30M5S"             | false  |
		| "([abc])+\\s+$" | "hello@endjin.com"             | false  |
		| "([abc])+\\s+$" | "www.example.com"              | false  |
		| "([abc])+\\s+$" | "([abc])+\\s+$"                | true   |
		| "([abc])+\\s+$" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "([abc])+\\s+$" | "{ \"first\": \"1\" }"         | false  |
		| "([abc])+\\s+$" | "192.168.0.1"                  | false  |
		| "([abc])+\\s+$" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for regex json element backed value as an object
	Given the JsonElement backed JsonRegex <jsonValue>
	When I compare the regex to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue       | value                          | result |
		| "([abc])+\\s+$" | "Hello"                        | false  |
		| "([abc])+\\s+$" | "Goodbye"                      | false  |
		| "([abc])+\\s+$" | 1                              | false  |
		| "([abc])+\\s+$" | 1.1                            | false  |
		| "([abc])+\\s+$" | [1,2,3]                        | false  |
		| "([abc])+\\s+$" | { "first": "1" }               | false  |
		| "([abc])+\\s+$" | true                           | false  |
		| "([abc])+\\s+$" | false                          | false  |
		| "([abc])+\\s+$" | "2018-11-13T20:20:39+00:00"    | false  |
		| "([abc])+\\s+$" | "P3Y6M4DT12H30M5S"             | false  |
		| "([abc])+\\s+$" | "2018-11-13"                   | false  |
		| "([abc])+\\s+$" | "hello@endjin.com"             | false  |
		| "([abc])+\\s+$" | "www.example.com"              | false  |
		| "([abc])+\\s+$" | "([abc])+\\s+$"                | true   |
		| "([abc])+\\s+$" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "([abc])+\\s+$" | "{ \"first\": \"1\" }"         | false  |
		| "([abc])+\\s+$" | "192.168.0.1"                  | false  |
		| "([abc])+\\s+$" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "([abc])+\\s+$" | <new object()>                 | false  |
		| "([abc])+\\s+$" | null                           | false  |

Scenario Outline: Equals for regex dotnet backed value as an object
	Given the dotnet backed JsonRegex <jsonValue>
	When I compare the regex to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue       | value                          | result |
		| "([abc])+\\s+$" | "Hello"                        | false  |
		| "([abc])+\\s+$" | "Goodbye"                      | false  |
		| "([abc])+\\s+$" | 1                              | false  |
		| "([abc])+\\s+$" | 1.1                            | false  |
		| "([abc])+\\s+$" | [1,2,3]                        | false  |
		| "([abc])+\\s+$" | { "first": "1" }               | false  |
		| "([abc])+\\s+$" | true                           | false  |
		| "([abc])+\\s+$" | false                          | false  |
		| "([abc])+\\s+$" | "2018-11-13T20:20:39+00:00"    | false  |
		| "([abc])+\\s+$" | "2018-11-13"                   | false  |
		| "([abc])+\\s+$" | "P3Y6M4DT12H30M5S"             | false  |
		| "([abc])+\\s+$" | "hello@endjin.com"             | false  |
		| "([abc])+\\s+$" | "www.example.com"              | false  |
		| "([abc])+\\s+$" | "([abc])+\\s+$"                | true   |
		| "([abc])+\\s+$" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "([abc])+\\s+$" | "{ \"first\": \"1\" }"         | false  |
		| "([abc])+\\s+$" | "192.168.0.1"                  | false  |
		| "([abc])+\\s+$" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "([abc])+\\s+$" | <new object()>                 | false  |
		| "([abc])+\\s+$" | null                           | false  |
		| "([abc])+\\s+$" | <null>                         | false  |
		| "([abc])+\\s+$" | <undefined>                    | false  |
		| null            | null                           | true   |
		| null            | <null>                         | true   |
		| null            | <undefined>                    | false  |