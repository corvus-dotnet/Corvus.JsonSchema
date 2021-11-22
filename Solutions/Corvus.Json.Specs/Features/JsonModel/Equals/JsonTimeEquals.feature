Feature: JsonTimeEquals
	Valitime the Json Equals operator, equality overrides and hashcode

# JsonTime
Scenario Outline: Equals for json element backed value as a time
	Given the JsonElement backed JsonTime <jsonValue>
	When I compare it to the time <value>
	Then the result should be <result>

	Examples:
		| jsonValue        | value            | result |
		| "08:30:06+00:20" | "08:30:06+00:20" | true   |
		| "Garbage"        | "08:30:06+00:20" | false  |
		| "08:30:06+00:20" | "Goodbye"        | false  |
		| null             | null             | true   |
		| null             | "08:30:06+00:20" | false  |

Scenario Outline: Equals for dotnet backed value as a time
	Given the dotnet backed JsonTime <jsonValue>
	When I compare it to the time <value>
	Then the result should be <result>

	Examples:
		| jsonValue        | value            | result |
		| "Garbage"        | "08:30:06+00:20" | false  |
		| "08:30:06+00:20" | "08:30:06+00:20" | true   |
		| "08:30:06+00:20" | "Goodbye"        | false  |

Scenario Outline: Equals for time json element backed value as an IJsonValue
	Given the JsonElement backed JsonTime <jsonValue>
	When I compare the time to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue        | value                           | result |
		| "08:30:06+00:20" | "Hello"                         | false  |
		| "08:30:06+00:20" | "Goodbye"                       | false  |
		| "08:30:06+00:20" | 1                               | false  |
		| "08:30:06+00:20" | 1.1                             | false  |
		| "08:30:06+00:20" | [1,2,3]                         | false  |
		| "08:30:06+00:20" | { "first": "1" }                | false  |
		| "08:30:06+00:20" | true                            | false  |
		| "08:30:06+00:20" | false                           | false  |
		| "08:30:06+00:20" | "08:30:06+00:20T20:20:39+00:00" | false  |
		| "08:30:06+00:20" | "P3Y6M4DT12H30M5S"              | false  |
		| "08:30:06+00:20" | "08:30:06+00:20"                | true   |
		| "Garbage"        | "08:30:06+00:20"                | false  |
		| "08:30:06+00:20" | "hello@endjin.com"              | false  |
		| "08:30:06+00:20" | "www.example.com"               | false  |
		| "08:30:06+00:20" | "http://foo.bar/?baz=qux#quux"  | false  |
		| "08:30:06+00:20" | "eyAiaGVsbG8iOiAid29ybGQiIH0="  | false  |
		| "08:30:06+00:20" | "{ \"first\": \"1\" }"          | false  |
		| "08:30:06+00:20" | "192.168.0.1"                   | false  |
		| "08:30:06+00:20" | "0:0:0:0:0:ffff:c0a8:0001"      | false  |

Scenario Outline: Equals for time dotnet backed value as an IJsonValue
	Given the dotnet backed JsonTime <jsonValue>
	When I compare the time to the IJsonValue <value>
	Then the result should be <result>

	Examples:
		| jsonValue        | value                           | result |
		| "08:30:06+00:20" | "Hello"                         | false  |
		| "08:30:06+00:20" | "Goodbye"                       | false  |
		| "08:30:06+00:20" | 1                               | false  |
		| "08:30:06+00:20" | 1.1                             | false  |
		| "08:30:06+00:20" | [1,2,3]                         | false  |
		| "08:30:06+00:20" | { "first": "1" }                | false  |
		| "08:30:06+00:20" | true                            | false  |
		| "08:30:06+00:20" | false                           | false  |
		| "08:30:06+00:20" | "08:30:06+00:20T20:20:39+00:00" | false  |
		| "08:30:06+00:20" | "P3Y6M4DT12H30M5S"              | false  |
		| "08:30:06+00:20" | "08:30:06+00:20"                | true   |
		| "Garbage"        | "08:30:06+00:20T20:20:39+00:00" | false  |
		| "08:30:06+00:20" | "hello@endjin.com"              | false  |
		| "08:30:06+00:20" | "www.example.com"               | false  |
		| "08:30:06+00:20" | "http://foo.bar/?baz=qux#quux"  | false  |
		| "08:30:06+00:20" | "eyAiaGVsbG8iOiAid29ybGQiIH0="  | false  |
		| "08:30:06+00:20" | "{ \"first\": \"1\" }"          | false  |
		| "08:30:06+00:20" | "192.168.0.1"                   | false  |
		| "08:30:06+00:20" | "0:0:0:0:0:ffff:c0a8:0001"      | false  |

