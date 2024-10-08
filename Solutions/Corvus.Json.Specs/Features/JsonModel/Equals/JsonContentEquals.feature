Feature: JsonContentEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonContent
Scenario Outline: Equals for json element backed value as a content
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare it to the <TargetType> <value>
	Then the result should be <result>

Examples:
	| jsonValue                  | value                      | result | TargetType           |
	| "{ \\"first\\": \\"1\\" }" | "{ \\"first\\": \\"1\\" }" | true   | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "Goodbye"                  | false  | JsonContent          |
	| null                       | null                       | true   | JsonContent          |
	| null                       | "{ \\"first\\": \\"1\\" }" | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "{ \\"first\\": \\"1\\" }" | true   | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "Goodbye"                  | false  | JsonContentPre201909 |
	| null                       | null                       | true   | JsonContentPre201909 |
	| null                       | "{ \\"first\\": \\"1\\" }" | false  | JsonContentPre201909 |

Scenario Outline: Equals for dotnet backed value as a content
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare it to the <TargetType> <value>
	Then the result should be <result>

Examples:
	| jsonValue                  | value                      | result | TargetType           |
	| "{ \\"first\\": \\"1\\" }" | "{ \\"first\\": \\"1\\" }" | true   | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "Goodbye"                  | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "{ \\"first\\": \\"1\\" }" | true   | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "Goodbye"                  | false  | JsonContentPre201909 |

Scenario Outline: Equals for content json element backed value as an IJsonValue
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the IJsonValue <value>
	Then the result should be <result>

Examples:
	| jsonValue                  | value                          | result | TargetType           |
	| "{ \\"first\\": \\"1\\" }" | "Hello"                        | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "Goodbye"                      | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | 1                              | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | 1.1                            | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | [1,2,3]                        | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | { "first": "1" }               | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | true                           | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | false                          | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "2018-11-13T20:20:39+00:00"    | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "P3Y6M4DT12H30M5S"             | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "2018-11-13"                   | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "hello@endjin.com"             | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "www.example.com"              | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "http://foo.bar/?baz=qux#quux" | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "{ \\"first\\": \\"1\\" }"     | true   | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "192.168.0.1"                  | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "Hello"                        | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "Goodbye"                      | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | 1                              | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | 1.1                            | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | [1,2,3]                        | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | { "first": "1" }               | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | true                           | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | false                          | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "2018-11-13T20:20:39+00:00"    | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "P3Y6M4DT12H30M5S"             | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "2018-11-13"                   | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "hello@endjin.com"             | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "www.example.com"              | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "http://foo.bar/?baz=qux#quux" | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "{ \\"first\\": \\"1\\" }"     | true   | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "192.168.0.1"                  | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonContentPre201909 |

Scenario Outline: Equals for content dotnet backed value as an IJsonValue
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the IJsonValue <value>
	Then the result should be <result>

Examples:
	| jsonValue                  | value                          | result | TargetType           |
	| "{ \\"first\\": \\"1\\" }" | "Hello"                        | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "Goodbye"                      | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | 1                              | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | 1.1                            | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | [1,2,3]                        | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | { "first": "1" }               | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | true                           | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | false                          | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "2018-11-13T20:20:39+00:00"    | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "P3Y6M4DT12H30M5S"             | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "2018-11-13"                   | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "hello@endjin.com"             | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "www.example.com"              | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "http://foo.bar/?baz=qux#quux" | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "{ \\"first\\": \\"1\\" }"     | true   | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "192.168.0.1"                  | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "Hello"                        | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "Goodbye"                      | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | 1                              | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | 1.1                            | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | [1,2,3]                        | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | { "first": "1" }               | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | true                           | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | false                          | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "2018-11-13T20:20:39+00:00"    | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "P3Y6M4DT12H30M5S"             | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "2018-11-13"                   | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "hello@endjin.com"             | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "www.example.com"              | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "http://foo.bar/?baz=qux#quux" | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "{ \\"first\\": \\"1\\" }"     | true   | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "192.168.0.1"                  | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonContentPre201909 |

Scenario Outline: Equals for content json element backed value as an object
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the object <value>
	Then the result should be <result>

Examples:
	| jsonValue                  | value                          | result | TargetType           |
	| "{ \\"first\\": \\"1\\" }" | "Hello"                        | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "Goodbye"                      | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | 1                              | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | 1.1                            | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | [1,2,3]                        | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | { "first": "1" }               | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | true                           | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | false                          | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "2018-11-13T20:20:39+00:00"    | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "P3Y6M4DT12H30M5S"             | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "2018-11-13"                   | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "hello@endjin.com"             | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "www.example.com"              | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "http://foo.bar/?baz=qux#quux" | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "{ \\"first\\": \\"1\\" }"     | true   | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "192.168.0.1"                  | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | <new object()>                 | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | null                           | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "Hello"                        | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "Goodbye"                      | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | 1                              | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | 1.1                            | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | [1,2,3]                        | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | { "first": "1" }               | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | true                           | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | false                          | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "2018-11-13T20:20:39+00:00"    | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "P3Y6M4DT12H30M5S"             | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "2018-11-13"                   | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "hello@endjin.com"             | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "www.example.com"              | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "http://foo.bar/?baz=qux#quux" | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "{ \\"first\\": \\"1\\" }"     | true   | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "192.168.0.1"                  | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | <new object()>                 | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | null                           | false  | JsonContentPre201909 |

Scenario Outline: Equals for content dotnet backed value as an object
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the object <value>
	Then the result should be <result>

Examples:
	| jsonValue                  | value                          | result | TargetType           |
	| "{ \\"first\\": \\"1\\" }" | "Hello"                        | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "Goodbye"                      | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | 1                              | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | 1.1                            | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | [1,2,3]                        | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | { "first": "1" }               | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | true                           | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | false                          | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "2018-11-13T20:20:39+00:00"    | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "P3Y6M4DT12H30M5S"             | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "2018-11-13"                   | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "hello@endjin.com"             | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "www.example.com"              | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "http://foo.bar/?baz=qux#quux" | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "{ \\"first\\": \\"1\\" }"     | true   | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "192.168.0.1"                  | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | <new object()>                 | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | null                           | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | <null>                         | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | <undefined>                    | false  | JsonContent          |
	| null                       | null                           | true   | JsonContent          |
	| null                       | <null>                         | true   | JsonContent          |
	| null                       | <undefined>                    | false  | JsonContent          |
	| "{ \\"first\\": \\"1\\" }" | "Hello"                        | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "Goodbye"                      | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | 1                              | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | 1.1                            | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | [1,2,3]                        | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | { "first": "1" }               | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | true                           | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | false                          | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "2018-11-13T20:20:39+00:00"    | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "P3Y6M4DT12H30M5S"             | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "2018-11-13"                   | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "hello@endjin.com"             | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "www.example.com"              | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "http://foo.bar/?baz=qux#quux" | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "{ \\"first\\": \\"1\\" }"     | true   | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "192.168.0.1"                  | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | "0:0:0:0:0:ffff:c0a8:0001"     | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | <new object()>                 | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | null                           | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | <null>                         | false  | JsonContentPre201909 |
	| "{ \\"first\\": \\"1\\" }" | <undefined>                    | false  | JsonContentPre201909 |
	| null                       | null                           | true   | JsonContentPre201909 |
	| null                       | <null>                         | true   | JsonContentPre201909 |
	| null                       | <undefined>                    | false  | JsonContentPre201909 |