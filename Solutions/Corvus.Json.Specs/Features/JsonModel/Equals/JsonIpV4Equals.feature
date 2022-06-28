Feature: JsonIpV4Equals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonIpV4
Scenario Outline: Equals for json element backed value as a ipV4
	Given the JsonElement backed JsonIpV4 <jsonValue>
	When I compare it to the ipV4 <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue     | value         | result |
		| "192.168.0.1" | "192.168.0.1" | true   |
		| "192.168.0.1" | "192.168.0.2" | false  |
		| null          | null          | true   |
		| null          | "192.168.0.1" | false  |

Scenario Outline: Equals for dotnet backed value as a ipV4
	Given the dotnet backed JsonIpV4 <jsonValue>
	When I compare it to the ipV4 <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue     | value         | result |
		| "192.168.0.1" | "192.168.0.1" | true   |
		| "192.168.0.1" | "192.168.0.2" | false  |

Scenario Outline: Equals for ipV4 json element backed value as an IJsonValue
	Given the JsonElement backed JsonIpV4 <jsonValue>
	When I compare the ipV4 to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue     | value                          | result |
		| "192.168.0.1" | "Hello"                        | false  |
		| "192.168.0.1" | "Goodbye"                      | false  |
		| "192.168.0.1" | 1                              | false  |
		| "192.168.0.1" | 1.1                            | false  |
		| "192.168.0.1" | [1,2,3]                        | false  |
		| "192.168.0.1" | { "first": "1" }               | false  |
		| "192.168.0.1" | true                           | false  |
		| "192.168.0.1" | false                          | false  |
		| "192.168.0.1" | "2018-11-13T20:20:39+00:00"    | false  |
		| "192.168.0.1" | "2018-11-13"                   | false  |
		| "192.168.0.1" | "P3Y6M4DT12H30M5S"             | false  |
		| "192.168.0.1" | "2018-11-13"                   | false  |
		| "192.168.0.1" | "hello@endjin.com"             | false  |
		| "192.168.0.1" | "www.example.com"              | false  |
		| "192.168.0.1" | "http://foo.bar/?baz=qux#quux" | false  |
		| "192.168.0.1" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "192.168.0.1" | "{ \"first\": \"1\" }"         | false  |
		| "192.168.0.1" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "192.168.0.1" | "192.168.0.1"                  | true   |

Scenario Outline: Equals for ipV4 dotnet backed value as an IJsonValue
	Given the dotnet backed JsonIpV4 <jsonValue>
	When I compare the ipV4 to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue     | value                          | result |
		| "192.168.0.1" | "Hello"                        | false  |
		| "192.168.0.1" | "Goodbye"                      | false  |
		| "192.168.0.1" | 1                              | false  |
		| "192.168.0.1" | 1.1                            | false  |
		| "192.168.0.1" | [1,2,3]                        | false  |
		| "192.168.0.1" | { "first": "1" }               | false  |
		| "192.168.0.1" | true                           | false  |
		| "192.168.0.1" | false                          | false  |
		| "192.168.0.1" | "2018-11-13T20:20:39+00:00"    | false  |
		| "192.168.0.1" | "P3Y6M4DT12H30M5S"             | false  |
		| "192.168.0.1" | "2018-11-13"                   | false  |
		| "192.168.0.1" | "P3Y6M4DT12H30M5S"             | false  |
		| "192.168.0.1" | "hello@endjin.com"             | false  |
		| "192.168.0.1" | "www.example.com"              | false  |
		| "192.168.0.1" | "http://foo.bar/?baz=qux#quux" | false  |
		| "192.168.0.1" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "192.168.0.1" | "{ \"first\": \"1\" }"         | false  |
		| "192.168.0.1" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "192.168.0.1" | "192.168.0.1"                  | true   |

Scenario Outline: Equals for ipV4 json element backed value as an object
	Given the JsonElement backed JsonIpV4 <jsonValue>
	When I compare the ipV4 to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue     | value                          | result |
		| "192.168.0.1" | "Hello"                        | false  |
		| "192.168.0.1" | "Goodbye"                      | false  |
		| "192.168.0.1" | 1                              | false  |
		| "192.168.0.1" | 1.1                            | false  |
		| "192.168.0.1" | [1,2,3]                        | false  |
		| "192.168.0.1" | { "first": "1" }               | false  |
		| "192.168.0.1" | true                           | false  |
		| "192.168.0.1" | false                          | false  |
		| "192.168.0.1" | "2018-11-13T20:20:39+00:00"    | false  |
		| "192.168.0.1" | "P3Y6M4DT12H30M5S"             | false  |
		| "192.168.0.1" | "2018-11-13"                   | false  |
		| "192.168.0.1" | "hello@endjin.com"             | false  |
		| "192.168.0.1" | "www.example.com"              | false  |
		| "192.168.0.1" | "http://foo.bar/?baz=qux#quux" | false  |
		| "192.168.0.1" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "192.168.0.1" | "{ \"first\": \"1\" }"         | false  |
		| "192.168.0.1" | <new object()>                 | false  |
		| "192.168.0.1" | "192.168.0.1"                  | true   |
		| "192.168.0.1" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "192.168.0.1" | null                           | false  |

Scenario Outline: Equals for ipV4 dotnet backed value as an object
	Given the dotnet backed JsonIpV4 <jsonValue>
	When I compare the ipV4 to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue     | value                          | result |
		| "192.168.0.1" | "Hello"                        | false  |
		| "192.168.0.1" | "Goodbye"                      | false  |
		| "192.168.0.1" | 1                              | false  |
		| "192.168.0.1" | 1.1                            | false  |
		| "192.168.0.1" | [1,2,3]                        | false  |
		| "192.168.0.1" | { "first": "1" }               | false  |
		| "192.168.0.1" | true                           | false  |
		| "192.168.0.1" | false                          | false  |
		| "192.168.0.1" | "2018-11-13T20:20:39+00:00"    | false  |
		| "192.168.0.1" | "2018-11-13"                   | false  |
		| "192.168.0.1" | "P3Y6M4DT12H30M5S"             | false  |
		| "192.168.0.1" | "hello@endjin.com"             | false  |
		| "192.168.0.1" | "www.example.com"              | false  |
		| "192.168.0.1" | "http://foo.bar/?baz=qux#quux" | false  |
		| "192.168.0.1" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "192.168.0.1" | "{ \"first\": \"1\" }"         | false  |
		| "192.168.0.1" | "192.168.0.1"                  | true   |
		| "192.168.0.1" | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
		| "192.168.0.1" | <new object()>                 | false  |
		| "192.168.0.1" | null                           | false  |
		| "192.168.0.1" | <null>                         | false  |
		| "192.168.0.1" | <undefined>                    | false  |
		| null          | null                           | true   |
		| null          | <null>                         | true   |
		| null          | <undefined>                    | false  |