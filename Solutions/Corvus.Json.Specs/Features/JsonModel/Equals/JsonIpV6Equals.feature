Feature: JsonIpV6Equals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonIpV6
Scenario Outline: Equals for json element backed value as a ipV6
	Given the JsonElement backed JsonIpV6 <jsonValue>
	When I compare it to the ipV6 <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue                  | value                      | result |
		| "0:0:0:0:0:ffff:c0a8:0001" | "0:0:0:0:0:ffff:c0a8:0001" | true   |
		| "0:0:0:0:0:ffff:c0a8:0001" | "0:0:0:0:0:ffff:c0a8:0002" | false  |
		| null                       | null                       | true   |
		| null                       | "0:0:0:0:0:ffff:c0a8:0001" | false  |

Scenario Outline: Equals for dotnet backed value as a ipV6
	Given the dotnet backed JsonIpV6 <jsonValue>
	When I compare it to the ipV6 <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue                  | value                      | result |
		| "0:0:0:0:0:ffff:c0a8:0001" | "0:0:0:0:0:ffff:c0a8:0001" | true   |
		| "0:0:0:0:0:ffff:c0a8:0001" | "0:0:0:0:0:ffff:c0a8:0002" | false  |

Scenario Outline: Equals for ipV6 json element backed value as an IJsonValue
	Given the JsonElement backed JsonIpV6 <jsonValue>
	When I compare the ipV6 to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue                  | value                          | result |
		| "0:0:0:0:0:ffff:c0a8:0001" | "Hello"                        | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "Goodbye"                      | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | 1                              | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | 1.1                            | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | [1,2,3]                        | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | { "first": "1" }               | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | true                           | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | false                          | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "2018-11-13T20:20:39+00:00"    | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "2018-11-13"                   | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "P3Y6M4DT12H30M5S"             | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "2018-11-13"                   | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "hello@endjin.com"             | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "www.example.com"              | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "http://foo.bar/?baz=qux#quux" | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "{ \"first\": \"1\" }"         | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "192.168.0.1"                  | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "0:0:0:0:0:ffff:c0a8:0001"     | true   |

Scenario Outline: Equals for ipV6 dotnet backed value as an IJsonValue
	Given the dotnet backed JsonIpV6 <jsonValue>
	When I compare the ipV6 to the IJsonValue <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue                  | value                          | result |
		| "0:0:0:0:0:ffff:c0a8:0001" | "Hello"                        | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "Goodbye"                      | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | 1                              | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | 1.1                            | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | [1,2,3]                        | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | { "first": "1" }               | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | true                           | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | false                          | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "2018-11-13T20:20:39+00:00"    | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "P3Y6M4DT12H30M5S"             | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "2018-11-13"                   | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "P3Y6M4DT12H30M5S"             | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "hello@endjin.com"             | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "www.example.com"              | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "http://foo.bar/?baz=qux#quux" | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "{ \"first\": \"1\" }"         | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "192.168.0.1"                  | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "0:0:0:0:0:ffff:c0a8:0001"     | true   |

Scenario Outline: Equals for ipV6 json element backed value as an object
	Given the JsonElement backed JsonIpV6 <jsonValue>
	When I compare the ipV6 to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue                  | value                          | result |
		| "0:0:0:0:0:ffff:c0a8:0001" | "Hello"                        | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "Goodbye"                      | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | 1                              | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | 1.1                            | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | [1,2,3]                        | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | { "first": "1" }               | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | true                           | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | false                          | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "2018-11-13T20:20:39+00:00"    | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "P3Y6M4DT12H30M5S"             | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "2018-11-13"                   | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "hello@endjin.com"             | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "www.example.com"              | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "http://foo.bar/?baz=qux#quux" | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "{ \"first\": \"1\" }"         | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "192.168.0.1"                  | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "0:0:0:0:0:ffff:c0a8:0001"     | true   |
		| "0:0:0:0:0:ffff:c0a8:0001" | <new object()>                 | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | null                           | false  |

Scenario Outline: Equals for ipV6 dotnet backed value as an object
	Given the dotnet backed JsonIpV6 <jsonValue>
	When I compare the ipV6 to the object <value>
	Then the result should be exactly <result>

	Examples:
		| jsonValue                  | value                          | result |
		| "0:0:0:0:0:ffff:c0a8:0001" | "Hello"                        | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "Goodbye"                      | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | 1                              | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | 1.1                            | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | [1,2,3]                        | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | { "first": "1" }               | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | true                           | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | false                          | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "2018-11-13T20:20:39+00:00"    | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "2018-11-13"                   | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "P3Y6M4DT12H30M5S"             | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "hello@endjin.com"             | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "www.example.com"              | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "http://foo.bar/?baz=qux#quux" | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "{ \"first\": \"1\" }"         | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "192.168.0.1"                  | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | "0:0:0:0:0:ffff:c0a8:0001"     | true   |
		| "0:0:0:0:0:ffff:c0a8:0001" | <new object()>                 | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | null                           | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | <null>                         | false  |
		| "0:0:0:0:0:ffff:c0a8:0001" | <undefined>                    | false  |
		| null                       | null                           | true   |
		| null                       | <null>                         | true   |
		| null                       | <undefined>                    | false  |
