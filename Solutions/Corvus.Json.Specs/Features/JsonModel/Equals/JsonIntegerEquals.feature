Feature: JsonIntegerEquals
	Validate the Json Equals operator, equality overrides and hashcode

# JsonInteger
Scenario Outline: Equals for json element backed value as a integer
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the integer <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | true   | JsonInteger |
	| 1         | 3     | false  | JsonInteger |
	| null      | null  | true   | JsonInteger |
	| null      | 1     | false  | JsonInteger |
	| 1         | 1     | true   | JsonInt64   |
	| 1         | 3     | false  | JsonInt64   |
	| null      | null  | true   | JsonInt64   |
	| null      | 1     | false  | JsonInt64   |
	| 1         | 1     | true   | JsonInt32   |
	| 1         | 3     | false  | JsonInt32   |
	| null      | null  | true   | JsonInt32   |
	| null      | 1     | false  | JsonInt32   |
	| 1         | 1     | true   | JsonInt16   |
	| 1         | 3     | false  | JsonInt16   |
	| null      | null  | true   | JsonInt16   |
	| null      | 1     | false  | JsonInt16   |
	| 1         | 1     | true   | JsonSByte   |
	| 1         | 3     | false  | JsonSByte   |
	| null      | null  | true   | JsonSByte   |
	| null      | 1     | false  | JsonSByte   |
	| 1         | 1     | true   | JsonUInt64  |
	| 1         | 3     | false  | JsonUInt64  |
	| null      | null  | true   | JsonUInt64  |
	| null      | 1     | false  | JsonUInt64  |
	| 1         | 1     | true   | JsonUInt32  |
	| 1         | 3     | false  | JsonUInt32  |
	| null      | null  | true   | JsonUInt32  |
	| null      | 1     | false  | JsonUInt32  |
	| 1         | 1     | true   | JsonUInt16  |
	| 1         | 3     | false  | JsonUInt16  |
	| null      | null  | true   | JsonUInt16  |
	| null      | 1     | false  | JsonUInt16  |
	| 1         | 1     | true   | JsonByte    |
	| 1         | 3     | false  | JsonByte    |
	| null      | null  | true   | JsonByte    |
	| null      | 1     | false  | JsonByte    |

Scenario Outline: Equals for dotnet backed value as a integer
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the integer <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | true   | JsonInteger |
	| 1         | 3     | false  | JsonInteger |
	| 1         | 1     | true   | JsonInt64   |
	| 1         | 3     | false  | JsonInt64   |
	| 1         | 1     | true   | JsonInt32   |
	| 1         | 3     | false  | JsonInt32   |
	| 1         | 1     | true   | JsonInt16   |
	| 1         | 3     | false  | JsonInt16   |
	| 1         | 1     | true   | JsonSByte   |
	| 1         | 3     | false  | JsonSByte   |
	| 1         | 1     | true   | JsonUInt64  |
	| 1         | 3     | false  | JsonUInt64  |
	| 1         | 1     | true   | JsonUInt32  |
	| 1         | 3     | false  | JsonUInt32  |
	| 1         | 1     | true   | JsonUInt16  |
	| 1         | 3     | false  | JsonUInt16  |
	| 1         | 1     | true   | JsonByte    |
	| 1         | 3     | false  | JsonByte    |

Scenario Outline: Equals for integer json element backed value as an IJsonValue
	Given the JsonElement backed JsonInteger <jsonValue>
	When I compare the JsonInteger to the IJsonValue <value>
	Then the result should be <result>

Examples:
	| jsonValue | value                          | result |
	| 1         | "Hello"                        | false  |
	| 1         | "Goodbye"                      | false  |
	| 1         | 1                              | true   |
	| 1         | 1.0                            | true   |
	| 1         | 1.1                            | false  |
	| 1         | [1,2,3]                        | false  |
	| 1         | { "first": "1" }               | false  |
	| 1         | true                           | false  |
	| 1         | false                          | false  |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  |
	| 1         | "2018-11-13"                   | false  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  |
	| 1         | "2018-11-13"                   | false  |
	| 1         | "hello@endjin.com"             | false  |
	| 1         | "www.example.com"              | false  |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  |
	| 1         | "192.168.0.1"                  | false  |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for integer dotnet backed value as an IJsonValue
	Given the dotnet backed JsonInteger <jsonValue>
	When I compare the JsonInteger to the IJsonValue <value>
	Then the result should be <result>

Examples:
	| jsonValue | value                          | result |
	| 1         | "Hello"                        | false  |
	| 1         | "Goodbye"                      | false  |
	| 1         | 1                              | true   |
	| 1         | 1.0                            | true   |
	| 1         | 1.1                            | false  |
	| 1         | [1,2,3]                        | false  |
	| 1         | { "first": "1" }               | false  |
	| 1         | true                           | false  |
	| 1         | false                          | false  |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  |
	| 1         | "2018-11-13"                   | false  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  |
	| 1         | "hello@endjin.com"             | false  |
	| 1         | "www.example.com"              | false  |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  |
	| 1         | "192.168.0.1"                  | false  |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  |

Scenario Outline: Equals for integer json element backed value as an object
	Given the JsonElement backed JsonInteger <jsonValue>
	When I compare the JsonInteger to the object <value>
	Then the result should be <result>

Examples:
	| jsonValue | value                          | result |
	| 1         | "Hello"                        | false  |
	| 1         | "Goodbye"                      | false  |
	| 1         | 1                              | true   |
	| 1         | 1.0                            | true   |
	| 1         | 1.1                            | false  |
	| 1         | [1,2,3]                        | false  |
	| 1         | { "first": "1" }               | false  |
	| 1         | true                           | false  |
	| 1         | false                          | false  |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  |
	| 1         | "2018-11-13"                   | false  |
	| 1         | "hello@endjin.com"             | false  |
	| 1         | "www.example.com"              | false  |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  |
	| 1         | "192.168.0.1"                  | false  |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
	| 1         | <new object()>                 | false  |
	| 1         | null                           | false  |

Scenario Outline: Equals for integer dotnet backed value as an object
	Given the dotnet backed JsonInteger <jsonValue>
	When I compare the JsonInteger to the object <value>
	Then the result should be <result>

Examples:
	| jsonValue | value                          | result |
	| 1         | "Hello"                        | false  |
	| 1         | "Goodbye"                      | false  |
	| 1         | 1                              | true   |
	| 1         | 1.0                            | true   |
	| 1         | 1.1                            | false  |
	| 1         | [1,2,3]                        | false  |
	| 1         | { "first": "1" }               | false  |
	| 1         | true                           | false  |
	| 1         | false                          | false  |
	| 1         | "2018-11-13T20:20:39+00:00"    | false  |
	| 1         | "2018-11-13"                   | false  |
	| 1         | "P3Y6M4DT12H30M5S"             | false  |
	| 1         | "hello@endjin.com"             | false  |
	| 1         | "www.example.com"              | false  |
	| 1         | "http://foo.bar/?baz=qux#quux" | false  |
	| 1         | "eyAiaGVsbG8iOiAid29ybGQiIH0=" | false  |
	| 1         | "{ \\"first\\": \\"1\\" }"     | false  |
	| 1         | "192.168.0.1"                  | false  |
	| 1         | "0:0:0:0:0:ffff:c0a8:0001"     | false  |
	| 1         | <new object()>                 | false  |
	| 1         | null                           | false  |
	| 1         | <null>                         | false  |
	| 1         | <undefined>                    | false  |
	| null      | null                           | true   |
	| null      | <null>                         | true   |
	| null      | <undefined>                    | false  |