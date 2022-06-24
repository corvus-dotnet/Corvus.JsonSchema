Feature: JsonBooleanEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonBoolean
Scenario Outline: Equals for json element backed value as a boolean
	Given the JsonElement backed JsonBoolean <jsonValue>
	When I compare it to the boolean <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value | result |
		| true      | true  | true   |
		| false     | false | true   |
		| true      | false | false  |
		| false     | true  | false  |
		| true      | null  | false  |
		| null      | true  | false  |
		| null      | null  | true   |

Scenario Outline: Equals for dotnet backed value as a boolean
	Given the dotnet backed JsonBoolean <jsonValue>
	When I compare it to the boolean <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value | result |
		| false     | true  | false  |
		| false     | false | true   |
		| true      | true  | true   |
		| true      | false | false  |

Scenario Outline: Equals for boolean json element backed value as an IJsonValue
	Given the JsonElement backed JsonBoolean <jsonValue>
	When I compare the boolean to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value                          | result |
		| false     | "Hello"                        | false  |
		| false     | 1                              | false  |
		| false     | 1.1                            | false  |
		| false     | [1,2,3]                        | false  |
		| false     | { "first": "1" }               | false  |
		| true      | true                           | true   |
		| false     | false                          | true   |
		| true      | false                          | false  |
		| false     | true                           | false  |
		| false     | "2018-11-13T20:20:39+00:00"    | false  |
		| false     | "P3Y6M4DT12H30M5S"             | false  |
		| false     | "2018-11-13"                   | false  |
		| false     | "hello@endjin.com"             | false  |
		| false     | "www.example.com"              | false  |
		| false     | "http://foo.bar/?baz=qux#quux" | false  |
		| false     | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| false     | "{ \"first\": \"1\" }"         | false  |
		| false     | "192.168.0.1"                  | false  |
		| false     | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for boolean dotnet backed value as an IJsonValue
	Given the dotnet backed JsonBoolean <jsonValue>
	When I compare the boolean to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value                          | result |
		| false     | "Hello"                        | false  |
		| false     | 1                              | false  |
		| false     | 1.1                            | false  |
		| false     | [1,2,3]                        | false  |
		| false     | { "first": "1" }               | false  |
		| true      | true                           | true   |
		| false     | false                          | true   |
		| true      | false                          | false  |
		| false     | true                           | false  |
		| false     | "2018-11-13T20:20:39+00:00"    | false  |
		| false     | "P3Y6M4DT12H30M5S"             | false  |
		| false     | "2018-11-13"                   | false  |
		| false     | "hello@endjin.com"             | false  |
		| false     | "www.example.com"              | false  |
		| false     | "http://foo.bar/?baz=qux#quux" | false  |
		| false     | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| false     | "{ \"first\": \"1\" }"         | false  |
		| false     | "192.168.0.1"                  | false  |
		| false     | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for boolean json element backed value as an object
	Given the JsonElement backed JsonBoolean <jsonValue>
	When I compare the boolean to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value                          | result |
		| false     | "Hello"                        | false  |
		| false     | 1                              | false  |
		| false     | 1.1                            | false  |
		| false     | [1,2,3]                        | false  |
		| false     | { "first": "1" }               | false  |
		| true      | true                           | true   |
		| false     | false                          | true   |
		| true      | false                          | false  |
		| false     | true                           | false  |
		| false     | "2018-11-13T20:20:39+00:00"    | false  |
		| false     | "P3Y6M4DT12H30M5S"             | false  |
		| false     | "2018-11-13"                   | false  |
		| false     | "hello@endjin.com"             | false  |
		| false     | "www.example.com"              | false  |
		| false     | "http://foo.bar/?baz=qux#quux" | false  |
		| false     | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| false     | "{ \"first\": \"1\" }"         | false  |
		| false     | "192.168.0.1"                  | false  |
		| false     | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| false     | <new object()>                 | false  |
		| false     | null                           | false  |

Scenario Outline: Equals for boolean dotnet backed value as an object
	Given the dotnet backed JsonBoolean <jsonValue>
	When I compare the boolean to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value                          | result |
		| false     | "Hello"                        | false  |
		| false     | 1                              | false  |
		| false     | 1.1                            | false  |
		| false     | [1,2,3]                        | false  |
		| false     | { "first": "1" }               | false  |
		| true      | true                           | true   |
		| false     | false                          | true   |
		| true      | false                          | false  |
		| false     | true                           | false  |
		| false     | "2018-11-13T20:20:39+00:00"    | false  |
		| false     | "P3Y6M4DT12H30M5S"             | false  |
		| false     | "2018-11-13"                   | false  |
		| false     | "hello@endjin.com"             | false  |
		| false     | "www.example.com"              | false  |
		| false     | "http://foo.bar/?baz=qux#quux" | false  |
		| false     | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| false     | "{ \"first\": \"1\" }"         | false  |
		| false     | "192.168.0.1"                  | false  |
		| false     | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| false     | <new object()>                 | false  |
		| false     | null                           | false  |
		| false     | <null>                         | false  |
		| false     | <undefined>                    | false  |
		| null      | null                           | true   |
		| null      | <null>                         | true   |
		| null      | <undefined>                    | false  |