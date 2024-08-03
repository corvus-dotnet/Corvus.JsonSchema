Feature: JsonNumberEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonNumber
Scenario Outline: Equals for json element backed value as a number
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the <TargetType> <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | true   | JsonNumber  |
	| 1.1       | 1.1   | true   | JsonNumber  |
	| 1.1       | 1     | false  | JsonNumber  |
	| 1.1       | 3     | false  | JsonNumber  |
	| null      | null  | true   | JsonNumber  |
	| null      | 1.1   | false  | JsonNumber  |
	| 1         | 1     | true   | JsonDouble  |
	| 1.1       | 1.1   | true   | JsonDouble  |
	| 1.1       | 1     | false  | JsonDouble  |
	| 1.1       | 3     | false  | JsonDouble  |
	| null      | null  | true   | JsonDouble  |
	| null      | 1.1   | false  | JsonDouble  |
	| 1         | 1     | true   | JsonSingle  |
	| 1.1       | 1.1   | true   | JsonSingle  |
	| 1.1       | 1     | false  | JsonSingle  |
	| 1.1       | 3     | false  | JsonSingle  |
	| null      | null  | true   | JsonSingle  |
	| null      | 1.1   | false  | JsonSingle  |
	| 1         | 1     | true   | JsonDecimal |
	| 1.1       | 1.1   | true   | JsonDecimal |
	| 1.1       | 1     | false  | JsonDecimal |
	| 1.1       | 3     | false  | JsonDecimal |
	| null      | null  | true   | JsonDecimal |
	| null      | 1.1   | false  | JsonDecimal |

Scenario Outline: Equals for dotnet backed value as a number
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the <TargetType> <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | true   | JsonNumber  |
	| 1.1       | 1.1   | true   | JsonNumber  |
	| 1.1       | 1     | false  | JsonNumber  |
	| 1         | 3     | false  | JsonNumber  |
	| 1         | 1     | true   | JsonDouble  |
	| 1.1       | 1.1   | true   | JsonDouble  |
	| 1.1       | 1     | false  | JsonDouble  |
	| 1         | 3     | false  | JsonDouble  |
	| 1         | 1     | true   | JsonSingle  |
	| 1.1       | 1.1   | true   | JsonSingle  |
	| 1.1       | 1     | false  | JsonSingle  |
	| 1         | 3     | false  | JsonSingle  |
	| 1         | 1     | true   | JsonDecimal |
	| 1.1       | 1.1   | true   | JsonDecimal |
	| 1.1       | 1     | false  | JsonDecimal |
	| 1         | 3     | false  | JsonDecimal |

Scenario Outline: Equals for number json element backed value as an IJsonValue
	Given the JsonElement backed JsonNumber <jsonValue>
	When I compare the number to the IJsonValue <value>
	Then the result should be <result>

Examples:
	| jsonValue | value                          | result |
	| 1.1       | "Hello"                        | false  |
	| 1.1       | "Goodbye"                      | false  |
	| 1.1       | 1                              | false  |
	| 1.1       | 1.1                            | true   |
	| 1.1       | [1,2,3]                        | false  |
	| 1.1       | { "first": "1" }               | false  |
	| 1.1       | true                           | false  |
	| 1.1       | false                          | false  |
	| 1.1       | "2018-11-13T20:20:39+00:00"    | false  |
	| 1.1       | "2018-11-13"                   | false  |
	| 1.1       | "P3Y6M4DT12H30M5S"             | false  |
	| 1.1       | "2018-11-13"                   | false  |
	| 1.1       | "hello@endjin.com"             | false  |
	| 1.1       | "www.example.com"              | false  |
	| 1.1       | "http://foo.bar/?baz=qux#quux" | false  |
	| 1.1       | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
	| 1.1       | "{ \\"first\\": \\"1\\" }"     | false  |
	| 1.1       | "192.168.0.1"                  | false  |
	| 1.1       | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for number dotnet backed value as an IJsonValue
	Given the dotnet backed JsonNumber <jsonValue>
	When I compare the number to the IJsonValue <value>
	Then the result should be <result>