Scenario Outline: Equals for time json element backed value as an object
	Given the JsonElement backed JsonTime <jsonValue>
	When I compare the time to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue        | value                           | result |
		| "08:30:06+00:20" | "Hello"                         | false  |
		| "08:30:06+00:20" | "Goodbye"                       | false  |
		| "08:30:06+00:20" | 1                               | false  |
		| "08:30:06+00:20" | 1.1                             | false  |
		| "08:30:06+00:20" | [1,2,3]                         | false  |
		| "08:30:06+00:20" | { "first": "1" }                | false  |
		| "08:30:06+00:20" | true                            | false  |
		| "08:30:06+00:20" | false                           | false  |
		| "08:30:06+00:20" | "08:30:06+00:20T20:20:39+00:00" | false  |
		| "08:30:06+00:20" | "P3Y6M4DT12H30M5S"              | false  |
		| "08:30:06+00:20" | "08:30:06+00:20"                | true   |
		| "08:30:06+00:20" | "hello@endjin.com"              | false  |
		| "08:30:06+00:20" | "www.example.com"               | false  |
		| "08:30:06+00:20" | "http://foo.bar/?baz=qux#quux"  | false  |
		| "08:30:06+00:20" | "eyAiaGVsbG8iOiAid29ybGQiIH0="  | false  |
		| "08:30:06+00:20" | "{ \"first\": \"1\" }"          | false  |
		| "08:30:06+00:20" | "192.168.0.1"                   | false  |
		| "08:30:06+00:20" | "0:0:0:0:0:ffff:c0a8:0001"      | false  |
		| "08:30:06+00:20" | <new object()>                  | false  |
		| "08:30:06+00:20" | null                            | false  |

Scenario Outline: Equals for time dotnet backed value as an object
	Given the dotnet backed JsonTime <jsonValue>
	When I compare the time to the object <value>
	Then the result should be <result>

	Examples:
		| jsonValue        | value                           | result |
		| "08:30:06+00:20" | "Hello"                         | false  |
		| "08:30:06+00:20" | "Goodbye"                       | false  |
		| "08:30:06+00:20" | 1                               | false  |
		| "08:30:06+00:20" | 1.1                             | false  |
		| "08:30:06+00:20" | [1,2,3]                         | false  |
		| "08:30:06+00:20" | { "first": "1" }                | false  |
		| "08:30:06+00:20" | true                            | false  |
		| "08:30:06+00:20" | false                           | false  |
		| "08:30:06+00:20" | "08:30:06+00:20T20:20:39+00:00" | false  |
		| "08:30:06+00:20" | "P3Y6M4DT12H30M5S"              | false  |
		| "08:30:06+00:20" | "08:30:06+00:20"                | true   |
		| "08:30:06+00:20" | "hello@endjin.com"              | false  |
		| "08:30:06+00:20" | "www.example.com"               | false  |
		| "08:30:06+00:20" | "http://foo.bar/?baz=qux#quux"  | false  |
		| "08:30:06+00:20" | "eyAiaGVsbG8iOiAid29ybGQiIH0="  | false  |
		| "08:30:06+00:20" | "{ \"first\": \"1\" }"          | false  |
		| "08:30:06+00:20" | "192.168.0.1"                   | false  |
		| "08:30:06+00:20" | "0:0:0:0:0:ffff:c0a8:0001"      | false  |
		| "08:30:06+00:20" | <new object()>                  | false  |
		| "08:30:06+00:20" | null                            | false  |
		| "08:30:06+00:20" | <null>                          | false  |
		| "08:30:06+00:20" | <undefined>                     | false  |
		| null             | null                            | true   |
		| null             | <null>                          | true   |
		| null             | <undefined>                     | false  |