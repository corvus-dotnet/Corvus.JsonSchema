Feature: JsonBase64ContentEquals
	Validate the Json Equals operator, equality overrides and hashcode

# <TargetType>
Scenario Outline: Equals for json element backed value as a base64content
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare it to the <TargetType> <value>
	Then the result should be <result>

Examples:
	| jsonValue                      | value                          | result | TargetType                 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64Content          |
	| null                           | null                           | true   | JsonBase64Content          |
	| null                           | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64ContentPre201909 |
	| null                           | null                           | true   | JsonBase64ContentPre201909 |
	| null                           | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonBase64ContentPre201909 |

Scenario Outline: Equals for dotnet backed value as a base64content
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare it to the <TargetType> <value>
	Then the result should be <result>

Examples:
	| jsonValue                      | value                          | result | TargetType                 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64ContentPre201909 |

Scenario Outline: Equals for base64content json element backed value as an IJsonValue
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the IJsonValue <value>
	Then the result should be <result>

Examples:
	| jsonValue                      | value                          | result | TargetType                 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \\"first\\": \\"1\\" }"     | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \\"first\\": \\"1\\" }"     | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonBase64ContentPre201909 |

Scenario Outline: Equals for base64content dotnet backed value as an IJsonValue
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the IJsonValue <value>
	Then the result should be <result>

Examples:
	| jsonValue                      | value                          | result | TargetType                 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \\"first\\": \\"1\\" }"     | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \\"first\\": \\"1\\" }"     | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonBase64ContentPre201909 |

Scenario Outline: Equals for base64content json element backed value as an object
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the object <value>
	Then the result should be <result>

Examples:
	| jsonValue                      | value                          | result | TargetType                 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \\"first\\": \\"1\\" }"     | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <new object()>                 | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | null                           | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \\"first\\": \\"1\\" }"     | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <new object()>                 | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | null                           | false  | JsonBase64ContentPre201909 |

Scenario Outline: Equals for base64content dotnet backed value as an object
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the object <value>
	Then the result should be <result>

Examples:
	| jsonValue                      | value                          | result | TargetType                 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \\"first\\": \\"1\\" }"     | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <new object()>                 | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | null                           | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <null>                         | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <undefined>                    | false  | JsonBase64Content          |
	| null                           | null                           | true   | JsonBase64Content          |
	| null                           | <null>                         | true   | JsonBase64Content          |
	| null                           | <undefined>                    | false  | JsonBase64Content          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \\"first\\": \\"1\\" }"     | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <new object()>                 | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | null                           | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <null>                         | false  | JsonBase64ContentPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <undefined>                    | false  | JsonBase64ContentPre201909 |
	| null                           | null                           | true   | JsonBase64ContentPre201909 |
	| null                           | <null>                         | true   | JsonBase64ContentPre201909 |
	| null                           | <undefined>                    | false  | JsonBase64ContentPre201909 |