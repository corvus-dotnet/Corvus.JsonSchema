Feature: JsonArrayEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonArray
Scenario Outline: Equals for json element backed value as an array
	Given the JsonElement backed JsonArray <jsonValue>
	When I compare it to the array <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value     | result |
		| [1,"2",3] | [1,"2",3] | true   |
		| [1,"2"]   | [1,"2",3] | false  |
		| [1,"2",3] | [1,"2"]   | false  |
		| [1,"2",3] | [3,"2",1] | false  |
		| [1,"2",3] | [1,2,3]   | false  |
		| []        | []        | true   |
		| []        | [3,2,1]   | false  |
		| null      | null      | true   |
		| null      | [1,2,3]   | false  |

Scenario Outline: Equals for dotnet backed value as an array
	Given the dotnet backed JsonArray <jsonValue>
	When I compare it to the array <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value     | result |
		| [1,"2",3] | [1,"2",3] | true   |
		| [1,"2"]   | [1,"2",3] | false  |
		| [1,"2",3] | [1,"2"]   | false  |
		| [1,"2",3] | [3,"2",1] | false  |
		| [1,"2",3] | [1,2,3]   | false  |
		| []        | []        | true   |
		| []        | [3,2,1]   | false  |

Scenario Outline: Equals for array json element backed value as an IJsonValue
	Given the JsonElement backed JsonArray <jsonValue>
	When I compare the array to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value                          | result |
		| [1,2,3]   | "Hello"                        | false  |
		| [1,2,3]   | 1                              | false  |
		| [1,2,3]   | 1.1                            | false  |
		| [1,2,3]   | [1,2,3]                        | true   |
		| [1,2,3]   | { "first": "1" }               | false  |
		| [1,2,3]   | true                           | false  |
		| [1,2,3]   | false                          | false  |
		| [1,2,3]   | "2018-11-13T20:20:39+00:00"    | false  |
		| [1,2,3]   | "P3Y6M4DT12H30M5S"             | false  |
		| [1,2,3]   | "2018-11-13"                   | false  |
		| [1,2,3]   | "hello@endjin.com"             | false  |
		| [1,2,3]   | "www.example.com"              | false  |
		| [1,2,3]   | "http://foo.bar/?baz=qux#quux" | false  |
		| [1,2,3]   | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| [1,2,3]   | "{ \"first\": \"1\" }"         | false  |
		| [1,2,3]   | "192.168.0.1"                  | false  |
		| [1,2,3]   | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for array dotnet backed value as an IJsonValue
	Given the dotnet backed JsonArray <jsonValue>
	When I compare the array to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value                          | result |
		| [1,2,3]   | "Hello"                        | false  |
		| [1,2,3]   | 1                              | false  |
		| [1,2,3]   | 1.1                            | false  |
		| [1,2,3]   | [1,2,3]                        | true   |
		| [1,2,3]   | { "first": "1" }               | false  |
		| [1,2,3]   | true                           | false  |
		| [1,2,3]   | false                          | false  |
		| [1,2,3]   | "2018-11-13T20:20:39+00:00"    | false  |
		| [1,2,3]   | "P3Y6M4DT12H30M5S"             | false  |
		| [1,2,3]   | "2018-11-13"                   | false  |
		| [1,2,3]   | "hello@endjin.com"             | false  |
		| [1,2,3]   | "www.example.com"              | false  |
		| [1,2,3]   | "http://foo.bar/?baz=qux#quux" | false  |
		| [1,2,3]   | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| [1,2,3]   | "{ \"first\": \"1\" }"         | false  |
		| [1,2,3]   | "192.168.0.1"                  | false  |
		| [1,2,3]   | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for array json element backed value as an object
	Given the JsonElement backed JsonArray <jsonValue>
	When I compare the array to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value                          | result |
		| [1,2,3]   | "Hello"                        | false  |
		| [1,2,3]   | 1                              | false  |
		| [1,2,3]   | 1.1                            | false  |
		| [1,2,3]   | [1,2,3]                        | true   |
		| [1,2,3]   | { "first": "1" }               | false  |
		| [1,2,3]   | true                           | false  |
		| [1,2,3]   | false                          | false  |
		| [1,2,3]   | "2018-11-13T20:20:39+00:00"    | false  |
		| [1,2,3]   | "P3Y6M4DT12H30M5S"             | false  |
		| [1,2,3]   | "2018-11-13"                   | false  |
		| [1,2,3]   | "hello@endjin.com"             | false  |
		| [1,2,3]   | "www.example.com"              | false  |
		| [1,2,3]   | "http://foo.bar/?baz=qux#quux" | false  |
		| [1,2,3]   | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| [1,2,3]   | "{ \"first\": \"1\" }"         | false  |
		| [1,2,3]   | "192.168.0.1"                  | false  |
		| [1,2,3]   | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for array dotnet backed value as an object
	Given the dotnet backed JsonArray <jsonValue>
	When I compare the array to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue | value                          | result |
		| [1,2,3]   | "Hello"                        | false  |
		| [1,2,3]   | 1                              | false  |
		| [1,2,3]   | 1.1                            | false  |
		| [1,2,3]   | [1,2,3]                        | true   |
		| [1,2,3]   | { "first": "1" }               | false  |
		| [1,2,3]   | true                           | false  |
		| [1,2,3]   | false                          | false  |
		| [1,2,3]   | "2018-11-13T20:20:39+00:00"    | false  |
		| [1,2,3]   | "P3Y6M4DT12H30M5S"             | false  |
		| [1,2,3]   | "2018-11-13"                   | false  |
		| [1,2,3]   | "hello@endjin.com"             | false  |
		| [1,2,3]   | "www.example.com"              | false  |
		| [1,2,3]   | "http://foo.bar/?baz=qux#quux" | false  |
		| [1,2,3]   | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| [1,2,3]   | "{ \"first\": \"1\" }"         | false  |
		| [1,2,3]   | "192.168.0.1"                  | false  |
		| [1,2,3]   | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| [1,2,3]   | <new object()>                 | false  |
		| [1,2,3]   | null                           | false  |
		| [1,2,3]   | <null>                         | false  |
		| [1,2,3]   | <undefined>                    | false  |
		| null      | null                           | true   |
		| null      | <null>                         | true   |
		| null      | <undefined>                    | false  |