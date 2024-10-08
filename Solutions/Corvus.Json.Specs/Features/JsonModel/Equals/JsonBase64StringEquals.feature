Feature: JsonBase64StringEquals
	Validate the Json Equals operator, equality overrides and hashcode

# <TargetType>
Scenario Outline: Equals for json element backed value as a base64string
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare it to the <TargetType> <value>
	Then the result should be <result>

Examples:
	| jsonValue                      | value                          | result | TargetType                |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64String          |
	| null                           | null                           | true   | JsonBase64String          |
	| null                           | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64StringPre201909 |
	| null                           | null                           | true   | JsonBase64StringPre201909 |
	| null                           | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonBase64StringPre201909 |

Scenario Outline: Equals for dotnet backed value as a base64string
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare it to the <TargetType> <value>
	Then the result should be <result>

Examples:
	| jsonValue                      | value                          | result | TargetType                |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64StringPre201909 |

Scenario Outline: Equals for base64string json element backed value as an IJsonValue
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the IJsonValue <value>
	Then the result should be <result>

Examples:
	| jsonValue                      | value                          | result | TargetType                |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \\"first\\": \\"1\\" }"     | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \\"first\\": \\"1\\" }"     | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonBase64StringPre201909 |

Scenario Outline: Equals for base64string dotnet backed value as an IJsonValue
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the IJsonValue <value>
	Then the result should be <result>

Examples:
	| jsonValue                      | value                          | result | TargetType                |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \\"first\\": \\"1\\" }"     | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \\"first\\": \\"1\\" }"     | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonBase64StringPre201909 |

Scenario Outline: Equals for base64string json element backed value as an object
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the object <value>
	Then the result should be <result>

Examples:
	| jsonValue                      | value                          | result | TargetType                |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \\"first\\": \\"1\\" }"     | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <new object()>                 | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | null                           | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \\"first\\": \\"1\\" }"     | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <new object()>                 | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | null                           | false  | JsonBase64StringPre201909 |

Scenario Outline: Equals for base64string dotnet backed value as an object
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the object <value>
	Then the result should be <result>

Examples:
	| jsonValue                      | value                          | result | TargetType                |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \\"first\\": \\"1\\" }"     | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <new object()>                 | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | null                           | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <null>                         | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <undefined>                    | false  | JsonBase64String          |
	| null                           | null                           | true   | JsonBase64String          |
	| null                           | <null>                         | true   | JsonBase64String          |
	| null                           | <undefined>                    | false  | JsonBase64String          |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Hello"                        | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "Goodbye"                      | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1                              | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | 1.1                            | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | [1,2,3]                        | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | { "first": "1" }               | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true                           | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false                          | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13T20:20:39+00:00"    | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "P3Y6M4DT12H30M5S"             | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "2018-11-13"                   | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "hello@endjin.com"             | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "www.example.com"              | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "http://foo.bar/?baz=qux#quux" | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | true   | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "{ \\"first\\": \\"1\\" }"     | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "192.168.0.1"                  | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <new object()>                 | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | null                           | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <null>                         | false  | JsonBase64StringPre201909 |
	| "eyAiaGVsbG8iOiAid29ybGQiIH0=" | <undefined>                    | false  | JsonBase64StringPre201909 |
	| null                           | null                           | true   | JsonBase64StringPre201909 |
	| null                           | <null>                         | true   | JsonBase64StringPre201909 |
	| null                           | <undefined>                    | false  | JsonBase64StringPre201909 |