Examples:
	| jsonValue | value                          | result |
	| 1.1       | "Hello"                        | false  |
	| 1.1       | "Goodbye"                      | false  |
	| 1.1       | 1                              | false  |
	| 1.1       | 1.1                            | true   |
	| 1.1       | [1,2,3]                        | false  |
	| 1.1       | { "first": "1" }               | false  |
	| 1.1       | true                           | false  |
	| 1.1       | false                          | false  |
	| 1.1       | "2018-11-13T20:20:39+00:00"    | false  |
	| 1.1       | "P3Y6M4DT12H30M5S"             | false  |
	| 1.1       | "2018-11-13"                   | false  |
	| 1.1       | "P3Y6M4DT12H30M5S"             | false  |
	| 1.1       | "hello@endjin.com"             | false  |
	| 1.1       | "www.example.com"              | false  |
	| 1.1       | "http://foo.bar/?baz=qux#quux" | false  |
	| 1.1       | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
	| 1.1       | "{ \\"first\\": \\"1\\" }"     | false  |
	| 1.1       | "192.168.0.1"                  | false  |
	| 1.1       | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for number json element backed value as an object
	Given the JsonElement backed JsonNumber <jsonValue>
	When I compare the number to the object <value>
	Then the result should be <result>

Examples:
	| jsonValue | value                          | result |
	| 1.1       | "Hello"                        | false  |
	| 1.1       | "Goodbye"                      | false  |
	| 1.1       | 1                              | false  |
	| 1.1       | 1.1                            | true   |
	| 1.1       | [1,2,3]                        | false  |
	| 1.1       | { "first": "1" }               | false  |
	| 1.1       | true                           | false  |
	| 1.1       | false                          | false  |
	| 1.1       | "2018-11-13T20:20:39+00:00"    | false  |
	| 1.1       | "P3Y6M4DT12H30M5S"             | false  |
	| 1.1       | "2018-11-13"                   | false  |
	| 1.1       | "hello@endjin.com"             | false  |
	| 1.1       | "www.example.com"              | false  |
	| 1.1       | "http://foo.bar/?baz=qux#quux" | false  |
	| 1.1       | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
	| 1.1       | "{ \\"first\\": \\"1\\" }"     | false  |
	| 1.1       | "192.168.0.1"                  | false  |
	| 1.1       | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
	| 1.1       | <new object()>                 | false  |
	| 1.1       | null                           | false  |

Scenario Outline: Equals for number dotnet backed value as an object
	Given the dotnet backed JsonNumber <jsonValue>
	When I compare the number to the object <value>
	Then the result should be <result>

Examples:
	| jsonValue | value                          | result |
	| 1.1       | "Hello"                        | false  |
	| 1.1       | "Goodbye"                      | false  |
	| 1.1       | 1                              | false  |
	| 1.1       | 1.1                            | true   |
	| 1.1       | [1,2,3]                        | false  |
	| 1.1       | { "first": "1" }               | false  |
	| 1.1       | true                           | false  |
	| 1.1       | false                          | false  |
	| 1.1       | "2018-11-13T20:20:39+00:00"    | false  |
	| 1.1       | "2018-11-13"                   | false  |
	| 1.1       | "P3Y6M4DT12H30M5S"             | false  |
	| 1.1       | "hello@endjin.com"             | false  |
	| 1.1       | "www.example.com"              | false  |
	| 1.1       | "http://foo.bar/?baz=qux#quux" | false  |
	| 1.1       | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
	| 1.1       | "{ \\"first\\": \\"1\\" }"     | false  |
	| 1.1       | "192.168.0.1"                  | false  |
	| 1.1       | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
	| 1.1       | <new object()>                 | false  |
	| 1.1       | null                           | false  |
	| 1.1       | <null>                         | false  |
	| 1.1       | <undefined>                    | false  |
	| null      | null                           | true   |
	| null      | <null>                         | true   |
	| null      | <undefined>                    | false  |